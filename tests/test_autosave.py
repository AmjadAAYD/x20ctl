"""Settings that save themselves, without flooding the link."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtCore import QCoreApplication, QEventLoop, QTimer   # noqa: E402
from PySide6.QtWidgets import QApplication                        # noqa: E402

from x20ctl.gui.autosave import Debounced                         # noqa: E402
from x20ctl.gui.rumble import Rumbler                              # noqa: E402

app = QApplication.instance() or QApplication([])


def spin(ms: int) -> None:
    """Let Qt's timers run for a while."""
    loop = QEventLoop()
    QTimer.singleShot(ms, loop.quit)
    loop.exec()


def test_a_drag_writes_once_not_once_per_step():
    """The whole point: forty slider steps must not be forty packets."""
    sent = []
    saver = Debounced(settle_ms=60)
    saver.ready.connect(sent.append)

    for value in range(0, 40):
        saver.push(value)
    spin(200)

    assert sent == [39], "only the value it settled on should be written"


def test_nothing_is_written_until_it_settles():
    sent = []
    saver = Debounced(settle_ms=120)
    saver.ready.connect(sent.append)
    saver.push(30)
    spin(40)
    assert sent == [], "still moving"
    spin(200)
    assert sent == [30]


def test_leaving_early_still_saves_the_last_value():
    """Closing the window a moment after a change must not lose it."""
    sent = []
    saver = Debounced(settle_ms=10_000)
    saver.ready.connect(sent.append)
    saver.push(75)
    assert saver.pending
    saver.flush()
    assert sent == [75]
    assert not saver.pending


def test_flushing_with_nothing_pending_does_nothing():
    sent = []
    saver = Debounced(settle_ms=10)
    saver.ready.connect(sent.append)
    saver.flush()
    assert sent == []


def test_cancelling_drops_the_pending_write():
    sent = []
    saver = Debounced(settle_ms=50)
    saver.ready.connect(sent.append)
    saver.push(10)
    saver.cancel()
    spin(150)
    assert sent == []


def test_the_rumbler_is_quiet_rather_than_broken_without_a_pad():
    """A pad paired only to a phone can be configured and not felt. That is
    not an error."""
    rumbler = Rumbler()
    result = rumbler.pulse(30)
    assert result in (True, False)
    rumbler.stop()


def test_a_percentage_maps_onto_the_full_motor_range():
    from x20ctl.gui.rumble import FULL
    assert round(FULL * 0 / 100) == 0
    assert round(FULL * 100 / 100) == FULL
    # 30% of 65535 is 19660.5, and Python rounds halves to even
    assert round(FULL * 30 / 100) == 19660
