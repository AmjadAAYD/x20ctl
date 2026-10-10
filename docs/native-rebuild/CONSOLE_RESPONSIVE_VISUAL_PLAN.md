# Maximized shell and vibration visual preview — 2026-10-09

Owner explicitly requests a global responsive polish pass, not redesigns. Device/Tester remain deferred. No controller protocol, hardware writes, release or publishing.

- Normal launches maximize to the startup cursor monitor's work area. Keep native restore/minimize/close and PerMonitorV2 awareness; use monitor-relative native bounds, negative-coordinate-safe work-area math and DPI-aware minimum tracking. WPF processes DPI changes; reapply work-area bounds if maximized.
- Maximized is the primary review. Increase bounded content/control spacing and important typography, with restored/minimum as compatibility views. Make footer hints/context yield space before Apply.
- Macro cards retain a readable fixed width. Snap the shelf by whole cards with end padding, mask partial offscreen neighbors, and navigate first/last. Keep the true 47-entry capacity budget distinct from UI events. Add a proportional timeline viewport/zoom that follows selected/preview time rather than squeezing all 47 intervals into one view.
- Vibration uses the owned ControllerOutline geometry: a tight contour, a propagating/fading second contour, and subtle paired grip-region ripples. Speed, distance and opacity follow the local percentage. Zero stops/disappears; reduced motion uses static contours. Artwork RGB remains unchanged. Label Visual preview, and retain unavailable readback/Test/Apply.
- Record 0/25/50/75/100/63%, focused-unselected, reduced motion, maximized/restored/minimum and a timed 0→100→0 visual sequence.
- Review all five pages maximized on the actual available monitor. Add representative 100/125/150% WPF root-DPI/resolution renders and pure monitor/work-area tests. Clearly distinguish render simulation from observed physical monitor transitions; report host limitations.

Preservation checkpoint: C:/Users/amjad/.codex/checkpoints/x20ctl-native-rebuild/20261009-before-responsive-visuals.
