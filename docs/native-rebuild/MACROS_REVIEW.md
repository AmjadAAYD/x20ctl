> Historical review: owner subsequently approved Macros. Final capacity, scrolling and preview polish is recorded in APPROVED_MACROS.md; current Vibration evidence is in VIBRATION_REVIEW.md.

# Macros event-editor review — 2026-10-09

Macros remains **pending owner approval**. Controller Zone, Buttons and Curves are approved/frozen compositions. The Macros timeline model, event shelf, M1–M4 selection, empty state, lanes and hold/pause semantics were retained.

## Requested refinement

- Selected-event title and input/timing summary remain visible while Inputs / Timing / Actions progressively reveal one editing area.
- Inputs separates spatial button groups from Sticks. Circular stick selectors show the programmed eight-way direction or Neutral; the dot is not live input. Keyboard arrows choose cardinal directions, Shift combines a diagonal and Space/Home returns to Neutral.
- Timing uses clear Hold / Pause afterward fields and 5 ms adjustment actions. Actions exposes Move earlier, Move later, Duplicate and Delete event. Alt+Left/Right reorders the focused event.
- Add event and Preview timeline are workspace actions. Slot clearing lives in Slot actions; recording is unavailable and visually subordinate.
- Selected event has a brighter fill, border and depth. Keyboard focus independently adds the shared detached halo and scale. Focus has room inside the event shelf at minimum size.
- Exact owned M1–M4 paths illuminate directly on the rear thumbnail, with stronger internal contrast for the compact presentation. Full-size Buttons/Curves illumination is unchanged.
- Lane labels read Buttons, Left Stick, Right Stick and Pause. The full end time stays inside the timeline viewport. Neutral holds have a distinct dashed interval rather than disappearing into empty space.

## Verification

- Release build passed, zero warnings/errors.
- Domain suite: 51 passed, zero failed, including trigger geometry, curve provenance/reset, macro slot isolation, timing validation, capacity, reorder and duplication identity.
- WPF review: 64 checks passed, 57 captured states, no binding errors. Includes all editor views, circular-selector keyboard editing, every slot, empty/populated/focus, minimum/maximized/reduced motion, draft retention, preview stop and transitions back to frozen pages.
- Timed client-render GIFs: Buttons 65 frames, Curves 42, Macros 28; 60 ms presentation delay per frame. These sample actual WPF animation/playhead states, not an OS desktop recording.

Evidence directory: artifacts/macros-native/editor-review. Primary images: 45-macros-sequence.png, 49-macros-minimum.png, 50-macros-maximized.png, 55-macros-timing.png, 56-macros-actions.png and 57-macros-stick-selectors.png. Motion: macros-sequence-preview.gif. Machine-readable checks: review.json.

## Scope and truth

Everything is an in-session local UI draft. Hardware contents are unknown. Preview emits no controller or Windows input. Recording, hardware Apply, packet encoding, native device-engine parity, persistence and import/export remain unavailable. UI guards use the preserved reference's 5 ms timing and 47-entry ceiling; actual wire-slot mapping still needs engine/hardware verification. No fabricated live telemetry is shown.

Mouse drag on the circular selector is implemented, while the deterministic harness verifies its keyboard path. Real controller navigation/input, actual monitor-DPI transitions, physical viewing-distance acceptance and FPS are not established by this review. License changes remain parked. Stop here for Macros review.

