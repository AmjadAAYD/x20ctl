# Four additional preview models — 1 October 2026

The user explicitly approved restoring X05/X05 Pro and adding X10/D10 as
**UI-only previews**. This supersedes the earlier two-model scope. X20 remains
the only verified configuration backend; all five other models have no backend.
No release, commit, push, packaging or firmware work was performed.

## Implemented

- The picker now contains X20, X20 Pro, X05, X05 Pro, X10 and D10. Four-player
  assignment and independent session drafts are retained.
- The new Studios share mappings, curves, sliders, localized vibration previews,
  capability information and Support X20ctl. Connection, apply, reset,
  calibration, recording, profile persistence and hardware reports stay disabled.
- Native model selection reads the registry. Every preview is rejected before
  device/profile/report access; unregistered models are still rejected rather
  than falling back to the X20 protocol.
- The four added models use 1536×1024 front-view illustrations generated from
  isolated, front-facing source references. Each has a complete plate for static
  views and an edited base plate with empty stick wells for interactive canvases.
  Boxes, rear views, docks, source captions and callout lines are excluded from
  the runtime artwork. The original reference files are retained in
  `artifacts/front-controller-sources/`.
- Each moving cap samples its model's complete plate. Neutral placement and drag
  origins use the fixed socket centers measured in the edited base; texture crop
  centers are independent sampling coordinates. Crops exclude the stems and
  surrounding glow. Both caps remain opaque, and no glow or drop shadow is added
  by the control overlays. The shell lighting belongs to the artwork.
- Existing X20/X20 Pro assets, geometry and the first two registry definitions
  are protected by the SHA256/object snapshot in
  `artifacts/front-controller-sources/protected-before.json`. The shared renderer
  is unchanged. This artwork update leaves every non-X20 hardware preview gate
  unchanged.

## Front artwork references and edits

| Model | Front-facing input used |
| --- | --- |
| X05 | The separate controller at the lower-left of the official package photograph, `x05-marketing-12.jpg`. |
| X05 Pro | The single front-facing controller in the retailer photograph, `x05_pro-retailer.webp`; manufacturer images cross-check the shell and control layout. No capability claims come from the retailer listing. |
| X10 | The left/front controller in the official marketing photograph, `x10-front-marketing.jpg`. |
| D10 | The upper/front controller in the official control diagram photograph, `d10-black-15.jpg`. |

The image edits isolate each selected front controller, retain its model-specific
shell and controls, and produce a complete illuminated plate with full grips and
shoulders. A second edit removes only the movable caps and raised stems, leaving
the fixed rings and empty recessed wells. Runtime cap crops come from the complete
plate rather than a separately generated replacement cap. These edited
illustrations are presentation assets, not untouched manufacturer photographs or
physical input calibration evidence.

Source URLs and selection notes are recorded in
`artifacts/front-controller-sources/x05-sources-report.md` and
`artifacts/front-controller-sources/x10-d10-source-review.md`. The corresponding
`*-complete.png` and `*-base.png` files retain the edited outputs alongside the
original references.

## Corrections to the supplied research

| Model | Programmable controls | Established details and limits |
| --- | --- | --- |
| X05 | 0 | Official comparison shows no back paddles and no gyro. Hall sticks/triggers, RGB and USB/2.4 GHz/Bluetooth. |
| X05 Pro | 2, on top | Hall sticks, trigger locks and grip/trigger vibration zones. Gyro remains unknown. Four visual zones do not establish independent software control. |
| X10 | 2, rear | Hall sticks/triggers and Switch gyro. Decorative RGB remains unknown; no 1000 Hz claim was added. |
| D10 | 2, rear | TMR sticks, Hall/microswitch trigger modes, RGB and Switch gyro. Bluetooth and a separate included receiver are supported; the dock is not its only wireless path. |

Manufacturer references:
[X05](https://www.easysmx.com/products/easysmx%C2%AE-x05-multiplatform-gaming-controller-with-hall-effect-joysticks),
[X05 support](https://www.easysmx.com/pages/support-about-easysmx-x05-controller),
[X05 Pro](https://www.easysmx.com/products/easysmx-x05pro-multiplatform-wireless-gaming-controller-with-noise-canceling-button),
[X10 launch](https://www.easysmx.com/blogs/easysmx-controllers/easysmx-launches-x10-mechanic-master-gaming-controller-redefined-precision-durability-and-customization),
[X10 support](https://www.easysmx.com/pages/support-about-easysmx-x10-controller),
[D10](https://www.easysmx.com/products/easysmx-d10-multiplatform-gaming-controller-with-tmr-joysticks-trigger-lock-charging-dock),
[D10 support](https://www.easysmx.com/pages/support-about-easysmx-d10-controller).

No guessed VID/PID, Bluetooth identity, GATT service, HID command or motor-control
protocol was registered. Unknown physical flags use `null` and show “Unknown”.

## Verification

- `npm run lint` and the final `npm run build` passed.
- Six targeted native Python registry/selection tests passed, covering published
  macro counts and preview selection without device access.
- All six `geometry.json` files match their catalog definitions. All twelve
  complete/base plates are 1536×1024, and all cap crop bounds are valid.
- The protected snapshot comparison passed for six X20/X20 Pro asset/geometry
  hashes and the first two registry objects.
- The revised offscreen headless production review passed all four added models
  at 1400×940 and 1060×760, saved seventeen images and produced no report error.
  It verified two opaque photographic caps above each stickless base, neutral
  centers against the catalog, WASD deflection/release, both caps' pointer
  deflection/reset, and selected/pressed/hover styling without added overlay
  glow. It also verified complete static header art, preview safety controls,
  four-player assignments, navigation, vibration, preserved drafts and fresh
  startup. Cap transitions are accepted only at or below 1 ms, accommodating the
  existing reduced-motion style without changing the renderer.

Final evidence: `artifacts/front-layered-models-2026-10-01-final/report.json`.
The earlier `artifacts/preview-models-2026-10-01/report.json` predates these image
edits and is not the acceptance report for the new layers.

Run the offscreen review with `tools/preview_models_review.cjs`, specifying
the installed Playwright module and Chromium executable with `--playwright`
and `--browser`. The script serves local production assets temporarily and
closes its own headless browser/server afterward.

The user prohibited screen access. No desktop UI was controlled or captured
for this artwork update. These new models were **not** verified in the native
WebView2 window; headless rendering uses a mocked selection bridge. Physical
hardware and protocol validation remain outstanding. Reopen the source desktop
app manually to load the rebuilt assets; released EXEs are unchanged. No hardware
commands or release work were performed.
