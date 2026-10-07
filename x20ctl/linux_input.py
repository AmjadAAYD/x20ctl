"""Read-only Linux gamepad adapter using standard kernel input mappings.

No grabs, force-feedback writes, synthetic events or permission changes.
Enumeration is deferred until the X20 workspace requests live input.
"""
from __future__ import annotations

import time

from . import protocol as p
from .input import GamepadState, TRIGGER_THRESHOLD


def scaled_axis(info, *, invert=False, trigger=False) -> int:
    if info is None or info.max <= info.min:
        return 0
    fraction = max(0.0, min(1.0, (info.value - info.min) / (info.max - info.min)))
    if trigger:
        return round(fraction * 255)
    center = (info.max + info.min) / 2
    # Preserve the kernel dead zone and an exact neutral value on unsigned axes.
    value = 0 if abs(info.value - center) <= max(getattr(info, "flat", 0), 0.5) else round(fraction * 65535 - 32768)
    return max(-32768, min(32767, -value if invert else value))


class LinuxInputReader:
    def __init__(self, module=None):
        if module is None:
            try:
                import evdev as module
            except ImportError:
                pass
        self._module = module
        self._device = None
        self._axes = set()
        self._next_scan = 0.0
        self._packet = 0
        self.reports_home = True

    @property
    def available(self):
        return self._module is not None

    def close(self):
        if self._device is not None:
            self._device.close()
            self._device = None

    def _discover(self):
        if not self.available or time.monotonic() < self._next_scan:
            return
        self._next_scan = time.monotonic() + 2
        e = self._module.ecodes
        for path in sorted(self._module.list_devices()):
            device = None
            try:
                device = self._module.InputDevice(path)
                capabilities = device.capabilities(absinfo=False)
                axes = set(capabilities.get(e.EV_ABS, []))
                if e.BTN_SOUTH in capabilities.get(e.EV_KEY, []) and {e.ABS_X, e.ABS_Y} <= axes:
                    self._device, self._axes = device, axes
                    return
            except OSError:
                # Inaccessible input devices remain inaccessible; no elevation.
                pass
            if device is not None:
                device.close()

    def poll(self) -> GamepadState | None:
        if self._device is None:
            self._discover()
        if self._device is None:
            return None
        e = self._module.ecodes
        device = self._device
        try:
            # Nonblocking drain, then query current state. This also recovers
            # from SYN_DROPPED without retaining stale pressed buttons.
            try:
                for _ in device.read():
                    self._packet += 1
            except BlockingIOError:
                pass
            keys = set(device.active_keys())

            def axis(*names, invert=False, trigger=False):
                for name in names:
                    code = getattr(e, name, None)
                    if code in self._axes:
                        return scaled_axis(device.absinfo(code), invert=invert, trigger=trigger)
                return 0

            mapping = {
                "BTN_SOUTH": p.Key.A, "BTN_EAST": p.Key.B,
                "BTN_WEST": p.Key.X, "BTN_NORTH": p.Key.Y,
                "BTN_TL": p.Key.LB, "BTN_TR": p.Key.RB,
                "BTN_SELECT": p.Key.SELECT, "BTN_START": p.Key.START,
                "BTN_MODE": p.Key.HOME, "BTN_THUMBL": p.Key.L3, "BTN_THUMBR": p.Key.R3,
                "BTN_DPAD_UP": p.Key.DPAD_UP, "BTN_DPAD_DOWN": p.Key.DPAD_DOWN,
                "BTN_DPAD_LEFT": p.Key.DPAD_LEFT, "BTN_DPAD_RIGHT": p.Key.DPAD_RIGHT,
            }
            buttons = {key for name, key in mapping.items() if getattr(e, name, None) in keys}
            # HAT2 follows the kernel gamepad spec. Z/RZ are the common xpad
            # legacy layout; only use reported axes, never vendor HID packets.
            lt = axis("ABS_HAT2Y", "ABS_Z", "ABS_BRAKE", trigger=True)
            rt = axis("ABS_HAT2X", "ABS_RZ", "ABS_GAS", trigger=True)
            if getattr(e, "BTN_TL2", None) in keys:
                lt = 255
            if getattr(e, "BTN_TR2", None) in keys:
                rt = 255
            if lt > TRIGGER_THRESHOLD:
                buttons.add(p.Key.LT)
            if rt > TRIGGER_THRESHOLD:
                buttons.add(p.Key.RT)
            for name, negative, positive in (
                ("ABS_HAT0X", p.Key.DPAD_LEFT, p.Key.DPAD_RIGHT),
                ("ABS_HAT0Y", p.Key.DPAD_UP, p.Key.DPAD_DOWN),
            ):
                code = getattr(e, name, None)
                if code in self._axes:
                    value = device.absinfo(code).value
                    if value:
                        buttons.add(negative if value < 0 else positive)
            return GamepadState(0, self._packet, frozenset(buttons), lt, rt,
                                (axis("ABS_X"), axis("ABS_Y", invert=True)),
                                (axis("ABS_RX"), axis("ABS_RY", invert=True)))
        except OSError:
            self.close()
            self._next_scan = 0.0
            return None
