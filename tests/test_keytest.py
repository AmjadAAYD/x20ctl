"""The test tab, driven with fabricated pad state."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication      # noqa: E402

from x20ctl import protocol as p                 # noqa: E402
from x20ctl.gui.keytest import GROUPS, KeyTestPage   # noqa: E402

app = QApplication.instance() or QApplication([])


def test_every_button_xinput_reports_has_a_lamp():
    """Anything the reader can produce must be visible, or a working button
    looks broken."""
    page = KeyTestPage()
    from x20ctl.input import XINPUT_BUTTONS
    reported = {int(key) for key in XINPUT_BUTTONS.values()}
    reported |= {int(p.Key.LT), int(p.Key.RT)}      # triggers, past threshold
    assert reported <= set(page.lamps)


def test_a_press_lights_exactly_that_button():
    page = KeyTestPage()
    page.set_buttons([p.Key.A])
    assert page.lit() == {int(p.Key.A)}


def test_releasing_darkens_it_again():
    page = KeyTestPage()
    page.set_buttons([p.Key.A])
    page.set_buttons([])
    assert page.lit() == set()


def test_a_chord_lights_every_button_in_it():
    page = KeyTestPage()
    page.set_buttons([p.Key.A, p.Key.RB, p.Key.DPAD_UP])
    assert page.lit() == {int(p.Key.A), int(p.Key.RB), int(p.Key.DPAD_UP)}


def test_select_and_start_appear_here_even_though_they_cannot_be_remapped():
    """Two different questions. This tab asks whether the button works."""
    page = KeyTestPage()
    page.set_buttons([p.Key.SELECT, p.Key.START])
    assert page.lit() == {int(p.Key.SELECT), int(p.Key.START)}


def test_sticks_read_from_the_centre_in_both_directions():
    page = KeyTestPage()
    page.set_axis("left_x", -80)
    page.set_axis("left_y", 40)
    assert page.axes["left_x"].bar.value() == -80
    assert page.axes["left_y"].bar.value() == 40


def test_triggers_only_go_one_way():
    page = KeyTestPage()
    assert page.axes["left_trigger"].bar.minimum() == 0
    page.set_axis("left_trigger", -50)
    assert page.axes["left_trigger"].bar.value() == 0


def test_impossible_readings_are_clamped_rather_than_drawn():
    page = KeyTestPage()
    page.set_axis("right_x", 250)
    assert page.axes["right_x"].bar.value() == 100


def test_clearing_releases_everything_and_recentres():
    page = KeyTestPage()
    page.set_buttons([p.Key.A])
    page.set_axis("left_x", 90)
    page.clear()
    assert page.lit() == set()
    assert page.axes["left_x"].bar.value() == 0


def test_an_unknown_axis_name_is_ignored_rather_than_raising():
    page = KeyTestPage()
    page.set_axis("nose", 50)


def test_the_groups_cover_every_lamp_once():
    seen = [key for _, group in GROUPS for key, _ in group]
    assert len(seen) == len(set(seen)), "a button listed twice would confuse"
