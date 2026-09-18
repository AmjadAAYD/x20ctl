"""The trigger page: how far a pull travels before it counts, and how it ramps.

Two independent triggers, each with a travel range and a response shape. The
range is the app's four zones, which are a pair of deadzone bytes. The shape is
a pair of curve control points. Both are presets over settings this library
already writes, so this page chooses rather than computes.

Our own names for the shapes. The values came off the hardware; the words are
ours.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QButtonGroup, QFrame, QHBoxLayout, QLabel, QProgressBar, QPushButton,
    QVBoxLayout, QWidget,
)

from .. import protocol as p
from .tester import TriggerBar

SIDES = ("left", "right")

# The travel zones, widest first in the sense of "counts soonest".
ZONES = (
    ("zero", "Full", "Counts the instant you touch it. No dead travel."),
    ("small", "Long", "A little slack at each end. The factory setting."),
    ("medium", "Short", "Counts sooner and saturates earlier."),
    ("large", "Hair", "The shortest usable pull. Least travel, fastest."),
)

# Our words for the response shapes, mapped onto the curve presets.
SHAPES = (
    ("default", "Straight", "Output follows the pull exactly."),
    ("quick", "Snap", "Answers early, then eases into the top."),
    ("slow", "Ease", "Answers early, then flattens off."),
    ("smooth", "Glide", "Lazy at first, with a late climb."),
    ("precise", "Precise", "Compressed, for small careful movements."),
)

# "precise" is our name for the preset stored as "fine".
SHAPE_TO_PRESET = {"precise": "fine"}


def preset_for(shape: str) -> str:
    """The protocol preset behind one of our shape names."""
    return SHAPE_TO_PRESET.get(shape, shape)


def shape_for(preset: str | None) -> str | None:
    """Our shape name for a protocol preset, or None if the curve is custom."""
    if preset is None:
        return None
    for shape, target in SHAPE_TO_PRESET.items():
        if target == preset:
            return shape
    return preset


class TravelMeter(QWidget):
    """Live position of one trigger, drawn by the tester's own bar.

    Literally the same widget the Test tab uses rather than a lookalike, so
    the two pages cannot drift apart.
    """

    def __init__(self, title: str) -> None:
        super().__init__()
        row = QHBoxLayout(self)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(0)

        self.bar = TriggerBar(title)
        row.addWidget(self.bar, 1)

        self.zone = QLabel()          # kept for the caller; shown in the panel
        self.zone.setObjectName("RowDetail")

    def set_position(self, percent: int) -> None:
        """The tester's bar speaks in 0-255, the way the pad reports it."""
        percent = max(0, min(100, percent))
        self._percent = percent
        self.bar.set_value(round(percent * 255 / 100))

    def value(self) -> int:
        return getattr(self, "_percent", 0)

    def set_zone(self, inner: int, outer: int) -> None:
        self.zone.setText(f"counts from {inner} to {outer}")


class ChoiceRow(QWidget):
    """A label and a row of mutually exclusive buttons."""

    chosen = Signal(str)

    def __init__(self, title: str, options) -> None:
        super().__init__()
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(6)

        caption = QLabel(title)
        caption.setObjectName("RailHeading")
        layout.addWidget(caption)

        row = QHBoxLayout()
        row.setSpacing(8)
        self.group = QButtonGroup(self)
        self.group.setExclusive(True)
        self.buttons: dict[str, QPushButton] = {}
        self.blurbs = {key: blurb for key, _, blurb in options}

        for key, label, blurb in options:
            button = QPushButton(label)
            button.setObjectName("Ghost")
            button.setCheckable(True)
            button.setCursor(Qt.PointingHandCursor)
            button.clicked.connect(lambda _=False, k=key: self.chosen.emit(k))
            self.group.addButton(button)
            self.buttons[key] = button
            row.addWidget(button)
        row.addStretch(1)
        layout.addLayout(row)

        # Written out rather than hidden behind a hover: a setting you cannot
        # see the meaning of is a setting nobody changes on purpose.
        self.explain = QLabel()
        self.explain.setObjectName("RowDetail")
        self.explain.setWordWrap(True)
        self.explain.setMinimumHeight(34)
        layout.addWidget(self.explain)
        self.chosen.connect(self.describe)

    def describe(self, key: str | None) -> None:
        self.explain.setText(self.blurbs.get(key, "") if key else
                             "This controller is holding something of its own, "
                             "matching none of these.")

    def select(self, key: str | None) -> None:
        """Check one option, or none of them if the pad holds something custom."""
        self.group.setExclusive(False)
        for name, button in self.buttons.items():
            button.setChecked(name == key)
        self.group.setExclusive(True)
        self.describe(key)

    def current(self) -> str | None:
        for name, button in self.buttons.items():
            if button.isChecked():
                return name
        return None


class TriggerSide(QWidget):
    """One trigger: its meter, its travel zone and its response shape."""

    zone_chosen = Signal(str, str)          # side, zone
    shape_chosen = Signal(str, str)         # side, shape

    def __init__(self, side: str) -> None:
        super().__init__()
        self.side = side

        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(14)

        panel = QFrame()
        panel.setObjectName("ControllerRow")
        inner = QVBoxLayout(panel)
        inner.setContentsMargins(16, 12, 16, 12)
        inner.setSpacing(8)

        heading = QLabel(f"{side.title()} trigger")
        heading.setObjectName("RowTitle")
        inner.addWidget(heading)

        self.meter = TravelMeter("LT" if side == "left" else "RT")
        inner.addWidget(self.meter)
        inner.addWidget(self.meter.zone)
        layout.addWidget(panel)

        self.zones = ChoiceRow("Travel", ZONES)
        self.zones.chosen.connect(lambda key: self.zone_chosen.emit(side, key))
        layout.addWidget(self.zones)

        self.shapes = ChoiceRow("Response", SHAPES)
        self.shapes.chosen.connect(lambda key: self.shape_chosen.emit(side, key))
        layout.addWidget(self.shapes)

    def load(self, curve) -> None:
        """Show what the pad holds for this trigger."""
        zone = p.gear_name(curve.inner_deadzone, curve.outer_raw)
        self.zones.select(zone)
        self.shapes.select(shape_for(p.preset_name(curve.point1, curve.point2)))
        self.meter.set_zone(curve.inner_deadzone, curve.outer_raw)


class TriggersPage(QWidget):
    """Both triggers, side by side."""

    zone_chosen = Signal(str, str)
    shape_chosen = Signal(str, str)
    swap_toggled = Signal(bool)
    save_requested = Signal()

    def __init__(self) -> None:
        super().__init__()
        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(6)

        title = QLabel("Triggers")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        blurb = QLabel(
            "Travel decides how far you pull before the trigger counts. "
            "Response decides how the pull maps to what the game receives. "
            "Pull a trigger to watch it move here.")
        blurb.setObjectName("PageSubtitle")
        blurb.setWordWrap(True)
        root.addWidget(blurb)
        root.addSpacing(16)

        columns = QHBoxLayout()
        columns.setSpacing(24)
        self.sides: dict[str, TriggerSide] = {}
        for side in SIDES:
            column = TriggerSide(side)
            column.zone_chosen.connect(self.zone_chosen.emit)
            column.shape_chosen.connect(self.shape_chosen.emit)
            self.sides[side] = column
            columns.addWidget(column, 1)
        root.addLayout(columns)

        # One flag for both channels: the app writes isTriggerExchange into the
        # flag byte of each side, never one alone. So this belongs to the page.
        root.addSpacing(14)
        self.swap = QPushButton("Swap L2 and R2")
        self.swap.setCheckable(True)
        self.swap.setCursor(Qt.PointingHandCursor)
        self.swap.clicked.connect(self._on_swap)
        root.addWidget(self.swap, 0, Qt.AlignLeft)

        swap_note = QLabel(
            "Pulling the left trigger sends the right one, and the other way "
            "round. The swap lives on the controller, so it applies in every "
            "game and stays after unplugging.")
        swap_note.setObjectName("RowDetail")
        swap_note.setWordWrap(True)
        root.addWidget(swap_note)

        root.addStretch(1)

        footer = QHBoxLayout()
        footer.setSpacing(10)
        footer.addStretch(1)
        self.status = QLabel()
        self.status.setObjectName("RowDetail")
        footer.addWidget(self.status)

        self.save_button = QPushButton("Save to controller")
        self.save_button.setCursor(Qt.PointingHandCursor)
        self.save_button.clicked.connect(self._save)
        footer.addWidget(self.save_button)
        root.addLayout(footer)

        self.zone_chosen.connect(lambda *_: self.status.setText("Not saved yet"))
        self.shape_chosen.connect(lambda *_: self.status.setText("Not saved yet"))

    def _on_swap(self) -> None:
        self.status.setText("Not saved yet")
        self.swap_toggled.emit(self.swap.isChecked())

    def _save(self) -> None:
        self.status.setText("Saving...")
        self.save_requested.emit()

    def load(self, curves) -> None:
        """Take the pad's two trigger channels, left first."""
        curves = list(curves)
        for side, curve in zip(SIDES, curves):
            self.sides[side].load(curve)
        if curves:
            # Read back from the pad rather than remembered, so the button
            # always shows what the controller actually holds.
            self.swap.setChecked(curves[0].swapped)

    def set_positions(self, left: int, right: int) -> None:
        self.sides["left"].meter.set_position(left)
        self.sides["right"].meter.set_position(right)
