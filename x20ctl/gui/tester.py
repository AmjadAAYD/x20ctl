"""Live input tester and polling-rate meter.

Both read XInput only. Nothing here writes to the controller.

The polling meter measures how often the controller's state actually changes,
using XInput's packet counter, which increments on every new report. At rest the
counter does not move, so a measurement is only meaningful while something is
moving; the meter says so rather than reporting a misleading zero.
"""

from __future__ import annotations

import time
from collections import deque

from PySide6.QtCore import QPointF, QRectF, Qt, QTimer
from PySide6.QtGui import QColor, QPainter, QPen
from PySide6.QtWidgets import (
    QFrame, QGridLayout, QHBoxLayout, QLabel, QPushButton, QVBoxLayout, QWidget,
)

from .. import protocol as p
from ..input import XInputReader
from . import theme
from .widgets import divider, section_label

POLL_MS = 1                 # as fast as Qt will run us
WINDOW_SECONDS = 1.0        # averaging window for the rate

# Layout of the button lamps: rows of (label, Key)
LAMP_ROWS = [
    [("A", p.Key.A), ("B", p.Key.B), ("X", p.Key.X), ("Y", p.Key.Y)],
    [("LB", p.Key.LB), ("RB", p.Key.RB), ("LT", p.Key.LT), ("RT", p.Key.RT)],
    [("L3", p.Key.L3), ("R3", p.Key.R3), ("Start", p.Key.START), ("Select", p.Key.SELECT)],
    [("Up", p.Key.DPAD_UP), ("Down", p.Key.DPAD_DOWN),
     ("Left", p.Key.DPAD_LEFT), ("Right", p.Key.DPAD_RIGHT)],
]


class Lamp(QLabel):
    """A button indicator that lights when held."""

    def __init__(self, caption: str) -> None:
        super().__init__(caption)
        self.setAlignment(Qt.AlignCenter)
        self.setFixedHeight(30)
        self.setObjectName("Lamp")
        self._lit = False
        self._apply()

    def set_lit(self, lit: bool) -> None:
        if lit != self._lit:
            self._lit = lit
            self._apply()

    def _apply(self) -> None:
        if self._lit:
            self.setStyleSheet(
                f"background:{theme.ACCENT}; color:#0b1220; border-radius:6px;"
                f"font-weight:700;")
        else:
            self.setStyleSheet(
                f"background:{theme.BG}; color:{theme.TEXT_FAINT};"
                f"border:1px solid {theme.BORDER}; border-radius:6px;")


class StickPad(QWidget):
    """A stick position, drawn as a dot inside its circle."""

    def __init__(self, caption: str) -> None:
        super().__init__()
        self.caption = caption
        self.setFixedSize(118, 128)
        self._x = 0.0
        self._y = 0.0
        self._trail: deque = deque(maxlen=48)

    def set_position(self, x: int, y: int) -> None:
        # XInput axes are signed 16-bit, y positive upward
        self._x = max(-1.0, min(1.0, x / 32767))
        self._y = max(-1.0, min(1.0, y / 32767))
        self._trail.append((self._x, self._y))
        self.update()

    def clear_trail(self) -> None:
        self._trail.clear()
        self.update()

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        side = min(self.width(), self.height() - 18) - 6
        box = QRectF((self.width() - side) / 2, 4, side, side)
        centre = box.center()
        radius = side / 2

        painter.setPen(QPen(QColor(theme.BORDER), 1))
        painter.setBrush(QColor(theme.BG))
        painter.drawEllipse(box)
        painter.setPen(QPen(QColor(theme.BORDER), 1, Qt.DotLine))
        painter.drawLine(box.left(), centre.y(), box.right(), centre.y())
        painter.drawLine(centre.x(), box.top(), centre.x(), box.bottom())

        # trail shows the path, which is what makes circularity visible
        if len(self._trail) > 1:
            painter.setPen(QPen(QColor(theme.ACCENT_DIM), 1))
            previous = None
            for tx, ty in self._trail:
                point = QPointF(centre.x() + tx * radius, centre.y() - ty * radius)
                if previous is not None:
                    painter.drawLine(previous, point)
                previous = point

        point = QPointF(centre.x() + self._x * radius, centre.y() - self._y * radius)
        painter.setPen(Qt.NoPen)
        painter.setBrush(QColor(theme.ACCENT))
        painter.drawEllipse(point, 5, 5)

        painter.setPen(QColor(theme.TEXT_FAINT))
        painter.drawText(QRectF(0, box.bottom() + 1, self.width(), 14),
                         Qt.AlignCenter, self.caption)


class TriggerBar(QWidget):
    """An analog trigger, 0 to 255."""

    def __init__(self, caption: str) -> None:
        super().__init__()
        self.caption = caption
        self.setFixedHeight(26)
        self._value = 0

    def set_value(self, value: int) -> None:
        if value != self._value:
            self._value = value
            self.update()

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setPen(QColor(theme.TEXT_FAINT))
        painter.drawText(QRectF(0, 0, 26, self.height()),
                         Qt.AlignLeft | Qt.AlignVCenter, self.caption)

        track = QRectF(30, self.height() / 2 - 4, self.width() - 74, 8)
        painter.setPen(Qt.NoPen)
        painter.setBrush(QColor(theme.BORDER))
        painter.drawRoundedRect(track, 4, 4)

        if self._value:
            filled = QRectF(track)
            filled.setWidth(track.width() * self._value / 255)
            painter.setBrush(QColor(theme.SUCCESS))
            painter.drawRoundedRect(filled, 4, 4)

        painter.setPen(QColor(theme.TEXT))
        painter.drawText(QRectF(self.width() - 40, 0, 40, self.height()),
                         Qt.AlignRight | Qt.AlignVCenter, str(self._value))


class TesterPage(QWidget):
    """Live input and polling rate. Read-only."""

    def __init__(self) -> None:
        super().__init__()
        self.reader = XInputReader()
        self._samples: deque = deque()
        self._last_packet: int | None = None
        self._peak = 0.0

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 20, 24, 20)
        layout.setSpacing(14)

        heading = QHBoxLayout()
        title = QLabel("Input tester")
        title.setObjectName("Title")
        self.state = QLabel("waiting for a controller")
        self.state.setObjectName("Faint")
        stack = QVBoxLayout()
        stack.setSpacing(1)
        stack.addWidget(title)
        stack.addWidget(self.state)
        heading.addLayout(stack)
        heading.addStretch(1)
        self.reset_button = QPushButton("Reset")
        self.reset_button.setObjectName("Ghost")
        self.reset_button.clicked.connect(self.reset)
        heading.addWidget(self.reset_button)
        layout.addLayout(heading)
        layout.addWidget(divider())

        # -- polling rate -------------------------------------------------
        layout.addWidget(section_label("report rate", (
            "How often the controller sends a new state.\n\n"
            "XInput increments a packet counter on every fresh report, and this "
            "counts how many arrive per second.\n\n"
            "At rest the controller has nothing new to say, so the rate reads "
            "zero. Move a stick continuously to measure it properly.\n\n"
            "The figure is what actually reaches Windows, so it reflects the "
            "whole path including the link you are on. Comparing wired, 2.4 GHz "
            "and Bluetooth here is meaningful.")))

        rate_row = QHBoxLayout()
        self.rate_label = QLabel("—")
        self.rate_label.setStyleSheet(
            f"font-size:30px; font-weight:700; color:{theme.TEXT};")
        unit = QLabel("reports / second")
        unit.setObjectName("Faint")
        self.peak_label = QLabel("")
        self.peak_label.setObjectName("Muted")
        rate_row.addWidget(self.rate_label)
        rate_row.addWidget(unit)
        rate_row.addStretch(1)
        rate_row.addWidget(self.peak_label)
        layout.addLayout(rate_row)

        self.hint = QLabel("move a stick in circles to measure")
        self.hint.setObjectName("Faint")
        layout.addWidget(self.hint)

        # -- buttons ------------------------------------------------------
        layout.addWidget(section_label("buttons", (
            "Every button lights while held.\n\n"
            "Start, Select and Home are shown because the controller reports "
            "them, but they cannot be placed in a macro: the pad's own macro "
            "key list leaves them out.")))
        grid = QGridLayout()
        grid.setSpacing(6)
        self.lamps: dict[p.Key, Lamp] = {}
        for row, entries in enumerate(LAMP_ROWS):
            for column, (caption, key) in enumerate(entries):
                lamp = Lamp(caption)
                self.lamps[key] = lamp
                grid.addWidget(lamp, row, column)
        layout.addLayout(grid)

        # -- sticks and triggers ------------------------------------------
        layout.addWidget(section_label("sticks and triggers", (
            "The trail shows where the stick has been.\n\n"
            "Rolling the stick around its edge should trace a clean circle. A "
            "flat side or a corner means the stick is not reaching its full "
            "range in that direction.\n\n"
            "Released to centre, the dot should return to the middle and stay "
            "there. Drift shows up as a dot that settles off centre.")))

        bottom = QHBoxLayout()
        bottom.setSpacing(18)
        self.left_stick = StickPad("left")
        self.right_stick = StickPad("right")
        bottom.addWidget(self.left_stick)
        bottom.addWidget(self.right_stick)

        bars = QVBoxLayout()
        bars.setSpacing(8)
        self.left_trigger = TriggerBar("LT")
        self.right_trigger = TriggerBar("RT")
        bars.addStretch(1)
        bars.addWidget(self.left_trigger)
        bars.addWidget(self.right_trigger)
        bars.addStretch(1)
        bottom.addLayout(bars, 1)
        layout.addLayout(bottom)
        layout.addStretch(1)

        self.timer = QTimer(self)
        self.timer.setInterval(POLL_MS)
        self.timer.timeout.connect(self._tick)

    # -- lifecycle -------------------------------------------------------

    def start(self) -> None:
        if not self.reader.available:
            self.state.setText("XInput is unavailable on this system")
            return
        self.timer.start()

    def stop(self) -> None:
        self.timer.stop()

    def reset(self) -> None:
        self._samples.clear()
        self._peak = 0.0
        self.peak_label.setText("")
        self.left_stick.clear_trail()
        self.right_stick.clear_trail()

    # -- sampling --------------------------------------------------------

    def _tick(self) -> None:
        state = self.reader.poll()
        if state is None:
            self.state.setText("no controller on XInput")
            self.rate_label.setText("—")
            return

        self.state.setText(f"XInput slot {state.slot}")

        for key, lamp in self.lamps.items():
            lamp.set_lit(key in state.buttons)
        self.left_stick.set_position(*state.left_stick)
        self.right_stick.set_position(*state.right_stick)
        self.left_trigger.set_value(state.left_trigger)
        self.right_trigger.set_value(state.right_trigger)

        now = time.perf_counter()
        if self._last_packet is not None and state.packet != self._last_packet:
            self._samples.append(now)
        self._last_packet = state.packet

        cutoff = now - WINDOW_SECONDS
        while self._samples and self._samples[0] < cutoff:
            self._samples.popleft()

        rate = len(self._samples) / WINDOW_SECONDS
        if rate <= 0:
            self.rate_label.setText("0")
            self.hint.setText("idle · move a stick in circles to measure")
            return

        self.rate_label.setText(f"{rate:.0f}")
        self.hint.setText("measuring")
        if rate > self._peak:
            self._peak = rate
            self.peak_label.setText(f"peak {self._peak:.0f} Hz")
