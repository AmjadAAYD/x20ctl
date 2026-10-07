"""Local reusable sequences. Never detects, connects to, or writes a controller."""
import json
from pathlib import Path
from uuid import uuid4

from x20ctl.controllers import get_profile
from .settings import macro_from_ui, number, TARGETS

MAX_FILE_BYTES = 100_000
MAX_SAVED_MACROS = 200


def validate_macro(value):
    if not isinstance(value, dict) or value.get("format") != "x20ctl-macro" or type(value.get("version")) is not int or value["version"] != 1:
        raise ValueError("Choose an X20CTL macro file (version 1)")
    if set(value) - {"id", "format", "version", "name", "sourceModel", "loopMs", "steps"}:
        raise ValueError("This file contains unsupported macro options")
    name = value.get("name")
    if not isinstance(name, str) or not name.strip() or len(name) > 80:
        raise ValueError("Macro names must contain 1–80 characters")
    model = get_profile(value.get("sourceModel"))
    if not model.macro_slots:
        raise ValueError("This source model has no programmable macro controls")
    rows = value.get("steps")
    if not isinstance(rows, list) or not rows:
        raise ValueError("Choose a nonempty macro sequence")
    if len(rows) > 47:
        raise ValueError("Macro exceeds the 47-entry editing limit")
    clean = []
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("Invalid macro step")
        if set(row) - {"id", "buttons", "leftStick", "rightStick", "durationMs", "intervalMs"}:
            raise ValueError("This step contains unsupported actions")
        buttons = row.get("buttons")
        if not isinstance(buttons, list) or any(not isinstance(key, str) or key not in TARGETS or key in ("CAPTURE", "TURBO") for key in buttons) or len(set(buttons)) != len(buttons):
            raise ValueError("Unsupported or repeated macro input")
        clean.append({
            "id": str(uuid4()), "buttons": list(buttons),
            "leftStick": number(row.get("leftStick"), "Left stick", 0, 8, integer=True),
            "rightStick": number(row.get("rightStick"), "Right stick", 0, 8, integer=True),
            "durationMs": number(row.get("durationMs"), "Hold", 5, 327675, integer=True),
            "intervalMs": number(row.get("intervalMs"), "Pause", 0, 327675, integer=True),
        })
    loop = number(value.get("loopMs"), "Loop interval", 0, 20475, integer=True)
    # Validate against the established editor format, not a preview device protocol.
    macro_from_ui(clean, loop_ms=loop)
    return {"format": "x20ctl-macro", "version": 1, "name": name.strip(),
            "sourceModel": model.id, "loopMs": loop, "steps": clean}


class MacroLibrary:
    def __init__(self, directory):
        self.path = Path(directory) / "macro-library.json"

    def load(self):
        if not self.path.exists():
            return []
        if self.path.stat().st_size > 20_000_000:
            raise ValueError("Macro library is too large")
        values = json.loads(self.path.read_text(encoding="utf-8-sig"))
        if not isinstance(values, list) or len(values) > MAX_SAVED_MACROS:
            raise ValueError("Invalid macro library")
        result = []
        for item in values:
            validated = validate_macro(item)
            identity = item.get("id")
            if not isinstance(identity, str) or not identity or any(entry["id"] == identity for entry in result):
                raise ValueError("Invalid macro library identity")
            result.append({**validated, "id": identity})
        return result

    def change(self, action, payload):
        entries = self.load()
        if action == "save":
            macro = validate_macro(payload.get("entry"))
            if len(entries) >= MAX_SAVED_MACROS:
                raise ValueError("The library holds 200 sequences. Remove one before saving another.")
            entries.append({**macro, "id": str(uuid4())})
        elif action == "delete":
            identity = payload.get("id")
            if not any(entry["id"] == identity for entry in entries):
                raise ValueError("Macro library entry not found")
            entries = [entry for entry in entries if entry["id"] != identity]
        else:
            raise ValueError("Unknown macro library action")
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.path.with_suffix(".json.tmp")
        temporary.write_text(json.dumps(entries, indent=2, allow_nan=False), encoding="utf-8")
        temporary.replace(self.path)
        return entries
