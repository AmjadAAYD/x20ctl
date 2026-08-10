"""Desktop interface for x20ctl.

    python -m x20ctl.gui
"""

from __future__ import annotations

import os
import sys


def icon_path() -> str | None:
    """Locate the icon, whether running from source or from a frozen build.

    PyInstaller unpacks bundled data to a temporary directory it advertises as
    sys._MEIPASS, so the path differs between the two cases.
    """
    root = getattr(sys, "_MEIPASS", None) or os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "..", "..")
    candidate = os.path.join(root, "assets", "x20ctl.ico")
    return candidate if os.path.exists(candidate) else None


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

    path = icon_path()
    if path:
        app.setWindowIcon(QIcon(path))

    # Windows groups taskbar buttons by this id. Without it a pythonw-hosted
    # app inherits Python's own identity and shows Python's icon instead.
    if os.name == "nt":
        try:
            import ctypes

            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
                "amjadaayd.x20ctl")
        except Exception:
            pass

    window = MainWindow()
    window.show()
    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
