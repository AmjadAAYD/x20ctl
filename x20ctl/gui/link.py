"""One controller's live connection, as the GUI sees it.

Everything that talks to hardware goes through here, so the pages stay ignorant
of Bluetooth and there is exactly one place that decides what a failed write
looks like.

Work is handed to the async bridge and comes back as Qt signals. Both the
bridge and the way a pad is opened are injected, which is what lets the whole
thing be driven by a fake in tests.
"""

from __future__ import annotations

from PySide6.QtCore import QObject, Signal

from .. import protocol as p


class ControllerLink(QObject):
    """Reads and writes for one pad, off the GUI thread."""

    loaded = Signal(object)         # a Snapshot
    written = Signal(str)           # what was written, for the status line
    failed = Signal(str)            # a sentence fit to show a person
    busy_changed = Signal(bool)

    def __init__(self, address: str, *, bridge, open_pad) -> None:
        super().__init__()
        self.address = address
        self._bridge = bridge
        self._open = open_pad
        self._busy = False

    @property
    def busy(self) -> bool:
        return self._busy

    def _start(self) -> bool:
        """Refuse to stack work: this link drops when several writes overlap."""
        if self._busy:
            return False
        self._busy = True
        self.busy_changed.emit(True)
        return True

    def _finish(self) -> None:
        self._busy = False
        self.busy_changed.emit(False)

    def _run(self, work, note: str, on_done=None) -> bool:
        if not self._start():
            return False

        def done(result) -> None:
            self._finish()
            if on_done is not None:
                on_done(result)
            else:
                self.written.emit(note)

        def failed(message: str) -> None:
            self._finish()
            self.failed.emit(message)

        self._bridge.run(work, on_done=done, on_error=failed)
        return True

    # -- reading ---------------------------------------------------------

    def load(self) -> bool:
        """Read everything the pages need, in one connection."""
        async def work():
            async with self._open(self.address) as pad:
                return await pad.snapshot()

        return self._run(work, "loaded", on_done=self.loaded.emit)

    # -- writing ---------------------------------------------------------

    def set_vibration(self, percent: int) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.set_vibration(percent)

        return self._run(work, f"Vibration saved at {percent}%.")

    def set_shutdown(self, minutes) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.set_shutdown_timeout(minutes)

        note = ("Power timeout set to never."
                if minutes is None else f"Power timeout set to {minutes} min.")
        return self._run(work, note)

    def set_curves(self, kind: str, channels) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.set_curves(kind, channels)

        return self._run(work, f"{kind.title()} written.")

    def set_remapping(self, changes: dict) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.set_remapping(changes)

        note = ("Button remapping cleared." if not changes
                else f"{len(changes)} button(s) remapped.")
        return self._run(work, note)

    def write_macro(self, slot: int, steps, loop_ms: int = 0) -> bool:
        """Write one macro slot from steps the grid produced."""
        async def work():
            async with self._open(self.address) as pad:
                return await pad.write_macro_steps(slot, steps, loop_ms=loop_ms)

        return self._run(work, f"Macro written to M{slot}.")

    def clear_macro(self, slot: int) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.clear_macro(slot)

        return self._run(work, f"M{slot} cleared.")

    def read_macro(self, slot: int, on_done) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.read_macro(slot)

        return self._run(work, "", on_done=on_done)

    def calibrate(self) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.calibrate()

        return self._run(
            work, "Calibration sent. Press + on the controller to confirm.")

    def factory_reset(self) -> bool:
        async def work():
            async with self._open(self.address) as pad:
                return await pad.factory_reset()

        return self._run(work, "Controller reset to factory settings.")
