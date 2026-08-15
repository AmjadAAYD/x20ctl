"""Moving between the roster and a controller's workspace, with no hardware."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication              # noqa: E402

from x20ctl.client import Found                          # noqa: E402
from x20ctl.gui.shell import (                           # noqa: E402
    ROSTER_PAGE, WORKSPACE_PAGE, AppShell, profile_dir,
)

app = QApplication.instance() or QApplication([])

A = Found("98:B6:ED:E3:15:C4", "EasySMX X20")
B = Found("98:B6:ED:E3:15:C5", "EasySMX X10")


def test_the_app_starts_on_the_roster_not_on_settings():
    shell = AppShell()
    assert shell.pages.currentIndex() == ROSTER_PAGE
    assert not shell.roster


def test_adding_a_controller_puts_it_on_the_roster():
    shell = AppShell()
    slot = shell.add_controller(A.address, A.name, 2)
    assert slot.player == 2
    assert shell.start.rows.count() == 1


def test_opening_a_controller_shows_its_workspace():
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.open_controller(1)

    assert shell.pages.currentIndex() == WORKSPACE_PAGE
    assert shell.workspace.slot.address == A.address
    assert "P1" in shell.workspace.title.text()


def test_going_back_returns_to_the_roster():
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.open_controller(1)
    shell.show_roster()
    assert shell.pages.currentIndex() == ROSTER_PAGE


def test_the_workspace_offers_a_tab_per_controller():
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.add_controller(B.address, B.name, 3)
    shell.open_controller(1)

    labels = []
    for i in range(shell.workspace.tabs.count()):
        widget = shell.workspace.tabs.itemAt(i).widget()
        if widget is not None:
            labels.append(widget.text())
    assert labels == ["P1", "P3"]


def test_switching_player_moves_to_the_other_controller():
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.add_controller(B.address, B.name, 2)
    shell.open_controller(1)
    shell.workspace.switch_player.emit(2)
    assert shell.workspace.slot.address == B.address


def test_removing_the_open_controller_falls_back_to_another():
    """Being left staring at a workspace for a pad you just removed is worse
    than being moved somewhere real."""
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.add_controller(B.address, B.name, 2)
    shell.open_controller(1)
    shell.remove_controller(1)

    assert shell.workspace.slot.address == B.address
    assert shell.pages.currentIndex() == WORKSPACE_PAGE


def test_removing_the_last_controller_returns_to_the_empty_roster():
    shell = AppShell()
    shell.add_controller(A.address, A.name, 1)
    shell.open_controller(1)
    shell.remove_controller(1)

    assert shell.pages.currentIndex() == ROSTER_PAGE
    assert not shell.roster


def test_each_controller_gets_its_own_save_directory():
    first = profile_dir("98b6ede315c4")
    second = profile_dir("98b6ede315c5")
    assert first != second
    assert first.endswith("98b6ede315c4")


def test_scanning_fills_the_sheet_from_whatever_discovery_returned():
    shell = AppShell(scan=lambda: [A, B])
    sheet = shell.open_add_sheet()
    assert sheet.results.count() == 2
    assert "2 controllers available" in sheet.status.text()
    sheet.close()


def test_a_scan_that_raises_is_reported_not_crashed():
    def broken():
        raise OSError("Bluetooth radio is not powered on")

    shell = AppShell(scan=broken)
    sheet = shell.open_add_sheet()
    assert "not powered on" in sheet.status.text()
    assert sheet.scan_button.isEnabled()
    sheet.close()


def test_a_build_without_scanning_says_so_rather_than_hanging():
    shell = AppShell(scan=None)
    sheet = shell.open_add_sheet()
    assert "unavailable" in sheet.status.text()
    sheet.close()


def test_the_sheet_adds_through_the_shell():
    shell = AppShell(scan=lambda: [A])
    sheet = shell.open_add_sheet()
    sheet.results.setCurrentRow(0)
    sheet._accept()

    assert len(shell.roster) == 1
    assert shell.roster.slots[1].address == A.address
