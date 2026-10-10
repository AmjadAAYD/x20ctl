# Macro library and X15 preview

Preserve the current working tree, X20 encoding/write path, and all existing controller artwork. No screen or controller access, publishing, or release.

## Implementation

- Add one reusable macro library to the shared Macro Studio: save a named sequence with its loop interval and source model, load into the selected slot, duplicate to another slot, remove a library entry, and validated JSON import/export through desktop file dialogs. Persist the library in the app data directory separately from full setups.
- Offer fixed pauses and no pauses for existing sequences. Reuse the current 5 ms timing grid and 47-entry editing budget; reject malformed or oversized sequences before changing a draft. Keep the visual preview local. Do not add hold/toggle/conditions, host game output, or unverified hardware writes.
- Add X15 to the shared registry with official-source capabilities, backend null, two rear programmable controls, front/rear artwork, stickless base and opaque stick overlays, measured input shapes, shell framing, localized lighting, and grip vibration contours. Preview controls describe local drafts, not established X15 protocol support.
- Match the existing shared visuals and player assignment flow. Existing X20 and X20 Pro artwork remains untouched.

## Verification

Run focused macro serialization/library tests, controller registry/preview guards, frontend typecheck/build, isolated headless flow checks with a disconnected mock bridge and several viewport sizes, and a local desktop package build. Do not claim live native UI or hardware playback was checked.
