"""The wiring between live input and the pages that show it.

These cover the things that were built and never connected, plus the attribute
names that were guessed rather than checked.
"""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl import protocol as p                     # noqa: E402
from x20ctl.gui.shell import AppShell, _percent, _trigger   # noqa: E402

app = QApplication.instance() or QApplication([])


class FakeState:
    """Exactly the fields GamepadState really has."""

    def __init__(self, buttons=(), left=(0, 0), right=(0, 0), lt=0, rt=0):
        self.slot = 0
        self.packet = 1
        self.buttons = frozenset(buttons)
        self.left_stick = left
        self.right_stick = right
        self.left_trigger = lt
        self.right_trigger = rt


class FakeReader:
    """Stands in for XInputReader. `available` is a property on the real one,
    so it cannot simply be assigned over."""

    def __init__(self, state=None) -> None:
        self.state = state
        self.available = True

    def poll(self):
        return self.state


# Shells are kept alive here: letting one be collected takes its QTimer with
# it, and the next line then fails with a deleted C++ object.
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


def test_polling_lights_the_test_tab():
    work = workspace_with(FakeState(buttons=[p.Key.A, p.Key.RB]))
    work.show_section("test")
    work.poll_inputs()
    assert work.pages["test"].lit() == {int(p.Key.A), int(p.Key.RB)}


def test_polling_moves_the_sticks_on_the_test_tab():
    work = workspace_with(FakeState(left=(-32767, 0), right=(0, 32767)))
    work.show_section("test")
    work.poll_inputs()
    assert work.pages["test"].axes["left_x"].bar.value() == -100
    assert work.pages["test"].axes["right_y"].bar.value() == 100


def test_polling_moves_the_trigger_meters():
    work = workspace_with(FakeState(lt=255, rt=0))
    work.show_section("triggers")
    work.poll_inputs()
    assert work.pages["triggers"].sides["left"].meter.bar.value() == 100
    assert work.pages["triggers"].sides["right"].meter.bar.value() == 0


def test_a_page_that_is_not_showing_is_not_fed():
    """No point redrawing lamps nobody is looking at, 25 times a second."""
    work = workspace_with(FakeState(buttons=[p.Key.A]))
    work.show_section("motor")
    work.poll_inputs()
    assert work.pages["test"].lit() == set()


def test_polling_survives_having_no_pad():
    work = workspace_with(None)
    work.show_section("test")
    work.poll_inputs()          # must not raise


def test_no_gamepad_says_which_connection_is_missing():
    """A pad on the settings link and not on XInput is a normal state, and
    an empty tab looks identical to a broken one."""
    work = workspace_with(None)
    work.show_section("test")
    work.poll_inputs()
    hint = work.pages["test"].hint.text()
    assert "gamepad" in hint and "settings link is separate" in hint


def test_the_hint_clears_once_a_pad_appears():
    work = workspace_with(None)
    work.show_section("test")
    work.poll_inputs()
    assert work.pages["test"].hint.text()

    work.reader.state = FakeState(buttons=[p.Key.A])
    work.poll_inputs()
    assert work.pages["test"].hint.text() == ""
    assert work.pages["test"].lit() == {int(p.Key.A)}


def test_the_poll_timer_is_running_from_the_start():
    work = workspace_with(None)
    assert work.poll_timer.isActive()


def test_every_section_the_rail_offers_has_a_page():
    from x20ctl.gui.nav import SECTIONS
    work = workspace_with(None)
    for section in SECTIONS:
        assert section.key in work.pages, f"{section.key} has no page"


def test_sticks_is_a_real_editor_now():
    work = workspace_with(None)
    assert hasattr(work.pages["sticks"], "load")
    assert hasattr(work.pages["sticks"], "write_requested")
