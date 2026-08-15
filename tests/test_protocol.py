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
    """The app ships a literal 256-entry table. Generating it from the
    polynomial reproduces it exactly, which proves the checksum is plain
    reflected CRC-8 with polynomial 0xEB rather than an arbitrary permutation."""
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
    # 8-byte payload: the app doesn't decode the trailing bitfield, nor do we
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
    buttons. If the bit order were wrong these wouldn't all line up."""
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

    Payload [0] means zero remaps. It's the packet the official app emits when
    the user changed nothing, so it's a no-op by construction. The pad
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


# --- support lists: count byte + RLE, both captured from the same X20 -------

def test_changekey_support_list_count_matches_decode():
    """kind 3. The leading 0x10 claims 16 keys; the RLE must expand to exactly
    16, which is what makes this decode self-verifying."""
    keys = p.decode_key_list(bytes.fromhex("1001880d840b825d82"))
    assert len(keys) == 16
    assert keys == [1, 2, 3, 4, 5, 6, 7, 8, 13, 14, 15, 16, 11, 12, 93, 94]


def test_macro_support_list_count_matches_decode():
    """kind 5. Declares 18 keys and includes 18 and 19, the two analog inputs
    the macro encoder treats specially."""
    keys = p.decode_key_list(bytes.fromhex("1212130d8401880b825d82"))
    assert len(keys) == 18
    assert keys[:2] == [18, 19]
    assert set(keys) == {1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 14, 15, 16, 18, 19, 93, 94}


def test_key_list_rejects_count_mismatch():
    try:
        p.decode_key_list(bytes([99, 1, 2, 3]))
    except ValueError:
        return
    raise AssertionError("a wrong count must be rejected, that is the point of it")


def test_key_list_rejects_leading_run_marker():
    try:
        p.decode_key_list(bytes([4, 0x84]))
    except ValueError:
        return
    raise AssertionError("a run with nothing to extend must be rejected")


def test_macro_step_round_trips():
    step = p.MacroStep(mask=0b101, duration_ms=100)
    raw = step.to_bytes()
    assert len(raw) == p.MACRO_STEP_SIZE
    assert raw == bytes([0x05, 0x00, 0x00, 20, 0x00])   # 100ms / 5 = 20 ticks
    assert p.MacroStep.parse(raw) == step


def test_macro_step_rejects_unrepresentable_duration():
    try:
        p.MacroStep(mask=1, duration_ms=7).to_bytes()
    except ValueError:
        return
    raise AssertionError("durations are counted in 5 ms units")


def test_macro_payload_header_sizes_the_steps():
    steps = [p.MacroStep(mask=1, duration_ms=50), p.MacroStep(mask=0, duration_ms=50)]
    payload = p.build_macro_payload(steps, loop_interval_ms=0)
    assert payload[0] == len(steps) * 5 + 2
    assert len(payload) == 3 + len(steps) * p.MACRO_STEP_SIZE
    assert p.MacroStep.parse(payload[3:8]) == steps[0]


def test_mask_defaults_to_analog_neutral():
    """Regression for a bug that reached hardware.

    The analog nibbles encode "untouched" as 0b1000, not 0b0000. A mask built
    with zeroed nibbles selects direction 0 on both sticks and drives them
    continuously. Every mask must therefore start from the neutral value.
    """
    assert p.mask_for([]) == p.MACRO_ANALOG_NEUTRAL
    assert p.mask_for([]) & 0xFF == 0x88
    a_mask = p.mask_for([p.Key.A])
    assert a_mask & 0xFF == 0x88, "analog nibbles must stay neutral"
    assert a_mask >> 12 & 1 == 1, "A is bit 12"


def test_every_digital_key_leaves_analog_nibbles_neutral():
    for key in p.MACRO_MASK_BIT:
        assert p.mask_for([key]) & 0xFF == p.MACRO_ANALOG_NEUTRAL, key.name


def test_macro_clear_is_a_bare_zero():
    """writeMacroData empties a slot with [0], not with an empty header."""
    assert p.MACRO_CLEAR == b"\x00"


def test_analog_keys_rejected_from_mask():
    try:
        p.mask_for([p.Key.LSTICK_ANALOG])
    except ValueError:
        return
    raise AssertionError("analog keys must not be silently packed as single bits")


def test_release_step_keeps_analog_neutral():
    """Second regression for the same bug, one line further along.

    Fixing mask_for() wasn't enough: a release step built as MacroStep(mask=0)
    bypasses it and re-creates the fault. MacroStep.released() exists so the
    safe form is the obvious one.
    """
    step = p.MacroStep.released(50)
    assert step.mask & 0xFF == p.MACRO_ANALOG_NEUTRAL
    assert step.to_bytes()[0] == 0x88
    # the unsafe form is still expressible, but must not be what tools reach for
    assert p.MacroStep(mask=0, duration_ms=50).to_bytes()[0] == 0x00


def test_a_full_tap_sequence_never_zeroes_the_nibbles():
    steps = [
        p.MacroStep(mask=p.mask_for([p.Key.A]), duration_ms=50),
        p.MacroStep.released(50),
    ]
    payload = p.build_macro_payload(steps, loop_interval_ms=0)
    for i in range(3, len(payload), p.MACRO_STEP_SIZE):
        assert payload[i] == 0x88, f"step at {i} has non-neutral analog nibbles"


def test_short_macro_needs_only_one_packet():
    payload = p.build_macro_payload([p.MacroStep(mask=p.mask_for([p.Key.A]), duration_ms=50),
                                     p.MacroStep.released(50)])
    assert len(p.build_macro_writes(payload, slot=0)) == 1


def test_two_button_macro_is_chunked_and_indexed():
    steps = []
    for key in (p.Key.A, p.Key.B):
        steps.append(p.MacroStep(mask=p.mask_for([key]), duration_ms=100))
        steps.append(p.MacroStep.released(60))
    payload = p.build_macro_payload(steps)
    assert len(payload) == 3 + 4 * p.MACRO_STEP_SIZE == 23

    packets = p.build_macro_writes(payload, slot=0)
    assert len(packets) == 2, "23 bytes does not fit one packet"

    for i, packet in enumerate(packets):
        assert len(packet) <= p.MAX_PACKET
        pkt = p.parse(packet)
        assert pkt.crc_valid
        assert pkt.opcode == p.Op.WRITE_MACRO
        # middle nibble of the serial carries the chunk index
        assert pkt.serial >> 3 & 0x0F == i

    # first chunk is exactly at the limit, which is why 15 was chosen
    assert len(packets[0]) == p.MAX_PACKET
    # reassembling the chunks must reproduce the payload
    rebuilt = b"".join(p.parse(x).payload for x in packets)
    assert rebuilt == payload


def test_chunks_carry_the_slot_in_every_packet():
    payload = p.build_macro_payload([p.MacroStep.released(50)] * 6)
    for packet in p.build_macro_writes(payload, slot=3):
        assert p.parse(packet).length >> 5 == 3


def test_macro_writes_rejects_empty_payload():
    try:
        p.build_macro_writes(b"", slot=0)
    except ValueError:
        return
    raise AssertionError("empty payload must be rejected; use MACRO_CLEAR")


def test_a_written_macro_parses_back_to_what_was_written():
    """parse_macro_payload is the inverse of build_macro_payload, so anything
    the builder emits must survive the trip back."""
    steps = p.parse_sequence("A:100/60,B+X:200/50")
    payload = p.build_macro_payload(steps, loop_interval_ms=250)

    program = p.parse_macro_payload(payload)
    assert [s.mask for s in program.steps] == [s.mask for s in steps]
    assert [s.duration_ms for s in program.steps] == [s.duration_ms for s in steps]
    assert program.loop_interval_ms == 250


def test_a_read_back_macro_renders_in_editor_syntax():
    """The point of reading a macro back is being able to show it, so what
    comes out has to be something you could have typed in."""
    written = "A:100/60,B+X:200/50"
    program = p.parse_macro_payload(p.build_macro_payload(p.parse_sequence(written)))
    assert program.as_sequence() == written


def test_a_trailing_press_with_no_release_still_renders():
    """The last step of a recording can be a press the recorder never saw
    released. It must not swallow the step or borrow the next one's gap."""
    steps = [p.MacroStep(mask=p.mask_for([p.Key.A]), duration_ms=100)]
    program = p.parse_macro_payload(p.build_macro_payload(steps))
    assert program.as_sequence() == "A:100/0"


def test_an_empty_record_reads_as_empty_not_as_a_crash():
    program = p.parse_macro_payload(p.build_macro_payload([]))
    assert program.steps == []
    assert program.as_sequence() == "empty"


def test_a_truncated_macro_record_is_rejected():
    try:
        p.parse_macro_payload(b"\x0c\x00")
    except ValueError as exc:
        assert "too short" in str(exc)
        return
    raise AssertionError("a record shorter than its header must be rejected")


def test_all_sixteen_digital_buttons_are_macro_capable():
    """Every key in the pad's macro list that's a single-bit digital button
    must be usable. 18 and 19 are analog; 93 and 94 are Select and Start."""
    pad_list = p.decode_key_list(bytes.fromhex("1212130d8401880b825d82"))
    digital = [k for k in pad_list if k not in (18, 19)]
    assert len(digital) == 16
    for code in digital:
        mask = p.mask_for([p.Key(code)])
        assert mask != p.MACRO_ANALOG_NEUTRAL, f"code {code} set no bit"


def test_select_start_are_macro_capable_home_is_not():
    """93 (Select) and 94 (Start) are real digital buttons on the X20 and must
    be usable in macros. Home (17) is absent from the pad's macro list and must
    fail loudly instead of silently producing a mask that does nothing.

    The bit positions are the ones a live pad was measured at: a macro setting
    bit 22 made XInput report BACK, and bit 23 made it report START.
    """
    select = p.mask_for([p.Key.SELECT])
    assert select & 0xFF == p.MACRO_ANALOG_NEUTRAL
    assert select >> 22 & 1, "Select should sit at bit 22"

    start = p.mask_for([p.Key.START])
    assert start & 0xFF == p.MACRO_ANALOG_NEUTRAL
    assert start >> 23 & 1, "Start should sit at bit 23"

    try:
        p.mask_for([p.Key.HOME])
    except ValueError as exc:
        assert "not macro-capable" in str(exc)
    else:
        raise AssertionError("HOME should have been rejected")


def test_chord_sets_multiple_bits_in_one_step():
    mask = p.mask_for([p.Key.A, p.Key.B])
    assert mask >> 12 & 1 and mask >> 13 & 1
    assert mask & 0xFF == p.MACRO_ANALOG_NEUTRAL
    # a chord is one step, so it stays within a single packet
    payload = p.build_macro_payload([p.MacroStep(mask=mask, duration_ms=100),
                                     p.MacroStep.released(50)])
    assert len(p.build_macro_writes(payload, slot=0)) == 1


# --- thumbstick directions in macros ----------------------------------------

def test_idle_nibble_is_not_a_direction():
    """The trap that drove both sticks: 0b1000 is idle, 0b0000 is direction UP."""
    assert p.ANALOG_IDLE_NIBBLE == 0b1000
    assert p.MACRO_ANALOG_NEUTRAL == (0b1000 | 0b1000 << 4)
    assert p.describe_mask(p.mask_for([])) == []


def test_stick_direction_occupies_the_right_nibble():
    left = p.mask_for([p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.UP)])
    assert left & 0x0F == 0            # UP is direction 1, encoded as 0
    assert left >> 4 & 0x0F == 0b1000  # the other stick stays idle

    right = p.mask_for([p.StickInput(p.Key.RSTICK_ANALOG, p.Direction.LEFT)])
    assert right >> 4 & 0x0F == p.Direction.LEFT - 1
    assert right & 0x0F == 0b1000


def test_all_eight_directions_round_trip():
    for stick in (p.Key.LSTICK_ANALOG, p.Key.RSTICK_ANALOG):
        for direction in p.Direction:
            item = p.StickInput(stick, direction)
            assert p.describe_mask(p.mask_for([item])) == [item.name]


def test_parse_token_handles_buttons_and_sticks():
    assert p.parse_token("a") is p.Key.A
    assert p.parse_token("LS_UP") == p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.UP)
    assert p.parse_token("rs-down-left") == p.StickInput(
        p.Key.RSTICK_ANALOG, p.Direction.DOWN_LEFT)


def test_parse_token_rejects_nonsense():
    for bad in ("", "NOPE", "LS_SIDEWAYS", "LS"):
        try:
            p.parse_token(bad)
        except ValueError:
            continue
        raise AssertionError(f"{bad!r} should have been rejected")


def test_button_and_stick_combine_in_one_step():
    mask = p.mask_for([p.Key.A,
                       p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.LEFT),
                       p.StickInput(p.Key.RSTICK_ANALOG, p.Direction.RIGHT)])
    assert set(p.describe_mask(mask)) == {"A", "LS_LEFT", "RS_RIGHT"}


def test_a_stick_cannot_point_two_ways_at_once():
    try:
        p.mask_for([p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.UP),
                    p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.DOWN)])
    except ValueError:
        return
    raise AssertionError("conflicting directions must be rejected")


def test_bare_stick_name_asks_for_a_direction():
    try:
        p.mask_for([p.Key.LSTICK_ANALOG])
    except ValueError as exc:
        assert "needs a direction" in str(exc)
        return
    raise AssertionError("a bare stick name must be rejected")


# --- battery, captured from a real X20 --------------------------------------
# reply to the mode query: 19 0f 01 32 | 09 df aa 94 80 bd 93 01 00 5e | crc
POWER_BODY = p.Body(declared=9, data=bytes.fromhex("dfaa9480bd9301005e"))


def test_battery_from_real_hardware():
    """The pad was on mains-free full charge when this was captured."""
    battery = p.parse_battery(POWER_BODY)
    assert battery.level == 4
    assert battery.charging is False
    assert battery.approximate_percent == 100
    assert "4/4" in str(battery)


def test_battery_bit_layout():
    def status(byte):
        return p.parse_battery(p.Body(declared=9, data=bytes([0, 0, 0, byte, 0])))

    assert status(0b0000_0000).level == 1
    assert status(0b0010_0000).level == 2
    assert status(0b0100_0000).level == 3
    assert status(0b1000_0000).level == 4
    assert status(0b0001_0000).charging is True
    assert status(0b0000_0000).charging is False
    assert status(0b1001_0000) == p.Battery(level=4, charging=True)


def test_battery_precedence_follows_the_app():
    """The app tests bit 5 first, so a lower bit wins when several are set.
    Reproduced over 'corrected', since the device's meaning is unknown."""
    both = p.parse_battery(p.Body(declared=9, data=bytes([0, 0, 0, 0b1010_0000, 0])))
    assert both.level == 2


def test_battery_rejects_short_body():
    try:
        p.parse_battery(p.Body(declared=2, data=b"\x01\x02"))
    except ValueError:
        return
    raise AssertionError("a short power reply must be rejected")


# --- stick and trigger curves, captured from a real X20 ---------------------

def test_stick_curve_from_real_hardware_is_linear():
    """Control points on the diagonal are an untouched response."""
    body = p.unwrap(bytes.fromhex("0e08085555aaaa0008085555aaaa00"))
    left, right = p.parse_curves(body)
    assert left == right, "both sticks ship identical"
    assert left.inner_deadzone == 8
    assert left.point1 == (85, 85) and left.point2 == (170, 170)
    assert left.is_linear
    assert not (left.invert_x or left.invert_y or left.swapped)


def test_trigger_curve_from_real_hardware_is_not_linear():
    """The X20 ships triggers with a curve that ramps faster than linear."""
    body = p.unwrap(bytes.fromhex("0e04225285e5eb0004225285e5eb00"))
    left, right = p.parse_curves(body, p.TRIGGER_MAX_PROGRESS)
    assert left == right
    assert left.point1 == (82, 133), "y above x means a faster ramp"
    assert left.point2 == (229, 235)
    assert not left.is_linear


def test_curves_round_trip():
    for raw, scale in (("0e08085555aaaa0008085555aaaa00", p.STICK_MAX_PROGRESS),
                       ("0e04225285e5eb0004225285e5eb00", p.TRIGGER_MAX_PROGRESS)):
        body = p.unwrap(bytes.fromhex(raw))
        assert p.build_curves(p.parse_curves(body, scale)) == bytes.fromhex(raw)


def test_curve_flags_decode():
    curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170,
                                 p.FLAG_INVERT_X | p.FLAG_SWAP]))
    assert curve.invert_x and curve.swapped and not curve.invert_y


def test_outer_deadzone_is_stored_subtracted():
    """The wire holds (max - outer), which the app shows the other way round."""
    curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170, 0]))
    assert curve.outer_raw == 8
    assert curve.outer_deadzone == p.STICK_MAX_PROGRESS - 8


def test_curve_rejects_a_wrong_sized_channel():
    try:
        p.Curve.parse(bytes(5))
    except ValueError:
        return
    raise AssertionError("a channel must be exactly 7 bytes")


def test_a_macro_that_is_too_long_says_so():
    """47 entries, not the 50 the length byte alone would allow.

    The payload is chunked and the chunk index is four bits, so only 16 chunks
    of 15 bytes can be addressed. 240 bytes is 47 entries, and that binds before
    the length byte does. Past it, bytes() raised "must be in range(0, 256)"
    from inside the header builder, which tells you nothing about macros."""
    assert p.MAX_MACRO_ENTRIES == 47
    fits = p.parse_sequence(",".join(["LS_UP:50/50"] * 23), 100, 60)
    payload = p.build_macro_payload(fits, loop_interval_ms=0)
    assert len(payload) <= p.MAX_MACRO_PAYLOAD
    assert len(p.build_macro_writes(payload, slot=0)) <= p.MAX_MACRO_CHUNKS

    too_long = p.parse_sequence(",".join(["LS_UP:50/50"] * 26), 100, 60)
    try:
        p.build_macro_payload(too_long, loop_interval_ms=0)
        raise AssertionError("a macro past the ceiling must be refused")
    except ValueError as exc:
        assert "at most 47 entries" in str(exc)
        assert "hardware" in str(exc), "say whose limit it is"
        assert "four bits" in str(exc), "name the thing that actually binds"


def test_the_chunk_index_ceiling_is_reported_not_crashed_into():
    """build_macro_writes must refuse a payload it cannot address.

    A caller that builds a payload by hand bypasses build_macro_payload's entry
    check, and used to reach bytes() with a serial that no longer fit a byte."""
    over = bytes(p.MAX_MACRO_PAYLOAD + 1)
    try:
        p.build_macro_writes(over, slot=0)
        raise AssertionError("a payload past the chunk ceiling must be refused")
    except ValueError as exc:
        assert "four bits" in str(exc)
        assert str(p.MAX_MACRO_CHUNKS) in str(exc)

    ok = bytes(p.MAX_MACRO_PAYLOAD)
    assert len(p.build_macro_writes(ok, slot=0)) == p.MAX_MACRO_CHUNKS


def test_a_two_channel_curve_write_exactly_fills_a_packet():
    """Confirmed on hardware: 20 bytes on the wire, the whole cap.

    Two channels leave nothing spare, so a pad reporting a third could not be
    written in one packet. Worth pinning: a byte added to this record silently
    breaks every stick and trigger write.
    """
    body = p.unwrap(bytes.fromhex("0e08085555aaaa0008085555aaaa00"))
    payload = p.build_curves(p.parse_curves(body))
    assert len(payload) == 15
    packet = p.build(p.Op.WRITE_STICK, payload, serial=0x81,
                     length_field=p.host_length(len(payload), 7), nonce=0x5A)
    assert len(packet) == p.MAX_PACKET
    assert len(payload) + p.FRAME_OVERHEAD == p.MAX_PACKET


def test_the_deadzone_change_confirmed_on_hardware_round_trips():
    """The exact bytes the pad accepted, and read back, for inner 8 -> 10."""
    body = p.unwrap(bytes.fromhex("0e08085555aaaa0008085555aaaa00"))
    left, right = p.parse_curves(body)
    changed = p.build_curves([left.with_deadzones(inner=10), right])
    assert changed.hex(" ") == "0e 0a 08 55 55 aa aa 00 08 08 55 55 aa aa 00"
    assert p.build_curves([left, right]).hex(" ") == \
        "0e 08 08 55 55 aa aa 00 08 08 55 55 aa aa 00", "restoring is exact"


# --- editing a curve --------------------------------------------------------

def test_deadzones_are_set_in_the_units_a_person_reads():
    """`outer` goes in the way the UI shows it and comes out subtracted."""
    curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170, 0]))
    edited = curve.with_deadzones(inner=5, outer=95)
    assert edited.inner_deadzone == 5
    assert edited.outer_deadzone == 95
    assert edited.to_bytes()[1] == p.STICK_MAX_PROGRESS - 95
    assert curve.inner_deadzone == 8, "editing returns a copy, it doesn't mutate"


def test_editing_one_field_leaves_the_rest_alone():
    curve = p.Curve.parse(bytes([4, 34, 82, 133, 229, 235, 0]),
                          p.TRIGGER_MAX_PROGRESS)
    edited = curve.with_deadzones(inner=10)
    assert edited.outer_raw == 34
    assert edited.point1 == (82, 133) and edited.point2 == (229, 235)
    assert edited.max_progress == p.TRIGGER_MAX_PROGRESS


def test_straightening_moves_the_points_and_keeps_the_deadzones():
    curve = p.Curve.parse(bytes([4, 34, 82, 133, 229, 235, 0]),
                          p.TRIGGER_MAX_PROGRESS)
    straight = curve.straightened()
    assert straight.is_linear
    assert straight.inner_deadzone == 4 and straight.outer_raw == 34


def test_flags_keep_bits_nobody_has_decoded():
    """An unknown bit is preserved, the same as the vibration write does."""
    curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170, 0x80]))
    edited = curve.with_flags(invert_y=True)
    assert edited.flags == 0x80 | p.FLAG_INVERT_Y
    assert edited.with_flags(invert_y=False).flags == 0x80


def test_validate_rejects_deadzones_that_leave_no_travel():
    for inner, outer in ((60, 40), (0, 0)):
        curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170, 0])) \
            .with_deadzones(inner=inner, outer=outer)
        try:
            curve.validate()
        except ValueError:
            continue
        raise AssertionError(f"inner {inner} past outer {outer} must be rejected")


def test_validate_rejects_points_out_of_order():
    curve = p.Curve.parse(bytes([8, 8, 85, 85, 170, 170, 0])) \
        .with_points((200, 100), (50, 100))
    try:
        curve.validate()
    except ValueError:
        return
    raise AssertionError("control points have to run left to right")


def test_validate_accepts_everything_the_hardware_ships_with():
    for raw, scale in (("08085555aaaa00", p.STICK_MAX_PROGRESS),
                       ("04225285e5eb00", p.TRIGGER_MAX_PROGRESS)):
        p.Curve.parse(bytes.fromhex(raw), scale).validate()


def test_curve_shape_passes_through_the_control_points():
    """The drawing is an interpolation, but it may not move the known values."""
    point1, point2 = (82, 133), (229, 235)
    shape = dict(p.curve_shape(point1, point2, steps=256))
    for x, y in (point1, point2):
        nearest = min(shape, key=lambda sx: abs(sx - x))
        assert abs(shape[nearest] - y) < 1.0, f"the curve misses {(x, y)}"
    assert shape[0.0] == 0.0
    assert abs(shape[float(p.CURVE_AXIS_MAX)] - p.CURVE_AXIS_MAX) < 1e-6


def test_curve_shape_never_doubles_back():
    """A response curve is monotone, so the interpolation has to be too."""
    for point1, point2 in (((85, 85), (170, 170)), ((82, 133), (229, 235)),
                           ((10, 240), (250, 245)), ((120, 5), (130, 250))):
        shape = p.curve_shape(point1, point2, steps=64)
        for (_, previous), (_, current) in zip(shape, shape[1:]):
            assert current >= previous - 1e-9, f"{point1}->{point2} dips"


def test_curve_shape_survives_two_points_stacked():
    shape = p.curve_shape((100, 60), (100, 200), steps=16)
    assert len(shape) == 16


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
