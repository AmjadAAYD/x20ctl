"""The macro editor: draw a sequence, one column at a time.

Four slots, each independent, the way the buttons on the back of the pad are
independent. Click a cell to hold that input for that step. Leave a column
empty and it is a gap. The numbers along the top are how long each column
lasts, in the controller's own 5 ms units.

Nothing here talks to a controller. The page says what it wants and something
else decides what a failed write looks like.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QCheckBox, QGridLayout, QHBoxLayout, QLabel, QPushButton, QScrollArea,
    QSpinBox, QVBoxLayout, QWidget,
)

from .macrogrid import (
    DEFAULT_GAP_MS, DEFAULT_STEP_MS, ROWS, STEP_GRID_MS, MacroGrid,
    TooManySteps,
)

SLOTS = ("M1", "M2", "M3", "M4")
STARTING_COLUMNS = 10
APPLY_FEEDBACK_PERCENT = 30


class MacroEditor(QWidget):
    """One grid per slot, plus the things you do to a macro."""

    apply_requested = Signal(str, object)        # slot, MacroGrid
    read_requested = Signal(str)                 # slot
    record_requested = Signal(str)               # slot

    def __init__(self, rumbler=None) -> None:
        super().__init__()
        self.rumbler = rumbler
        self.grids = {slot: MacroGrid() for slot in SLOTS}
        self.slot = SLOTS[0]
        self.cells: dict[tuple[int, int], QPushButton] = {}
        self.durations: list[QSpinBox] = []

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(6)

        title = QLabel("Macros")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        self.blurb = QLabel(
            "Each of the four buttons on the back can replay a sequence. "
            "Click a cell to hold that input for that step. A column with "
            "nothing in it is a pause. The number above each column is how "
            "long it lasts.")
        self.blurb.setObjectName("PageSubtitle")
        self.blurb.setWordWrap(True)
        root.addWidget(self.blurb)
        root.addSpacing(12)

        tabs = QHBoxLayout()
        tabs.setSpacing(8)
        self.tabs: dict[str, QPushButton] = {}
        for slot in SLOTS:
            button = QPushButton(slot)
            button.setObjectName("Ghost")
            button.setCheckable(True)
            button.setChecked(slot == self.slot)
            button.setCursor(Qt.PointingHandCursor)
            button.setToolTip(f"The sequence stored on {slot}")
            button.clicked.connect(lambda _=False, s=slot: self.show_slot(s))
            self.tabs[slot] = button
            tabs.addWidget(button)
        tabs.addStretch(1)
        root.addLayout(tabs)
        root.addSpacing(8)

        area = QScrollArea()
        area.setWidgetResizable(True)
        area.setFrameShape(QScrollArea.NoFrame)
        holder = QWidget()
        self.grid = QGridLayout(holder)
        self.grid.setHorizontalSpacing(4)
        self.grid.setVerticalSpacing(4)
        area.setWidget(holder)
        root.addWidget(area, 1)

        controls = QHBoxLayout()
        controls.setSpacing(10)

        self.loop_on = QCheckBox("Repeat")
        self.loop_on.setToolTip(
            "Replay the sequence until you press a different macro button")
        self.loop_on.toggled.connect(self._on_loop)
        controls.addWidget(self.loop_on)

        self.loop_ms = QSpinBox()
        self.loop_ms.setRange(STEP_GRID_MS, 5000)
        self.loop_ms.setSingleStep(STEP_GRID_MS)
        self.loop_ms.setSuffix(" ms between runs")
        self.loop_ms.setEnabled(False)
        self.loop_ms.valueChanged.connect(self._on_loop_ms)
        controls.addWidget(self.loop_ms)
        controls.addStretch(1)

        self.summary = QLabel()
        self.summary.setObjectName("RowDetail")
        controls.addWidget(self.summary)
        root.addLayout(controls)

        buttons = QHBoxLayout()
        buttons.setSpacing(10)
        for label, slot_fn, tip in (
                ("Add step", self.add_column, "Another column at the end"),
                ("Clear", self.clear, "Empty this slot"),
                ("Record", self._record,
                 "Play the sequence on the controller and have it written down"),
                ("Read from controller", self._read,
                 "Load what this slot already holds"),
        ):
            button = QPushButton(label)
            button.setObjectName("Ghost")
            button.setCursor(Qt.PointingHandCursor)
            button.setToolTip(tip)
            button.clicked.connect(slot_fn)
            buttons.addWidget(button)
        buttons.addStretch(1)

        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        buttons.addWidget(self.status)

        self.apply_button = QPushButton("Apply")
        self.apply_button.setCursor(Qt.PointingHandCursor)
        self.apply_button.clicked.connect(self.apply)
        buttons.addWidget(self.apply_button)
        root.addLayout(buttons)

        self.show_slot(self.slot)

    # -- slots -----------------------------------------------------------

    @property
    def current(self) -> MacroGrid:
        return self.grids[self.slot]

    def show_slot(self, slot: str) -> None:
        self.slot = slot
        for name, button in self.tabs.items():
            button.setChecked(name == slot)
        self.current.ensure(STARTING_COLUMNS)
        self.rebuild()

    def load(self, slot: str, grid: MacroGrid) -> None:
        """Put a macro read off the pad into a slot."""
        grid.ensure(STARTING_COLUMNS)
        self.grids[slot] = grid
        if slot == self.slot:
            self.rebuild()

    # -- drawing ---------------------------------------------------------

    def rebuild(self) -> None:
        while self.grid.count():
            item = self.grid.takeAt(0)
            if item.widget():
                item.widget().deleteLater()
        self.cells.clear()
        self.durations.clear()

        grid = self.current
        columns = len(grid)

        for column in range(columns):
            box = QSpinBox()
            box.setRange(STEP_GRID_MS, 10_000)
            box.setSingleStep(STEP_GRID_MS)
            box.setValue(grid.steps[column].duration_ms)
            box.setFixedWidth(74)
            box.setToolTip("How long this column lasts, in milliseconds")
            box.valueChanged.connect(
                lambda value, c=column: self._on_duration(c, value))
            self.grid.addWidget(box, 0, column + 1)
            self.durations.append(box)

        for row, (key, label) in enumerate(ROWS, start=1):
            name = QLabel(label)
            name.setObjectName("RowDetail")
            name.setFixedWidth(96)
            self.grid.addWidget(name, row, 0)

            for column in range(columns):
                cell = QPushButton()
                cell.setObjectName("Cell")
                cell.setCheckable(True)
                cell.setFixedSize(72, 24)
                cell.setCursor(Qt.PointingHandCursor)
                cell.setChecked(key in grid.steps[column].keys)
                cell.clicked.connect(
                    lambda _=False, c=column, k=key: self._on_toggle(c, k))
                self.grid.addWidget(cell, row, column + 1)
                self.cells[(column, row - 1)] = cell

        self.loop_on.blockSignals(True)
        self.loop_ms.blockSignals(True)
        self.loop_on.setChecked(bool(grid.loop_ms))
        self.loop_ms.setEnabled(bool(grid.loop_ms))
        if grid.loop_ms:
            self.loop_ms.setValue(grid.loop_ms)
        self.loop_on.blockSignals(False)
        self.loop_ms.blockSignals(False)
        self.refresh_summary()

    def refresh_summary(self) -> None:
        grid = self.current
        presses = sum(1 for step in grid.steps if not step.empty)
        if not presses:
            self.summary.setText("empty")
            return
        seconds = grid.total_ms() / 1000
        self.summary.setText(
            f"{presses} press{'' if presses == 1 else 'es'}, {seconds:.2f}s")

    # -- editing ---------------------------------------------------------

    def add_column(self) -> None:
        self.current.add_step(DEFAULT_STEP_MS)
        self.rebuild()

    def clear(self) -> None:
        self.current.clear()
        self.current.ensure(STARTING_COLUMNS)
        self.rebuild()
        self.status.setText("")

    def _on_toggle(self, column: int, key: str) -> None:
        self.current.toggle(column, key)
        self.refresh_summary()
        self.status.setText("")

    def _on_duration(self, column: int, value: int) -> None:
        snapped = self.current.set_duration(column, value)
        if snapped != value and column < len(self.durations):
            box = self.durations[column]
            box.blockSignals(True)
            box.setValue(snapped)
            box.blockSignals(False)
        self.refresh_summary()

    def _on_loop(self, on: bool) -> None:
        self.loop_ms.setEnabled(on)
        self.current.loop_ms = self.loop_ms.value() if on else 0

    def _on_loop_ms(self, value: int) -> None:
        if self.loop_on.isChecked():
            self.current.loop_ms = value

    # -- actions ---------------------------------------------------------

    def _record(self) -> None:
        self.record_requested.emit(self.slot)

    def _read(self) -> None:
        self.read_requested.emit(self.slot)

    def apply(self) -> bool:
        """Hand the grid over, if it is something the pad can hold."""
        try:
            self.current.to_steps()
        except TooManySteps as exc:
            self.status.setText(str(exc))
            return False
        except ValueError:
            self.status.setText("Nothing to apply: this slot has no presses.")
            return False

        self.apply_requested.emit(self.slot, self.current)
        self.status.setText(f"Sent to {self.slot}.")
        if self.rumbler is not None:
            self.rumbler.pulse(APPLY_FEEDBACK_PERCENT)
        self.rebuild()
        return True
