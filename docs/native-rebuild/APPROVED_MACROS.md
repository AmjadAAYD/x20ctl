# Approved Macros baseline — 2026-10-09

Owner approved the architecture and required a final small polish pass. That pass is complete. Freeze the timeline, event shelf, progressive Inputs / Timing / Actions, circular direction selectors, M1–M4 selection, empty/populated states and overall proportions.

Macro actions replaces Slot actions. The paddle rail and exact-geometry rear thumbnail share one compact context. The header shows UI event count and capacity separately: the preserved reference has 47 entries, with each hold and each nonzero following pause consuming an entry. A blanket 47-event label would misrepresent ordinary events with pauses.

Cards stay 190 DIP wide and scroll to editing/preview selection. The stress matrix verifies 10 and 20 ordinary events and 47 zero-pause events, including minimum/maximized with the last event selected. The bright thin playhead, active event card and appropriate hold/pause lane phase synchronize to local time. Preview state remains separate from editing selection and emits no input.

Freeze evidence: artifacts/macros-native/freeze-review — 74 passed WPF checks, 62 states, plus timed animation files. Domain suite at this boundary: 51 passed. Build: zero warnings/errors. Hardware contents, recording, native protocol/slot parity and actual Apply remain unavailable. No licensing changes.

Initial checkpoint: C:/Users/amjad/.codex/checkpoints/x20ctl-native-rebuild/20261009-approved-macros. Final checkpoint: C:/Users/amjad/.codex/checkpoints/x20ctl-native-rebuild/20261009-approved-macros-final. Long-sequence inspection found scrollbar-induced vertical card clipping; horizontal scrolling now uses selection snap and mouse wheel with the scrollbar hidden. The final check verifies both dimensions. Refreshed evidence is in artifacts/vibration-native/review-final, including full 47-event minimum/maximized states. Next page is the local Vibration milestone.
