# Console design revision — 2026-10-08

This revision responds to the user's request to carry the reference's animation, design, UI, UX and style into X20CTL more substantially. The earlier muted-sidebar pass was too conservative.

## Implemented

- One top line for the X20CTL brand, device context and utility actions; remove the separate sidebar and header boxes.
- Horizontal studio sections with concise visual labels and unchanged full accessible names. Keyboard arrows/Home/End and opt-in bumper navigation continue to work.
- A measured, animated selection underline follows the selected section in 220 ms. Focus uses a crisp light ring and restrained selection fill.
- Quiet navy frame, rounded panels and soft option rows. Primary action uses blue; warm identity remains a small support/draft/vibration accent.
- Reference-like button family: neutral dark pills, white selected options with dark text, blue primary actions, soft hover lift and short press feedback. Controller front/rear selection, curve presets, macro slots and device presets share the same selection language.
- Interaction-dependent life: a faint pointer-follow highlight on player cards, tiny setup-row motion and a brief success-confirmation entry. No continuous card breathing or background movement. Reduced motion disables the transforms.
- Button assignment shows the selected control first; the full mapping list expands under All assignments. Both use the same draft callbacks and values.
- Shared page entry is 210 ms; dialogs use a 220 ms opacity/position/scale transition. Reduced motion suppresses these animations and transitions. Background lighting remains static.
- Navigation/layout changes apply to X20 and existing preview/input studios. No controller model, native window, protocol or write capability changes.

Files: `src/App.tsx`, `src/components/ModelWorkspace.tsx`, `src/components/InputWorkspace.tsx`, `src/components/Motion.tsx`, `src/components/StudioSections.tsx`, `src/components/pages/ButtonsPage.tsx`, `src/product-design.css`, and `tools/product_design_review.cjs`.

## Verification

| Check | Result |
| --- | --- |
| `npm run lint` | PASS, exit 0 |
| `npm test` | 41 passed, 0 failed |
| `npm run build` | PASS, 1698 modules; large-chunk warning remains |
| `python -m pytest tests/test_desktop.py tests/test_controller_registry.py tests/test_recording_flow.py tests/test_profiles.py tests/test_research_boundary.py -q` | 266 passed |
| `$env:X20CTL_DESIGN_OUTPUT='artifacts/console-design'; node tools/product_design_review.cjs` | PASS; 23 normal/compact views, 8 behavioral checks, four motion frames |
| `$env:X20CTL_REVIEW_OUTPUT='artifacts/console-design/scanner-regression'; node tools/x15_scanner_review.cjs` | PASS; 9 scanner/model regression checks |
| `git diff --check` | PASS |

The browser harness verifies that the underline reaches the selected tab's position and width, measures the transition duration, checks page/dialog entry animations and the reduced-motion alternative, and retains the earlier tester/recorder/modal/text-edit navigation guards. It checks that selected-control editing and the expandable mapping list reflect the same draft without hardware writes. No browser runtime errors or document horizontal overflow were found at 1400×940 or 1100×760.

Rendered normal/compact views were reviewed for spacing, clipping, focus, panel hierarchy and controller framing. A inherited full-width support-button rule was corrected after rendering. Capture scroll is reset before full-page screenshots so off-viewport fixed accessibility controls do not appear as screenshot artifacts.

Evidence: `artifacts/console-design/report.json`, PNGs, `motion-tab-0.png` through `motion-tab-3.png`, and `scanner-regression/report.json`. These use the production bundle and isolated fixture bridge. Physical hardware, native-window motion and packaged EXE behavior remain unverified; no executable was rebuilt or released. The full Python suite was not rerun for this frontend-only revision; the scoped 266-test result is the current backend regression evidence.

Pre-revision snapshot: `C:/Users/amjad/.codex/checkpoints/x20ctl-console-revision-20261008/snapshot.zip`, SHA-256 `6F4651BA405294AD0C4DFBB9398FE21593D31647D1EFD5ED0700872ED479F87F`. Concurrent scanner work was left intact. No version, tag, publication or push.
