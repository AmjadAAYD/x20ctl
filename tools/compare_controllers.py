"""Compare what two controllers report about themselves.

    python tools/compare_controllers.py x20 path/to/unknown-controllers.json
    python tools/compare_controllers.py capture-a.json capture-b.json --json

Each side is `x20` (the recorded X20 on firmware 1.23), a fingerprint file, the
app's unknown-controllers.json (use `file.json#2` for an entry other than the
last) or a capability-discovery result (`#n` picks a profile). Reads files
only; no controller is contacted.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from x20ctl.scanning import fingerprint  # noqa: E402


def load(spec: str) -> dict:
    if spec.lower() == "x20":
        return fingerprint.X20_REFERENCE
    path, _, pick = spec.partition("#")
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if isinstance(data, dict) and "profiles" in data:
        profiles = data["profiles"]
        if not profiles:
            raise SystemExit(f"{path}: no complete exchange in this capture")
        return fingerprint.from_discovery(profiles[int(pick) if pick else -1])
    if isinstance(data, list):
        if not data:
            raise SystemExit(f"{path}: empty")
        data = data[int(pick) if pick else -1]
    if "identity" not in data:
        raise SystemExit(f"{path}: not a fingerprint")
    return data


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("a")
    parser.add_argument("b")
    parser.add_argument("--json", action="store_true", help="print the differences as JSON")
    args = parser.parse_args(argv)
    differences = fingerprint.compare(load(args.a), load(args.b))
    if args.json:
        print(json.dumps(differences, indent=2))
    else:
        print(fingerprint.render(differences, "A", "B"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
