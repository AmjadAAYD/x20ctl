"""Opt-in bounded standard XInput motor test; no vendor reports or settings writes."""

import ctypes
import math
import time


class Adapter:
    def __init__(self):
        from x20ctl.scanning.windows import XInput

        self.input = XInput()
        if self.input.fn is None:
            raise OSError("Standard XInput API unavailable")

    def state(self, slot):
        return self.input.state(slot)

    def set_state(self, slot, left, right):
        class Motors(ctypes.Structure):
            _fields_ = [("left", ctypes.c_ushort), ("right", ctypes.c_ushort)]

        fn = self.input.dll.XInputSetState
        fn.argtypes = [ctypes.c_uint32, ctypes.POINTER(Motors)]
        fn.restype = ctypes.c_uint32
        return fn(slot, ctypes.byref(Motors(left, right)))


def stop(slot, adapter=None):
    if type(slot) is not int or not 0 <= slot <= 3:
        raise ValueError("Invalid motor-stop slot")
    if (adapter or Adapter()).set_state(slot, 0, 0) != 0:
        raise OSError("Motor stop was not acknowledged; disconnect the controller")


def probe(slot, left, right, duration, confirmed, adapter=None, sleep=time.sleep):
    if (
        confirmed is not True
        or type(slot) is not int
        or not 0 <= slot <= 3
        or any(
            type(v) not in (int, float) or not math.isfinite(v) or not 0 <= v <= 1
            for v in (left, right)
        )
        or type(duration) not in (int, float)
        or not math.isfinite(duration)
        or not 0 < duration <= 0.5
    ):
        raise ValueError("Unapproved or unbounded XInput rumble request")
    adapter = adapter or Adapter()
    if adapter.state(slot) is None:
        raise OSError("Selected XInput slot disconnected")
    try:
        status = adapter.set_state(slot, round(left * 65535), round(right * 65535))
        if status != 0:
            raise OSError("XInput rumble unavailable")
        sleep(duration)
        return {
            "api": "XInputSetState",
            "slot": slot,
            "left": left,
            "right": right,
            "duration": duration,
            "apiAccepted": True,
            "physicalFeedbackVerified": False,
            "configurationWritten": False,
        }
    finally:
        if adapter.set_state(slot, 0, 0) != 0:
            raise OSError("Motor stop was not acknowledged; disconnect the controller")
