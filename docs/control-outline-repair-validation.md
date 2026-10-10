# Control outlines and compact layouts — 2026-10-02

Applies to X20, X20 Pro, X05, X05 Pro, X10 and D10.

## Changes

- Replaced the shared 6%-wide front circles with measured source-pixel ellipses,
  D-pad regions and shaped center controls. Source photographs remain unchanged.
- Centralized front, rear and Macro target contours in
  `src/assets/controllers/control-shapes.json`. The photo, hit region and outline
  share the same 1536 × 1024 coordinate plane. Curved buttons now render curved
  paths; selected state no longer adds a circular HTML border around every input.
- Rear shoulder/trigger contours were measured separately for every model.
  Rear orientation mirrors the front: RB/RT appear on the viewer's left and
  LB/LT on the right, consistent with the labels visible in the X05 Pro photo.
- Selecting LB/LT/RB/RT in the inspector opens the rear view and highlights the
  corresponding control. Selecting a front input returns to the front. Manual
  Front/Back buttons remain available; front view has no shoulder targets.
- Pointer hit-testing follows the actual SVG fill inside each HTML button.
  This avoids a trigger's bounding rectangle intercepting a bumper click.
- Refined all programmable controls against their visible button surfaces,
  including X20 Pro's angled paddles and upper M5/M6 controls. Macro views remain
  rear-only and omit shoulder assignment controls.
- Refined shell clips/rim paths to preserve raised top details and follow the
  photo boundary more closely.
- Buttons artwork is limited to 470px wide. Macro artwork sits in a 290px rail
  beside the sequencer with compact two-column slot controls underneath. Narrow
  windows stack the editor and use a 280px rear preview.

## Verification performed

- Frontend type check and production build passed.
- Thirteen rear/haptics geometry tests passed. Rear tests now validate the
  centralized contours actually used by the renderer.
- `control_outline_review.cjs` passed all six models: sixty front targets,
  seventy-two shoulder selections over three viewport sizes, automatic rear
  switching, returning to front inputs, manual view switching, inspector focus,
  scaled source geometry, all sixteen Macro destinations, rear-only Macro
  canvases, compact/narrow layouts and no horizontal overflow.
- `controller_framing_review.cjs` passed all 54 model/view/size combinations,
  checking complete shells, source resolution, undistorted scaling and stick
  placement after the layout changes.
- `controller_presentation_review.cjs` passed all six models, including lighting,
  vibration strength, reduced motion and local Macro sequence playback.
- Reviewed all six front and rear canvases with every contour shown together,
  plus the compact X20 Pro Macro layout. Screenshots and the check report are
  under `artifacts/control-outline-repair`.
- Original front image hashes were unchanged. Preview models issued only
  `select_model` to the mock bridge; no controller operations were added.

The separate Windows bundle is
`dist/local-control-outlines-20261002/x20ctl.exe`. Packaging results, bundled
frontend comparison, scanner integrity and SHA-256 are recorded in
`artifacts/control-outline-repair/local-desktop-build.json`.

## Boundaries

All interactive reviews used isolated headless Chromium with a disconnected mock
bridge. The host screen, native GUI and physical controllers were not accessed.
Native WebView2 rendering and physical hardware behavior remain unverified for
this change. No speculative protocol commands, release, push or publication.
Existing dirty-tree work and earlier local builds were preserved.
