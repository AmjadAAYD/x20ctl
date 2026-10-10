# Vibration contours — 2 October 2026

Replaced the generic expanding oval motor overlays with model-specific contours
traced against the existing front artwork. The six controller images and their
front input geometry remain unchanged.

Each `haptics-geometry.json` stores curves in the corresponding photo's 1536×1024
coordinate space. The shared renderer uses the existing registry's motor zones
and draws an overlay in the same image plane as the artwork. A clip follows each
closed grip shape. Two light segments travel around its contour in two layers:
a thicker bright core and a broader blurred halo, above a soft secondary contour
band and surface pulse. No shape scaling or controller shaking is used. Left-side
effects use pale blue and right-side effects use amber, matching the artwork.

X20 Pro's upper zones follow the narrow, curved visible trigger caps, rather than
floating ovals over the face or display. X05 Pro retains its existing registry's
upper visualization zones with separately traced cap shapes. These are local
visual effects; no trigger or motor command, independent motor control, model
capability or protocol behavior was added.

Draft strength adjusts speed and opacity immediately. At 100%, the travel cycle
is 0.4 seconds, compared with the earlier 0.85 seconds. A quadratic speed curve
keeps low strengths slower. Grip core strokes increased from 12 to 18 photo units;
trigger strokes increased from 7 to 10. The halo and secondary band follow the
same clipped contour, so they cannot spill into the face controls.
Zero strength removes the
overlay. Reduced motion retains a static contour; losing focus or hiding the app
pauses the traveling segments and surface pulse.

## Verification

- Type checking and production frontend build passed.
- Six focused haptics geometry tests passed: each declared zone has a closed,
  photo-sized contour with in-bounds coordinates; upper zones remain above the
  face controls.
- Isolated headless production UI review passed for all six models at 1400×940
  and 1060×760. It verified zone counts, correct contour data, clipping, image
  plane alignment, no horizontal overflow and no controller-image transform.
- Animation time advanced while shape positions stayed fixed. Gentle strength
  reduced brightness and slowed motion; Off removed every overlay. Reduced
  motion and pause styling passed. No runtime errors occurred.
- SHA-256 checks confirmed all 18 existing front complete/base/geometry files
  were unchanged. Geometry outlines were visually inspected against all six
  rendered controller previews. Alignment measurements establish renderer
  placement, not physical motor position or hardware actuation.
- The two existing native motion-review scripts were updated for the new path
  animation and passed Python syntax compilation; their desktop workflows were
  not executed, following the user's screen-access prohibition.
- `git diff --check` passed.

Evidence: `artifacts/vibration-contour-review/report.json` and twelve per-model
normal/compact screenshots. All review bridge calls are mocked, including
disconnected X20 input. Preview models only perform mocked model selection.
There was no physical controller or user's screen access.

```powershell
node --test tests/controller-haptics.test.mjs
node tools/vibration_review.cjs --playwright C:/Users/amjad/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright --browser 'C:/Program Files/Google/Chrome/Application/chrome.exe'
```

No release, publication, push or commit was performed. Actual Windows UI playback
and physical vibration remain unverified in this run.

## Local Windows build

`dist/local-vibration-glow-20261002/x20ctl.exe` contains the updated renderer
and animation styles, verified by inspecting the executable's bundled frontend.
PyInstaller succeeded, and the paired scanner integrity check passed without
opening the desktop UI or performing a scan. Source scanner identity was restored
byte-for-byte. Evidence: `artifacts/vibration-contour-review/local-desktop-build.json`
and the local build logs.

Previous executable builds remain unchanged. This local preview is approximately
56.3 MiB and exceeds the existing 40 MiB release ceiling; it is not a validated
release package. Keep the paired `x20ctl-scanner.exe` beside the local desktop
executable when trying it manually.
