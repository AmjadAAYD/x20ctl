"""The trigger page: zones, response shapes, and reading the pad back."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication              # noqa: E402

from x20ctl import protocol as p                         # noqa: E402
from x20ctl.gui.triggers import (                        # noqa: E402
    SHAPES, ZONES, TriggersPage, preset_for, shape_for,
)

app = QApplication.instance() or QApplication([])


def curve(inner, outer, points):
    return p.Curve(inner_deadzone=inner, outer_raw=outer,
                   point1=points[0], point2=points[1], flags=0)


def test_every_zone_names_a_real_gear():
    """A zone the protocol cannot express would be a button that lies."""
    for key, _, _ in ZONES:
        assert key in p.TRIGGER_GEARS


def test_every_shape_maps_onto_a_real_preset():
    for key, _, _ in SHAPES:
        assert preset_for(key) in p.CURVE_PRESETS


def test_our_words_are_not_the_app_s_words():
    labels = {label.lower() for _, label, _ in SHAPES}
    assert not labels & {"quick", "slow", "smooth", "fine", "custom"}


def test_shape_names_round_trip_through_the_preset_they_stand_for():
    for key, _, _ in SHAPES:
        assert shape_for(preset_for(key)) == key


def test_a_custom_curve_is_no_shape_at_all():
    assert shape_for(None) is None


def test_loading_shows_the_zone_and_shape_the_pad_holds():
    page = TriggersPage()
    page.load([
        curve(20, 40, p.CURVE_PRESETS["quick"]),
        curve(0, 0, p.CURVE_PRESETS["default"]),
    ])

    left = page.sides["left"]
    assert left.zones.current() == "medium"
    assert left.shapes.current() == "quick"

    right = page.sides["right"]
    assert right.zones.current() == "zero"
    assert right.shapes.current() == "default"


def test_a_pad_holding_a_custom_curve_checks_no_shape_button():
    """The factory curve is none of the presets. Showing one selected would
    be a lie about what the pad holds."""
    page = TriggersPage()
    page.load([
        curve(4, 34, ((82, 133), (229, 235))),
        curve(4, 34, ((82, 133), (229, 235))),
    ])
    assert page.sides["left"].shapes.current() is None
    assert page.sides["left"].zones.current() == "small"


def test_choosing_a_zone_says_which_side_it_was_for():
    page = TriggersPage()
    seen = []
    page.zone_chosen.connect(lambda side, key: seen.append((side, key)))
    page.sides["right"].zones.buttons["large"].click()
    assert seen == [("right", "large")]


def test_choosing_a_shape_says_which_side_it_was_for():
    page = TriggersPage()
    seen = []
    page.shape_chosen.connect(lambda side, key: seen.append((side, key)))
    page.sides["left"].shapes.buttons["precise"].click()
    assert seen == [("left", "precise")]


def test_the_meters_follow_a_live_pull():
    page = TriggersPage()
    page.set_positions(64, 0)
    assert page.sides["left"].meter.bar.value() == 64
    assert page.sides["left"].meter.reading.text() == "64"
    assert page.sides["right"].meter.bar.value() == 0


def test_a_meter_refuses_impossible_readings():
    page = TriggersPage()
    page.set_positions(180, -20)
    assert page.sides["left"].meter.bar.value() == 100
    assert page.sides["right"].meter.bar.value() == 0


def test_each_choice_carries_its_own_explanation():
    for _, label, blurb in ZONES + SHAPES:
        assert blurb.strip().endswith("."), f"{label} needs a full sentence"
