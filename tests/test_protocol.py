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
