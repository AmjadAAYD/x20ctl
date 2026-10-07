"""Trace shell clipping geometry from existing photos; never changes those photos.

Existing SVG perimeters supply conservative foreground/background seeds. GrabCut
separates photographic shells from their colored ambient backgrounds. Review the
diagnostics before adopting generated paths.
"""
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/controller-shell-fit"
guides = json.loads((OUT / "guides.json").read_text())
lighting = json.loads((ROOT / "src/assets/controllers/lighting.json").read_text())
report = []
for model, views in guides.items():
    for view, points in views.items():
        source = ROOT / f"src/assets/controllers/{model}/{'controller-base.png' if view == 'front' else 'controller-rear.png'}"
        original = cv2.imread(str(source))
        image = cv2.resize(original, (768, 512), interpolation=cv2.INTER_AREA)
        polygon = np.round(np.array(points) / 2).astype(np.int32)
        guide = np.zeros((512, 768), np.uint8)
        cv2.fillPoly(guide, [polygon], 255)
        core = cv2.erode(guide, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25)))
        envelope = cv2.dilate(guide, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (33, 33)))
        mask = np.full(guide.shape, cv2.GC_BGD, np.uint8)
        mask[envelope > 0] = cv2.GC_PR_BGD
        mask[guide > 0] = cv2.GC_PR_FGD
        mask[core > 0] = cv2.GC_FGD
        cv2.setRNGSeed(7)
        cv2.grabCut(image, mask, None, np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64), 4, cv2.GC_INIT_WITH_MASK)
        shell = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
        # Textured white grips can be classified in small alternating fragments.
        # Close those texture gaps, then smooth the raster edge rather than
        # tracing individual texture pixels as physical shell indentations.
        shell = cv2.morphologyEx(shell, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)))
        shell = np.where(cv2.GaussianBlur(shell, (5, 5), .9) >= 128, 255, 0).astype(np.uint8)
        contours, _ = cv2.findContours(shell, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        contour = max(contours, key=cv2.contourArea)
        # Preserve measured corners while dropping subpixel raster stair steps.
        contour = cv2.approxPolyDP(contour, 4 if model == "x05" and view == "back" else .65, True) * 2
        coords = contour.reshape(-1, 2)
        path = "M" + " L".join(f"{x} {y}" for x, y in coords) + " Z"
        if model == "x05" and view == "back":
            # The inner edges of these lightly textured grips fade into the
            # backdrop. A smooth interpolant avoids turning that texture into
            # a jagged clipped edge, while keeping the measured silhouette.
            parts = [f"M{coords[0][0]} {coords[0][1]}"]
            for i, point in enumerate(coords):
                previous, end, following = coords[(i - 1) % len(coords)], coords[(i + 1) % len(coords)], coords[(i + 2) % len(coords)]
                first = point + (end - previous) / 6
                second = end - (following - point) / 6
                parts.append(f"C{first[0]:.2f} {first[1]:.2f} {second[0]:.2f} {second[1]:.2f} {end[0]} {end[1]}")
            path = " ".join(parts) + " Z"
        lighting["models"][model][view] = path
        diagnostic = original.copy()
        cv2.polylines(diagnostic, [contour], True, (80, 255, 80), 2, cv2.LINE_AA)
        cv2.imwrite(str(OUT / f"{model}-{view}-trace.png"), diagnostic)
        report.append({"model": model, "view": view, "points": len(coords), "source": str(source.relative_to(ROOT)), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
(OUT / "candidate-lighting.json").write_text(json.dumps(lighting, indent=2) + "\n")
(OUT / "trace-report.json").write_text(json.dumps(report, indent=2))
print(json.dumps({"tracedViews": len(report), "photosModified": False}))
