"""Work out how the controller is physically connected.

The pad presents a different identity on each transport, so the way to tell them
apart is to enumerate what Windows sees and match on the interface rather than
on the USB ids, which are cloned from Microsoft on every link.

Observed on an EasySMX X20:

    wired USB          USB\\VID_045E&PID_028E   REV_0110, xusb22 + HID
    2.4 GHz receiver   USB\\VID_045E&PID_028E   REV_0110, xusb22 + HID
    Bluetooth Classic  BTHENUM\\...045E&02FD    BR/EDR HID profile

**Wired and the receiver are indistinguishable.** They present the same vendor
id, the same product id, the same revision, the same driver, and the same device
tree shape. The receiver is completely transparent: it makes the pad look like a
wired Xbox 360 controller. Compared side by side on the same machine, the only
difference was the interface group number, which is an enumeration artefact
rather than a property of the link.

So this module reports Link.USB for both and says so, rather than picking one.
An earlier version claimed "wired" while the pad was on the receiver, which is
worse than admitting the ambiguity.

Bluetooth is checked last, because the pad remains listed as a present Bluetooth
device while its configuration link is up, even when you are playing over USB.
"""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass
from enum import Enum


class Link(Enum):
    # Wired and the 2.4 GHz receiver are indistinguishable on this hardware,
    # so they share one value rather than pretending we can tell.
    USB = "USB or 2.4 GHz receiver"
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
    """Classify how the controller is attached for play.

    USB is checked before Bluetooth, which is the opposite of the obvious
    order. The reason is that this pad stays listed as a present Bluetooth
    device while its configuration link is up, even when you are playing over
    the 2.4 GHz receiver. A gamepad physically present on USB is much stronger
    evidence of the active link than a Bluetooth entry that may only reflect
    pairing.
    """
    ids = _enumerate() if ids is None else ids
    if not ids:
        return Connection(Link.UNKNOWN, "could not enumerate devices")

    # 1. The pad's USB identity. Cannot tell a cable from the receiver here.
    for entry in ids:
        if WIRED_ID.search(entry):
            return Connection(
                Link.USB, "cable and receiver look identical to Windows", entry)

    # 2. Some other gamepad on USB. A different vendor's receiver would land
    #    here, and unlike the case above we can at least name it.
    for entry in ids:
        if not GAMEPAD_HINT.search(entry):
            continue
        match = XINPUT_ANY.search(entry)
        if match:
            vid, pid = match.group(1).upper(), match.group(2).upper()
            return Connection(Link.DONGLE, f"VID {vid} PID {pid}", entry)

    # 3. Bluetooth last, since its entry can outlive the playing connection.
    for entry in ids:
        if BLUETOOTH_ID.search(entry):
            return Connection(Link.BLUETOOTH, "Xbox profile over BR/EDR", entry)
    for entry in ids:
        match = BLUETOOTH_ANY.search(entry)
        if match:
            mac = ":".join(match.group(1)[i:i + 2] for i in range(0, 12, 2))
            return Connection(Link.BLUETOOTH, mac, entry)

    return Connection(Link.ABSENT)


def describe_for_config() -> str:
    """A note about which link carries configuration.

    Configuration always runs over the BLE peripheral regardless of how the pad
    is being played, so a wired or dongle connection is not a problem.
    """
    return ("Configuration always runs over the separate Bluetooth LE peripheral, "
            "whichever link you play on. Both can be live at once.")
