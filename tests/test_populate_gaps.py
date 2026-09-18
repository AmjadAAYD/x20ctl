"""populate() must survive a snapshot with records missing.

A pad that answers HOST_STICK but not HOST_TRIGGER used to raise AttributeError
here and abort the whole load, because _trigger_curves was only created inside
the `if triggers:` branch and then read inside the `if sticks:` branch.

It is reachable two ways: a pad that reports no triggers, and a single read
timing out — which the first query on a fresh BLE link is known to do, so the
crash would have been intermittent.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication

from x20ctl import protocol as p
from x20ctl.client import Snapshot
from x20ctl.gui.shell import Workspace

app = QApplication.instance() or QApplication([])

STICK = bytes([8, 5, 85, 85, 170, 170, 0])
TRIGGER = bytes([4, 34, 82, 133, 229, 235, 0])


def _snapshot(**kw) -> Snapshot:
    base = dict(device=None, capabilities=None, name="Xpert2",
                vibration=(70, 70), sticks=[], triggers=[], battery=None,
                raw={})
    base.update(kw)
    return Snapshot(**base)


def test_sticks_without_triggers_does_not_crash():
    ws = Workspace()
    ws.populate(_snapshot(sticks=[STICK, STICK], triggers=[]))
    assert ws._trigger_curves == []
    assert len(ws._stick_curves) == 2


def test_triggers_without_sticks_does_not_crash():
    ws = Workspace()
    ws.populate(_snapshot(sticks=[], triggers=[TRIGGER, TRIGGER]))
    assert len(ws._trigger_curves) == 2
    assert ws._stick_curves == []


def test_a_snapshot_with_nothing_in_it_is_survivable():
    ws = Workspace()
    ws.populate(_snapshot())
    assert ws._trigger_curves == [] and ws._stick_curves == []


def test_curves_are_initialised_before_any_snapshot():
    """Every reader uses these; none should have to guard with getattr."""
    ws = Workspace()
    assert ws._trigger_curves == []
    assert ws._stick_curves == []
    assert ws._snapshot is None


def test_editing_triggers_before_a_snapshot_says_so_rather_than_raising():
    ws = Workspace()
    said: list[str] = []
    ws.say = said.append
    ws._swap_triggers(True)
    assert said and "not loaded" in said[-1].lower()


def test_battery_is_cleared_when_leaving_a_controller():
    """The tray outlives the window, so a stale reading would sit there."""
    from x20ctl.gui.shell import AppShell

    shell = AppShell()
    seen: list = []
    shell.workspace.battery_read.connect(seen.append)
    shell.workspace.populate(_snapshot(
        battery=p.Battery(level=3, charging=False, status=0x40)))
    assert seen and seen[-1] is not None

    shell.show_roster()
    assert seen[-1] is None, "leaving a controller must clear the tray"
