"""Turn connection failures into something a person can act on.

Raw exception text is written for whoever wrote the library, not for whoever is
holding the controller. "BleakDeviceNotFoundError" and a repr of an internal
object tell a user nothing about what to do next.

Each entry pairs a short statement of what happened with a concrete next step.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Diagnosis:
    headline: str
    advice: str
    # A precondition the user has simply not met is not a fault. Marking those
    # calmly keeps genuine failures visually distinct from "you are not using
    # this part right now".
    expected: bool = False

    def __str__(self) -> str:
        return f"{self.headline}. {self.advice}" if self.advice else self.headline


NO_CONTROLLER = Diagnosis(
    "Controller not found over Bluetooth",
    "Settings are configured over a separate Bluetooth link, whichever way you "
    "play. Turn the pad on and pair it if you want to change settings. "
    "The input tester works without it.",
    expected=True)

BLUETOOTH_OFF = Diagnosis(
    "Bluetooth is off",
    "Only settings need it. The controller exposes its configuration on a "
    "separate Bluetooth link, so playing over USB or the receiver is "
    "unaffected. Turn Bluetooth on to change settings, or use the input "
    "tester, which does not need it.",
    expected=True)

OUT_OF_RANGE = Diagnosis(
    "Controller is not responding",
    "It may be asleep or out of range. Press a button on it, then reconnect.")

WRONG_DEVICE = Diagnosis(
    "That device is not a configurable controller",
    "The pad exposes a separate Bluetooth link for settings. Make sure the "
    "controller is on and paired.")

BUSY = Diagnosis(
    "Controller is connected to something else",
    "Close any other app configuring it, or unpair it from your phone.")

UNKNOWN = Diagnosis(
    "Could not reach the controller",
    "Turn it off and on, then press Connect.")


# Matched against the lowercased text of the exception. Order matters: the more
# specific phrases have to be tested before the general ones.
_PATTERNS = (
    ("bluetooth radio is not powered", BLUETOOTH_OFF),
    ("bluetooth is not", BLUETOOTH_OFF),
    ("radio is off", BLUETOOTH_OFF),
    ("was not found", NO_CONTROLLER),
    ("device not found", NO_CONTROLLER),
    ("no controller found", NO_CONTROLLER),
    ("not expose the configuration service", WRONG_DEVICE),
    ("access denied", BUSY),
    ("in use", BUSY),
    ("unreachable", OUT_OF_RANGE),
    ("timed out", OUT_OF_RANGE),
    ("timeout", OUT_OF_RANGE),
    ("disconnected", OUT_OF_RANGE),
)


def diagnose(error) -> Diagnosis:
    """Classify a failure into something worth showing a user."""
    text = str(error).lower()
    for phrase, diagnosis in _PATTERNS:
        if phrase in text:
            return diagnosis
    return UNKNOWN
