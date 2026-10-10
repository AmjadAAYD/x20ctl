# Shared controller presentation validation — 2026-10-02

Applies to X20, X20 Pro, X05, X05 Pro, X10 and D10. Existing intentional
working-tree changes and original controller photos were preserved.

## Behavior

- Glass-style workspace tabs reuse the existing moving selection indicator.
  Front/Back controls use the same material; keyboard focus remains visible.
  This is a CSS approximation for desktop WebViews, not Apple's native material.
- Model-specific source-coordinate silhouette clips retain an opaque controller
  body while attenuating a separate photographic ambient layer. Supported card
  ambient opacity is 0.22 normally, 0.55 on hover and 1 when selected/connected.
  Preview cards use 0.035 normally and 0.09 on hover. These values apply to the
  background layer, not the physical RGB details already in the photographs.
- Buttons uses a thin, twelve-second blue/violet/pink/amber outline sweep.
  Rear views instead use brief, localized hover/selection feedback on the actual
  model-specific programmable control outlines.
- Vibration retains grip contours and X20 Pro upper zones. One broad traveling
  packet plus a soft second layer replaces multiple racing bands. The controller
  stays stationary. Cycles are 5s at 10%, 3.5s at 30%, 2.4s at 50%, 1.6s at 70%,
  and 1s at 100%; brightness only increases from 0.42 to 0.56. At zero, animated
  effects disappear and the faint static rim remains.
- Picker tiles use two columns with a narrow-window list fallback and explicit
  Supported/Preview badges. Preview actions say Open preview; assignment still
  targets the chosen player and does not establish a hardware connection.
- Macro artwork remains rear-only and is 15% smaller. The background is quieter
  and the empty state offers Add first step. A local run-once sequence preview
  highlights the current timeline column and displays active input labels below
  the rear image. It never sends controller commands. Stop, slot changes, edits,
  visibility loss and unmount cancel playback.
- Reduced-motion and reduced-transparency fallbacks are included.

## Executed verification

- Frontend type check and production build passed.
- Thirteen existing haptics/rear geometry unit checks passed.
- `controller_presentation_review.cjs` passed all six models: wide/narrow picker,
  normal/hover/selected lighting, opaque bodies, advancing/paused rim animations,
  glass styles and keyboard navigation, rear-only Macro views, local hold/pause/
  next-step/stop/slot-change playback on all five macro-capable models, vibration
  presets, zero strength, compact layout, reduced motion and mock bridge calls.
- `vibration_review.cjs` passed all six models, including shape placement at
  multiple viewport sizes, upper X20 Pro zones and updated speed/layer styles.
- `preview_models_review.cjs` passed all four expansion previews with seventeen
  screenshots after the final frontend build.
- Existing framing review passed 54 model/view/size cases and rear-view review
  passed all six models and sixteen macro destinations during this change.
- Reviewed isolated production screenshots of the picker, glass navigation,
  all six front controller canvases, rear Macro playback and vibration rendering.
  Original front photo hashes remained unchanged.

Reports and screenshots are under `artifacts/controller-presentation-review`,
`artifacts/vibration-contour-review` and the existing focused review directories.
The separate local Windows build is `dist/local-glass-lighting-20261002/x20ctl.exe`;
its packaging/integrity result is recorded in the presentation review directory.

## Verification boundaries

Reviews used headless Chromium and disconnected mock bridge responses. The host
screen, native GUI and physical controllers were not accessed. Native packaging
verification checks bundled frontend bytes, all six rear assets and scanner
integrity; it does not prove interactive WebView2 rendering or hardware behavior.
No new controller protocol commands were added. Preview models remain without a
verified hardware backend. Nothing was committed, pushed, published or released.
Earlier local Windows/Linux builds were preserved; the previous Linux binary
was not rebuilt for this visual change.
