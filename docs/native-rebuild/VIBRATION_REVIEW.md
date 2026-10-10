# Vibration local UI review — 2026-10-09

Vibration is ready for owner review. Controller Zone, Buttons, Curves and Macros are approved compositions. Macros' final pass adds Macro actions, grouped paddle/thumbnail context, visible capacity, fixed-width card scrolling and synchronized playhead/card/lane highlights.

## Vibration behavior

- Centered owned controller artwork, prominent draft strength, subordinate offline motor status and a full-width strength shelf.
- One linked 0–100% local setting, percentage presets, 1% keyboard steps (Shift 5%), reset and per-player/model in-session retention.
- The 70% initial value comes from the preserved UI profile default. It is never presented as current hardware strength. Motor readbacks remain unavailable, including when the local setting is zero.
- The preserved Python client documents two motor percentages and its setter updates both together after reading the existing record. This page does not invent independent motors, physical motor positions, trigger haptics, frequency or envelopes.
- Test vibration and hardware Apply remain disabled. Changing the draft does not shake the image, emit input or drive a controller. The timed sequence shows UI transitions only.

## Verification and limits

Release build passed with no warnings/errors. Domain suite: 53 passed, including bounded vibration strength, reset, model guards and independent drafts. Combined WPF report: 82 checks passed, 71 states, zero binding errors. Evidence is in artifacts/vibration-native/review-final/review.json, including minimum/maximized/reduced motion, preset and keyboard changes, unavailable readback, retention and page transitions. The Vibration UI sequence has 24 timed frames at 60 ms presentation delay each; this is UI motion, not physical vibration.

Macros long-sequence evidence includes 10/20 ordinary events and 47 zero-pause events. The reference limit is 47 encoded entries, not always 47 UI events: nonzero pauses consume another entry. Cards remain 190 DIP wide and scroll into view. The final pass also removes the horizontal scrollbar's vertical clipping and verifies the selected card in both dimensions.

Key Vibration images: 63-vibration-default.png, 64-vibration-25.png, 65-vibration-100.png, 66-vibration-off.png, 67-vibration-console-focus.png, 68-vibration-minimum.png, 69-vibration-maximized.png, 70-vibration-reset.png, 71-vibration-reduced-motion.png. Motion: vibration-draft-sequence.gif. Macro stress: 58-macros-{10,20,47}-events.png and 59-macros-47-minimum.png.

Captures are shown app-owned WPF client renders; animation files sample actual intermediate WPF states. They are not OS desktop recordings or physical-device tests. No OS input injection or hardware access. Actual controller navigation, response/intensity, firmware parity, monitor-DPI transitions, physical couch-distance acceptance and FPS remain unverified. Licensing changes are still parked. Stop for Vibration review before Device.
