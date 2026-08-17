"""Read the controller's live input, and record it into a macro.

The pad presents itself as an Xbox controller, so XInput gives us a documented
button bitmask. A raw HID report would mean guessing at the bit layout.
Entirely read-only: XInputGetState asks the driver for the current state and
changes nothing.

    reader = XInputReader()
    recorder = MacroRecorder(reader)
    recorder.start()
    ... user presses buttons ...
    spec = recorder.stop()

Recording quantises to the protocol's 5 ms grid, which is finer than anyone can
press a button, so a performance survives the conversion intact.

Both buttons and the left stick are captured. The stick is bucketed to the
pad's eight compass headings because that is all a macro step can store, so
what comes back is not exactly what was played; see stick_direction.
"""

from __future__ import annotations

import ctypes
import math
import time
from ctypes import wintypes
from dataclasses import dataclass, field

from . import protocol as p

# XInput button flags, from the Windows SDK.
XINPUT_BUTTONS = {
    0x0001: p.Key.DPAD_UP,
    0x0002: p.Key.DPAD_DOWN,
    0x0004: p.Key.DPAD_LEFT,
    0x0008: p.Key.DPAD_RIGHT,
    0x0010: p.Key.START,
    0x0020: p.Key.SELECT,
    0x0040: p.Key.L3,
    0x0080: p.Key.R3,
    0x0100: p.Key.LB,
    0x0200: p.Key.RB,
    # 0x0400 is the Guide/Home button. It is NOT in the public SDK header and
    # XInputGetState deliberately masks it out, which is why the Test page never
    # showed Home. It only arrives through XInputGetStateEx; see _load_xinput.
    0x0400: p.Key.HOME,
    0x1000: p.Key.A,
    0x2000: p.Key.B,
    0x4000: p.Key.X,
    0x8000: p.Key.Y,
}

XINPUT_GAMEPAD_GUIDE = 0x0400

# XInputGetStateEx has no exported name, only ordinal 100. Present in
# XInput1_3.dll and XInput1_4.dll, absent from XInput9_1_0.dll.
XINPUT_GETSTATE_EX_ORDINAL = 100

# Triggers are analog; treat them as pressed past the standard threshold.
TRIGGER_THRESHOLD = 30

RECORD_TICK_MS = 5          # the protocol's own resolution
MAX_STEPS = 60              # keeps a recording inside a sane number of packets

# XInput's own documented left-stick deadzone. Below it the stick counts as
# untouched, which in a macro means "no input from the macro" and NOT "stick
# centred": the format cannot express centred at all, so on any step that names
# no direction the player's own thumb passes through.
LEFT_STICK_DEADZONE = 7849


def stick_direction(x: int, y: int) -> "p.Direction | None":
    """XInput thumb coordinates to one of the pad's eight compass headings.

    Direction runs clockwise from up, so the angle is measured from +Y toward
    +X. Returns None inside the deadzone.

    The bucket is lossy and that is the hardware: a stick held at 30 degrees
    comes back as 45. Measured on real recordings, wave dashes survive it
    (worst error 15.9 degrees, side dashes within 3.4) while a speed flip does
    not, which is the whole difference between the two.
    """
    if math.hypot(x, y) < LEFT_STICK_DEADZONE:
        return None
    angle = math.degrees(math.atan2(x, y)) % 360
    return p.Direction((round(angle / 45) % 8) + 1)


def _input_order(item) -> tuple:
    """Buttons first in key order, then the stick. Matches how macros read."""
    if isinstance(item, p.StickInput):
        return (1, int(item.direction))
    return (0, int(item))


class XINPUT_GAMEPAD(ctypes.Structure):
    _fields_ = [
        ("wButtons", wintypes.WORD),
        ("bLeftTrigger", ctypes.c_ubyte),
        ("bRightTrigger", ctypes.c_ubyte),
        ("sThumbLX", ctypes.c_short),
        ("sThumbLY", ctypes.c_short),
        ("sThumbRX", ctypes.c_short),
        ("sThumbRY", ctypes.c_short),
    ]


class XINPUT_STATE(ctypes.Structure):
    _fields_ = [("dwPacketNumber", wintypes.DWORD), ("Gamepad", XINPUT_GAMEPAD)]


def _load_xinput():
    for name in ("XInput1_4.dll", "xinput1_3.dll", "XInput9_1_0.dll"):
        try:
            return ctypes.WinDLL(name)
        except OSError:
            continue
    return None


def _get_state_fn(dll):
    """Prefer XInputGetStateEx, which reports the Guide/Home button.

    A user asked why Home never appears in the Test page. The answer is that the
    documented XInputGetState clears bit 0x0400 before returning, so Home is
    invisible through it no matter what the pad sends. The undocumented
    XInputGetStateEx does not, and is reachable only by ordinal 100.

    Returns (callable, reports_home). Falls back to XInputGetState where the
    ordinal is missing, e.g. XInput9_1_0.dll, in which case Home genuinely
    cannot be seen and the UI should say so rather than look broken.
    """
    if dll is None:
        return None, False
    try:
        return dll[XINPUT_GETSTATE_EX_ORDINAL], True
    except Exception:
        return getattr(dll, "XInputGetState", None), False


@dataclass
class GamepadState:
    slot: int
    packet: int
    buttons: frozenset
    left_trigger: int
    right_trigger: int
    left_stick: tuple[int, int]
    right_stick: tuple[int, int]

    @property
    def pressed_names(self) -> list[str]:
        order = [k.name for k in p.MACRO_MASK_BIT]
        return sorted((k.name for k in self.buttons),
                      key=lambda n: order.index(n) if n in order else 99)

    @property
    def macro_keys(self) -> list:
        """Only the buttons this pad can actually put in a macro."""
        return [k for k in self.buttons if k in p.MACRO_MASK_BIT]

    @property
    def left_direction(self) -> "p.Direction | None":
        return stick_direction(*self.left_stick)

    @property
    def macro_inputs(self) -> list:
        """Everything here a macro step can store: buttons plus a stick heading.

        Recording buttons alone cannot capture a wave dash, which is mostly
        stick. Use this rather than macro_keys for anything that replays a
        performance.
        """
        out = list(self.macro_keys)
        direction = self.left_direction
        if direction is not None:
            out.append(p.StickInput(stick=p.Key.LSTICK_ANALOG,
                                    direction=direction))
        return out


class XInputReader:
    """Polls whichever XInput slot has a controller on it."""

    def __init__(self) -> None:
        self._dll = _load_xinput()
        self._get_state, self.reports_home = _get_state_fn(self._dll)
        self._slot: int | None = None

    @property
    def available(self) -> bool:
        return self._dll is not None and self._get_state is not None

    def poll(self) -> GamepadState | None:
        if self._get_state is None:
            return None

        slots = [self._slot] if self._slot is not None else range(4)
        for slot in slots:
            state = XINPUT_STATE()
            if self._get_state(slot, ctypes.byref(state)) != 0:
                continue    # 1167 == ERROR_DEVICE_NOT_CONNECTED
            self._slot = slot
            pad = state.Gamepad

            buttons = {key for flag, key in XINPUT_BUTTONS.items()
                       if pad.wButtons & flag}
            if pad.bLeftTrigger > TRIGGER_THRESHOLD:
                buttons.add(p.Key.LT)
            if pad.bRightTrigger > TRIGGER_THRESHOLD:
                buttons.add(p.Key.RT)

            return GamepadState(
                slot=slot,
                packet=state.dwPacketNumber,
                buttons=frozenset(buttons),
                left_trigger=pad.bLeftTrigger,
                right_trigger=pad.bRightTrigger,
                left_stick=(pad.sThumbLX, pad.sThumbLY),
                right_stick=(pad.sThumbRX, pad.sThumbRY),
            )
        self._slot = None
        return None


def _quantise(ms: float) -> int:
    """Round to the protocol's 5 ms grid, never to zero."""
    return max(RECORD_TICK_MS, int(round(ms / RECORD_TICK_MS)) * RECORD_TICK_MS)


@dataclass
class RecordedStep:
    keys: list
    duration_ms: int

    @property
    def label(self) -> str:
        if not self.keys:
            return f"pause {self.duration_ms}ms"
        return "+".join(k.name for k in self.keys) + f" {self.duration_ms}ms"


@dataclass
class MacroRecorder:
    """Turns live button presses into macro steps.

    Only buttons the pad can put in a macro are captured; pressing Start or Home
    while recording is ignored rather than producing a macro that can't be
    written.
    """

    reader: XInputReader
    trim_leading_pause: bool = True
    steps: list[RecordedStep] = field(default_factory=list)
    _last: frozenset | None = None
    _changed_at: float = 0.0
    _recording: bool = False
    ignored: set = field(default_factory=set)

    @property
    def recording(self) -> bool:
        return self._recording

    def start(self) -> None:
        self.steps.clear()
        self.ignored.clear()
        self._last = None
        self._changed_at = time.perf_counter()
        self._recording = True

    def poll(self) -> None:
        """Sample once. Call repeatedly while recording."""
        if not self._recording:
            return
        state = self.reader.poll()
        if state is None:
            return

        for key in state.buttons:
            if key not in p.MACRO_MASK_BIT:
                self.ignored.add(key.name)

        current = frozenset(state.macro_inputs)
        if self._last is None:
            self._last = current
            self._changed_at = time.perf_counter()
            return

        if current == self._last:
            return

        now = time.perf_counter()
        held = _quantise((now - self._changed_at) * 1000)
        if len(self.steps) < MAX_STEPS:
            self.steps.append(
                RecordedStep(keys=sorted(self._last, key=_input_order),
                             duration_ms=held))
        self._last = current
        self._changed_at = now

    def stop(self):
        """Finish and return a MacroSpec, or None if nothing usable was caught."""
        from .profiles import MacroSpec

        self._recording = False
        if self._last:
            held = _quantise((time.perf_counter() - self._changed_at) * 1000)
            if len(self.steps) < MAX_STEPS:
                self.steps.append(
                    RecordedStep(keys=sorted(self._last, key=_input_order),
                                 duration_ms=held))

        steps = list(self.steps)
        if self.trim_leading_pause:
            while steps and not steps[0].keys:
                steps.pop(0)
        while steps and not steps[-1].keys:
            steps.pop()
        if not steps:
            return None

        # Keep the timing that was actually played. The protocol stores a
        # duration per step, so averaging them into a single hold and gap, which
        # an earlier version did, threw away the whole point of recording.
        parts: list[str] = []
        index = 0
        while index < len(steps):
            step = steps[index]
            if not step.keys:
                index += 1
                continue
            names = "+".join(k.name for k in step.keys)
            gap = 0
            if index + 1 < len(steps) and not steps[index + 1].keys:
                gap = steps[index + 1].duration_ms
            parts.append(f"{names}:{step.duration_ms}/{gap}" if gap
                         else f"{names}:{step.duration_ms}")
            index += 2

        if not parts:
            return None
        return MacroSpec(keys=",".join(parts), hold_ms=100, gap_ms=60, loop_ms=0)

    def summary(self) -> str:
        if not self.steps:
            return "nothing recorded yet"
        parts = [s.label for s in self.steps[-4:]]
        prefix = "… " if len(self.steps) > 4 else ""
        return prefix + "  ".join(parts)
