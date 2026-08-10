"""Headless construction test for the GUI.

Builds the window offscreen and exercises the pure-logic paths: the form
round-trip, validation feedback, and that a malformed macro disables Apply. No
event loop is run, so no BLE connection is attempted.

Skips cleanly if PySide6 isn't installed.
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
    # the reason is shown in place of the summary not in a second label
    assert "unknown key" in card.summary.text().lower()


def test_non_macro_capable_button_is_flagged():
    card = MacroCard("M4")
    card.keys.setText("START")
    assert not card.is_valid()
    assert "not macro-capable" in card.summary.text()


def test_clear_empties_the_card():
    card = MacroCard("M1")
    card.set_spec(MacroSpec(keys="X,Y"))
    card.clear()
    assert card.spec() is None


def test_looping_macro_is_marked_as_a_warning():
    card = MacroCard("M1")
    card.set_spec(MacroSpec(keys="A", loop_ms=200))
    assert "loops" in card.summary.text()
    assert "repeats until interrupted" in card.summary.text()


def test_vibration_row_reports_its_value():
    row = VibrationRow()
    row.set_value(65)
    assert row.value() == 65
    assert row.readout.text() == "65%"


def test_window_builds_and_loads_a_profile():
    from x20ctl.gui.window import MainWindow

    window = MainWindow(auto_connect=False)
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

        window = MainWindow(auto_connect=False)
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

        window = MainWindow(auto_connect=False)
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
    settings, not strand you on a page that can't show it."""
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="one"))
        store.save(Profile(name="two"))

        window = MainWindow(auto_connect=False)
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


def test_repeated_easing_does_not_touch_a_deleted_animation():
    """Regression: ease_to kept a reference to an animation started with
    DeleteWhenStopped, so the second call stopped an object Qt had destroyed.
    Hovering a card twice triggered it, and pythonw has no console to show it."""
    from x20ctl.gui.motion import Animatable, ease_to

    value = Animatable(0.0)
    for _ in range(30):
        ease_to(value, 1.0)
        ease_to(value, 0.0)
    assert value.get_value() is not None


def test_hovering_a_card_repeatedly_is_safe():
    """The same fault reached through the card's own hover animation."""
    from x20ctl.gui.motion import ease_to

    card = MacroCard("M1")
    for _ in range(20):
        ease_to(card._hover, 1.0)
        ease_to(card._hover, 0.0)
    assert card.is_valid()


def test_unsaved_edits_are_detected():
    """The bug: clearing a field and closing lost the macro without asking."""
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        saved = Profile(name="one")
        saved.macros["M1"] = MacroSpec(keys="A+B")
        store.save(saved)

        window = MainWindow(auto_connect=False)
        try:
            window.store = store
            window.reload_profiles(select="one")
            assert not window.has_unsaved_changes()

            window.cards["M1"].keys.clear()          # the backspace case
            assert window.has_unsaved_changes()
            # clearing a slot is the one thing worth interrupting for
            assert window.emptied_slots() == ["M1"]
        finally:
            window.bridge.shutdown()


def test_a_saved_edit_is_no_longer_dirty():
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="one"))

        window = MainWindow(auto_connect=False)
        try:
            window.store = store
            window.reload_profiles(select="one")
            window.cards["M2"].keys.setText("X,Y")
            assert window.has_unsaved_changes()
            assert window.emptied_slots() == [], "adding a macro is not destruction"

            window._autosave_now()          # what the debounce timer would do
            assert not window.has_unsaved_changes()
            assert store.load("one").macros["M2"].keys == "X,Y"
        finally:
            window.bridge.shutdown()


def test_edits_save_themselves():
    """No Save button: typing a macro and waiting is enough."""
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="auto"))

        window = MainWindow(auto_connect=False)
        try:
            window.store = store
            window.reload_profiles(select="auto")
            window.cards["M1"].keys.setText("A+B")
            window._autosave_now()
            assert store.load("auto").macros["M1"].keys == "A+B"
        finally:
            window.bridge.shutdown()


def test_restoring_a_cleared_slot_puts_it_back():
    import tempfile

    from x20ctl.gui.window import MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        profile = Profile(name="one")
        profile.macros["M1"] = MacroSpec(keys="A+B")
        store.save(profile)

        window = MainWindow(auto_connect=False)
        try:
            window.store = store
            window.reload_profiles(select="one")
            window.cards["M1"].keys.clear()
            window._autosave_now()
            assert store.load("one").macros["M1"] is None, "autosave applies"

            # what the Restore button does
            for slot in window.emptied_slots():
                window.cards[slot].set_spec(window._session_baseline.get(slot))
            window.autosave()
            assert store.load("one").macros["M1"].keys == "A+B"
        finally:
            window.bridge.shutdown()


def test_the_header_names_the_product_and_both_versions():
    """The screenshot tool once hardcoded "Xpert2" here and went stale.

    Anything showing this line has to derive the name, so a mapping added to
    PRODUCT_NAMES reaches every place it's shown.
    """
    from x20ctl import __version__, protocol as p
    from x20ctl.gui.window import MainWindow

    assert p.product_name("Xpert2", "0710", "1320") == "EasySMX X20"
    assert p.product_name("Xpert2") == "EasySMX X20", "by reported name alone"

    line = MainWindow.describe_device("9.01", "98:B6:ED:E3:15:C4")
    assert "firmware 9.01" in line
    assert f"x20ctl {__version__}" in line, "the app's version, labelled"
    assert "98:B6:ED:E3:15:C4" in line


# --- the curves page --------------------------------------------------------

def _stick_curves():
    from x20ctl import protocol as p
    return [p.Curve.parse(bytes.fromhex("08085555aaaa00"), p.STICK_MAX_PROGRESS)
            for _ in range(2)]


def test_curves_page_reports_nothing_pending_until_something_moves():
    from x20ctl.gui.curves import CurvesPage

    page = CurvesPage()
    page.set_available(True, True)
    page.load("sticks", _stick_curves(), baseline=True)
    assert page.changed_kinds() == []
    assert not page.write_button.isEnabled(), "nothing to write yet"

    editor = page.editors[("sticks", 0)]
    editor.inner.setValue(20)
    assert page.changed_kinds() == ["sticks"]
    assert page.write_button.isEnabled()
    assert page.values("sticks")[0].inner_deadzone == 20
    assert page.values("sticks")[1].inner_deadzone == 8, "only one side moved"


def test_curves_page_keeps_the_pads_own_values_to_go_back_to():
    from x20ctl.gui.curves import CurvesPage

    page = CurvesPage()
    page.set_available(True, True)
    page.load("sticks", _stick_curves(), baseline=True)
    page.editors[("sticks", 0)].straighten.click()      # already linear, no change
    page.editors[("sticks", 1)].inner.setValue(30)
    assert page.baseline("sticks")[1].inner_deadzone == 8


def test_deadzone_sliders_cannot_be_pushed_past_each_other():
    """Inner at or beyond outer would leave the stick no travel at all."""
    from x20ctl.gui.curves import CurvesPage

    page = CurvesPage()
    page.set_available(True, True)
    page.load("sticks", _stick_curves(), baseline=True)
    editor = page.editors[("sticks", 0)]

    editor.inner.setValue(100)
    assert editor.inner.value() < editor.outer.value()
    editor.curve().validate()

    editor.outer.setValue(0)
    assert editor.inner.value() < editor.outer.value()
    editor.curve().validate()


def test_dragging_a_control_point_cannot_make_it_overtake_the_other():
    from PySide6.QtCore import QPointF

    from x20ctl.gui.curves import CurvePlot

    plot = CurvePlot()
    plot.resize(200, 200)
    plot.set_curve(_stick_curves()[0])

    plot._dragging = 0                       # as if the first point were grabbed
    plot._drag_to(QPointF(199.0, 4.0))       # dragged past the second, top right
    assert plot.curve().point1[0] <= plot.curve().point2[0]
    plot.curve().validate()

    plot._dragging = 1
    plot._drag_to(QPointF(0.0, 199.0))       # and the second past the first
    assert plot.curve().point1[0] <= plot.curve().point2[0]
    plot.curve().validate()


def test_trigger_channels_hide_flags_that_are_only_decoded_for_sticks():
    from x20ctl.gui.curves import CurvesPage

    page = CurvesPage()
    assert page.editors[("sticks", 0)].invert_x.isHidden() is False
    assert page.editors[("triggers", 0)].invert_x.isHidden() is True


def test_curves_page_is_reachable_and_leaves_the_tester_stopped():
    import tempfile

    from x20ctl.gui.window import CURVES_PAGE, MainWindow
    from x20ctl.profiles import ProfileStore

    with tempfile.TemporaryDirectory() as tmp:
        window = MainWindow(auto_connect=False)
        try:
            window.store = ProfileStore(tmp)
            window.tester_button.setChecked(True)
            window.toggle_tester()
            assert window.tester.timer.isActive()

            window.curves_button.setChecked(True)
            window.toggle_curves()
            assert window.pages.currentIndex() == CURVES_PAGE
            assert not window.tester.timer.isActive(), "the tester must stop"
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
