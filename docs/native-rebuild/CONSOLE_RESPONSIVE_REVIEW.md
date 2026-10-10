# Maximized shell, responsive polish and visual vibration review — 2026-10-09

Requested pass completed; stop for owner review before Device/Tester. Approved compositions remain intact. No controller protocols, hardware writes, release or publishing.

## Window behavior

Normal app launches start maximized. Startup uses the cursor monitor, centers native restore bounds there, then maximizes to its work area. Restore/minimize/close remain native and restoring is allowed for the session. Monitor-relative maximum bounds and DPI-aware minimum tracking handle negative origins and taskbars on other edges. PerMonitorV2 remains enabled. WPF processes DPI changes; maximized windows reapply the current work area afterward.

Observed startup on this host: HWND bounds 0,0 / 1920×1146 exactly matched the reported work area, at 110 DPI. One connected monitor; zero actual monitor-DPI transitions occurred. Physical cross-monitor transitions are therefore unverified. Work-area/origin/DPI math is tested independently at 96/120/144 DPI. See Microsoft's [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged) and [MINMAXINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-minmaxinfo) contracts; the implementation preserves WPF's DPI handling rather than applying a second UI scale.

## Shared responsive changes

Maximized is the primary view. Bounded navigation/shelf/footer sizing and key typography use additional space. Artwork and plots grow with their existing layouts. Footer hints, optional context and fixed-size Apply are separated; secondary hints yield at minimum width. Disabled Apply remains subdued and has no frontend write authority.

## Macro overflow and timeline

Whole-card snapping replaces arbitrary pixel offsets. Cards remain 190 DIP in restored/minimum views and 220 DIP in large maximized views. End padding and partial-neighbor masking prevent half cards at viewport boundaries. Wheel and keyboard selection navigate the shelf; first/middle/last of 47 events were verified, including minimum size.

Long timelines use a proportional horizontal viewport with detail zoom instead of squeezing all intervals into the window. Selection and preview follow time into view. Lane labels remain anchored while panning. Active event blocks brighten, with phase emphasis, and editing selection remains distinct. Capacity stays truthful: 47 encoded entries; nonzero pauses also consume entries. Full capacity has an explicit message and unavailable Add event.

## Vibration visualization

Owned ControllerOutline geometry drives tight and propagating contours. Frequency, distance and opacity increase with local strength. Grip-region ripples use the same linked preview percentage; these are illustrative grip regions, not independently verified physical motor mappings. Existing RGB artwork is unchanged and is not shaken.

Zero clears contours and stops the animation clock. Reduced motion shows static strength contours only. Focused-but-unselected presets keep a dark fill and detached halo; the selected value keeps blue fill. Visual preview is explicit. Motor readback, Test vibration and actual Apply remain unavailable.

## Evidence

- Release build: passed, zero warnings/errors.
- Domain suite: 57 passed, zero failed.
- Existing Studio regression: 82 passed checks, 71 states; no binding errors.
- New maximized/responsive review: 52 passed checks, 54 states; no binding errors.
- Actual maximized, restored and minimum renders cover Zone, Buttons, Curves, Macros and Vibration.
- Root-DPI render matrix covers 100/125/150%, including 1920×1040 at all three scales and representative 2560×1400/3840×2120 work-area resolutions. These are labelled detached WPF renders, not system scaling changes or actual monitor transitions.
- Vibration 0/25/50/75/100/63%, focused-unselected and reduced-motion states captured. The 72-frame GIF samples actual WPF animation states at 100 ms presentation intervals, returning to zero. Read-only pixel inspection found zero change in the final off controller region.

Primary evidence: artifacts/console-responsive/complete-review. Start with maximized-vibration-75.png, vibration-visual-0-to-100-to-0.gif, maximized-macros-47-selected-47.png and review.json. High-DPI files are explicitly prefixed render-dpi. Regression evidence: artifacts/console-responsive/studio-regression. Pure tests: artifacts/console-responsive/tests/console-presentation-domain.trx.

All captures stay inside the app-owned WPF review harness. No OS input injection or hardware access. Physical motor behavior, engine parity, real controller navigation, physical couch-distance acceptance and FPS are not established. Single-monitor availability limits native transition verification. Licensing stays parked.
