"""Controller state: reading every record whole, and comparing two reads.

No hardware. The pad is scripted from records shaped like a real X20's, split
into packets the way the pad splits them.
"""

from __future__ import annotations

import asyncio
import json

import pytest

from x20ctl import cli
from x20ctl import protocol as p
from x20ctl import state
from x20ctl.client import X20

X20_IDENTITY = bytes.fromhex("0710132009012352")
X20_DESCRIPTOR = bytes.fromhex("030303000f010002c000")   # as a real X20 reports it
STICK = bytes.fromhex("0000528ce5eb00" "0000528ce5eb00")
MOTOR = bytes.fromhex("8080ffff" "c0d40100")         # 50%, 50%, then 10 minutes of idle
CHANGEKEY = bytes(14)


def _packet(payload: bytes, serial: int = 1) -> p.Packet:
    opcode, length, nonce = int(p.Op.RESPONSE), len(payload) + 5, 0
    crc = p.crc8(bytes([opcode, length, serial, nonce]) + payload)
    return p.Packet(opcode=opcode, length=length, serial=serial, nonce=nonce, payload=payload, crc=crc)


def _chunks(record: bytes) -> list[bytes]:
    """[size] + the first 14 bytes, then 15 bytes per packet, as the pad sends."""
    out = [bytes([len(record)]) + record[:14]]
    rest = record[14:]
    while rest:
        out.append(rest[:15])
        rest = rest[15:]
    return out


class Pad(X20):
    def __init__(self, records: dict, identity: bytes = X20_IDENTITY, descriptor: bytes = X20_DESCRIPTOR):
        super().__init__("00:00:00:00:00:00")
        self.records = records
        self.identity = identity
        self.descriptor = descriptor
        self.asked: list[tuple[int, bytes]] = []

    async def query(self, opcode, payload: bytes = b""):
        opcode, payload = int(opcode), bytes(payload)
        self.asked.append((opcode, payload))
        if opcode == p.Op.READ_VID_PID_VERSION:
            return _packet(self.identity)
        if opcode == p.Op.HOST_MENU:
            record = self.descriptor if payload[1] == p.MenuKind.MENU else bytes([2, 0x01, 0x02])
            parts = _chunks(record)
            return _packet(parts[payload[0]]) if payload[0] < len(parts) else None
        key = (opcode, payload[1]) if opcode == p.Op.HOST_MACRO else opcode
        if key not in self.records:
            return None
        parts = _chunks(self.records[key])
        return _packet(parts[payload[0]]) if payload[0] < len(parts) else None


def _records() -> dict:
    out = {int(p.Op.HOST_STICK): STICK, int(p.Op.HOST_TRIGGER): STICK, int(p.Op.HOST_MOTOR): MOTOR,
           int(p.Op.HOST_CHANGEKEY): CHANGEKEY,
           (int(p.Op.HOST_MACRO), 0): b"", (int(p.Op.HOST_MACRO), 1): bytes(range(1, 33)),
           (int(p.Op.HOST_MACRO), 2): b"", (int(p.Op.HOST_MACRO), 3): b"",
           (int(p.Op.HOST_MACRO), 4): b"", (int(p.Op.HOST_MACRO), 5): b""}
    return out


def _capture(pad: Pad, label: str = "before") -> state.State:
    return asyncio.run(state.capture(pad, label))


def test_every_exposed_record_is_read_whole():
    pad = Pad(_records())
    saved = _capture(pad)
    assert saved.records["stick"].data == STICK
    assert saved.records["motor"].data == MOTOR
    assert saved.records["macro2"].status == "read" and saved.records["macro2"].data == bytes(range(1, 33))
    assert saved.records["macro1"].status == "read" and saved.records["macro1"].declared == 0
    assert saved.context["vid"] == "1013" and saved.context["descriptor"] == X20_DESCRIPTOR.hex()
    assert saved.context["changekeySources"] == ["01", "02"]


def test_a_long_macro_continues_with_position_and_slot():
    pad = Pad(_records())
    _capture(pad)
    asked = [payload for op, payload in pad.asked if op == p.Op.HOST_MACRO and len(payload) == 2 and payload[1] == 1]
    assert asked == [bytes([0, 1]), bytes([1, 1]), bytes([2, 1])]


def test_records_the_descriptor_switches_off_are_never_asked_for():
    pad = Pad(_records())
    saved = _capture(pad)
    # the X20 reports zero for turbo and lighting
    assert saved.records["turbo"].status == "not offered"
    assert saved.records["lighting"].status == "not offered"
    assert not any(op in (p.Op.HOST_TURBO, p.Op.HOST_LIGHTING) for op, _ in pad.asked)


def test_only_the_macro_slots_the_descriptor_offers_are_read():
    # macro byte 0x03: M1 and M2 only
    pad = Pad(_records(), descriptor=bytes.fromhex("0303030003010002c000"))
    saved = _capture(pad)
    assert saved.records["macro2"].status == "read"
    assert saved.records["macro3"].status == "not offered" and saved.records["macro4"].status == "not offered"
    assert not any(op == p.Op.HOST_MACRO and payload[1] >= 2 for op, payload in pad.asked if len(payload) == 2)


def test_six_macro_slots_read_when_offered_by_descriptor():
    # macro byte 0x3F: M1 to M6 (e.g. X20 Pro)
    pad = Pad(_records(), descriptor=bytes.fromhex("030303013f030002c000"))
    saved = _capture(pad)
    assert saved.records["macro5"].status == "read"
    assert saved.records["macro6"].status == "read"


def test_an_unproven_controller_gets_identification_only():
    pad = Pad(_records(), identity=bytes.fromhex("0710139999012352"))
    with pytest.raises(PermissionError):
        _capture(pad)
    assert {op for op, _ in pad.asked} <= {int(p.Op.READ_VID_PID_VERSION)}


def test_a_record_cut_short_is_incomplete_not_unchanged():
    pad, short = Pad(_records()), Pad(_records())

    async def lose_tail(opcode, payload=b"", _orig=short.query):
        if int(opcode) == p.Op.HOST_MACRO and bytes(payload) == bytes([2, 1]):
            return None
        return await _orig(opcode, payload)
    short.query = lose_tail
    a, b = _capture(pad), _capture(short, "after")
    assert b.records["macro2"].status == "incomplete"
    diff = state.compare(a, b)
    assert next(r for r in diff.records if r.name == "macro2").outcome == "not recorded"


def test_a_change_is_named_and_the_restore_comes_back_clean():
    before = _capture(Pad(_records()))
    changed = bytes([0xCC]) + MOTOR[1:4] + bytes.fromhex("80a90300")      # 80%, and 20 minutes
    after = _capture(Pad(_records() | {int(p.Op.HOST_MOTOR): changed}), "after")
    restored = _capture(Pad(_records()), "restored")

    diff = state.compare(before, after)
    assert diff.comparable
    assert [r.name for r in diff.changed] == ["motor"]
    motor = diff.changed[0]
    assert (0, 0x80, 0xCC) in motor.bytes
    assert any("motor 1 strength" in f and "80%" in f for f in motor.fields)
    assert "idle shutdown: 10 min -> 20 min" in motor.fields
    assert "doesn't show the messages the app sent" in diff.text()

    back = state.compare(before, restored)
    assert back.comparable and not back.changed


def test_a_different_firmware_makes_the_comparison_void():
    before = _capture(Pad(_records()))
    after = _capture(Pad(_records()), "after")
    after.context["firmware"] = "1.24"
    diff = state.compare(before, after)
    assert not diff.comparable
    assert "Inconclusive" in diff.text()


def test_a_saved_state_reads_back_the_same(tmp_path):
    saved = _capture(Pad(_records()))
    path = tmp_path / "before.json"
    saved.save(str(path))
    again = state.State.load(str(path))
    assert again.to_json() == saved.to_json()
    assert not state.compare(saved, again).changed


def test_a_file_that_is_no_state_is_refused(tmp_path):
    path = tmp_path / "x.json"
    path.write_text(json.dumps({"format": "something"}))
    with pytest.raises(ValueError):
        state.State.load(str(path))


def test_saving_never_overwrites_an_earlier_read_by_accident(tmp_path):
    path = tmp_path / "before.json"
    path.write_text("keep me")
    with pytest.raises(SystemExit):
        asyncio.run(cli.cmd_state_save(cli.build_parser().parse_args(["--address", "00:00:00:00:00:00", "state", "save", str(path), "--label", "after"])))
    assert path.read_text() == "keep me"


def test_the_diff_command_prints_the_comparison(tmp_path, capsys):
    before = _capture(Pad(_records()))
    after = _capture(Pad(_records() | {int(p.Op.HOST_STICK): bytes([5]) + STICK[1:]}), "after")
    before.save(str(tmp_path / "a.json"))
    after.save(str(tmp_path / "b.json"))
    assert cli.main(["state", "diff", str(tmp_path / "a.json"), str(tmp_path / "b.json")]) == 0
    out = capsys.readouterr().out
    assert "stick: changed" in out and "left stick: inner deadzone: 0 -> 5" in out
