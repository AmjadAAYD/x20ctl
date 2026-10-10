# X20CTL startup intro

The existing app initializes under a temporary fullscreen React layer. Its
Controller zone destination and hardware services are unchanged. The layer
unmounts after 7 seconds, supports Escape / Skip intro, and does not appear
when reduced motion is requested. Navigation never replays it.

The initial 3.7-second pass was too short for the user, with a black logo tile
and off-center solo X. Revised timeline: dark stars (0–0.9s), blue/violet energy
gathering (0.9–1.8s), first diagonal (1.8–2.25s), second diagonal and localized
impact (2.35–2.8s), official X stabilization (2.8–3.5s), 20CTL reveal with smooth
centering of the complete wordmark (3.5–4.6s), a readable hold (4.6–5.8s), then
fade directly into the already mounted app (5.8–7s). The solo X starts centered;
the completed wordmark is centered using the measured text width. The nebula
retains its color throughout the handoff.

The original logo source is only 51 by 54 pixels. A local SVG traces its three
segments and central notch; the original PNG remains unchanged elsewhere in the
app. The intro uses these vector paths for both the forming segments and the
finished X, displayed 20% larger. Shape-clipped energy edges, blue/violet glow,
12 brief sparks and one finishing sheen replace the plain lines and raster fade.
Existing stars.svg and nebula.svg supply the atmosphere. No audio, video player,
external runtime asset, dependency or backend command was added. There are 16
gathering particles and no decorative JavaScript frame loop. Background UI is
inert only during the intro; cleanup restores keyboard/pointer access.

Before-intro source snapshot:
C:/Users/amjad/.codex/automations/continue-x20ctl-prompt-2-motion-design-visual-polish-you-are-continuing-fro/startup-intro-checkpoint/before-intro.zip.
Restore only the recorded intro paths from this checkpoint after verifying
their after-edit hashes, leaving the earlier Point 1 and later controller work
alone; regenerate dist-ui with the production build. Validation and final design
review are recorded below when complete.

## Validation

Kept the seven-second revision and vector formation. Removed the initial short
pacing, black logo tile, low-resolution raster and generic thin slash shapes,
and corrected both the solo X and full wordmark centering. Existing
Controller zone navigation, player assignments, hardware guards and galaxy
remain. No Prompt 1 or unrelated controller work was reverted.

TypeScript and Vite production build pass. Four existing frontend tests pass;
the edited desktop-review Python modules compile. Focused actual Windows
WebView2 review passes seven checks in artifacts/startup-intro-review/report.json:
natural playback measured 7.044s, exact centering within 0.16 CSS pixels, compact
layout, Escape, Skip intro, reduced motion on launch and during playback,
minimize/restore, and navigation without replay. Native phase captures visually
confirm no black icon tile, retained nebula color and direct UI reveal. The
existing acceptance/review tools now wait for the intro before interacting.

The effect uses 16 particles, CSS animation and one cleanup state change.
All 43 finite intro animations and their DOM are removed after playback; minimize
also clears the intro. It adds no perpetual work after launch. GPU utilization
and a new packaged EXE were not tested; the normal controller polling/protocol
code was not changed. The full desktop smoke suite was not rerun for this change.

## Skip handoff — 2026-10-02

Skip intro and Escape now freeze the current decorative frame and fade the
whole intro into the mounted player screen over 450 milliseconds. The fade
starts from the current opacity, so skipping during the natural exit does not
flash back to an opaque intro. Repeated requests cannot restart the transition.
The underlying UI stays inert until the fade completes. A 600-millisecond
fallback releases it if animation events are suspended; reduced motion and a
hidden document still dismiss immediately. Natural playback keeps its timeline.

Type checking and the production build passed. Nine focused headless production
checks passed: Skip, Escape, late skip, missing animation event, restored player
navigation without replay, natural playback, reduced motion on launch/during
exit, and hiding the document during exit. Timing is measured inside the browser
to exclude automation transport latency. Evidence is in
`artifacts/startup-skip-review/report.json`. This review uses a mock disconnected
bridge and does not access the user's screen or controller, or claim an
interactive Windows desktop inspection.

An editable seven-second motion study is in Figma:
https://www.figma.com/design/NgfK44DAxssFV1MAj2YW6T?node-id=3-2.
It uses the same traced logo and a native Controller zone capture. Its keyframes
were authored and resting composition inspected; no Figma video was exported.
Canva was searched for existing X20CTL material; no matching assets were used.

To undo only this intro, run startup-intro-checkpoint/restore-startup-intro.py
with no flags for a guarded dry run. Add --restore to restore the recorded ten
source paths, then run npm run build. It rejects newer edits, changed HEAD or
new staging on those paths. Original Point 1 remains separately preserved.

## Wordmark typography revision

The intro now uses Oxanium SemiBold with slightly open tracking and optical
vertical alignment against the X. A 2,168-byte local font subset contains only
the five wordmark characters. Its SIL Open Font License is included in
public/licenses/Oxanium-OFL.txt and copied into dist-ui by the build. Source:
https://raw.githubusercontent.com/google/fonts/main/ofl/oxanium/OFL.txt.

The font is emitted as a local file instead of an inline data URL so it obeys
the existing desktop font security policy. The title is measured again when
the font loads, before its reveal, to preserve complete-wordmark centering.
Other interface typography and the seven-second choreography are unchanged.

TypeScript and production build pass. Focused Windows WebView2 typography review
in artifacts/startup-font-preview/report.json verifies that the bundled font
loads, the wordmark remains centered at full and compact widths, no horizontal
overflow occurs, and Skip restores the Controller zone. Native captures were
visually inspected. The earlier seven-check intro review predates this font
revision; it was not rerun in full. The Figma study retains its previous font.
The accepted pre-typography source files are preserved in
startup-intro-checkpoint/before-typography.zip.
