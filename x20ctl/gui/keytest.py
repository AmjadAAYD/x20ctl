"""Press everything and watch it light up.

Its own tab because it answers a different question from the settings pages:
not "what should this button do" but "is this controller working". It reads
XInput, so it needs no Bluetooth connection and works on a pad that is only
plugged in.
"""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QFrame, QGridLayout, QHBoxLayout, QLabel, QProgressBar, QVBoxLayout, QWidget,
)

from .. import protocol as p

# Every button XInput can report, in the order they are laid out on the pad.
FACE = ((p.Key.A, "A"), (p.Key.B, "B"), (p.Key.X, "X"), (p.Key.Y, "Y"))
SHOULDER = ((p.Key.LB, "LB"), (p.Key.RB, "RB"),
            (p.Key.LT, "LT"), (p.Key.RT, "RT"))
DPAD = ((p.Key.DPAD_UP, "Up"), (p.Key.DPAD_DOWN, "Down"),
        (p.Key.DPAD_LEFT, "Left"), (p.Key.DPAD_RIGHT, "Right"))
CENTRE = ((p.Key.SELECT, "Select"), (p.Key.START, "Start"),
          (p.Key.L3, "L3"), (p.Key.R3, "R3"))

GROUPS = (("Face", FACE), ("Shoulders", SHOULDER),
          ("D-pad", DPAD), ("Centre", CENTRE))


class Lamp(QLabel):
    """One button. Dim until it is pressed."""

    def __init__(self, text: str) -> None:
        super().__init__(text)
        self.setObjectName("Lamp")
        self.setAlignment(Qt.AlignCenter)
        self.setFixedHeight(38)
        self.setMinimumWidth(64)
        self._lit = False

    @property
    def lit(self) -> bool:
        return self._lit

    def set_lit(self, lit: bool) -> None:
        if lit == self._lit:
            return
        self._lit = lit
        self.setProperty("lit", "yes" if lit else "no")
        self.style().unpolish(self)
        self.style().polish(self)


class AxisBar(QWidget):
    """A stick axis or a trigger, drawn from its own resting position."""

    def __init__(self, title: str, signed: bool = True) -> None:
        super().__init__()
        self.signed = signed
        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(10)

        name = QLabel(title)
        name.setObjectName("RowDetail")
        name.setFixedWidth(74)
        layout.addWidget(name)

        self.bar = QProgressBar()
        self.bar.setRange(-100 if signed else 0, 100)
        self.bar.setValue(0)
        self.bar.setTextVisible(False)
        self.bar.setFixedHeight(9)
        layout.addWidget(self.bar, 1)

        self.reading = QLabel("0")
        self.reading.setObjectName("RowDetail")
        self.reading.setFixedWidth(48)
        self.reading.setAlignment(Qt.AlignRight)
        layout.addWidget(self.reading)

    def set_value(self, percent: int) -> None:
        low = -100 if self.signed else 0
        percent = max(low, min(100, percent))
        self.bar.setValue(percent)
        self.reading.setText(str(percent))


class KeyTestPage(QWidget):
    """Every input on the pad, live."""

    cleared = Signal()

    def __init__(self) -> None:
        super().__init__()
        self.lamps: dict[int, Lamp] = {}

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(6)

        title = QLabel("Test")
        title.setObjectName("PageTitle")
        root.addWidget(title)

        self.blurb = QLabel(
            "Press anything on the controller and watch it answer here. "
            "This reads the controller directly, so it works whether or not "
            "the settings connection is up. Nothing here changes anything.")
        self.blurb.setObjectName("PageSubtitle")
        self.blurb.setWordWrap(True)
        root.addWidget(self.blurb)
        root.addSpacing(14)

        grid = QGridLayout()
        grid.setHorizontalSpacing(18)
        grid.setVerticalSpacing(10)
        for column, (heading, group) in enumerate(GROUPS):
            caption = QLabel(heading)
            caption.setObjectName("RailHeading")
            grid.addWidget(caption, 0, column)
            holder = QVBoxLayout()
            holder.setSpacing(6)
            for key, label in group:
                lamp = Lamp(label)
                self.lamps[int(key)] = lamp
                holder.addWidget(lamp)
            box = QWidget()
            box.setLayout(holder)
            grid.addWidget(box, 1, column)
        root.addLayout(grid)
        root.addSpacing(16)

        sticks = QFrame()
        sticks.setObjectName("ControllerRow")
        axes = QVBoxLayout(sticks)
        axes.setContentsMargins(16, 14, 16, 14)
        axes.setSpacing(8)
        self.axes = {
            "left_x": AxisBar("Left X"), "left_y": AxisBar("Left Y"),
            "right_x": AxisBar("Right X"), "right_y": AxisBar("Right Y"),
            "left_trigger": AxisBar("LT", signed=False),
            "right_trigger": AxisBar("RT", signed=False),
        }
        for bar in self.axes.values():
            axes.addWidget(bar)
        root.addWidget(sticks)
        root.addStretch(1)

    def set_buttons(self, pressed) -> None:
        """Light exactly the buttons in `pressed` and darken the rest."""
        held = {int(key) for key in pressed}
        for code, lamp in self.lamps.items():
            lamp.set_lit(code in held)

    def set_axis(self, name: str, percent: int) -> None:
        bar = self.axes.get(name)
        if bar is not None:
            bar.set_value(percent)

    def clear(self) -> None:
        """Everything released, everything centred."""
        self.set_buttons(())
        for bar in self.axes.values():
            bar.set_value(0)
        self.cleared.emit()

    def lit(self) -> set:
        return {code for code, lamp in self.lamps.items() if lamp.lit}
