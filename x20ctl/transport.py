"""Work out how the controller is physically connected.

The pad presents a different identity on each transport, so the way to tell them
apart is to enumerate what Windows sees and match on the interface rather than
on the USB ids, which are cloned from Microsoft on every link.

Observed on an EasySMX X20:

    wired USB          USB\\VID_045E&PID_028E   two interfaces, xusb22 + HID
    Bluetooth Classic  BTHENUM\\...045E&02FD    BR/EDR HID profile
    2.4 GHz dongle     not yet observed

The dongle case is reported honestly as "USB receiver" when a gamepad is present
on USB under ids that are not the known wired ones, rather than guessed at.
"""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass
from enum import Enum


class Link(Enum):
    WIRED = "wired USB"
    DONGLE = "2.4 GHz receiver"
    BLUETOOTH = "Bluetooth"
    UNKNOWN = "unknown"
    ABSENT = "not detected"


# The identities this pad is known to clone.
WIRED_ID = re.compile(r"USB\\VID_045E&PID_028E", re.I)
BLUETOOTH_ID = re.compile(r"BTHENUM\\.*VID&0002045E.*PID&02FD", re.I)
BLUETOOTH_ANY = re.compile(r"BTHENUM\\DEV_(98B6E[0-9A-F]{7})", re.I)
XINPUT_ANY = re.compile(r"USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", re.I)

# Anything matching this is a gamepad-ish device we should consider.
GAMEPAD_HINT = re.compile(r"IG_[0-9]{2}|xusb|XnaComposite|Xbox", re.I)


@dataclass
class Connection:
    link: Link
    detail: str = ""
    device_id: str = ""

    @property
    def connected(self) -> bool:
        return self.link not in (Link.ABSENT, Link.UNKNOWN)

    def __str__(self) -> str:
        if not self.detail:
            return self.link.value
        return f"{self.link.value} · {self.detail}"


def _enumerate() -> list[str]:
    """Present PnP device ids. Empty list if enumeration is unavailable."""
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command",
             "Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | "
             "Select-Object -ExpandProperty InstanceId"],
            capture_output=True, text=True, timeout=15,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except (OSError, subprocess.SubprocessError):
        return []
    return [line.strip() for line in result.stdout.splitlines() if line.strip()]


def detect(ids: list[str] | None = None) -> Connection:
    """Classify how the controller is attached.

    Bluetooth is checked first because the pad can be on Bluetooth while a
    completely unrelated gamepad sits on USB.
    """
    ids = _enumerate() if ids is None else ids
    if not ids:
        return Connection(Link.UNKNOWN, "could not enumerate devices")

    for entry in ids:
        if BLUETOOTH_ID.search(entry):
            return Connection(Link.BLUETOOTH, "Xbox profile over BR/EDR", entry)
    for entry in ids:
        match = BLUETOOTH_ANY.search(entry)
        if match:
            mac = ":".join(match.group(1)[i:i + 2] for i in range(0, 12, 2))
            return Connection(Link.BLUETOOTH, mac, entry)

    for entry in ids:
        if WIRED_ID.search(entry) and GAMEPAD_HINT.search(entry):
            return Connection(Link.WIRED, "Xbox 360 profile", entry)
    for entry in ids:
        if WIRED_ID.search(entry):
            return Connection(Link.WIRED, "Xbox 360 profile", entry)

    # A gamepad on USB that is not the known wired identity is most likely the
    # 2.4 GHz receiver, which enumerates as its own device.
    for entry in ids:
        if not GAMEPAD_HINT.search(entry):
            continue
        match = XINPUT_ANY.search(entry)
        if match:
            vid, pid = match.group(1).upper(), match.group(2).upper()
            return Connection(Link.DONGLE, f"VID {vid} PID {pid}", entry)

    return Connection(Link.ABSENT)


def describe_for_config() -> str:
    """A note about which link carries configuration.

    Configuration always runs over the BLE peripheral regardless of how the pad
    is being played, so a wired or dongle connection is not a problem.
    """
    return ("Configuration always runs over the separate Bluetooth LE peripheral, "
            "whichever link you play on. Both can be live at once.")
