# Controller Artwork & Geometry Assets

This directory holds the V0.025 layered artwork and vector/contour geometry used by the WPF (`X20Ctl.Product`) and Linux Avalonia (`X20Ctl.Linux`) desktop studios on a `1536x1024` canvas.

## Structure per model (`x20`, `x20_pro`, `x15`, `x10`, `x05_pro`, `d10`, `dune`)

- `native/`: Base colourway layer stack (`front-body.png`, `front-full.png`, `rear-body.png`, `rear-full.png`, `stick-l.png`, `stick-r.png`, `trigger-l.png`, `trigger-r.png`, `holo-front.png`, `holo-rear.png`).
- `finishes/<colour>/`: Alternate colour finishes built on the same alignment grid.
- `haptics-geometry.json`: Grip and trigger haptic highlight contours rendered on the Vibration page.
- `x20/rear-geometry.json`: Reference rear button polygons shared across desktop geometry checks.

## Root manifests

- `native-layers.json` — stick pivot centers, trigger hinge/motion metadata, and per-model layer alignment.
- `finishes.json` — ordered colourway list and display names per model (original colour first).
- `control-shapes.json` — clickable/interactive front and rear control hit regions.
- `outlines.json` — traced shell silhouettes per model and finish (`model/colour`).
- `lighting.json` — RGB lighting zone coordinates and rim-light contour paths.
- `framing.json` — stage framing and crop boxes.
- `vibration-outlines.json` — per-finish stroke segments used by the Vibration visualizer.

Legacy single-plate React assets from early versions have been removed. Artwork layers can be regenerated or re-trimmed from finish packs with `tools/import_finish_packs.py`, `tools/build_controller_outlines.py`, and `tools/build_vibration_outlines.py`.
