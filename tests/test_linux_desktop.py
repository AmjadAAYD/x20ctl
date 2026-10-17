"""Linux portability boundaries, exercised without host screen or devices."""
from types import SimpleNamespace

import pytest

from x20ctl import protocol as p
from x20ctl.linux_input import LinuxInputReader, scaled_axis


def info(value, low=0, high=255, flat=0):
    return SimpleNamespace(value=value, min=low, max=high, flat=flat)


def test_axis_ranges_neutral_and_inverted_y():
    assert scaled_axis(info(128)) == 0
    assert scaled_axis(info(0)) == -32768
    assert scaled_axis(info(255)) == 32767
    assert scaled_axis(info(0), invert=True) == 32767
    assert scaled_axis(info(255), invert=True) == -32767
    assert scaled_axis(info(127, flat=4)) == 0
    assert scaled_axis(info(255), trigger=True) == 255
    assert scaled_axis(info(0), trigger=True) == 0
    assert scaled_axis(info(99, 99, 99)) == 0


class Pad:
    def __init__(self, ecodes):
        self.ecodes = ecodes
        self.closed = False
        self.unplugged = False
        self.values = {ecodes.ABS_X: info(255), ecodes.ABS_Y: info(0),
                       ecodes.ABS_RX: info(128), ecodes.ABS_RY: info(128),
                       ecodes.ABS_Z: info(255), ecodes.ABS_RZ: info(0),
                       ecodes.ABS_HAT0X: info(-1, -1, 1), ecodes.ABS_HAT0Y: info(1, -1, 1)}

    def capabilities(self, absinfo=False):
        return {self.ecodes.EV_ABS: list(self.values), self.ecodes.EV_KEY: [self.ecodes.BTN_SOUTH]}

    def read(self):
        if self.unplugged:
            raise OSError("Disconnected")
        raise BlockingIOError()

    def active_keys(self):
        return [self.ecodes.BTN_SOUTH, self.ecodes.BTN_MODE, self.ecodes.BTN_TR2]

    def absinfo(self, code):
        return self.values[code]

    def close(self):
        self.closed = True


def module():
    names = "EV_ABS EV_KEY BTN_SOUTH BTN_EAST BTN_WEST BTN_NORTH BTN_TL BTN_TR BTN_SELECT BTN_START BTN_MODE BTN_THUMBL BTN_THUMBR BTN_TL2 BTN_TR2 BTN_DPAD_UP BTN_DPAD_DOWN BTN_DPAD_LEFT BTN_DPAD_RIGHT ABS_X ABS_Y ABS_RX ABS_RY ABS_Z ABS_RZ ABS_HAT0X ABS_HAT0Y ABS_HAT2X ABS_HAT2Y".split()
    e = SimpleNamespace(**{name: i for i, name in enumerate(names)})
    pad = Pad(e)
    calls = []
    return SimpleNamespace(ecodes=e, list_devices=lambda: calls.append("scan") or ["/fake-pad"], InputDevice=lambda path: pad), pad, calls


def test_lazy_reader_mapping_and_disconnect():
    evdev, pad, calls = module()
    reader = LinuxInputReader(evdev)
    assert calls == []  # Model selection itself never enumerates devices.
    state = reader.poll()
    assert state.left_stick == (32767, 32767)
    assert state.right_stick == (0, 0)
    assert state.left_trigger == state.right_trigger == 255
    assert state.buttons == frozenset({p.Key.A, p.Key.HOME, p.Key.LT, p.Key.RT, p.Key.DPAD_LEFT, p.Key.DPAD_DOWN})
    pad.unplugged = True
    assert reader.poll() is None
    assert pad.closed


def test_input_permission_failure_does_not_request_elevation():
    evdev, _pad, _calls = module()
    def denied(_path):
        raise PermissionError("Unreadable input")
    evdev.InputDevice = denied
    reader = LinuxInputReader(evdev)
    assert reader.poll() is None
    assert reader.poll() is None


def test_xinput_is_unavailable_on_linux(monkeypatch):
    from x20ctl import input
    monkeypatch.setattr(input.sys, "platform", "linux")
    assert not input.XInputReader().available
    assert input.XInputReader().poll() is None
