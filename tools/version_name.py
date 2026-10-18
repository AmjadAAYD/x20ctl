"""Version names for the release tooling (owner decision, 18 Oct 2026).

  python tools/version_name.py v0.017          ->  V0.017                 (the name people see)
  python tools/version_name.py v4.1.0-preview.6 ->  V0.015                (an old tag, from RELEASES/versions.json)
  python tools/version_name.py --numeric v0.017.1 -> 0.17.1               (what .NET needs for its version numbers)

The new numbering is V<super>.<release>[.<fix>] with a three-digit release number, tagged the same way (v0.017, v0.017.1, v1.000),
so a new tag can never collide with an old one. Exits non-zero for anything it can't name.
"""
import json
import re
import sys
from pathlib import Path

NEW = re.compile(r"^[vV]?(\d+)\.(\d{3})(?:\.(\d+))?$")


def legacy() -> dict:
    data = json.loads(Path(__file__).resolve().parents[1].joinpath("RELEASES", "versions.json").read_text(encoding="utf-8"))
    return {item["tag"]: item["name"] for item in data["legacy"]}


def name(version: str) -> str:
    v = version.strip()
    m = NEW.match(v)
    if m:
        fix = int(m.group(3) or 0)
        return f"V{int(m.group(1))}.{m.group(2)}" + (f".{fix}" if fix else "")
    tag = "v" + v.lstrip("vV")
    names = legacy()
    if tag in names:
        return names[tag]
    raise SystemExit(f"{version} is neither in the V0.016 form nor an old tag listed in RELEASES/versions.json")


def numeric(version: str) -> str:
    m = NEW.match(version.strip())
    if not m:
        raise SystemExit(f"{version} is not in the V0.016 form")
    return f"{int(m.group(1))}.{int(m.group(2))}.{int(m.group(3) or 0)}"


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[:1] == ["--numeric"]:
        print(numeric(args[1]))
    elif args[:1] == ["--is-new"]:
        sys.exit(0 if NEW.match(args[1].strip()) else 1)
    else:
        print(name(args[0]))
