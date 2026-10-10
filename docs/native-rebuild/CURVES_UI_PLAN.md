# Curves — inherit the approved Buttons language

Owner approved the Buttons visual architecture on 2026-10-09 and directed the work to Curves after the punch list. Preserve the same native frame, navigation rail, working context, controller canvas, environmental light, detached focus and footer.

The next UI exposes four separate local channels: left/right stick and LT/RT. Selection illuminates the owned physical geometry. The Inspector becomes an interactive response plot; the lower shelf contains channel choices, the reference presets and deadzone sliders. Drag two stored points or adjust them with keyboard arrows. Retain drafts per player/model across Studio visits.

Use the reference preset coordinates from x20ctl/protocol.py: Default, Quick, Slow, Smooth and Fine, plus Custom points. The graph is a frontend illustration through the stored points; firmware interpolation is unknown. Deadzones in this local UI use percentages. No encoding, controller readback, engine parity, live telemetry, persistence or hardware Apply is claimed. Hardware state stays unknown. Non-X20 models retain inspection but no curve editing.

Verification: channel isolation, reference preset values, invalid point/deadzone rejection, per-player draft retention, selection of actual geometry, preset/custom/reset flows, minimum/maximized/reduced-motion WPF renders, no binding errors and no enabled hardware write path.
