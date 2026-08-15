"""The macro editor: four slots, drawable columns, and what apply does."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication              # noqa: E402

from x20ctl import protocol as p                         # noqa: E402
from x20ctl.gui.macrogrid import ROWS, MacroGrid         # noqa: E402
from x20ctl.gui.macros import (                          # noqa: E402
    APPLY_FEEDBACK_PERCENT, SLOTS, MacroEditor,
)

app = QApplication.instance() or QApplication([])


class FakeRumbler:
    def __init__(self):
        self.pulses = []

    def pulse(self, percent, **kw):
        self.pulses.append(percent)
        return True

    def stop(self):
        pass


def test_the_four_slots_are_independent():
    """M1 is alone, M2 is alone. Drawing on one must not touch another."""
    editor = MacroEditor()
    editor.show_slot("M1")
    editor.current.toggle(0, "A")

    editor.show_slot("M2")
    assert editor.current.steps[0].empty

    editor.show_slot("M1")
    assert editor.current.steps[0].keys == {"A"}


def test_every_slot_has_a_tab():
    editor = MacroEditor()
    assert set(editor.tabs) == set(SLOTS)


def test_choosing_a_slot_checks_only_that_tab():
    editor = MacroEditor()
    editor.show_slot("M3")
    checked = [name for name, b in editor.tabs.items() if b.isChecked()]
    assert checked == ["M3"]


def test_clicking_a_cell_holds_that_input_for_that_step():
    editor = MacroEditor()
    editor.cells[(0, 2)].click()          # column 0, third row
    assert not editor.current.steps[0].empty


def test_a_cell_shows_what_the_grid_already_holds():
    editor = MacroEditor()
    grid = MacroGrid()
    grid.toggle(0, "A")
    editor.load("M1", grid)
    row = [key for key, _ in ROWS].index("A")
    assert editor.cells[(0, row)].isChecked()


def test_the_summary_counts_presses_and_says_how_long_it_runs():
    editor = MacroEditor()
    editor.current.toggle(0, "A")
    editor.current.set_duration(0, 200)
    editor.refresh_summary()
    assert "1 press" in editor.summary.text()
    assert "s" in editor.summary.text()


def test_an_empty_slot_says_so_rather_than_showing_zeroes():
    editor = MacroEditor()
    assert editor.summary.text() == "empty"


def test_applying_an_empty_slot_is_refused_with_a_reason():
    editor = MacroEditor()
    seen = []
    editor.apply_requested.connect(lambda *a: seen.append(a))
    assert editor.apply() is False
    assert "no presses" in editor.status.text()
    assert seen == []


def test_applying_hands_over_the_slot_and_the_grid():
    editor = MacroEditor()
    editor.current.toggle(0, "A")
    seen = []
    editor.apply_requested.connect(lambda slot, grid: seen.append((slot, grid)))

    assert editor.apply() is True
    assert seen[0][0] == "M1"
    assert seen[0][1] is editor.grids["M1"]


def test_applying_buzzes_the_pad_so_you_know_it_landed():
    """Amjad's ask: a faint nudge when a macro is written."""
    rumbler = FakeRumbler()
    editor = MacroEditor(rumbler)
    editor.current.toggle(0, "A")
    editor.apply()
    assert rumbler.pulses == [APPLY_FEEDBACK_PERCENT]


def test_a_refused_apply_does_not_buzz():
    rumbler = FakeRumbler()
    editor = MacroEditor(rumbler)
    editor.apply()
    assert rumbler.pulses == []


def test_too_many_steps_is_reported_before_anything_is_sent():
    editor = MacroEditor()
    for column in range(p.MAX_MACRO_ENTRIES + 2):
        editor.current.toggle(column, "A")
    seen = []
    editor.apply_requested.connect(lambda *a: seen.append(a))

    assert editor.apply() is False
    assert str(p.MAX_MACRO_ENTRIES) in editor.status.text()
    assert seen == []


def test_durations_snap_when_typed():
    editor = MacroEditor()
    editor._on_duration(0, 33)
    assert editor.current.steps[0].duration_ms == 35
    assert editor.durations[0].value() == 35


def test_repeat_is_off_until_asked_for():
    editor = MacroEditor()
    assert not editor.loop_on.isChecked()
    assert editor.current.loop_ms == 0
    assert not editor.loop_ms.isEnabled()


def test_turning_repeat_on_stores_the_interval():
    editor = MacroEditor()
    editor.loop_ms.setValue(250)
    editor.loop_on.setChecked(True)
    assert editor.current.loop_ms == 250
    editor.loop_on.setChecked(False)
    assert editor.current.loop_ms == 0


def test_loading_a_macro_read_off_the_pad_shows_its_repeat():
    editor = MacroEditor()
    grid = MacroGrid(loop_ms=500)
    grid.toggle(0, "B")
    editor.load("M1", grid)
    assert editor.loop_on.isChecked()
    assert editor.loop_ms.value() == 500


def test_clearing_empties_the_slot_it_is_showing():
    editor = MacroEditor()
    editor.current.toggle(0, "A")
    editor.clear()
    assert editor.current.empty


def test_record_and_read_name_the_slot_they_are_for():
    editor = MacroEditor()
    editor.show_slot("M4")
    recorded, read = [], []
    editor.record_requested.connect(recorded.append)
    editor.read_requested.connect(read.append)
    editor._record()
    editor._read()
    assert recorded == ["M4"] and read == ["M4"]
