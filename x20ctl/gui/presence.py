"""Notice when a controller stops answering, and ask what to do about it.

A pad that has been switched off is not the same as one that was never added.
Silently leaving it in the list makes the app look wrong; silently removing it
throws away a player number somebody chose. So it goes red, and asks.

Waiting is a real answer, so Reconnect keeps watching rather than dismissing
the problem.
"""

from __future__ import annotations

from PySide6.QtCore import QObject, QTimer, Signal
from PySide6.QtWidgets import QMessageBox

SWEEP_MS = 6000
MISSES_BEFORE_ASKING = 2        # one quiet sweep is noise; two is an absence


class PresenceWatcher(QObject):
    """Scans now and then, and reports which controllers answered."""

    changed = Signal()                  # the roster's connected flags moved
    lost = Signal(object)               # a Slot that stopped answering

    def __init__(self, roster, *, scan=None, bridge=None,
                 interval_ms: int = SWEEP_MS) -> None:
        super().__init__()
        self.roster = roster
        self._scan = scan
        self._bridge = bridge
        self._misses: dict[str, int] = {}
        self._asked: set = set()
        self._running = False

        self.timer = QTimer(self)
        self.timer.setInterval(interval_ms)
        self.timer.timeout.connect(self.sweep)

    def start(self) -> None:
        if self._scan is not None:
            self.timer.start()

    def stop(self) -> None:
        self.timer.stop()

    def watch_again(self, slot) -> None:
        """Keep looking for one that was reported lost, without nagging."""
        self._asked.discard(slot.address.lower())

    def forget(self, slot) -> None:
        key = slot.address.lower()
        self._misses.pop(key, None)
        self._asked.discard(key)

    def sweep(self) -> None:
        if self._scan is None or self._running:
            return
        self._running = True

        if self._bridge is not None:
            self._bridge.run(self._scan, on_done=self._seen,
                             on_error=lambda _msg: self._seen([]))
            return
        try:
            self._seen(self._scan())
        except Exception:                       # noqa: BLE001 - a quiet sweep
            self._seen([])

    def _seen(self, found) -> None:
        """Update every slot from one sweep's results."""
        self._running = False
        addresses = {getattr(f, "address", "").lower() for f in found or ()}
        moved = False

        for slot in self.roster.ordered():
            key = slot.address.lower()
            here = key in addresses

            if here:
                self._misses[key] = 0
                if not slot.connected:
                    slot.connected = True
                    moved = True
                continue

            self._misses[key] = self._misses.get(key, 0) + 1
            if self._misses[key] < MISSES_BEFORE_ASKING:
                continue                        # a single quiet sweep is noise

            if slot.connected:
                slot.connected = False
                moved = True
            if key not in self._asked:
                self._asked.add(key)
                self.lost.emit(slot)

        if moved:
            self.changed.emit()


def ask_about_lost(slot, parent=None) -> QMessageBox:
    """The dialog for a controller that stopped answering.

    Two answers, both reasonable: let it go, or keep waiting for it.
    """
    box = QMessageBox(parent)
    box.setWindowTitle("Controller not answering")
    box.setIcon(QMessageBox.Warning)
    box.setText(f"{slot.label} has stopped answering.")
    box.setInformativeText(
        "It may be switched off, out of range, or connected to something "
        "else.\n\nRemove it, or keep watching for it to come back?")
    box.addButton("Reconnect", QMessageBox.AcceptRole)
    box.addButton("Remove", QMessageBox.DestructiveRole)
    return box
