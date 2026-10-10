# Vibration outline visibility — 2026-10-02

Applies through the shared renderer to X20, X20 Pro, X05, X05 Pro, X10, D10 and X15, including declared upper motor visualization zones. No controller photo, geometry or hardware command changed.

The full grip contour increased from 3 to 12 source pixels at 90% stroke opacity. The colored moving packet increased from 13 to 24 pixels, with a broader halo and a thin unblurred near-white core. Upper-zone contours use 8 pixels and packets 14 pixels. A narrow dark separator beneath the contour preserves contrast on white shells. Vibration uses its own brighter blue/violet/pink/amber gradient, leaving decorative lighting elsewhere unchanged.

Overall overlay opacity now ranges from 0.82 to 0.96 instead of 0.42 to 0.56. Existing speed response is preserved, including the one-second cycle at maximum. Shapes remain clipped inside their model-specific motor contours, the controller stays stationary, Off removes the effect, and reduced motion/focus pause remain functional.

Verification:

- The new visibility check first reproduced the existing faint-outline failure on X20.
- Type checking and the final production frontend build passed. The existing large-JavaScript-chunk warning remains.
- Seven focused haptics geometry tests passed. All seven models passed the final isolated production UI review at 1400 × 940 and 1060 × 760, with 14 screenshots.
- The review checked shared image-plane alignment, clipping, contour/core widths, opacity, contrast separator, animation progression, stable controller positions, strength response, Off, pause and reduced motion. It reported no runtime errors and used only mocked bridge operations.
- All seven final normal-size screenshots were visually inspected. Original front-photo hashes remained unchanged.
- JavaScript review syntax and scoped diff whitespace checks passed. The broader presentation review's opacity expectation was updated; that unrelated full review was not rerun.
- PyInstaller built `dist/local-vibration-visibility-20261002/x20ctl.exe`. All 34 embedded frontend assets match the final production build, the controller catalog matches source, and embedded launcher/geometry code equals current source, retaining the 1583 × 1147 centered launch-size policy.
- Executable: 83,021,028 bytes. SHA-256: `9f27a79ebc74e459388d6322b35d84a6a90c51ebf6e661df69862a2c7eb385ff`.

Evidence: `artifacts/vibration-visibility-final/report.json`, its per-model screenshots and `local-build.json`. Earlier reviews and executable builds remain available. No native desktop window, user's screen or physical controller was accessed; actual WebView2 playback and hardware vibration were not observed. Nothing was published, pushed or released.

This supersedes the vibration brightness/stroke values in the earlier quiet-lighting and vibration-contour notes.
