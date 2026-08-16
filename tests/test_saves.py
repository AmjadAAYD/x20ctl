"""Save files: one file, one whole controller."""

from __future__ import annotations

import os
import sys
import tempfile

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

pytest.importorskip("PySide6")

from PySide6.QtWidgets import QApplication      # noqa: E402

from x20ctl.gui.saves import SavesBar            # noqa: E402
from x20ctl.gui.shell import AppShell            # noqa: E402
from x20ctl.profiles import ProfileStore         # noqa: E402

app = QApplication.instance() or QApplication([])
_alive = []


def shell_with_store(directory):
    shell = AppShell()
    _alive.append(shell)
    shell.add_controller("98:B6:ED:E3:15:C4", "X20", 1)
    shell.open_controller(1)
    work = shell.workspace
    work.store = lambda: ProfileStore(directory)
    return work


def test_nothing_can_be_loaded_or_deleted_before_a_file_is_chosen():
    bar = SavesBar()
    assert not bar.load_button.isEnabled()
    assert not bar.delete_button.isEnabled()


def test_choosing_a_file_enables_the_actions():
    bar = SavesBar()
    bar.show_saves(["Rocket League"])
    bar.select("Rocket League")
    assert bar.load_button.isEnabled()
    assert bar.selected() == "Rocket League"


def test_a_save_holds_all_four_macros_not_one_slot():
    """The whole point: a setup is four macros, not a file per slot."""
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        for slot, key in (("M1", "A"), ("M3", "B")):
            work.pages["macros"].grids[slot].toggle(0, key)
        work.pages["motor"].load(45)

        work._save_profile("Rocket League")

        profile = ProfileStore(tmp).load("Rocket League")
        assert profile.vibration == 45
        assert profile.macros["M1"] is not None
        assert profile.macros["M3"] is not None
        assert profile.macros["M2"] is None, "an empty slot stays empty"


def test_saving_then_loading_puts_the_macros_back():
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        work.pages["macros"].grids["M1"].toggle(0, "A")
        work.pages["macros"].grids["M1"].set_duration(0, 150)
        work._save_profile("Setup")

        work.pages["macros"].clear()
        work.pages["macros"].grids["M1"].clear()
        assert work.pages["macros"].grids["M1"].empty

        work._load_profile("Setup")
        grid = work.pages["macros"].grids["M1"]
        assert not grid.empty
        assert "A" in grid.steps[0].keys


def test_loading_restores_the_vibration_ceiling_too():
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        work.pages["motor"].load(70)
        work.pages["macros"].grids["M1"].toggle(0, "A")
        work._save_profile("Loud")

        work.pages["motor"].load(10)
        work._load_profile("Loud")
        assert work.pages["motor"].value() == 70


def test_the_list_shows_what_was_saved():
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        work.pages["macros"].grids["M1"].toggle(0, "A")
        work._save_profile("One")
        work._save_profile("Two")

        names = [work.pages["macros"].saves.list.item(i).text()
                 for i in range(work.pages["macros"].saves.list.count())]
        assert set(names) == {"One", "Two"}


def test_deleting_removes_it_from_the_list():
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        work.pages["macros"].grids["M1"].toggle(0, "A")
        work._save_profile("Gone")
        work._delete_profile("Gone")
        assert work.pages["macros"].saves.list.count() == 0


def test_loading_something_missing_says_so_rather_than_raising():
    with tempfile.TemporaryDirectory() as tmp:
        work = shell_with_store(tmp)
        work._load_profile("never existed")
        assert "Could not load" in work.status.text()
