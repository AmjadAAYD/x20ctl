"""Write a setting to the pad once the user stops fiddling with it.

Some settings do not want an Apply button. Vibration is the clearest case: it
is a ceiling that games scale to, there is nothing to compose, and you tune it
by feel rather than by number.

What it must not do is write on every step of a drag. Each write is a packet
plus a settle delay, and this link drops when hammered. So changes are
collected and only the last one is sent, once the value has held still.
"""

from __future__ import annotations

from PySide6.QtCore import QObject, QTimer, Signal

SETTLE_MS = 450


class Debounced(QObject):
    """Emits `ready` with the latest value once it stops changing."""

    ready = Signal(object)

    def __init__(self, settle_ms: int = SETTLE_MS, parent=None) -> None:
        super().__init__(parent)
        self._value = None
        self._pending = False
        self._timer = QTimer(self)
        self._timer.setSingleShot(True)
        self._timer.setInterval(settle_ms)
        self._timer.timeout.connect(self._fire)

    @property
    def pending(self) -> bool:
        return self._pending

    def push(self, value) -> None:
        """Note a new value and restart the clock."""
        self._value = value
        self._pending = True
        self._timer.start()

    def flush(self) -> None:
        """Send the pending value now, if there is one.

        Used when leaving a page or closing the window, so a setting adjusted a
        moment before is not lost.
        """
        if self._pending:
            self._timer.stop()
            self._fire()

    def cancel(self) -> None:
        self._timer.stop()
        self._pending = False
        self._value = None

    def _fire(self) -> None:
        if not self._pending:
            return
        self._pending = False
        value, self._value = self._value, None
        self.ready.emit(value)
