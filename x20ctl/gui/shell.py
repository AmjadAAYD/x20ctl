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

from PySide6.QtCore import Qt, QTimer, Signal
from PySide6.QtWidgets import (
    QHBoxLayout, QLabel, QMessageBox, QPushButton, QStackedWidget, QVBoxLayout,
    QWidget,
)

from .. import protocol as p
from ..input import MacroRecorder, XInputReader
from ..profiles import DEFAULT_DIR
from .addsheet import AddControllerSheet
from .buttons import ButtonsPage
from .curves import CurvesPage
from .header import HeaderBar
from .keytest import KeyTestPage
from .macros import MacroEditor
from .nav import NavRail
from .panels import Page, PowerPage, VibrationPage
from .presence import PresenceWatcher, ask_about_lost
from .roster import AlreadyAdded, PlayerTaken, Roster, RosterFull
from .updatebar import UpdateBar
from .saves import SavedMacrosPage
from .start import StartPage
from .tester import TesterPage
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
POLL_MS = 40                    # 25 times a second, smooth enough to read


def _percent(axis: int) -> int:
    """An XInput stick axis, which is signed 16 bit, as a percentage."""
    return max(-100, min(100, round(axis * 100 / 32767)))


def _trigger(value: int) -> int:
    """An XInput trigger, which is a byte, as a percentage."""
    return max(0, min(100, round(value * 100 / 255)))


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
    # Emitted whenever a snapshot brings a new reading, so anything outside the
    # window (the tray icon) can mirror it without reaching into the header.
    battery_read = Signal(object)

    # Set from a snapshot. Declared here rather than sprung into existence by
    # populate(), so every reader sees an empty list instead of AttributeError
    # when a record does not arrive.
    def __init__(self, rumbler=None) -> None:
        super().__init__()
        self.slot = None
        self.link = None
        # Set from a snapshot. Initialised here rather than sprung into
        # existence by populate(), so every reader sees an empty list instead of
        # AttributeError when a record does not arrive.
        self._trigger_curves: list = []
        self._stick_curves: list = []
        self._snapshot = None

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
            "sticks": CurvesPage(),
            "triggers": TriggersPage(),
            "motor": VibrationPage(rumbler),
            "macros": MacroEditor(rumbler),
            "saves": SavedMacrosPage(),
            # The 0.2.1 tester, which draws the sticks as circles you can
            # watch move. Amjad asked for it back, and it reads better than a
            # row of numbers.
            "test": TesterPage(),
            "timeout": PowerPage(),
            "device": DevicePage(),
        }
        for page in self.pages.values():
            self.stack.addWidget(page)
        root.addWidget(self.stack, 1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        root.addWidget(self.status)

        # Live input for the test tab and the trigger meters. Nothing was
        # driving them, so both looked broken while working perfectly.
        self.reader = XInputReader()
        self.poll_timer = QTimer(self)
        self.poll_timer.setInterval(POLL_MS)
        self.poll_timer.timeout.connect(self.poll_inputs)
        self.poll_timer.start()

        self.recorder = None
        self.section = None
        self.show_section(self.rail.current() or "buttons")

    def poll_inputs(self) -> None:
        """Feed whatever the pad is doing to the pages that show it live."""
        state = self.reader.poll() if self.reader.available else None
        if state is None:
            # Silence looks identical to a broken page, so say which
            # connection is missing.
            if self.section == "triggers":
                self.pages["triggers"].set_positions(0, 0)
            return

        if self.recorder is not None and self.recorder.recording:
            self.recorder.poll()

        if self.section == "triggers":
            self.pages["triggers"].set_positions(
                _trigger(state.left_trigger), _trigger(state.right_trigger))

    def show_section(self, key: str) -> None:
        """Bring one section's page to the front."""
        page = self.pages.get(key)
        if page is None:
            return
        if self.section == "motor" and key != "motor":
            self.pages["motor"].flush()      # do not lose a pending change
        if self.section == "test" and key != "test":
            self.pages["test"].stop()        # the old tester owns its own poll
        self.section = key
        self.stack.setCurrentWidget(page)
        if key == "test":
            self.pages["test"].start()

    def say(self, message: str) -> None:
        self.status.setText(message)

    def show_slot(self, slot, roster: Roster) -> None:
        """Point the workspace at one controller, with tabs for the others."""
        self.slot = slot
        self.header.show_slot(slot)

        # Draw the buttons page immediately from the list this family reports,
        # rather than leaving it blank for the several seconds a connection and
        # two reads take. The live read replaces it when it lands.
        if not self.pages["buttons"].boxes:
            self.pages["buttons"].load(list(p.CHANGEKEY_DEFAULT_SOURCES))

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
            (self.pages["buttons"].save_requested, link.set_remapping),
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
        self.pages["buttons"].save_requested.connect(link.set_remapping)
        self.pages["device"].calibrate_requested.connect(link.calibrate)
        self.pages["macros"].apply_requested.connect(self._write_macro)
        self.pages["macros"].read_requested.connect(self._read_macro)
        self.pages["macros"].saves.save_requested.connect(self._save_profile)
        saved = self.pages["saves"]
        saved.show_requested.connect(self._show_profile)
        saved.load_requested.connect(self._send_profile)
        saved.delete_requested.connect(self._delete_profile)
        self.refresh_saves()
        self.pages["macros"].record_requested.connect(self._record_macro)
        self.pages["triggers"].zone_chosen.connect(self._choose_zone)
        self.pages["triggers"].shape_chosen.connect(self._choose_shape)
        self.pages["triggers"].swap_toggled.connect(self._swap_triggers)
        self.pages["triggers"].save_requested.connect(self._write_triggers)
        self.pages["sticks"].write_requested.connect(self._write_sticks)
        self.header.factory_reset.connect(link.factory_reset)
        # One at a time: the link refuses overlapping work, so the remapping
        # read is chained onto the snapshot rather than fired alongside it.
        link.load()

    def populate(self, snapshot) -> None:
        """Show what the pad currently holds, without writing any of it back."""
        from .. import protocol as p

        vibration = getattr(snapshot, "vibration", None)
        if vibration:
            self.pages["motor"].load(vibration[0])

        triggers = getattr(snapshot, "triggers", None) or []
        if triggers:
            self._trigger_curves = [p.Curve.parse(raw, p.TRIGGER_MAX_PROGRESS)
                                    for raw in triggers]
            self.pages["triggers"].load(self._trigger_curves)

        sticks = getattr(snapshot, "sticks", None) or []
        if sticks:
            self._stick_curves = [p.Curve.parse(raw, p.STICK_MAX_PROGRESS)
                                  for raw in sticks]
            self.pages["sticks"].load("sticks", self._stick_curves,
                                      baseline=True)
        # Separately, and guarded. These used to be one block, so a pad that
        # answered HOST_STICK but not HOST_TRIGGER raised AttributeError here and
        # aborted the whole load. That is reachable two ways: a pad reporting no
        # triggers, and a single read timing out, which the first query on a
        # fresh link is known to do.
        if self._trigger_curves:
            self.pages["sticks"].load("triggers", self._trigger_curves,
                                      baseline=True)

        # The shutdown timeout is carried in the motor record the snapshot
        # already read. Before this it was never loaded, so the page showed its
        # own default and a pad set to 30 minutes read as 10.
        # The PowerPage is keyed "timeout", not "power".
        power = self.pages.get("timeout")
        if power is not None and hasattr(power, "load"):
            power.load(getattr(snapshot, "shutdown", None))

        self._snapshot = snapshot
        battery = getattr(snapshot, "battery", None)
        self.header.show_battery(battery)
        self.battery_read.emit(battery)
        self.pages["device"].load(snapshot)
        self.say("Loaded.")
        if self.link is not None and not self.pages["buttons"].boxes:
            self.link.read_remapping(self._show_remapping)

    def _show_remapping(self, result) -> None:
        """Fill the buttons page from what the pad reports."""
        sources, mapping = result
        self.pages["buttons"].blockSignals(True)
        self.pages["buttons"].load(sources, mapping)
        self.pages["buttons"].blockSignals(False)
        # Chained, not fired alongside: the link refuses overlapping work.
        self._read_all_macros()

    def _read_all_macros(self) -> None:
        """Load every macro slot the pad has, without being asked per slot.

        A user reported "each macro needs an individual Read from controller
        press". They are read here instead, as one operation after the remapping
        read, so the editor opens showing what the controller actually holds.
        """
        if self.link is None:
            return
        # Ask only for slots the pad says it has, by bit position rather than by
        # count: a pad with a gap in its macro bits would otherwise be asked for
        # the wrong slots. The editor holds M1-M4, so stop there.
        caps = getattr(self._snapshot, "capabilities", None)
        slots = list(range(1, 5))
        bits = getattr(caps, "macros", None)
        if bits:
            present = [n for n in range(1, 5) if bits >> (n - 1) & 1]
            if present:
                slots = present
        self.link.read_macros(slots, self._macros_read)

    def _macros_read(self, programs) -> None:
        from .macrogrid import MacroGrid

        if not programs:
            return
        loaded = []
        for number, program in sorted(programs.items()):
            slot = f"M{number}"
            if program is None:
                continue
            self.pages["macros"].load(slot, MacroGrid.from_program(program))
            loaded.append(slot)
        if loaded:
            self.say("Macros read from the controller: " + ", ".join(loaded))
        else:
            self.say("No macros stored on the controller.")

    # -- writes from the pages -------------------------------------------

    def _choose_zone(self, side: str, zone: str) -> None:
        """Remember the choice. Nothing reaches the pad until Save."""
        self._edited_triggers(side, gear=zone)

    def _choose_shape(self, side: str, shape: str) -> None:
        self._edited_triggers(side, shape=shape)

    def _write_triggers(self) -> None:
        curves = getattr(self, "_trigger_curves", None)
        if not curves or self.link is None:
            self.say("Trigger settings have not loaded yet.")
            return
        if not self.link.set_curves("triggers", curves):
            self.say("The controller is busy. Try that again in a moment.")

    def _edited_triggers(self, side: str, *, gear=None, shape=None):
        """One side changed; the other keeps exactly what it had."""
        from .triggers import SIDES, preset_for

        if not getattr(self, "_trigger_curves", None):
            self.say("Trigger settings have not loaded yet.")
            return None

        index = SIDES.index(side)
        out = []
        for position, curve in enumerate(self._trigger_curves):
            if position == index:
                if gear is not None:
                    inner, outer = p.TRIGGER_GEARS[gear]
                    curve = curve.with_deadzones(inner, outer)
                if shape is not None:
                    curve = curve.with_points(*p.CURVE_PRESETS[preset_for(shape)])
            out.append(curve)
        self._trigger_curves = out
        return out

    def _swap_triggers(self, swapped: bool) -> None:
        """L2/R2 exchange. The pad stores the flag on both channels, not one.

        KeyLinker writes isTriggerExchange into the flag byte of each side, so
        setting it on one channel alone would be a state the vendor app never
        produces. Nothing reaches the pad until Save, like the rest of the page.
        """
        curves = getattr(self, "_trigger_curves", None)
        if not curves:
            self.say("Trigger settings have not loaded yet.")
            return
        self._trigger_curves = [c.with_flags(swapped=swapped) for c in curves]

    def _write_sticks(self) -> None:
        page = self.pages["sticks"]
        for kind in ("sticks", "triggers"):
            channels = page.channels(kind) if hasattr(page, "channels") else None
            if channels and self.link is not None:
                self.link.set_curves(kind, channels)

    # -- save files ------------------------------------------------------

    def store(self):
        """This controller's own save directory, or None before one is open."""
        if self.slot is None:
            return None
        from ..profiles import ProfileStore
        return ProfileStore(profile_dir(self.slot.save_key))

    def refresh_saves(self) -> None:
        store = self.store()
        if store is None:
            return
        self.pages["saves"].show_saves(
            [profile.name for profile in store.list()])

    def _save_profile(self, name: str) -> None:
        """Write what is on screen: all four macros, vibration, the curves."""
        from .. import protocol as p
        from ..profiles import MacroSpec, Profile

        store = self.store()
        if store is None:
            return
        profile = Profile(name=name, vibration=self.pages["motor"].value())
        for slot, grid in self.pages["macros"].grids.items():
            if grid.empty:
                continue
            program = p.MacroProgram(steps=grid.to_steps(),
                                     loop_interval_ms=grid.loop_ms)
            profile.macros[slot] = MacroSpec(keys=program.as_sequence(),
                                             loop_ms=grid.loop_ms)
        try:
            store.save(profile)
        except Exception as exc:                # noqa: BLE001 - shown to user
            self.say(f"Could not save: {exc}")
            return
        self.refresh_saves()
        self.pages["saves"].select(name)
        self.pages["macros"].saves.clear()
        self.say(f"Saved “{name}”.")

    def _show_profile(self, name: str) -> None:
        """Open a save file in the editor and go there, without sending it."""
        if self._load_profile(name):
            self.rail.select("macros")

    def _send_profile(self, name: str) -> None:
        """Load a save file and write its macros to the controller."""
        if not self._load_profile(name):
            return
        if self.link is None:
            self.pages["saves"].say("No controller connected.")
            return
        for slot, grid in self.pages["macros"].grids.items():
            if not grid.empty:
                self._write_macro(slot, grid)
        self.pages["saves"].say(f"Sent “{name}” to the controller.")

    def _load_profile(self, name: str) -> bool:
        """Put a save file on screen. Returns whether it worked."""
        from .macrogrid import MacroGrid
        from .macrogrid import grid_from_recorded as grid_from_spec

        store = self.store()
        if store is None:
            return False
        try:
            profile = store.load(name)
        except Exception as exc:                # noqa: BLE001 - shown to user
            self.say(f"Could not load: {exc}")
            self.pages["saves"].say(f"Could not load: {exc}")
            return False

        if profile.vibration is not None:
            self.pages["motor"].load(profile.vibration)
        for slot, spec in profile.macros.items():
            if spec is None:
                self.pages["macros"].load(slot, MacroGrid())
                continue
            self.pages["macros"].load(slot, grid_from_spec(spec))

        self.say(f"Loaded “{name}”.")
        return True

    def _delete_profile(self, name: str) -> None:
        store = self.store()
        if store is None:
            return
        try:
            store.delete(name)
        except Exception as exc:                # noqa: BLE001 - shown to user
            self.say(f"Could not delete: {exc}")
            return
        self.refresh_saves()
        self.say(f"Deleted “{name}”.")

    # -- macros ----------------------------------------------------------

    def _read_macro(self, slot: str) -> None:
        if self.link is None:
            self.say("No controller connected.")
            return
        number = int(slot[1])
        self.say(f"Reading {slot}...")
        started = self.link.read_macro(
            number, lambda program: self._macro_read(slot, program))
        if not started:
            self.say("The controller is busy. Try that again in a moment.")

    def _macro_read(self, slot: str, program) -> None:
        from .macrogrid import MacroGrid
        if program is None:
            self.say(f"{slot} is empty.")
            return
        self.pages["macros"].load(slot, MacroGrid.from_program(program))
        self.say(f"{slot} loaded from the controller.")

    def _record_macro(self, slot: str) -> None:
        """Start or stop writing down what is played on the pad."""
        from .macrogrid import grid_from_recorded

        if self.recorder is not None and self.recorder.recording:
            spec = self.recorder.stop()
            ignored = sorted(self.recorder.ignored)
            self.recorder = None
            if spec is None:
                # Nothing was played between starting and stopping.
                self.say("Nothing was recorded. Press something next time.")
                return
            grid = grid_from_recorded(spec)
            self.pages["macros"].load(slot, grid)
            note = f"Recorded {len(grid)} steps into {slot}."
            if ignored:
                note += (" Ignored " + ", ".join(ignored)
                         + ": the pad cannot put those in a macro.")
            self.say(note)
            return

        if not self.reader.available:
            self.say("Connect the controller to this PC to record.")
            return
        self.recorder = MacroRecorder(self.reader)
        self.recorder.start()
        self.say(f"Recording into {slot}. Press Record again to stop.")

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

    hidden_to_tray = Signal()

    def __init__(self, *, scan=None, roster: Roster | None = None,
                 bridge=None, open_pad=None, rumbler=None) -> None:
        super().__init__()
        # Closing the window hides it and leaves the app running in the tray.
        # Off by default so a build without a tray, or a test, still closes.
        self._close_to_tray = False
        self._told_about_tray = False
        self.first_hide_to_tray = False
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

        # Bottom left, under everything: the launch update check. Quiet unless
        # there is something to say.
        self.updates = UpdateBar()
        self.updates.finished.connect(self._update_checked)
        root.addWidget(self.updates)

        self.watcher = PresenceWatcher(self.roster, scan=scan, bridge=bridge)
        self.watcher.changed.connect(self.refresh)
        self.watcher.lost.connect(self.controller_lost)
        self.watcher.start()

        self.refresh()

    def check_for_updates(self, fetch=None) -> None:
        """Start the launch check. Separate from __init__ so a test can skip it."""
        self.updates.start(fetch)

    def _update_checked(self, _message: str, release) -> None:
        """Only interrupt when there is actually a newer version."""
        if release is None:
            return
        self._update_box = self.updates.offer(release)

    def controller_lost(self, slot) -> None:
        """A controller stopped answering. Ask, rather than guessing."""
        box = ask_about_lost(slot, self)

        def answered(button) -> None:
            if box.buttonRole(button) == QMessageBox.DestructiveRole:
                self.remove_controller(slot.player)
            else:
                self.watcher.watch_again(slot)

        box.buttonClicked.connect(answered)
        box.show()
        return box

    # -- roster ----------------------------------------------------------

    def refresh(self) -> None:
        self.start.show_roster(self.roster)

    def set_close_to_tray(self, enabled: bool) -> None:
        """Hide on close instead of quitting.

        Only ever enabled when a tray icon exists, because the window is the
        only way back in and an app with no way back is just gone.
        """
        self._close_to_tray = bool(enabled)

    def closeEvent(self, event) -> None:            # noqa: N802 - Qt's name
        if self._close_to_tray:
            event.ignore()
            self.hide()
            first = not self._told_about_tray
            self._told_about_tray = True
            self.hidden_to_tray.emit()
            if first:
                # Windows convention: say where it went, once.
                self.first_hide_to_tray = True
            return
        super().closeEvent(event)

    def show_roster(self) -> None:
        self.pages.setCurrentIndex(ROSTER_PAGE)
        # No controller is on screen, so any battery reading on the tray belongs
        # to a pad we are no longer watching. Leaving it there is worse than
        # showing nothing: it could be hours stale, or from a pad now switched
        # off, and the tray is visible with the window closed.
        self.workspace.battery_read.emit(None)
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
        slot = self.roster.slots.get(player)
        if slot is not None:
            self.watcher.forget(slot)
        self.links.pop(player, None)
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
