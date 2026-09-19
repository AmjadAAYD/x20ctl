# Metallic desktop interface replacement

User direction: replace the entire x20ctl interface with a metallic grey design based on the nine supplied references. Continue was approved September 18, 2026. Keep the x20ctl identity and Windows desktop packaging.

## Current execution state

Lifecycle owner: Plumbline Execute. The existing user-approved design and this
work order control the task. The user authorized implementation, verification,
commits, pushes and release publication without repeated permission prompts.

Current checkpoint: complete and published as v3.0.0. All UI work is integrated.
Code review found no actionable logic regressions. Native source acceptance
passed 23 checks, with 14 real captures and both normal/minimum page layouts.
Expanded checks protect remap changes, macro chords, both stick directions,
hold timing, profile round trips and accessible dialogs. A harness selector was
corrected to target the active dialog, not its background editor.

All 503 Python regressions, 170 focused native tests, two frontend curve tests,
TypeScript and formatting checks passed. The current run has no BLE/XInput
device; Windows Bluetooth discovery reports the adapter is not ready. This is
an observed environment limit, not fresh hardware proof. The bundled
`dist/x20ctl.exe` exited with code 0 and passed all 23 packaged checks. Its
3.0.0 screenshots are tracked under `assets/screenshots/desktop`, the
validation notes and changelog are reconciled, and GitHub `main`, tag `v3.0.0`
and the release assets are published.

## Visual specification

- Horizontal navigation replaces the left sidebar. A compact top chassis carries the brand, configuration status and connection action. A persistent bottom action strip carries read/apply and draft status.
- Graphite background, brushed steel panels, silver bevels and cyan selected controls. System sans-serif for legibility, monospaced values and restrained engraved labels. No warm brown or copper surfaces remain.
- Rebuild the controller illustration, remap inspector, response curves, macro step sequencer and piano roll, vibration, live input tester, power/device page, saved setup library, device scanner, help and confirmation dialogs.
- Connection animation represents scanning only. All displayed readings come from the existing native bridge. Do not introduce invented polling rate, latency, temperature, power consumption, haptic waveforms, firmware flashing or unsupported analogue macro precision from the visual references.
- Render empty, loading, error, disconnected and focused states in the same design. Support the existing minimum desktop viewport and larger windows without clipped actions.

## Implementation

1. Preserve the verified Python protocol/bridge and React state/persistence contracts.
2. Main agent replaces App shell, shared theme, profile library, vibration/power screens and dialogs. Separate agents own curves, macros, and controller/tester components with isolated CSS.
3. Keep actual draft/apply semantics, per-category failure handling, saved profiles, import/export, recording and reset confirmation.
4. Build and run the local desktop UI, capture real screenshots, inspect every screen and dialog. Check keyboard focus, scrolling and minimum-window layout.
5. Update native-window acceptance selectors to the new real UI. Run appropriate frontend checks, existing regression coverage and packaged acceptance. Provide a working EXE and genuine captures.

## Acceptance

- Every old UI surface is replaced, not merely recoloured.
- User references are implemented as interface styling, never embedded as pretend screenshots.
- No controller write is required to validate the visual redesign; preserve the user's actual settings during UI testing.
- Document exact verification performed. Publish only after the built desktop application is verified.
