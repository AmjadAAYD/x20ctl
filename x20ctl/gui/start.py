"""The screen the app opens on: what is connected, and how to add more.

Deliberately not the macro editor. Somebody opening this for the first time
should see one obvious thing to do, and somebody with two pads on the desk
should see both and pick one.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QLabel, QPushButton, QSizePolicy, QVBoxLayout, QWidget,
)

from .. import __version__
from . import theme
from .roster import MAX_PLAYERS, Roster

REPO_URL = "https://github.com/AmjadAAYD/x20ctl"


class ControllerRow(QFrame):
    """One added controller: what it is, which player, and how to drop it."""

    opened = Signal(int)
    removed = Signal(int)

    def __init__(self, slot) -> None:
        super().__init__()
        self.setObjectName("ControllerRow")
        self.slot = slot
        self.setCursor(Qt.PointingHandCursor)

        layout = QHBoxLayout(self)
        layout.setContentsMargins(18, 14, 14, 14)
        layout.setSpacing(14)

        badge = QLabel(f"P{slot.player}")
        badge.setObjectName("PlayerBadge")
        badge.setAlignment(Qt.AlignCenter)
        badge.setFixedSize(44, 44)
        layout.addWidget(badge)

        stack = QVBoxLayout()
        stack.setSpacing(2)
        name = QLabel(slot.product or slot.name)
        name.setObjectName("RowTitle")
        stack.addWidget(name)
        detail = QLabel(slot.address)
        detail.setObjectName("RowDetail")
        stack.addWidget(detail)
        layout.addLayout(stack, 1)

        state = QLabel("connected" if slot.connected else "added")
        state.setObjectName("RowState" if slot.connected else "RowStateIdle")
        layout.addWidget(state)

        drop = QPushButton("Remove")
        drop.setObjectName("Ghost")
        drop.setCursor(Qt.PointingHandCursor)
        drop.clicked.connect(lambda: self.removed.emit(slot.player))
        layout.addWidget(drop)

    def mouseReleaseEvent(self, event) -> None:      # noqa: N802 (Qt naming)
        if event.button() == Qt.LeftButton:
            self.opened.emit(self.slot.player)
        super().mouseReleaseEvent(event)


class StartPage(QWidget):
    """Empty state, or the list of controllers you have added."""

    add_requested = Signal()
    opened = Signal(int)
    removed = Signal(int)

    def __init__(self) -> None:
        super().__init__()
        self.setObjectName("StartPage")

        root = QVBoxLayout(self)
        root.setContentsMargins(46, 40, 46, 28)
        root.setSpacing(0)

        title = QLabel("Controllers")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        self.subtitle = QLabel()
        self.subtitle.setObjectName("PageSubtitle")
        root.addWidget(self.subtitle)
        root.addSpacing(26)

        self.rows = QVBoxLayout()
        self.rows.setSpacing(10)
        root.addLayout(self.rows)

        self.empty = self._build_empty()
        root.addWidget(self.empty)

        self.add_button = QPushButton("+")
        self.add_button.setObjectName("AddTile")
        self.add_button.setCursor(Qt.PointingHandCursor)
        self.add_button.setFixedHeight(74)
        self.add_button.clicked.connect(self.add_requested.emit)
        root.addSpacing(12)
        root.addWidget(self.add_button)

        root.addStretch(1)
        root.addLayout(self._build_footer())

        self.show_roster(Roster())

    def _build_empty(self) -> QWidget:
        panel = QFrame()
        panel.setObjectName("EmptyPanel")
        panel.setSizePolicy(QSizePolicy.Preferred, QSizePolicy.Fixed)
        layout = QVBoxLayout(panel)
        layout.setContentsMargins(28, 34, 28, 34)
        layout.setSpacing(8)
        layout.setAlignment(Qt.AlignCenter)

        headline = QLabel("No controller is connected")
        headline.setObjectName("EmptyHeadline")
        headline.setAlignment(Qt.AlignCenter)
        layout.addWidget(headline)

        hint = QLabel("Turn a controller on, then add it below.\n"
                      f"You can add up to {MAX_PLAYERS}, one per player.")
        hint.setObjectName("EmptyHint")
        hint.setAlignment(Qt.AlignCenter)
        layout.addWidget(hint)
        return panel

    def _build_footer(self) -> QHBoxLayout:
        footer = QHBoxLayout()
        footer.setSpacing(10)

        version = QLabel(
            f'<a href="{REPO_URL}" style="color:{theme.EMBER};'
            f'text-decoration:none;">v{__version__}</a>')
        version.setObjectName("VersionLink")
        version.setOpenExternalLinks(True)
        version.setToolTip("Open the project on GitHub to check for updates")
        footer.addWidget(version)
        footer.addStretch(1)
        return footer

    def show_roster(self, roster: Roster) -> None:
        """Redraw for the current roster. Empty panel or rows, never both."""
        while self.rows.count():
            item = self.rows.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        self.empty.setVisible(not roster)
        for slot in roster.ordered():
            row = ControllerRow(slot)
            row.opened.connect(self.opened.emit)
            row.removed.connect(self.removed.emit)
            self.rows.addWidget(row)

        full = len(roster) >= MAX_PLAYERS
        self.add_button.setEnabled(not full)
        self.add_button.setText("+" if not full else f"{MAX_PLAYERS} is the limit")

        if not roster:
            self.subtitle.setText("Nothing added yet")
        elif full:
            self.subtitle.setText(f"{len(roster)} controllers, every player taken")
        else:
            free = ", ".join(f"P{n}" for n in roster.free())
            plural = "" if len(roster) == 1 else "s"
            self.subtitle.setText(f"{len(roster)} controller{plural}, {free} free")
