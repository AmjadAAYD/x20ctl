"""Desktop interface for x20ctl.

    python -m x20ctl.gui
"""

from __future__ import annotations

import sys


def main() -> int:
    from PySide6.QtWidgets import QApplication

    from . import theme
    from .window import MainWindow

    app = QApplication(sys.argv)
    app.setApplicationName("x20ctl")
    app.setStyleSheet(theme.STYLESHEET)

    window = MainWindow()
    window.show()
    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
