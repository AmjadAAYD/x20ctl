"""Receiver evidence is independent of gameplay binding and configuration support."""

import re

INPUT_MODELS = frozenset({"d10", "x15", "x05", "x10"})


def known_other_model_name(name):
    """Exclude explicitly named unsupported configurators from the X20 writer.

    This is a negative guard, not positive model identification.
    """
    return bool(
        re.search(
            r"\b(?:x0?5(?:\s*pro)?|x10|x15|d10|x20\s*pro|qmacro)\b", name or "", re.I
        )
    )


STANDARD_CONTROLS = (
    "A",
    "B",
    "X",
    "Y",
    "DPAD_UP",
    "DPAD_DOWN",
    "DPAD_LEFT",
    "DPAD_RIGHT",
    "LB",
    "RB",
    "LT",
    "RT",
    "L3",
    "R3",
    "SELECT",
    "START",
)


def identify_receiver(row):
    pair = row.get("vid"), row.get("pid")
    model, status = {
        (0x2345, 0xE062): ("d10", "verified_receiver"),
        (0x1A34, 0xF517): ("x15", "experimental_receiver"),
        (0x045E, 0x028E): (None, "ambiguous_compatibility"),
    }.get(pair, (None, "unknown"))
    return {
        "model": model,
        "status": status,
        "scope": "receiver_identity",
        "controller_link_verified": False,
        "configuration_supported": False,
    }
