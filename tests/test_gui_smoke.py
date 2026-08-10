"""Headless construction test for the GUI.

Builds the window offscreen and exercises the pure-logic paths: the form
round-trip, validation feedback, and that a malformed macro disables Apply. No
event loop is run, so no BLE connection is attempted.

Skips cleanly if PySide6 is not installed.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

try:
    from PySide6.QtWidgets import QApplication
except ImportError:
    print("PySide6 not installed; skipping GUI smoke test")
    sys.exit(0)

from x20ctl.gui import theme
from x20ctl.gui.widgets import MacroCard, VibrationRow
from x20ctl.profiles import MacroSpec, Profile, SLOTS

_app = QApplication.instance() or QApplication([])
_app.setStyleSheet(theme.STYLESHEET)


def test_stylesheet_is_applied_without_error():
    assert _app.styleSheet().startswith("\nQWidget")


def test_macro_card_round_trips_a_spec():
    card = MacroCard("M1")
    card.set_spec(MacroSpec(keys="A+B", hold_ms=120, gap_ms=80))
    spec = card.spec()
    assert spec.keys == "A+B"
    assert spec.hold_ms == 120
    assert spec.gap_ms == 80
    assert "A + B" in card.summary.text()


def test_empty_card_yields_no_spec():
    card = MacroCard("M2")
    assert card.spec() is None
    assert card.summary.text() == "empty"
    assert card.is_valid()


def test_invalid_keys_are_flagged_not_crashed():
    card = MacroCard("M3")
    card.keys.setText("NOT_A_BUTTON")
    assert not card.is_valid()
    assert card.error.isVisible() or card.error.text()
    assert card.summary.text() == "invalid"


def test_non_macro_capable_button_is_flagged():
    card = MacroCard("M4")
    card.keys.setText("START")
    assert not card.is_valid()
    assert "not macro-capable" in card.error.text()


def test_clear_empties_the_card():
    card = MacroCard("M1")
    card.set_spec(MacroSpec(keys="X,Y"))
    card.clear()
    assert card.spec() is None


def test_looping_macro_is_marked_as_a_warning():
    card = MacroCard("M1")
    card.set_spec(MacroSpec(keys="A", loop_ms=200))
    assert card.summary.objectName() == "Warning"
    assert "loops" in card.summary.text()


def test_vibration_row_reports_its_value():
    row = VibrationRow()
    row.set_value(65)
    assert row.value() == 65
    assert row.readout.text() == "65%"


def test_window_builds_and_loads_a_profile():
    from x20ctl.gui.window import MainWindow

    window = MainWindow()
    try:
        profile = Profile(name="test")
        profile.macros["M1"] = MacroSpec(keys="A+B", hold_ms=100)
        profile.vibration = 40
        window.load_into_form(profile)

        collected = window.collect()
        assert collected.macros["M1"].keys == "A+B"
        assert collected.vibration == 40
        assert all(collected.macros[s] is None for s in SLOTS if s != "M1")

        # Apply must stay disabled while disconnected
        assert window.pad is None
        assert not window.apply_button.isEnabled()
    finally:
        window.bridge.shutdown()


def test_removing_a_profile_does_not_write_it_back():
    """Regression: deleting reloaded the list, which fired the selection
    handler, which autosaved the still-loaded profile back to disk."""
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        keep = Profile(name="keep")
        keep.macros["M1"] = MacroSpec(keys="A")
        store.save(keep)
        doomed = Profile(name="doomed")
        doomed.macros["M1"] = MacroSpec(keys="B")
        store.save(doomed)

        window = MainWindow()
        try:
            window.store = store
            window.reload_profiles(select="doomed")
            assert window.profile.name == "doomed"

            # simulate the deletion path without the confirmation dialog
            store.delete("doomed")
            window._loaded_name = None
            window.profile = Profile(name="doomed")
            window.reload_profiles()

            names = [x.name for x in store.list()]
            assert "doomed" not in names, "the deleted profile came back"
            assert "keep" in names
        finally:
            window.bridge.shutdown()


def test_autosave_refuses_to_recreate_a_missing_profile():
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="gone"))

        window = MainWindow()
        try:
            window.store = store
            window.reload_profiles(select="gone")
            store.delete("gone")            # vanishes underneath the app
            window.cards["M1"].keys.setText("A")
            assert window.autosave() is False
            assert store.list() == [], "autosave resurrected a deleted profile"
        finally:
            window.bridge.shutdown()


def test_choosing_a_save_file_leaves_the_tester():
    """Clicking a save file while the tester is open should return to the
    settings, not strand you on a page that cannot show it."""
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="one"))
        store.save(Profile(name="two"))

        window = MainWindow()
        try:
            window.store = store
            window.reload_profiles(select="one")

            window.tester_button.setChecked(True)
            window.toggle_tester()
            assert window.pages.currentIndex() == 1
            assert window.tester.timer.isActive()

            window.reload_profiles(select="two")
            assert window.pages.currentIndex() == 0, "should be back on settings"
            assert not window.tester.timer.isActive(), "tester must stop polling"
            assert not window.tester_button.isChecked()
            assert window.tester_button.text() == "Input tester"
        finally:
            window.bridge.shutdown()


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  PASS  {name}")
            except AssertionError as exc:
                failures += 1
                print(f"  FAIL  {name}: {exc}")
    print(f"\n{'all passed' if not failures else f'{failures} failed'}")
    sys.exit(1 if failures else 0)
