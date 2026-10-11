"""The evidence table and the gate in front of every request the client sends."""

import asyncio
import json

import pytest

from x20ctl import protocol as p
from x20ctl import validation as v
from x20ctl.client import X20, NotValidated
from x20ctl.scanning import fingerprint as fp

X20_RECORD = "10132009012352"
X20_DESCRIPTOR = "0a030303000f010002c000"


def info(record=X20_RECORD):
    return p.parse_device_info(bytes.fromhex(record))


def with_flags(mode, generation=2, version=(0x01, 0x23), vid_pid="10132009"):
    return p.parse_device_info(
        bytes.fromhex(vid_pid) + bytes(version) + bytes([generation << 5 | 0x10 | mode]))


def test_the_recorded_x20_is_validated_and_writes_what_was_proven():
    verdict = v.assess(info())
    assert verdict.status == "validated" and verdict.product == "EasySMX X20"
    for feature in ("remaps", "stick_curves", "trigger_curves", "vibration", "macros", "factory_reset"):
        assert verdict.may_write(feature), feature
    # the X20 reports neither, and neither write is proven
    assert not verdict.may_write("lighting") and not verdict.may_write("turbo")
    assert verdict.table()["macros"]["level"] == "readback"


def test_other_firmware_keeps_proven_features_another_mode_only_reads():
    newer = v.assess(with_flags(2, version=(0x01, 0x25)))
    assert newer.status == "known_product" and newer.may_write("remaps") and "1.25" in newer.reason
    other_mode = v.assess(with_flags(7))
    assert other_mode.status == "known_product_other_mode"
    assert other_mode.may_read() and not other_mode.may_write("remaps")


def test_an_unknown_controller_is_identified_only():
    verdict = v.assess(with_flags(2, vid_pid="10133001"))
    assert verdict.status == "unknown" and verdict.product is None
    assert not verdict.may_read()
    assert not any(verdict.may_write(f) for f in v.FEATURES)
    assert verdict.summary()["writable"] == []


def test_the_pro_flag_never_decides_anything():
    # the X20 itself sets the "Pro" gamepad flag (D9 = C0); identity alone decides
    caps = p.parse_capabilities(p.unwrap(bytes.fromhex(X20_DESCRIPTOR)))
    assert caps.nfc >> 6 & 1
    assert v.assess(info()).product == "EasySMX X20"


@pytest.mark.parametrize("opcode,payload,expected", [
    (p.Op.READ_VID_PID_VERSION, b"", ("identify", None)),
    (p.Op.HOST_MENU, b"\x00\x06", ("identify", None)),
    (p.Op.HOST_STICK, b"\x00", ("read", "stick_curves")),
    (p.Op.READ_3D, b"\x00", ("read", None)),
    (p.Op.WRITE_CHANGEKEY, b"\x01", ("write", "remaps")),
    (p.Op.WRITE_LIGHTING, b"\x01", ("write", "lighting")),
    (p.Op.RECOVER, b"\x03\xdf\xa9\x02", ("write", "factory_reset")),
    (p.Op.SET_MODE, p.POWER_QUERY_PAYLOAD, ("read", "battery")),
    (p.Op.NORMAL_MODE, p.NORMAL_MODE_PAYLOAD, ("read", "battery")),
    (p.Op.SET_MODE, p.CALIBRATE_PAYLOAD, ("write", "calibrate")),
    (p.Op.SET_MODE, b"\x02\x0e", ("write", None)),   # a page/mode switch: never proven
    (0x77, b"", ("write", None)),
])
def test_every_request_is_classified(opcode, payload, expected):
    assert v.request_feature(opcode, payload) == expected


class Wire:
    def __init__(self):
        self.sent = []
        self.is_connected = True

    async def write_gatt_char(self, _uuid, packet, response=True):
        self.sent.append(p.unscramble(bytes(packet))[0])


def pad_with(identity, **kwargs):
    pad = X20("test", timeout=0.01, **kwargs)
    pad._client = Wire()
    pad._info = identity
    return pad


def send(pad, opcode, payload=b""):
    return asyncio.run(pad._send(p.build(opcode, payload, serial=1), 1, expect_reply=False))


def test_nothing_unproven_reaches_an_unknown_controller():
    pad = pad_with(with_flags(2, vid_pid="10133001"))
    send(pad, p.Op.READ_VID_PID_VERSION)
    send(pad, p.Op.HOST_MENU, b"\x00\x01")
    for opcode, payload in ((p.Op.HOST_STICK, b"\x00"), (p.Op.WRITE_MOTOR, b"\x08"),
                            (p.Op.RECOVER, b"\x03\xdf\xa9\x02")):
        with pytest.raises(NotValidated):
            send(pad, opcode, payload)
    with pytest.raises(NotValidated):
        asyncio.run(pad.battery())
    # only the two identification requests went out; battery's NORMAL_MODE did not
    assert pad._client.sent == [p.Op.READ_VID_PID_VERSION, p.Op.HOST_MENU]


def test_research_reads_never_open_writes():
    pad = pad_with(with_flags(2, vid_pid="10133001"), unvalidated_reads=True)
    send(pad, p.Op.HOST_STICK, b"\x00")
    with pytest.raises(NotValidated, match="stick curves"):
        send(pad, p.Op.WRITE_STICK, b"\x00")
    assert pad._client.sent == [p.Op.HOST_STICK]


def test_the_x20_still_takes_proven_writes_and_refuses_unproven_ones():
    pad = pad_with(info())
    send(pad, p.Op.WRITE_MOTOR, b"\x08")
    send(pad, p.Op.RECOVER, b"\x03\xdf\xa9\x02")
    with pytest.raises(NotValidated, match="lighting"):
        send(pad, p.Op.WRITE_LIGHTING, b"\x01")
    with pytest.raises(NotValidated, match="0x1D"):
        send(pad, p.Op.SET_MODE, b"\x02\x0e")
    assert pad._client.sent == [p.Op.WRITE_MOTOR, p.Op.RECOVER]


def test_descriptor_keeps_raw_bytes_and_names_what_nobody_has_traced():
    x20 = p.parse_capabilities(p.unwrap(bytes.fromhex(X20_DESCRIPTOR)))
    assert x20.raw == X20_DESCRIPTOR[2:]
    assert x20.unexplained_bits() == [(5, 0)]
    assert not x20.remap_select_list and x20.new_macro_engine
    longer = p.parse_capabilities(p.Body(declared=11, data=bytes.fromhex("030303800f030012c00105")))
    labels = [p.descriptor_bit_label(*bit) for bit in longer.unexplained_bits()]
    for label in ("D4.bit7 (turbo)", "D8.bit4 (engine)", "D10.bit0 (sensor)",
                  "D11.bit0 (extra)", "D11.bit2 (extra)"):
        assert label in labels, label
    assert longer.remap_select_list
    assert sum(bin(m).count("1") for m in p.DESCRIPTOR_KNOWN_BITS) == 30


def test_identity_record_is_kept():
    assert info().record == X20_RECORD


def test_a_fingerprint_matches_itself_and_puts_protocol_differences_first():
    ref = fp.X20_REFERENCE
    assert fp.compare(ref, json.loads(json.dumps(ref))) == []
    other = json.loads(json.dumps(ref))
    other["identity"].update(mode=7, effectiveGeneration=1)
    other["descriptor"]["raw"] = "030303000f000002c001"     # D6.bit0 cleared, D10.bit0 set
    other["lists"]["macroKeys"] = ref["lists"]["macroKeys"][:-2]
    diffs = fp.compare(ref, other)
    assert diffs[0]["field"] == "effectiveGeneration"
    areas = [d["area"] for d in diffs]
    assert areas == sorted(areas, key=["protocol", "identity", "features", "unexplained"].index)
    fields = {d["field"]: d for d in diffs}
    assert fields["macroKeys"]["meaning"].startswith("only in A: [93, 94]")
    assert fields["D6.bit0 (changekey)"]["area"] == "unexplained"
    assert fields["D10.bit0 (sensor)"]["area"] == "unexplained"
    assert "effectiveGeneration" in fp.render(diffs)


def test_a_capture_and_a_live_fingerprint_have_the_same_shape():
    profile = {"identity": fp.identity_of(info()), "descriptor": fp.X20_REFERENCE["descriptor"],
               "supportedSourceCodes": fp.X20_REFERENCE["lists"]["changekeySources"]}
    converted = fp.from_discovery(profile)
    assert set(converted) == set(fp.X20_REFERENCE) and set(converted["lists"]) == set(fp.LISTS)
    assert [d["field"] for d in fp.compare(fp.X20_REFERENCE, converted)] == [
        "turboKeys", "macroKeys", "macroLimits"]


def test_compare_tool_reads_the_reference_and_a_log(tmp_path, capsys):
    from tools.compare_controllers import main
    other = json.loads(json.dumps(fp.X20_REFERENCE))
    other["identity"]["version"] = "1.25"
    log = tmp_path / "unknown-controllers.json"
    log.write_text(json.dumps([{"name": "Xpert2", **other}]), encoding="utf-8")
    assert main(["x20", str(log)]) == 0
    assert "version: A=1.23  B=1.25" in capsys.readouterr().out


class UnknownPad:
    """A stand-in for a connected controller that is not in the evidence table."""

    _client = type("Link", (), {"is_connected": True})()

    def __init__(self, _address):
        self.identity = with_flags(2, vid_pid="10133001")
        self.caps = p.parse_capabilities(p.unwrap(bytes.fromhex(X20_DESCRIPTOR)))

    async def connect(self):
        pass

    async def disconnect(self):
        pass

    async def device_info(self):
        return self.identity

    async def capabilities(self):
        return self.caps

    async def assessment(self):
        return v.assess(self.identity)

    async def name(self):
        return "Xpert2"

    async def read_body(self, _op, payload):
        if payload[1] == p.MenuKind.TURBO_SUPPORT:
            return p.unwrap(bytes.fromhex("050c01880d84"))
        return None

    async def battery(self):
        raise AssertionError("an unknown controller is never asked for its battery")


def test_an_unknown_controller_connects_reads_nothing_and_is_logged_once(tmp_path):
    from x20ctl.desktop.service import DeviceService
    service = DeviceService(client_factory=UnknownPad, directory=tmp_path)
    service._found["pad"] = type("Found", (), {"name": "Xpert2"})()
    for _ in range(2):
        result = asyncio.run(service.dispatch("connect", {"address": "pad"}))
    assert result["assessment"]["status"] == "unknown"
    assert result["values"] == {} and result["editable"] == []
    assert "nothing will be changed" in result["warnings"][0]
    entries = json.loads((tmp_path / "unknown-controllers.json").read_text(encoding="utf-8"))
    assert len(entries) == 1
    assert entries[0]["identity"]["internalProductId"] == "3001"
    assert entries[0]["descriptor"]["unexplainedBits"] == ["D6.bit0 (changekey)"]
    assert entries[0]["lists"]["turboKeys"] == [1, 2, 3, 4, 5, 6, 7, 8, 13, 14, 15, 16]
    assert "address" not in json.dumps(entries)


def test_macro_limits_are_pairs_matched_to_slots_by_position():
    caps = p.parse_capabilities(p.unwrap(bytes.fromhex(X20_DESCRIPTOR)))
    # the recorded X20 reply: 42 for M1-M4, then zeros for the four slots it lacks
    limits = p.decode_macro_limits(bytes.fromhex("2a002a002a002a000000000000000000"))
    assert limits == [42, 42, 42, 42, 0, 0, 0, 0]
    assert caps.slot_limits(limits) == {"M1": 42, "M2": 42, "M3": 42, "M4": 42}
    assert p.decode_macro_limits(bytes([0x00, 0x01, 0xFF, 0xFF])) == [256, 65535]
    with pytest.raises(ValueError, match="whole pairs"):
        p.decode_macro_limits(bytes.fromhex("2a002a"))        # the app drops the odd byte; we refuse


def test_the_pad_is_asked_for_limits_only_when_its_descriptor_says_so():
    class Scripted(X20):
        def __init__(self, descriptor):
            super().__init__("test")
            self._caps = p.parse_capabilities(p.unwrap(bytes.fromhex(descriptor)))
            self.asked = []

        async def read_body(self, opcode, payload=b""):
            self.asked.append((int(opcode), bytes(payload)))
            return p.unwrap(bytes.fromhex("10" + "2a002a002a002a000000000000000000"))

    new = Scripted(X20_DESCRIPTOR)
    assert asyncio.run(new.macro_limits()) == {"M1": 42, "M2": 42, "M3": 42, "M4": 42}
    assert new.asked == [(int(p.Op.HOST_MENU), bytes([0, 6]))]
    old = Scripted("0a030303000f010001c000")          # D8 bit 1 clear
    assert asyncio.run(old.macro_limits()) is None and old.asked == []


def test_only_the_apps_three_address_prefixes_count_as_a_pad():
    from x20ctl.client import is_controller
    for address in ("98:B6:E9:00:00:01", "98:b6:ec:00:00:01", "98:B6:ED:00:00:01"):
        assert is_controller("", address), address
    for address in ("98:B6:E0:00:00:01", "98:B6:EF:00:00:01", "98:B6:C9:00:00:01"):
        assert not is_controller("", address), address
    assert is_controller("Xpert2", "11:22:33:44:55:66")


def test_a_capture_decodes_limits_into_the_same_shape_as_the_reference():
    from x20ctl.scanning.capability_discovery import discover
    from test_capability_discovery import frame
    # kind 6 as the X20 sends it: 16 declared bytes, 14 in the first packet, 2 in the next
    head = bytes([16]) + bytes.fromhex("2a002a002a002a0000000000000000")[:14]
    events = [frame(0x91, b"", 1, "write"), frame(0x91, bytes.fromhex("0710132009012352"), 1, "notify"),
              frame(p.Op.HOST_MENU, b"\x00", 2, "write"),
              frame(p.Op.HOST_MENU, bytes([10]) + bytes.fromhex(X20_DESCRIPTOR[2:]), 2, "notify"),
              frame(p.Op.HOST_MENU, b"\x00", 3, "write"), frame(p.Op.HOST_MENU, head, 3, "notify"),
              frame(p.Op.HOST_MENU, b"", 4, "write"), frame(p.Op.HOST_MENU, b"\x00\x00", 4, "notify")]
    profile = discover(events)["profiles"][0]
    assert profile["macroLimits"] == [42, 42, 42, 42, 0, 0, 0, 0]
    assert fp.from_discovery(profile)["lists"]["macroLimits"] == fp.X20_REFERENCE["lists"]["macroLimits"]
