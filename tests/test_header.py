"""The header actions, and the update check, with no network."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication, QMessageBox     # noqa: E402

from x20ctl import __version__                               # noqa: E402
from x20ctl.gui.header import HeaderBar                      # noqa: E402
from x20ctl.gui.roster import Roster                         # noqa: E402
from x20ctl.gui.updates import (                             # noqa: E402
    Release, check, is_newer, parse_version,
)

app = QApplication.instance() or QApplication([])


# -- version comparison ---------------------------------------------------

def test_versions_compare_by_number_not_by_text():
    """'0.10.0' is newer than '0.9.0', which string comparison gets wrong."""
    assert is_newer("0.10.0", "0.9.0")
    assert not is_newer("0.9.0", "0.10.0")


def test_a_leading_v_and_a_suffix_are_tolerated():
    assert parse_version("v0.2.1") == (0, 2, 1)
    assert parse_version("0.3.0-beta") == (0, 3, 0)
    assert is_newer("v0.3.0", "0.2.1")


def test_the_same_version_is_not_newer():
    assert not is_newer(__version__, __version__)


def test_nonsense_sorts_lowest_rather_than_raising():
    """A tag somebody typed by hand should not break the check."""
    assert parse_version("") == (0,)
    assert not is_newer("not-a-version", "0.2.1")


def test_a_newer_release_is_reported_with_its_version():
    message, release = check(
        "0.2.1", lambda: Release(version="v0.4.0", notes="macros"))
    assert "0.4.0" in message
    assert release is not None


def test_being_up_to_date_says_so():
    message, release = check("0.2.1", lambda: Release(version="0.2.1"))
    assert "newest" in message
    assert release is None


def test_a_network_failure_is_a_sentence_not_a_crash():
    def broken():
        raise OSError("getaddrinfo failed")

    message, release = check("0.2.1", broken)
    assert "Could not reach GitHub" in message
    assert "getaddrinfo" in message
    assert release is None


def test_a_release_with_no_version_is_handled():
    message, release = check("0.2.1", lambda: Release(version=""))
    assert "did not name a version" in message
    assert release is None


# -- header ---------------------------------------------------------------

def test_the_header_names_the_controller():
    roster = Roster()
    slot = roster.add("98:B6:ED:E3:15:C4", product="EasySMX X20", player=2)
    bar = HeaderBar()
    bar.show_slot(slot)
    assert bar.title.text() == "EasySMX X20, P2"


def test_checking_updates_shows_the_answer_in_the_header():
    bar = HeaderBar(fetch=lambda: Release(version="9.9.9"))
    bar.check_updates()
    assert "9.9.9" in bar.status.text()


def test_a_failed_check_leaves_the_header_usable():
    def broken():
        raise OSError("no network")

    bar = HeaderBar(fetch=broken)
    bar.check_updates()
    assert "Could not reach GitHub" in bar.status.text()


def test_sharing_produces_text_with_the_project_link():
    bar = HeaderBar()
    seen = []
    bar.share.connect(seen.append)
    text = bar.share_app()
    assert "github.com" in text
    assert seen == [text]
    assert "clipboard" in bar.status.text().lower()


def test_factory_reset_asks_first_and_says_what_goes():
    bar = HeaderBar()
    box = bar.confirm_reset()
    assert box.icon() == QMessageBox.Warning
    assert box.defaultButton().text().lower().strip("&") == "cancel"
    detail = box.informativeText().lower()
    assert "macros" in detail and "cannot be undone" in detail
    assert "firmware is not touched" in detail


def test_factory_reset_only_fires_when_confirmed():
    bar = HeaderBar()
    fired = []
    bar.factory_reset.connect(lambda: fired.append(True))
    box = bar.confirm_reset()
    box.reject()
    assert fired == []
    box.accept()
    assert fired == [True]


def test_help_explains_every_section_by_name():
    bar = HeaderBar()
    text = bar.show_help().informativeText().lower()
    for section in ("buttons", "sticks", "triggers", "vibration", "macros",
                    "test"):
        assert section in text


def test_about_shows_the_running_version():
    bar = HeaderBar()
    assert __version__ in bar.show_about().text()
