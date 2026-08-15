"""The macro grid: a column is a step the controller stores."""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest        # noqa: E402

from x20ctl import protocol as p                     # noqa: E402
from x20ctl.gui.macrogrid import (                   # noqa: E402
    BUTTON_ROWS, ROWS, MacroGrid, Step, TooManySteps,
)


def test_every_row_is_something_the_pad_can_put_in_a_macro():
    """A row the hardware cannot express would be a lane that does nothing."""
    for key in BUTTON_ROWS:
        assert p.Key[key] in p.MACRO_MASK_BIT, f"{key} is not macro-capable"


def test_the_rows_include_both_sticks():
    keys = [key for key, _ in ROWS]
    assert keys[:2] == ["LS", "RS"], "a recording usually starts with a stick"


def test_an_empty_column_is_a_gap():
    step = Step()
    assert step.empty
    step.toggle("A")
    assert not step.empty


def test_toggling_twice_puts_it_back():
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.toggle(0, "A")
    assert grid.steps[0].empty


def test_a_column_holds_a_chord():
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.toggle(0, "B")
    steps = grid.to_steps()
    assert steps[0].mask == p.mask_for([p.Key.A, p.Key.B])


def test_drawing_past_the_end_grows_the_grid():
    grid = MacroGrid()
    grid.toggle(4, "A")
    assert len(grid) == 5
    assert grid.steps[0].empty and not grid.steps[4].empty


def test_durations_snap_to_the_controllers_own_resolution():
    """The pad counts in 5 ms ticks, so 33 ms is not a thing it can store."""
    grid = MacroGrid()
    assert grid.set_duration(0, 33) == 35
    assert grid.set_duration(0, 101) == 100
    assert grid.set_duration(0, 0) == 5, "a step of no time is not a step"


def test_a_grid_becomes_the_steps_the_pad_stores():
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.set_duration(0, 100)
    grid.ensure(2)
    grid.set_duration(1, 60)          # an empty column: the gap
    grid.toggle(2, "B")
    grid.set_duration(2, 80)

    steps = grid.to_steps()
    assert len(steps) == 3
    assert steps[0].duration_ms == 100
    assert steps[1].mask == p.MACRO_ANALOG_NEUTRAL, "a gap presses nothing"
    assert steps[1].duration_ms == 60
    assert steps[2].mask == p.mask_for([p.Key.B])


def test_a_gap_is_a_released_step_not_a_zero_mask():
    """A zero mask drives both sticks. That mistake reached hardware once."""
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.ensure(2)
    grid.toggle(2, "B")          # so the gap is interior, not trailing
    steps = grid.to_steps()
    assert steps[1].mask == p.MacroStep.released(60).mask


def test_trailing_gaps_are_dropped():
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.ensure(6)
    assert len(grid.to_steps()) == 1, "the pad would sit through them for nothing"


def test_a_macro_with_no_presses_is_refused():
    grid = MacroGrid()
    grid.ensure(4)
    with pytest.raises(ValueError):
        grid.to_steps()


def test_the_grid_refuses_more_steps_than_the_hardware_holds():
    grid = MacroGrid()
    for column in range(p.MAX_MACRO_ENTRIES + 1):
        grid.toggle(column, "A")
    with pytest.raises(TooManySteps) as caught:
        grid.to_steps()
    assert str(p.MAX_MACRO_ENTRIES) in str(caught.value)


def test_exactly_the_maximum_is_allowed():
    grid = MacroGrid()
    for column in range(p.MAX_MACRO_ENTRIES):
        grid.toggle(column, "A")
    assert len(grid.to_steps()) == p.MAX_MACRO_ENTRIES


def test_a_macro_read_off_the_pad_lands_in_the_grid_unchanged():
    """The round trip that makes the picture trustworthy."""
    original = MacroGrid()
    original.toggle(0, "A")
    original.toggle(0, "B")
    original.set_duration(0, 150)
    original.ensure(2)
    original.set_duration(1, 45)
    original.toggle(2, "LT")

    payload = p.build_macro_payload(original.to_steps(), loop_interval_ms=250)
    program = p.parse_macro_payload(payload)
    rebuilt = MacroGrid.from_program(program)

    assert len(rebuilt) == len(original)
    assert rebuilt.steps[0].keys == {"A", "B"}
    assert rebuilt.steps[0].duration_ms == 150
    assert rebuilt.steps[1].empty and rebuilt.steps[1].duration_ms == 45
    assert rebuilt.steps[2].keys == {"LT"}
    assert rebuilt.loop_ms == 250


def test_the_grid_can_say_how_long_the_whole_thing_runs():
    grid = MacroGrid()
    grid.toggle(0, "A")
    grid.set_duration(0, 100)
    grid.toggle(1, "B")
    grid.set_duration(1, 200)
    assert grid.total_ms() == 300


def test_clearing_empties_the_slot_and_forgets_the_loop():
    grid = MacroGrid(loop_ms=500)
    grid.toggle(0, "A")
    grid.clear()
    assert len(grid) == 0 and grid.loop_ms == 0
