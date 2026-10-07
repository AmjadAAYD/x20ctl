"""Save files: named sets of controller settings that can be applied in one go.

A profile is plain JSON, so it can be edited by hand, kept in version control, or
shared. Applying one writes every setting it names and leaves the rest alone.

    store = ProfileStore()
    profile = Profile(name="Save file 1")
    profile.macros["M1"] = MacroSpec(keys="A+B", hold_ms=100)
    store.save(profile)

    async with X20(address) as pad:
        report = await profile.apply(pad)

Profiles are a software concept. The controller has four fixed macro slots and
knows nothing about save files; switching profiles rewrites those slots.
"""

from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone

from . import protocol as p
from .controllers import get_profile

SLOTS = get_profile("x20").macro_slots  # Legacy X20 API only.

DEFAULT_DIR = os.path.join(
    os.environ.get("APPDATA") or os.path.expanduser("~"), "x20ctl", "profiles"
)


@dataclass
class MacroSpec:
    """One macro.

    `keys` uses ',' to separate steps in time and '+' to join keys within a
    step, so "A,B" is A then B while "A+B" is both at once.
    """

    keys: str
    hold_ms: int = 100
    gap_ms: int = 60
    loop_ms: int = 0

    def steps(self):
        """The macro as protocol steps. Raises ValueError if it won't build."""
        if self.hold_ms <= 0 or self.gap_ms < 0:
            raise ValueError("durations must be positive")
        if self.hold_ms % 5 or self.gap_ms % 5 or self.loop_ms % 5:
            raise ValueError("durations must be multiples of 5 ms")
        return p.parse_sequence(self.keys, self.hold_ms, self.gap_ms)

    def validate(self) -> None:
        """Check the spec builds before anything is sent to hardware."""
        steps = self.steps()
        # Round-trips through the real builder, so a saved profile can't hold
        # something that would fail at apply time, including one too long to fit.
        p.build_macro_writes(
            p.build_macro_payload(steps, loop_interval_ms=self.loop_ms), slot=0)

    def describe(self) -> str:
        body = p.describe_sequence(self.keys, self.hold_ms, self.gap_ms)
        bits = []
        if ":" not in self.keys:
            bits.append(f"{self.hold_ms}ms hold")
        if self.loop_ms:
            bits.append(f"loops every {self.loop_ms}ms")
        return f"{body}  ({', '.join(bits)})" if bits else body


@dataclass
class Profile:
    name: str
    macros: dict[str, MacroSpec | None] = field(default_factory=dict)
    vibration: int | None = None
    updated: str = ""
    # When true, applying clears any slot this profile doesn't define, so the
    # controller ends up matching the save file exactly.
    #
    # This defaults to true because that's what a save file means. An earlier
    # version defaulted to false, to avoid disturbing assignments made with
    # button combinations on the pad, which are invisible to the protocol. That
    # was the wrong trade: it meant switching profiles left the previous
    # profile's macros on the controller, so the pad silently disagreed with
    # what the app displayed.
    #
    # Set it false per-profile if you have on-pad assignments to preserve.
    clear_undefined: bool = True
    controller_id: str = "x20"

    def __post_init__(self):
        slots = get_profile(self.controller_id).macro_slots
        if not self.macros:
            self.macros = dict.fromkeys(slots)
        if any(slot not in slots for slot in self.macros):
            raise ValueError("Macro slot does not belong to this controller")

    # -- serialisation ---------------------------------------------------

    def to_dict(self) -> dict:
        return {
            "schema_version": 2,
            "controller_id": self.controller_id,
            "name": self.name,
            "vibration": self.vibration,
            "updated": self.updated,
            "clear_undefined": self.clear_undefined,
            "macros": {
                slot: (asdict(spec) if spec else None)
                for slot, spec in self.macros.items()
            },
        }

    @classmethod
    def from_dict(cls, data: dict) -> "Profile":
        controller_id = data.get("controller_id", "x20")
        slots = get_profile(controller_id).macro_slots
        if data.get("schema_version", 2) != 2:
            raise ValueError("Unsupported profile version")
        macros: dict[str, MacroSpec | None] = {slot: None for slot in slots}
        for slot, spec in (data.get("macros") or {}).items():
            if slot not in slots:
                raise ValueError(f"unknown slot {slot!r}; expected one of {slots}")
            macros[slot] = MacroSpec(**spec) if spec else None
        return cls(
            controller_id=controller_id,
            name=data.get("name") or "unnamed",
            macros=macros,
            vibration=data.get("vibration"),
            updated=data.get("updated", ""),
            clear_undefined=bool(data.get("clear_undefined", True)),
        )

    def validate(self) -> None:
        if any(slot not in get_profile(self.controller_id).macro_slots for slot in self.macros):
            raise ValueError("Macro slot does not belong to this controller")
        if self.vibration is not None and not 0 <= self.vibration <= 100:
            raise ValueError("vibration must be 0-100")
        for slot, spec in self.macros.items():
            if spec is None:
                continue
            try:
                spec.validate()
            except ValueError as exc:
                raise ValueError(f"{slot}: {exc}") from None

    # -- applying --------------------------------------------------------

    async def apply(self, pad, *, on_step=None) -> list[str]:
        """Write this profile to a connected controller.

        Settings the pad doesn't expose are skipped rather than attempted, so
        the same profile is safe across different hardware. Returns a list of
        human-readable lines describing what happened.
        """
        self.validate()
        if self.controller_id != "x20":
            raise ValueError("Configuration backend is unverified for this controller")
        if getattr(pad, "controller_id", "x20") != self.controller_id:
            raise ValueError("Profile controller does not match connected controller")
        caps = await pad.capabilities()
        report: list[str] = []

        def note(line: str) -> None:
            report.append(line)
            if on_step:
                on_step(line)

        if self.vibration is not None:
            if caps.motors:
                await pad.set_vibration(self.vibration)
                note(f"vibration set to {self.vibration}%")
            else:
                note("vibration skipped: not exposed by this controller")

        if not caps.macros:
            note("macros skipped: not exposed by this controller")
            return report

        for index, slot in enumerate(get_profile(self.controller_id).macro_slots, start=1):
            spec = self.macros.get(slot)
            if spec is not None:
                await pad.set_macro(
                    index, spec.keys, hold_ms=spec.hold_ms,
                    gap_ms=spec.gap_ms, loop_ms=spec.loop_ms,
                )
                note(f"{slot} set to {spec.describe()}")
            elif self.clear_undefined:
                await pad.clear_macro(index)
                note(f"{slot} cleared")
            else:
                note(f"{slot} left as it was")
        return report


def slugify(name: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
    return slug or "profile"


class ProfileStore:
    """Profiles on disk, one JSON file each."""

    def __init__(self, directory: str = DEFAULT_DIR):
        self.directory = directory

    def _path(self, name: str) -> str:
        return os.path.join(self.directory, f"{slugify(name)}.json")

    def list(self) -> list[Profile]:
        if not os.path.isdir(self.directory):
            return []
        out = []
        for entry in sorted(os.listdir(self.directory)):
            if entry.endswith(".json"):
                try:
                    out.append(self._read(os.path.join(self.directory, entry)))
                except (ValueError, TypeError, json.JSONDecodeError):
                    continue    # a malformed file should not break the listing
        return out

    def _read(self, path: str) -> Profile:
        with open(path, encoding="utf-8") as fh:
            return Profile.from_dict(json.load(fh))

    def load(self, name: str) -> Profile:
        path = self._path(name)
        if not os.path.exists(path):
            raise FileNotFoundError(f"no profile named {name!r}")
        return self._read(path)

    def save(self, profile: Profile) -> str:
        profile.validate()
        profile.updated = datetime.now(timezone.utc).isoformat(timespec="seconds")
        os.makedirs(self.directory, exist_ok=True)
        path = self._path(profile.name)
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(profile.to_dict(), fh, indent=2)
            fh.write("\n")
        return path

    def delete(self, name: str) -> None:
        path = self._path(name)
        if os.path.exists(path):
            os.remove(path)
