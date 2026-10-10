# Larger Macro controller preview — 2026-10-02

The shared Macro preview now uses measured detail framing rather than the smaller
inset presentation. Its desktop rail grows from 290px to 340–440px (392px at a
1400px window), retaining the editor beside it. Narrow layouts use up to 400px.
The complete measured shell retains at least 5% canvas margins; photographs are
not stretched or edited and all button geometry scales with the same photo plane.

Frontend type check and build passed. The focused Macro review passed X20,
X20 Pro, X05 Pro, X10 and D10 at 1920, 1400, 1060 and 800px window widths:
20 framing/layout cases, all 16 Macro destinations, aligned target bounds, original
1536px source images and no horizontal overflow. X05 has no Macro controls, so
its other screens are unchanged. Reports/screenshots: `artifacts/macro-preview-fit`.

Separate local Windows build: `dist/local-macro-preview-fit-20261002/x20ctl.exe`.
Packaging/integrity evidence: `artifacts/macro-preview-fit/local-desktop-build.json`.
Reviews were isolated/headless with a mock disconnected bridge; no host screen,
native GUI or physical controller access. Existing builds and dirty work preserved.
