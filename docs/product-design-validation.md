# Product design refinement — 2026-10-08

Implemented locally on `codex/native-2.0-recovery`. No version, tag, release, binary publication or push.

## Design reference and decisions

Reviewed the supplied 56.5-second ApexSenseBridge recording across its timeline (eighteen scene samples), plus its stable MainWindow and ConsoleTheme definitions for design reference. The useful concepts are quiet navy surfaces, separated content/selection/feedback, broad option rows, a stable hardware visualization, crisp focus, and library selection with a separate inspector. No source implementation, branding, logo, controller artwork or other assets were copied.

X20CTL keeps its own brand, controller photos, blue/warm identity and four-player model. The initial muted sidebar pass was superseded by the console revision: horizontal sections beneath a unified top line, an animated selection underline, soft option rows and focused inspectors. Arrow keys/Home/End move navigation focus; Enter activates it. Seven sections remain present. Orange is reserved mainly for draft/warning and vibration feedback. See `docs/console-design-validation.md` for the current revision's evidence; the results below record the first pass.

## Changes

- `src/product-design.css`: common surface/color/spacing/radius/motion tokens, static ambient navy, subtle app frame, consistent card surfaces, muted inactive controls, crisp focus, restrained sliders, responsive layouts and reduced-motion rules. Legacy sheets retain necessary geometry and previous fit repairs; wholesale CSS deletion was deliberately avoided.
- `src/components/ControllerHub.tsx`: four controller cards with distinct player/assignment metadata, large artwork and quieter entry actions. Assignment still does not claim a hardware connection.
- `src/components/pages/MacrosPage.tsx`: slot summaries first, then the existing sequencer, piano roll, library/copy, timing, loop and local preview controls. Recording lives in the editor. Returning to overview stops local playback without losing draft steps. Recording prevents leaving the editor through section navigation until stopped.
- `src/components/pages/SetupsPage.tsx`: coherent extraction from App; visual setup selection and a side inspector with stored values, real creation date, rename, load into draft and confirmed delete. Import/export and legacy profile behavior continue through the existing service calls.
- `src/components/pages/TesterPage.tsx`: controller-centered input rendering and optional detailed readings. Buttons, stick position and trigger travel come from the existing sample; unavailable data remains unavailable. Preview studios pass their own model into the shared tester.
- `src/hooks/useStudioNavigation.ts` and `src/studio-navigation.ts`: opt-in X20 studio navigation reads existing samples. D-pad/stick moves focus, A activates only marked navigation targets, B returns to overview/players, LB/RB changes sections. Tester, recording, dialogs, busy operations, text editing and inactive/unfocused windows suspend navigation. Held activation buttons cannot fire on resume. Hardware writes/reset/record controls are never marked activation targets. Preview studios and the player hub retain mouse/keyboard navigation; gamepad navigation is not enabled there.
- `src/components/Motion.tsx`: keyboard navigation handling and removal of rendered dust/star fields. Decorative controller sweeps are suppressed; state/input feedback remains.
- Preview support copy is concise; the Known/Missing lists remain in Technical details.
- Scanner has a six-stage guide and a concise explanation of reads, local storage, optional tests and sharing consent. Its collector, permissions, review and upload semantics are unchanged.
- README credits UI/UX design inspiration to ApexSenseBridge by ReynArts with permission, without implying code contribution or endorsement.

## Deterministic verification

| Command | Result |
| --- | --- |
| `npm run lint` | PASS — TypeScript check, exit 0 |
| `npm test` | PASS — 41 tests, 0 failed |
| `npm run build` | PASS — 1698 modules, exit 0; existing large-chunk warning remains |
| `python -m pytest -q` | 915 passed, 1 skipped, 1 failed; exit 1 |
| `node tools/product_design_review.cjs` | PASS — 22 captures at 1400×940 and 1100×760, no browser runtime errors or document horizontal overflow |
| `$env:X20CTL_REVIEW_OUTPUT='artifacts/product-design/scanner-regression'; node tools/x15_scanner_review.cjs` | PASS — all 9 existing scanner/model regression checks |
| `git diff --check` | PASS — exit 0 |

The Python failure is `tests/test_capability_discovery.py::test_scanner_import_and_removal_update_saved_profile_and_review`: `ResearchScanner.attach_file` accesses absent `attachment_scopes`. The same failure reproduced in the untouched checkpoint copy with `python -m pytest tests/test_capability_discovery.py -q` (12 passed, 1 failed). It belongs to prior uncommitted scanner work and was not changed in this UI pass.

Browser checks cover macro overview/editor draft retention, keyboard focus, bumper navigation, tester/recorder/modal/text-edit suspension, marked-only A activation, live fixture button/trigger/stick rendering, local setup rename, delete confirmation, and absence of configuration writes/resets/capture/uploads in the new design harness. The existing scanner harness additionally covers read-only input for X15/D10/X05/X10, X05 Pro preview, support guards, capture review, retained ZIP after simulated upload failure and manual fallback. All bridge results are fixtures; no physical device or Internet submission was used.

## Visual review

All ten requested views are captured under `artifacts/product-design`: controllers, buttons, curves, macro overview, macro editor, vibration, power/device, tester, saved setups and X15 preview. Additional captures include expanded tester readings, preview technical details, scanner and compact variants. Screenshots were reviewed for clipping, spacing, controller framing, excessive glow, contrast and focus. Review found and corrected stale legacy glow overrides, misplaced macro heading/recorder grid items, tester grid placement, and a compact capture that had retained the previously selected section.

Vertical scrolling remains intentional on detailed editor, expanded readings and compact layouts. The modal backdrop is viewport-fixed; full-page captures can show content below the visible viewport. Native window dragging, tray/single-instance behavior and physical controller navigation/motion have not been verified in this pass. Native/window and hardware code was not changed.

## Preservation evidence

External pre-edit ZIP: `C:/Users/amjad/.codex/checkpoints/x20ctl-product-design-20261008/snapshot.zip`.

SHA-256: `258A9402E094FCE7D05BD774A3C6BC13BC4D940E96FC1782CC25BB09F4D6A622`. All 669 archive entries passed CRC inspection. Backend scanner, capability discovery, its test, existing scanner CSS, package/version configuration and changelog were byte-identical to the checkpoint. The existing dirty scanner review tool received only a one-line selector update from Known/Missing to Technical details; all its prior changes were retained. The dirty scanner TSX received only the stage guide and introductory presentation block on top of its existing work.

Evidence: `artifacts/product-design/report.json`, `preservation.json`, screenshot PNGs and `scanner-regression/report.json`.
