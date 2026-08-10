"""Motion.

Animation earns its place when it explains something: where a page came from,
that a value changed, that the app is waiting on hardware. Decoration that moves
for its own sake is noise, so there is not much here and each piece has a reason.

Durations are short. Anything past about 250ms starts to feel like the interface
is arguing with you.
"""

from __future__ import annotations

from PySide6.QtCore import (
    QEasingCurve, QObject, QParallelAnimationGroup, QPoint, QPropertyAnimation,
    Property, Signal,
)
from PySide6.QtWidgets import QGraphicsOpacityEffect

FAST = 140
NORMAL = 200
SLOW = 320

# Decelerating: quick to start, settles gently. Reads as responsive.
EASE_OUT = QEasingCurve.OutCubic
EASE_IN_OUT = QEasingCurve.InOutCubic


def fade_in(widget, duration: int = NORMAL, start: float = 0.0):
    """Fade a widget up from transparent. Returns the animation."""
    effect = QGraphicsOpacityEffect(widget)
    widget.setGraphicsEffect(effect)
    animation = QPropertyAnimation(effect, b"opacity", widget)
    animation.setDuration(duration)
    animation.setStartValue(start)
    animation.setEndValue(1.0)
    animation.setEasingCurve(EASE_OUT)
    # Dropping the effect afterwards keeps repaints cheap; an idle
    # QGraphicsOpacityEffect still forces the widget through an offscreen buffer.
    animation.finished.connect(lambda: widget.setGraphicsEffect(None))
    animation.start(QPropertyAnimation.DeleteWhenStopped)
    return animation


def slide_in(widget, distance: int = 14, duration: int = NORMAL):
    """Fade in while rising slightly, so a new page reads as arriving.

    Does nothing if the widget is not on screen. An animation that never gets
    to run would otherwise leave the widget stuck at zero opacity, which is a
    blank page rather than a missing flourish.
    """
    if widget is None or not widget.isVisible():
        return None

    effect = QGraphicsOpacityEffect(widget)
    widget.setGraphicsEffect(effect)

    opacity = QPropertyAnimation(effect, b"opacity", widget)
    opacity.setDuration(duration)
    opacity.setStartValue(0.0)
    opacity.setEndValue(1.0)
    opacity.setEasingCurve(EASE_OUT)

    start = widget.pos()
    position = QPropertyAnimation(widget, b"pos", widget)
    position.setDuration(duration)
    position.setStartValue(QPoint(start.x(), start.y() + distance))
    position.setEndValue(start)
    position.setEasingCurve(EASE_OUT)

    group = QParallelAnimationGroup(widget)
    group.addAnimation(opacity)
    group.addAnimation(position)
    group.finished.connect(lambda: widget.setGraphicsEffect(None))
    group.start(QParallelAnimationGroup.DeleteWhenStopped)
    return group


class Animatable(QObject):
    """A float that can be animated and watched.

    Qt can only animate a Property, and widgets that paint themselves need a
    plain number to interpolate. This wraps one so custom widgets can ease
    between states rather than snapping.
    """

    changed = Signal(float)

    def __init__(self, value: float = 0.0, parent=None):
        super().__init__(parent)
        self._value = value

    def get_value(self) -> float:
        return self._value

    def set_value(self, value: float) -> None:
        if value != self._value:
            self._value = value
            self.changed.emit(value)

    value = Property(float, get_value, set_value, notify=changed)


def ease_to(animatable: Animatable, target: float, duration: int = FAST,
            curve=EASE_OUT) -> QPropertyAnimation:
    """Animate an Animatable towards a target, replacing any run in progress."""
    existing = getattr(animatable, "_animation", None)
    if existing is not None:
        existing.stop()
    animation = QPropertyAnimation(animatable, b"value", animatable)
    animation.setDuration(duration)
    animation.setStartValue(animatable.get_value())
    animation.setEndValue(target)
    animation.setEasingCurve(curve)
    animatable._animation = animation
    animation.start(QPropertyAnimation.DeleteWhenStopped)
    return animation
