"""Re-traces vibration-outlines.json from the controller pictures themselves, for every model and every colour.

Each vibration stroke is a stretch of the controller's own front silhouette (round a grip, or along a bumper top), set
just inside the edge and smoothed (owner, 13 Oct 2026: the line follows the controller's own outline, never a drawn
shape). When the art changes, the strokes are cut again from the new edge: the ends of each stroke now in the file are
projected onto the new inset edge, and the stretch between them that holds the stroke's middle becomes the new stroke.
A colour whose shell differs from its original gets its own entry, keyed "model/colour", as outlines.json does; the
original keeps the plain model key the Linux app reads too.

    python tools/build_vibration_outlines.py

Run it after tools/import_finish_packs.py. Needs numpy and OpenCV. Running it twice changes nothing.
"""
import json
from pathlib import Path

import cv2
import numpy as np

CONTROLLERS = Path(__file__).resolve().parents[1] / "src" / "assets" / "controllers"
W, H = 1536, 1024
INSET = {"dune": 4.5}  # px inside the edge; 6.5 for the rest, as the strokes were first drawn


def edge(alpha, inset):
    """The outer contour of the picture, inset and smoothed, as a closed (n, 2) array at about 1 px steps."""
    solid = (alpha > 128).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(solid)
    solid = (labels == 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))).astype(np.uint8)
    inside = (cv2.distanceTransform(solid, cv2.DIST_L2, 5) >= inset).astype(np.uint8)
    contours, _ = cv2.findContours(inside, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = max(contours, key=cv2.contourArea)[:, 0, :].astype(np.float64)
    # smooth round the loop: a wide Gaussian takes off the pixel steps, not the shape
    k = np.exp(-np.arange(-12, 13) ** 2 / (2 * 4.0 ** 2)); k /= k.sum()
    padded = np.concatenate([c[-12:], c, c[:12]])
    return np.stack([np.convolve(padded[:, i], k, "valid") for i in (0, 1)], 1)


def nearest(loop, p):
    return int(np.argmin(((loop - p) ** 2).sum(1)))


def arc(loop, a, b, middle):
    """The stretch of the loop from index a to b that passes the index nearest the stroke's middle."""
    n = len(loop)
    forward = [(a + i) % n for i in range((b - a) % n + 1)]
    backward = [(a - i) % n for i in range((a - b) % n + 1)]
    return forward if middle in forward else backward


def resample(points, count):
    step = np.r_[0, np.cumsum(np.hypot(*np.diff(points, axis=0).T))]
    at = np.linspace(0, step[-1], count)
    return np.stack([np.interp(at, step, points[:, i]) for i in (0, 1)], 1)


def silhouette(alpha):
    """The whole outline the light is clipped to, simplified the way the file has always held it."""
    solid = (alpha > 128).astype(np.uint8)
    contours, _ = cv2.findContours(solid, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = cv2.approxPolyDP(max(contours, key=cv2.contourArea), 1.2, True)[:, 0, :]
    return [[round(float(x) / W, 5), round(float(y) / H, 5)] for x, y in c]


def strokes(loop, template):
    out = []
    for stroke in template:
        p = np.array(stroke["points"]) * [W, H]
        if stroke.get("closed"):
            new = loop
        else:
            a, b, m = nearest(loop, p[0]), nearest(loop, p[-1]), nearest(loop, p[len(p) // 2])
            new = loop[arc(loop, a, b, m)]
        new = resample(new, len(p))
        out.append({**stroke, "points": [[round(float(x) / W, 5), round(float(y) / H, 5)] for x, y in new]})
    return out


def main():
    path = CONTROLLERS / "vibration-outlines.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    finishes = json.loads((CONTROLLERS / "finishes.json").read_text(encoding="utf-8"))["models"]
    templates = {k: v for k, v in data["models"].items() if "/" not in k}
    models, silhouettes = {}, {}
    for model, template in templates.items():
        for finish in [f["id"] for f in finishes.get(model, [])] or ["original"]:
            folder = CONTROLLERS / model / ("native" if finish == "original" else f"finishes/{finish}")
            alpha = cv2.imread(str(folder / "front-full.png"), cv2.IMREAD_UNCHANGED)[:, :, 3]
            key = model if finish == "original" else f"{model}/{finish}"
            models[key] = strokes(edge(alpha, INSET.get(model, 6.5)), template)
            silhouettes[key] = silhouette(alpha)
            print(key, len(silhouettes[key]), [len(s["points"]) for s in models[key]])
    data["models"], data["silhouettes"] = models, silhouettes
    data["note"] = ("Vibration strokes: stretches of each controller's own silhouette (inset, smoothed), fractions of the "
                    "1536x1024 front picture; a colour cut differently from its original is keyed \"model/colour\". "
                    "Rebuilt from the art by tools/build_vibration_outlines.py.")
    path.write_text(json.dumps(data, indent=1) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
