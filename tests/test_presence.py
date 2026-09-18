"""Noticing a controller that stopped answering."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication, QMessageBox    # noqa: E402

from x20ctl.client import Found                             # noqa: E402
from x20ctl.gui.presence import (                           # noqa: E402
    MISSES_BEFORE_ASKING, PresenceWatcher, ask_about_lost,
)
from x20ctl.gui.roster import Roster                        # noqa: E402
from x20ctl.gui.shell import AppShell                       # noqa: E402

app = QApplication.instance() or QApplication([])

A = Found("98:B6:ED:E3:15:C4", "X20")
B = Found("98:B6:ED:E3:15:C5", "X10")

_alive = []


def watcher_for(roster, results):
    """A watcher whose sweeps return whatever is next in `results`."""
    calls = iter(results)
    return PresenceWatcher(roster, scan=lambda: next(calls, []))


def test_a_controller_that_answers_is_marked_connected():
    roster = Roster()
    roster.add(A.address)
    watcher = watcher_for(roster, [[A]])
    watcher.sweep()
    assert roster.slots[1].connected


def test_one_quiet_sweep_is_treated_as_noise():
    """Bluetooth misses an advertisement all the time. Panicking on the first
    silence would cry wolf constantly."""
    roster = Roster()
    roster.add(A.address)
    watcher = watcher_for(roster, [[A], []])
    watcher.sweep()
    lost = []
    watcher.lost.connect(lost.append)
    watcher.sweep()
    assert lost == []
    assert roster.slots[1].connected


def test_a_second_silence_reports_it_lost():
    roster = Roster()
    roster.add(A.address)
    watcher = watcher_for(roster, [[A]] + [[]] * MISSES_BEFORE_ASKING)
    watcher.sweep()
    lost = []
    watcher.lost.connect(lost.append)
    for _ in range(MISSES_BEFORE_ASKING):
        watcher.sweep()

    assert [slot.address for slot in lost] == [A.address]
    assert not roster.slots[1].connected


def test_it_asks_once_rather_than_every_sweep():
    """A dialog every six seconds is worse than the problem it reports."""
    roster = Roster()
    roster.add(A.address)
    watcher = PresenceWatcher(roster, scan=lambda: [])
    lost = []
    watcher.lost.connect(lost.append)
    for _ in range(8):
        watcher.sweep()
    assert len(lost) == 1


def test_choosing_reconnect_lets_it_be_reported_again_later():
    roster = Roster()
    slot = roster.add(A.address)
    watcher = PresenceWatcher(roster, scan=lambda: [])
    lost = []
    watcher.lost.connect(lost.append)
    for _ in range(3):
        watcher.sweep()
    watcher.watch_again(slot)
    for _ in range(3):
        watcher.sweep()
    assert len(lost) == 2, "still gone, and the user asked to keep waiting"


def test_a_controller_that_comes_back_goes_green_again():
    roster = Roster()
    roster.add(A.address)
    watcher = watcher_for(roster, [[], [], [A]])
    for _ in range(3):
        watcher.sweep()
    assert roster.slots[1].connected


def test_one_controller_going_quiet_does_not_touch_another():
    """The reported bug: two pads, one switched off."""
    roster = Roster()
    roster.add(A.address, player=1)
    roster.add(B.address, player=2)
    watcher = PresenceWatcher(roster, scan=lambda: [A])
    for _ in range(3):
        watcher.sweep()

    assert roster.slots[1].connected
    assert not roster.slots[2].connected


def test_a_failing_sweep_is_treated_as_silence_not_a_crash():
    def broken():
        raise OSError("radio off")

    roster = Roster()
    roster.add(A.address)
    watcher = PresenceWatcher(roster, scan=broken)
    for _ in range(3):
        watcher.sweep()
    assert not roster.slots[1].connected


def test_the_dialog_offers_removing_and_waiting():
    roster = Roster()
    slot = roster.add(A.address, product="EasySMX X20")
    box = ask_about_lost(slot)
    labels = {b.text() for b in box.buttons()}
    assert labels == {"Reconnect", "Remove"}
    assert "stopped answering" in box.text()


def test_removing_from_the_dialog_drops_it_from_the_roster():
    shell = AppShell()
    _alive.append(shell)
    shell.add_controller(A.address, "X20", 1)
    box = shell.controller_lost(shell.roster.slots[1])

    remove = next(b for b in box.buttons() if b.text() == "Remove")
    box.buttonClicked.emit(remove)
    assert len(shell.roster) == 0


def test_reconnect_keeps_it_on_the_roster():
    shell = AppShell()
    _alive.append(shell)
    shell.add_controller(A.address, "X20", 1)
    box = shell.controller_lost(shell.roster.slots[1])

    again = next(b for b in box.buttons() if b.text() == "Reconnect")
    box.buttonClicked.emit(again)
    assert len(shell.roster) == 1


def test_the_row_shows_a_dot_that_follows_the_state():
    roster = Roster()
    roster.add(A.address)
    shell = AppShell(roster=roster)
    _alive.append(shell)

    shell.refresh()
    row = shell.start.rows.itemAt(0).widget()
    assert row.dot.property("state") == "off"

    roster.slots[1].connected = True
    shell.refresh()
    row = shell.start.rows.itemAt(0).widget()
    assert row.dot.property("state") == "on"
