"""Translate UI values through the verified protocol, never through guessed packets."""

from __future__ import annotations

import math
import time
import uuid
from copy import deepcopy
from dataclasses import replace

from x20ctl import protocol as p

KEYS = [p.Key(k).name for k in p.CHANGEKEY_DEFAULT_SOURCES]
TARGETS = [name for name in KEYS if name not in ("CAPTURE", "TURBO")]
CATEGORIES = [
    "remaps",
    "stickCurves",
    "triggerCurves",
    "vibration",
    "idleTimeoutMinutes",
    "M1",
    "M2",
    "M3",
    "M4",
]


def number(value, label, low, high, *, integer=False):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} must be a number")
    if not math.isfinite(value) or not low <= value <= high:
        raise ValueError(f"{label} must be between {low} and {high}")
    if integer and int(value) != value:
        raise ValueError(f"{label} must be a whole number")
    return int(value) if integer else value


def curve_to_ui(curve):
    scale = curve.max_progress
    return {
        "innerDeadzone": curve.inner_deadzone * 100 / scale,
        "outerDeadzone": curve.outer_deadzone * 100 / scale,
        "p1": dict(zip(("x", "y"), [v * 100 / 255 for v in curve.point1])),
        "p2": dict(zip(("x", "y"), [v * 100 / 255 for v in curve.point2])),
        "preset": "linear" if curve.is_linear else "custom",
    }


def curve_from_ui(value, original):
    inner = number(value["innerDeadzone"], "Inner deadzone", 0, 100)
    outer = number(value["outerDeadzone"], "Outer deadzone", 0, 100)
    points = [
        tuple(
            round(number(value[key][axis], "Curve point", 0, 100) * 255 / 100)
            for axis in ("x", "y")
        )
        for key in ("p1", "p2")
    ]
    curve = replace(
        original,
        inner_deadzone=round(inner * original.max_progress / 100),
        outer_raw=original.max_progress - round(outer * original.max_progress / 100),
        point1=points[0],
        point2=points[1],
    )
    curve.validate()
    return curve


def macro_from_ui(rows, layout=None, loop_ms=0):
    if not isinstance(rows, list) or len(rows) > p.MAX_MACRO_ENTRIES:
        raise ValueError("Macro exceeds the controller's 47-entry limit")
    entries = []
    for row in rows:
        buttons = row["buttons"]
        if not isinstance(buttons, list) or any(k not in TARGETS for k in buttons):
            raise ValueError("Unsupported macro button")
        inputs = [p.Key[k] for k in buttons]
        for field, stick in (
            ("leftStick", p.Key.LSTICK_ANALOG),
            ("rightStick", p.Key.RSTICK_ANALOG),
        ):
            direction = number(row[field], "Stick direction", 0, 8, integer=True)
            if direction:
                inputs.append(p.StickInput(stick, p.Direction(direction)))
        hold = number(row["durationMs"], "Hold", 5, 327675, integer=True)
        gap = number(row["intervalMs"], "Gap", 0, 327675, integer=True)
        if hold % 5 or gap % 5:
            raise ValueError("Macro timing must use multiples of 5 ms")
        entries.append(p.MacroStep(p.mask_for(inputs, layout), hold))
        if gap:
            entries.append(p.MacroStep.released(gap))
    loop_ms = number(loop_ms, "Loop interval", 0, 20475, integer=True)
    p.build_macro_payload(entries, loop_interval_ms=loop_ms)
    return entries


def macro_to_ui(program, layout=None):
    if program is None:
        return []
    rows = []
    for index, entry in enumerate(program.steps):
        tokens = p.describe_mask(entry.mask, layout)
        row = {
            "id": f"read-{index}",
            "buttons": [],
            "leftStick": 0,
            "rightStick": 0,
            "durationMs": entry.duration_ms,
            "intervalMs": 0,
        }
        for token in tokens:
            item = p.parse_token(token)
            if isinstance(item, p.StickInput):
                row[
                    "leftStick" if item.stick == p.Key.LSTICK_ANALOG else "rightStick"
                ] = int(item.direction)
            else:
                row["buttons"].append(item.name)
        # Preserve standalone idle entries, including an initial delay.
        if not tokens and rows:
            rows[-1]["intervalMs"] += entry.duration_ms
        else:
            rows.append(row)
    return rows


def validate_profile(profile):
    if not isinstance(profile, dict) or not isinstance(profile.get("name"), str):
        raise ValueError("Profile must have a name")
    if not 1 <= len(profile["name"].strip()) <= 100:
        raise ValueError("Profile name must be 1–100 characters")
    if profile.get("schemaVersion") != 2:
        raise ValueError("Unsupported profile version")
    if not isinstance(profile.get("id"), str) or not 1 <= len(profile["id"]) <= 100:
        raise ValueError("Profile must have a valid identifier")
    categories = profile.get("categories", CATEGORIES)
    if not isinstance(categories, list) or any(c not in CATEGORIES for c in categories):
        raise ValueError("Unsupported profile category")
    remaps = profile.get("remaps", {})
    if not isinstance(remaps, dict) or any(
        k not in TARGETS or v not in TARGETS for k, v in remaps.items()
    ):
        raise ValueError("Unsupported remapping")
    if any(remaps.get(k, k) != k for k in ("SELECT", "START")):
        raise ValueError(
            "Select and Start are remap targets, not supported remap sources"
        )
    number(profile["vibration"], "Vibration", 0, 100, integer=True)
    timeout = number(
        profile["idleTimeoutMinutes"], "Sleep timeout", 0, 1092, integer=True
    )
    p.encode_shutdown(timeout or None)
    for field, scale in (
        ("stickCurves", p.STICK_MAX_PROGRESS),
        ("triggerCurves", p.TRIGGER_MAX_PROGRESS),
    ):
        for side in ("left", "right"):
            curve_from_ui(
                profile[field][side],
                p.Curve(0, 0, (85, 85), (170, 170), max_progress=scale),
            )
    loops = profile.get("macroLoops", {})
    for slot in ("M1", "M2", "M3", "M4"):
        rows = profile["macros"][slot]
        ids = [row.get("id") for row in rows] if isinstance(rows, list) else []
        if any(not isinstance(value, str) or not value for value in ids) or len(set(ids)) != len(ids):
            raise ValueError(f"{slot}: macro steps need unique identifiers")
        macro_from_ui(profile["macros"][slot], loop_ms=loops.get(slot, 0))
    return profile


def import_profile(value):
    """Import native v2 or legacy v1 without silently applying missing settings."""
    if not isinstance(value, dict):
        raise ValueError("Expected a profile object")
    if value.get("schemaVersion") == 2:
        result = deepcopy(value)
        result["id"] = str(uuid.uuid4())
        return validate_profile(result)
    if "schemaVersion" in value or not isinstance(value.get("macros"), dict):
        raise ValueError("Unsupported profile format")
    from x20ctl.profiles import Profile

    legacy = Profile.from_dict(value)
    legacy.validate()
    linear = curve_to_ui(p.Curve(0, 0, (85, 85), (170, 170)))
    result = {
        "schemaVersion": 2,
        "id": str(uuid.uuid4()),
        "createdAt": int(time.time() * 1000),
        "name": legacy.name,
        "remaps": {k: k for k in TARGETS},
        "stickCurves": {side: deepcopy(linear) for side in ("left", "right")},
        "triggerCurves": {side: deepcopy(linear) for side in ("left", "right")},
        "vibration": legacy.vibration if legacy.vibration is not None else 70,
        "idleTimeoutMinutes": 10,
        "macros": {},
        "macroLoops": {},
        "categories": [],
    }
    if legacy.vibration is not None:
        result["categories"].append("vibration")
    for slot, spec in legacy.macros.items():
        result["macros"][slot] = (
            macro_to_ui(p.MacroProgram(spec.steps())) if spec else []
        )
        result["macroLoops"][slot] = spec.loop_ms if spec else 0
        if spec or legacy.clear_undefined:
            result["categories"].append(slot)
    return validate_profile(result)
