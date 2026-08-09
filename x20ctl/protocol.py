"""KeyLinker wire protocol: framing, checksum and the scrambling layer.

Reconstructed from com.pulsenet.inputset.util.CodeHelper. This module is pure
computation with no I/O, so every function here can be exercised without a
controller attached.

Packet layout, before scrambling:

    0        opcode
    1        length field
    2        serial
    3        random nonce
    4..n-2   payload
    n-1      CRC-8 over bytes 0..n-2

The finished packet is passed through `scramble` before being written to the BLE
characteristic, and received packets through `unscramble`.
"""

from __future__ import annotations

import random
from dataclasses import dataclass
from enum import IntEnum

# Cap comes from SERIAL_ENCRYPT being 40 entries with a 20-byte offset, which is
# also exactly the payload of a default 23-byte BLE ATT MTU.
MAX_PACKET = 20

# Reflected form. Equivalent to normal-form polynomial 0xD7.
CRC_POLY = 0xEB


def _build_crc_table() -> tuple[int, ...]:
    """CRC-8, LSB-first (reflected), polynomial 0xEB, init 0, no final xor.

    Verified against the literal table shipped in the app, and the table is
    xor-linear, which confirms this is a real CRC rather than an arbitrary
    substitution box.
    """
    table = []
    for value in range(256):
        crc = value
        for _ in range(8):
            crc = ((crc >> 1) ^ CRC_POLY) & 0xFF if crc & 1 else (crc >> 1) & 0xFF
        table.append(crc)
    return tuple(table)


CRC_TABLE = _build_crc_table()

# Verbatim from CodeHelper. Kept so the generated table can be proven equivalent
# rather than merely assumed; see tests/test_protocol.py.
CRC_TABLE_FROM_APP = (
    0, 121, 242, 139, 51, 74, 193, 184, 102, 31, 148, 237, 85, 44, 167, 222,
    204, 181, 62, 71, 255, 134, 13, 116, 170, 211, 88, 33, 153, 224, 107, 18,
    79, 54, 189, 196, 124, 5, 142, 247, 41, 80, 219, 162, 26, 99, 232, 145,
    131, 250, 113, 8, 176, 201, 66, 59, 229, 156, 23, 110, 214, 175, 36, 93,
    158, 231, 108, 21, 173, 212, 95, 38, 248, 129, 10, 115, 203, 178, 57, 64,
    82, 43, 160, 217, 97, 24, 147, 234, 52, 77, 198, 191, 7, 126, 245, 140,
    209, 168, 35, 90, 226, 155, 16, 105, 183, 206, 69, 60, 132, 253, 118, 15,
    29, 100, 239, 150, 46, 87, 220, 165, 123, 2, 137, 240, 72, 49, 186, 195,
    235, 146, 25, 96, 216, 161, 42, 83, 141, 244, 127, 6, 190, 199, 76, 53,
    39, 94, 213, 172, 20, 109, 230, 159, 65, 56, 179, 202, 114, 11, 128, 249,
    164, 221, 86, 47, 151, 238, 101, 28, 194, 187, 48, 73, 241, 136, 3, 122,
    104, 17, 154, 227, 91, 34, 169, 208, 14, 119, 252, 133, 61, 68, 207, 182,
    117, 12, 135, 254, 70, 63, 180, 205, 19, 106, 225, 152, 32, 89, 210, 171,
    185, 192, 75, 50, 138, 243, 120, 1, 223, 166, 45, 84, 236, 149, 30, 103,
    58, 67, 200, 177, 9, 112, 251, 130, 92, 37, 174, 215, 111, 22, 157, 228,
    246, 143, 4, 125, 197, 188, 55, 78, 144, 233, 98, 27, 163, 218, 81, 40,
)

SEND_DATA_KEY = (51, 99, 157, 121, 242, 219, 162, 26, 170, 33, 139, 232, 116, 211, 88)

SERIAL_KEY = (
    161, 82, 213, 163, 245, 137, 246, 143, 240, 157, 72, 147, 234, 52,
    49, 186, 195, 77, 198, 235, 73, 96, 216, 163, 218, 42, 83, 141,
    244, 97, 24, 191, 174, 215, 111, 81, 228, 160, 217, 146,
)


class Op(IntEnum):
    """Opcodes from CodeHelper. Names follow the app's own constants."""

    # queries
    READ_NAME = 0x90
    READ_VID_PID_VERSION = 0x91
    HOST_GUID = 0x93
    READ_BUTTON_MODE = 0x97
    LOAD_BUTTON = 0x98
    READ_3D = 0x9A
    READ_PRESS_GUN = 0xA4
    READ_TURBO = 0xA5           # app spells this TOOBLE
    READ_SLIDE_SCREEN = 0xA6
    READ_MACRO = 0xA7
    TWO_IN_ONE_STATE = 0xA8
    HOST_MENU = 0xB0
    HOST_STICK = 0xB1           # app calls this ROCK
    HOST_TRIGGER = 0xB2
    HOST_MOTOR = 0xB3
    HOST_TURBO = 0xB4
    HOST_MACRO = 0xB5
    HOST_CHANGEKEY = 0xB6
    HOST_LIGHTING = 0xB7

    # writes
    WRITE_STICK = 0x31
    WRITE_TRIGGER = 0x32
    WRITE_MOTOR = 0x33
    WRITE_TURBO = 0x34
    WRITE_MACRO = 0x35
    WRITE_CHANGEKEY = 0x36
    WRITE_LIGHTING = 0x37       # app misspells this WHITE_LIGHTING

    # control
    RECOVER = 0x16
    RESPONSE = 0x19
    SET_MODE = 0x1D
    NORMAL_MODE = 0x1E


def crc8(data: bytes | bytearray | list[int]) -> int:
    crc = 0
    for byte in data:
        crc = CRC_TABLE[crc ^ byte]
    return crc


class SerialCounter:
    """Reproduces CodeHelper.getSerial().

    Odd calls yield n % 128, even calls yield (n % 128) + 128, so the high bit
    alternates between consecutive packets.
    """

    def __init__(self) -> None:
        self._n = 0

    def next(self) -> int:
        self._n += 1
        if self._n % 2 == 0:
            return (self._n % 128) + 128
        return self._n % 128


def save_button_serial(slot: int, counter: int) -> int:
    """Reproduces CodeHelper.getSaveButtonSerial().

    Assembles parity, a 4-bit slot index and a 3-bit counter into one byte.
    """
    parity = counter % 2
    return int(f"{parity:b}{slot:04b}{counter % 8:03b}", 2)


def host_length(payload_len: int, counter: int) -> int:
    """Reproduces CodeHelper.getHostLength(): 3-bit counter, 5-bit length."""
    if payload_len <= 0:
        return 5
    return ((counter % 8) << 5) | ((payload_len + 5) & 0x1F)


def plain_length(payload_len: int) -> int:
    """Reproduces CodeHelper.getLength()."""
    return 5 if payload_len <= 0 else payload_len + 5


def scramble(packet: bytes) -> bytes:
    """CodeHelper.encrypt(). Not cryptography, just obfuscation."""
    if len(packet) > MAX_PACKET:
        raise ValueError(f"packet of {len(packet)} exceeds the {MAX_PACKET}-byte cap")

    out = bytearray(len(packet))
    b2, b3 = packet[2], packet[3]

    out[0] = (packet[0] ^ ((b2 + b3) - 154)) & 0xFF
    out[1] = (packet[1] ^ ((b2 + b3) + 155)) & 0xFF
    out[2] = ((b2 - 173) ^ b3) & 0xFF
    out[3] = ((b3 + 191) ^ 219) & 0xFF

    # Note the java loop stops one short of the end, leaving the CRC byte alone.
    for i in range(4, len(packet) - 1):
        out[i] = (packet[i] ^ ((b2 + b3) - SEND_DATA_KEY[i - 4])) & 0xFF
    out[len(packet) - 1] = packet[len(packet) - 1]

    offset = (out[3] & 2) * 10
    for i in range(len(packet)):
        out[i] ^= SERIAL_KEY[offset + i]
    return bytes(out)


def unscramble(packet: bytes) -> bytes:
    """CodeHelper.decode(), the exact inverse of scramble()."""
    offset = 20 if ((packet[3] ^ SERIAL_KEY[3]) & 2) else 0
    out = bytearray((packet[i] ^ SERIAL_KEY[offset + i]) & 0xFF for i in range(len(packet)))

    b3 = ((out[3] ^ 219) - 191) & 0xFF
    out[3] = b3
    b2 = ((out[2] ^ b3) + 173) & 0xFF
    out[2] = b2
    out[1] = (out[1] ^ ((b2 + b3) + 155)) & 0xFF
    out[0] = (out[0] ^ ((b2 + b3) - 154)) & 0xFF

    for i in range(4, len(packet) - 1):
        out[i] = (out[i] ^ ((b2 + b3) - SEND_DATA_KEY[i - 4])) & 0xFF
    return bytes(out)


@dataclass
class Packet:
    opcode: int
    length: int
    serial: int
    nonce: int
    payload: bytes
    crc: int

    @property
    def crc_valid(self) -> bool:
        body = bytes([self.opcode, self.length, self.serial, self.nonce]) + self.payload
        return crc8(body) == self.crc

    @property
    def declared_length(self) -> int:
        """Low 5 bits of the length field; the top 3 are a rotating counter."""
        return self.length & 0x1F

    def __repr__(self) -> str:
        name = Op(self.opcode).name if self.opcode in Op._value2member_map_ else "?"
        ok = "ok" if self.crc_valid else "BAD CRC"
        return (
            f"Packet({name} 0x{self.opcode:02X} len=0x{self.length:02X} "
            f"serial=0x{self.serial:02X} payload={self.payload.hex(' ')} crc={ok})"
        )


def parse(raw: bytes, *, scrambled: bool = True) -> Packet:
    data = unscramble(raw) if scrambled else raw
    return Packet(
        opcode=data[0],
        length=data[1],
        serial=data[2],
        nonce=data[3],
        payload=data[4:-1],
        crc=data[-1],
    )


def build(
    opcode: int,
    payload: bytes = b"",
    *,
    serial: int,
    length_field: int | None = None,
    nonce: int | None = None,
) -> bytes:
    """Assemble one packet, append the CRC, and scramble it.

    `nonce` is exposed only so tests can pin it; leave it None in real use.
    """
    if nonce is None:
        nonce = random.randrange(256)
    if length_field is None:
        length_field = plain_length(len(payload))

    body = bytes([opcode, length_field, serial, nonce]) + payload
    return scramble(body + bytes([crc8(body)]))


def build_query(opcode: int, index: int, *, serial: int, nonce: int | None = None) -> bytes:
    """The shape shared by every getHost* query: a single index byte."""
    return build(opcode, bytes([index]), serial=serial, nonce=nonce)


class MenuKind(IntEnum):
    """Second payload byte of a HOST_MENU (0xB0) query.

    0xB0 is multiplexed: the payload is [position, kind], where position is the
    chunk index and kind selects the sub-query. Values are taken from the app's
    call sites, e.g. getHostChangeKeySupport(pos, 3), getHostToobleSupport(pos, 4).
    """

    MENU = 1
    GAMEPAD_ALL_KEYS = 2
    CHANGEKEY_SUPPORT = 3
    TURBO_SUPPORT = 4
    MACRO_SUPPORT = 5
    CHANGEKEY_ALT6 = 6
    CHANGEKEY_ALT7 = 7


def build_menu_query(
    kind: int,
    position: int = 0,
    *,
    serial: int,
    nonce: int | None = None,
) -> bytes:
    """A HOST_MENU sub-query. Payload is [position, kind], in that order."""
    return build(Op.HOST_MENU, bytes([position, kind]), serial=serial, nonce=nonce)


def build_write(
    opcode: int,
    payload: bytes,
    slot: int,
    counter: int,
    *,
    nonce: int | None = None,
) -> bytes:
    """The shape shared by every writeHost*Data call."""
    return build(
        opcode,
        payload,
        serial=save_button_serial(slot, counter),
        length_field=host_length(len(payload), counter),
        nonce=nonce,
    )


@dataclass
class Body:
    """A response payload after its length prefix has been stripped.

    Byte 0 of every response payload is the length of the data that follows.
    Confirmed by READ_NAME, which returns `06` followed by exactly the six ASCII
    bytes of "Xpert2".

    When `declared` exceeds what arrived, the record is split across chunks and
    the remainder must be fetched by re-querying with the next index.
    """

    declared: int
    data: bytes

    @property
    def complete(self) -> bool:
        return len(self.data) == self.declared

    @property
    def missing(self) -> int:
        return max(0, self.declared - len(self.data))

    def as_text(self) -> str:
        return self.data.decode("utf-8", "replace")

    def groups(self, size: int) -> list[bytes]:
        """Split the data into fixed-size records.

        Several settings are stored as repeated per-channel blocks: sticks and
        triggers each return two identical 7-byte groups, one per side.
        """
        if size <= 0:
            raise ValueError("group size must be positive")
        return [self.data[i:i + size] for i in range(0, len(self.data), size)]

    def __repr__(self) -> str:
        state = "complete" if self.complete else f"partial, {self.missing} more expected"
        return f"Body(declared={self.declared}, {state}, data={self.data.hex(' ')})"


def unwrap(payload: bytes) -> Body:
    """Strip the length prefix from a response payload."""
    if not payload:
        raise ValueError("empty payload has no length prefix")
    return Body(declared=payload[0], data=payload[1:])


@dataclass
class Capabilities:
    """What a pad exposes to the configuration protocol.

    Decoded from the HOST_MENU kind 1 reply. Byte offsets and bit meanings come
    from HostActivity.initMenu, which switches the app's whole UI off this one
    record: a zero byte means the corresponding settings page is never shown.

    A zero here means "not configurable over this protocol", NOT "the hardware
    lacks the feature". An X20 has RGB lighting and a gyro in hardware, both
    driven by on-pad button combinations, yet reports zero for both.
    """

    sticks: int
    triggers: int
    motors: int
    turbo: int
    macros: int
    changekey: int
    lighting: int
    eq: int
    nfc: int
    sensor: int

    # bit 0 is the rightmost; the app reads binaryString(x).charAt(7) for bit 0
    @staticmethod
    def _bit(value: int, index: int) -> bool:
        return bool(value >> index & 1)

    @property
    def has_left_stick(self) -> bool:
        return self._bit(self.sticks, 1)

    @property
    def has_right_stick(self) -> bool:
        return self._bit(self.sticks, 0)

    @property
    def has_left_trigger(self) -> bool:
        return self._bit(self.triggers, 1)

    @property
    def has_right_trigger(self) -> bool:
        return self._bit(self.triggers, 0)

    @property
    def motor_count(self) -> int:
        return sum(self._bit(self.motors, i) for i in range(4))

    @property
    def macro_slots(self) -> list[str]:
        names = ["M1", "M2", "M3", "M4", "M5", "M6", "ML", "MR"]
        return [n for i, n in enumerate(names) if self._bit(self.macros, i)]

    @property
    def lighting_zones(self) -> int:
        return sum(self._bit(self.lighting, i) for i in range(8))

    def supported(self) -> list[str]:
        out = []
        if self.sticks:
            out.append("sticks")
        if self.triggers:
            out.append("triggers")
        if self.motors:
            out.append("vibration")
        if self.turbo:
            out.append("turbo")
        if self.macros:
            out.append("macros")
        if self.changekey:
            out.append("button remapping")
        if self.lighting:
            out.append("lighting")
        if self.eq:
            out.append("eq")
        if self.nfc:
            out.append("nfc")
        if self.sensor:
            out.append("sensor")
        return out

    def __str__(self) -> str:
        got = ", ".join(self.supported()) or "nothing"
        return (f"configurable: {got}\n"
                f"  sticks     L={self.has_left_stick} R={self.has_right_stick}\n"
                f"  triggers   L={self.has_left_trigger} R={self.has_right_trigger}\n"
                f"  motors     {self.motor_count}\n"
                f"  macros     {', '.join(self.macro_slots) or 'none'}\n"
                f"  lighting   {self.lighting_zones} zone(s)")


def parse_capabilities(body: "Body") -> Capabilities:
    """Decode the HOST_MENU kind 1 reply.

    `body` is the payload with the length prefix already stripped, so index 0
    here is the app's iArr[1].
    """
    if len(body.data) < 10:
        raise ValueError(f"capability record needs 10 bytes, got {len(body.data)}")
    d = body.data
    return Capabilities(
        sticks=d[0], triggers=d[1], motors=d[2], turbo=d[3], macros=d[4],
        changekey=d[5], lighting=d[6], eq=d[7], nfc=d[8], sensor=d[9],
    )


LIGHTING_ENTRY_SIZE = 6


@dataclass
class LightingEntry:
    """One lighting record.

    Field order is taken from ZXBTHelper.writeLightingData, which appends
    r, g, b, data4, data5, light per entry and prefixes the whole run with
    entry_count * 6. The two middle bytes are named data4/data5 in the app
    itself and their meaning is genuinely undocumented; they are preserved
    verbatim rather than guessed at.
    """

    r: int
    g: int
    b: int
    data4: int
    data5: int
    light: int

    @classmethod
    def parse(cls, raw: bytes) -> "LightingEntry":
        if len(raw) != LIGHTING_ENTRY_SIZE:
            raise ValueError(f"lighting entry must be {LIGHTING_ENTRY_SIZE} bytes, got {len(raw)}")
        return cls(*raw)

    def to_bytes(self) -> bytes:
        return bytes([self.r, self.g, self.b, self.data4, self.data5, self.light])

    @property
    def rgb(self) -> tuple[int, int, int]:
        return (self.r, self.g, self.b)

    @property
    def hex_colour(self) -> str:
        return f"#{self.r:02x}{self.g:02x}{self.b:02x}"

    def __repr__(self) -> str:
        return (f"LightingEntry({self.hex_colour} light=0x{self.light:02x} "
                f"data4=0x{self.data4:02x} data5=0x{self.data5:02x})")


def parse_lighting(body: "Body") -> list[LightingEntry]:
    """Split a reassembled lighting record into entries.

    Requires the complete record; a chunked read must be concatenated first.
    """
    if not body.complete:
        raise ValueError(f"lighting record incomplete, {body.missing} bytes missing")
    if body.declared % LIGHTING_ENTRY_SIZE:
        raise ValueError(
            f"lighting length {body.declared} is not a multiple of {LIGHTING_ENTRY_SIZE}"
        )
    return [LightingEntry.parse(g) for g in body.groups(LIGHTING_ENTRY_SIZE)]


@dataclass
class DeviceInfo:
    """Decoded reply to READ_VID_PID_VERSION.

    Field extraction follows ZXBTHelper.parsingVidAndPidAndVersion exactly. Note
    that `vid` and `pid` are the vendor's own identifiers rendered as hex strings,
    not USB ids; the USB descriptor advertises a cloned Microsoft identity that
    has nothing to do with these.
    """

    vid: str
    pid: str
    version: str
    device_id: int
    model: int | None = None
    sensor: int | None = None
    version_family: int | None = None

    def __str__(self) -> str:
        parts = [f"vid={self.vid} pid={self.pid} version={self.version} device_id={self.device_id}"]
        if self.model is not None:
            parts.append(f"model={self.model} sensor={self.sensor} family={self.version_family}")
        return "  ".join(parts)


def parse_device_info(payload: bytes) -> DeviceInfo:
    """Decode the payload of a RESPONSE to READ_VID_PID_VERSION.

    The trailing bitfield is only decoded when the payload is exactly 7 bytes.
    The app explicitly skips it otherwise, and we do not guess in its place.
    """
    if len(payload) < 6:
        raise ValueError(f"device info payload too short: {len(payload)} bytes")

    vid = f"{payload[0]:02x}{payload[1]:02x}"
    pid = f"{payload[2]:02x}{payload[3]:02x}"
    # Version is major in bare hex, minor zero-padded to two hex digits.
    version = f"{payload[4]:x}.{payload[5]:02x}"
    device_id = int(pid, 16) >> 12  # top 4 bits of the 16-bit pid

    info = DeviceInfo(vid=vid, pid=pid, version=version, device_id=device_id)

    if len(payload) == 7:
        bits = payload[6]
        info.model = bits & 0x0F
        info.sensor = (bits >> 4) & 0x01
        info.version_family = (bits >> 5) & 0x07
    return info


# Factory reset, guarded by a magic payload. Restores settings only; it does not
# touch firmware. This is the documented recovery path.
RECOVER_MAGIC = bytes([0x03, 0xDF, 0xA9])


def build_recover(*, host: bool = False, serial: int, nonce: int | None = None) -> bytes:
    return build(Op.RECOVER, RECOVER_MAGIC + bytes([2 if host else 1]), serial=serial, nonce=nonce)
