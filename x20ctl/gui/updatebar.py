"""The launch update check: a quiet line at the bottom, loud only when it matters.

Behaviour asked for directly:

- On launch, look for a newer release. While looking, say so, with a spinner.
- If there is nothing newer, say "Currently at the latest version" and the
  version, and never interrupt. Being up to date is not news.
- If there IS something newer, pop a dialog with Cancel and Update. Nobody is
  forced; Update opens the release page.

Two things this must not do, both of which the on-demand version did:

- Block the UI. `updates.check` calls urllib with an eight-second timeout, and
  running that on the main thread at launch would freeze the window on any slow
  network. It runs on a worker thread here.
- Nag. A tool that reports at you every launch to say it is fine is noise.
"""

from __future__ import annotations

from PySide6.QtCore import QObject, QThread, QTimer, Qt, Signal
from PySide6.QtGui import QColor, QDesktopServices, QPainter, QPen
from PySide6.QtWidgets import QHBoxLayout, QLabel, QMessageBox, QWidget

from .. import __version__
from .updates import RELEASES_PAGE, check

SPIN_MS = 60
ARC_SPAN = 100 * 16          # Qt angles are sixteenths of a degree


class Spinner(QWidget):
    """A small rotating arc. Only paints while running, so it costs nothing idle."""

    def __init__(self, size: int = 13, parent=None) -> None:
        super().__init__(parent)
        self._angle = 0
        self._running = False
        self.setFixedSize(size, size)
        self._timer = QTimer(self)
        self._timer.setInterval(SPIN_MS)
        self._timer.timeout.connect(self._advance)

    @property
    def running(self) -> bool:
        return self._running

    def start(self) -> None:
        self._running = True
        self.show()
        self._timer.start()

    def stop(self) -> None:
        self._running = False
        self._timer.stop()
        self.hide()

    def _advance(self) -> None:
        self._angle = (self._angle + 30) % 360
        self.update()

    def paintEvent(self, _event) -> None:            # noqa: N802 - Qt's name
        if not self._running:
            return
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)
        pen = QPen(QColor("#8ab4f8"))
        pen.setWidth(2)
        pen.setCapStyle(Qt.RoundCap)
        painter.setPen(pen)
        box = self.rect().adjusted(1, 1, -1, -1)
        painter.drawArc(box, -self._angle * 16, ARC_SPAN)
        painter.end()


class _Worker(QObject):
    """Runs the blocking check off the UI thread."""

    done = Signal(str, object)

    def __init__(self, current: str, fetch=None) -> None:
        super().__init__()
        self._current = current
        self._fetch = fetch

    def run(self) -> None:
        try:
            message, release = (check(self._current, self._fetch)
                                if self._fetch else check(self._current))
        except Exception as exc:                     # noqa: BLE001 - reported
            message, release = f"Could not check for updates: {exc}", None
        self.done.emit(message, release)


class UpdateBar(QWidget):
    """Bottom-left status line for the launch check."""

    finished = Signal(str, object)       # message, release or None

    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        row = QHBoxLayout(self)
        row.setContentsMargins(12, 4, 12, 6)
        row.setSpacing(7)

        self.spinner = Spinner()
        self.spinner.hide()
        row.addWidget(self.spinner)

        self.label = QLabel(f"x20ctl {__version__}")
        self.label.setObjectName("RowDetail")
        row.addWidget(self.label)
        row.addStretch(1)

        self._thread: QThread | None = None
        self._worker: _Worker | None = None
        self.release = None

    # -- the check -------------------------------------------------------

    def start(self, fetch=None) -> None:
        """Look for a newer release, off the UI thread."""
        if self._thread is not None:
            return                                   # one at a time
        self.spinner.start()
        self.label.setText("Searching for a new update...")

        self._thread = QThread(self)
        self._worker = _Worker(__version__, fetch)
        self._worker.moveToThread(self._thread)
        self._thread.started.connect(self._worker.run)
        self._worker.done.connect(self._settle)
        self._thread.start()

    def _settle(self, message: str, release) -> None:
        self.spinner.stop()
        self.release = release
        if release is not None:
            self.label.setText(f"Update available: {release.version}")
        elif message.startswith("Could not"):
            # Say it plainly and stay out of the way: an unreachable GitHub is
            # not the user's problem and must not produce a dialog.
            self.label.setText(f"x20ctl {__version__} — update check failed")
        else:
            self.label.setText(f"Currently at the latest version — {__version__}")

        if self._thread is not None:
            self._thread.quit()
            self._thread.wait(2000)
            self._thread = None
            self._worker = None
        self.finished.emit(message, release)

    # -- the dialog ------------------------------------------------------

    def offer(self, release, *, show: bool = True) -> QMessageBox:
        """Ask whether to update. Cancel is a real answer, and the default."""
        box = QMessageBox(self)
        box.setWindowTitle("Update available")
        box.setText(f"x20ctl {release.version} is available.\n"
                    f"You are on {__version__}.")
        notes = (getattr(release, "notes", "") or "").strip()
        if notes:
            box.setInformativeText(notes[:600])
        update = box.addButton("Update", QMessageBox.AcceptRole)
        box.addButton("Cancel", QMessageBox.RejectRole)
        box.setDefaultButton(update)

        def choose(_button=None) -> None:
            if box.clickedButton() is update:
                QDesktopServices.openUrl(
                    _url(getattr(release, "url", RELEASES_PAGE)))

        box.finished.connect(choose)
        if show:
            box.show()
        return box


def _url(text: str):
    from PySide6.QtCore import QUrl
    return QUrl(text or RELEASES_PAGE)
