"""Entry point for the packaged executable.

PyInstaller needs a script rather than a module, and `x20ctl/gui/__main__.py`
uses a relative import that only resolves when run with `-m`. This is that
file's contents with an absolute import.
"""

import sys

from x20ctl.gui import main

sys.exit(main())
