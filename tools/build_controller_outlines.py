"""Traces each controller's outline from its own artwork (owner direction 10 Oct 2026: the rim must follow the
controller exactly; the old lighting.json silhouettes were 2-pixel stair steps that wobbled round the bumpers).

For every model, front-full.png and rear-full.png are read, the alpha is closed and opened to drop stray pixels, the
largest outer contour is taken, smoothed with a circular Gaussian (so the cut-out's jagged pixels become one clean
curve) and simplified. Writes src/assets/controllers/outlines.json: {"models": {id: {"front": path, "back": path}}}
in the 1536x1024 art space every controller picture uses.
"""
import json
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1] / "src" / "assets" / "controllers"
MODELS = ["x20", "x20_pro", "x05", "x05_pro", "x10", "d10", "x15", "dune"]
SIZE = (1536, 1024)


def trace(png: Path, sigma: float = 3.0) -> str | None:
    image = cv2.imread(str(png), cv2.IMREAD_UNCHANGED)
    if image is None or image.ndim != 3 or image.shape[2] != 4:
        return None
    if (image.shape[1], image.shape[0]) != SIZE:
        image = cv2.resize(image, SIZE, interpolation=cv2.INTER_AREA)
    # trace inside the fully opaque body (owner direction 10 Oct 2026: the soft, foggy cut-out band must be covered),
    # then step in one more pixel so the rim drawn on this line hides the whole transition
    mask = (image[:, :, 3] >= 235).astype(np.uint8) * 255
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    mask = cv2.erode(mask, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not contours:
        return None
    contour = max(contours, key=cv2.contourArea)[:, 0, :].astype(float)
    # circular Gaussian smoothing along the contour
    radius = int(sigma * 3)
    weights = np.exp(-0.5 * (np.arange(-radius, radius + 1) / sigma) ** 2)
    weights /= weights.sum()
    padded = np.concatenate([contour[-radius:], contour, contour[:radius]])
    smooth = np.stack([np.convolve(padded[:, k], weights, mode="valid") for k in (0, 1)], 1)
    simple = cv2.approxPolyDP(smooth.astype(np.float32).reshape(-1, 1, 2), 0.35, True)[:, 0, :]
    points = " L ".join(f"{x:.1f} {y:.1f}" for x, y in simple)
    return f"M {points} Z"


def main() -> None:
    out = {"source": "traced from native/front-full.png and native/rear-full.png alpha by tools/build_controller_outlines.py", "models": {}}
    for model in MODELS:
        entry = {}
        for view, name in (("front", "front-full.png"), ("back", "rear-full.png")):
            source = ROOT / model / "native" / name
            path = trace(source) if source.exists() else None
            if path:
                entry[view] = path
        out["models"][model] = entry
        print(model, {k: len(v) for k, v in entry.items()})
    (ROOT / "outlines.json").write_text(json.dumps(out, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
