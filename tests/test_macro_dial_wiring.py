"""Pointing a stick from inside the macro editor."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl.gui.macrogrid import ROWS                # noqa: E402
from x20ctl.gui.macros import ARROWS, MacroEditor    # noqa: E402

app = QApplication.instance() or QApplication([])

LS_ROW = [key for key, _ in ROWS].index("LS")


def test_a_stick_cell_opens_a_dial_rather_than_toggling_blindly():
    """Toggling would silently pick a direction the user never chose."""
    editor = MacroEditor()
    popup = editor.open_dial(0, "LS")
    assert popup.isVisible()
    assert editor.current.direction(0, "LS") is None, "opening picks nothing"
    popup.close()


def test_the_dial_opens_showing_the_heading_already_there():
    editor = MacroEditor()
    editor.current.point(0, "LS", "DOWN_LEFT")
    popup = editor.open_dial(0, "LS")
    assert editor._dial.direction == "DOWN_LEFT"
    popup.close()


def test_choosing_a_heading_stores_it_and_shows_an_arrow():
    editor = MacroEditor()
    editor._on_point(0, "LS", "RIGHT")
    assert editor.current.direction(0, "LS") == "RIGHT"
    assert editor.cells[(0, LS_ROW)].text() == ARROWS["RIGHT"]
    assert editor.cells[(0, LS_ROW)].isChecked()


def test_choosing_the_middle_takes_the_stick_out_of_the_step():
    editor = MacroEditor()
    editor._on_point(0, "LS", "UP")
    editor._on_point(0, "LS", None)
    assert editor.current.direction(0, "LS") is None
    assert not editor.cells[(0, LS_ROW)].isChecked()
    assert editor.cells[(0, LS_ROW)].text() == ""


def test_every_heading_has_an_arrow():
    from x20ctl.gui.macrogrid import DIRECTIONS
    assert set(ARROWS) == set(DIRECTIONS)


def test_a_stick_step_can_still_be_applied():
    editor = MacroEditor()
    editor._on_point(0, "LS", "UP_RIGHT")
    sent = []
    editor.apply_requested.connect(lambda slot, grid: sent.append(slot))
    assert editor.apply() is True
    assert sent == ["M1"]
