"""Choosing a controller and a player, without any hardware."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl.client import Found, is_controller      # noqa: E402
from x20ctl.gui.addsheet import AddControllerSheet  # noqa: E402
from x20ctl.gui.roster import Roster                # noqa: E402

app = QApplication.instance() or QApplication([])

A = Found("98:B6:ED:E3:15:C4", "Xpert2")
B = Found("98:B6:ED:E3:15:C5", "Xpert2")


def test_an_advertisement_matches_on_name_or_vendor_range():
    assert is_controller("Xpert2", "AA:BB:CC:DD:EE:FF")
    assert is_controller("", "98:B6:ED:00:00:01"), "vendor MAC range"
    assert not is_controller("Galaxy Buds", "11:22:33:44:55:66")
    assert not is_controller("", ""), "an empty advertisement is not a pad"


def test_nothing_can_be_added_before_something_is_picked():
    sheet = AddControllerSheet(Roster())
    assert not sheet.add_button.isEnabled()
    assert sheet.selection() is None


def test_picking_a_result_enables_add_and_reports_the_choice():
    sheet = AddControllerSheet(Roster())
    sheet.show_results([A])
    sheet.results.setCurrentRow(0)

    assert sheet.add_button.isEnabled()
    address, name, player = sheet.selection()
    assert address == A.address and name == "Xpert2" and player == 1


def test_only_free_players_are_offered():
    roster = Roster()
    roster.add(A.address, player=1)
    roster.add(B.address, player=3)
    sheet = AddControllerSheet(roster)

    offered = [sheet.player.itemData(i) for i in range(sheet.player.count())]
    assert offered == [2, 4]


def test_a_controller_already_added_is_listed_but_not_selectable():
    """Seeing it and being told why beats it silently vanishing."""
    roster = Roster()
    roster.add(A.address, player=1)
    sheet = AddControllerSheet(roster)
    sheet.show_results([A, B])

    assert sheet.results.count() == 2
    assert "already added" in sheet.results.item(0).text()
    from PySide6.QtCore import Qt
    assert not (sheet.results.item(0).flags() & Qt.ItemIsEnabled)
    assert "1 controller available" in sheet.status.text()


def test_finding_nothing_says_what_to_do_about_it():
    sheet = AddControllerSheet(Roster())
    sheet.show_results([])
    assert "Turn the controller on" in sheet.status.text()
    assert not sheet.add_button.isEnabled()


def test_everything_found_already_added_is_its_own_message():
    roster = Roster()
    roster.add(A.address, player=1)
    sheet = AddControllerSheet(roster)
    sheet.show_results([A])
    assert "already added" in sheet.status.text().lower()


def test_a_failed_scan_re_enables_the_button():
    """Bluetooth off should not leave the sheet stuck mid-scan."""
    sheet = AddControllerSheet(Roster())
    sheet.scanning()
    assert not sheet.scan_button.isEnabled()
    sheet.failed("Bluetooth is off")
    assert sheet.scan_button.isEnabled()
    assert "Bluetooth is off" in sheet.status.text()


def test_accepting_emits_the_choice():
    sheet = AddControllerSheet(Roster())
    sheet.show_results([A])
    sheet.results.setCurrentRow(0)

    seen = []
    sheet.accepted_controller.connect(lambda *args: seen.append(args))
    sheet._accept()
    assert seen == [(A.address, "Xpert2", 1)]
