# Motion acceptance continuation — 2 October 2026

The existing Prompt 2 presentation is retained locally. This run made no
production-source changes, protocol changes, releases or Git commits. Desktop
review was stopped after Computer Use reported the user's physical Escape key.
The final acceptance gate is therefore still open.

## Checkpoints and preservation

- Revalidated all **248 files** in the original Point 1 archive against its
  SHA-256 manifest. It remains at
  `C:\Users\amjad\.codex\automations\continue-x20ctl-prompt-2-motion-design-visual-polish-you-are-continuing-fro\point-1\snapshot.zip`.
- The original `restore-prompt-2.py` supports dry run and `--restore`, but its
  recorded experiment hashes predate later intentional edits. Its refusal to
  overwrite those edits is required. Reconcile the allowlist against subsequent
  work before using it; do not reset or extract the entire ZIP over this checkout.
- Archived and independently hash-verified **341 current source/build files**
  before acceptance work in
  `C:\Users\amjad\.codex\automations\continue-x20ctl-prompt-2-motion-design-visual-polish-you-are-continuing-fro\resume-20261002T103537Z`.
  The snapshot includes HEAD, index, status, staged/working binary diffs and
  a file manifest. It is a separate resume checkpoint, not a replacement for
  Point 1.
- Concurrent edits changed App, ModelWorkspace, ProWorkspace, two review tools,
  the desktop smoke fixture and compiled output during this run. They were
  preserved. HEAD and staging remained unchanged. The six-model UI already
  existed when this run began; this automation added or removed no models.

## Presentation decision

Inspected the actual Windows landing screen and compared it with the saved
Point 1 native capture. Keep the existing colored galaxy, restrained stars,
stationary four-player cards and clear dark surfaces. The atmosphere adds
depth while the layout, typography and readable controls remain recognizable.
Retain the existing shared motion timings, controller environmental breathing,
navigation light, slider feedback, direct opaque stick caps, localized motor
rings, picker entrances and reduced-motion/background-pause rules.

No additional effects were introduced or removed in this run. The prior
removal of nested fades, whole-controller motor movement and persistent
backdrop blur remains in the current styles. No Prompt 1 work was reverted.
The preliminary decision remains to keep the stronger effects; a completed
current native interaction review is still required before final acceptance.

## Verification and performance

- TypeScript: passed.
- Frontend tests: **4 passed**.
- Focused Python tests: **341 passed** across controller registry, desktop
  service, profiles, reports and protocol.
- Production Vite build: passed.
- `git diff --check`: passed.

These checks establish the tree at execution time. Concurrent later edits were
not rechecked after the stop; do not describe them as covered by this result.

The first actual source Windows/WebView2 motion run rendered landing, captured
about **121.3 seconds** of idle behavior, and confirmed all four ambient loops
paused while minimized. Process-tree CPU was **12.27% of one core** while idle
(approximately **0.38% of this 32-thread machine**) and **0.074% of one core**
while minimized. End-of-idle working set was **527 MiB**. This is an observed
sample with another app running, not a controlled comparison or GPU benchmark.

That run failed waiting for focus after restore: Windows restored the app behind
another foreground window. Pausing while unfocused is expected; the fixture
incorrectly assumed restore always grants focus. Evidence:
`artifacts/motion-resume-2026-10-02/motion-report.json` (`passed=false`).

Prepared an isolated ignored copy of the fixture with explicit focus handshakes
and active-Studio selector scopes; production sources were untouched. The focused
retry was interrupted by physical Escape during idle. Its owned exec process
was terminated with Ctrl+C and its Python process was confirmed absent. No
further Computer Use, native input, or acceptance run followed that stop.

## Remaining checks

Finish current-build native interaction acceptance: both models, assignment,
picker, all requested sections, sliders, exact cap geometry, compact layout,
normal focus-away/resume and reduced motion. Latest startup typography was not
visually reverified in this run. Existing earlier native reports remain historical
evidence, not current completion evidence. Physical hardware response, input
latency, GPU utilization and a new packaged EXE remain unverified.
