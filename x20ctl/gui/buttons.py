"""The buttons page: make one button do another button's job.

The pad decides which buttons can be sources; it reports that list and this
page shows exactly those. Targets are wider than sources, which is the part
worth knowing: C and T can be placed onto a button even though neither can be
remapped away, and Select and Start are listed as sources but ignore the write.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QComboBox, QGridLayout, QHBoxLayout, QLabel, QPushButton, QScrollArea,
    QVBoxLayout, QWidget,
)

from .. import protocol as p

UNCHANGED = "unchanged"

# Buttons that can receive a mapping but never send one. The pad never lists
# them as sources, and nothing in the record format says a target has to be one.
EXTRA_TARGETS = (p.Key.CAPTURE, p.Key.TURBO)

# Paired across the page the way they are paired on the controller: the two
# triggers on one line, the two bumpers on the next, the two sticks after that,
# then the d-pad against the face buttons.
PAIRS = (
    (p.Key.LT, p.Key.RT),
    (p.Key.LB, p.Key.RB),
    (p.Key.L3, p.Key.R3),
    (p.Key.DPAD_UP, p.Key.X),
    (p.Key.DPAD_DOWN, p.Key.Y),
    (p.Key.DPAD_LEFT, p.Key.A),
    (p.Key.DPAD_RIGHT, p.Key.B),
)

LEFT_HAND = tuple(int(left) for left, _ in PAIRS)

FRIENDLY = {
    "DPAD_UP": "D-pad up",
    "DPAD_DOWN": "D-pad down",
    "DPAD_LEFT": "D-pad left",
    "DPAD_RIGHT": "D-pad right",
    "LB": "LB", "RB": "RB", "LT": "LT", "RT": "RT",
    "L3": "L3", "R3": "R3",
    "SELECT": "Select", "START": "Start",
    "CAPTURE": "C", "TURBO": "T",
}


def label_for(code: int) -> str:
    """A human name for a key code."""
    try:
        name = p.Key(code).name
    except ValueError:
        return f"0x{code:02x}"
    return FRIENDLY.get(name, name)


class ButtonsPage(QWidget):
    """One row per remappable button, each with what it should do instead."""

    # object, not dict: Qt cannot marshal a Python dict through a typed signal
    changed = Signal(object)        # {source: target}, as it is edited
    save_requested = Signal(object)  # the same, when Save is pressed

    def __init__(self) -> None:
        super().__init__()
        self.sources: list[int] = []
        self.boxes: dict[int, QComboBox] = {}

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(6)

        title = QLabel("Buttons")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        self.blurb = QLabel(
            "Pick what each button should do instead. Leave one on "
            "“unchanged” and it keeps its own job. C and T can be "
            "placed onto a button even though they cannot be remapped away.")
        self.blurb.setObjectName("PageSubtitle")
        self.blurb.setWordWrap(True)
        root.addWidget(self.blurb)
        root.addSpacing(14)

        area = QScrollArea()
        area.setWidgetResizable(True)
        area.setFrameShape(QScrollArea.NoFrame)
        holder = QWidget()
        self.grid = QGridLayout(holder)
        self.grid.setContentsMargins(0, 0, 8, 0)
        self.grid.setHorizontalSpacing(16)
        self.grid.setVerticalSpacing(8)
        area.setWidget(holder)
        root.addWidget(area, 1)

        footer = QHBoxLayout()
        footer.setSpacing(10)
        self.reset_button = QPushButton("Clear all remapping")
        self.reset_button.setObjectName("Ghost")
        self.reset_button.setCursor(Qt.PointingHandCursor)
        self.reset_button.clicked.connect(self.clear)
        footer.addWidget(self.reset_button)
        footer.addStretch(1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        footer.addWidget(self.status)

        self.save_button = QPushButton("Save to controller")
        self.save_button.setCursor(Qt.PointingHandCursor)
        self.save_button.clicked.connect(self.save)
        footer.addWidget(self.save_button)
        root.addLayout(footer)

    def load(self, sources, mapping: dict | None = None) -> None:
        """Draw a row per source the pad reports, showing its current target."""
        mapping = mapping or {}
        self.sources = [code for code in sources
                        if code not in p.CHANGEKEY_TARGET_ONLY]
        self.boxes.clear()

        while self.grid.count():
            item = self.grid.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        targets = list(sources) + [int(k) for k in EXTRA_TARGETS]

        # Laid out the way the pad is: everything your left hand reaches on the
        # left, everything your right hand reaches on the right. Scanning for a
        # button then matches looking down at the controller.
        placed: set = set()
        ordered = []
        for row, (left, right) in enumerate(PAIRS):
            for side, key in ((0, int(left)), (2, int(right))):
                if key in self.sources:
                    ordered.append((key, side, row))
                    placed.add(key)

        # Anything the pad reports that this layout has no seat for still gets
        # a row, rather than vanishing because a table here is out of date.
        spare = [code for code in self.sources if code not in placed]
        for index, code in enumerate(spare):
            ordered.append((code, index % 2 * 2, len(PAIRS) + index // 2))

        for code, side, row in ordered:

            name = QLabel(label_for(code))
            name.setObjectName("RowTitle")
            name.setFixedWidth(96)
            self.grid.addWidget(name, row, side)

            box = QComboBox()
            box.addItem("unchanged", UNCHANGED)
            for target in targets:
                box.addItem(label_for(target), target)
            chosen = mapping.get(code)
            box.setCurrentIndex(box.findData(chosen) if chosen else 0)
            box.currentIndexChanged.connect(self._emit)
            self.grid.addWidget(box, row, side + 1)
            self.boxes[code] = box

        self.grid.setColumnStretch(1, 1)
        self.grid.setColumnStretch(3, 1)
        self.grid.setColumnMinimumWidth(2, 24)

    def mapping(self) -> dict:
        """Only the buttons actually pointed somewhere else."""
        out = {}
        for code, box in self.boxes.items():
            target = box.currentData()
            if target != UNCHANGED and target != code:
                out[code] = target
        return out

    def clear(self) -> None:
        for box in self.boxes.values():
            box.blockSignals(True)
            box.setCurrentIndex(0)
            box.blockSignals(False)
        self._emit()

    def save(self) -> None:
        """Send what is on screen. Nothing reaches the pad before this."""
        mapping = self.mapping()
        self.status.setText("Saving..." if mapping else "Clearing...")
        self.save_requested.emit(mapping)

    def _emit(self) -> None:
        self.status.setText("Not saved yet")
        self.changed.emit(self.mapping())
