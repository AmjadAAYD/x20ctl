# Approved Curves baseline — 2026-10-09

The owner approved the Curves design and requested only minor polish before Macros. Controller Zone, Buttons and Curves are now frozen compositions.

Completed: page-specific controls, INPUT / OUTPUT labels, explicit base-preset provenance, human-facing preview copy, readable stick labels, selected-point context, numeric deadzone adjustment and scoped reset actions. Visible percentages are rounded to whole percentages; exact stored values remain in tooltips. Reset options contains the implemented Reset all curves action only. Current reset remains separate.

Trigger orientation tests establish consistency with owned geometry: front LT viewer-left / RT viewer-right; rear RT viewer-left / LT viewer-right; exact labelled source paths and mirrored bounds are retained, without double-transforming the paths. This is artwork/selection verification, not new hardware or wire-slot authorization.

The preserved protocol reference names inner and outer deadzone fields for sticks and triggers, with max-progress scales 100 and 200 respectively. Native UI percentages remain local normalized drafts, not serialized bytes or measured activation thresholds. Future engine integration must verify channel-specific semantics and conversion before writes, then refine copy if needed.

Final Curves evidence: artifacts/curves-polish/frozen-review — 51 WPF checks, 43 states, 65 Buttons frames and 42 Curves frames. Domain suite before Macros: 46 passed, including two added trigger-geometry tests. Build passed without warnings/errors. Captures are app-owned shown-window client renders; no OS input injection or hardware access.

Checkpoint: C:/Users/amjad/.codex/checkpoints/x20ctl-native-rebuild/20261009-approved-curves, saved before Macros UI integration. Existing motion and geometry are preserved. Live input and actual controller response remain deferred to the C++20 engine.
