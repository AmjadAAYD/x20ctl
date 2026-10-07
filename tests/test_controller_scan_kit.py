"""Evidence integrity, device association and read-only collection contracts."""
import asyncio
import hashlib
import json
import zipfile
from types import SimpleNamespace

import pytest


def test_inventory_reordering_does_not_create_a_new_device():
    from tools.controller_scan_kit.evidence import candidates
    a = {"_key": "A", "_parent": "pad1", "kind": "hid"}
    b = {"_key": "B", "_parent": "pad2", "kind": "hid"}
    assert candidates([a, b], [b, a]) == []


def test_selected_parent_excludes_an_unrelated_identical_vid_pid():
    from tools.controller_scan_kit.evidence import related
    rows = [{"_key": "a", "_parent": "one", "vid": 0x045E},
            {"_key": "b", "_parent": "two", "vid": 0x045E}]
    assert related(rows, rows[0]) == [rows[0]]


def test_export_drops_device_paths_addresses_and_serials():
    from tools.controller_scan_kit.evidence import public_device
    row = {"_key": "private", "_parent": "private", "path": "secret",
           "serial": "123456", "address": "AA:BB:CC:DD:EE:FF", "vid": 0x045E,
           "product": r"Pad C:\Users\Alice\data", "hardware_ids": [r"USB\VID_045E&PID_028E"]}
    result = public_device(row)
    text = json.dumps(result)
    assert "private" not in text and "123456" not in text and "AA:BB" not in text
    assert "Alice" not in text
    assert result["vid"] == "045E"


def test_packet_counter_alone_does_not_verify_button_mapping():
    from tools.controller_scan_kit.input_tests import summarize
    samples = [{"source": "xinput_state", "values": {"buttons": 0, "packet": n}} for n in (1, 2)]
    assert summarize("A", samples)["status"] == "no_change_observed"


def test_button_mapping_uses_actual_state_change_and_keeps_api_label():
    from tools.controller_scan_kit.input_tests import summarize
    samples = [{"source": "xinput_state", "values": {"buttons": n, "packet": 1}} for n in (0, 4096, 0)]
    result = summarize("A", samples)
    assert result["status"] == "change_observed"
    assert result["changed_fields"] == ["buttons"]
    assert result["source"] == "xinput_state"
    assert "hid_report" not in result


def test_zip_hashes_are_of_the_actual_files_and_corruption_is_rejected(tmp_path):
    from tools.controller_scan_kit.evidence import Report, validate_archive
    report = Report(tmp_path, "X10")
    report.write_json("device/session.json", {"status": "unavailable", "reason": "no controller"})
    path = report.package()
    info = validate_archive(path)
    assert info["claimedModel"] == "X10"
    with zipfile.ZipFile(path) as z:
        manifest = json.loads(z.read("manifest.json"))
        data = z.read("device/session.json")
        item = next(x for x in manifest["files"] if x["path"] == "device/session.json")
        assert item["sha256"] == hashlib.sha256(data).hexdigest()
    bad = tmp_path / "bad.zip"
    with zipfile.ZipFile(path) as z, zipfile.ZipFile(bad, "w") as out:
        for name in z.namelist():
            out.writestr(name, b"{}" if name == "device/session.json" else z.read(name))
    with pytest.raises(ValueError, match="hash|size"):
        validate_archive(bad)


@pytest.mark.parametrize("name", ["../escape.txt", "C:/escape", "a\\b", "payload.exe"])
def test_zip_rejects_untrusted_entry_names(tmp_path, name):
    from tools.controller_scan_kit.evidence import validate_archive
    path = tmp_path / "bad.zip"
    with zipfile.ZipFile(path, "w") as z:
        z.writestr("manifest.json", "{}")
        z.writestr(name, "bad")
    with pytest.raises(ValueError):
        validate_archive(path)


def test_zip_rejects_unmanifested_entries(tmp_path):
    from tools.controller_scan_kit.evidence import Report, validate_archive
    path = Report(tmp_path, "X20").package()
    with zipfile.ZipFile(path, "a") as z:
        z.writestr("docs/extra.txt", "not listed")
    with pytest.raises(ValueError, match="manifest|listed"):
        validate_archive(path)


def test_collection_cannot_silently_overwrite_evidence(tmp_path):
    from tools.controller_scan_kit.evidence import Report
    report = Report(tmp_path, "X20")
    report.write_json("device/a.json", {"first": True})
    with pytest.raises(FileExistsError):
        report.write_json("device/a.json", {"second": True})


def test_raw_hid_is_not_normalized_as_xinput():
    from tools.controller_scan_kit.input_tests import summarize
    result = summarize("M1", [{"source": "hid_input", "report_hex": "0000"},
                              {"source": "hid_input", "report_hex": "0001"}])
    assert result["source"] == "hid_input"
    assert result["changed_byte_offsets"] == [1]


def test_usb_configuration_parser_decodes_endpoints_without_guessing_ids():
    from tools.controller_scan_kit.usb import parse_configuration
    raw = bytes.fromhex("09021900010100803209040000010300000007058103400001")
    result = parse_configuration(raw)
    assert result["interfaces"][0]["class"] == 3
    assert result["interfaces"][0]["endpoints"][0] == {
        "address": "81", "direction": "IN", "transfer_type": "interrupt",
        "max_packet_size": 64, "interval": 1}
    assert "report_ids" not in result


@pytest.mark.parametrize("raw", [b"\x00\x02", b"\x09\x02\x20", b"\x02\x04"])
def test_usb_configuration_parser_rejects_truncation(raw):
    from tools.controller_scan_kit.usb import parse_configuration
    with pytest.raises(ValueError):
        parse_configuration(raw)


def test_ble_reads_only_the_standard_service_characteristic_pair():
    from tools.controller_scan_kit.ble import inspect_client
    # Hardware boundary double: any forbidden request raises; resulting values
    # and omitted vendor/serial payloads establish the collector's contract.
    battery = SimpleNamespace(uuid="00002a19-0000-1000-8000-00805f9b34fb",
                              properties=["read"], descriptors=[])
    vendor = SimpleNamespace(uuid=battery.uuid, properties=["read", "write"], descriptors=[])
    serial = SimpleNamespace(uuid="00002a25-0000-1000-8000-00805f9b34fb",
                             properties=["read"], descriptors=[])
    class Client:
        services = [SimpleNamespace(uuid="vendor-service", characteristics=[vendor]),
                    SimpleNamespace(uuid="0000180f-0000-1000-8000-00805f9b34fb", characteristics=[battery]),
                    SimpleNamespace(uuid="0000180a-0000-1000-8000-00805f9b34fb", characteristics=[serial])]
        async def read_gatt_char(self, char):
            assert char is battery, "forbidden standard/vendor collision"
            return bytes([74])
        def __getattr__(self, name):
            raise AssertionError(f"forbidden operation: {name}")
    result = asyncio.run(inspect_client(Client()))
    assert result["standard_values"]["battery_level"] == 74
    assert "serial_number" not in result["standard_values"]


def test_timed_input_recording_preserves_disconnection(tmp_path):
    from tools.controller_scan_kit.input_tests import record_action
    from tools.controller_scan_kit.evidence import Report
    class Reader:
        def __init__(self): self.n = 0
        def read(self, timeout_ms):
            self.n += 1
            if self.n > 1: raise OSError("private device address")
            return {"source": "xinput_state", "values": {"buttons": 0, "packet": 1}}
    report = Report(tmp_path, "X20")
    result = record_action(report, "input/test.jsonl", "A", Reader(), duration=0.2)
    assert result["status"] == "failed"
    content = (report.directory / "input/test.jsonl").read_text()
    assert "private device address" not in content
    assert "xinput_state" in content


def test_usb_query_uses_full_initialized_buffers_and_only_standard_configuration_request():
    import ctypes as c
    from tools.controller_scan_kit.usb import descriptor
    config = bytes.fromhex("090209000001008032")
    class Kernel:
        def DeviceIoControl(self, handle, code, source, source_size, output, output_size, actual, overlap):
            assert handle == 7 and code == (0x22 << 16) | (260 << 2)
            assert source_size == output_size == 21 and overlap is None
            assert source.raw[:12] == bytes.fromhex("010000008006000200000900")
            c.memmove(c.addressof(output) + 12, config, len(config))
            actual._obj.value = 21
            return True
    assert descriptor(SimpleNamespace(kernel=Kernel()), 7, 1, 0, 9) == config


def test_xinput_summary_records_button_bits_and_axis_range():
    from tools.controller_scan_kit.input_tests import summarize
    result = summarize("A", [{"source": "xinput_state", "values": {"buttons": b, "lt": t}}
                              for b, t in [(0, 0), (4096, 255), (0, 0)]])
    assert result["changed_button_bits"] == ["1000"]
    assert result["observed_ranges"]["lt"] == {"min": 0, "max": 255}


def test_validator_rejects_invented_input_sources(tmp_path):
    from tools.controller_scan_kit.evidence import Report
    report = Report(tmp_path, "X20")
    report.write_text("input/a.jsonl", json.dumps({"source": "claimed_hid", "timestamp": "now",
        "action": "A", "elapsed_ms": 0, "report_hex": "00"}) + "\n")
    with pytest.raises(ValueError, match="input"):
        report.package()


def test_scripted_wizard_exports_only_correlated_device_and_preserves_claim(tmp_path):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    class Backend:
        def __init__(self): self.calls = 0
        def inventory(self):
            self.calls += 1
            unrelated = {"_key": "other-private", "_parent": "other", "product": "UNRELATED-PAD", "kind": "hid"}
            target = {"_key": "target-serial", "_parent": "target-private", "_path": "secret-path",
                      "kind": "hid", "product": "Fixture pad", "vid": 0x1234, "pid": 0x5678,
                      "usage_page": 1, "usage": 5, "input_len": 64}
            return {"devices": [unrelated] + ([target] if self.calls == 2 else []),
                    "xinput_slots": [0] if self.calls == 2 else []}
        def usb(self, selected):
            assert selected["_key"] == "target-serial"
            return {"metadata": {"status": "unavailable", "reason": "synthetic"}, "binaries": {}}
        def details(self, rows):
            assert len(rows) == 1 and rows[0]["_key"] == "target-serial"
            return rows
        def capture(self, report, selected, source, slot, filename, action, duration):
            assert source == "xinput_state" and slot == 0 and action == "neutral"
            report.write_text(filename, json.dumps({"source": source, "timestamp": "2026-10-01T00:00:00.000Z",
                "elapsed_ms": 0, "action": action, "values": {"buttons": 0, "packet": 1}}) + "\n")
            return {"action": action, "source": source, "status": "no_change_observed", "file": filename}
    answers = iter(["y", "3", "X10", "1", "XInput", "", "", "1", "y", "1", "y", "",
                    "", "q", "n", "n", "", "y", "y"])
    archive = wizard(tmp_path, backend=Backend(), ask=lambda _: next(answers), say=lambda _: None)
    manifest = validate_archive(archive)
    assert manifest["claimedModel"] == "X10" and manifest["modelDetected"] is False
    assert manifest["privacy"]["userReviewed"] is True
    with zipfile.ZipFile(archive) as z:
        exported = "\n".join(z.read(n).decode("utf-8") for n in z.namelist())
        for private in ("UNRELATED-PAD", "target-serial", "target-private", "secret-path"):
            assert private not in exported
    assert manifest["sessions"][0]["input_source"] == "xinput_state"


def test_cancelled_wizard_retains_a_marked_partial_report(tmp_path):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    answers = iter(["y", "1", "1", "XInput"])
    def ask(_):
        try: return next(answers)
        except StopIteration: raise KeyboardInterrupt
    archive = wizard(tmp_path, backend=SimpleNamespace(), ask=ask, say=lambda _: None)
    manifest = validate_archive(archive)
    assert manifest["collectionStoppedEarly"] is True
    assert manifest["sessions"][0]["status"] == "cancelled"
    assert manifest["privacy"]["userReviewed"] is False


def test_beginner_run_needs_no_mode_input_source_or_bluetooth_knowledge(tmp_path):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    class Backend:
        calls = 0
        def inventory(self):
            self.calls += 1
            device = {"_key": "private-instance", "_parent": "private-parent", "kind": "usb",
                      "description": "Fixture controller", "vid": 0x1234, "pid": 0x5678}
            return {"devices": [device] if self.calls == 2 else [], "xinput_slots": []}
        def details(self, rows): return rows
        def usb(self, selected): return {"metadata": {"status": "unavailable"}, "binaries": {}}
        def __getattr__(self, name): raise AssertionError("Beginner flow must not call " + name)
    answers = iter(["y", "3", "X05 Pro", "1", "", "", "y", "y"])
    prompts = []
    def ask(prompt):
        prompts.append(prompt)
        return next(answers)
    path = wizard(tmp_path, backend=Backend(), ask=ask, say=lambda _: None, beginner=True)
    manifest = validate_archive(path)
    assert manifest["claimedModel"] == "X05 Pro"
    session = manifest["sessions"][0]
    assert session["mode"] == "unknown" and session["input_status"] == "not_requested_first_run"
    assert session["ble_status"] == "not_requested_first_run" and session["tests"] == []
    assert manifest["privacy"]["userReviewed"] is True
    assert not any("mode (" in p or "input source" in p or "BLE" in p for p in prompts)
    with zipfile.ZipFile(path) as z:
        review = z.read("BEFORE_YOU_SEND.txt").decode()
        assert "X05 Pro" in review and "Fixture controller" in review
        assert "private-instance" not in review and "private-parent" not in review


@pytest.mark.parametrize("include_raw", [False, True])
def test_full_guided_run_retains_intake_both_input_paths_and_ble(tmp_path, include_raw):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    class Backend:
        count = 0
        recorded = []
        ble_count = 0
        def inventory(self):
            self.count += 1
            device = {"_key": "private-path", "_parent": "private-parent", "kind": "hid",
                      "description": "Fixture pad", "vid": 0x1234, "pid": 0x5678,
                      "usage_page": 1, "usage": 5, "input_len": 64}
            return {"devices": [device] if self.count % 2 == 0 else [],
                    "xinput_slots": [0] if self.count % 2 == 0 else []}
        def details(self, rows): return rows
        def usb(self, selected): return {"metadata": {"status": "unavailable"}, "binaries": {}}
        def capture(self, report, selected, source, slot, filename, action, duration):
            self.recorded.append(source)
            data = {"timestamp": "2026-10-01T00:00:00.000Z", "elapsed_ms": 0, "action": action, "source": source}
            data.update({"values": {"buttons": 0, "packet": 1}} if source == "xinput_state" else {"report_hex": "0001"})
            report.write_text(filename, json.dumps(data) + "\n")
            return {"action": action, "status": "no_change_observed", "source": source, "file": filename}
        def ble_scan(self):
            self.ble_count += 1
            return [] if self.ble_count == 1 else [{"_key": "AA:BB:CC:DD:EE:FF", "name": "Fixture BLE"}]
        def ble_inspect(self, selected):
            assert selected["_key"] == "AA:BB:CC:DD:EE:FF"
            return {"status": "observed", "services": [], "standard_values": {"battery_level": 60}}
    backend = Backend()
    # Intake; one cable session; normal readings and optional raw readings;
    # actual BLE off/on confirmation; no second session; observed-feature notes.
    answers = ["y", "1", "rev A", "Official app", "1.2", "unknown", "RGB, remapping",
               "1", "", "", "", "y", "y", "y" if include_raw else "n", "", "", "q"]
    if include_raw: answers += ["", "q"]
    answers += ["y", "y", "", "1", "y", "n", "n", "Vibration worked in my game", "y"]
    iterator = iter(answers)
    archive = wizard(tmp_path, backend=backend, ask=lambda _: next(iterator), say=lambda _: None, guided=True)
    manifest = validate_archive(archive)
    expected = ["xinput_state", "hid_input"] if include_raw else ["xinput_state"]
    assert backend.recorded == expected
    session = manifest["sessions"][0]
    assert session["mode"] == "unknown" and session["ble_status"] == "observed"
    assert {r["source"] for r in session["tests"]} == set(expected)
    with zipfile.ZipFile(archive) as z:
        intake = json.loads(z.read("intake.json"))
        assert intake["appName"] == "Official app" and intake["appSettingsObserved"] == "RGB, remapping"
        assert intake["evidenceType"] == "owner_reported"
        review = z.read("BEFORE_YOU_SEND.txt").decode()
        assert "No button recording" not in review
        exported = b"\n".join(z.read(n) for n in z.namelist())
        assert b"private-path" not in exported and b"AA:BB:CC:DD:EE:FF" not in exported


def test_executable_defaults_to_full_guided_collection_and_keeps_basic_option(monkeypatch, tmp_path):
    from tools.controller_scan_kit import cli
    calls = []
    monkeypatch.setattr(cli, "wizard", lambda output, **options: calls.append(options))
    assert cli.main(["--output", str(tmp_path)]) == 0
    assert calls[-1] == {"beginner": False, "guided": True}
    assert cli.main(["--basic", "--output", str(tmp_path)]) == 0
    assert calls[-1] == {"beginner": True, "guided": False}
    assert cli.main(["--app-capture", "--output", str(tmp_path)]) == 0
    assert calls[-1] == {"guided": True, "app_only": True}


def usb_trace():
    import struct
    return (struct.pack("<IHHIIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, 249)
            + struct.pack("<IIII", 1, 0, 2, 2) + b"\x00\x01")


def bluetooth_trace():
    import struct
    return b"btsnoop\0" + struct.pack(">II", 1, 1002) + struct.pack(">IIIIQ", 2, 2, 0, 0, 1) + b"\x04\x00"


def test_capture_import_extracts_only_bluetooth_not_private_bug_report(tmp_path):
    from tools.controller_scan_kit.app_capture import read_trace
    path = tmp_path / "phone.zip"
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("bugreport-phone.txt", "PRIVATE ACCOUNT AND OTHER LOGS")
        archive.writestr("FS/data/misc/bluetooth/logs/btsnoop_hci.log", bluetooth_trace())
    data, info = read_trace(path, "android")
    assert data == bluetooth_trace() and info["packets"] == 1
    assert len(list(tmp_path.iterdir())) == 1  # No ZIP member was extracted to disk.
    assert path.read_bytes().startswith(b"PK")


@pytest.mark.parametrize("route,data", [("windows", b"MZ" + b"0" * 40),
    ("windows", usb_trace()[:-1]), ("android", bluetooth_trace()[:-1]),
    ("android", b"btsnoop\0" + b"\0" * 8), ("windows", bluetooth_trace())])
def test_capture_import_rejects_wrong_empty_or_truncated_formats(route, data):
    from tools.controller_scan_kit.app_capture import trace_info
    with pytest.raises(ValueError):
        trace_info(data, route)


def test_capture_import_rejects_network_pcap_and_mixed_pcapng_interfaces():
    import struct
    from tools.controller_scan_kit.app_capture import trace_info
    data = bytearray(usb_trace())
    struct.pack_into("<I", data, 20, 1)
    with pytest.raises(ValueError, match="network"):
        trace_info(bytes(data), "windows")
    section = struct.pack("<IIIHHqI", 0x0A0D0D0A, 28, 0x1A2B3C4D, 1, 0, -1, 28)
    interface = struct.pack("<IIHHII", 1, 20, 249, 0, 65535, 20)
    packet = struct.pack("<IIIIIII", 6, 36, 0, 0, 0, 2, 2) + b"\x00\x01\0\0" + struct.pack("<I", 36)
    assert trace_info(section + interface + packet, "windows")["packets"] == 1
    ethernet = struct.pack("<IIHHII", 1, 20, 1, 0, 65535, 20)
    with pytest.raises(ValueError, match="network"):
        trace_info(section + interface + packet + ethernet, "windows")


@pytest.mark.parametrize("trace_count", [0, 2])
def test_android_bug_report_requires_one_unambiguous_full_log(tmp_path, trace_count):
    from tools.controller_scan_kit.app_capture import read_trace
    path = tmp_path / "phone.zip"
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("bugreport.txt", "Diagnostic text only")
        for index in range(trace_count):
            archive.writestr(f"logs{index}/btsnoop_hci.log", bluetooth_trace())
    with pytest.raises(ValueError, match="single"):
        read_trace(path, "android")


def test_app_only_wizard_attaches_trace_and_timeline_with_honest_privacy(tmp_path):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    capture = tmp_path / "private-capture.pcap"
    capture.write_bytes(usb_trace())
    answers = iter(["y", "3", "X10", "y", "2", "Working app", "1.2", "vibration",
                    "2", "3", "y", "y", "y", "4.0", "", "", "", "", "stronger", "", "", "y", "",
                    str(capture), "y", "", "y"])
    output = []
    archive = wizard(tmp_path / "results", backend=SimpleNamespace(),
                     ask=lambda _: next(answers), say=output.append, guided=True, app_only=True)
    manifest = validate_archive(archive)
    assert manifest["sessions"] == []
    experiment = manifest["appCapture"]
    assert experiment["status"] == "trace_attached_for_review" and experiment["restored"] is True
    assert experiment["commandsVerified"] is False
    assert experiment["trace"]["packets"] == 1
    assert manifest["privacy"]["serialsCollected"] is None
    assert manifest["privacy"]["addressesExported"] is None
    assert manifest["privacy"]["userReviewed"] is True
    with zipfile.ZipFile(archive) as zipped:
        assert zipped.read("app-capture/setting-change.pcap") == usb_trace()
        timeline = json.loads(zipped.read("app-capture/experiment.json"))
        assert len(timeline["events"]) == 14
        review = zipped.read("BEFORE_YOU_SEND.txt").decode()
        assert "RAW APP TRACE INCLUDED" in review
        exported = b"\n".join(zipped.read(n) for n in zipped.namelist())
        assert str(capture).encode() not in exported and b"private-capture" not in exported


def test_cancelled_app_experiment_retains_timeline_and_cleanup_instruction(tmp_path):
    from tools.controller_scan_kit.app_capture import collect_app_capture
    from tools.controller_scan_kit.evidence import Report, validate_archive
    report = Report(tmp_path, "X10")
    answers = iter(["y", "2", "Working app", "", "vibration", "2", "3", "y", "y", "y", "", "", "", ""])
    def ask(_):
        try: return next(answers)
        except StopIteration: raise KeyboardInterrupt
    output = []
    collect_app_capture(report, ask, output.append)
    assert report.manifest["appCapture"]["status"] == "cancelled"
    assert report.manifest["collectionStoppedEarly"] is True
    assert any("Restore the original" in line for line in output)
    manifest = validate_archive(report.package())
    assert manifest["appCapture"]["events"][-1]["event"] == "change_prompt"


def test_trace_without_explicit_privacy_provenance_is_rejected(tmp_path):
    from tools.controller_scan_kit.evidence import Report
    report = Report(tmp_path, "X10")
    report.write_bytes("app-capture/setting-change.pcap", usb_trace())
    with pytest.raises(ValueError, match="privacy"):
        report.package()


def test_cancellation_at_app_intro_still_creates_marked_partial_zip(tmp_path):
    from tools.controller_scan_kit.cli import wizard
    from tools.controller_scan_kit.evidence import validate_archive
    answers = iter(["y", "1"])
    def ask(_):
        try: return next(answers)
        except StopIteration: raise EOFError
    archive = wizard(tmp_path, backend=SimpleNamespace(), ask=ask, say=lambda _: None, guided=True, app_only=True)
    manifest = validate_archive(archive)
    assert manifest["collectionStoppedEarly"] is True
    assert manifest["appCapture"]["status"] == "cancelled"
    assert manifest["privacy"]["userReviewed"] is False
