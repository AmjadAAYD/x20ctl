"""Reusable pieces of the interface."""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QColor, QPainter
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QLabel, QLineEdit, QPushButton, QSizePolicy,
    QSlider, QSpinBox, QVBoxLayout, QWidget,
)

from .. import protocol as p
from . import theme


class StatusDot(QWidget):
    """A small coloured dot showing connection state."""

    def __init__(self) -> None:
        super().__init__()
        self.setFixedSize(9, 9)
        self._colour = theme.TEXT_FAINT

    def set_state(self, state: str) -> None:
        self._colour = {
            "connected": theme.SUCCESS,
            "connecting": theme.WARNING,
            "error": theme.DANGER,
        }.get(state, theme.TEXT_FAINT)
        self.update()

    def paintEvent(self, _event) -> None:
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.setBrush(QColor(self._colour))
        painter.setPen(Qt.NoPen)
        painter.drawEllipse(0, 0, 9, 9)


def section_label(text: str) -> QLabel:
    label = QLabel(text.upper())
    label.setObjectName("SectionTitle")
    return label


def divider() -> QFrame:
    line = QFrame()
    line.setObjectName("Divider")
    line.setFixedHeight(1)
    return line


class VibrationRow(QWidget):
    """Slider plus live percentage."""

    changed = Signal(int)

    def __init__(self) -> None:
        super().__init__()
        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(12)

        self.slider = QSlider(Qt.Horizontal)
        self.slider.setRange(0, 100)
        self.slider.setSingleStep(5)
        self.slider.setPageStep(10)

        self.readout = QLabel("0%")
        self.readout.setFixedWidth(42)
        self.readout.setAlignment(Qt.AlignRight | Qt.AlignVCenter)

        self.slider.valueChanged.connect(self._on_change)
        layout.addWidget(self.slider, 1)
        layout.addWidget(self.readout)

    def _on_change(self, value: int) -> None:
        self.readout.setText(f"{value}%")
        self.changed.emit(value)

    def value(self) -> int:
        return self.slider.value()

    def set_value(self, value: int) -> None:
        self.slider.blockSignals(True)
        self.slider.setValue(value)
        self.readout.setText(f"{value}%")
        self.slider.blockSignals(False)


class MacroCard(QFrame):
    """One M-slot: the key sequence and its timing."""

    changed = Signal()
    record_requested = Signal(str)

    def __init__(self, slot: str) -> None:
        super().__init__()
        self.slot = slot
        self.recording = False
        self.setObjectName("Card")

        outer = QVBoxLayout(self)
        outer.setContentsMargins(14, 10, 14, 10)
        outer.setSpacing(6)

        row = QHBoxLayout()
        row.setSpacing(8)

        name = QLabel(slot)
        name.setFixedWidth(26)
        name.setStyleSheet(
            f"font-size:14px; font-weight:700; color:{theme.ACCENT};")
        row.addWidget(name)

        self.keys = QLineEdit()
        self.keys.setPlaceholderText("A+B    A,B    LS_UP+A")
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
        self.record_button.setFixedWidth(76)
        self.record_button.setToolTip(
            "Press buttons on the controller and they are captured here,\n"
            "with the real timing between them.")
        self.record_button.clicked.connect(
            lambda: self.record_requested.emit(self.slot))
        row.addWidget(self.record_button)

        self.clear_button = QPushButton("Clear")
        self.clear_button.setObjectName("Ghost")
        self.clear_button.setFixedWidth(58)
        self.clear_button.clicked.connect(self.clear)
        row.addWidget(self.clear_button)
        outer.addLayout(row)

        # summary and errors share the second line, indented under the field
        self.summary = QLabel("empty")
        self.summary.setObjectName("Faint")
        self.summary.setContentsMargins(34, 0, 0, 0)
        self.summary.setStyleSheet("font-size:11px;")
        outer.addWidget(self.summary)

        self.error = QLabel("")
        self.error.setObjectName("Danger")
        self.error.setContentsMargins(34, 0, 0, 0)
        self.error.setStyleSheet("font-size:11px;")
        self.error.setWordWrap(True)
        self.error.hide()
        outer.addWidget(self.error)

    def _spin(self, caption: str, default: int, layout: QHBoxLayout) -> QSpinBox:
        spin = QSpinBox()
        spin.setRange(0, 4000)
        spin.setSingleStep(5)
        spin.setValue(default)
        spin.setFixedWidth(84)
        spin.setSuffix(" ms")
        spin.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        spin.setToolTip(f"{caption} duration in milliseconds")
        spin.valueChanged.connect(self._refresh)
        layout.addWidget(spin)
        return spin

    # -- state ----------------------------------------------------------

    def clear(self) -> None:
        self.keys.clear()
        self._refresh()

    def spec(self):
        """A validated MacroSpec, or None if the slot is empty."""
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
            self.summary.setText("invalid")
            self.summary.setObjectName("Danger")
            self.error.setText(str(exc))
            self.error.show()
            self.setObjectName("Card")
        else:
            self.error.hide()
            if spec is None:
                self.summary.setText("empty")
                self.summary.setObjectName("Faint")
                self.setObjectName("Card")
            else:
                text = spec.describe()
                if spec.loop_ms:
                    text += "  ·  repeats until interrupted"
                self.summary.setText(text)
                self.summary.setObjectName("Warning" if spec.loop_ms else "Muted")
                self.setObjectName("CardActive")
        # re-apply the stylesheet so the objectName change takes effect
        self.summary.style().unpolish(self.summary)
        self.summary.style().polish(self.summary)
        self.style().unpolish(self)
        self.style().polish(self)
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
        self.record_button.setObjectName("Danger" if active else "Ghost")
        self.record_button.style().unpolish(self.record_button)
        self.record_button.style().polish(self.record_button)
        self.keys.setEnabled(not active)
        self.clear_button.setEnabled(not active)
        if active:
            self.error.hide()
            self.summary.setText("recording · press buttons on the controller")
            self.summary.setObjectName("Warning")
        else:
            self._refresh()
        self.summary.style().unpolish(self.summary)
        self.summary.style().polish(self.summary)

    def show_recording_progress(self, text: str) -> None:
        if self.recording:
            self.summary.setText(f"recording · {text}")
