"""Stick and trigger response curves.

Reading these was settled early; writing them is the newest thing this app does,
so the page is built around making a write visible. What the pad reported when
the app connected is kept, so any change can be put back, and after a write the
page shows what the pad reports rather than what it was sent.

The two control points are dragged directly. The deadzones are sliders, because
they are a distance along one axis and a slider says that better than a number
does.
"""

from __future__ import annotations

from PySide6.QtCore import QPointF, QRectF, Qt, Signal
from PySide6.QtGui import QColor, QPainter, QPen, QPolygonF
from PySide6.QtWidgets import (
    QFrame, QGridLayout, QHBoxLayout, QLabel, QPushButton, QScrollArea, QSlider,
    QVBoxLayout, QWidget,
)

from .. import protocol as p
from . import theme
from .widgets import divider, section_label

# Which channel of which record each editor shows, and what to call it.
CHANNELS = (
    ("sticks", 0, "Left stick"),
    ("sticks", 1, "Right stick"),
    ("triggers", 0, "Left trigger"),
    ("triggers", 1, "Right trigger"),
)

EXPLANATION = (
    "How far a stick or trigger has to move before the controller reports it, "
    "and how its travel maps to what a game receives.\n\n"
    "DEADZONES\n"
    "  inner: the slack around the centre that is ignored. Raise it if a "
    "released stick still drifts; lower it for finer aim.\n\n"
    "  outer: where travel counts as fully pressed. Lower it if the stick "
    "reaches its edge before the game reads maximum.\n\n"
    "THE CURVE\n"
    "Drag either point. The dotted diagonal is a linear response, where the "
    "output matches the movement exactly. Above the line the controller reacts "
    "faster than your thumb moves, below it more gently.\n\n"
    "An untouched X20 has linear sticks and triggers that already ramp faster "
    "than linear.\n\n"
    "WHAT IS KNOWN\n"
    "The two points are read from the pad and written back to it exactly. How "
    "the firmware joins them is not documented anywhere, so the line drawn "
    "between them is this app's own smooth interpolation, not a promise about "
    "what the hardware computes between those points.\n\n"
    "Nothing here is written until you press Write. Holding C for five seconds "
    "factory resets the controller if a value ever leaves it feeling wrong."
)


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


class CurvePlot(QWidget):
    """One channel's response curve, with both control points draggable."""

    changed = Signal()

    PAD = 12
    GRAB = 16          # how close a click has to land to catch a point

    def __init__(self) -> None:
        super().__init__()
        # Fixed rather than expanding: the plot is square, so any width it took
        # beyond its height would be blank, and it would take it from the
        # sliders beside it.
        self.setFixedSize(148, 148)
        self.setMouseTracking(True)
        # Let the card behind show through; this widget paints its own plot area.
        self.setStyleSheet("background: transparent;")
        self._curve: p.Curve | None = None
        self._dragging: int | None = None
        self._hover: int | None = None

    # -- state -----------------------------------------------------------

    def curve(self) -> p.Curve | None:
        return self._curve

    def set_curve(self, curve: p.Curve | None) -> None:
        self._curve = curve
        self.update()

    def setEnabled(self, enabled: bool) -> None:      # noqa: N802  (Qt naming)
        super().setEnabled(enabled)
        self.update()

    # -- geometry --------------------------------------------------------

    def _area(self) -> QRectF:
        side = min(self.width(), self.height()) - 2 * self.PAD
        return QRectF(self.PAD, self.PAD, max(side, 1), max(side, 1))

    def _to_pixel(self, x: float, y: float) -> QPointF:
        area = self._area()
        return QPointF(area.left() + area.width() * x / p.CURVE_AXIS_MAX,
                       area.bottom() - area.height() * y / p.CURVE_AXIS_MAX)

    def _to_value(self, pos) -> tuple[int, int]:
        area = self._area()
        x = (pos.x() - area.left()) / area.width() * p.CURVE_AXIS_MAX
        y = (area.bottom() - pos.y()) / area.height() * p.CURVE_AXIS_MAX
        return (round(_clamp(x, 0, p.CURVE_AXIS_MAX)),
                round(_clamp(y, 0, p.CURVE_AXIS_MAX)))

    def _nearest_point(self, pos) -> int | None:
        if self._curve is None:
            return None
        best, distance = None, self.GRAB
        for index, point in enumerate((self._curve.point1, self._curve.point2)):
            offset = self._to_pixel(*point) - pos
            length = (offset.x() ** 2 + offset.y() ** 2) ** 0.5
            if length < distance:
                best, distance = index, length
        return best

    # -- interaction -----------------------------------------------------

    def mousePressEvent(self, event) -> None:        # noqa: N802
        if not self.isEnabled() or self._curve is None:
            return
        self._dragging = self._nearest_point(event.position())
        if self._dragging is not None:
            self._drag_to(event.position())

    def mouseMoveEvent(self, event) -> None:         # noqa: N802
        if self._dragging is not None:
            self._drag_to(event.position())
            return
        hover = self._nearest_point(event.position()) if self.isEnabled() else None
        if hover != self._hover:
            self._hover = hover
            self.setCursor(Qt.PointingHandCursor if hover is not None
                           else Qt.ArrowCursor)
            self.update()

    def mouseReleaseEvent(self, _event) -> None:     # noqa: N802
        self._dragging = None

    def _drag_to(self, pos) -> None:
        x, y = self._to_value(pos)
        first, second = self._curve.point1, self._curve.point2
        # The points can meet but not cross: a curve that doubled back would be
        # a response that falls as you push further.
        if self._dragging == 0:
            self._curve = self._curve.with_points(point1=(min(x, second[0]), y))
        else:
            self._curve = self._curve.with_points(point2=(max(x, first[0]), y))
        self.update()
        self.changed.emit()

    # -- painting --------------------------------------------------------

    def paintEvent(self, _event) -> None:            # noqa: N802
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        area = self._area()

        painter.setPen(QPen(QColor(theme.LINE), 1))
        painter.setBrush(QColor(theme.BG))
        painter.drawRoundedRect(area, 8, 8)

        if self._curve is None:
            painter.setPen(QColor(theme.TEXT_FAINT))
            painter.drawText(area, Qt.AlignCenter, "not read yet")
            return

        self._paint_deadzones(painter, area)

        painter.setPen(QPen(QColor(theme.LINE), 1, Qt.SolidLine))
        for step in (0.25, 0.5, 0.75):
            painter.drawLine(QPointF(area.left() + area.width() * step, area.top()),
                             QPointF(area.left() + area.width() * step, area.bottom()))
            painter.drawLine(QPointF(area.left(), area.top() + area.height() * step),
                             QPointF(area.right(), area.top() + area.height() * step))

        painter.setPen(QPen(QColor(theme.TEXT_FAINT), 1, Qt.DotLine))
        painter.drawLine(QPointF(area.left(), area.bottom()),
                         QPointF(area.right(), area.top()))

        faded = not self.isEnabled()
        line = QColor(theme.EMBER_DEEP if faded else theme.EMBER)
        painter.setPen(QPen(line, 2))
        painter.setBrush(Qt.NoBrush)
        painter.drawPolyline(QPolygonF(
            [self._to_pixel(x, y) for x, y in self._curve.shape(steps=48)]))

        for index, point in enumerate((self._curve.point1, self._curve.point2)):
            active = index in (self._hover, self._dragging)
            centre = self._to_pixel(*point)
            painter.setPen(QPen(QColor(theme.TEXT if active else theme.INK), 2))
            painter.setBrush(line)
            painter.drawEllipse(centre, 5.5 if active else 4.5,
                                5.5 if active else 4.5)

    def _paint_deadzones(self, painter: QPainter, area: QRectF) -> None:
        """Shade the travel that is ignored at each end."""
        curve = self._curve
        shade = QColor(theme.SURFACE_TOP)
        shade.setAlpha(150)
        painter.setPen(Qt.NoPen)
        painter.setBrush(shade)

        inner = area.width() * curve.inner_deadzone / curve.max_progress
        if inner > 0:
            painter.drawRect(QRectF(area.left(), area.top(), inner, area.height()))
        outer = area.width() * curve.outer_deadzone / curve.max_progress
        if outer < area.width():
            painter.drawRect(QRectF(area.left() + outer, area.top(),
                                    area.width() - outer, area.height()))


class ChannelEditor(QFrame):
    """One channel: its curve, its deadzones, and its flags."""

    changed = Signal()

    def __init__(self, kind: str, index: int, caption: str) -> None:
        super().__init__()
        self.kind = kind
        self.index = index
        self.setObjectName("Card")
        self._loading = False

        layout = QVBoxLayout(self)
        layout.setContentsMargins(14, 12, 14, 12)
        layout.setSpacing(8)

        heading = QHBoxLayout()
        title = QLabel(caption)
        title.setObjectName("Muted")
        self.shape_label = QLabel("")
        self.shape_label.setObjectName("Faint")
        heading.addWidget(title)
        heading.addStretch(1)
        heading.addWidget(self.shape_label)
        layout.addLayout(heading)

        body = QHBoxLayout()
        body.setSpacing(12)
        self.plot = CurvePlot()
        self.plot.changed.connect(self._on_plot_changed)
        body.addWidget(self.plot)

        controls = QVBoxLayout()
        controls.setSpacing(6)
        deadzones = QLabel("deadzone")
        deadzones.setObjectName("Section")
        controls.addWidget(deadzones)
        self.inner, self.inner_value = self._slider(
            "inner", "The slack around the centre that is ignored.", controls)
        self.outer, self.outer_value = self._slider(
            "outer", "Where travel starts counting as fully pressed.", controls)
        controls.addStretch(1)
        body.addLayout(controls, 1)
        layout.addLayout(body)

        # Along the bottom rather than beside the plot: three buttons in a
        # column would set the card's width, and four of these cards have to sit
        # two abreast without the window growing to suit them.
        self.flag_row = QHBoxLayout()
        self.flag_row.setSpacing(6)
        self.invert_x = self._toggle("Invert X", self.flag_row)
        self.invert_y = self._toggle("Invert Y", self.flag_row)
        self.straighten = QPushButton("Straighten")
        self.straighten.setObjectName("Ghost")
        self.straighten.setToolTip(
            "Put the two points back on the diagonal. Deadzones are left alone.")
        self.straighten.clicked.connect(self._straighten)
        self.flag_row.addWidget(self.straighten)
        layout.addLayout(self.flag_row)

        # Invert is a stick-only control. The trigger record has the same flag
        # byte, but per HostActivity.applyTriggerData only bit 2 is ever set on
        # it (the L2/R2 exchange, offered on the Triggers page); its invert bits
        # are written as zero by the vendor app and are not exposed here.
        if kind != "sticks":
            for widget in (self.invert_x, self.invert_y):
                widget.hide()

    def _slider(self, caption: str, explanation: str,
                layout: QVBoxLayout) -> tuple[QSlider, QLabel]:
        row = QHBoxLayout()
        row.setSpacing(8)
        name = QLabel(caption)
        name.setObjectName("Faint")
        name.setFixedWidth(34)
        name.setToolTip(explanation)
        slider = QSlider(Qt.Horizontal)
        slider.setMinimumWidth(70)
        slider.setToolTip(explanation)
        slider.valueChanged.connect(self._on_slider_changed)
        value = QLabel("0")
        value.setObjectName("Muted")
        value.setFixedWidth(26)
        value.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        row.addWidget(name)
        row.addWidget(slider, 1)
        row.addWidget(value)
        layout.addLayout(row)
        return slider, value

    def _toggle(self, caption: str, layout: QHBoxLayout) -> QPushButton:
        button = QPushButton(caption)
        button.setObjectName("Ghost")
        button.setCheckable(True)
        button.clicked.connect(self._on_flag_changed)
        layout.addWidget(button)
        return button

    # -- state -----------------------------------------------------------

    def curve(self) -> p.Curve | None:
        return self.plot.curve()

    def set_curve(self, curve: p.Curve | None) -> None:
        self._loading = True
        try:
            self.plot.set_curve(curve)
            for slider in (self.inner, self.outer):
                slider.setEnabled(curve is not None)
            if curve is None:
                self.shape_label.setText("")
                return
            for slider in (self.inner, self.outer):
                slider.setMaximum(curve.max_progress)
            self.inner.setValue(curve.inner_deadzone)
            self.outer.setValue(curve.outer_deadzone)
            self.invert_x.setChecked(curve.invert_x)
            self.invert_y.setChecked(curve.invert_y)
            self._refresh()
        finally:
            self._loading = False

    def _refresh(self) -> None:
        curve = self.plot.curve()
        if curve is None:
            return
        self.inner_value.setText(str(curve.inner_deadzone))
        self.outer_value.setText(str(curve.outer_deadzone))
        self.shape_label.setText("linear" if curve.is_linear else "curved")

    # -- edits -----------------------------------------------------------

    def _on_plot_changed(self) -> None:
        self._refresh()
        if not self._loading:
            self.changed.emit()

    def _on_slider_changed(self) -> None:
        curve = self.plot.curve()
        if curve is None or self._loading:
            return
        inner, outer = self.inner.value(), self.outer.value()
        # Push whichever slider the user isn't holding rather than refusing to
        # move: an inner deadzone at or past the outer one leaves no travel. At
        # the very end of the range there is nowhere to push to, so the held one
        # gives way instead.
        if inner >= outer:
            if self.sender() is self.inner:
                outer = min(curve.max_progress, inner + 1)
                inner = min(inner, outer - 1)
            else:
                inner = max(0, outer - 1)
                outer = max(outer, inner + 1)
            self._show_slider(self.inner, inner)
            self._show_slider(self.outer, outer)
        self.plot.set_curve(curve.with_deadzones(inner=inner, outer=outer))
        self._refresh()
        self.changed.emit()

    @staticmethod
    def _show_slider(slider: QSlider, value: int) -> None:
        """Move a slider without it reporting back as another edit."""
        slider.blockSignals(True)
        slider.setValue(value)
        slider.blockSignals(False)

    def _on_flag_changed(self) -> None:
        curve = self.plot.curve()
        if curve is None or self._loading:
            return
        self.plot.set_curve(curve.with_flags(invert_x=self.invert_x.isChecked(),
                                             invert_y=self.invert_y.isChecked()))
        self.changed.emit()

    def _straighten(self) -> None:
        curve = self.plot.curve()
        if curve is None:
            return
        self.plot.set_curve(curve.straightened())
        self._refresh()
        self.changed.emit()


class CurvesPage(QWidget):
    """The whole page: four channels, and one button that writes them."""

    write_requested = Signal()
    revert_requested = Signal()

    def __init__(self) -> None:
        super().__init__()
        self.editors: dict[tuple[str, int], ChannelEditor] = {}
        self._baseline: dict[str, list[p.Curve]] = {}
        self._busy = False

        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 20, 24, 20)
        layout.setSpacing(14)

        heading = QHBoxLayout()
        title = QLabel("Sticks and triggers")
        title.setObjectName("Title")
        self.subtitle = QLabel("not connected")
        self.subtitle.setObjectName("Faint")
        stack = QVBoxLayout()
        stack.setSpacing(1)
        stack.addWidget(title)
        stack.addWidget(self.subtitle)
        heading.addLayout(stack)
        heading.addStretch(1)
        layout.addLayout(heading)
        layout.addWidget(divider())

        layout.addWidget(section_label("response", EXPLANATION))

        # Four cards of a fixed minimum size would otherwise set the floor for
        # the whole window, so they scroll instead. The window stays as small as
        # the settings page allows.
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        holder = QWidget()
        grid = QGridLayout(holder)
        grid.setContentsMargins(0, 0, 6, 0)
        grid.setSpacing(10)
        for position, (kind, index, caption) in enumerate(CHANNELS):
            editor = ChannelEditor(kind, index, caption)
            editor.changed.connect(self._on_edit)
            self.editors[(kind, index)] = editor
            grid.addWidget(editor, position // 2, position % 2)
        scroll.setWidget(holder)
        layout.addWidget(scroll, 1)

        # Swap is one flag for the pair, not a per-channel setting: the app
        # writes the same bit into both halves of the record. So it lives on the
        # page, beside the write button, rather than on a card.
        swap_row = QHBoxLayout()
        swap_row.setSpacing(10)
        self.swap_sticks = QPushButton("Swap left and right sticks")
        self.swap_sticks.setCheckable(True)
        self.swap_sticks.setObjectName("Ghost")
        self.swap_sticks.clicked.connect(self._on_edit)
        swap_row.addWidget(self.swap_sticks)
        swap_note = QLabel(
            "The left stick does what the right one did, and the other way "
            "round. Stored on the controller, so it applies in every game.")
        swap_note.setObjectName("Faint")
        swap_note.setWordWrap(True)
        swap_row.addWidget(swap_note, 1)
        layout.addLayout(swap_row)

        layout.addWidget(divider())
        footer = QHBoxLayout()
        footer.setSpacing(10)
        self.status = QLabel("")
        self.status.setObjectName("Muted")
        self.revert_button = QPushButton("Back to what the pad had")
        self.revert_button.setObjectName("Ghost")
        self.revert_button.setToolTip(
            "Restore the values read when this app connected, and write them.")
        self.revert_button.clicked.connect(self.revert_requested)
        self.write_button = QPushButton("Write to controller")
        self.write_button.setObjectName("Primary")
        self.write_button.clicked.connect(self.write_requested)
        footer.addWidget(self.status, 1)
        footer.addWidget(self.revert_button)
        footer.addWidget(self.write_button)
        layout.addLayout(footer)

        self.set_available(False, False)

    # -- state -----------------------------------------------------------

    def load(self, kind: str, curves: list[p.Curve], *, baseline: bool = False) -> None:
        """Show what the pad reports. `baseline` also makes it the undo point."""
        for index, curve in enumerate(curves):
            editor = self.editors.get((kind, index))
            if editor is not None:
                editor.set_curve(curve)
        if kind == "sticks" and curves:
            # Read back from the pad, not remembered, so the button shows what
            # the controller actually holds.
            self.swap_sticks.blockSignals(True)
            self.swap_sticks.setChecked(curves[0].swapped)
            self.swap_sticks.blockSignals(False)
        if baseline:
            self._baseline[kind] = list(curves)
        self._sync()

    def baseline(self, kind: str) -> list[p.Curve]:
        return list(self._baseline.get(kind, []))

    def values(self, kind: str) -> list[p.Curve]:
        """Every channel of one record, in wire order.

        The swap flag is applied here rather than held on a card, because the
        pad stores it on both halves of the record or neither.
        """
        out = []
        for (editor_kind, _index), editor in sorted(self.editors.items()):
            curve = editor.curve()
            if editor_kind == kind and curve is not None:
                out.append(curve)
        if kind == "sticks":
            wanted = self.swap_sticks.isChecked()
            out = [c.with_flags(swapped=wanted) for c in out]
        return out

    def changed_kinds(self) -> list[str]:
        """Which records differ from what the pad last reported."""
        out = []
        for kind, original in self._baseline.items():
            current = self.values(kind)
            if len(current) == len(original) and any(
                    a != b for a, b in zip(current, original)):
                out.append(kind)
        return out

    def set_available(self, sticks: bool, triggers: bool) -> None:
        for (kind, _index), editor in self.editors.items():
            editor.setEnabled(sticks if kind == "sticks" else triggers)
        if not (sticks or triggers):
            self.subtitle.setText("connect the controller to read its curves")
        self._sync()

    def set_busy(self, busy: bool) -> None:
        self._busy = busy
        self._sync()

    def set_status(self, text: str, kind: str = "Muted") -> None:
        self.status.setText(text)
        self.status.setObjectName(kind)
        self.status.style().unpolish(self.status)
        self.status.style().polish(self.status)

    def _on_edit(self) -> None:
        self._sync()

    def _sync(self) -> None:
        busy = self._busy
        pending = self.changed_kinds()
        readable = bool(self._baseline)
        self.write_button.setEnabled(bool(pending) and not busy)
        self.write_button.setText(
            "Write to controller" if not pending
            else "Write " + " and ".join(pending))
        self.revert_button.setEnabled(readable and not busy)
        if readable and not busy:
            self.subtitle.setText("read from the controller"
                                  if not pending else "edited, not yet written")
