# Multi-controller UI validation — 1 October 2026

Unreleased source changes, continued from the intentional 30 September working tree. No reset, commit, tag, push, publication or deployment was performed.

The checks below record the earlier two-model implementation. The user later
approved four additional UI-only previews; current scope and separate offscreen
verification are recorded in [preview-models-validation.md](preview-models-validation.md).

## Completed behavior

- Every launch opens Player 1–4. Empty cards open the existing Switch Controller picker, which contains only X20 and X20 Pro.
- Selection assigns the clicked player and returns to the cards. Assigned cards enter their own Studio. Connect on the X20 picker row opens that player's Studio and the existing BLE scanner; it does not fabricate a connection.
- Studio switching changes that player's model. Players returns to the landing screen. Each player/model keeps its own draft during this app session.
- One native configuration connection remains active at a time. Changing players, even between two X20 assignments, disconnects the old link and clears its discovery candidates. Returning to the landing page disconnects; hidden studios stop input polling.
- X20 retains the existing configuration, recorder, profiles and protocol. Its saved setup list refreshes when entering a Studio without replacing its current draft.
- Pro mappings, response curves, six macro slots and vibration visualization are editable local drafts. Hardware connection, apply, lighting, display upload, calibration, reset and persistent Pro profile actions remain unavailable.

## Visual changes

- Shared navy framing retains the supplied blue-left/amber-right artwork. Source images and physical controller details were preserved.
- An inset image plane scales the photo and all overlays together. Contain sizing and explicit stage width prevent cropped grips and aspect distortion, including at the compact desktop size.
- Opaque photographic stick caps sit above the real artwork. Each model uses its registry geometry, with local pointer/WASD motion relative to its physical stick centers.
- Grip and Pro trigger effects use localized zones rather than shaking the whole controller. The Pro strength is a common visual intensity, not a claim of independent hardware motor control.
- One shared slider renders the fill, thumb and focus states for both Studios. Preset/keyboard changes transition; pointer dragging disables position transitions for direct response.
- Player cards, picker rows and header previews share the same art renderer. Headers wrap at smaller sizes, and the Pro vibration art and controls occupy the same row.

## Executed checks

| Check | Result |
| --- | --- |
| `npm run lint` (TypeScript) | Passed after final source changes |
| `npm test` | 4 passed; curve/vector helpers unchanged afterward |
| `.venv/Scripts/python.exe -m pytest tests/test_controller_registry.py tests/test_desktop.py tests/test_profiles.py tests/test_reports.py tests/test_protocol.py -q` | 185 passed |
| `npm run build` | Passed after final source changes; output in `dist-ui` |
| `git diff --check` | Passed |
| Source Windows desktop acceptance | 34 checks passed; 28 native screenshots |

Native command:

```powershell
.venv/Scripts/python.exe app.py --smoke-test artifacts/acceptance-multicontroller-2026-10-01-verified
```

The acceptance run uses the actual Windows pywebview/WebView2 host and native bridge with temporary isolated profile storage. It drives the real DOM and captures the GPU-composited desktop surface; it is not browser-preview acceptance and does not claim trusted OS pointer/keyboard injection or measured drag latency.

It verified all four assignments and Studio entries, Player 2 switching independently of Player 1, separate X20/Pro drafts, fresh reload startup, both models' overlay alignment and full art containment, opaque caps, local WASD previews, shared sliders, zero-strength motor removal, six Pro macro slots, Pro native-operation rejection, the existing X20 pages/profile workflow, dialog focus, stopped hidden polling, navigation protection and tray close/restore. Normal and compact native window sizes were 1400×940 and 1060×760.

[Native acceptance report](../artifacts/acceptance-multicontroller-2026-10-01-verified/smoke-report.json)

Selected native captures:

- [All four players assigned](../artifacts/acceptance-multicontroller-2026-10-01-verified/four-players-assigned.png)
- [Player 2 model picker](../artifacts/acceptance-multicontroller-2026-10-01-verified/picker-player-two.png)
- [Compact X20 controls](../artifacts/acceptance-multicontroller-2026-10-01-verified/buttons-compact.png)
- [X20 Pro controls](../artifacts/acceptance-multicontroller-2026-10-01-verified/pro-buttons.png)
- [X20 Pro vibration](../artifacts/acceptance-multicontroller-2026-10-01-verified/pro-vibration.png)

## Remaining boundaries

No BLE configuration link or XInput device was detected in this run. Live-controller reads/writes, physical macro playback and motor response were not tested. X20 Pro USB/HID/BLE commands, motor independence, lighting/display control and persistent setup application require actual hardware and protocol evidence. No speculative commands were added.

Player model assignments and Pro drafts are session-local. They do not identify or bind physical XInput slots. Simultaneous independent hardware configuration connections are outside this UI completion.

The frontend was rebuilt and tested in the Windows desktop host from source. A new packaged EXE/ZIP was not built; existing release binaries were left alone.

## Oct 1 thumbstick correction

The earlier visual review missed that translated photographic caps exposed the
stationary caps underneath. Its opacity/alignment checks did not establish that
each stick appeared only once. Both models now have edited cap-free background
plates, with the moving caps sampled from the original artwork using separate
crop bounds for each side. Static previews retain the complete original photo.
Added input-overlay glow and drop shadows were removed, including pressed-state
styling inherited from the motion stylesheet.

The focused native correction review passed for both models and saved ten
WebView2 screenshots covering neutral, left/right travel of both sticks,
selection and compact layout. It also checks intact card/header artwork and no
computed glow/filter on the cap or input controls. Local pointer events exercise
the actual React preview handlers; no hardware connection/input is fabricated.
The harness uses a 0.02 normalized tolerance for integer pointer-coordinate
rounding, and the real pointer-cancel handler to return the caps to neutral.

[Thumbstick correction report](../artifacts/thumbstick-review-2026-10-01-verified/thumbstick-report.json)

Type checking and the production build passed. The build used Vite's native
configuration loader inside the sandbox. No packaged release was rebuilt.

### Thumbstick centering follow-up

The cap crops were also incorrectly used as display offsets, leaving neutral
caps above the fixed rings. Crop sampling and display placement are now separate:
caps stay centered in their controls, and both control positions/drag origins
use ring coordinates measured from each model's background plate. No artwork or
cap dimensions changed in this follow-up.

The focused native review passed at 1400×940 and 1060×760 for both models and
after returning from full travel. Cap centers and interaction origins matched
the measured coordinates to within 0.021 CSS pixels. Ten screenshots were saved
with the numeric checks in
[the centering report](../artifacts/thumbstick-centering-2026-10-01-verified/thumbstick-report.json).
The standard production build, TypeScript check, Python syntax checks and
`git diff --check` passed.

An existing isolated design preview held the shared smoke mutex. Review windows
now use a process-specific smoke mutex so independent native reviews can coexist;
the normal desktop application retains its single-instance mutex.
