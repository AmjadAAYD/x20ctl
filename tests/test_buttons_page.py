"""The buttons page: which buttons can be sources, and what they can become."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl import protocol as p                     # noqa: E402
from x20ctl.gui.buttons import (                     # noqa: E402
    UNCHANGED, ButtonsPage, label_for,
)

app = QApplication.instance() or QApplication([])

SOURCES = list(p.CHANGEKEY_DEFAULT_SOURCES)


def test_select_and_start_get_no_row_because_the_pad_ignores_them():
    """They appear in the pad's source list, accept a write, read it back and
    then do nothing. A row for them would be a control that lies."""
    page = ButtonsPage()
    page.load(SOURCES)
    assert int(p.Key.SELECT) not in page.boxes
    assert int(p.Key.START) not in page.boxes
    assert len(page.boxes) == len(SOURCES) - 2


def test_c_and_t_are_offered_as_targets_although_never_as_sources():
    page = ButtonsPage()
    page.load(SOURCES)
    box = page.boxes[int(p.Key.A)]
    offered = [box.itemData(i) for i in range(box.count())]
    assert int(p.Key.CAPTURE) in offered
    assert int(p.Key.TURBO) in offered
    assert int(p.Key.CAPTURE) not in page.boxes


def test_select_and_start_remain_available_as_targets():
    page = ButtonsPage()
    page.load(SOURCES)
    box = page.boxes[int(p.Key.A)]
    offered = [box.itemData(i) for i in range(box.count())]
    assert int(p.Key.SELECT) in offered


def test_a_fresh_page_reports_no_remapping():
    page = ButtonsPage()
    page.load(SOURCES)
    assert page.mapping() == {}


def test_choosing_a_target_shows_up_in_the_mapping():
    page = ButtonsPage()
    page.load(SOURCES)
    box = page.boxes[int(p.Key.A)]
    box.setCurrentIndex(box.findData(int(p.Key.B)))
    assert page.mapping() == {int(p.Key.A): int(p.Key.B)}


def test_pointing_a_button_at_itself_is_not_a_remapping():
    page = ButtonsPage()
    page.load(SOURCES)
    box = page.boxes[int(p.Key.A)]
    box.setCurrentIndex(box.findData(int(p.Key.A)))
    assert page.mapping() == {}


def test_loading_shows_what_the_pad_already_holds():
    page = ButtonsPage()
    page.load(SOURCES, {int(p.Key.A): int(p.Key.TURBO)})
    box = page.boxes[int(p.Key.A)]
    assert box.currentData() == int(p.Key.TURBO)
    assert page.mapping() == {int(p.Key.A): int(p.Key.TURBO)}


def test_clearing_puts_every_button_back_to_unchanged():
    page = ButtonsPage()
    page.load(SOURCES, {int(p.Key.A): int(p.Key.B), int(p.Key.X): int(p.Key.Y)})
    page.clear()
    assert page.mapping() == {}
    assert page.boxes[int(p.Key.A)].currentData() == UNCHANGED


def test_changes_are_announced():
    page = ButtonsPage()
    page.load(SOURCES)
    seen = []
    page.changed.connect(seen.append)
    box = page.boxes[int(p.Key.X)]
    box.setCurrentIndex(box.findData(int(p.Key.Y)))
    assert seen[-1] == {int(p.Key.X): int(p.Key.Y)}


def test_reloading_does_not_leave_old_rows_behind():
    page = ButtonsPage()
    page.load(SOURCES)
    page.load(SOURCES)
    assert len(page.boxes) == len(SOURCES) - 2


def test_the_page_follows_whatever_sources_the_pad_reports():
    """A different pad reports a different list, and the page is built from
    that rather than from a table baked in here."""
    page = ButtonsPage()
    page.load([int(p.Key.A), int(p.Key.B)])
    assert set(page.boxes) == {int(p.Key.A), int(p.Key.B)}


def test_awkward_names_are_written_the_way_people_say_them():
    assert label_for(int(p.Key.DPAD_UP)) == "D-pad up"
    assert label_for(int(p.Key.CAPTURE)) == "C"
    assert label_for(int(p.Key.TURBO)) == "T"
    assert label_for(0xEE) == "0xee", "an unknown code still has to render"
