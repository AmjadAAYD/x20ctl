"""read_body must keep the payload shape when following chunks.

HOST_MENU is the only multiplexed query: its payload is [position, kind], where
position is the chunk index. read_body used to follow continuations with a bare
index byte, which is malformed for HOST_MENU and answers with silence, so a
chunked menu record truncated without raising. Kind 6 declares 16 bytes and only
14 arrive in the first packet, which is how the bug was found.
"""

from __future__ import annotations

import asyncio

from x20ctl import protocol as p
from x20ctl.client import X20


def _packet(payload: bytes) -> p.Packet:
    """A well-formed reply carrying `payload`, with a CRC that validates."""
    opcode, length, serial, nonce = int(p.Op.RESPONSE), len(payload) + 5, 1, 0
    crc = p.crc8(bytes([opcode, length, serial, nonce]) + payload)
    return p.Packet(opcode=opcode, length=length, serial=serial,
                    nonce=nonce, payload=payload, crc=crc)


class ScriptedPad(X20):
    """An X20 whose `query` replays canned packets and records what was asked."""

    def __init__(self, replies: list[bytes]) -> None:
        super().__init__("00:00:00:00:00:00")
        self._replies = list(replies)
        self.asked: list[tuple[int, bytes]] = []

    async def query(self, opcode, payload: bytes = b""):
        self.asked.append((int(opcode), bytes(payload)))
        if not self._replies:
            return None
        return _packet(self._replies.pop(0))


# Kind 6 on a real X20: declares 16, first packet carries 14, tail is two zeros.
MENU6_HEAD = bytes([16]) + bytes.fromhex("2a002a002a002a0000000000000000")[:14]
MENU6_TAIL = bytes([0x00, 0x00])


def test_menu_continuation_keeps_the_kind_byte():
    pad = ScriptedPad([MENU6_HEAD, MENU6_TAIL])
    body = asyncio.run(pad.read_body(p.Op.HOST_MENU, bytes([0, 6])))

    assert body is not None
    assert body.complete, "the record should be complete after the continuation"
    assert len(body.data) == 16

    assert pad.asked[0] == (int(p.Op.HOST_MENU), bytes([0, 6]))
    # The continuation must advance position and PRESERVE the sub-query. A bare
    # bytes([1]) here is the bug: the pad answers it with silence.
    assert pad.asked[1] == (int(p.Op.HOST_MENU), bytes([1, 6]))


def test_single_byte_records_still_use_a_bare_index():
    """Every other record takes one index byte; that path must not change."""
    head = bytes([30]) + b"\x01" * 14
    tail = b"\x02" * 16
    pad = ScriptedPad([head, tail])
    body = asyncio.run(pad.read_body(p.Op.HOST_LIGHTING, bytes([0])))

    assert body is not None and body.complete
    assert pad.asked[0] == (int(p.Op.HOST_LIGHTING), bytes([0]))
    assert pad.asked[1] == (int(p.Op.HOST_LIGHTING), bytes([1]))


def test_a_complete_first_packet_asks_nothing_more():
    pad = ScriptedPad([bytes([4]) + b"\xaa\xbb\xcc\xdd"])
    body = asyncio.run(pad.read_body(p.Op.HOST_STICK, bytes([0])))

    assert body is not None and body.complete
    assert len(pad.asked) == 1


def test_silence_mid_record_returns_what_arrived():
    pad = ScriptedPad([MENU6_HEAD])          # no tail: the pad goes quiet
    body = asyncio.run(pad.read_body(p.Op.HOST_MENU, bytes([0, 6])))

    assert body is not None
    assert not body.complete
    assert len(body.data) == 14
