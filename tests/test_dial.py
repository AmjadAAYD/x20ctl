"""The direction dial: eight notches, and a middle that means nothing."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtCore import QPointF                  # noqa: E402
from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl.gui.dial import (                        # noqa: E402
    DIRECTIONS, DirectionDial, angle_of, direction_for,
)

app = QApplication.instance() or QApplication([])


def test_up_is_up_in_screen_coordinates():
    """Screen y grows downward, which is the easy thing to get backwards."""
    assert direction_for(0, -1) == "UP"
    assert direction_for(0, 1) == "DOWN"
    assert direction_for(1, 0) == "RIGHT"
    assert direction_for(-1, 0) == "LEFT"


def test_the_diagonals_land_where_they_should():
    assert direction_for(0.7, -0.7) == "UP_RIGHT"
    assert direction_for(-0.7, 0.7) == "DOWN_LEFT"
    assert direction_for(0.7, 0.7) == "DOWN_RIGHT"
    assert direction_for(-0.7, -0.7) == "UP_LEFT"


def test_the_middle_means_the_stick_is_not_in_this_step():
    assert direction_for(0, 0) is None
    assert direction_for(0.05, -0.05) is None


def test_every_angle_snaps_to_one_of_the_eight():
    """Nothing between the notches, because the pad stores nothing between."""
    import math
    seen = set()
    for degree in range(0, 360):
        radians = math.radians(degree)
        seen.add(direction_for(math.sin(radians), -math.cos(radians)))
    assert seen == set(DIRECTIONS)


def test_a_heading_and_its_angle_agree_both_ways():
    import math
    for direction in DIRECTIONS:
        radians = math.radians(angle_of(direction))
        assert direction_for(math.sin(radians), -math.cos(radians)) == direction


def test_the_dial_reports_what_was_picked():
    dial = DirectionDial()
    seen = []
    dial.chosen.connect(seen.append)
    dial._pick(QPointF(dial.width() / 2, 4))          # top edge
    assert seen == ["UP"]
    assert dial.direction == "UP"


def test_clicking_the_middle_clears_the_stick():
    dial = DirectionDial("LEFT")
    seen = []
    dial.chosen.connect(seen.append)
    dial._pick(QPointF(dial.width() / 2, dial.height() / 2))
    assert seen == [None]
    assert dial.direction is None


def test_the_dial_can_be_shown_a_heading_without_reporting_one():
    """Loading a step must not look like the user picked something."""
    dial = DirectionDial()
    seen = []
    dial.chosen.connect(seen.append)
    dial.set_direction("DOWN")
    assert dial.direction == "DOWN"
    assert seen == []
