"""The main window."""

from __future__ import annotations

from PySide6.QtCore import Qt, QTimer
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QInputDialog, QLabel, QListWidget, QListWidgetItem,
    QMessageBox, QPushButton, QScrollArea, QStackedWidget, QVBoxLayout, QWidget,
)

from .. import transport
from ..config import load_address, save_address
from ..client import X20, find_controller
from ..input import MacroRecorder, XInputReader
from ..profiles import MacroSpec, Profile, ProfileStore, SLOTS
from . import theme
from .bridge import AsyncBridge
from .tester import TesterPage
from .widgets import MacroCard, StatusDot, VibrationRow, divider, section_label

RECORD_POLL_MS = 5      # matches the protocol's own timing resolution


class MainWindow(QWidget):
    def __init__(self) -> None:
        super().__init__()
        self.setWindowTitle("x20ctl")
        self.resize(940, 660)
        self.setMinimumSize(820, 560)

        self.bridge = AsyncBridge()
        self.store = ProfileStore()
        self.address: str | None = load_address()
        self.pad: X20 | None = None
        self.profile = Profile(name="Save file 1")
        self._loaded_name: str | None = None
        self._busy = False

        self.reader = XInputReader()
        self.recorder = MacroRecorder(self.reader)
        self.recording_slot: str | None = None
        self.record_timer = QTimer(self)
        self.record_timer.setInterval(RECORD_POLL_MS)
        self.record_timer.timeout.connect(self._record_tick)

        root = QHBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(0)
        root.addWidget(self._build_sidebar())

        self.pages = QStackedWidget()
        self.pages.addWidget(self._build_main())
        self.tester = TesterPage()
        self.pages.addWidget(self.tester)
        root.addWidget(self.pages, 1)

        self.reload_profiles()
        QTimer.singleShot(200, self.connect_controller)

    # -- layout ----------------------------------------------------------

    def _build_sidebar(self) -> QWidget:
        panel = QFrame()
        panel.setObjectName("Sidebar")
        panel.setFixedWidth(232)

        layout = QVBoxLayout(panel)
        layout.setContentsMargins(0, 18, 0, 14)
        layout.setSpacing(10)

        heading = QLabel("x20ctl")
        heading.setObjectName("Title")
        heading.setContentsMargins(16, 0, 16, 0)
        layout.addWidget(heading)

        caption = QLabel("controller configuration")
        caption.setObjectName("Faint")
        caption.setContentsMargins(16, 0, 16, 0)
        layout.addWidget(caption)

        layout.addSpacing(14)
        holder = QWidget()
        holder_layout = QVBoxLayout(holder)
        holder_layout.setContentsMargins(16, 0, 16, 0)
        holder_layout.addWidget(section_label("save files", (
            "A save file is a complete set of macros plus a vibration level.\n\n"
            "The controller has four fixed slots and knows nothing about save "
            "files: switching between them rewrites those slots. So a save file "
            "lives on this computer, not on the pad.\n\n"
            "Applying makes the controller match the save file exactly. Any "
            "slot this file leaves empty is cleared on the pad, so switching "
            "between save files really switches.\n\n"
            "That includes assignments you made with button combinations on the "
            "pad itself: software cannot read those back, so it cannot preserve "
            "them. If you rely on one, define it in the save file too.\n\n"
            "Edits save by themselves. Recording a macro, or switching to "
            "another save file, writes your changes to disk first.")))
        layout.addWidget(holder)

        self.profile_list = QListWidget()
        self.profile_list.currentItemChanged.connect(self._profile_selected)
        layout.addWidget(self.profile_list, 1)

        buttons = QWidget()
        column = QVBoxLayout(buttons)
        column.setContentsMargins(16, 0, 16, 0)
        column.setSpacing(6)

        add_button = QPushButton("Add save file")
        add_button.setObjectName("Ghost")
        add_button.clicked.connect(self.new_profile)
        column.addWidget(add_button)

        row = QHBoxLayout()
        row.setSpacing(6)
        rename_button = QPushButton("Rename")
        rename_button.setObjectName("Ghost")
        rename_button.clicked.connect(self.rename_profile)
        remove_button = QPushButton("Remove")
        remove_button.setObjectName("Danger")
        remove_button.clicked.connect(self.delete_profile)
        row.addWidget(rename_button)
        row.addWidget(remove_button)
        column.addLayout(row)
        layout.addWidget(buttons)

        layout.addSpacing(4)
        nav = QWidget()
        nav_layout = QVBoxLayout(nav)
        nav_layout.setContentsMargins(16, 0, 16, 0)
        self.tester_button = QPushButton("Input tester")
        self.tester_button.setObjectName("Ghost")
        self.tester_button.setCheckable(True)
        self.tester_button.clicked.connect(self.toggle_tester)
        nav_layout.addWidget(self.tester_button)
        layout.addWidget(nav)
        return panel

    def _build_main(self) -> QWidget:
        page = QWidget()
        layout = QVBoxLayout(page)
        layout.setContentsMargins(24, 20, 24, 20)
        layout.setSpacing(16)

        # header ---------------------------------------------------------
        header = QHBoxLayout()
        header.setSpacing(10)
        self.dot = StatusDot()
        self.device_name = QLabel("Not connected")
        self.device_name.setObjectName("Title")
        self.device_detail = QLabel("")
        self.device_detail.setObjectName("Faint")
        self.link_label = QLabel("")
        self.link_label.setObjectName("Faint")
        self.link_label.setToolTip(transport.describe_for_config())
        self.battery_label = QLabel("")
        self.battery_label.setObjectName("Muted")
        self.battery_label.setToolTip(
            "The controller reports charge as four steps, not a percentage.")

        stack = QVBoxLayout()
        stack.setSpacing(1)
        stack.addWidget(self.device_name)
        stack.addWidget(self.device_detail)

        self.connect_button = QPushButton("Connect")
        self.connect_button.setObjectName("Ghost")
        self.connect_button.clicked.connect(self.connect_controller)

        header.addWidget(self.dot)
        header.addLayout(stack)
        header.addStretch(1)
        header.addWidget(self.battery_label)
        header.addWidget(self.link_label)
        header.addWidget(self.connect_button)
        layout.addLayout(header)
        layout.addWidget(divider())

        # capability notice ----------------------------------------------
        self.notice = QLabel("")
        self.notice.setObjectName("Faint")
        self.notice.setWordWrap(True)
        self.notice.hide()
        layout.addWidget(self.notice)

        # macro cards ----------------------------------------------------
        layout.addWidget(section_label("macros", (
            "Each of the four rear buttons can replay a sequence.\n\n"
            "WRITING A SEQUENCE\n"
            "  '+' presses keys together:  A+B is a chord\n"
            "  ',' plays them in turn:     A,B is A then B\n"
            "  Combine them:               A+B,X\n\n"
            "Sticks work too, as a direction: LS_UP, RS_DOWN_LEFT.\n"
            "So LS_UP+A pushes the stick up while holding A.\n\n"
            "THE THREE TIMING FIELDS\n"
            "  hold: how long each press is held down. 50ms is faster than a "
            "human can press, and some games poll slowly enough to miss it. "
            "80 to 120ms behaves like a deliberate press.\n\n"
            "  gap: the pause between one press and the next. Games that "
            "debounce input need a real gap or they treat two presses as one.\n\n"
            "  loop: an interval, not a duration. Zero means the macro fires "
            "once. Anything else makes it repeat forever with that gap between "
            "runs, until you press a different macro button. There is no "
            "'repeat for N seconds' setting on this hardware.\n\n"
            "All three are stored in units of 5ms, which is the controller's own "
            "resolution, so values snap to multiples of five.")))
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        holder = QWidget()
        cards = QVBoxLayout(holder)
        cards.setContentsMargins(0, 0, 6, 0)
        cards.setSpacing(10)
        self.cards: dict[str, MacroCard] = {}
        for slot in SLOTS:
            card = MacroCard(slot)
            card.changed.connect(self._sync_apply_state)
            card.record_requested.connect(self.toggle_recording)
            self.cards[slot] = card
            cards.addWidget(card)
        cards.addStretch(1)
        scroll.setWidget(holder)
        layout.addWidget(scroll, 1)

        # vibration ------------------------------------------------------
        layout.addWidget(section_label("vibration", (
            "Rumble strength for both motors, 0 to 100 percent.\n\n"
            "The controller stores this as a single byte per motor, so the "
            "percentage is converted on the way in. Zero silences rumble "
            "entirely, which the pad's own button combinations cannot do.\n\n"
            "This is one of the few settings with no on-pad equivalent, so "
            "software is the only way to reach it.")))
        self.vibration = VibrationRow()
        layout.addWidget(self.vibration)

        # footer ---------------------------------------------------------
        layout.addWidget(divider())
        footer = QHBoxLayout()
        footer.setSpacing(10)
        self.status = QLabel("")
        self.status.setObjectName("Muted")
        self.save_button = QPushButton("Save")
        self.save_button.setObjectName("Ghost")
        self.save_button.clicked.connect(self.save_profile)
        self.apply_button = QPushButton("Apply to controller")
        self.apply_button.setObjectName("Primary")
        self.apply_button.clicked.connect(self.apply_profile)
        footer.addWidget(self.status, 1)
        footer.addWidget(self.save_button)
        footer.addWidget(self.apply_button)
        layout.addLayout(footer)
        return page

    # -- profiles --------------------------------------------------------

    def reload_profiles(self, select: str | None = None) -> None:
        self.profile_list.blockSignals(True)
        self.profile_list.clear()
        profiles = self.store.list()
        for profile in profiles:
            item = QListWidgetItem(profile.name)
            item.setData(Qt.UserRole, profile.name)
            self.profile_list.addItem(item)
        self.profile_list.blockSignals(False)

        target = select or (profiles[0].name if profiles else None)
        if target:
            for index in range(self.profile_list.count()):
                if self.profile_list.item(index).data(Qt.UserRole) == target:
                    self.profile_list.setCurrentRow(index)
                    return
        else:
            self.load_into_form(Profile(name="Save file 1"))

    def _profile_selected(self, current, _previous) -> None:
        if current is None:
            return
        # Persist edits to the profile being left, so switching away never
        # silently discards a recording or a tweak.
        if self._loaded_name and self.has_unsaved_changes():
            self.autosave()

        # Picking a save file means you want to edit it, so leave the tester.
        self.show_tester(False)

        name = current.data(Qt.UserRole) or current.text()
        try:
            self.load_into_form(self.store.load(name))
        except (FileNotFoundError, ValueError) as exc:
            # A profile deleted or corrupted outside the app should not take the
            # window down; show the empty form and say so.
            self.load_into_form(Profile(name=name))
            self.set_status(f"could not load “{name}”: {exc}", "Danger")

    def load_into_form(self, profile: Profile) -> None:
        self.profile = profile
        self._loaded_name = profile.name
        for slot in SLOTS:
            self.cards[slot].set_spec(profile.macros.get(slot))
        self.vibration.set_value(profile.vibration or 0)
        self._sync_apply_state()

    def collect(self) -> Profile:
        """The form's current contents as a Profile.

        Carries clear_undefined through from the loaded profile; dropping it
        would silently reset the setting every time the form was read.
        """
        profile = Profile(name=self.profile.name,
                          clear_undefined=self.profile.clear_undefined)
        for slot in SLOTS:
            profile.macros[slot] = self.cards[slot].spec()
        profile.vibration = self.vibration.value()
        return profile

    def has_unsaved_changes(self) -> bool:
        try:
            current = self.collect()
        except ValueError:
            return True     # an invalid edit is still an edit
        stored = self.profile
        return (current.macros != stored.macros
                or current.vibration != stored.vibration)

    def autosave(self, reason: str = "") -> bool:
        """Persist the form if it is valid. Returns whether it saved.

        Refuses to recreate a profile whose file has gone: if it was removed,
        here or elsewhere, silently writing it back would be the wrong answer.
        """
        if self._loaded_name is None:
            return False
        try:
            self.store.load(self._loaded_name)
        except FileNotFoundError:
            self._loaded_name = None
            return False
        except ValueError:
            pass        # corrupt on disk; overwriting it with valid data is fine

        try:
            profile = self.collect()
            self.store.save(profile)
        except ValueError as exc:
            self.set_status(f"not saved: {exc}", "Danger")
            return False
        self.profile = profile
        if reason:
            self.set_status(reason, "Success")
        return True

    def new_profile(self) -> None:
        name, accepted = QInputDialog.getText(
            self, "New save file", "Name:",
            text=f"Save file {self.profile_list.count() + 1}")
        if not accepted or not name.strip():
            return
        profile = Profile(name=name.strip())
        self.store.save(profile)
        self.reload_profiles(select=profile.name)

    def rename_profile(self) -> None:
        item = self.profile_list.currentItem()
        if item is None:
            return
        old = item.data(Qt.UserRole) or item.text()
        name, accepted = QInputDialog.getText(self, "Rename save file", "Name:",
                                              text=old)
        name = name.strip()
        if not accepted or not name or name == old:
            return
        try:
            profile = self.store.load(old)
        except (FileNotFoundError, ValueError) as exc:
            self.set_status(f"could not rename: {exc}", "Danger")
            return
        profile.name = name
        self.store.save(profile)
        self.store.delete(old)          # remove the file under the old slug
        self.reload_profiles(select=name)
        self.set_status(f"renamed to “{name}”", "Success")

    def toggle_tester(self) -> None:
        self.show_tester(self.tester_button.isChecked())

    def show_tester(self, showing: bool) -> None:
        """Switch between the settings page and the tester.

        The tester polls every millisecond, so it is stopped whenever it is not
        the visible page.
        """
        self.pages.setCurrentIndex(1 if showing else 0)
        self.tester_button.setChecked(showing)
        self.tester_button.setText("Back to settings" if showing
                                   else "Input tester")
        if showing:
            self.tester.start()
        else:
            self.tester.stop()

    def delete_profile(self) -> None:
        item = self.profile_list.currentItem()
        if item is None:
            return
        name = item.data(Qt.UserRole)
        confirm = QMessageBox.question(
            self, "Delete save file",
            f"Delete “{name}”?\n\nThis removes the file from disk. "
            "It does not change anything on the controller.")
        if confirm == QMessageBox.Yes:
            self.store.delete(name)
            # Forget what is loaded before reloading. Otherwise selecting the
            # next profile triggers an autosave of the one just deleted, which
            # writes the file straight back.
            self._loaded_name = None
            self.profile = Profile(name=name)
            self.reload_profiles()
            self.set_status(f"removed “{name}”", "Muted")

    def save_profile(self) -> None:
        try:
            profile = self.collect()
            self.store.save(profile)
        except ValueError as exc:
            self.set_status(f"cannot save: {exc}", "Danger")
            return
        self.profile = profile
        self.set_status(f"saved “{profile.name}”", "Success")

    # -- controller ------------------------------------------------------

    def set_status(self, text: str, kind: str = "Muted") -> None:
        self.status.setText(text)
        self.status.setObjectName(kind)
        self.status.style().unpolish(self.status)
        self.status.style().polish(self.status)

    def _sync_apply_state(self) -> None:
        valid = all(card.is_valid() for card in self.cards.values())
        self.apply_button.setEnabled(valid and self.pad is not None and not self._busy)
        self.save_button.setEnabled(valid and not self._busy)

    def connect_controller(self) -> None:
        if self._busy:
            return
        self._busy = True
        self.dot.set_state("connecting")
        self.device_name.setText("Connecting…")
        self.device_detail.setText(self.address or "scanning for the controller")
        self.connect_button.setEnabled(False)
        self._sync_apply_state()

        async def task():
            address = self.address or await find_controller()
            if not address:
                raise RuntimeError("no controller found; is it on and paired?")
            pad = X20(address)
            await pad.connect()
            snapshot = await pad.snapshot()
            return address, pad, snapshot

        self.bridge.run(task, on_done=self._on_connected, on_error=self._on_connect_failed)

    def _on_connected(self, result) -> None:
        address, pad, snapshot = result
        self.address = address
        save_address(address)
        self.pad = pad
        self._busy = False

        self.dot.set_state("connected")
        self.device_name.setText(snapshot.name or "Controller")
        self.device_detail.setText(
            f"firmware {snapshot.device.version}   ·   {address}")
        self.connect_button.setText("Reconnect")
        self.connect_button.setEnabled(True)
        self.vibration.set_value(snapshot.vibration[0])

        if snapshot.battery is not None:
            battery = snapshot.battery
            icon = "⚡" if battery.charging else "🔋"
            self.battery_label.setText(f"{icon} {battery.level}/4")
            self.battery_label.setObjectName(
                "Danger" if battery.level == 1 and not battery.charging else "Muted")
            self.battery_label.style().unpolish(self.battery_label)
            self.battery_label.style().polish(self.battery_label)
        else:
            self.battery_label.setText("")

        caps = snapshot.capabilities
        missing = [n for n, v in (("lighting", caps.lighting),
                                  ("turbo", caps.turbo),
                                  ("gyro", caps.sensor)) if not v]
        if missing:
            self.notice.setText(
                "This controller does not expose " + ", ".join(missing) +
                " to the configuration protocol, so they are not shown. "
                "They are controlled by button combinations on the pad itself.")
            self.notice.show()
        self.refresh_link()
        self.set_status("connected", "Success")
        self._sync_apply_state()

    def refresh_link(self) -> None:
        """Show how the pad is attached for play, which is separate from the
        configuration link this app uses."""
        connection = transport.detect()
        if connection.link is transport.Link.ABSENT:
            self.link_label.setText("")
            return
        self.link_label.setText(f"playing over {connection.link.value}")

    def _on_connect_failed(self, message: str) -> None:
        self._busy = False
        self.pad = None
        self.dot.set_state("error")
        self.device_name.setText("Not connected")
        self.device_detail.setText(message)
        self.connect_button.setText("Connect")
        self.connect_button.setEnabled(True)
        self.set_status(message, "Danger")
        self._sync_apply_state()

    def apply_profile(self) -> None:
        if self.pad is None or self._busy:
            return
        try:
            profile = self.collect()
            profile.validate()
        except ValueError as exc:
            self.set_status(f"cannot apply: {exc}", "Danger")
            return

        # Save before writing, so the file on disk always matches what the
        # controller was last given.
        self.store.save(profile)
        self.profile = profile

        self._busy = True
        self._sync_apply_state()
        self.set_status("applying…")
        pad = self.pad

        async def task():
            return await profile.apply(pad)

        self.bridge.run(task, on_done=self._on_applied, on_error=self._on_apply_failed)

    def _on_applied(self, report: list[str]) -> None:
        self._busy = False
        looping = [s for s in SLOTS
                   if (spec := self.cards[s].spec()) and spec.loop_ms]
        written = sum(1 for line in report if "left as it was" not in line)
        name = self.profile.name
        if looping:
            self.set_status(
                f"“{name}” applied · {', '.join(looping)} will repeat until "
                "interrupted", "Warning")
        else:
            self.set_status(
                f"“{name}” applied · the controller now matches this save file "
                f"({written} settings written)", "Success")
        self._sync_apply_state()

    def _on_apply_failed(self, message: str) -> None:
        self._busy = False
        self.set_status(f"apply failed: {message}", "Danger")
        self._sync_apply_state()

    # -- recording -------------------------------------------------------

    def toggle_recording(self, slot: str) -> None:
        if self.recording_slot == slot:
            self.finish_recording()
            return
        if self.recording_slot is not None:
            self.finish_recording()

        if not self.reader.available:
            self.set_status("XInput is unavailable, so recording cannot run",
                            "Danger")
            return
        if self.reader.poll() is None:
            self.set_status(
                "no controller on XInput. The pad must also be connected as a "
                "gamepad, not only over the configuration link.", "Danger")
            return

        self.recording_slot = slot
        self.recorder.start()
        self.cards[slot].set_recording(True)
        self.set_status(f"recording {slot} · press buttons, then Stop", "Warning")
        self.record_timer.start()

    def _record_tick(self) -> None:
        self.recorder.poll()
        if self.recording_slot:
            self.cards[self.recording_slot].show_recording_progress(
                self.recorder.summary())

    def finish_recording(self) -> None:
        if self.recording_slot is None:
            return
        self.record_timer.stop()
        slot = self.recording_slot
        self.recording_slot = None

        spec = self.recorder.stop()
        card = self.cards[slot]
        card.set_recording(False)

        if spec is None:
            self.set_status(f"nothing recorded for {slot}", "Muted")
            return
        card.set_spec(spec)

        # Save immediately. A recording is work the user just performed by hand
        # and should not be lost by clicking somewhere else.
        saved = self.autosave()

        message = f"{slot} recorded: {spec.describe()}"
        if saved:
            message += f" · saved to “{self.profile.name}”"
        if self.recorder.ignored:
            message += (f" · ignored {', '.join(sorted(self.recorder.ignored))}, "
                        "which this pad cannot put in a macro")
            self.set_status(message, "Warning")
        else:
            self.set_status(message, "Success" if saved else "Danger")

    # -- lifecycle -------------------------------------------------------

    def closeEvent(self, event) -> None:
        self.record_timer.stop()
        self.tester.stop()
        pad = self.pad
        if pad is not None:
            self.bridge.run(lambda: pad.disconnect())
        self.bridge.shutdown()
        super().closeEvent(event)
