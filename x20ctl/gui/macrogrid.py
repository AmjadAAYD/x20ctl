"""The macro grid, and what a column of it means.

One column is one step the controller stores. Not a bar of music, not a
translation through the text syntax: the same mask-and-duration pair that goes
over the wire. That keeps the picture honest, so what you drew is what the pad
holds, and a macro read back off a controller lands in the grid unchanged.

A column with nothing selected is a gap, which is exactly how the hardware
expresses "nothing pressed for this long".
"""

from __future__ import annotations

from dataclasses import dataclass, field

from .. import protocol as p

# The rows, top to bottom. Sticks first because a recorded movement usually
# starts with one, then the buttons in the order they sit on the pad.
ROWS = (
    ("LS", "Left stick"),
    ("RS", "Right stick"),
    ("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"),
    ("LB", "LB"), ("RB", "RB"), ("LT", "LT"), ("RT", "RT"),
    ("L3", "L3"), ("R3", "R3"),
    ("DPAD_UP", "D-pad up"), ("DPAD_DOWN", "D-pad down"),
    ("DPAD_LEFT", "D-pad left"), ("DPAD_RIGHT", "D-pad right"),
    ("SELECT", "Select"), ("START", "Start"),
)

BUTTON_ROWS = tuple(key for key, _ in ROWS if key not in ("LS", "RS"))
DEFAULT_STEP_MS = 100
DEFAULT_GAP_MS = 60
STEP_GRID_MS = 5            # the controller's own resolution


class TooManySteps(ValueError):
    """More steps than the controller can store."""


@dataclass
class Step:
    """One column: what is held down, and for how long."""

    keys: set = field(default_factory=set)
    duration_ms: int = DEFAULT_STEP_MS

    @property
    def empty(self) -> bool:
        return not self.keys

    def toggle(self, key: str) -> None:
        if key in self.keys:
            self.keys.discard(key)
        else:
            self.keys.add(key)


@dataclass
class MacroGrid:
    """A sequence of steps for one slot."""

    steps: list = field(default_factory=list)
    loop_ms: int = 0

    def __len__(self) -> int:
        return len(self.steps)

    @property
    def empty(self) -> bool:
        return not any(not step.empty for step in self.steps)

    def add_step(self, duration_ms: int = DEFAULT_STEP_MS) -> Step:
        step = Step(duration_ms=duration_ms)
        self.steps.append(step)
        return step

    def ensure(self, count: int) -> None:
        """Grow the grid so at least `count` columns exist."""
        while len(self.steps) < count:
            self.add_step()

    def toggle(self, column: int, key: str) -> None:
        self.ensure(column + 1)
        self.steps[column].toggle(key)

    def set_duration(self, column: int, milliseconds: int) -> int:
        """Set a column's length, snapped to the controller's 5 ms grid."""
        self.ensure(column + 1)
        snapped = max(STEP_GRID_MS,
                      round(milliseconds / STEP_GRID_MS) * STEP_GRID_MS)
        self.steps[column].duration_ms = snapped
        return snapped

    def trim(self) -> None:
        """Drop trailing gaps, which the pad would sit through for nothing."""
        while self.steps and self.steps[-1].empty:
            self.steps.pop()

    def clear(self) -> None:
        self.steps.clear()
        self.loop_ms = 0

    # -- the hardware ----------------------------------------------------

    def to_steps(self) -> list:
        """Turn the grid into MacroSteps, ready for build_macro_payload."""
        self.trim()
        if self.empty:
            raise ValueError("this macro has no presses in it")
        if len(self.steps) > p.MAX_MACRO_ENTRIES:
            raise TooManySteps(
                f"a macro holds at most {p.MAX_MACRO_ENTRIES} steps and this "
                f"has {len(self.steps)}")

        out = []
        for step in self.steps:
            if step.empty:
                out.append(p.MacroStep.released(step.duration_ms))
                continue
            mask = p.mask_for([p.parse_token(key) for key in sorted(step.keys)])
            out.append(p.MacroStep(mask=mask, duration_ms=step.duration_ms))
        return out

    @classmethod
    def from_program(cls, program) -> "MacroGrid":
        """Rebuild the grid from a macro read off the controller."""
        grid = cls(loop_ms=getattr(program, "loop_interval_ms", 0))
        for step in getattr(program, "steps", []):
            names = p.describe_mask(step.mask)
            grid.steps.append(Step(keys=set(names),
                                   duration_ms=step.duration_ms))
        return grid

    def total_ms(self) -> int:
        return sum(step.duration_ms for step in self.steps)
