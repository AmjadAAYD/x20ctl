"""Nothing reaches the controller until Save is pressed."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication          # noqa: E402

from x20ctl import protocol as p                     # noqa: E402
from x20ctl.gui.buttons import ButtonsPage           # noqa: E402
from x20ctl.gui.panels import PowerPage              # noqa: E402
from x20ctl.gui.triggers import TriggersPage         # noqa: E402

app = QApplication.instance() or QApplication([])

SOURCES = list(p.CHANGEKEY_DEFAULT_SOURCES)


def test_every_page_that_edits_the_pad_has_a_save_button():
    for page in (ButtonsPage(), TriggersPage(), PowerPage()):
        assert hasattr(page, "save_button"), f"{type(page).__name__} cannot save"
        assert "save" in page.save_button.text().lower()


def test_changing_a_button_does_not_write_it():
    page = ButtonsPage()
    page.load(SOURCES)
    saved = []
    page.save_requested.connect(saved.append)

    box = page.boxes[int(p.Key.A)]
    box.setCurrentIndex(box.findData(int(p.Key.B)))
    assert saved == [], "an edit is not a decision"
    assert "not saved" in page.status.text().lower()


def test_pressing_save_sends_what_is_on_screen():
    page = ButtonsPage()
    page.load(SOURCES)
    saved = []
    page.save_requested.connect(saved.append)

    box = page.boxes[int(p.Key.A)]
    box.setCurrentIndex(box.findData(int(p.Key.B)))
    page.save()
    assert saved == [{int(p.Key.A): int(p.Key.B)}]


def test_saving_an_empty_mapping_still_sends_it():
    """Clearing every remap is a change, and has to reach the pad."""
    page = ButtonsPage()
    page.load(SOURCES, {int(p.Key.A): int(p.Key.B)})
    saved = []
    page.save_requested.connect(saved.append)
    page.clear()
    page.save()
    assert saved == [{}]


def test_choosing_a_trigger_zone_does_not_write_it():
    page = TriggersPage()
    saved = []
    page.save_requested.connect(lambda: saved.append(True))
    page.sides["left"].zones.buttons["large"].click()
    assert saved == []
    assert "not saved" in page.status.text().lower()


def test_pressing_save_on_triggers_asks_for_a_write():
    page = TriggersPage()
    saved = []
    page.save_requested.connect(lambda: saved.append(True))
    page._save()
    assert saved == [True]


def test_the_power_page_saves_the_value_on_screen():
    page = PowerPage()
    page.load(20)
    saved = []
    page.save_requested.connect(saved.append)
    page.save_button.click()
    assert saved == [20]


def test_the_power_page_saves_never_as_none():
    page = PowerPage()
    page.never.setChecked(True)
    saved = []
    page.save_requested.connect(saved.append)
    page.save_button.click()
    assert saved == [None]
