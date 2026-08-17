"""Closing the window leaves the app running in the tray.

Amjad: "THE APP MUST BE ON IN THE BACKGROUND". 1.1.0 shipped the opposite -- the
tray was a readout and closing the window quit -- so this pins the behaviour.

The flag defaults to off, because the window is the only way back in: enabling
it without a tray icon would hide the app with no route to reopen it.
"""

from __future__ import annotations

from PySide6.QtGui import QCloseEvent
from PySide6.QtWidgets import QApplication

from x20ctl.gui.shell import AppShell

app = QApplication.instance() or QApplication([])


def _close(shell) -> QCloseEvent:
    event = QCloseEvent()
    shell.closeEvent(event)
    return event


def test_closing_quits_by_default():
    """No tray, no hiding: otherwise the app vanishes with no way back."""
    shell = AppShell()
    event = _close(shell)
    assert event.isAccepted(), "without a tray the close must go through"


def test_closing_hides_when_the_tray_is_there():
    shell = AppShell()
    shell.show()
    shell.set_close_to_tray(True)

    event = _close(shell)
    assert not event.isAccepted(), "the close must be refused, not honoured"
    assert shell.isHidden(), "the window should be hidden, not destroyed"


def test_hiding_announces_itself_once():
    shell = AppShell()
    shell.set_close_to_tray(True)
    seen: list[int] = []
    shell.hidden_to_tray.connect(lambda: seen.append(1))

    _close(shell)
    assert len(seen) == 1
    assert shell.first_hide_to_tray is True, "first close explains where it went"

    shell.first_hide_to_tray = False
    _close(shell)
    assert len(seen) == 2, "the signal fires every time"
    assert not getattr(shell, "first_hide_to_tray", False), (
        "but the balloon is only offered once")


def test_quit_path_can_really_close():
    """Turning the flag off is what lets Quit actually exit."""
    shell = AppShell()
    shell.set_close_to_tray(True)
    assert not _close(shell).isAccepted()

    shell.set_close_to_tray(False)
    assert _close(shell).isAccepted()
