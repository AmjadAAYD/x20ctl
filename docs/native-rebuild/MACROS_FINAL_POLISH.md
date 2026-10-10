# Macros final polish and freeze gate — 2026-10-09

Owner approved the architecture, requiring a final small polish pass before Vibration. Preserve the complete event-editor structure.

- Rename Slot actions to Macro actions.
- Treat the M1–M4 rail and rear thumbnail as one compact component, with less separation and retained exact paddle illumination.
- Show event count plus capacity. The preserved X20 reference limits encoded entries to 47: holds cost one and nonzero pauses cost another. Do not mislabel this as an unconditional 47 UI events.
- Keep cards fixed at 190 DIP; selected/preview cards scroll into view. Verify 10 and 20 ordinary events and 47 zero-pause events at normal/minimum/maximized sizes.
- Keep editing selection separate from preview state. A bright thin playhead, active card and active lane phase synchronize to the local timeline clock. No device or Windows input is emitted.
- Refresh screenshots/motion, then checkpoint Macros as the owner-approved composition and proceed to a local Vibration UI milestone. Hardware recording, writes and applied-state truth stay unavailable.
