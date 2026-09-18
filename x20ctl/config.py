"""Small persistent settings, kept beside the profiles.

Separate from the CLI so the GUI doesn't have to import it. A windowed entry
point runs under pythonw, where importing terminal-oriented code has bitten us
once already.
"""

from __future__ import annotations

import json
import os

from .profiles import DEFAULT_DIR

CONFIG_PATH = os.path.join(os.path.dirname(DEFAULT_DIR), "config.json")


def load_address() -> str | None:
    """The last controller we connected to, if any."""
    try:
        with open(CONFIG_PATH, encoding="utf-8") as fh:
            value = json.load(fh).get("address")
    except (OSError, ValueError):
        return None
    return value if isinstance(value, str) and value else None


def save_address(address: str) -> None:
    try:
        os.makedirs(os.path.dirname(CONFIG_PATH), exist_ok=True)
        with open(CONFIG_PATH, "w", encoding="utf-8") as fh:
            json.dump({"address": address}, fh, indent=2)
    except OSError:
        pass    # remembering the address is a convenience, never a hard failure
