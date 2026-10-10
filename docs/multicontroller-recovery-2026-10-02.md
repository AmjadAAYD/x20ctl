# Multi-controller recovery — 2 October 2026

## Latest user correction

The user explicitly requested removal of **Players** and a single **Switch
Controller** action that returns directly to the four-player **Controller zone**.
Both Studios now follow that flow, preserving assignments and session drafts.
Choosing or changing a model happens from the player cards. This supersedes the
earlier dual-action implementation described below.

After this correction, TypeScript checking and the production build passed.
The updated hidden native WebView2 review passed 11 checks, including the absence
of a Players button and returning straight to the hub without opening a picker.
Current native evidence: `artifacts/controller-zone-return-2026-10-02/report.json`.

Recovered the current intentional working tree without resetting, reverting,
committing, pushing, publishing or rebuilding release packages.

## Scope recovered

This recurring prompt predates later manual sessions. The automation memory and
`docs/preview-models-validation.md` record the user's explicit approval to restore
X05/X05 Pro and add X10/D10 as UI-only previews. The current registry and picker
contain **six models**. Those intentional additions were preserved; this run does
not claim compliance with the older requirement for an exclusively two-model
picker. All non-X20 models still have no configuration backend.

The later sessions also prohibited screen access and protected the X20/X20 Pro
artwork. This review did not capture the user's screen, control their open app,
alter artwork or remove newer models.

## Earlier implementation in this run (superseded)

The existing Studio button called “Switch Controller” had been wired to return to
the player landing page. Both Studios now provide separate actions:

- **Players** disconnects the configuration session and returns to the cards.
- **Switch Controller** opens the current player's picker directly. Choosing a
  model enters that same player's Studio; cancelling keeps the current Studio.

Player/model drafts remain session-local. Switching Player 2 from Pro to X20 and
back preserves Player 2's draft and Player 1's assignment. Launch and fresh reload
still open four unassigned players rather than a Studio.

Updated the existing native/offscreen review scripts for these distinct actions.
Added `tools/multicontroller_review.py` for a repeatable hidden WebView2 check.

## Visual assessment

Preserved and verified the existing navy, blue-left/amber-right artwork, complete
static preview plates and stickless interactive base plates. No artwork changes
were necessary in this run. Offscreen rendered images were inspected for the
four-player cards, both compact mapping Studios and both vibration presentations.

The controllers remain fully visible and undistorted. Interactive artwork uses
the inset 84% plane with model-specific geometry; caps are opaque and neutral
centers match independently measured artwork points within 0.1 CSS pixel.
The added header action fits within both tested window sizes. Vibration effects
stay localized to two X20 zones and four Pro visual zones. Shared sliders respond
directly during dragging and retain transitions for preset changes.

## Verification performed

| Check | Result |
| --- | --- |
| `npm run lint` | Passed after final product changes |
| `npm run build` | Passed after final product changes; rebuilt `dist-ui` |
| `npm test` | 4 passed; tested helpers were unchanged afterward |
| Focused registry/desktop/profiles/reports Python tests | 228 passed; production Python service unchanged afterward |
| Python syntax checks for review scripts | Passed |
| `git diff --check` | Passed |
| Hidden native Windows desktop review | 10 checks passed, no report error |
| X20/X20 Pro offscreen visual/pointer review | Passed, 14 rendered images |
| Four newer preview-model regressions | Passed, 17 rendered images; protected X20/Pro hashes and registry objects unchanged |

The native review uses the **real desktop launcher, production assets, Windows
WebView2 host and desktop bridge** in its own hidden window with temporary profile
storage and a separate smoke mutex. It does not use a browser-preview bridge mock.
Only its temporary update preference is disabled. No screenshots are captured.
DOM input events are synthetic; trusted OS input and drag latency are not measured.

It verified startup, independent assignments, direct switching/cancellation,
draft restoration, four-player assignment, fresh reload, both models' geometry
and stick preview motion, resizing to 1400×940 and 1060×760 outer-window sizes,
shared slider transitions, localized motor animations and Pro's six macro controls.
Pro scan/connect/input/apply/reset requests were rejected by the real bridge before
hardware access. Expected rejection warnings appear in the test command output.

The separate headless Chrome checks use disconnected bridge fixtures for frontend
rendering. They cover normal/compact views, actual headless pointer dragging and
keyboard range navigation. Their screenshots are offscreen renderings, not native
desktop captures. The four newer models were checked offscreen only in this run.

Commands:

```powershell
npm run lint
npm run build
npm test
.venv/Scripts/python.exe -m pytest tests/test_controller_registry.py tests/test_desktop.py tests/test_profiles.py tests/test_reports.py -q
.venv/Scripts/python.exe tools/multicontroller_review.py --output artifacts/multicontroller-hidden-2026-10-02-verified
node artifacts/multicontroller-visual-2026-10-02/review.cjs
node tools/preview_models_review.cjs --playwright C:/Users/amjad/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright --browser "C:/Program Files/Google/Chrome/Application/chrome.exe" --output artifacts/preview-models-2026-10-02
```

Reports:

- `artifacts/multicontroller-hidden-2026-10-02-verified/report.json`
- `artifacts/multicontroller-visual-2026-10-02/report.json`
- `artifacts/preview-models-2026-10-02/report.json`

The first hidden-review attempt stopped because the harness required an exact
“Maximum” text match, while X20's preset includes descriptive text and its value.
The corrected helper recognizes the preset's heading. The focused retry passed;
no product change was needed for that harness failure.

## Remaining boundaries

No physical controller operation, BLE scan/connection, firmware change or protocol
write was performed. Physical X20 validation, X20 Pro protocol reverse engineering,
motor independence, display/lighting commands and persistent Pro hardware profiles
remain unverified. Player assignment does not bind a physical XInput slot, and
there is still one hardware configuration session at a time.

Existing packaged EXEs and the user's running app were left untouched. Manually
reopen the source app to load the rebuilt frontend. The older automation's model
inventory conflicts with the later approved six-model preview scope; this run
preserved the later work instead of removing it unattended.
