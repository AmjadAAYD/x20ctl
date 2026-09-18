"""A taskbar tray icon showing controller battery.

Asked for by a user who wanted Windows-style battery at a glance without the
window open. Their request also asked for finer resolution than the pad's own
25% steps; that part is not possible and the tooltip says so rather than
inventing precision. `protocol.BATTERY_LEVELS` is 4 because the hardware reports
four states, not because we round.

The icon is drawn rather than loaded, so there are no new asset files and it
scales to whatever the tray asks for.
"""

from __future__ import annotations

from PySide6.QtCore import QRect, Qt, Signal
from PySide6.QtGui import QColor, QIcon, QPainter, QPixmap
from PySide6.QtWidgets import QMenu, QSystemTrayIcon

from .. import protocol as p

SIZE = 64

# Deliberately not from theme.py: the tray sits on the OS chrome, not our
# window, so it needs to read against both a light and a dark taskbar.
OUTLINE = QColor("#e8e8ea")
EMPTY = QColor(0, 0, 0, 0)
FULL = QColor("#4ade80")        # green, 3-4 of 4
MID = QColor("#fbbf24")         # amber, 2 of 4
LOW = QColor("#f87171")         # red, 1 of 4
CHARGE = QColor("#38bdf8")      # blue, charging at any level


def level_colour(level: int, charging: bool) -> QColor:
    if charging:
        return CHARGE
    if level <= 1:
        return LOW
    if level == 2:
        return MID
    return FULL


def battery_icon(level: int | None, charging: bool = False) -> QIcon:
    """A battery glyph filled to `level` of BATTERY_LEVELS. None means unknown."""
    pix = QPixmap(SIZE, SIZE)
    pix.fill(Qt.transparent)
    painter = QPainter(pix)
    painter.setRenderHint(QPainter.Antialiasing, False)

    body = QRect(6, 18, 44, 28)
    nub = QRect(50, 26, 6, 12)

    painter.setPen(OUTLINE)
    painter.setBrush(EMPTY)
    painter.drawRect(body)
    painter.setBrush(OUTLINE)
    painter.setPen(Qt.NoPen)
    painter.drawRect(nub)

    if level:
        span = body.width() - 6
        width = max(2, round(span * level / p.BATTERY_LEVELS))
        painter.setBrush(level_colour(level, charging))
        painter.drawRect(QRect(body.x() + 3, body.y() + 3, width,
                               body.height() - 6))
    else:
        # Unknown is a question of fact, so show nothing rather than empty,
        # which would read as flat.
        painter.setPen(OUTLINE)
        painter.drawText(body, Qt.AlignCenter, "?")

    painter.end()
    return QIcon(pix)


def describe(battery) -> str:
    if battery is None:
        return ("x20ctl\nBattery: not read yet\n"
                "The controller reports charge in four steps only.")
    state = " and charging" if battery.charging else ""
    return (f"x20ctl\nBattery: {battery.level} of {p.BATTERY_LEVELS}"
            f" (about {battery.approximate_percent}%){state}\n"
            "The controller reports four steps only; finer detail is not "
            "available from the hardware.")


class BatteryTray(QSystemTrayIcon):
    """Tray icon that mirrors whatever the workspace last read."""

    show_requested = Signal()
    quit_requested = Signal()

    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self._battery = None

        menu = QMenu()
        self.show_action = menu.addAction("Show x20ctl")
        self.show_action.triggered.connect(self.show_requested.emit)
        menu.addSeparator()
        self.battery_action = menu.addAction("Battery: not read yet")
        self.battery_action.setEnabled(False)
        menu.addSeparator()
        self.quit_action = menu.addAction("Quit")
        self.quit_action.triggered.connect(self.quit_requested.emit)
        self.setContextMenu(menu)
        self._menu = menu

        self.activated.connect(self._on_activated)
        self.show_battery(None)

    def _on_activated(self, reason) -> None:
        if reason in (QSystemTrayIcon.Trigger, QSystemTrayIcon.DoubleClick):
            self.show_requested.emit()

    def show_battery(self, battery) -> None:
        """Take a protocol.Battery, or None when nothing has been read."""
        self._battery = battery
        level = getattr(battery, "level", None)
        charging = bool(getattr(battery, "charging", False))
        self.setIcon(battery_icon(level, charging))
        self.setToolTip(describe(battery))
        if battery is None:
            self.battery_action.setText("Battery: not read yet")
        else:
            self.battery_action.setText(
                f"Battery: {battery.level}/{p.BATTERY_LEVELS}"
                + (" (charging)" if battery.charging else ""))

    @property
    def battery(self):
        return self._battery
