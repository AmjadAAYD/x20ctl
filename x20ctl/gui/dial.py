"""A dial for pointing a stick, because the thing being chosen is an angle.

Typing DOWN_LEFT into a dropdown is a poor way to express a direction. Dragging
towards one is better, as long as the control is honest about what it stores: a
macro step holds one of eight compass headings, not a position. So the dial
clicks into eight notches rather than following the mouse smoothly and rounding
behind your back.

The angle convention matches the recorder: measured from up, going clockwise,
so a heading picked here and a heading recorded off the real stick mean the
same thing.
"""

from __future__ import annotations

import math

from PySide6.QtCore import QPointF, Qt, Signal
from PySide6.QtGui import QColor, QPainter, QPen
from PySide6.QtWidgets import QWidget

from . import theme
from .macrogrid import DIRECTIONS

NOTCHES = len(DIRECTIONS)
STEP_DEGREES = 360 / NOTCHES
DEAD_RADIUS = 0.22          # nearer the middle than this means "no direction"


def direction_for(dx: float, dy: float) -> str | None:
    """The heading a pull towards (dx, dy) means, or None if too central.

    Screen coordinates: y grows downward, so up is negative dy. Returns None
    inside the middle, which is how the dial expresses "take the stick out of
    this step".
    """
    if math.hypot(dx, dy) < DEAD_RADIUS:
        return None
    angle = math.degrees(math.atan2(dx, -dy)) % 360
    return DIRECTIONS[round(angle / STEP_DEGREES) % NOTCHES]


def angle_of(direction: str) -> float:
    """Degrees clockwise from up, for drawing."""
    return DIRECTIONS.index(direction) * STEP_DEGREES


class DirectionDial(QWidget):
    """Eight notches. Drag towards one, or into the middle to clear it."""

    chosen = Signal(object)          # a heading, or None for "not in this step"

    def __init__(self, direction: str | None = None) -> None:
        super().__init__()
        self.setFixedSize(148, 148)
        self.setCursor(Qt.PointingHandCursor)
        self.setMouseTracking(True)
        self.direction = direction
        self.hovered: str | None = None

    def _at(self, position) -> str | None:
        """Which notch a point on the widget falls in."""
        radius = min(self.width(), self.height()) / 2
        dx = (position.x() - self.width() / 2) / radius
        dy = (position.y() - self.height() / 2) / radius
        return direction_for(dx, dy)

    def set_direction(self, direction: str | None) -> None:
        self.direction = direction
        self.update()

    def mouseMoveEvent(self, event) -> None:        # noqa: N802 (Qt naming)
        self.hovered = self._at(event.position())
        self.update()

    def leaveEvent(self, event) -> None:            # noqa: N802
        self.hovered = None
        self.update()

    def mousePressEvent(self, event) -> None:       # noqa: N802
        self._pick(event.position())

    def mouseMoveEventWhilePressed(self, event) -> None:
        self._pick(event.position())

    def _pick(self, position) -> None:
        self.direction = self._at(position)
        self.update()
        self.chosen.emit(self.direction)

    def paintEvent(self, event) -> None:            # noqa: N802
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        centre = QPointF(self.width() / 2, self.height() / 2)
        radius = min(self.width(), self.height()) / 2 - 12

        painter.setPen(QPen(QColor(theme.LINE), 1))
        painter.setBrush(QColor(theme.SURFACE))
        painter.drawEllipse(centre, radius, radius)

        for direction in DIRECTIONS:
            radians = math.radians(angle_of(direction))
            point = QPointF(centre.x() + math.sin(radians) * radius * 0.78,
                            centre.y() - math.cos(radians) * radius * 0.78)
            if direction == self.direction:
                colour, size = QColor(theme.EMBER), 7.0
            elif direction == self.hovered:
                colour, size = QColor(theme.EMBER_DEEP), 6.0
            else:
                colour, size = QColor(theme.LINE_HI), 4.0
            painter.setPen(Qt.NoPen)
            painter.setBrush(colour)
            painter.drawEllipse(point, size, size)

        if self.direction is not None:
            radians = math.radians(angle_of(self.direction))
            tip = QPointF(centre.x() + math.sin(radians) * radius * 0.62,
                          centre.y() - math.cos(radians) * radius * 0.62)
            painter.setPen(QPen(QColor(theme.EMBER), 3))
            painter.drawLine(centre, tip)

        painter.setPen(Qt.NoPen)
        painter.setBrush(QColor(theme.LINE_HI if self.direction
                                else theme.TEXT_FAINT))
        painter.drawEllipse(centre, 5, 5)
