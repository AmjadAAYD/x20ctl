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


def _write_diagnostic(resolved: str | None) -> None:
    """Record how startup went, for when the app has no console to say it on.

    A windowed build cannot print, so a problem like a missing icon is silent.
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


def main() -> int:
    from PySide6.QtGui import QIcon
    from PySide6.QtWidgets import QApplication

    from . import theme
    from .window import MainWindow

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

    window = MainWindow()
    # Also set it on the window. The application icon alone is not always what
    # the taskbar picks up.
    if not icon.isNull():
        window.setWindowIcon(icon)
    window.show()
    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
