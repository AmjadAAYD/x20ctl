"""The app's outer frame: the roster, and the workspace for one controller.

Opening the app lands on the roster. Adding a controller scans, asks which
player it is, and puts it in the list. Choosing one opens its workspace, and
everything inside that workspace belongs to that controller alone, including
its save files.

The scan is injected rather than imported so the whole flow can be driven with
no radio present.
"""

from __future__ import annotations

import os

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QHBoxLayout, QLabel, QPushButton, QStackedWidget, QVBoxLayout, QWidget,
)

from ..profiles import DEFAULT_DIR
from .addsheet import AddControllerSheet
from .header import HeaderBar
from .nav import NavRail
from .roster import AlreadyAdded, PlayerTaken, Roster, RosterFull
from .start import StartPage

ROSTER_PAGE = 0
WORKSPACE_PAGE = 1


def profile_dir(save_key: str) -> str:
    """Where one controller's save files live.

    Per controller rather than global: two pads on one desk should not fight
    over the same profile list. Only a path; the directory is created when
    something is actually saved, so adding a controller writes nothing.
    """
    return os.path.join(DEFAULT_DIR, "controllers", save_key)


class Workspace(QWidget):
    """Everything you can change about one controller.

    A shell for now: the header, the player tabs and the back door. The pages
    themselves land here next.
    """

    back = Signal()
    switch_player = Signal(int)
    factory_reset = Signal()

    def __init__(self) -> None:
        super().__init__()
        self.slot = None

        outer = QHBoxLayout(self)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        self.rail = NavRail()
        self.rail.selected.connect(self.show_section)
        outer.addWidget(self.rail)

        column = QWidget()
        outer.addWidget(column, 1)
        root = QVBoxLayout(column)
        root.setContentsMargins(30, 24, 30, 24)
        root.setSpacing(14)

        self.header = HeaderBar()
        self.header.back.connect(self.back.emit)
        self.header.factory_reset.connect(self.factory_reset.emit)
        # Kept as attributes because they are the two the rest of the app and
        # the tests reach for by name.
        self.back_button = self.header.back_button
        self.title = self.header.title
        root.addWidget(self.header)

        self.tabs = QHBoxLayout()
        self.tabs.setSpacing(8)
        root.addLayout(self.tabs)

        self.body = QLabel()
        self.body.setObjectName("PageSubtitle")
        self.body.setAlignment(Qt.AlignCenter)
        self.body.setWordWrap(True)
        root.addWidget(self.body, 1)

        self.section = None
        self.show_section(self.rail.current() or "buttons")

    def show_section(self, key: str) -> None:
        """Until the real pages land, say what this section will hold.

        Named rather than blank: an empty panel reads as broken, and the blurb
        is the same text the rail shows on hover.
        """
        from .nav import SECTIONS
        self.section = key
        blurb = next((s.blurb for s in SECTIONS if s.key == key), "")
        title = next((s.title for s in SECTIONS if s.key == key), key)
        self.body.setText(f"{title}\n\n{blurb}")

    def show_slot(self, slot, roster: Roster) -> None:
        """Point the workspace at one controller, with tabs for the others."""
        self.slot = slot
        self.header.show_slot(slot)

        while self.tabs.count():
            item = self.tabs.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        for other in roster.ordered():
            tab = QPushButton(f"P{other.player}")
            tab.setObjectName("Ghost")
            tab.setCheckable(True)
            tab.setChecked(other.player == slot.player)
            tab.setCursor(Qt.PointingHandCursor)
            tab.clicked.connect(
                lambda _=False, n=other.player: self.switch_player.emit(n))
            self.tabs.addWidget(tab)
        self.tabs.addStretch(1)


class AppShell(QWidget):
    """Roster and workspace, and the moving between them."""

    def __init__(self, *, scan=None, roster: Roster | None = None) -> None:
        super().__init__()
        self.roster = roster if roster is not None else Roster()
        self._scan = scan

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)

        self.pages = QStackedWidget()
        self.start = StartPage()
        self.start.add_requested.connect(self.open_add_sheet)
        self.start.opened.connect(self.open_controller)
        self.start.removed.connect(self.remove_controller)
        self.pages.addWidget(self.start)

        self.workspace = Workspace()
        self.workspace.back.connect(self.show_roster)
        self.workspace.switch_player.connect(self.open_controller)
        self.pages.addWidget(self.workspace)

        root.addWidget(self.pages)
        self.refresh()

    # -- roster ----------------------------------------------------------

    def refresh(self) -> None:
        self.start.show_roster(self.roster)

    def show_roster(self) -> None:
        self.pages.setCurrentIndex(ROSTER_PAGE)
        self.refresh()

    def open_controller(self, player: int) -> None:
        slot = self.roster.slots.get(player)
        if slot is None:
            self.show_roster()
            return
        self.workspace.show_slot(slot, self.roster)
        self.pages.setCurrentIndex(WORKSPACE_PAGE)

    def remove_controller(self, player: int) -> None:
        self.roster.remove(player)
        if not self.roster:
            self.show_roster()
        elif (self.workspace.slot is not None
                and self.workspace.slot.player == player):
            self.open_controller(self.roster.taken()[0])
        self.refresh()

    def add_controller(self, address: str, name: str, player: int):
        """Put a discovered controller in the roster. Returns the slot, or None.

        Refuses politely rather than raising into the GUI: the sheet already
        prevents both cases, so reaching here means something raced.
        """
        try:
            slot = self.roster.add(address, name=name, product=name,
                                   player=player)
        except (AlreadyAdded, PlayerTaken, RosterFull):
            self.refresh()
            return None
        self.refresh()
        return slot

    # -- adding ----------------------------------------------------------

    def open_add_sheet(self) -> AddControllerSheet:
        sheet = AddControllerSheet(self.roster, self)
        sheet.accepted_controller.connect(self.add_controller)
        sheet.scan_button.clicked.connect(lambda: self.run_scan(sheet))
        self.run_scan(sheet)
        sheet.show()
        return sheet

    def run_scan(self, sheet: AddControllerSheet) -> None:
        if self._scan is None:
            sheet.failed("Scanning is unavailable in this build.")
            return
        sheet.scanning()
        try:
            sheet.show_results(self._scan())
        except Exception as exc:                    # noqa: BLE001 - shown to user
            sheet.failed(str(exc) or exc.__class__.__name__)
