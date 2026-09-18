"""Save files: a whole controller in one file, on their own page.

One file is one controller's whole setup: the four macro slots, the vibration
ceiling, the stick and trigger settings. Not a file per macro slot, because
nobody thinks in slots. They think about a setup for one game, and that is four
macros plus the deadzones that go with them.

The list lives on its own page rather than above the macro grid, where it
crowded the thing it was meant to serve. Naming a save happens where the work
is, on the macro page; managing saves happens here.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QHBoxLayout, QLabel, QLineEdit, QListWidget, QPushButton, QVBoxLayout,
    QWidget,
)


class SaveAsRow(QWidget):
    """Name the current setup and save it, from the macro page."""

    save_requested = Signal(str)

    def __init__(self) -> None:
        super().__init__()
        row = QHBoxLayout(self)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(8)

        caption = QLabel("Save as")
        caption.setObjectName("RowDetail")
        row.addWidget(caption)

        self.name = QLineEdit()
        self.name.setPlaceholderText("name this setup")
        self.name.setFixedWidth(220)
        self.name.returnPressed.connect(self._save)
        row.addWidget(self.name)

        self.button = QPushButton("Save")
        self.button.setObjectName("Ghost")
        self.button.setCursor(Qt.PointingHandCursor)
        self.button.setToolTip(
            "Saves all four macros, the vibration ceiling and the stick and "
            "trigger settings under this name")
        self.button.clicked.connect(self._save)
        row.addWidget(self.button)
        row.addStretch(1)

    def _save(self) -> None:
        name = self.name.text().strip()
        if name:
            self.save_requested.emit(name)

    def clear(self) -> None:
        self.name.clear()


class SavedMacrosPage(QWidget):
    """Every save file for this controller, and what to do with one."""

    show_requested = Signal(str)        # open it in the macro editor
    load_requested = Signal(str)        # send it to the controller
    delete_requested = Signal(str)

    def __init__(self) -> None:
        super().__init__()
        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(6)

        title = QLabel("Saved macros")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        blurb = QLabel(
            "Every setup saved for this controller. Each one holds all four "
            "macros along with the vibration ceiling and the stick and trigger "
            "settings.\n\n"
            "Show on Macros opens it in the editor so you can change it and "
            "save it again. Load sends it to the controller as it is.")
        blurb.setObjectName("PageSubtitle")
        blurb.setWordWrap(True)
        root.addWidget(blurb)
        root.addSpacing(14)

        self.list = QListWidget()
        self.list.itemSelectionChanged.connect(self._sync)
        self.list.itemDoubleClicked.connect(lambda _: self._show())
        root.addWidget(self.list, 1)

        row = QHBoxLayout()
        row.setSpacing(8)
        self.show_button = QPushButton("Show on Macros")
        self.show_button.setCursor(Qt.PointingHandCursor)
        self.show_button.clicked.connect(self._show)
        row.addWidget(self.show_button)

        self.load_button = QPushButton("Load")
        self.load_button.setObjectName("Ghost")
        self.load_button.setCursor(Qt.PointingHandCursor)
        self.load_button.setToolTip("Send this setup to the controller now")
        self.load_button.clicked.connect(self._load)
        row.addWidget(self.load_button)

        self.delete_button = QPushButton("Delete")
        self.delete_button.setObjectName("Danger")
        self.delete_button.setCursor(Qt.PointingHandCursor)
        self.delete_button.clicked.connect(self._delete)
        row.addWidget(self.delete_button)
        row.addStretch(1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        row.addWidget(self.status)
        root.addLayout(row)

        self.empty = QLabel(
            "Nothing saved yet. Build a macro, then use Save as on the "
            "Macros page.")
        self.empty.setObjectName("RowDetail")
        root.addWidget(self.empty)

        self._sync()

    def show_saves(self, names) -> None:
        chosen = self.selected()
        self.list.clear()
        for name in names:
            self.list.addItem(name)
        if chosen in names:
            self.select(chosen)
        self.empty.setVisible(not names)
        self._sync()

    def select(self, name: str) -> None:
        for row in range(self.list.count()):
            if self.list.item(row).text() == name:
                self.list.setCurrentRow(row)
                return

    def selected(self) -> str | None:
        item = self.list.currentItem()
        return item.text() if item is not None else None

    def say(self, message: str) -> None:
        self.status.setText(message)

    def _sync(self) -> None:
        has = self.selected() is not None
        for button in (self.show_button, self.load_button, self.delete_button):
            button.setEnabled(has)

    def _show(self) -> None:
        name = self.selected()
        if name:
            self.show_requested.emit(name)

    def _load(self) -> None:
        name = self.selected()
        if name:
            self.load_requested.emit(name)

    def _delete(self) -> None:
        name = self.selected()
        if name:
            self.delete_requested.emit(name)
