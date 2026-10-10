> Historical review: Curves was subsequently approved and its minor corrections completed. See APPROVED_CURVES.md for the frozen baseline and MACROS_REVIEW.md for the current milestone.

# Curves local UI review — 2026-10-09

Buttons is frozen at the owner's approved direction. Curves uses the same native shell, hardware canvas, atmosphere, focus, navigation, working context and footer. No Macros, Scanner or device-engine work was added.

Implemented: four separate local channel drafts, Default/Quick/Slow/Smooth/Fine reference presets, two draggable or keyboard-adjustable points, inner deadzone and outer travel sliders, per-channel reset, reset all, exact physical selection, and per-player/model in-session retention. Preset changes morph the illustrated curve and handles in 160 ms. Channel/page changes use 220 ms spatial transitions. Reduced motion removes both.

Reference presets come from the preserved Python protocol source; raw point coordinates use its 0–255 scale. Sliders here represent frontend percentages. The plotted Hermite shape is a frontend illustration through those points, not measured controller output or verified firmware interpolation. It does not encode packets. The eventual C++20 engine owns actual protocol behavior and authorization. Live input, controller readback, hardware Apply and new draft persistence remain unavailable.

## Verification

- Release build passed, zero warnings/errors.
- Domain suite: 42 passed, zero failed. New coverage includes channel isolation, exact reference preset values, invalid travel/point rejection and unverified-model editing guards.
- Combined WPF harness: 39 checks passed, 39 captured states, zero binding errors. Covers Buttons regressions, all Curves channels, deadzones, custom points, keyboard focus, reset, draft retention, minimum/maximized and reduced motion.
- Buttons sequence: 65 timed frames. Curves sequence: 42 timed frames, covering channel/preset transitions and returning to Buttons. Both GIFs use 60 ms frame delays, independently inspected.
- Approved Zone XAML, Colors and Typography hashes still match the checkpoint.

Evidence: artifacts/curves-native/complete-review. Key files: 28-curves-left-stick-quick.png, 29-curves-deadzones.png, 31-curves-left-trigger.png, 33-curves-custom.png, 35-curves-minimum.png, 36-curves-maximized.png, 38-return-to-buttons.png, 39-curves-reduced-motion.png, curves-selection-sequence.gif and review.json.

The captures render the shown app-owned WPF client. Harness events stay inside this app; no OS input is injected and no hardware is accessed. GIFs sample actual animation states, not an OS desktop recording. Physical gamepad behavior, performance/FPS, monitor-DPI transitions and real couch-distance reading are not verified by these artifacts. Curves visual acceptance remains with the owner.

The first review exposed an unfocusable custom point handle; it was made explicitly focusable and the keyboard regression passed. Initial minimum Buttons clipping and half-DIP target rounding were also corrected and verified. No known relevant UI failure remains in the final harness.

