import asyncio
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import stat
import struct
import sys
import tempfile
from types import SimpleNamespace as NS
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))
from controller_scan_kit import cli, ble, evidence, usb
from controller_scan_kit.backend import Files
from controller_scan_kit.app_capture import trace_info, read_trace
from controller_scan_kit.windows import HidReader


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_report_hashes_and_validation(self):
        report = evidence.Report(self.root, "X20 Pro")
        report.write_json("device/info.json", {"vid": "1234"})
        manifest = evidence.validate_archive(report.package())
        self.assertFalse(manifest["modelDetected"])
        self.assertEqual(len(manifest["files"]), 2)
        self.assertEqual(manifest["claimedModel"], "X20 Pro")

    def test_tampered_content_rejected(self):
        archive = evidence.Report(self.root, "X20").package()
        with zipfile.ZipFile(archive) as z:
            data = {n: z.read(n) for n in z.namelist()}
        data["RESULTS_README.txt"] += b"tampered"
        with zipfile.ZipFile(archive, "w") as z:
            for n, b in data.items(): z.writestr(n, b)
        with self.assertRaisesRegex(ValueError, "size mismatch"):
            evidence.validate_archive(archive)

    def test_unsafe_names(self):
        for n in ("../info.txt", "a/../info.txt", "/info.txt", "C:/info.txt", "a\\b.txt",
                  "a//b.txt", "./info.txt", "evil.exe", "script.py", "a/info.json:stream"):
            with self.subTest(n=n), self.assertRaises(ValueError): evidence.safe_name(n)

    def test_worker_path_containment(self):
        with self.assertRaises(ValueError): Files(self.root).path("../leak.txt")

    def test_symlink_rejected(self):
        archive = self.root / "link.zip"
        info = zipfile.ZipInfo("link.txt")
        info.create_system = 3
        info.external_attr = (stat.S_IFLNK | 0o777) << 16
        with zipfile.ZipFile(archive, "w") as z: z.writestr(info, "target")
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            evidence.validate_archive(archive)

    def test_case_duplicate_rejected(self):
        archive = self.root / "duplicate.zip"
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("info.txt", "one")
            z.writestr("INFO.txt", "two")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            evidence.validate_archive(archive)

    def test_size_and_file_count_limits(self):
        report = evidence.Report(self.root, "X20")
        with patch.object(evidence, "MAX_FILE", 4), self.assertRaises(ValueError):
            report.write_text("large.txt", "12345")
        archive = report.package()
        with patch.object(evidence, "MAX_FILES", 1), self.assertRaisesRegex(ValueError, "entry count"):
            evidence.validate_archive(archive)
        with patch.object(evidence, "MAX_TOTAL", 4), self.assertRaises(ValueError):
            evidence.validate_archive(archive)

    def test_nested_executable_rejected(self):
        report = evidence.Report(self.root, "X20")
        report.write_bytes("device/data.bin", b"MZfake executable")
        with self.assertRaisesRegex(ValueError, "executable"):
            report.package()

    def test_metadata_allowlist_and_redaction(self):
        row = {"vid": 0x1234, "pid": 0x5678, "serial": "SECRET", "_path": "SECRET",
               "_instance": "SECRET", "_key": "SECRET", "_parent": "SECRET",
               "description": "Controller AA:BB:CC:DD:EE:FF", "product": "X20"}
        out = evidence.public_device(row)
        self.assertEqual(out["vid"], "1234")
        self.assertEqual(out["product"], "X20")
        self.assertNotIn("SECRET", json.dumps(out))
        self.assertNotIn("AA:BB", json.dumps(out))
        for value in (r"C:\Users\Amjad\notes", r"\\server\Amjad\notes",
                      r"USB\VID_1234&PID_5678\UNIQUE_SERIAL"):
            self.assertNotIn("Amjad", evidence.clean_text(value))
            self.assertNotIn("UNIQUE_SERIAL", evidence.clean_text(value))

    def test_raw_hid_requires_privacy_flags(self):
        report = evidence.Report(self.root, "X20")
        record = {"timestamp": evidence.utc_now(), "action": "a", "source": "hid_input",
                  "elapsed_ms": 0, "report_hex": "0102", "report_length": 2}
        report.write_text("input/a.jsonl", json.dumps(record) + "\n")
        with self.assertRaisesRegex(ValueError, "privacy flags"): report.package()

    def test_raw_hid_with_explicit_flags_valid(self):
        report = evidence.Report(self.root, "X20")
        report.manifest["privacy"].update(rawHidInputIncluded=True, rawHidInputMayContainIdentifiers=True,
            rawHidInputAutomaticallyAnonymized=False, serialsCollected=None, addressesExported=None)
        report.write_text("input/a.jsonl", json.dumps({"timestamp": evidence.utc_now(), "action": "a",
            "source": "hid_input", "elapsed_ms": 0, "report_hex": "01", "report_length": 1}) + "\n")
        evidence.validate_archive(report.package())

    def test_input_schema_bounds(self):
        record = {"timestamp": evidence.utc_now(), "action": "a", "source": "xinput_state",
                  "elapsed_ms": 0, "values": {"buttons": 1, "slot": 0}}
        evidence.validate_input(record)
        for bad in (float("nan"), -1, True):
            with self.assertRaises(ValueError): evidence.validate_input({**record, "elapsed_ms": bad})
        with self.assertRaises(ValueError):
            evidence.validate_input({**record, "values": {"buttons": 65536}})

    def test_cancelled_guided_does_not_package(self):
        report = evidence.Report(self.root, "X20")
        with patch.object(report, "package", side_effect=AssertionError("packaged without review")):
            result = cli.finish_beginner(report, [], True, lambda _: "", lambda _: None, full=True)
        self.assertEqual(result, report.directory)
        self.assertFalse(list(self.root.glob("*.zip")))

    def test_declined_review_does_not_package(self):
        report = evidence.Report(self.root, "X20")
        result = cli.finish_beginner(report, [], False, lambda _: "n", lambda _: None)
        self.assertEqual(result, report.directory)
        self.assertFalse(list(self.root.glob("*.zip")))

    def test_guided_does_not_offer_raw_app_capture(self):
        report = evidence.Report(self.root, "X20")
        with patch("controller_scan_kit.app_capture.collect_app_capture", side_effect=AssertionError("unexpected")):
            cli.finish_beginner(report, [], False, lambda _: "n", lambda _: None, full=True)

    def test_cancelled_advanced_does_not_package(self):
        responses = iter(["y", "1", "1", "PC"])
        def ask(_):
            try: return next(responses)
            except StopIteration: raise KeyboardInterrupt
        result = cli.wizard(self.root, backend=NS(), ask=ask, say=lambda _: None)
        self.assertTrue(result.is_dir())
        self.assertFalse(list(self.root.glob("*.zip")))

    def test_hid_opt_in_declined_no_capture(self):
        report = evidence.Report(self.root, "X20")
        session = {"id": "session01", "tests": []}
        row = {"kind": "hid", "usage_page": 1, "usage": 5, "input_len": 64}
        backend = NS(capture=lambda *a: self.fail("capture called without opt-in"))
        cli.record_inputs(report, session, [row], row, {"xinput_slots": []}, {"xinput_slots": []},
                          backend, lambda _: "n", lambda _: None, True)
        self.assertEqual(session["input_status"], "skipped_or_unavailable")

    def test_advanced_declined_privacy_review_does_not_package(self):
        answers = iter(["y", "1", "", "", "y", "n"])
        result = cli.wizard(self.root, backend=NS(), ask=lambda _: next(answers), say=lambda _: None)
        self.assertTrue(result.is_dir())
        self.assertFalse(list(self.root.glob("*.zip")))

    def test_hid_rejects_keyboard_and_mouse(self):
        for usage in (2, 6):
            with self.assertRaises(ValueError):
                HidReader(NS(), {"usage_page": 1, "usage": usage, "input_len": 64})


class HardwareFreeTests(unittest.TestCase):
    def test_explanations_do_not_construct_backend_or_hardware(self):
        for option in ("--capabilities", "--privacy", "--dry-run"):
            with self.subTest(option=option), patch.object(cli, "Backend", side_effect=AssertionError("hardware")), \
                 patch.object(cli, "wizard", side_effect=AssertionError("wizard")), \
                 redirect_stdout(io.StringIO()) as out:
                self.assertEqual(cli.main([option]), 0)
                self.assertTrue(out.getvalue())

    def test_conflicting_modes_rejected(self):
        with self.assertRaises(SystemExit), redirect_stdout(io.StringIO()):
            cli.main(["--dry-run", "--worker"])

    def test_self_check_is_hardware_free(self):
        with patch("controller_scan_kit.windows.Native", side_effect=AssertionError("hardware")), \
             patch("controller_scan_kit.windows.XInput", side_effect=AssertionError("hardware")):
            result = cli.self_check()
        self.assertEqual(result["status"], "passed")
        self.assertFalse(result["hardware_access"])


class UsbAndBleTests(unittest.TestCase):
    def test_usb_parser(self):
        raw = bytes.fromhex("09021900010100803209040000010300000007058103400001")
        endpoint = usb.parse_configuration(raw)["interfaces"][0]["endpoints"][0]
        self.assertEqual(endpoint["direction"], "IN")
        self.assertEqual(endpoint["max_packet_size"], 64)
        for bad in (b"", raw[:-1], raw[:9] + b"\0\0" + raw[11:]):
            with self.assertRaises(ValueError): usb.parse_configuration(bad)

    def test_usb_requests_only_standard_configuration(self):
        with patch.object(usb, "query", return_value=b"\0" * 12 + b"\x09\x02" * 5) as q:
            usb.descriptor(NS(), 1, 2, 0, 9)
        request = q.call_args.args[3]
        self.assertEqual(struct.unpack("<IBBHHH", request), (2, 0x80, 6, 0x0200, 0, 9))
        with self.assertRaises(ValueError): usb.query(NS(), 1, 999, b"", 10)

    def test_ble_only_allowlisted_reads(self):
        chars = [NS(uuid=u, properties=["read"], descriptors=[]) for u in
                 ("00002a19-0000-1000-8000-00805f9b34fb", "00002a25-0000-1000-8000-00805f9b34fb", "vendor")]
        calls = []
        async def read(char): calls.append(char.uuid); return b"\x32"
        client = NS(services=[NS(uuid=ble.BATTERY, characteristics=chars)], read_gatt_char=read)
        result = asyncio.run(ble.inspect_client(client))
        self.assertEqual(calls, [chars[0].uuid])
        self.assertEqual(result["standard_values"], {"battery_level": 50})
        self.assertFalse(any("2a25" in c for _, c in ble.STANDARD))

    def test_raw_capture_rejects_network_and_truncated_packets(self):
        packet = struct.pack("<IIII", 0, 0, 1, 1) + b"x"
        def pcap(link): return struct.pack("<IHHIIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, link) + packet
        self.assertEqual(trace_info(pcap(249), "windows")["packets"], 1)
        with self.assertRaises(ValueError): trace_info(pcap(1), "windows")
        with self.assertRaises(ValueError): trace_info(pcap(249)[:-1], "windows")

    def test_android_zip_only_imports_one_bounded_log(self):
        log = b"btsnoop\0" + struct.pack(">II", 1, 1002) + struct.pack(">IIIIQ", 1, 1, 0, 0, 0) + b"x"
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "bugreport.zip"
            with zipfile.ZipFile(path, "w") as z:
                z.writestr("private/person.txt", "never imported")
                z.writestr("logs/btsnoop_hci.log", log)
            data, info = read_trace(path, "android")
            self.assertEqual(data, log)
            self.assertEqual(info["packets"], 1)
            self.assertEqual(list(Path(tmp).iterdir()), [path])


if __name__ == "__main__": unittest.main()
