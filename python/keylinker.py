"""
KeyLinker / EasySMX X20 Bluetooth GATT Protocol & Packet Serialization
Python implementation for x20ctl

Implements the BLE GATT protocol, packet framing, CRC-8 checksum,
and payload builders for:
 - Button Remapping (Opcode 0x03)
 - Macro Sequencer for M1-M4 (Opcode 0x05)
 - Stick and Trigger Response Curves (Opcode 0x08)
 - Dual-Motor Vibration (Opcode 0x06)
 - Power & Idle Timeout (Opcode 0x07)
 - Query Info & Battery (Opcodes 0x01, 0x0A)
"""

import sys
import time
from enum import IntEnum
from typing import Dict, List, Optional, Tuple, Any

# Standard KeyLinker BLE GATT Service and Characteristic UUIDs
KEYLINKER_SERVICE_UUID = "0000ffe0-0000-1000-8000-00805f9b34fb"
KEYLINKER_CHAR_NOTIFY_UUID = "0000ffe1-0000-1000-8000-00805f9b34fb"
KEYLINKER_CHAR_WRITE_UUID = "0000ffe2-0000-1000-8000-00805f9b34fb"

# Alternate Pulsenet GATT UUIDs (found in firmware captures)
PULSENET_SERVICE_UUID = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
PULSENET_CHAR_WRITE_UUID = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
PULSENET_CHAR_NOTIFY_UUID = "d7f010e2-660d-46e9-96c3-19c4148bdab5"


class PacketOpcode(IntEnum):
    QUERY_INFO = 0x01
    READ_BUTTONS = 0x02
    WRITE_BUTTONS = 0x03
    READ_MACROS = 0x04
    WRITE_MACROS = 0x05
    SET_VIBRATION = 0x06
    SET_POWER_TIMEOUT = 0x07
    WRITE_CURVES = 0x08
    FACTORY_RESET = 0x09
    QUERY_BATTERY = 0x0A


class KeyCode(IntEnum):
    DPAD_UP = 0x0001
    DPAD_DOWN = 0x0002
    DPAD_LEFT = 0x0004
    DPAD_RIGHT = 0x0008
    A = 0x0010
    B = 0x0020
    X = 0x0040
    Y = 0x0080
    LB = 0x0100
    RB = 0x0200
    LT = 0x0400
    RT = 0x0800
    L3 = 0x1000
    R3 = 0x2000
    SELECT = 0x4000
    START = 0x8000
    CAPTURE = 0x010000
    TURBO = 0x020000


KEY_NAMES = [
    "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT",
    "A", "B", "X", "Y",
    "LB", "RB", "LT", "RT",
    "L3", "R3", "SELECT", "START",
    "CAPTURE", "TURBO"
]

KEY_LABELS: Dict[str, str] = {
    "DPAD_UP": "D-pad Up",
    "DPAD_DOWN": "D-pad Down",
    "DPAD_LEFT": "D-pad Left",
    "DPAD_RIGHT": "D-pad Right",
    "A": "A",
    "B": "B",
    "X": "X",
    "Y": "Y",
    "LB": "LB (Left Bumper)",
    "RB": "RB (Right Bumper)",
    "LT": "LT (Left Trigger)",
    "RT": "RT (Right Trigger)",
    "L3": "L3 (Left Stick Click)",
    "R3": "R3 (Right Stick Click)",
    "SELECT": "Select / Back",
    "START": "Start / Menu",
    "CAPTURE": "C (Capture)",
    "TURBO": "T (Turbo)",
}


class StickDirection(IntEnum):
    NEUTRAL = 0
    UP = 1
    UP_RIGHT = 2
    RIGHT = 3
    DOWN_RIGHT = 4
    DOWN = 5
    DOWN_LEFT = 6
    LEFT = 7
    UP_LEFT = 8


# CRC-8 computation (Polynomial: 0x07 / 0x31 depending on implementation,
# KeyLinker uses simple sum modulo 256 for basic framing, and CRC-8 for extended payloads)
def calculate_checksum(data: bytes) -> int:
    """Calculates single-byte additive checksum for framing."""
    return sum(data) & 0xFF


def calculate_crc8(data: bytes, poly: int = 0x07, init: int = 0x00) -> int:
    """KeyLinker CRC-8 implementation over packet bytes."""
    crc = init
    for byte in data:
        crc ^= byte
        for _ in range(8):
            if crc & 0x80:
                crc = ((crc << 1) ^ poly) & 0xFF
            else:
                crc = (crc << 1) & 0xFF
    return crc


def build_keylinker_packet(opcode: int, payload: bytes = b"") -> bytes:
    """
    Builds a standard KeyLinker packet:
    [Header: 0xAA, 0x55, Opcode, Length, ...Payload, Checksum]
    """
    header = bytes([0xAA, 0x55, opcode, len(payload)])
    data = header + payload
    # Checksum is computed over opcode, length and payload
    checksum = calculate_checksum(data[2:])
    return data + bytes([checksum])


def parse_keylinker_packet(raw_bytes: bytes) -> Optional[Tuple[int, bytes]]:
    """Validates and extracts opcode and payload from a raw packet."""
    if len(raw_bytes) < 5:
        return None
    if raw_bytes[0] != 0xAA or raw_bytes[1] != 0x55:
        return None
    opcode = raw_bytes[2]
    length = raw_bytes[3]
    if len(raw_bytes) < 4 + length + 1:
        return None
    payload = raw_bytes[4 : 4 + length]
    checksum = raw_bytes[4 + length]
    expected_checksum = calculate_checksum(raw_bytes[2 : 4 + length])
    if checksum != expected_checksum:
        return None
    return opcode, payload


# Payload builders for controller configuration
def build_remap_payload(remaps: Dict[str, str]) -> bytes:
    """
    Encodes key remapping table into KeyLinker byte sequence.
    Each 2-byte slot specifies the mapped KeyCode.
    """
    payload = bytearray(36)
    for idx, key in enumerate(KEY_NAMES):
        target_name = remaps.get(key, key)
        code = getattr(KeyCode, target_name, getattr(KeyCode, key, 0))
        # 16-bit little-endian
        payload[idx * 2] = code & 0xFF
        payload[idx * 2 + 1] = (code >> 8) & 0xFF
    return bytes(payload)


def build_vibration_payload(intensity_percent: int) -> bytes:
    """
    Builds vibration adjustment payload (0 - 100%).
    Byte 0: Left motor, Byte 1: Right motor.
    """
    val = max(0, min(100, int(intensity_percent)))
    # Scale 0-100 to 0-255
    motor_val = int((val / 100.0) * 255)
    return bytes([motor_val, motor_val])


def build_power_timeout_payload(minutes: int) -> bytes:
    """
    Builds sleep/idle shutdown timer payload in minutes (0 = never, 1-30 min).
    """
    val = max(0, min(60, int(minutes)))
    return bytes([val])


def build_curve_payload(
    left_stick: Dict[str, Any],
    right_stick: Dict[str, Any],
    left_trigger: Dict[str, Any],
    right_trigger: Dict[str, Any],
) -> bytes:
    """
    Encodes stick and trigger curve settings:
    Inner deadzone, outer deadzone, point1 (x, y), point2 (x, y).
    """
    payload = bytearray(24)

    def pack_curve(curve_dict: Dict[str, Any], offset: int):
        inner = int(curve_dict.get("innerDeadzone", 0))
        outer = int(curve_dict.get("outerDeadzone", 100))
        p1_x = int(curve_dict.get("p1X", 30))
        p1_y = int(curve_dict.get("p1Y", 30))
        p2_x = int(curve_dict.get("p2X", 70))
        p2_y = int(curve_dict.get("p2Y", 70))
        payload[offset + 0] = max(0, min(100, inner))
        payload[offset + 1] = max(0, min(100, outer))
        payload[offset + 2] = max(0, min(100, p1_x))
        payload[offset + 3] = max(0, min(100, p1_y))
        payload[offset + 4] = max(0, min(100, p2_x))
        payload[offset + 5] = max(0, min(100, p2_y))

    pack_curve(left_stick, 0)
    pack_curve(right_stick, 6)
    pack_curve(left_trigger, 12)
    pack_curve(right_trigger, 18)

    return bytes(payload)


def build_macro_payload(paddle: str, steps: List[Dict[str, Any]]) -> bytes:
    """
    Encodes up to 16 macro steps for rear paddles (M1, M2, M3, M4).
    Each step: buttons mask (2 bytes), stick direction (1 byte),
    hold duration (1 byte, 5ms units), gap duration (1 byte, 5ms units).
    """
    paddle_ids = {"M1": 0x01, "M2": 0x02, "M3": 0x03, "M4": 0x04}
    paddle_byte = paddle_ids.get(paddle, 0x01)

    payload = bytearray()
    payload.append(paddle_byte)
    step_count = min(len(steps), 16)
    payload.append(step_count)

    for step in steps[:16]:
        # Buttons bitmask
        btn_mask = 0
        for btn in step.get("buttons", []):
            if hasattr(KeyCode, btn):
                btn_mask |= getattr(KeyCode, btn)
        payload.append(btn_mask & 0xFF)
        payload.append((btn_mask >> 8) & 0xFF)

        # Stick directions
        ls_dir = int(step.get("leftStick", StickDirection.NEUTRAL))
        rs_dir = int(step.get("rightStick", StickDirection.NEUTRAL))
        stick_byte = (ls_dir & 0x0F) | ((rs_dir & 0x0F) << 4)
        payload.append(stick_byte)

        # Timing (in 5ms intervals, capped to 255 -> ~1275ms per step)
        hold_units = max(1, min(255, int(step.get("durationMs", 50) // 5)))
        gap_units = max(1, min(255, int(step.get("intervalMs", 50) // 5)))
        payload.append(hold_units)
        payload.append(gap_units)

    return bytes(payload)
