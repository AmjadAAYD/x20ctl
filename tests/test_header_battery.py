"""Battery in the header: four levels, because four is all there is."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication      # noqa: E402

from x20ctl import protocol as p                 # noqa: E402
from x20ctl.gui.buttons import LEFT_HAND, ButtonsPage   # noqa: E402
from x20ctl.gui.header import HeaderBar          # noqa: E402

app = QApplication.instance() or QApplication([])


def test_a_full_battery_draws_four_bars():
    bar = HeaderBar()
    bar.show_battery(p.Battery(level=4, charging=False))
    assert bar.battery.text().count("■") == 4


def test_a_flat_battery_draws_one_and_goes_red():
    """Amjad found out he was at zero from Steam, not from us."""
    bar = HeaderBar()
    bar.show_battery(p.Battery(level=1, charging=False))
    assert bar.battery.text().startswith("■□□□")
    assert "color" in bar.battery.styleSheet()
    assert "low" in bar.battery.toolTip().lower()


def test_charging_is_said_rather_than_implied():
    bar = HeaderBar()
    bar.show_battery(p.Battery(level=1, charging=True))
    assert "charging" in bar.battery.text()
    assert bar.battery.styleSheet() == "", "charging at level 1 is not a worry"


def test_no_battery_reading_shows_nothing_rather_than_zero():
    """A pad that did not report is not a pad that is flat."""
    bar = HeaderBar()
    bar.show_battery(None)
    assert bar.battery.text() == ""


def test_no_percentage_is_invented():
    """The level lives in three bits. A percentage would be made up."""
    bar = HeaderBar()
    bar.show_battery(p.Battery(level=3, charging=False))
    assert "%" not in bar.battery.text()


# -- the two column layout ------------------------------------------------

def test_the_left_column_is_what_the_left_hand_reaches():
    page = ButtonsPage()
    page.load(list(p.CHANGEKEY_DEFAULT_SOURCES))

    for code in LEFT_HAND:
        assert page.grid.getItemPosition(
            page.grid.indexOf(page.boxes[code]))[1] == 1, (
                f"{p.Key(code).name} belongs on the left")


def test_the_face_buttons_are_on_the_right():
    page = ButtonsPage()
    page.load(list(p.CHANGEKEY_DEFAULT_SOURCES))

    for key in (p.Key.A, p.Key.B, p.Key.X, p.Key.Y, p.Key.RB, p.Key.RT,
                p.Key.R3):
        column = page.grid.getItemPosition(
            page.grid.indexOf(page.boxes[int(key)]))[1]
        assert column == 3, f"{key.name} belongs on the right"


def test_every_source_still_gets_a_row():
    page = ButtonsPage()
    sources = list(p.CHANGEKEY_DEFAULT_SOURCES)
    page.load(sources)
    expected = [c for c in sources if c not in p.CHANGEKEY_TARGET_ONLY]
    assert set(page.boxes) == set(expected)
