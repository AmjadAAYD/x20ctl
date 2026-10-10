# Desktop launch bounds validation

Windows launches now prefer an outer window size of **1583 × 1147**, centered within the selected monitor's working area. Taskbars on any edge are excluded. A dimension that exceeds the available space shrinks with a 16 px margin. The minimum resize size also shrinks on small displays. Fresh launches use the preferred size again; user resizing and tray restoration remain available.

The pinned pywebview 6.1 Windows host applies DPI autoscaling and multiplies move coordinates. The launcher instead registers a native Form.Load callback, completes autoscaling, then assigns native MinimumSize and Bounds using WorkingArea coordinates. This occurs before the first visible frame, following the documented [Form.Load timing](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.load). Linux launch sizing is preserved.

Verification on 2026-10-02:

- `python -m pytest tests/test_window_geometry.py -q`: **17 passed**. Fixtures cover the preferred size, exact fit, bottom/left/top taskbars, negative monitor coordinates, portrait and small displays, minimum-size clamping, invalid work areas, startup wiring, and simulated 100/125/150/200% scaling.
- Focused Ruff checks and Python compilation passed for the launcher, geometry helper and tests.
- PyInstaller built `dist/local-window-fit-20261002/x20ctl.exe` successfully. Existing optional pycparser hook warnings remain.
- Extracted launcher and geometry code objects equal locally compiled source. All **34** embedded frontend files and the controller catalog byte-match the current source assets.
- Executable: **83,020,122 bytes**, SHA-256 `1517066e0f968cf6863bc8e7ee60d4bb335aed22652ee5121f791f0469684977`.
- Build evidence: `artifacts/window-fit-review/local-build.json`.

No desktop window was shown, no screen was captured, and no controller was accessed. Native placement on the user's display remains unobserved under the continuing request not to use that screen. No release, upload or push was performed.
