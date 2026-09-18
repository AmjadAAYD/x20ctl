"""The intro: the mark arrives, holds, then leaves to the left at speed.

An overlay child of the main window rather than a separate splash window, so
there's no second taskbar entry and nothing to get lost behind other windows.
It covers the interface while it runs and removes itself afterwards.

Deliberately short. The whole thing is under a second, and a click, a key, or
`X20CTL_NO_SPLASH=1` skips it, because an intro you can't get past stops being
a flourish the second time you see it.
"""

from __future__ import annotations

import os

from PySide6.QtCore import (
    QEasingCurve, QPauseAnimation, QPoint, QPropertyAnimation,
    QSequentialAnimationGroup, Qt, Signal,
)
from PySide6.QtGui import QColor, QPainter, QPixmap
from PySide6.QtWidgets import QGraphicsOpacityEffect, QLabel, QWidget

from . import theme

ARRIVE_MS = 260
HOLD_MS = 420
DEPART_MS = 260         # short on purpose: this is the "top speed" part

LOGO = 72
GAP = 18


class SplashOverlay(QWidget):
    """Covers its parent, plays the intro, then gets out of the way."""

    finished = Signal()

    def __init__(self, parent: QWidget, logo: str | None = None):
        super().__init__(parent)
        self.setAttribute(Qt.WA_StyledBackground, False)
        self.setAttribute(Qt.WA_TransparentForMouseEvents, False)
        self.setFocusPolicy(Qt.StrongFocus)
        self._done = False
        self._retired = False
        self._group: QSequentialAnimationGroup | None = None

        # One child holding mark and wordmark, so a single position animation
        # moves them together and they can't drift apart mid-flight.
        self.content = QWidget(self)
        self.content.setAttribute(Qt.WA_TransparentForMouseEvents)

        self.mark = QLabel(self.content)
        pixmap = QPixmap(logo) if logo else QPixmap()
        if not pixmap.isNull():
            self.mark.setPixmap(pixmap.scaled(
                LOGO, LOGO, Qt.KeepAspectRatio, Qt.SmoothTransformation))
        self.mark.setFixedSize(LOGO, LOGO)
        self.mark.move(0, 0)

        self.word = QLabel("x20ctl", self.content)
        # Sized through the stylesheet, not setFont. The application sets
        # `QWidget { font-size: 13px }` globally, and a stylesheet rule beats a
        # font set on the widget, so setFont here does nothing at all and the
        # wordmark comes out at body size. Family is left to inherit.
        self.word.setStyleSheet(
            "color: %s;"
            " background: transparent;"
            " font-size: 38px;"
            " font-weight: 700;" % theme.TEXT)
        self.word.adjustSize()
        self.word.move(LOGO + GAP, (LOGO - self.word.height()) // 2)

        self.content.resize(LOGO + GAP + self.word.width(), LOGO)
        self.resize(parent.size())
        self._centre()
        # A child doesn't follow its parent's size on its own, and the window
        # can be resized or maximised while this is playing.
        parent.installEventFilter(self)

    def eventFilter(self, watched, event) -> bool:
        if watched is self.parent() and event.type() == event.Type.Resize:
            self.resize(watched.size())
        return False

    # -- painting --------------------------------------------------------

    def paintEvent(self, event) -> None:
        """Opaque, so the interface underneath doesn't show through."""
        painter = QPainter(self)
        painter.fillRect(self.rect(), QColor(theme.BG))

    def resizeEvent(self, event) -> None:
        self._centre()

    def _centre(self) -> None:
        if self._group is not None:      # mid-flight, leave it where it is
            return
        self.content.move((self.width() - self.content.width()) // 2,
                          (self.height() - self.content.height()) // 2)

    # -- the sequence ----------------------------------------------------

    def start(self) -> None:
        self.show()
        self.raise_()
        self.setFocus()

        home = self.content.pos()
        # Off the left edge by its own width, so it's fully gone.
        away = QPoint(-self.content.width() - 40, home.y())

        arrive = QPropertyAnimation(self.content, b"pos", self)
        arrive.setDuration(ARRIVE_MS)
        arrive.setStartValue(QPoint(home.x() + 90, home.y()))
        arrive.setEndValue(home)
        # Overshoot slightly and settle: it reads as landing rather than stopping.
        arrive.setEasingCurve(QEasingCurve.OutBack)

        depart = QPropertyAnimation(self.content, b"pos", self)
        depart.setDuration(DEPART_MS)
        depart.setStartValue(home)
        depart.setEndValue(away)
        # Winds back a touch, then leaves hard. This is the woosh.
        depart.setEasingCurve(QEasingCurve.InBack)

        self._group = QSequentialAnimationGroup(self)
        self._group.addAnimation(arrive)
        self._group.addAnimation(QPauseAnimation(HOLD_MS, self))
        self._group.addAnimation(depart)
        self._group.finished.connect(self._reveal)
        self._group.start()

        self._fade_in_mark()

    def _fade_in_mark(self) -> None:
        """The mark fades up as it flies in, so it doesn't just appear."""
        effect = QGraphicsOpacityEffect(self.content)
        self.content.setGraphicsEffect(effect)
        fade = QPropertyAnimation(effect, b"opacity", self)
        fade.setDuration(ARRIVE_MS)
        fade.setStartValue(0.0)
        fade.setEndValue(1.0)
        fade.setEasingCurve(QEasingCurve.OutCubic)
        fade.finished.connect(lambda: self.content.setGraphicsEffect(None))
        fade.start(QPropertyAnimation.DeleteWhenStopped)

    def _reveal(self) -> None:
        """Uncover the interface at the natural end, fading rather than cutting."""
        if self._done:
            return
        self._done = True

        effect = QGraphicsOpacityEffect(self)
        self.setGraphicsEffect(effect)
        fade = QPropertyAnimation(effect, b"opacity", self)
        fade.setDuration(150)
        fade.setStartValue(1.0)
        fade.setEndValue(0.0)
        fade.setEasingCurve(QEasingCurve.OutCubic)
        fade.finished.connect(self._retire)
        fade.start(QPropertyAnimation.DeleteWhenStopped)

    def _retire(self) -> None:
        # Guarded because a click, a keypress and the timer running out can all
        # arrive at nearly the same moment, and each one wants to finish this.
        if self._retired:
            return
        self._retired = True
        self._done = True
        self.hide()
        self.finished.emit()
        self.deleteLater()

    # -- skipping --------------------------------------------------------

    def skip(self) -> None:
        """Leave immediately. No fade: someone asking to skip wants it gone."""
        if self._group is not None:
            self._group.stop()
        self._retire()

    def mousePressEvent(self, event) -> None:
        self.skip()

    def keyPressEvent(self, event) -> None:
        self.skip()


def play(window: QWidget, logo: str | None = None) -> SplashOverlay | None:
    """Run the intro over `window`, unless it's been turned off.

    Returns None when skipped, so the caller can tell the difference between
    "played" and "declined" without inspecting the environment itself.
    """
    if os.environ.get("X20CTL_NO_SPLASH"):
        return None
    overlay = SplashOverlay(window, logo)
    overlay.start()
    return overlay
