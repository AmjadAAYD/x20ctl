"""The main window."""

from __future__ import annotations

from PySide6.QtCore import Qt, QTimer
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QInputDialog, QLabel, QListWidget, QListWidgetItem,
    QMessageBox, QPushButton, QScrollArea, QVBoxLayout, QWidget,
)

from ..cli import load_address, save_address
from ..client import X20, find_controller
from ..profiles import MacroSpec, Profile, ProfileStore, SLOTS
from . import theme
from .bridge import AsyncBridge
from .widgets import MacroCard, StatusDot, VibrationRow, divider, section_label


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
        self._busy = False

        root = QHBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(0)
        root.addWidget(self._build_sidebar())
        root.addWidget(self._build_main(), 1)

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
        holder_layout.addWidget(section_label("save files"))
        layout.addWidget(holder)

        self.profile_list = QListWidget()
        self.profile_list.currentItemChanged.connect(self._profile_selected)
        layout.addWidget(self.profile_list, 1)

        buttons = QWidget()
        row = QHBoxLayout(buttons)
        row.setContentsMargins(16, 0, 16, 0)
        row.setSpacing(8)
        new_button = QPushButton("New")
        new_button.setObjectName("Ghost")
        new_button.clicked.connect(self.new_profile)
        delete_button = QPushButton("Delete")
        delete_button.setObjectName("Danger")
        delete_button.clicked.connect(self.delete_profile)
        row.addWidget(new_button)
        row.addWidget(delete_button)
        layout.addWidget(buttons)
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
        layout.addWidget(section_label("macros"))
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
            self.cards[slot] = card
            cards.addWidget(card)
        cards.addStretch(1)
        scroll.setWidget(holder)
        layout.addWidget(scroll, 1)

        # vibration ------------------------------------------------------
        layout.addWidget(section_label("vibration"))
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
        for slot in SLOTS:
            self.cards[slot].set_spec(profile.macros.get(slot))
        self.vibration.set_value(profile.vibration or 0)
        self._sync_apply_state()

    def collect(self) -> Profile:
        profile = Profile(name=self.profile.name)
        for slot in SLOTS:
            profile.macros[slot] = self.cards[slot].spec()
        profile.vibration = self.vibration.value()
        return profile

    def new_profile(self) -> None:
        name, accepted = QInputDialog.getText(
            self, "New save file", "Name:",
            text=f"Save file {self.profile_list.count() + 1}")
        if not accepted or not name.strip():
            return
        profile = Profile(name=name.strip())
        self.store.save(profile)
        self.reload_profiles(select=profile.name)

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
            self.reload_profiles()

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
        self.set_status("connected", "Success")
        self._sync_apply_state()

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
        if looping:
            self.set_status(
                f"applied · {', '.join(looping)} will repeat until interrupted",
                "Warning")
        else:
            self.set_status(f"applied {len(report)} settings", "Success")
        self._sync_apply_state()

    def _on_apply_failed(self, message: str) -> None:
        self._busy = False
        self.set_status(f"apply failed: {message}", "Danger")
        self._sync_apply_state()

    # -- lifecycle -------------------------------------------------------

    def closeEvent(self, event) -> None:
        pad = self.pad
        if pad is not None:
            self.bridge.run(lambda: pad.disconnect())
        self.bridge.shutdown()
        super().closeEvent(event)
