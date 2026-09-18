"""The vibration and power pages, driven without a controller."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtCore import QEventLoop, QTimer          # noqa: E402
from PySide6.QtWidgets import QApplication             # noqa: E402

from x20ctl import protocol as p                        # noqa: E402
from x20ctl.gui.panels import PowerPage, VibrationPage  # noqa: E402

app = QApplication.instance() or QApplication([])


class FakeRumbler:
    """Stands in for the motors. Records what it was asked to do."""

    def __init__(self, reachable: bool = True) -> None:
        self.reachable = reachable
        self.pulses: list[int] = []

    def pulse(self, percent: int, **kw) -> bool:
        self.pulses.append(percent)
        return self.reachable

    def stop(self) -> None:
        pass


def spin(ms: int) -> None:
    loop = QEventLoop()
    QTimer.singleShot(ms, loop.quit)
    loop.exec()


# -- vibration ------------------------------------------------------------

def test_letting_go_of_the_slider_buzzes_at_that_setting():
    """Amjad's idea: 30 should feel like 30."""
    rumbler = FakeRumbler()
    page = VibrationPage(rumbler)
    page.slider.setValue(30)
    page.preview()
    assert rumbler.pulses == [30]

    page.slider.setValue(90)
    page.preview()
    assert rumbler.pulses == [30, 90]


def test_an_unreachable_pad_explains_itself_instead_of_failing():
    page = VibrationPage(FakeRumbler(reachable=False))
    page.preview()
    assert "Connect the controller" in page.status.text()


def test_dragging_writes_once_and_only_the_final_value():
    page = VibrationPage(FakeRumbler())
    page.saver._timer.setInterval(60)

    saved = []
    page.save_requested.connect(saved.append)
    for value in range(20, 46):
        page.slider.setValue(value)
    spin(200)

    assert saved == [45]


def test_loading_the_current_setting_does_not_save_it_back():
    """Opening the page must not write to the pad."""
    page = VibrationPage(FakeRumbler())
    page.saver._timer.setInterval(40)
    saved = []
    page.save_requested.connect(saved.append)

    page.load(55)
    spin(150)
    assert saved == []
    assert page.value() == 55
    assert page.readout.text() == "55%"


def test_leaving_the_page_writes_a_pending_change():
    page = VibrationPage(FakeRumbler())
    saved = []
    page.save_requested.connect(saved.append)
    page.slider.setValue(70)
    page.flush()
    assert saved == [70]


def test_the_page_says_what_it_is_doing():
    page = VibrationPage(FakeRumbler())
    page.slider.setValue(40)
    assert "Saving" in page.status.text()
    page.saved(40)
    assert "40%" in page.status.text()


# -- power ----------------------------------------------------------------

def test_the_power_page_offers_the_range_the_protocol_allows():
    page = PowerPage()
    assert page.slider.minimum() == 1
    assert page.slider.maximum() == p.MAX_SHUTDOWN_MINUTES


def test_never_is_expressed_as_none_not_as_zero():
    """Zero minutes is a real timeout that sleeps immediately, so never has
    to be a different thing entirely."""
    page = PowerPage()
    page.never.setChecked(True)
    assert page.value() is None
    assert page.readout.text() == "never"


def test_choosing_never_disables_the_minutes_slider():
    page = PowerPage()
    page.never.setChecked(True)
    assert not page.slider.isEnabled()
    page.never.setChecked(False)
    assert page.slider.isEnabled()


def test_loading_never_from_the_pad_shows_it():
    page = PowerPage()
    page.load(None)
    assert page.never.isChecked()
    assert page.value() is None


def test_minutes_are_written_in_the_singular_when_there_is_one():
    page = PowerPage()
    page.load(1)
    assert page.readout.text() == "1 minute"
    page.load(2)
    assert page.readout.text() == "2 minutes"


def test_applying_reports_the_chosen_value():
    page = PowerPage()
    page.load(15)
    seen = []
    page.save_requested.connect(seen.append)
    page.apply()
    assert seen == [15]
