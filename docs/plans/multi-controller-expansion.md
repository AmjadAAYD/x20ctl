# Multi-controller expansion, unreleased

Source: all 41 pages of the supplied expansion PDF and the supplied X20 Pro research report, read 30 September 2026. The current React/pywebview UI is the design baseline; the PDF's older Qt implementation examples are architectural guidance.

## Scope and boundaries

- Preserve the existing theme and verified X20 protocol.
- Registry retains X20 (4 macros) and X20 Pro (6). On 1 October, the user explicitly approved X05 (0), X05 Pro (2), X10 (2) and D10 (2) as UI-only previews. This supersedes their removal on 30 September; see `../preview-models-validation.md`.
- Separate selected artwork/model from physical connections. Switching disconnects the old session and stops its input polling.
- X20 Pro supports local draft editing and visual previews. No scans, reads, writes, calibration, import/export, or device identification until hardware validation.
- Every launch opens the four-player landing screen. Each empty player opens the model picker; selection assigns that player for the current session without claiming a physical connection. Assigned cards enter their own Studio. Switch Controller changes that Studio's player; Players returns to the landing screen.
- Studios retain independent drafts for each player/model during the app session. Only one hardware configuration session is active; changing players or returning to the hub disconnects it.
- Shared photographic canvas uses normalized HTML control overlays, RGB regions, display bounds, and localized motor zones.
- Legacy profiles default to X20; reject cross-model application before device access. Preserve six-slot saves.
- Controller scan ZIP stays local; no automatic upload.
- No release, tag, publish, deployment, or updater change.

## Local mapping

`src/App.tsx`: per-player session selection and existing X20 studio. `ControllerHub`: four-player entry. `ControllerCanvas`: shared photographic rendering and overlays. `ModelWorkspace`: local draft Studio for unverified models. `MetalSlider`: shared slider interaction. `x20ctl/desktop/service.py`: native player/model boundary. `x20ctl/profiles.py` and `desktop/settings.py`: legacy and desktop profile validation. `desktop/reports.py`: existing constrained scanner ZIP.

## Verification

Baseline: 61 focused desktop/profile/report tests passed. After implementation: profile migration and model-isolation regressions, frontend typecheck/tests/build, and browser checks of switching, disabled Pro actions, and overlay alignment at multiple scales. Real-controller behavior cannot be validated without hardware. Manufacturer image redistribution rights remain a release prerequisite.

1 October continuation: 185 focused Python tests and 4 frontend tests passed; final TypeScript/build checks passed. Source Windows desktop acceptance passed 34 checks with 28 native screenshots, including all four assignments, independent drafts, both models at normal/compact sizes, Pro hardware guards and fresh reload startup. See `docs/multi-controller-validation.md`. A new packaged EXE was not built and no hardware writes were tested.
