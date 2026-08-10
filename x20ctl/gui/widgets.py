"""Reusable pieces of the interface.

Cards and indicators paint themselves rather than relying on stylesheets,
because Qt stylesheets cannot animate. Anything that changes state smoothly is
drawn by hand against an eased value.
"""

from __future__ import annotations

import math

from PySide6.QtCore import QPoint, QRectF, Qt, QTimer, Signal
from PySide6.QtGui import QColor, QFont, QPainter, QPainterPath, QPen
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QLabel, QLineEdit, QPushButton, QSlider, QSpinBox,
    QToolButton, QToolTip, QVBoxLayout, QWidget,
)

from .. import protocol as p
from . import theme
from .motion import Animatable, ease_to


def _mix(a: str, b: str, t: float) -> QColor:
    """Blend two hex colours. Used for eased state changes."""
    ca, cb = QColor(a), QColor(b)
    return QColor(
        round(ca.red() + (cb.red() - ca.red()) * t),
        round(ca.green() + (cb.green() - ca.green()) * t),
        round(ca.blue() + (cb.blue() - ca.blue()) * t),
    )


class StatusDot(QWidget):
    """Connection state, with a breathing ring while it is working.

    The pulse exists so a slow Bluetooth connection does not look like a frozen
    app. It stops the moment the state resolves.
    """

    def __init__(self) -> None:
        super().__init__()
        self.setFixedSize(22, 22)
        self._state = "idle"
        self._phase = 0.0
        self._timer = QTimer(self)
        self._timer.setInterval(33)
        self._timer.timeout.connect(self._advance)

    def set_state(self, state: str) -> None:
        self._state = state
        if state == "connecting":
            self._timer.start()
        else:
            self._timer.stop()
            self._phase = 0.0
        self.update()

    def _advance(self) -> None:
        self._phase = (self._phase + 0.05) % 1.0
        self.update()

    def _colour(self) -> str:
        return {
            "connected": theme.SAGE,
            "connecting": theme.GOLD,
            "error": theme.ROSE,
        }.get(self._state, theme.TEXT_FAINT)

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        centre = QRectF(self.rect()).center()
        colour = QColor(self._colour())

        if self._state == "connecting":
            # expanding ring that fades as it grows
            spread = 4 + self._phase * 7
            halo = QColor(colour)
            halo.setAlphaF(max(0.0, 0.45 * (1 - self._phase)))
            painter.setPen(Qt.NoPen)
            painter.setBrush(halo)
            painter.drawEllipse(centre, spread, spread)
        elif self._state == "connected":
            halo = QColor(colour)
            halo.setAlphaF(0.18)
            painter.setPen(Qt.NoPen)
            painter.setBrush(halo)
            painter.drawEllipse(centre, 8, 8)

        painter.setBrush(colour)
        painter.setPen(Qt.NoPen)
        painter.drawEllipse(centre, 4, 4)


class Wordmark(QWidget):
    """The app mark: a small drawn glyph beside the name."""

    def __init__(self) -> None:
        super().__init__()
        self.setFixedHeight(30)
        self.setMinimumWidth(150)

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        # a rounded pad silhouette with two sticks, drawn rather than shipped
        body = QRectF(0, 7, 26, 16)
        path = QPainterPath()
        path.addRoundedRect(body, 7, 7)
        painter.setPen(Qt.NoPen)
        painter.setBrush(QColor(theme.EMBER))
        painter.drawPath(path)

        painter.setBrush(QColor(theme.INK))
        painter.drawEllipse(QRectF(5.5, 12.5, 5, 5))
        painter.drawEllipse(QRectF(15.5, 12.5, 5, 5))

        font = QFont(painter.font())
        font.setPointSizeF(11.5)
        font.setWeight(QFont.Bold)
        font.setLetterSpacing(QFont.AbsoluteSpacing, 0.6)
        painter.setFont(font)
        painter.setPen(QColor(theme.TEXT))
        painter.drawText(QRectF(34, 0, self.width() - 34, self.height()),
                         Qt.AlignLeft | Qt.AlignVCenter, "x20ctl")


class InfoButton(QToolButton):
    """A small 'i' that explains a control on click or hover."""

    def __init__(self, explanation: str) -> None:
        super().__init__()
        self.explanation = explanation.strip()
        self.setObjectName("Info")
        self.setText("i")
        self.setFixedSize(16, 16)
        self.setCursor(Qt.WhatsThisCursor)
        self.setToolTip(self.explanation)
        self.clicked.connect(self._show)

    def _show(self) -> None:
        QToolTip.showText(self.mapToGlobal(QPoint(20, 0)), self.explanation, self)


def section_label(text: str, explanation: str | None = None) -> QWidget:
    label = QLabel(text.upper())
    label.setObjectName("Section")
    if explanation is None:
        return label

    holder = QWidget()
    row = QHBoxLayout(holder)
    row.setContentsMargins(0, 0, 0, 0)
    row.setSpacing(7)
    row.addWidget(label)
    row.addWidget(InfoButton(explanation))
    row.addStretch(1)
    return holder


def divider() -> QFrame:
    line = QFrame()
    line.setObjectName("Rule")
    line.setFixedHeight(1)
    return line


class VibrationRow(QWidget):
    """Slider plus a readout that eases between values."""

    changed = Signal(int)

    def __init__(self) -> None:
        super().__init__()
        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(14)

        self.slider = QSlider(Qt.Horizontal)
        self.slider.setRange(0, 100)
        self.slider.setSingleStep(5)
        self.slider.setPageStep(10)

        self.readout = QLabel("0%")
        self.readout.setFixedWidth(50)
        self.readout.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        self.readout.setStyleSheet(
            f"font-size:15px; font-weight:600; color:{theme.TEXT};")

        self.slider.valueChanged.connect(self._on_change)
        layout.addWidget(self.slider, 1)
        layout.addWidget(self.readout)

    def _on_change(self, value: int) -> None:
        self.readout.setText(f"{value}%")
        colour = theme.TEXT_FAINT if value == 0 else theme.TEXT
        self.readout.setStyleSheet(
            f"font-size:15px; font-weight:600; color:{colour};")
        self.changed.emit(value)

    def value(self) -> int:
        return self.slider.value()

    def set_value(self, value: int) -> None:
        self.slider.blockSignals(True)
        self.slider.setValue(value)
        self.slider.blockSignals(False)
        self._on_change(value)


class MacroCard(QFrame):
    """One M-slot.

    Painted by hand so filling a slot, hovering it, or arming it for recording
    can ease rather than snap. A card that is holding a macro sits visibly
    higher than an empty one, which is the fastest way to read the four slots.
    """

    changed = Signal()
    record_requested = Signal(str)

    def __init__(self, slot: str) -> None:
        super().__init__()
        self.slot = slot
        self.recording = False
        self.setAttribute(Qt.WA_Hover, True)
        self.setMinimumHeight(70)

        self._fill = Animatable(0.0, self)     # 0 empty, 1 holding a macro
        self._hover = Animatable(0.0, self)
        self._alarm = Animatable(0.0, self)    # recording or invalid
        for value in (self._fill, self._hover, self._alarm):
            value.changed.connect(lambda _v: self.update())

        outer = QVBoxLayout(self)
        outer.setContentsMargins(16, 9, 16, 9)
        outer.setSpacing(5)

        row = QHBoxLayout()
        row.setSpacing(9)

        self.tag = QLabel(slot)
        self.tag.setFixedWidth(30)
        self.tag.setStyleSheet(
            f"font-size:14px; font-weight:700; color:{theme.TEXT_FAINT};")
        row.addWidget(self.tag)

        self.keys = QLineEdit()
        self.keys.setPlaceholderText("A+B     A,B     A:150/40     LS_UP+A")
        self.keys.setMinimumWidth(150)
        self.keys.setToolTip(
            "'+' presses keys together, ',' plays them one after another.\n\n"
            "Buttons: " + ", ".join(k.name for k in p.MACRO_MASK_BIT) + "\n\n"
            "Sticks: LS_ or RS_ plus a direction, e.g. LS_UP, RS_DOWN_LEFT.\n"
            "Directions: " + ", ".join(d.name for d in p.Direction))
        self.keys.textChanged.connect(self._refresh)
        row.addWidget(self.keys, 1)

        self.hold = self._spin("hold", 100, row)
        self.gap = self._spin("gap", 60, row)
        self.loop = self._spin("loop", 0, row)
        self.loop.setToolTip(
            "0 fires once. Any other value repeats forever with this interval,\n"
            "until you press a different macro button.")

        self.record_button = QPushButton("Record")
        self.record_button.setObjectName("Ghost")
        self.record_button.setFixedWidth(78)
        self.record_button.setToolTip(
            "Press buttons on the controller and they are captured here,\n"
            "with the real timing between them.")
        self.record_button.clicked.connect(
            lambda: self.record_requested.emit(self.slot))
        row.addWidget(self.record_button)

        self.clear_button = QPushButton("Clear")
        self.clear_button.setObjectName("Ghost")
        self.clear_button.setFixedWidth(60)
        self.clear_button.clicked.connect(self.clear)
        row.addWidget(self.clear_button)
        outer.addLayout(row)

        self.summary = QLabel("empty")
        self.summary.setObjectName("Faint")
        self.summary.setContentsMargins(39, 0, 0, 0)
        self.summary.setStyleSheet(f"font-size:11px; color:{theme.TEXT_FAINT};")
        outer.addWidget(self.summary)

    def _spin(self, caption: str, default: int, layout: QHBoxLayout) -> QSpinBox:
        spin = QSpinBox()
        spin.setRange(0, 4000)
        spin.setSingleStep(5)
        spin.setValue(default)
        spin.setFixedWidth(82)
        spin.setSuffix(" ms")
        spin.setAlignment(Qt.AlignCenter)
        spin.setToolTip(f"{caption} duration in milliseconds")
        spin.valueChanged.connect(self._refresh)
        layout.addWidget(spin)
        return spin

    # -- painting --------------------------------------------------------

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        box = QRectF(self.rect()).adjusted(0.5, 0.5, -0.5, -0.5)

        fill = self._fill.get_value()
        hover = self._hover.get_value()
        alarm = self._alarm.get_value()

        surface = _mix(theme.SURFACE, theme.SURFACE_HI, max(fill, hover))
        painter.setPen(Qt.NoPen)
        painter.setBrush(surface)
        painter.drawRoundedRect(box, 10, 10)

        edge = _mix(theme.LINE, theme.EMBER_DEEP, fill)
        if hover:
            edge = _mix(edge.name(), theme.EMBER, hover * 0.6)
        if alarm:
            edge = _mix(edge.name(), theme.ROSE, alarm)
        painter.setPen(QPen(edge, 1))
        painter.setBrush(Qt.NoBrush)
        painter.drawRoundedRect(box, 10, 10)

        # A filled slot gets a marker down its left edge. Reading four slots at
        # a glance is easier from a bar than from text.
        strength = max(fill, alarm)
        if strength > 0.01:
            colour = _mix(theme.EMBER, theme.ROSE, alarm)
            colour.setAlphaF(min(1.0, strength))
            marker = QRectF(box.left() + 1, box.top() + 12,
                            3, max(0.0, box.height() - 24))
            painter.setPen(Qt.NoPen)
            painter.setBrush(colour)
            painter.drawRoundedRect(marker, 1.5, 1.5)

    def enterEvent(self, event) -> None:
        ease_to(self._hover, 1.0)
        super().enterEvent(event)

    def leaveEvent(self, event) -> None:
        ease_to(self._hover, 0.0)
        super().leaveEvent(event)

    # -- state -----------------------------------------------------------

    def clear(self) -> None:
        self.keys.clear()
        self._refresh()

    def spec(self):
        from ..profiles import MacroSpec

        text = self.keys.text().strip()
        if not text:
            return None
        spec = MacroSpec(keys=text, hold_ms=self.hold.value(),
                         gap_ms=self.gap.value(), loop_ms=self.loop.value())
        spec.validate()
        return spec

    def set_spec(self, spec) -> None:
        self.keys.blockSignals(True)
        self.keys.setText(spec.keys if spec else "")
        self.keys.blockSignals(False)
        if spec:
            self.hold.setValue(spec.hold_ms)
            self.gap.setValue(spec.gap_ms)
            self.loop.setValue(spec.loop_ms)
        self._refresh()

    def _refresh(self) -> None:
        try:
            spec = self.spec()
        except ValueError as exc:
            self.summary.setText(str(exc))
            self.summary.setStyleSheet(f"font-size:11px; color:{theme.ROSE};")
            self.tag.setStyleSheet(
                f"font-size:14px; font-weight:700; color:{theme.ROSE};")
            ease_to(self._fill, 0.0)
            ease_to(self._alarm, 1.0)
        else:
            if not self.recording:
                ease_to(self._alarm, 0.0)
            if spec is None:
                self.summary.setText("empty")
                self.summary.setStyleSheet(
                    f"font-size:11px; color:{theme.TEXT_FAINT};")
                self.tag.setStyleSheet(
                    f"font-size:14px; font-weight:700; color:{theme.TEXT_FAINT};")
                ease_to(self._fill, 0.0)
            else:
                text = spec.describe()
                colour = theme.GOLD if spec.loop_ms else theme.TEXT_MUTED
                if spec.loop_ms:
                    text += "  ·  repeats until interrupted"
                self.summary.setText(text)
                self.summary.setStyleSheet(f"font-size:11px; color:{colour};")
                self.tag.setStyleSheet(
                    f"font-size:14px; font-weight:700; color:{theme.EMBER};")
                ease_to(self._fill, 1.0)
        self.changed.emit()

    def is_valid(self) -> bool:
        try:
            self.spec()
        except ValueError:
            return False
        return True

    # -- recording -------------------------------------------------------

    def set_recording(self, active: bool) -> None:
        self.recording = active
        self.record_button.setText("Stop" if active else "Record")
        self.record_button.setObjectName("Recording" if active else "Ghost")
        self.record_button.style().unpolish(self.record_button)
        self.record_button.style().polish(self.record_button)
        self.keys.setEnabled(not active)
        self.clear_button.setEnabled(not active)
        ease_to(self._alarm, 1.0 if active else 0.0)
        if active:
            self.tag.setStyleSheet(
                f"font-size:14px; font-weight:700; color:{theme.ROSE};")
            self.summary.setText("recording, press buttons on the controller")
            self.summary.setStyleSheet(f"font-size:11px; color:{theme.ROSE};")
        else:
            self._refresh()

    def show_recording_progress(self, text: str) -> None:
        if self.recording:
            self.summary.setText(f"recording   {text}")
