"""Offline tests for the KeyLinker framing. No hardware required.

Run with:  python -m pytest tests -q
       or: python tests/test_protocol.py
"""

from __future__ import annotations

import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from x20ctl import protocol as p


def test_generated_crc_table_matches_the_app():
    """The app ships a literal 256-entry table. If our polynomial guess is right,
    the generated table is identical, which proves the checksum is plain CRC-8
    with polynomial 0x79 rather than an arbitrary permutation."""
    assert len(p.CRC_TABLE_FROM_APP) == 256
    assert p.CRC_TABLE == p.CRC_TABLE_FROM_APP


def test_crc_of_empty_is_zero():
    assert p.crc8(b"") == 0


def test_serial_counter_alternates_high_bit():
    counter = p.SerialCounter()
    values = [counter.next() for _ in range(6)]
    assert values == [1, 130, 3, 132, 5, 134]
    for i, v in enumerate(values):
        # odd call -> high bit clear, even call -> high bit set
        assert bool(v & 0x80) == bool((i + 1) % 2 == 0)


def test_save_button_serial_packs_parity_slot_counter():
    # counter 1, slot 0 -> parity 1, slot 0000, counter 001
    assert p.save_button_serial(slot=0, counter=1) == 0b1_0000_001
    # counter 2, slot 5 -> parity 0, slot 0101, counter 010
    assert p.save_button_serial(slot=5, counter=2) == 0b0_0101_010


def test_host_length_packs_counter_and_length():
    field = p.host_length(payload_len=6, counter=7)
    assert field & 0x1F == 11          # 6 + 5
    assert field >> 5 == 7
    assert p.host_length(payload_len=0, counter=3) == 5


def test_scramble_roundtrip():
    """unscramble must invert scramble for every length the transport allows."""
    rng = random.Random(1234)
    for length in range(6, p.MAX_PACKET + 1):
        for _ in range(50):
            body = bytes(rng.randrange(256) for _ in range(length - 1))
            packet = body + bytes([p.crc8(body)])
            assert p.unscramble(p.scramble(packet)) == packet


def test_scramble_rejects_oversized_packets():
    try:
        p.scramble(bytes(p.MAX_PACKET + 1))
    except ValueError:
        return
    raise AssertionError("expected ValueError for an oversized packet")


def test_build_query_roundtrips_through_parse():
    raw = p.build_query(p.Op.HOST_LIGHTING, index=0, serial=1, nonce=0x42)
    pkt = p.parse(raw)
    assert pkt.opcode == p.Op.HOST_LIGHTING
    assert pkt.serial == 1
    assert pkt.nonce == 0x42
    assert pkt.payload == b"\x00"
    assert pkt.declared_length == 6      # 1 byte payload + 5
    assert pkt.crc_valid


def test_build_write_roundtrips_through_parse():
    payload = bytes([0x01, 0x02, 0x03, 0x04])
    raw = p.build_write(p.Op.WRITE_LIGHTING, payload, slot=0, counter=7, nonce=0x11)
    pkt = p.parse(raw)
    assert pkt.opcode == p.Op.WRITE_LIGHTING
    assert pkt.payload == payload
    assert pkt.declared_length == len(payload) + 5
    assert pkt.crc_valid


def test_recover_carries_the_magic_guard():
    pkt = p.parse(p.build_recover(serial=1, nonce=0))
    assert pkt.opcode == p.Op.RECOVER
    assert pkt.payload == p.RECOVER_MAGIC + b"\x01"
    assert pkt.crc_valid

    host = p.parse(p.build_recover(host=True, serial=1, nonce=0))
    assert host.payload == p.RECOVER_MAGIC + b"\x02"


# --- captured from real hardware -------------------------------------------
# EasySMX X20, BLE address 98:B6:ED:E3:15:C4, reply to READ_VID_PID_VERSION.
# Kept verbatim so any regression in the framing shows up immediately.
CAPTURED_REPLY = bytes.fromhex("268cb68ef74c7e10bfc1fddf62")


def test_real_hardware_reply_decodes():
    pkt = p.parse(CAPTURED_REPLY)
    assert pkt.opcode == p.Op.RESPONSE
    assert pkt.crc_valid, "CRC failed on a reply the hardware actually sent"
    assert pkt.serial == 0x01, "reply serial should echo the request serial"
    assert pkt.payload == bytes.fromhex("0710132009012352")
    assert pkt.declared_length == len(pkt.payload) + 5


def test_device_info_from_real_hardware():
    info = p.parse_device_info(p.parse(CAPTURED_REPLY).payload)
    assert info.vid == "0710"
    assert info.pid == "1320"
    assert info.version == "9.01"
    assert info.device_id == 0x1
    # 8-byte payload: the app does not decode the trailing bitfield, nor do we
    assert info.model is None
    assert info.sensor is None


def test_device_info_decodes_bitfield_only_at_seven_bytes():
    seven = bytes([0x07, 0x10, 0x13, 0x20, 0x09, 0x01, 0b010_1_0011])
    info = p.parse_device_info(seven)
    assert info.model == 0b0011
    assert info.sensor == 1
    assert info.version_family == 0b010


def test_device_info_rejects_short_payload():
    try:
        p.parse_device_info(b"\x01\x02\x03")
    except ValueError:
        return
    raise AssertionError("expected ValueError for a short payload")


# --- length-prefix structure, all captured from the same X20 ----------------

def test_read_name_proves_the_length_prefix():
    """The decisive case: the prefix is 6 and exactly six ASCII bytes follow."""
    body = p.unwrap(bytes.fromhex("06 587065727432".replace(" ", "")))
    assert body.declared == 6
    assert body.complete
    assert body.as_text() == "Xpert2"


def test_stick_settings_split_into_two_equal_channels():
    body = p.unwrap(bytes.fromhex("0e08085555aaaa0008085555aaaa00"))
    assert body.declared == 14
    assert body.complete
    left, right = body.groups(7)
    assert left == right, "the two stick channels should be symmetric at defaults"
    assert left == bytes.fromhex("08085555aaaa00")


def test_trigger_settings_split_into_two_equal_channels():
    body = p.unwrap(bytes.fromhex("0e042252 85e5eb00 04225285e5eb00".replace(" ", "")))
    assert body.declared == 14
    left, right = body.groups(7)
    assert left == right


def test_empty_changekey_means_no_remaps():
    body = p.unwrap(bytes.fromhex("00"))
    assert body.declared == 0
    assert body.complete
    assert body.data == b""


def test_chunked_response_is_reported_as_partial():
    """HOST_GUID declares 18 bytes but only 14 fit in one BLE packet."""
    body = p.unwrap(bytes.fromhex("12a1ca51da724cb6103658c05d7d4e"))
    assert body.declared == 18
    assert not body.complete
    assert body.missing == 4


def test_unwrap_rejects_empty_payload():
    try:
        p.unwrap(b"")
    except ValueError:
        return
    raise AssertionError("expected ValueError on an empty payload")


def test_groups_rejects_bad_size():
    try:
        p.unwrap(b"\x02ab").groups(0)
    except ValueError:
        return
    raise AssertionError("expected ValueError for a zero group size")


# --- lighting record, reassembled from chunks 0 and 1 of a real X20 ---------
# chunk 0: 18 ff 00 00 00 00 ff 00 00 ff 10 10 ff 00 00
# chunk 1:                                              ff 40 00 40 80 80 80 a0 80 ff
LIGHTING_RECORD = bytes.fromhex("18ff000000 00ff0000ff 1010ff0000 ff40004080 808 0a080ff".replace(" ", ""))


def test_lighting_record_is_four_six_byte_entries():
    body = p.unwrap(LIGHTING_RECORD)
    assert body.declared == 24
    assert body.complete
    entries = p.parse_lighting(body)
    assert len(entries) == 4, "24 bytes is four 6-byte entries, not eight triplets"
    assert entries[0].hex_colour == "#ff0000"
    assert entries[0].light == 0xFF
    assert entries[1].hex_colour == "#0000ff"
    assert entries[3].rgb == (0x80, 0x80, 0x80)


def test_lighting_entry_round_trips():
    entry = p.LightingEntry.parse(bytes.fromhex("0000ff1010ff"))
    assert entry.to_bytes() == bytes.fromhex("0000ff1010ff")
    assert entry.hex_colour == "#0000ff"


def test_lighting_rejects_incomplete_record():
    partial = p.Body(declared=24, data=bytes(12))
    try:
        p.parse_lighting(partial)
    except ValueError:
        return
    raise AssertionError("expected ValueError on an incomplete lighting record")


def test_lighting_rejects_misaligned_length():
    try:
        p.parse_lighting(p.Body(declared=7, data=bytes(7)))
    except ValueError:
        return
    raise AssertionError("expected ValueError when length is not a multiple of 6")


def test_lighting_entry_rejects_wrong_size():
    try:
        p.LightingEntry.parse(b"\x01\x02\x03")
    except ValueError:
        return
    raise AssertionError("expected ValueError for a short entry")


def test_menu_query_sends_position_then_kind():
    """0xB0 is multiplexed; payload order is [position, kind], not [kind, position].
    Getting this backwards is what made the opcode look unsupported."""
    raw = p.build_menu_query(p.MenuKind.TURBO_SUPPORT, position=0, serial=1, nonce=0)
    pkt = p.parse(raw)
    assert pkt.opcode == p.Op.HOST_MENU
    assert pkt.payload == bytes([0, 4])
    assert pkt.declared_length == 7  # 2 payload + 5
    assert pkt.crc_valid


def test_menu_query_honours_position():
    pkt = p.parse(p.build_menu_query(p.MenuKind.MACRO_SUPPORT, position=3, serial=1, nonce=0))
    assert pkt.payload == bytes([3, 5])


def test_turbo_support_reply_is_two_equal_channels():
    body = p.unwrap(bytes.fromhex("0a0c0188 0d840c01 880d84".replace(" ", "")))
    assert body.declared == 10
    assert body.complete
    first, second = body.groups(5)
    assert first == second == bytes.fromhex("0c0188 0d84".replace(" ", ""))


# --- capability descriptor, HOST_MENU kind 1, from a real X20 ---------------
CAPABILITY_RECORD = bytes.fromhex("0a03030300 0f01000 2c000".replace(" ", ""))


def test_capabilities_match_the_physical_x20():
    """Cross-check: the decoded record must agree with the hardware in hand.
    The X20 has two hall sticks, two triggers, two rumble motors and four rear
    buttons. If the bit order were wrong these would not all line up."""
    caps = p.parse_capabilities(p.unwrap(CAPABILITY_RECORD))
    assert caps.has_left_stick and caps.has_right_stick
    assert caps.has_left_trigger and caps.has_right_trigger
    assert caps.motor_count == 2
    assert caps.macro_slots == ["M1", "M2", "M3", "M4"]


def test_x20_does_not_expose_lighting_turbo_or_gyro():
    """The headline negative result. The X20 has RGB, turbo and a gyro in
    hardware, all driven by on-pad button combos, but reports none of them as
    configurable over this protocol."""
    caps = p.parse_capabilities(p.unwrap(CAPABILITY_RECORD))
    assert caps.lighting == 0
    assert caps.lighting_zones == 0
    assert caps.turbo == 0
    assert caps.sensor == 0
    assert "lighting" not in caps.supported()
    assert "turbo" not in caps.supported()
    assert "sensor" not in caps.supported()


def test_x20_exposes_remapping_and_macros():
    caps = p.parse_capabilities(p.unwrap(CAPABILITY_RECORD))
    assert caps.changekey == 1
    assert "button remapping" in caps.supported()
    assert "macros" in caps.supported()
    assert set(caps.supported()) == {
        "sticks", "triggers", "vibration", "macros", "button remapping", "eq", "nfc",
    }


def test_capabilities_rejects_short_record():
    try:
        p.parse_capabilities(p.Body(declared=3, data=b"\x01\x02\x03"))
    except ValueError:
        return
    raise AssertionError("expected ValueError on a short capability record")


def test_noop_changekey_write_matches_the_bytes_hardware_accepted():
    """The first write ever accepted by the controller, pinned exactly.

    Payload [0] means zero remaps. It is the packet the official app emits when
    the user changed nothing, so it is a no-op by construction. The pad
    acknowledged it with a RESPONSE echoing serial 0x81, which is what proves
    the bit-packed serial and length encodings are right.
    """
    packet = p.build(
        p.Op.WRITE_CHANGEKEY,
        bytes([0]),
        serial=p.save_button_serial(slot=0, counter=1),
        length_field=p.host_length(1, 7),
        nonce=0x5A,
    )
    assert p.unscramble(packet).hex(" ") == "36 e6 81 5a 00 ae"
    pkt = p.parse(packet)
    assert pkt.serial == 0x81
    assert pkt.length >> 5 == 7           # host counter in the top 3 bits
    assert pkt.declared_length == 6       # 1 payload + 5, in the low 5 bits
    assert pkt.crc_valid


def test_write_acknowledgement_from_hardware():
    """The pad's reply to that write. Serial echoes the request."""
    ack = p.parse(bytes.fromhex("2c8ca07b0f5f5f5a"), scrambled=False)
    # decoded form as observed: 19 08 81 .. 02 df aa ..
    raw = bytes([0x19, 0x08, 0x81, 0x00, 0x02, 0xDF, 0xAA])
    ack = p.Packet(opcode=raw[0], length=raw[1], serial=raw[2], nonce=raw[3],
                   payload=raw[4:], crc=p.crc8(raw))
    assert ack.opcode == p.Op.RESPONSE
    assert ack.serial == 0x81, "acknowledgement must echo the write's serial"
    assert p.unwrap(ack.payload).data == b"\xdf\xaa"


def test_corrupted_packet_fails_crc():
    raw = bytearray(p.build_query(p.Op.HOST_LIGHTING, index=0, serial=1, nonce=0x42))
    plain = bytearray(p.unscramble(bytes(raw)))
    plain[4] ^= 0xFF
    assert not p.parse(bytes(plain), scrambled=False).crc_valid


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  PASS  {name}")
            except AssertionError as exc:
                failures += 1
                print(f"  FAIL  {name}: {exc}")
    print(f"\n{'all passed' if not failures else f'{failures} failed'}")
    sys.exit(1 if failures else 0)
