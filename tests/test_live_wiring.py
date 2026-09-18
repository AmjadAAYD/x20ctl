"""The wiring between live input and the pages that show it.

The lamp grid was folded into the Test tab, which is the old input tester, so
what remains here is the plumbing: conversions, the poll timer, and every
section having a page behind it.
"""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl.gui.shell import AppShell, _percent, _trigger   # noqa: E402

app = QApplication.instance() or QApplication([])


class FakeState:
    """Only what the trigger meters read."""

    def __init__(self, lt=0, rt=0):
        self.slot = 0
        self.packet = 1
        self.buttons = frozenset()
        self.left_stick = (0, 0)
        self.right_stick = (0, 0)
        self.left_trigger = lt
        self.right_trigger = rt


class FakeReader:
    """`available` is a property on the real one, so it cannot be assigned."""

    def __init__(self, state=None) -> None:
        self.state = state
        self.available = True

    def poll(self):
        return self.state


# Shells are kept alive: letting one be collected takes its QTimer with it.
_alive = []


def workspace_with(state):
    shell = AppShell()
    _alive.append(shell)
    work = shell.workspace
    work.reader = FakeReader(state)
    return work


def test_a_stick_axis_becomes_a_signed_percentage():
    assert _percent(32767) == 100
    assert _percent(-32767) == -100
    assert _percent(0) == 0


def test_a_trigger_is_a_byte_not_a_signed_axis():
    """The bug this replaces: state.triggers did not exist, and the field is
    a single byte per side rather than a pair."""
    assert _trigger(255) == 100
    assert _trigger(0) == 0
    assert _trigger(128) == 50


def test_polling_moves_the_trigger_meters():
    work = workspace_with(FakeState(lt=255, rt=0))
    work.show_section("triggers")
    work.poll_inputs()
    assert work.pages["triggers"].sides["left"].meter.value() == 100
    assert work.pages["triggers"].sides["right"].meter.value() == 0


def test_the_meters_fall_back_to_zero_with_no_pad():
    work = workspace_with(None)
    work.show_section("triggers")
    work.poll_inputs()
    assert work.pages["triggers"].sides["left"].meter.value() == 0


def test_polling_survives_having_no_pad():
    work = workspace_with(None)
    work.show_section("test")
    work.poll_inputs()          # must not raise


def test_the_poll_timer_is_running_from_the_start():
    work = workspace_with(None)
    assert work.poll_timer.isActive()


def test_every_section_the_rail_offers_has_a_page():
    from x20ctl.gui.nav import SECTIONS
    work = workspace_with(None)
    for section in SECTIONS:
        assert section.key in work.pages, f"{section.key} has no page"


def test_the_test_tab_is_the_input_tester():
    """One place to watch the controller, not two."""
    work = workspace_with(None)
    assert hasattr(work.pages["test"], "start")
    assert hasattr(work.pages["test"], "stop")


def test_sticks_is_a_real_editor():
    work = workspace_with(None)
    assert hasattr(work.pages["sticks"], "load")
    assert hasattr(work.pages["sticks"], "write_requested")
