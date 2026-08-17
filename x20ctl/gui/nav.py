"""The workspace sidebar: every section, and how much of it to show.

A left rail rather than a tab strip, because there are more sections than fit
across the top and a vertical list has room for names instead of icons alone.

Simple and Advanced are the same app with a different appetite. Simple shows
what most people came for. Advanced adds the settings that need a sentence of
explanation before they are safe to touch. Nothing is hidden to be coy: every
section says what it is, and Advanced says so on the item.
"""

from __future__ import annotations

from dataclasses import dataclass

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QButtonGroup, QFrame, QLabel, QPushButton, QVBoxLayout, QWidget,
)

SIMPLE = "simple"
ADVANCED = "advanced"


@dataclass(frozen=True)
class Section:
    """One entry in the rail."""

    key: str
    title: str
    blurb: str
    advanced: bool = False


SECTIONS = (
    Section("buttons", "Buttons",
            "Swap what each button does. A can be B, B can be X."),
    Section("sticks", "Sticks",
            "Deadzones and how quickly a stick answers your thumb."),
    Section("triggers", "Triggers",
            "How far a trigger travels before it counts, and how it ramps."),
    Section("motor", "Vibration",
            "How hard the motors are allowed to work. Saves itself."),
    Section("macros", "Macros",
            "Record or draw a sequence onto M1 to M4."),
    Section("saves", "Saved macros",
            "Setups you have saved, ready to open or send back."),
    Section("test", "Test",
            "Watch the sticks and triggers move as you use them."),
    # Not advanced. A user asked "Is Power standard enough that it shouldn't be
    # part of Advanced?" and they are right: a sleep timer needs no explanation
    # before it is safe to touch, which is the only thing Advanced is for.
    Section("timeout", "Power",
            "How long the controller waits before switching itself off."),
    Section("device", "Device",
            "Calibration, factory reset, and what the pad reports about itself.",
            advanced=True),
)


def sections_for(mode: str) -> list[Section]:
    """The sections one mode shows, in rail order."""
    if mode == ADVANCED:
        return list(SECTIONS)
    return [section for section in SECTIONS if not section.advanced]


class NavRail(QFrame):
    """Vertical section list, plus the Simple/Advanced switch."""

    selected = Signal(str)
    mode_changed = Signal(str)

    def __init__(self, mode: str = SIMPLE) -> None:
        super().__init__()
        self.setObjectName("NavRail")
        self.setFixedWidth(212)
        self.mode = mode
        self._buttons: dict[str, QPushButton] = {}

        self.layout = QVBoxLayout(self)
        self.layout.setContentsMargins(12, 16, 12, 14)
        self.layout.setSpacing(4)

        heading = QLabel("Settings")
        heading.setObjectName("RailHeading")
        self.layout.addWidget(heading)

        self.group = QButtonGroup(self)
        self.group.setExclusive(True)
        self.items = QVBoxLayout()
        self.items.setSpacing(4)
        self.layout.addLayout(self.items)
        self.layout.addStretch(1)

        self.mode_button = QPushButton()
        self.mode_button.setObjectName("Ghost")
        self.mode_button.setCursor(Qt.PointingHandCursor)
        self.mode_button.clicked.connect(self.toggle_mode)
        self.layout.addWidget(self.mode_button)

        self.rebuild()

    def rebuild(self) -> None:
        """Redraw the rail for the current mode, keeping the selection if it
        still exists. Switching to Simple while on an Advanced page has to go
        somewhere, and the first section is the safe answer."""
        current = self.current()

        for button in list(self._buttons.values()):
            self.group.removeButton(button)
            button.deleteLater()
        self._buttons.clear()
        while self.items.count():
            self.items.takeAt(0)

        for section in sections_for(self.mode):
            button = QPushButton(section.title)
            button.setObjectName("RailItem")
            button.setCheckable(True)
            button.setCursor(Qt.PointingHandCursor)
            button.setToolTip(section.blurb)
            if section.advanced:
                button.setText(f"{section.title}   ·")
                button.setToolTip(f"{section.blurb}\n\nAdvanced.")
            button.clicked.connect(
                lambda _=False, key=section.key: self.selected.emit(key))
            self.group.addButton(button)
            self.items.addWidget(button)
            self._buttons[section.key] = button

        keys = [section.key for section in sections_for(self.mode)]
        self.select(current if current in keys else keys[0])

        self.mode_button.setText(
            "Simple mode" if self.mode == ADVANCED else "Advanced mode")
        self.mode_button.setToolTip(
            "Hide the settings that need explaining"
            if self.mode == ADVANCED else
            "Show the power timeout and the device page")

    def current(self) -> str | None:
        for key, button in self._buttons.items():
            if button.isChecked():
                return key
        return None

    def select(self, key: str) -> None:
        button = self._buttons.get(key)
        if button is not None:
            button.setChecked(True)
            self.selected.emit(key)

    def set_mode(self, mode: str) -> None:
        if mode == self.mode:
            return
        self.mode = mode
        self.rebuild()
        self.mode_changed.emit(mode)

    def toggle_mode(self) -> None:
        self.set_mode(SIMPLE if self.mode == ADVANCED else ADVANCED)
