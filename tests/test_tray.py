"""The battery tray icon.

Requested by a user who wanted battery at a glance without the window open.
The same request asked for finer resolution than the pad's 25% steps; the tray
must not pretend to offer that, because BATTERY_LEVELS is 4 for hardware
reasons, not display ones.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication, QSystemTrayIcon

from x20ctl import protocol as p
from x20ctl.gui.tray import BatteryTray, battery_icon, describe, level_colour

app = QApplication.instance() or QApplication([])


def test_icon_is_drawn_for_every_level_and_unknown():
    for level in range(1, p.BATTERY_LEVELS + 1):
        assert not battery_icon(level).isNull()
    assert not battery_icon(None).isNull(), "unknown still needs a glyph"
    assert not battery_icon(3, charging=True).isNull()


def test_charging_overrides_the_level_colour():
    """A charging pad reads as charging whatever its level."""
    charging = {level_colour(n, True).name() for n in range(1, 5)}
    assert len(charging) == 1, "charging should be one colour at any level"
    assert level_colour(1, False) != level_colour(4, False)


def test_tooltip_states_the_hardware_limit():
    text = describe(p.Battery(level=2, charging=False, status=0x20))
    assert "2 of 4" in text
    assert "50%" in text
    assert "four steps" in text, (
        "the tooltip must say the granularity is the hardware's, not ours")
    assert "charging" not in text


def test_tooltip_before_anything_is_read_makes_no_claim():
    text = describe(None)
    assert "not read yet" in text
    assert "0%" not in text and "1 of 4" not in text


def test_tray_mirrors_readings_and_starts_unknown():
    if not QSystemTrayIcon.isSystemTrayAvailable():
        return
    tray = BatteryTray()
    assert tray.battery is None
    assert "not read yet" in tray.battery_action.text()

    tray.show_battery(p.Battery(level=3, charging=True, status=0x50))
    assert tray.battery.level == 3
    assert "3/4" in tray.battery_action.text()
    assert "charging" in tray.battery_action.text()

    tray.show_battery(None)
    assert "not read yet" in tray.battery_action.text()


def test_quit_and_show_are_offered():
    if not QSystemTrayIcon.isSystemTrayAvailable():
        return
    tray = BatteryTray()
    labels = [a.text() for a in tray.contextMenu().actions() if a.text()]
    assert any("Show" in t for t in labels)
    assert any("Quit" in t for t in labels)
