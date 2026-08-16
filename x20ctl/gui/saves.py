"""Save files: a whole controller, macros and all.

One file is one controller's whole setup: the four macro slots, the vibration
ceiling, the stick and trigger curves. Not one file per macro slot, because
nobody thinks in slots. They think "my Rocket League setup", and that is four
macros plus the deadzones that go with them.

Files live under the controller's own directory, so two pads never share a
list.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QHBoxLayout, QInputDialog, QLabel, QListWidget, QPushButton, QVBoxLayout,
    QWidget,
)


class SavesBar(QWidget):
    """The row of save-file controls that sits above the macro grid."""

    save_requested = Signal(str)        # name to save under
    load_requested = Signal(str)
    delete_requested = Signal(str)

    def __init__(self) -> None:
        super().__init__()
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(6)

        caption = QLabel("Save files")
        caption.setObjectName("RailHeading")
        layout.addWidget(caption)

        hint = QLabel(
            "A save file holds everything about this controller: all four "
            "macros, the vibration ceiling, and the stick and trigger "
            "settings. Saving writes what is on screen; loading sends it "
            "back to the controller.")
        hint.setObjectName("RowDetail")
        hint.setWordWrap(True)
        layout.addWidget(hint)

        self.list = QListWidget()
        self.list.setFixedHeight(96)
        self.list.itemSelectionChanged.connect(self._sync)
        layout.addWidget(self.list)

        row = QHBoxLayout()
        row.setSpacing(8)
        self.save_button = QPushButton("Save as...")
        self.save_button.setCursor(Qt.PointingHandCursor)
        self.save_button.clicked.connect(self._save_as)
        row.addWidget(self.save_button)

        self.load_button = QPushButton("Load")
        self.load_button.setObjectName("Ghost")
        self.load_button.setCursor(Qt.PointingHandCursor)
        self.load_button.clicked.connect(self._load)
        row.addWidget(self.load_button)

        self.delete_button = QPushButton("Delete")
        self.delete_button.setObjectName("Ghost")
        self.delete_button.setCursor(Qt.PointingHandCursor)
        self.delete_button.clicked.connect(self._delete)
        row.addWidget(self.delete_button)
        row.addStretch(1)
        layout.addLayout(row)

        self._sync()

    def show_saves(self, names) -> None:
        chosen = self.selected()
        self.list.clear()
        for name in names:
            self.list.addItem(name)
        if chosen in names:
            self.select(chosen)
        self._sync()

    def select(self, name: str) -> None:
        for row in range(self.list.count()):
            if self.list.item(row).text() == name:
                self.list.setCurrentRow(row)
                return

    def selected(self) -> str | None:
        item = self.list.currentItem()
        return item.text() if item is not None else None

    def _sync(self) -> None:
        has = self.selected() is not None
        self.load_button.setEnabled(has)
        self.delete_button.setEnabled(has)

    def _save_as(self) -> None:
        suggested = self.selected() or "Save file 1"
        name, ok = QInputDialog.getText(self, "Save as", "Name this setup:",
                                        text=suggested)
        if ok and name.strip():
            self.save_requested.emit(name.strip())

    def _load(self) -> None:
        name = self.selected()
        if name:
            self.load_requested.emit(name)

    def _delete(self) -> None:
        name = self.selected()
        if name:
            self.delete_requested.emit(name)
