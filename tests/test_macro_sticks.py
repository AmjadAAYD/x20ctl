"""Sticks in a macro step: a heading, never a bare stick.

The defect these cover: a row toggled to a bare "LS" produced a token the
protocol has never heard of, and it only failed at apply time, past every
other test in the suite.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import pytest        # noqa: E402

from x20ctl import protocol as p                          # noqa: E402
from x20ctl.gui.macrogrid import (                        # noqa: E402
    DIRECTIONS, STICK_ROWS, MacroGrid, split_token, stick_token,
)


def test_every_heading_is_one_the_protocol_knows():
    for stick in STICK_ROWS:
        for direction in DIRECTIONS:
            token = stick_token(stick, direction)
            parsed = p.parse_token(token)
            assert isinstance(parsed, p.StickInput)


def test_there_are_exactly_eight_headings():
    """The format stores a direction in a nibble, not a position."""
    assert len(DIRECTIONS) == 8
    assert len(set(DIRECTIONS)) == 8


def test_a_toggled_stick_row_builds_a_real_step():
    """The bug: this used to raise unknown key 'LS' at apply time."""
    grid = MacroGrid()
    grid.toggle(0, "LS")
    steps = grid.to_steps()
    assert steps[0].mask != p.MACRO_ANALOG_NEUTRAL


def test_a_toggled_stick_points_somewhere_by_default():
    grid = MacroGrid()
    grid.toggle(0, "LS")
    assert grid.direction(0, "LS") == "UP"


def test_toggling_a_stick_off_removes_its_heading():
    grid = MacroGrid()
    grid.toggle(0, "RS")
    grid.toggle(0, "RS")
    assert grid.direction(0, "RS") is None
    assert grid.steps[0].empty


def test_aiming_a_stick_replaces_the_previous_heading():
    """One stick cannot point two ways in the same step."""
    grid = MacroGrid()
    grid.point(0, "LS", "UP")
    grid.point(0, "LS", "DOWN_LEFT")
    assert grid.direction(0, "LS") == "DOWN_LEFT"
    assert len([k for k in grid.steps[0].keys if k.startswith("LS")]) == 1


def test_both_sticks_can_point_in_one_step():
    grid = MacroGrid()
    grid.point(0, "LS", "LEFT")
    grid.point(0, "RS", "RIGHT")
    assert grid.direction(0, "LS") == "LEFT"
    assert grid.direction(0, "RS") == "RIGHT"
    grid.to_steps()


def test_a_stick_and_buttons_share_a_step():
    grid = MacroGrid()
    grid.point(0, "LS", "UP_RIGHT")
    grid.toggle(0, "A")
    mask = grid.to_steps()[0].mask
    assert mask == p.mask_for([p.parse_token("LS_UP_RIGHT"), p.Key.A])


def test_a_heading_the_hardware_lacks_is_refused():
    grid = MacroGrid()
    with pytest.raises(ValueError):
        grid.point(0, "LS", "NORTH_BY_NORTHWEST")


def test_a_stick_step_survives_the_round_trip_off_the_pad():
    grid = MacroGrid()
    grid.point(0, "LS", "DOWN")
    grid.toggle(0, "B")

    payload = p.build_macro_payload(grid.to_steps())
    rebuilt = MacroGrid.from_program(p.parse_macro_payload(payload))
    assert rebuilt.direction(0, "LS") == "DOWN"
    assert "B" in rebuilt.steps[0].keys


def test_tokens_split_back_into_stick_and_heading():
    assert split_token("LS_DOWN_LEFT") == ("LS", "DOWN_LEFT")
    assert split_token("RS_UP") == ("RS", "UP")
    assert split_token("A") is None
