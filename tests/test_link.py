"""The controller link, driven by a fake pad and a fake bridge."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication      # noqa: E402

from x20ctl.gui.link import ControllerLink      # noqa: E402

app = QApplication.instance() or QApplication([])


class FakePad:
    """Records calls. Every method returns something harmless."""

    def __init__(self, fail_with: Exception | None = None) -> None:
        self.calls: list[tuple] = []
        self.fail_with = fail_with

    async def __aenter__(self):
        if self.fail_with is not None:
            raise self.fail_with
        return self

    async def __aexit__(self, *exc):
        return False

    def __call__(self, address):
        self.address = address
        return self

    async def snapshot(self):
        self.calls.append(("snapshot",))
        return "snapshot"

    async def set_vibration(self, percent):
        self.calls.append(("vibration", percent))
        return (percent, percent)

    async def set_shutdown_timeout(self, minutes):
        self.calls.append(("shutdown", minutes))
        return minutes

    async def set_curves(self, kind, channels):
        self.calls.append(("curves", kind, channels))
        return channels

    async def set_remapping(self, changes):
        self.calls.append(("remap", changes))
        return changes

    async def write_macro_steps(self, slot, steps, *, loop_ms=0):
        self.calls.append(("macro", slot, len(steps), loop_ms))

    async def clear_macro(self, slot):
        self.calls.append(("clear", slot))

    async def read_macro(self, slot):
        self.calls.append(("read", slot))
        return "program"

    async def calibrate(self):
        self.calls.append(("calibrate",))

    async def factory_reset(self):
        self.calls.append(("reset",))


class FakeBridge:
    """Runs the coroutine immediately, on this thread."""

    def __init__(self) -> None:
        self.ran = 0

    def run(self, work, on_done=None, on_error=None) -> None:
        import asyncio
        self.ran += 1
        try:
            result = asyncio.run(work())
        except Exception as exc:                # noqa: BLE001
            if on_error is not None:
                on_error(str(exc) or exc.__class__.__name__)
            return
        if on_done is not None:
            on_done(result)


def make(fail_with=None):
    pad = FakePad(fail_with)
    link = ControllerLink("98:B6:ED:E3:15:C4", bridge=FakeBridge(), open_pad=pad)
    return link, pad


def test_loading_hands_back_the_snapshot():
    link, pad = make()
    seen = []
    link.loaded.connect(seen.append)
    assert link.load() is True
    assert seen == ["snapshot"]
    assert pad.calls == [("snapshot",)]


def test_a_write_reports_what_it_did_in_words():
    link, pad = make()
    said = []
    link.written.connect(said.append)
    link.set_vibration(45)
    assert pad.calls == [("vibration", 45)]
    assert "45%" in said[0]


def test_never_is_worded_differently_from_a_number_of_minutes():
    link, _ = make()
    said = []
    link.written.connect(said.append)
    link.set_shutdown(None)
    link.set_shutdown(10)
    assert "never" in said[0].lower()
    assert "10" in said[1]


def test_a_failure_becomes_a_sentence_rather_than_an_exception():
    link, _ = make(fail_with=OSError("Device was not found"))
    problems = []
    link.failed.connect(problems.append)
    link.load()
    assert problems and "not found" in problems[0]


def test_the_link_is_free_again_after_a_failure():
    """A dropped write must not wedge every later one."""
    link, _ = make(fail_with=OSError("boom"))
    link.load()
    assert not link.busy


def test_work_does_not_stack_up():
    """This link drops when several writes overlap, so it takes one at a time."""
    link, pad = make()
    link._start()                    # pretend something is in flight
    assert link.set_vibration(50) is False
    assert pad.calls == []


def test_busy_is_announced_both_ways():
    link, _ = make()
    states = []
    link.busy_changed.connect(states.append)
    link.set_vibration(20)
    assert states == [True, False]


def test_a_macro_is_written_from_steps_not_from_text():
    link, pad = make()
    link.write_macro(2, ["a", "b", "c"], loop_ms=250)
    assert pad.calls == [("macro", 2, 3, 250)]


def test_reading_a_macro_hands_the_program_to_the_caller():
    link, _ = make()
    got = []
    link.read_macro(3, got.append)
    assert got == ["program"]


def test_clearing_naming_and_resetting_all_reach_the_pad():
    link, pad = make()
    link.clear_macro(1)
    link.calibrate()
    link.factory_reset()
    assert pad.calls == [("clear", 1), ("calibrate",), ("reset",)]


def test_calibration_says_the_part_people_forget():
    link, _ = make()
    said = []
    link.written.connect(said.append)
    link.calibrate()
    assert "+" in said[0], "the pad waits for the + button"


def test_clearing_remapping_reads_differently_from_setting_it():
    link, _ = make()
    said = []
    link.written.connect(said.append)
    link.set_remapping({})
    link.set_remapping({1: 2})
    assert "cleared" in said[0].lower()
    assert "1" in said[1]
