# Vibration local UI milestone — 2026-10-09

Proceed after the approved Macros freeze. Preserve Controller Zone, Buttons, Curves and Macros. Use the shared native shell, context, atmosphere, controller artwork, console focus and reduced-motion behavior.

Reference: x20ctl/client.py vibration() returns two documented motor percentages; set_vibration(percent) changes both together after reading the existing record and preserves all trailing bytes. This UI exposes that linked strength, not independent channels or trigger haptics. Additional motor fields/physical channel positions remain unverified for this milestone.

Composition: controller centered as the hardware anchor, a prominent local strength value alongside it, a subordinate offline hardware-status area, and a full-width strength shelf with large percentage choices plus precise keyboard/slider adjustment. A readback placeholder stays explicitly unavailable. No invented frequency/envelope controls, live effects or motor-region overlays.

Behavior: 0–100% local linked-strength draft, presets, reset, per-player/model in-session retention. Only X20 edits are enabled. Test vibration and hardware Apply remain disabled until the C++ engine connects and authorizes a bounded operation. No visual animation pretends to be physical vibration.

Verify ranges/model guards/reset/isolation, slider/preset keyboard interaction, no enabled hardware action, minimum/maximized/reduced motion, page/draft transitions and binding diagnostics. Capture the result and stop for owner review before Device.
