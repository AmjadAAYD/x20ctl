"""Working on more than one controller.

The mask layout isn't a constant. It's decided by whatever macro key list a
pad reports, so hardcoding the positions observed on one unit would silently
produce wrong macros on any device that reports a different list.

Also covers turning failures into advice not library exception text.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from x20ctl import protocol as p
from x20ctl.diagnose import (
    BLUETOOTH_OFF, NO_CONTROLLER, OUT_OF_RANGE, UNKNOWN, WRONG_DEVICE, diagnose,
)


# -- layouts ---------------------------------------------------------------

def test_default_layout_matches_the_observed_x20():
    """The derived layout must reproduce what was measured on real hardware."""
    layout = p.DEFAULT_LAYOUT
    assert layout.digital[p.Key.A] == 12
    assert layout.digital[p.Key.B] == 13
    assert layout.digital[p.Key.DPAD_DOWN] == 8
    assert layout.digital[p.Key.R3] == 21
    assert layout.analog[p.Key.LSTICK_ANALOG] == 0
    assert layout.analog[p.Key.RSTICK_ANALOG] == 4
    assert layout.neutral == 0x88
    assert layout.width == 24


def test_layout_is_derived_not_assumed():
    """A pad reporting a different order must get different bit positions.

    This is the whole point: the same code has to work on hardware nobody has
    tested it against.
    """
    other = p.layout_from_key_list([1, 2, 3, 4, 18, 19])
    assert other.digital[p.Key.A] == 0, "A is first in this list, so lowest bit"
    assert other.digital[p.Key.Y] == 3
    assert other.analog[p.Key.LSTICK_ANALOG] == 4
    assert other.neutral != p.DEFAULT_LAYOUT.neutral


def test_layout_without_sticks_has_no_analog_nibbles():
    layout = p.layout_from_key_list([1, 2, 3, 4])
    assert layout.analog == {}
    assert layout.neutral == 0, "nothing to hold idle"
    assert p.mask_for([p.Key.A], layout) == 1


def test_masks_follow_the_layout_they_are_given():
    other = p.layout_from_key_list([1, 2, 3, 4, 18, 19])
    assert p.mask_for([p.Key.A], other) != p.mask_for([p.Key.A])
    assert p.describe_mask(p.mask_for([p.Key.A], other), other) == ["A"]


def test_unknown_key_codes_still_consume_their_place():
    """A pad listing a key we have no name for must not shift everything else."""
    layout = p.layout_from_key_list([1, 200, 2])
    assert layout.digital[p.Key.A] == 0
    assert layout.digital[p.Key.B] == 2, "code 200 still occupies bit 1"


def test_select_and_start_are_digital_buttons_now():
    """93 and 94 are Select and Start on the X20, not placeholders."""
    layout = p.layout_from_key_list([1, 93, 94, 2])
    assert layout.digital[p.Key.SELECT] == 1
    assert layout.digital[p.Key.START] == 2
    assert layout.digital[p.Key.B] == 3, "Select and Start each take one bit"


def test_key_that_a_pad_omits_is_rejected_for_that_pad():
    limited = p.layout_from_key_list([1, 2])
    p.mask_for([p.Key.A], limited)
    try:
        p.mask_for([p.Key.X], limited)
    except ValueError as exc:
        assert "not macro-capable on this controller" in str(exc)
        return
    raise AssertionError("a key the pad does not list must be refused")


def test_stick_direction_rejected_on_a_pad_without_sticks():
    layout = p.layout_from_key_list([1, 2, 3, 4])
    try:
        p.mask_for([p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.UP)], layout)
    except ValueError as exc:
        assert "not available" in str(exc)
        return
    raise AssertionError("a stick the pad does not list must be refused")


def test_layout_round_trips_through_describe():
    for keys in ([1, 2, 18], [18, 19, 1], [5, 6, 7, 8], list(p.X20_MACRO_KEYS)):
        layout = p.layout_from_key_list(keys)
        for key in layout.digital:
            assert p.describe_mask(p.mask_for([key], layout), layout) == [key.name]


# The reply an X20 actually sends to HOST_MENU kind 5, captured from hardware.
# body.declared is 11, the byte count; data[0] is 0x12, the key count. Passing
# the former to decode_key_list raises, and macro_layout then falls back to
# DEFAULT_LAYOUT without telling anyone.
MACRO_SUPPORT_REPLY = bytes.fromhex("1212130d8401880b825d82")


def test_live_macro_support_reply_decodes():
    """Guards a caller bug that made every real pad fall back silently.

    The decode was never wrong; it was being handed a byte count where a key
    count belongs. This asserts against the captured record, so the unit test
    and the live path agree on what decode_key_list is fed.
    """
    keys = p.decode_key_list(MACRO_SUPPORT_REPLY)
    assert tuple(keys) == p.X20_MACRO_KEYS, "the pad's own list is the layout"

    with_byte_count = bytes([len(MACRO_SUPPORT_REPLY)]) + MACRO_SUPPORT_REPLY
    try:
        p.decode_key_list(with_byte_count)
    except ValueError:
        return
    raise AssertionError("prefixing the byte count must not decode as a key list")


def test_macro_layout_uses_the_pad_not_the_fallback():
    """macro_layout must derive from the reply, not quietly return the default."""
    import asyncio

    from x20ctl.client import X20

    pad = X20("00:00:00:00:00:00")

    async def fake_read_body(opcode, payload=b""):
        assert payload == bytes([0, p.MenuKind.MACRO_SUPPORT])
        return p.Body(declared=len(MACRO_SUPPORT_REPLY), data=MACRO_SUPPORT_REPLY)

    pad.read_body = fake_read_body
    layout = asyncio.run(pad.macro_layout())

    assert layout is not p.DEFAULT_LAYOUT, "falling back means the decode failed"
    assert layout.key_list == p.X20_MACRO_KEYS
    assert len(layout.digital) == 16 and len(layout.analog) == 2


# -- diagnoses -------------------------------------------------------------

def test_bluetooth_off_is_recognised():
    real = ("('Bluetooth radio is not powered on. Turn on Bluetooth and try "
            "again.', <BleakBluetoothNotEnabledError>)")
    assert diagnose(real) is BLUETOOTH_OFF


def test_missing_device_is_recognised():
    assert diagnose("Device with address 98:B6:ED:E3:15:C4 was not found.") \
        is NO_CONTROLLER
    assert diagnose("no controller found") is NO_CONTROLLER


def test_timeout_is_recognised():
    assert diagnose("Operation timed out") is OUT_OF_RANGE
    assert diagnose("device disconnected") is OUT_OF_RANGE


def test_wrong_peripheral_is_recognised():
    assert diagnose("does not expose the configuration service") is WRONG_DEVICE


def test_anything_else_still_gives_advice():
    finding = diagnose("some library detail nobody should read")
    assert finding is UNKNOWN
    assert finding.advice, "an unknown failure still needs a next step"


def test_a_diagnosis_never_leaks_the_raw_text():
    raw = "<BleakDeviceNotFoundError object at 0x7f9>"
    assert "Bleak" not in str(diagnose(raw))
    assert "0x7f9" not in str(diagnose(raw))


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  PASS  {name}")
            except AssertionError as exc:
                failures += 1
                print(f"  FAIL  {name}: {exc}")
    print(f"\n{'all passed' if not failures else f'{failures} failed'}")
    sys.exit(1 if failures else 0)
