# Desktop launch bounds

Use the requested 1583 × 1147 outer window size on every fresh Windows launch. Center inside the monitor's working area, excluding taskbars. If either dimension cannot fit, reduce that dimension and leave a 16 px edge margin. Reduce the minimum resize dimensions on small displays as well.

The current pywebview host uses WinForms DPI autoscaling and default screen centering. Apply the final native bounds during Form.Load, after automatic scaling and before the first visible frame. Keep normal user resizing and tray restoration. Preserve Linux hosting and all controller/UI work.

Verify the placement math and startup event wiring against display fixtures, including taskbars, negative monitor coordinates, small displays and simulated DPI scaling. Build a new local Windows executable. Do not use the user's desktop or controller, publish, or replace earlier builds.
