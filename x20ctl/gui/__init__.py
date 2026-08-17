"""Desktop interface for x20ctl.

    python -m x20ctl.gui
"""

from __future__ import annotations

import os
import sys


def icon_candidates() -> list[str]:
    """Every place the icon might reasonably live.

    A frozen build unpacks bundled data to sys._MEIPASS, a source checkout has
    it beside the package, and an installed copy may have it next to the
    executable. Checking all of them is cheaper than being wrong in one of them.
    """
    here = os.path.dirname(os.path.abspath(__file__))
    roots = [
        getattr(sys, "_MEIPASS", None),
        os.path.join(here, "..", ".."),          # source checkout
        os.path.join(here, ".."),                # inside the package
        os.path.dirname(os.path.abspath(sys.argv[0])),
        os.getcwd(),
    ]
    out = []
    for root in roots:
        if not root:
            continue
        for relative in ("assets/x20ctl.ico", "x20ctl.ico"):
            out.append(os.path.normpath(os.path.join(root, relative)))
    return out


def icon_path() -> str | None:
    for candidate in icon_candidates():
        if os.path.exists(candidate):
            return candidate
    return None


def logo_path() -> str | None:
    """The PNG mark, for drawing inside the window.

    Same search as the icon, since they sit side by side. Falls back to the
    .ico, which QPixmap can also load, so the intro still has a mark if only
    one of the two shipped.
    """
    for candidate in icon_candidates():
        png = candidate.replace(".ico", ".png")
        if os.path.exists(png):
            return png
    return icon_path()


def _write_diagnostic(resolved: str | None) -> None:
    """Record how startup went, for when the app has no console to say it on.

    A windowed build can't print, so a problem like a missing icon is silent.
    Set X20CTL_DIAGNOSE=1 to have it leave a note.
    """
    if not os.environ.get("X20CTL_DIAGNOSE"):
        return
    try:
        import tempfile

        path = os.path.join(tempfile.gettempdir(), "x20ctl-diagnose.txt")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(f"frozen: {hasattr(sys, '_MEIPASS')}\n")
            fh.write(f"meipass: {getattr(sys, '_MEIPASS', None)}\n")
            fh.write(f"argv0: {sys.argv[0]}\n")
            fh.write(f"resolved icon: {resolved}\n\ncandidates:\n")
            for candidate in icon_candidates():
                fh.write(f"  [{'x' if os.path.exists(candidate) else ' '}] {candidate}\n")
    except Exception:
        pass


def _build_tray(app, window):
    """A battery tray icon, if the desktop has a tray at all.

    Returns None when there is no system tray, which is normal on some Linux
    sessions, rather than failing to start. Closing the window still quits: the
    tray is a readout, not a background mode, so the app does not silently keep
    running after you close it.
    """
    from PySide6.QtWidgets import QSystemTrayIcon

    if not QSystemTrayIcon.isSystemTrayAvailable():
        return None

    from .tray import BatteryTray

    tray = BatteryTray(window)

    def surface() -> None:
        window.showNormal()
        window.raise_()
        window.activateWindow()

    def leave() -> None:
        # Turn hide-on-close off first, or the window would swallow the quit and
        # the app would sit in the tray forever with no way out.
        if hasattr(window, "set_close_to_tray"):
            window.set_close_to_tray(False)
        app.quit()

    tray.show_requested.connect(surface)
    tray.quit_requested.connect(leave)

    workspace = getattr(window, "workspace", None)
    if workspace is not None and hasattr(workspace, "battery_read"):
        workspace.battery_read.connect(tray.show_battery)

    # Closing the window now hides it: the app keeps running and stays in the
    # tray. Quit is on the tray menu. Without this, Qt would exit as soon as the
    # last window closed and the tray icon would vanish with it.
    if hasattr(window, "set_close_to_tray"):
        window.set_close_to_tray(True)
        app.setQuitOnLastWindowClosed(False)

        def explain() -> None:
            if getattr(window, "first_hide_to_tray", False):
                window.first_hide_to_tray = False
                tray.showMessage(
                    "x20ctl is still running",
                    "Battery stays on the taskbar. Click the icon to reopen, "
                    "or right-click and choose Quit.",
                    tray.icon(), 4000)

        window.hidden_to_tray.connect(explain)

    tray.show()
    return tray


def main() -> int:
    from PySide6.QtGui import QIcon
    from PySide6.QtWidgets import QApplication

    from . import theme
    from .shell import build_app_window

    app = QApplication(sys.argv)
    app.setApplicationName("x20ctl")
    app.setApplicationDisplayName("x20ctl")
    app.setOrganizationName("x20ctl")
    app.setStyleSheet(theme.STYLESHEET)

    # Windows groups taskbar buttons by this id, and decides which icon to show
    # from it. It has to be set before the first window is created, or the
    # taskbar keeps whatever it decided first.
    if os.name == "nt":
        try:
            import ctypes

            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
                "amjadaayd.x20ctl")
        except Exception:
            pass

    path = icon_path()
    _write_diagnostic(path)
    icon = QIcon(path) if path else QIcon()
    if not icon.isNull():
        app.setWindowIcon(icon)

    window = build_app_window()
    # Also set it on the window. The application icon alone isn't always what
    # the taskbar picks up.
    if not icon.isNull():
        window.setWindowIcon(icon)
    window.show()

    tray = _build_tray(app, window)
    if tray is not None:
        window._tray = tray          # keep it alive for the app's lifetime

    # After show(), so the overlay is created against the window's real size.
    from .splash import play
    play(window, logo_path())

    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
