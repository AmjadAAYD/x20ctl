# Prompt 2 — motion and visual polish

Local, unreleased work continued from the intentional Prompt 1 working tree on
1 October 2026. This retains the navy/charcoal palette, Segoe typography, layout,
navigation, player assignments, real controller artwork and blue-left/amber-right
lighting. No protocol, firmware, BLE, USB, HID, bootloader or profile-storage
implementation was changed. X20 Pro operations remain disabled.

## Point 1 checkpoint

Before editing, a byte-exact ZIP of all tracked and non-ignored untracked files
and the compiled `dist-ui` was created outside the repository. All 248 archived
files were verified against a SHA-256 manifest. The checkpoint also records HEAD,
the original index, dirty status, tracked binary diff and staged binary diff.

Checkpoint directory:
`C:\Users\amjad\.codex\automations\continue-x20ctl-prompt-2-motion-design-visual-polish-you-are-continuing-fro\point-1`.

Stop x20ctl, then restore only the Prompt 2 allowlist with:

```powershell
python 'C:\Users\amjad\.codex\automations\continue-x20ctl-prompt-2-motion-design-visual-polish-you-are-continuing-fro\point-1\restore-prompt-2.py' --restore
```

Omit `--restore` for a verified dry run. The script refuses newer edits, changed
HEAD or changed staged work. It restores captured source and compiled assets;
it removes only new allowlisted experiment files. It never resets Git, changes
branches or stages files. Prompt 1 files outside that allowlist remain byte-exact.
Ignored acceptance evidence is retained. No commit, push, tag or release occurred.

## Motion system and design decisions

`src/motion.css` supplies 110 ms micro-interactions, 180 ms section transitions,
240 ms panel transitions, 320 ms hero feedback, 64 s ambient drift and shared
easing curves. Input position never uses those timing constants.

Kept:

- Three-node ambient layer: dim irregular SVG stars, slow blue/violet clouds and
  faint amber warmth. Transform/opacity animate cached layers; no decorative
  React render loop, particle timer, canvas engine or new dependency is used.
- Blue-left and amber-right breathing environment around the unedited artwork.
  Header thumbnails remain calm. Image, opaque caps and model-specific control
  geometry retain their original positions and proportions.
- Moving navigation light behind stationary buttons; quick page/Studio fades.
  Editing a value keeps the page node, so input updates do not restart entrances.
- Short picker/backdrop entrance with a small row stagger and restrained artwork
  hover. Closing remains immediate and existing focus/Escape behavior is kept.
- Assigned-card lighting, quieter selected borders, hover/focus edge light and
  brief player-number confirmation. All four slots retain their original actions.
- One-shot primary-button sweep on hover/focus, inset press feedback, keyboard
  focus rings and slider halos. Dragging captures the real pointer and disables
  fill/thumb position transitions until release, even outside the input.
- Immediate pressed-input core/halo with short release decay, direct analog
  trigger depth and opaque, unsmoothed actual stick positions.
- Two local energy rings per motor zone. Draft intensity controls wave speed,
  spread and opacity; zero removes the zones. The photograph never wobbles.
  Pro still labels its four zones as a cosmetic local preview.
- Macro selection pulse, actual recording/pending-draft breathing and brief
  native-result feedback. Connection confirmation requires an actual connected
  state; no charging, success or hardware reception state was invented.
- Event-driven ambient pause on blur/hidden/minimized, with automatic resume.
  Reduced motion disables decorative loops, entry animations and hover scaling.

Removed or simplified after native review:

- The Point 1 angular background artwork and per-frame background-position/filter
  drift are disabled by the new CSS layer, while their source stays in the
  checkpoint. The atmosphere is quieter and no longer looks like a wallpaper.
- The first pass faded a page and its child panel simultaneously. The nested
  fade was removed because it briefly made text too dim during navigation.
- A first-pass pause rule also paused one-shot feedback. It was narrowed to
  perpetual decorations so an unfocused window cannot remain halfway faded.
- The old whole-zone motor scale/filter pulse was replaced by localized rings.
- Live backdrop blur was removed from nearly opaque persistent surfaces after
  CPU measurement showed avoidable rendering work. Their gradients, borders,
  colors and shadows are retained; transient modal backdrop blur remains.
- Moving buttons, bouncing controls, constant CTA shimmer, screen sheen and a
  particle swarm were omitted. Model switching uses a short new-Studio fade;
  old/new images are not held on screen to delay selection.

No Point 1 functionality was reverted. The decision is to keep the restrained
effects and remove the weaker/expensive ones (choice B).

## Validation

Point 1 was independently verified before editing: TypeScript, four frontend
tests and 34 real Windows WebView2 acceptance checks with 28 native captures.

The motion pass also passed TypeScript, all four frontend tests, 185 focused
Python tests (registry, desktop service, profiles, reports and protocol), and
the production Vite build. The first native run stopped at an old assertion
requiring the exact `motor-pulse` name. The assertion was updated to verify a
decorative localized animation and a stationary photograph. The following
34-check native acceptance run passed.

The separate `tools/motion_review.py` harness uses the real source Windows
pywebview/WebView2 app, isolated temporary native storage, its real DOM and
GPU-composited captures. It verifies all X20/X20 Pro sections, indicator alignment,
both models' exact opaque local stick previews, no artificial slider transition
while dragging, stable page identity during edits, 1400x940/1060x760 windows,
native minimize/restore, and actual WebView2 reduced-motion emulation. It does not
pretend that DOM-dispatched events are trusted OS input or live controller input.

Computer Use also supplied trusted Windows pointer drags: the X20 vibration
slider visibly changed 30% → 79% → 0%, including release beyond the slider.
This confirms pointer interaction; it is not a measured end-to-end input latency.

## Results at interruption

Computer Use reported that the user pressed physical Escape. UI interaction
stopped immediately, and the isolated final optimization review and its owned
WebView2 processes were closed. No further UI automation was attempted.

| Check | Result |
| --- | --- |
| Final TypeScript check | Passed |
| Frontend tests | 4 passed |
| Focused Python tests | 185 passed |
| Production Vite build, including the final blur optimization | Passed |
| `git diff --check` | Passed |
| Native acceptance after motion/fade changes | 34 checks passed, 28 captures |
| Extended native motion review | 7 checks passed, both models, reduced motion, compact layout, minimize/restore |
| Native review after the final blur optimization | Interrupted by user Escape; final performance and visual comparison remain pending |

Observed process-tree CPU use (Python + its WebView2 processes, expressed as
percent of **one core**, not whole-machine CPU):

| State | Point 1 | Motion pass before final blur optimization |
| --- | --- | --- |
| Landing, approximately 122 seconds idle | 58.9% | 32.2% |
| Minimized, approximately 21 seconds | 0.37% | 0.22% |
| Process working set at end of idle | 535 MiB | 503 MiB |

These are observed runs on this machine, not a controlled benchmark or a GPU
measurement. No performance improvement is claimed for the final removal of
persistent backdrop blur because that follow-up measurement was interrupted.
There is no decorative React state loop and no change to controller polling.

Evidence in the ignored `artifacts` directory:

- `point-1-motion-baseline/smoke-report.json`: independently verified Point 1.
- `point-1-motion-idle/motion-report.json`: Point 1 idle/minimize samples.
- `motion-native-review/motion-report.json`: complete extended motion review,
  before the final blur optimization, plus native screenshots of every section.
- `motion-final-acceptance/smoke-report.json`: passing 34-check acceptance after
  the nested fade was removed, before the final blur optimization.
- `motion-optimized-review/`: partial native captures from the interrupted run.

The motion pass is kept locally. The only remaining software acceptance step is
to finish the native appearance/performance comparison of the final blur removal
and normal focus-away/resume behavior. If that final surface change looks worse,
remove just that CSS override and retain the already validated motion pass.

## Remaining boundaries

No controller was physically exercised. Live XInput actuation, BLE configuration
writes, physical macro playback, battery transitions and hardware motor response
remain unverified. Pro protocol/display/lighting/motor commands were not invented.
GPU utilization and hardware input latency were not measured. Acceptance uses
the source desktop host; a fresh packaged EXE was not built and existing release
binaries were left alone. Player/model bindings and Pro drafts retain Prompt 1's
session-local limits.

## Visible galaxy revision (2026-10-01T07:54:47.1177434Z)
User feedback superseded the initial very faint atmosphere. Kept visibly colored blue/violet/amber clouds, drifting nebula texture, brighter twinkling stars and stronger controller rim breathing. Four CSS decorative layers pause on blur/minimize, with reduced motion retaining static color.
TypeScript and production build passed. Native revised captures show the galaxy, with four running loops in focused state and all paused minimized. Idle CPU over 31.3 seconds was 57.9% one-core equivalent (approximately 1.81% of 32 logical CPUs); minimized approximately 0.22% one-core. No performance improvement is claimed. Navigation fixture was invalidated by user entering Studio during idle; the complete review did not pass.
Latest native preview was launched. Physical Escape stopped Computer Use before its fresh capture. Concurrent controller artwork and geometry work was preserved. The original Point 1 archive remains intact; the stale restore manifest safely rejects later edits and needs reconciliation before restoration.

## Player-card image fill
Changed only the hub artwork layout: the image plane now fills its rectangle at 100% instead of 84%, with no extra glow inset and no smaller inner corner radius. The unchanged 3:2 aspect ratio retains the complete controller. Studio overlay geometry is unaffected. Vite build passed; actual Windows X20 Player 1 card visually verified. User interaction interrupted Pro review and moved into Studio; their current page was left in place. Before-edit stylesheet is preserved as point-1/before-player-card-fill.css.

## Picker and header image fill
Extended the shared edge-to-edge layout to both picker rows and X20/X20 Pro Studio headers. Header padding is removed and its frame follows 3:2 proportions at full and compact widths. Vite build and scoped diff check passed. Focused actual Windows WebView2 review passed, with exact image/frame alignment for both picker models and both headers plus the compact Pro header. Five native captures are in artifacts/image-fill-preview; report.json has passed=true. The isolated preview is left open at Switch Controller. Interactive Studio geometry and hardware behavior are unchanged.

## Page artwork removal
Removed the large body controller artwork from Pro Input tester, Power & device, Display and Lighting. Input Tester now opens directly on its signal, stick, button and trigger panels; other pages retain their existing details/actions. TypeScript and Vite build passed. Focused actual Windows review passed on six relevant pages across X20/Pro, with zero body controller canvases, retained header, no horizontal overflow and expected remaining controls. Pro Buttons/Vibration canvases remain. Four Pro screenshots inspected; evidence is artifacts/page-art-preview/report.json (passed=true). Isolated preview remains open at Pro Input tester. No hardware commands were exercised.

## Pro Connect visibility
Made the Pro picker Connect button fully legible with opacity 1, bright text and a stronger border. Hardware activation remains disabled with its existing unverified-protocol explanation. Vite build/scoped diff check passed; native capture confirmed the button is visible at full opacity and remains disabled. Evidence: artifacts/connect-button-preview/report.json (passed=true) and picker.png. Current isolated preview is left open at the picker.

## Single Switch Controller action
Removed the duplicate sidebar action from both X20 and Pro; header action remains. Updated existing desktop review selectors to use the header. TypeScript, Vite build and scoped diff check passed. Focused actual Windows review confirmed exactly one action per Studio, no sidebar copy, correct first-tab indicator alignment, and both header buttons successfully opening the picker. Evidence: artifacts/single-switch-preview/report.json (passed=true). Isolated preview is open at Pro Buttons. Full desktop acceptance suite was not rerun for this limited change.

## Controller zone navigation
Switch Controller now routes directly to the Controller zone, with separate Players buttons removed. Existing card picker, selected-player assignment and draft retention remain. TypeScript/Vite passed; actual Windows focused review passed (artifacts/switch-zone-preview/report.json). Both X20 and Pro returned directly without an intermediate dialog and retained their drafts across model switches.

