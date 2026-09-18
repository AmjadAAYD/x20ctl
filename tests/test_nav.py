"""The sidebar, and what Simple and Advanced each show."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication                      # noqa: E402

from x20ctl.gui.nav import (                                     # noqa: E402
    ADVANCED, SECTIONS, SIMPLE, NavRail, sections_for,
)

app = QApplication.instance() or QApplication([])


def test_simple_mode_hides_the_settings_that_need_explaining():
    simple = [s.key for s in sections_for(SIMPLE)]
    assert "buttons" in simple and "macros" in simple
    assert "device" not in simple, "factory reset is not a casual button"


def test_the_sleep_timer_is_not_an_advanced_setting():
    """A user asked why Power was behind Advanced, and they were right.

    Advanced is for settings that need a sentence of explanation before they
    are safe to touch. A shutdown timer does not: the worst case is that the
    controller stays awake. Moved into Simple 2026-08-16.
    """
    assert "timeout" in [s.key for s in sections_for(SIMPLE)]
    assert not next(s for s in SECTIONS if s.key == "timeout").advanced


def test_advanced_shows_everything_simple_shows_and_more():
    simple = {s.key for s in sections_for(SIMPLE)}
    advanced = {s.key for s in sections_for(ADVANCED)}
    assert simple < advanced, "advanced is a superset, not a different app"
    assert advanced == {s.key for s in SECTIONS}


def test_every_section_explains_itself():
    """Tooltips are the tutorial. A section with no blurb teaches nothing."""
    for section in SECTIONS:
        assert section.blurb.strip(), f"{section.key} has no explanation"
        assert section.blurb.strip()[-1] == ".", f"{section.key} blurb is a fragment"


def test_the_rail_starts_on_the_first_section():
    rail = NavRail()
    assert rail.current() == "buttons"


def test_choosing_a_section_reports_it():
    rail = NavRail()
    seen = []
    rail.selected.connect(seen.append)
    rail.select("macros")
    assert seen[-1] == "macros"
    assert rail.current() == "macros"


def test_switching_to_advanced_adds_the_extra_sections():
    rail = NavRail()
    assert "device" not in rail._buttons
    rail.set_mode(ADVANCED)
    assert "device" in rail._buttons
    assert rail.mode_button.text() == "Simple mode"


def test_a_section_survives_the_mode_switch_when_it_still_exists():
    rail = NavRail()
    rail.select("macros")
    rail.set_mode(ADVANCED)
    assert rail.current() == "macros", "you were reading macros; stay there"


def test_leaving_advanced_while_on_an_advanced_page_lands_somewhere_real():
    """Simple mode has no Device section, so staying put is not an option."""
    rail = NavRail(mode=ADVANCED)
    rail.select("device")
    rail.set_mode(SIMPLE)
    assert rail.current() == "buttons"
    assert "device" not in rail._buttons


def test_toggling_twice_returns_to_where_it_started():
    rail = NavRail()
    rail.toggle_mode()
    rail.toggle_mode()
    assert rail.mode == SIMPLE
    assert rail.mode_button.text() == "Advanced mode"


def test_the_mode_change_is_announced_once():
    rail = NavRail()
    seen = []
    rail.mode_changed.connect(seen.append)
    rail.set_mode(ADVANCED)
    rail.set_mode(ADVANCED)
    assert seen == [ADVANCED], "setting the mode it already has changes nothing"
