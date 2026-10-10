# Dynamic Controller Zone development preview

Default: four equal empty docks. Click/Enter expands any player; Escape/Back returns to overview. Tab/arrows navigate. F6 enables simulated gamepad navigation (A/Enter expands, B/Escape collapses). F9 cycles empty, one-controller and multiple-controller mock assignments. No live hardware access or saved-config migration.

Release compilation and state/layout tests pass. The new motion/UI has not been run or captured after the user revoked desktop automation. Existing static screenshots are historical.

Latest isolated executable: `artifacts/dynamic-controller-zone/build/bin/X20Ctl.Product/release/X20Ctl.exe` relative to repository root. This avoids touching a running prior build. The app was not restarted automatically.

`--reduced-motion` forces immediate layout changes. `--review-zone DIRECTORY` is an optional manually invoked static WPF render helper, not screen capture or proof of animated behavior.

Stop for visual approval before another page.