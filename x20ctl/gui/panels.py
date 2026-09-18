"""The settings pages themselves.

Each page owns its widgets and says what it wants through a signal. None of
them talk to a controller directly, which keeps them testable with no radio
and means one place decides what a failed write looks like.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QCheckBox, QHBoxLayout, QLabel, QPushButton, QSlider, QVBoxLayout, QWidget,
)

from .. import protocol as p
from .autosave import Debounced
from .rumble import Rumbler


class Page(QWidget):
    """Title, one sentence of explanation, then the controls."""

    def __init__(self, title: str, blurb: str) -> None:
        super().__init__()
        self.root = QVBoxLayout(self)
        self.root.setContentsMargins(0, 0, 0, 0)
        self.root.setSpacing(6)

        heading = QLabel(title)
        heading.setObjectName("PageTitle")
        self.root.addWidget(heading)

        self.blurb = QLabel(blurb)
        self.blurb.setObjectName("PageSubtitle")
        self.blurb.setWordWrap(True)
        self.root.addWidget(self.blurb)
        self.root.addSpacing(18)

        self.status = QLabel()
        self.status.setObjectName("PageSubtitle")

    def say(self, message: str) -> None:
        self.status.setText(message)


class VibrationPage(Page):
    """How hard the motors may work, and what that feels like.

    The only page with no Apply button. Vibration is a ceiling that games
    scale to rather than something you compose, so there is nothing to get
    half-written, and it is tuned by feel rather than by number.
    """

    save_requested = Signal(int)

    def __init__(self, rumbler: Rumbler | None = None) -> None:
        super().__init__(
            "Vibration",
            "How hard the motors are allowed to work. Games scale their rumble "
            "to this. Let go of the slider and the controller shows you what "
            "the setting feels like; it saves itself a moment later.")

        self.rumbler = rumbler if rumbler is not None else Rumbler()
        self.saver = Debounced()
        self.saver.ready.connect(self._save)

        row = QHBoxLayout()
        row.setSpacing(14)
        self.slider = QSlider(Qt.Horizontal)
        self.slider.setRange(0, 100)
        self.slider.setSingleStep(5)
        self.slider.setPageStep(10)
        self.slider.valueChanged.connect(self._on_change)
        self.slider.sliderReleased.connect(self.preview)
        row.addWidget(self.slider, 1)

        self.readout = QLabel("0%")
        self.readout.setObjectName("RowTitle")
        self.readout.setFixedWidth(56)
        self.readout.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        row.addWidget(self.readout)
        self.root.addLayout(row)

        self.root.addSpacing(10)
        self.root.addWidget(self.status)
        self.root.addStretch(1)

    def load(self, percent: int) -> None:
        """Show what the pad currently holds, without saving it back."""
        self.saver.cancel()
        self.slider.blockSignals(True)
        self.slider.setValue(percent)
        self.slider.blockSignals(False)
        self.readout.setText(f"{percent}%")
        self.say("")

    def value(self) -> int:
        return self.slider.value()

    def preview(self) -> None:
        """Buzz at the current setting, if the pad is reachable that way."""
        percent = self.slider.value()
        if self.rumbler.pulse(percent):
            return
        self.say("Connect the controller to this PC to feel the preview.")

    def _on_change(self, percent: int) -> None:
        self.readout.setText(f"{percent}%")
        self.say("Saving...")
        self.saver.push(percent)

    def _save(self, percent: int) -> None:
        self.save_requested.emit(percent)

    def saved(self, percent: int) -> None:
        self.say(f"Saved at {percent}%.")

    def flush(self) -> None:
        """Write a pending change now, for leaving the page or closing up."""
        self.saver.flush()


class PowerPage(Page):
    """When the controller switches itself off."""

    save_requested = Signal(object)          # minutes, or None for never

    def __init__(self) -> None:
        super().__init__(
            "Power",
            "How long the controller waits, with nothing pressed, before it "
            "switches itself off. Set it to never and it stays awake until you "
            "turn it off yourself.")

        row = QHBoxLayout()
        row.setSpacing(14)
        self.slider = QSlider(Qt.Horizontal)
        self.slider.setRange(1, p.MAX_SHUTDOWN_MINUTES)
        self.slider.setSingleStep(1)
        self.slider.valueChanged.connect(self._on_change)
        row.addWidget(self.slider, 1)

        self.readout = QLabel()
        self.readout.setObjectName("RowTitle")
        self.readout.setFixedWidth(96)
        self.readout.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        row.addWidget(self.readout)
        self.root.addLayout(row)

        self.never = QCheckBox("Never switch off")
        self.never.toggled.connect(self._on_never)
        self.root.addWidget(self.never)

        never_note = QLabel(
            "The controller stays awake indefinitely, which costs battery.")
        never_note.setObjectName("RowDetail")
        never_note.setWordWrap(True)
        self.root.addWidget(never_note)

        self.root.addSpacing(10)
        self.save_button = QPushButton("Save to controller")
        self.save_button.setCursor(Qt.PointingHandCursor)
        self.save_button.clicked.connect(self.apply)
        self.root.addWidget(self.save_button, 0, Qt.AlignLeft)

        self.root.addSpacing(8)
        self.root.addWidget(self.status)
        self.root.addStretch(1)

        # Do NOT assert a value before the pad has been read. This used to call
        # load(10), so a controller set to 30 minutes showed 10 and looked like
        # the app had read it. Start unread and say so.
        self.loaded = False
        self.show_unread()

    def show_unread(self) -> None:
        """State before the pad answers: no claim about the current setting."""
        self.loaded = False
        self.slider.blockSignals(True)
        self.slider.setValue(10)
        self.slider.blockSignals(False)
        self.readout.setText("reading...")
        self.status.setText("Reading the current setting from the controller.")

    def load(self, minutes: int | None) -> None:
        self.loaded = True
        self.status.setText("")
        self.never.blockSignals(True)
        self.slider.blockSignals(True)
        self.never.setChecked(minutes is None)
        if minutes is not None:
            self.slider.setValue(minutes)
        self.slider.setEnabled(minutes is not None)
        self.never.blockSignals(False)
        self.slider.blockSignals(False)
        self._show(minutes)
        self.say("")

    def value(self) -> int | None:
        return None if self.never.isChecked() else self.slider.value()

    def apply(self) -> None:
        self.save_requested.emit(self.value())

    def _show(self, minutes: int | None) -> None:
        if minutes is None:
            self.readout.setText("never")
        else:
            self.readout.setText(
                f"{minutes} minute" + ("" if minutes == 1 else "s"))

    def _on_change(self, minutes: int) -> None:
        self._show(minutes)

    def _on_never(self, never: bool) -> None:
        self.slider.setEnabled(not never)
        self._show(self.value())
