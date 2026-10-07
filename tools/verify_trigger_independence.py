"""Compare isolated LT and RT renders without accessing a screen or device.

The browser review supplies screenshot-relative bounds in geometry.json:
{"models": [{"model": "x20", "regions": {
    "LT": {"x": 100, "y": 20, "width": 50, "height": 40},
    "RT": {"x": 20, "y": 20, "width": 50, "height": 40}
}}]}

Each model needs <model>-independent-rest.png, <model>-LT-only.png, and
<model>-RT-only.png. Bounds must describe the actual displayed sides; obtaining
both from raw, untransformed SVG path bounds would hide a mirrored-LT defect.
The browser review can additionally assert transformed cap clip bounds overlap
these regions before capturing; pixel differences prove visible independence.
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image


DIFFERENCE_THRESHOLD = 8
REGION_PADDING = 2
MIN_ACTIVE_CHANGED_PIXELS = 10  # Strictly more than this amount must change.
MAX_INACTIVE_CHANGED_PIXELS = 2


def read_rgb(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.int16)


def region_crop(region: dict, image_shape: tuple) -> tuple[int, int, int, int]:
    values = [float(region[key]) for key in ("x", "y", "width", "height")]
    if not all(math.isfinite(value) for value in values):
        raise ValueError(f"Non-finite region coordinates: {region}")
    x, y, width, height = values
    if width <= 0 or height <= 0:
        raise ValueError(f"Empty trigger region: {region}")
    image_height, image_width = image_shape[:2]
    crop = (
        max(0, math.floor(x) - REGION_PADDING),
        max(0, math.floor(y) - REGION_PADDING),
        min(image_width, math.ceil(x + width) + REGION_PADDING),
        min(image_height, math.ceil(y + height) + REGION_PADDING),
    )
    if crop[0] >= crop[2] or crop[1] >= crop[3]:
        raise ValueError(f"Region is outside the {image_width}x{image_height} image: {region}")
    return crop


def region_change(rest: np.ndarray, current: np.ndarray, crop: tuple) -> dict:
    x0, y0, x1, y1 = crop
    difference = np.max(np.abs(current[y0:y1, x0:x1] - rest[y0:y1, x0:x1]), axis=2)
    changed = difference > DIFFERENCE_THRESHOLD
    coordinates = np.argwhere(changed)
    return {
        "crop": {"x": x0, "y": y0, "width": x1 - x0, "height": y1 - y0},
        "changedPixels": int(np.count_nonzero(changed)),
        "totalPixels": int(changed.size),
        "maximumChannelDifference": int(difference.max()),
        "firstChangedPixels": [
            {"x": int(x0 + x), "y": int(y0 + y), "difference": int(difference[y, x])}
            for y, x in coordinates[:8]
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path, help="Browser review output directory")
    parser.add_argument("--geometry", type=Path, help="Geometry JSON path; defaults to OUT/geometry.json or OUT/report.json")
    args = parser.parse_args()
    output_dir = args.out.resolve()
    geometry_path = args.geometry.resolve() if args.geometry else output_dir / "geometry.json"
    if not args.geometry and not geometry_path.exists():
        geometry_path = output_dir / "report.json"
    report = {
        "passed": False,
        "geometry": str(geometry_path),
        "differenceThreshold": DIFFERENCE_THRESHOLD,
        "regionPaddingPixels": REGION_PADDING,
        "activeChangedPixelsMustExceed": MIN_ACTIVE_CHANGED_PIXELS,
        "maximumInactiveChangedPixels": MAX_INACTIVE_CHANGED_PIXELS,
        "models": [],
        "errors": [],
    }
    try:
        geometry = json.loads(geometry_path.read_text(encoding="utf-8-sig"))
        models = geometry["models"]
        if not models:
            raise ValueError("Geometry contains no models; refusing an empty verification.")
        seen = set()
        for model_geometry in models:
            model = model_geometry["model"]
            entry = {"model": model, "passed": False, "cases": [], "errors": []}
            report["models"].append(entry)
            try:
                if model in seen:
                    raise ValueError(f"Duplicate model geometry: {model}")
                seen.add(model)
                rest = read_rgb(output_dir / f"{model}-independent-rest.png")
                crops = {key: region_crop(model_geometry["regions"][key], rest.shape) for key in ("LT", "RT")}
                for active, inactive in (("LT", "RT"), ("RT", "LT")):
                    current = read_rgb(output_dir / f"{model}-{active}-only.png")
                    if current.shape != rest.shape:
                        raise ValueError(f"{active} capture size {current.shape} differs from rest {rest.shape}")
                    active_change = region_change(rest, current, crops[active])
                    inactive_change = region_change(rest, current, crops[inactive])
                    passed = (
                        active_change["changedPixels"] > MIN_ACTIVE_CHANGED_PIXELS
                        and inactive_change["changedPixels"] <= MAX_INACTIVE_CHANGED_PIXELS
                    )
                    entry["cases"].append({
                        "pressed": active, "stationary": inactive, "passed": passed,
                        "activeRegion": active_change, "inactiveRegion": inactive_change,
                    })
                    if active_change["changedPixels"] <= MIN_ACTIVE_CHANGED_PIXELS:
                        entry["errors"].append(
                            f"{active}-only: {active} changed {active_change['changedPixels']} pixels; "
                            f"expected more than {MIN_ACTIVE_CHANGED_PIXELS}. Trigger movement is not visible in its own region."
                        )
                    if inactive_change["changedPixels"] > MAX_INACTIVE_CHANGED_PIXELS:
                        entry["errors"].append(
                            f"{active}-only: inactive {inactive} changed {inactive_change['changedPixels']} pixels; "
                            f"allowed at most {MAX_INACTIVE_CHANGED_PIXELS}. Check side registration or unrelated animation."
                        )
                entry["passed"] = not entry["errors"] and len(entry["cases"]) == 2
            except (KeyError, TypeError, ValueError, OSError) as error:
                entry["errors"].append(str(error))
            report["errors"].extend(f"{model}: {error}" for error in entry["errors"])
        report["passed"] = not report["errors"] and all(entry["passed"] for entry in report["models"])
    except (KeyError, TypeError, ValueError, OSError) as error:
        report["errors"].append(str(error))
    output_dir.mkdir(parents=True, exist_ok=True)
    report_path = output_dir / "independence-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "models": len(report["models"]), "report": str(report_path), "errors": report["errors"]}))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
