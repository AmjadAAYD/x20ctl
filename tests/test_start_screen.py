"""The start screen: empty state, rows, and the ceiling of four players."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication      # noqa: E402

from x20ctl.gui.roster import Roster            # noqa: E402
from x20ctl.gui.start import StartPage          # noqa: E402

app = QApplication.instance() or QApplication([])


def test_an_empty_roster_shows_the_empty_state():
    page = StartPage()
    page.show_roster(Roster())
    assert page.empty.isVisible() or not page.isVisible()
    assert page.rows.count() == 0
    assert "Nothing added" in page.subtitle.text()


def test_adding_a_controller_replaces_the_empty_state_with_a_row():
    roster = Roster()
    roster.add("98:B6:ED:E3:15:C4", product="EasySMX X20")
    page = StartPage()
    page.show_roster(roster)

    assert page.rows.count() == 1
    assert not page.empty.isVisible()
    assert "1 controller," in page.subtitle.text()


def test_a_row_reads_as_product_and_player():
    roster = Roster()
    slot = roster.add("98:B6:ED:E3:15:C4", product="EasySMX X10", player=2)
    assert slot.label == "EasySMX X10, P2"


def test_the_add_button_gives_up_once_four_are_added():
    roster = Roster()
    for i in range(4):
        roster.add(f"98:B6:ED:E3:15:C{i}", product="EasySMX X20")
    page = StartPage()
    page.show_roster(roster)

    assert not page.add_button.isEnabled()
    assert "limit" in page.add_button.text()
    assert "every player taken" in page.subtitle.text()


def test_the_subtitle_names_the_free_players():
    roster = Roster()
    roster.add("98:B6:ED:E3:15:C4", player=2)
    page = StartPage()
    page.show_roster(roster)
    assert "P1" in page.subtitle.text() and "P3" in page.subtitle.text()


def test_redrawing_does_not_stack_duplicate_rows():
    """show_roster is called on every change, so it has to clear first."""
    roster = Roster()
    roster.add("98:B6:ED:E3:15:C4")
    page = StartPage()
    for _ in range(3):
        page.show_roster(roster)
    assert page.rows.count() == 1


def test_opening_a_row_reports_its_player():
    roster = Roster()
    roster.add("98:B6:ED:E3:15:C4", player=3)
    page = StartPage()
    page.show_roster(roster)

    seen = []
    page.opened.connect(seen.append)
    page.rows.itemAt(0).widget().opened.emit(3)
    assert seen == [3]
