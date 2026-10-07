# Controller photo layers

## Vibration contours

Each model's `haptics-geometry.json` traces its grip silhouettes in front-photo
coordinates. Models with upper visualization zones also have curved trigger-cap
contours. The shared canvas clips traveling light and surface pulses to those
shapes, preserving the image and all input overlays. Strength controls speed and
brightness; Off removes the effects. No hardware commands were added.
See [vibration verification](../../../docs/vibration-contours-validation.md).

## Rear views

Each of the six models has a new `controller-rear.png` and `rear-geometry.json`.
The 1536×1024 plates share blue-left and amber-right lighting. Rear images use a
single contained image plane with normalized physical-button polygons; this
keeps the HTML hover/click overlays aligned through resizing. The shared
`ControllerView` offers Front View / Back View on Buttons and opens the selected
macro slot. Macros is fixed to the rear view with no view-switch buttons.
X05 has no macro buttons; X05 Pro has a slight rear tilt to show its top controls.
All existing front images, empty-well bases and front geometry were preserved.

See [rear verification and source notes](../../../docs/rear-controller-views-validation.md)
and [exact image edit prompts](../../../docs/rear-controller-image-prompts.json).

## Additional UI-only previews

On 1 October the user approved X05, X05 Pro, X10 and D10 as UI-only previews.
Each model now has an edited 1536×1024 front complete plate (`controller.png`)
and a matching empty-well base (`controller-base.png`). The image editor isolated
the front controller from the source reference, retained its model-specific shell
and controls, and produced the illuminated complete plate. A second edit removed
only the movable caps and raised stems. Boxes, rear views, docks, captions and
callout lines are excluded. Original reference images and source notes are
retained in `artifacts/front-controller-sources/`; runtime plates are edited
illustrations rather than untouched manufacturer photographs.

Static cards, picker previews and headers use the complete plate. Interactive
canvases use the empty-well base with two opaque moving cap crops sampled from the
complete plate. `geometry.json` mirrors each catalog entry. Neutral cap placement
and drag origins follow the actual fixed socket centers measured in the base;
the independent crop centers select source texture only. Crops exclude stems and
surrounding glow. Thin selection outlines add no glow, filter or drop shadow.
These visual coordinates do not establish physical calibration, hardware
identity or a configuration protocol.

Selected front reference sources (original downloads retained unchanged):

- X05: the lower-left controller in the [official package photograph](https://cdn.shopify.com/s/files/1/0075/2425/3809/files/EasySMX_X05_Gaming_Controller_Package.jpg?v=1722843760), saved as `x05-marketing-12.jpg`.
- X05 Pro: the single front controller in the [retailer photograph](https://acdn-us.mitiendanube.com/stores/005/542/994/products/easysmx-x05-pro-2-598ad17b9b97b3bba117616321613694-1024-1024.webp), saved as `x05_pro-retailer.webp`. Manufacturer imagery cross-checks appearance; retailer text supplies no technical claims.
- X10: the left/front controller in the [official marketing photograph](https://cdn.shopify.com/s/files/1/0075/2425/3809/files/5_3078f837-9049-480c-b516-abbd0f536a4c.jpg?v=1700304373), saved as `x10-front-marketing.jpg`.
- D10: the upper/front controller in the [official control diagram photograph](https://cdn.shopify.com/s/files/1/0075/2425/3809/files/15_f272e730-5f43-4cf9-ac21-1793dbd4b977.jpg?v=1747906313), saved as `d10-black-15.jpg`.

The generated outputs are retained there as `*-complete.png` and `*-base.png`.
Selection notes are in `x05-sources-report.md` and `x10-d10-source-review.md`.
Every non-X20 model remains an inactive hardware preview. X20/X20 Pro artwork,
geometry and registry definitions, and the shared renderer, are unchanged by
this update. Protected hashes and the first two catalog objects are recorded in
`artifacts/front-controller-sources/protected-before.json`.

The current preview verification and manufacturer capability references are in
`docs/preview-models-validation.md`.

For these four edited models, the final typecheck and production build passed,
along with six targeted native registry/preview-selection tests. All six geometry
files match the catalog, all twelve plates are 1536×1024, and cap crop bounds are
valid. The protected snapshot comparison passed for six asset/geometry hashes
and the first two catalog objects. The final offscreen headless layered-cap
review passed all four new models at normal and compact sizes, saved seventeen
images and has no report error:
`artifacts/front-layered-models-2026-10-01-final/report.json`.

The user prohibited screen access. This update used no desktop window control or
screen capture; the four new models were not verified in native WebView2.
Reopen the source desktop app manually to load the rebuilt artwork. No hardware
commands or release work were performed. The older native X20/X20 Pro evidence
below remains specific to those protected models.

## X20 / X20 Pro photo layers

The complete `x20/controller.png` and `x20_pro/controller.png` remain the original
artwork for cards, the picker, the header and inactive previews.

Interactive canvases use the following edited background plates:

- `C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/src/assets/controllers/x20/controller-base.png`
- `C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/src/assets/controllers/x20_pro/controller-base.png`

Both plates retain the 1536×1024 framing. Both stationary rubber caps were removed
using the built-in imagegen tool. Each moving cap samples the original photo;
model-specific `sticks.left.cap` and `sticks.right.cap` crop bounds are stored in
`x20ctl/controllers/catalog.json`, mirrored by each model's `geometry.json`.
The edited plate and moving caps are different sources. Moving a stick therefore
reveals its empty socket instead of another photographed cap. Selection uses a
thin outline; the input overlays have no added glow, filter or drop shadow.
The fixed RGB rings and ambient lighting belong to the controller artwork.

Crop centers are texture-sampling coordinates, not display offsets. Neutral caps
and drag origins use these independently measured fixed-ring centers in the
1536×1024 background plates:

| Model | Left ring (x, y) | Right ring (x, y) |
| --- | --- | --- |
| X20 | (400.67, 307.98) | (932.75, 510.55) |
| X20 Pro | (399, 283) | (960, 460) |

The centering follow-up saved native evidence to
`artifacts/thumbstick-centering-2026-10-01-verified/`. It verifies both neutral
caps and interaction origins against the measured centers at normal and compact
sizes and after returning from deflection. Maximum observed coordinate difference
was 0.021 CSS pixels. Source crop bounds and artwork were preserved.

## Edit prompt set

Shared prompt: precise object edit of the supplied controller image; remove only
the two movable rubber thumbstick caps and raised rubber sidewalls; leave empty
dark recessed sockets. Preserve the complete framing, 1536×1024 dimensions, shell,
buttons, D-pad, screen/branding, RGB strips, fixed rings, lighting and proportions.
No zoom, recentering, new objects, text, watermark or product redesign.

- X20 prompt: remove the grey rubber caps at the two stick locations and preserve
  the outer pink/purple RGB rings; fill the caps' areas with recessed dark wells.
- X20 Pro prompt: remove the black rubber caps and preserve the fixed silver
  toothed rings, their housing, the display and shell branding.

Generated transparent cap candidates were rejected because they changed the
original shapes and lighting; they are not project assets or runtime layers.

## Verification

`tools/thumbstick_review.py` drives isolated local previews in the actual Windows
WebView2 desktop host, without hardware commands. The Oct 1 correction passed for
both models at neutral, both travel directions, selected/pressed styling and a
1060×760 window. It saved ten native screenshots and a passing report in
`artifacts/thumbstick-review-2026-10-01-verified/`.

The production build passed using `node node_modules/vite/bin/vite.js build
--configLoader native`, which avoids the sandbox's blocked config-bundling
directory traversal. Type checking and `git diff --check` also passed.
