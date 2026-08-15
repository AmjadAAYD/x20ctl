"""Recording a macro: press Record, play something, press Record again."""

from __future__ import annotations

import os
import sys
import time

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl import protocol as p                     # noqa: E402
from x20ctl.gui.shell import AppShell                # noqa: E402

app = QApplication.instance() or QApplication([])
_alive = []


def FakeState(buttons=()):
    """The real GamepadState, so the recorder sees what it expects.

    Faking it by hand missed macro_inputs, which is where the recorder reads
    buttons and stick together.
    """
    from x20ctl.input import GamepadState
    return GamepadState(slot=0, packet=1, buttons=frozenset(buttons),
                        left_trigger=0, right_trigger=0,
                        left_stick=(0, 0), right_stick=(0, 0))


class FakeReader:
    def __init__(self):
        self.available = True
        self.state = FakeState()

    def poll(self):
        return self.state


def workspace():
    shell = AppShell()
    _alive.append(shell)
    work = shell.workspace
    work.reader = FakeReader()
    return work


def test_record_needs_a_pad_on_xinput_and_says_so():
    work = workspace()
    work.reader.available = False
    work._record_macro("M1")
    assert "Connect the controller" in work.status.text()
    assert work.recorder is None


def test_pressing_record_starts_and_says_how_to_stop():
    work = workspace()
    work._record_macro("M2")
    assert work.recorder is not None and work.recorder.recording
    assert "again to stop" in work.status.text()


def test_what_is_played_lands_in_the_slot_that_was_recorded():
    work = workspace()
    work._record_macro("M3")

    for buttons in ([p.Key.A], [], [p.Key.B], []):
        work.reader.state = FakeState(buttons)
        work.poll_inputs()
        time.sleep(0.02)

    work.reader.state = FakeState()
    work.poll_inputs()
    work._record_macro("M3")            # stop

    grid = work.pages["macros"].grids["M3"]
    assert work.recorder is None
    assert not grid.empty, "nothing was written down"
    assert "M3" in work.status.text()


def test_recording_leaves_the_other_slots_alone():
    work = workspace()
    work._record_macro("M4")
    work.reader.state = FakeState([p.Key.A])
    work.poll_inputs()
    time.sleep(0.02)
    work.reader.state = FakeState()
    work.poll_inputs()
    work._record_macro("M4")

    for other in ("M1", "M2", "M3"):
        assert work.pages["macros"].grids[other].empty


def test_the_macro_buttons_themselves_cannot_be_recorded():
    """M1 to M4 sit on the back of the pad and fire macros. They are not
    XInput buttons, so nothing can see them being pressed, including us."""
    from x20ctl.input import XINPUT_BUTTONS
    reported = {key.name for key in XINPUT_BUTTONS.values()}
    assert "M_LEFT" not in reported
    assert "M2" not in reported


def test_applying_a_macro_without_a_controller_does_not_raise():
    work = workspace()
    work.pages["macros"].current.toggle(0, "A")
    work.pages["macros"].apply()        # no link attached
