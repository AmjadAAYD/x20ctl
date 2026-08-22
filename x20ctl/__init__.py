"""Open configuration for the EasySMX X20 and other KeyLinker controllers.

The version lives here rather than in pyproject.toml because the packaged
executable has no installed distribution to ask, and a number the app cannot
read is a number that goes stale. Packaging reads it back out of this module.
"""

from __future__ import annotations

__version__ = "1.2.0"
