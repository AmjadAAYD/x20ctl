"""Check rendered rim confinement and released sprite registration offline."""
import json
import argparse
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--out", default="artifacts/controller-trigger-motion")
OUT = ROOT / parser.parse_args().out
catalog = json.loads((ROOT / "x20ctl/controllers/catalog.json").read_text())
report = {"passed": False, "rimDifferenceThreshold": 8, "antialiasMarginPixels": 3, "models": []}
for model in catalog:
    name = model["id"]
    on = cv2.imread(str(OUT / f"{name}-rim-stress-on.png"))
    off = cv2.imread(str(OUT / f"{name}-rim-stress-off.png"))
    shell = cv2.imread(str(OUT / f"{name}-shell-mask.png"), cv2.IMREAD_GRAYSCALE)
    assert on.shape == off.shape, name
    # Playwright encloses fractional element origins in whole screenshot pixels;
    # the canvas mask uses CSS dimensions. The extra edge is at most one pixel.
    dh, dw = on.shape[0] - shell.shape[0], on.shape[1] - shell.shape[1]
    assert 0 <= dh <= 1 and 0 <= dw <= 1, (name, on.shape, shell.shape)
    shell = cv2.copyMakeBorder(shell, 0, dh, 0, dw, cv2.BORDER_CONSTANT, value=0)
    # Three screen pixels cover screenshot rounding and edge antialiasing.
    boundary = cv2.dilate(shell, np.ones((7, 7), np.uint8)) > 0
    # Ignore low-level screenshot noise: one corner pixel differed by 4/255
    # between otherwise identical captures, far away from the controller.
    changed = np.max(cv2.absdiff(on, off), axis=2) > report["rimDifferenceThreshold"]
    escaped = int(np.count_nonzero(changed & ~boundary))
    assert escaped == 0, (name, escaped)
    released = cv2.imread(str(OUT / f"{name}-released.png"))
    reference = cv2.imread(str(OUT / f"{name}-reference.png"))
    changed_at_rest = np.max(cv2.absdiff(released, reference), axis=2) > 20
    fraction = float(np.mean(changed_at_rest))
    assert fraction < .01, (name, fraction)
    full = cv2.imread(str(OUT / f"{name}-100.png"))
    moving_pixels = int(np.count_nonzero(np.max(cv2.absdiff(full, released), axis=2) > 20))
    assert moving_pixels > 10, (name, moving_pixels)
    report["models"].append({"model": name, "escapedRimPixelsBeyond3pxAntialiasMargin": escaped,
        "releasedDifferenceFractionAbove20": fraction, "movingPixels": moving_pixels})
report["passed"] = True
(OUT / "pixel-report.json").write_text(json.dumps(report, indent=2))
print(json.dumps(report))
