"""Entry point for the packaged executable.

PyInstaller's entry script loads the local desktop runtime and native bridge.
"""

import sys

from x20ctl.desktop.launcher import main

sys.exit(main())
