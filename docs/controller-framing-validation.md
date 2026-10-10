# Buttons and vibration artwork framing — 2026-10-02

Buttons (front and back) and Vibration use shared, model-specific shell bounds
from `src/assets/controllers/framing.json`. The longest normalized shell
dimension occupies 90% of the artwork frame. The other dimension retains the
controller's real proportions; no stretching is used. Background padding is
excluded from the fit calculation. Empty photo edges blend into one dark frame.

The source-coordinate plane moves and scales as a unit, keeping stick caps,
button targets, rear macro polygons and vibration contours attached to the
artwork. Original PNG files, input geometry and hardware behavior are unchanged.
Cards, the picker, header and macro editor retain their existing presentation.
The Buttons stage sizes around its view switch and caption. The X20 vibration
caption now has its own row instead of overlapping the artwork.

Verified against the production frontend, using headless Chromium and a mock
disconnected native bridge; no user's screen or physical controller accessed:

- All six models, Buttons front/back and Vibration at 1060×760, 1400×940 and
  1920×1080: 54 framing checks, full 1536×1024 image loading, consistent 90%
  shell coverage, at least 5% shell margins, preserved aspect ratio, aligned
  stick centers, no enlargement beyond source resolution at these sizes and
  no horizontal overflow. Comparison screenshots were inspected.
- Existing rear-view review passed all six models and all 16 macro-slot links.
- Existing vibration review passed all six models, including scaled contour
  alignment, strength response, 0.4-second maximum cycle, pause and reduced motion.
- Type check, production build and 13 focused geometry tests passed.

Evidence: `artifacts/controller-framing-review/report.json`,
`artifacts/rear-views-review/report.json` and
`artifacts/vibration-contour-review/report.json`.

These checks verify the renderer and packaged frontend. They do not claim an
interactive Windows desktop inspection or physical-controller verification.
