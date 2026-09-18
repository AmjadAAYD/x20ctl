"""Make the pad buzz, so a vibration setting can demonstrate itself.

Everything else in this project reads the pad or writes settings to it over
Bluetooth. This is the one path that makes it *do* something right now:
XInputSetState hands Windows two motor speeds, which is how a game rumbles a
controller.

Two things worth knowing. The pad's vibration setting is a ceiling that games
scale to, so previewing at that percentage is exactly what the setting means.
And this needs the pad present on XInput, which is a different connection from
the Bluetooth config link: a pad paired only to a phone can be configured and
cannot be felt. That is not an error, just nothing to buzz.
"""

from __future__ import annotations

import ctypes
import threading
from ctypes import wintypes

from ..input import XINPUT_STATE, _load_xinput

FULL = 0xFFFF
PULSE_MS = 320


class XINPUT_VIBRATION(ctypes.Structure):
    _fields_ = [
        ("wLeftMotorSpeed", wintypes.WORD),      # low frequency, the heavy one
        ("wRightMotorSpeed", wintypes.WORD),     # high frequency, the light one
    ]


class Rumbler:
    """Pulses the motors at a percentage. Silent when there is no pad."""

    def __init__(self) -> None:
        self._dll = _load_xinput()
        self._timer: threading.Timer | None = None
        self._slot: int | None = None

    @property
    def available(self) -> bool:
        return self._dll is not None and hasattr(self._dll, "XInputSetState")

    def find_slot(self) -> int | None:
        """Whichever XInput slot has a pad on it, or None."""
        if self._dll is None:
            return None
        for slot in range(4):
            state = XINPUT_STATE()
            if self._dll.XInputGetState(slot, ctypes.byref(state)) == 0:
                self._slot = slot
                return slot
        self._slot = None
        return None

    def _set(self, slot: int, left: int, right: int) -> None:
        self._dll.XInputSetState(
            slot, ctypes.byref(XINPUT_VIBRATION(left, right)))

    def pulse(self, percent: int, *, milliseconds: int = PULSE_MS) -> bool:
        """Buzz both motors at `percent` for a moment. False if no pad.

        A short pulse rather than a continuous buzz: you cannot feel a
        difference you are still adjusting, and holding the motors on while
        someone drags a slider is unpleasant.
        """
        if not self.available:
            return False
        slot = self._slot if self._slot is not None else self.find_slot()
        if slot is None:
            return False

        percent = max(0, min(100, percent))
        speed = round(FULL * percent / 100)
        self.stop()
        try:
            self._set(slot, speed, speed)
        except OSError:
            return False

        self._timer = threading.Timer(milliseconds / 1000, self._silence, (slot,))
        self._timer.daemon = True
        self._timer.start()
        return True

    def _silence(self, slot: int) -> None:
        try:
            self._set(slot, 0, 0)
        except OSError:
            pass

    def stop(self) -> None:
        """Cancel any pending pulse and leave the motors off."""
        if self._timer is not None:
            self._timer.cancel()
            self._timer = None
        if self._slot is not None and self.available:
            self._silence(self._slot)
