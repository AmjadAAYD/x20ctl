"""The workspace header: what this controller is, and the things you do rarely.

Share, help, about, updates and factory reset live here rather than in the
sidebar, because none of them is a setting. The rail is for changing the
controller; this row is for everything around it.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QHBoxLayout, QLabel, QMessageBox, QPushButton, QWidget,
)

from .. import __version__
from .updates import RELEASES_PAGE, check

SHARE_TEXT = (
    "x20ctl configures EasySMX controllers over Bluetooth: buttons, sticks, "
    "triggers, vibration and macros.\n\n" + RELEASES_PAGE
)


class HeaderBar(QWidget):
    """Controller name on the left, the occasional actions on the right."""

    back = Signal()
    factory_reset = Signal()
    share = Signal(str)

    def __init__(self, *, fetch=None) -> None:
        super().__init__()
        self._fetch = fetch

        row = QHBoxLayout(self)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(10)

        self.back_button = QPushButton("Controllers")
        self.back_button.setObjectName("Ghost")
        self.back_button.setCursor(Qt.PointingHandCursor)
        self.back_button.clicked.connect(self.back.emit)
        row.addWidget(self.back_button)

        self.title = QLabel()
        self.title.setObjectName("PageTitle")
        row.addWidget(self.title)
        row.addStretch(1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        row.addWidget(self.status)

        for label, slot, tip in (
                ("Updates", self.check_updates,
                 "Ask GitHub whether a newer version exists"),
                ("Share", self.share_app, "Copy a short description and the link"),
                ("Help", self.show_help, "What each section does"),
                ("About", self.show_about, "Version and project links"),
        ):
            button = QPushButton(label)
            button.setObjectName("Ghost")
            button.setCursor(Qt.PointingHandCursor)
            button.setToolTip(tip)
            button.clicked.connect(slot)
            row.addWidget(button)

        self.reset_button = QPushButton("Factory reset")
        self.reset_button.setObjectName("Danger")
        self.reset_button.setCursor(Qt.PointingHandCursor)
        self.reset_button.setToolTip(
            "Clear every stored setting on the controller")
        self.reset_button.clicked.connect(self.confirm_reset)
        row.addWidget(self.reset_button)

    def show_slot(self, slot) -> None:
        self.title.setText(slot.label if slot is not None else "")

    # -- actions ---------------------------------------------------------

    def check_updates(self) -> None:
        self.status.setText("Checking...")
        message, release = (check(__version__, self._fetch) if self._fetch
                            else check(__version__))
        self.status.setText(message)
        if release is not None:
            self.status.setToolTip(release.notes or release.url)

    def share_app(self) -> str:
        """Hand the description to the clipboard, and to anyone listening."""
        from PySide6.QtWidgets import QApplication
        clipboard = QApplication.clipboard()
        if clipboard is not None:
            clipboard.setText(SHARE_TEXT)
        self.status.setText("Copied to the clipboard.")
        self.share.emit(SHARE_TEXT)
        return SHARE_TEXT

    def show_help(self) -> QMessageBox:
        box = QMessageBox(self)
        box.setWindowTitle("Help")
        box.setText("What each section does")
        box.setInformativeText(
            "Buttons — make one button do another button's job.\n"
            "Sticks — deadzones and how quickly a stick answers.\n"
            "Triggers — how far a pull travels, and how it ramps.\n"
            "Vibration — how hard the motors may work. Saves itself.\n"
            "Macros — record or draw a sequence onto M1 to M4.\n"
            "Test — press everything and watch it light up.\n\n"
            "Advanced mode adds the power timeout and the device page, which "
            "holds calibration and factory reset.")
        box.setStandardButtons(QMessageBox.Ok)
        return box

    def show_about(self) -> QMessageBox:
        box = QMessageBox(self)
        box.setWindowTitle("About")
        box.setText(f"x20ctl {__version__}")
        box.setInformativeText(
            "Configures EasySMX controllers over their Bluetooth "
            "configuration link.\n\n"
            f"{RELEASES_PAGE}")
        box.setStandardButtons(QMessageBox.Ok)
        return box

    def confirm_reset(self) -> QMessageBox:
        """Ask before wiping, and say exactly what goes."""
        box = QMessageBox(self)
        box.setWindowTitle("Factory reset")
        box.setIcon(QMessageBox.Warning)
        box.setText("Clear every stored setting on this controller?")
        box.setInformativeText(
            "Macros, button remapping, stick and trigger settings and the "
            "power timeout all go back to how the controller shipped.\n\n"
            "Firmware is not touched. This cannot be undone.")
        box.setStandardButtons(QMessageBox.Cancel | QMessageBox.Yes)
        box.setDefaultButton(QMessageBox.Cancel)
        box.accepted.connect(self.factory_reset.emit)
        return box
