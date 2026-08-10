"""Entry point for the packaged application.

PyInstaller runs its entry script as a top level module, so `x20ctl/gui/__main__.py`
can't be used directly: its relative import has no parent package at that point.
This imports absolutely instead.

Running from source is unaffected; `python -m x20ctl.gui` still works.
"""

from __future__ import annotations

import sys

from x20ctl.gui import main

if __name__ == "__main__":
    sys.exit(main())
