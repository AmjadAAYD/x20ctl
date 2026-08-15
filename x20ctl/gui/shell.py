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
from .buttons import ButtonsPage
from .header import HeaderBar
from .keytest import KeyTestPage
from .macros import MacroEditor
from .nav import NavRail
from .panels import Page, PowerPage, VibrationPage
from .roster import AlreadyAdded, PlayerTaken, Roster, RosterFull
from .start import StartPage
from .triggers import TriggersPage


class CurvesPlaceholder(Page):
    """Sticks reuse the existing curves editor, which is not moved yet."""

    def __init__(self, title: str) -> None:
        super().__init__(
            title,
            "Deadzones and response curves for both sticks. The existing "
            "curve editor moves here next.")
        self.root.addStretch(1)


class DevicePage(Page):
    """Calibration and what the pad says about itself."""

    calibrate_requested = Signal()

    def __init__(self) -> None:
        super().__init__(
            "Device",
            "Calibration and what this controller reports about itself. "
            "Factory reset lives in the header, away from the settings.")

        self.calibrate_button = QPushButton("Calibrate motion sensor")
        self.calibrate_button.setObjectName("Ghost")
        self.calibrate_button.setCursor(Qt.PointingHandCursor)
        self.calibrate_button.setToolTip(
            "Lay the controller flat first. It measures level from where it "
            "is resting, and waits for you to press + on the pad.")
        self.calibrate_button.clicked.connect(self.calibrate_requested.emit)
        self.root.addWidget(self.calibrate_button, 0, Qt.AlignLeft)

        self.detail = QLabel()
        self.detail.setObjectName("RowDetail")
        self.detail.setWordWrap(True)
        self.root.addSpacing(14)
        self.root.addWidget(self.detail)
        self.root.addWidget(self.status)
        self.root.addStretch(1)

    def load(self, snapshot) -> None:
        device = getattr(snapshot, "device", None)
        caps = getattr(snapshot, "capabilities", None)
        lines = [f"Name: {getattr(snapshot, 'name', 'unknown')}"]
        if device is not None:
            lines.append(f"Firmware: {getattr(device, 'version', 'unknown')}")
        if caps is not None:
            lines.append(f"Macro slots: {bin(caps.macros).count('1')}")
            lines.append(f"Remapping: {'yes' if caps.changekey else 'no'}")
        self.detail.setText("\n".join(lines))

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

    def __init__(self, rumbler=None) -> None:
        super().__init__()
        self.slot = None
        self.link = None

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

        self.stack = QStackedWidget()
        self.pages = {
            "buttons": ButtonsPage(),
            "sticks": CurvesPlaceholder("Sticks"),
            "triggers": TriggersPage(),
            "motor": VibrationPage(rumbler),
            "macros": MacroEditor(rumbler),
            "test": KeyTestPage(),
            "timeout": PowerPage(),
            "device": DevicePage(),
        }
        for page in self.pages.values():
            self.stack.addWidget(page)
        root.addWidget(self.stack, 1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        root.addWidget(self.status)

        self.section = None
        self.show_section(self.rail.current() or "buttons")

    def show_section(self, key: str) -> None:
        """Bring one section's page to the front."""
        page = self.pages.get(key)
        if page is None:
            return
        if self.section == "motor" and key != "motor":
            self.pages["motor"].flush()      # do not lose a pending change
        self.section = key
        self.stack.setCurrentWidget(page)

    def say(self, message: str) -> None:
        self.status.setText(message)

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

    # -- the live controller ---------------------------------------------

    def _wiring(self, link):
        """Every connection between this workspace and one link."""
        return (
            (link.loaded, self.populate),
            (link.written, self.say),
            (link.failed, self.say),
            (self.pages["motor"].save_requested, link.set_vibration),
            (self.pages["timeout"].save_requested, link.set_shutdown),
            (self.pages["buttons"].changed, link.set_remapping),
            (self.pages["device"].calibrate_requested, link.calibrate),
            (self.header.factory_reset, link.factory_reset),
        )

    def detach(self) -> None:
        """Let go of the current controller, so it cannot be written to by a
        page belonging to a different one."""
        if self.link is None:
            return
        for signal, slot in self._wiring(self.link):
            try:
                signal.disconnect(slot)
            except (RuntimeError, TypeError):
                pass
        try:
            self.pages["macros"].apply_requested.disconnect(self._write_macro)
        except (RuntimeError, TypeError):
            pass
        self.link = None

    def attach(self, link) -> None:
        """Point the pages at one controller and read what it holds."""
        if link is self.link:
            link.load()
            return
        self.detach()
        self.link = link
        link.loaded.connect(self.populate)
        link.written.connect(self.say)
        link.failed.connect(self.say)

        self.pages["motor"].save_requested.connect(link.set_vibration)
        self.pages["timeout"].save_requested.connect(link.set_shutdown)
        self.pages["buttons"].changed.connect(link.set_remapping)
        self.pages["device"].calibrate_requested.connect(link.calibrate)
        self.pages["macros"].apply_requested.connect(self._write_macro)
        self.header.factory_reset.connect(link.factory_reset)
        link.load()

    def populate(self, snapshot) -> None:
        """Show what the pad currently holds, without writing any of it back."""
        from .. import protocol as p

        vibration = getattr(snapshot, "vibration", None)
        if vibration:
            self.pages["motor"].load(vibration[0])

        triggers = getattr(snapshot, "triggers", None) or []
        if triggers:
            curves = [p.Curve.parse(raw, p.TRIGGER_MAX_PROGRESS)
                      for raw in triggers]
            self.pages["triggers"].load(curves)

        self.pages["device"].load(snapshot)
        self.say("Loaded.")

    def _write_macro(self, slot: str, grid) -> None:
        if self.link is None:
            return
        number = int(slot[1])
        self.link.write_macro(number, grid.to_steps(), loop_ms=grid.loop_ms)


def build_app_window():
    """The real thing: a shell with a radio, a rumbler and a title.

    Kept here rather than in the launcher so the wiring lives next to what it
    wires, and so a test can build the same object.
    """
    from ..client import X20, find_controllers
    from .bridge import AsyncBridge
    from .rumble import Rumbler

    shell = AppShell(scan=find_controllers, bridge=AsyncBridge(),
                     open_pad=X20, rumbler=Rumbler())
    shell.setWindowTitle("x20ctl")
    shell.resize(1280, 820)
    shell.setMinimumSize(1040, 680)
    return shell


class AppShell(QWidget):
    """Roster and workspace, and the moving between them."""

    def __init__(self, *, scan=None, roster: Roster | None = None,
                 bridge=None, open_pad=None, rumbler=None) -> None:
        super().__init__()
        self.roster = roster if roster is not None else Roster()
        self._scan = scan
        self._bridge = bridge
        self._open_pad = open_pad
        self._rumbler = rumbler
        self.links: dict[int, object] = {}

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)

        self.pages = QStackedWidget()
        self.start = StartPage()
        self.start.add_requested.connect(self.open_add_sheet)
        self.start.opened.connect(self.open_controller)
        self.start.removed.connect(self.remove_controller)
        self.pages.addWidget(self.start)

        self.workspace = Workspace(rumbler)
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
        self.connect_controller(slot)

    def connect_controller(self, slot) -> None:
        """Attach a live link for this controller, reusing one if it exists.

        Without a way to open a pad this does nothing, which is how the whole
        shell stays drivable with no radio.
        """
        if self._open_pad is None or self._bridge is None:
            return
        link = self.links.get(slot.player)
        if link is None:
            from .link import ControllerLink
            link = ControllerLink(slot.address, bridge=self._bridge,
                                  open_pad=self._open_pad)
            self.links[slot.player] = link
            self.workspace.attach(link)
        else:
            self.workspace.attach(link)

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

        if self._bridge is not None:
            # A real scan takes seconds. Off the GUI thread, or the window
            # freezes and Windows offers to close it.
            self._bridge.run(self._scan,
                             on_done=sheet.show_results,
                             on_error=sheet.failed)
            return

        try:
            sheet.show_results(self._scan())
        except Exception as exc:                    # noqa: BLE001 - shown to user
            sheet.failed(str(exc) or exc.__class__.__name__)
