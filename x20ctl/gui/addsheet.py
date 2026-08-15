"""Pick a controller and give it a player number.

Discovery hands back everything advertising; this is where a human decides
which one is theirs and which player it stands in for. The scan itself is
injectable so the sheet can be exercised without hardware.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QComboBox, QDialog, QHBoxLayout, QLabel, QListWidget, QListWidgetItem,
    QPushButton, QVBoxLayout,
)

from .roster import PLAYERS, Roster

ADDRESS_ROLE = Qt.UserRole
NAME_ROLE = Qt.UserRole + 1


class AddControllerSheet(QDialog):
    """Choose a discovered controller and the player it will be."""

    accepted_controller = Signal(str, str, int)     # address, name, player

    def __init__(self, roster: Roster, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Add a controller")
        self.setMinimumWidth(460)
        self.roster = roster

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 22, 24, 20)
        layout.setSpacing(14)

        title = QLabel("Add a controller")
        title.setObjectName("PageTitle")
        layout.addWidget(title)

        self.status = QLabel("Turn the controller on, then scan.")
        self.status.setObjectName("PageSubtitle")
        layout.addWidget(self.status)

        self.results = QListWidget()
        self.results.setMinimumHeight(150)
        self.results.itemSelectionChanged.connect(self._sync)
        layout.addWidget(self.results, 1)

        picker = QHBoxLayout()
        picker.setSpacing(10)
        picker.addWidget(QLabel("Player"))
        self.player = QComboBox()
        picker.addWidget(self.player, 1)
        layout.addLayout(picker)

        buttons = QHBoxLayout()
        buttons.setSpacing(10)
        self.scan_button = QPushButton("Scan")
        self.scan_button.setObjectName("Ghost")
        buttons.addWidget(self.scan_button)
        buttons.addStretch(1)
        cancel = QPushButton("Cancel")
        cancel.setObjectName("Ghost")
        cancel.clicked.connect(self.reject)
        buttons.addWidget(cancel)
        self.add_button = QPushButton("Add")
        self.add_button.clicked.connect(self._accept)
        buttons.addWidget(self.add_button)
        layout.addLayout(buttons)

        self._load_players()
        self._sync()

    def _load_players(self) -> None:
        """Only offer player numbers that are actually free."""
        self.player.clear()
        for number in PLAYERS:
            if number in self.roster.slots:
                continue
            self.player.addItem(f"P{number}", number)

    def scanning(self) -> None:
        self.status.setText("Scanning...")
        self.scan_button.setEnabled(False)
        self._sync()

    def show_results(self, controllers) -> None:
        """Fill the list from whatever discovery returned."""
        self.results.clear()
        self.scan_button.setEnabled(True)

        already = {slot.address.lower() for slot in self.roster.slots.values()}
        addable = 0
        for found in controllers:
            item = QListWidgetItem()
            item.setData(ADDRESS_ROLE, found.address)
            item.setData(NAME_ROLE, found.label)
            if found.address.lower() in already:
                item.setText(f"{found.label}   {found.address}   already added")
                item.setFlags(item.flags() & ~Qt.ItemIsEnabled)
            else:
                item.setText(f"{found.label}   {found.address}")
                addable += 1
            self.results.addItem(item)

        if not controllers:
            self.status.setText(
                "Nothing found. Turn the controller on and scan again.")
        elif not addable:
            self.status.setText("Everything found is already added.")
        else:
            plural = "" if addable == 1 else "s"
            self.status.setText(f"{addable} controller{plural} available.")
        self._sync()

    def failed(self, message: str) -> None:
        self.scan_button.setEnabled(True)
        self.status.setText(message)
        self._sync()

    def selection(self):
        """The chosen (address, name, player), or None if nothing is picked."""
        items = self.results.selectedItems()
        if not items or self.player.currentData() is None:
            return None
        item = items[0]
        return (item.data(ADDRESS_ROLE), item.data(NAME_ROLE),
                self.player.currentData())

    def _sync(self) -> None:
        self.add_button.setEnabled(self.selection() is not None)

    def _accept(self) -> None:
        chosen = self.selection()
        if chosen is None:
            return
        self.accepted_controller.emit(*chosen)
        self.accept()
