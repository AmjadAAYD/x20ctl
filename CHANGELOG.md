# Changelog

## 1.0.1 — 2026-08-16

Same application as 1.0.0. No code changes, no fixes, no new features.

### Added

- Standalone Windows executable. No Python install needed; download and run.

### Note on the missing 1.0.0 release page

1.0.0 was published without the executable, which made it a release only
useful to people who already had Python. Attaching the exe afterwards was not
possible: GitHub marks releases immutable, so assets cannot be added after
publishing. Deleting and republishing the release was not possible either —
GitHub permanently reserves the tag name of an immutable release.

So the 1.0.0 release page was removed and this one replaces it. The `v1.0.0`
tag still exists in git and still points at the same commit; only its release
page is gone. Anyone who cloned or downloaded source at 1.0.0 has exactly the
code described under 1.0.0 below.

## 1.0.0 — 2026-08-15

First stable release. Verified on two EasySMX X20 controllers.

### Added

- Controller roster: up to four controllers, one per player slot
- Per-controller save files, keyed by Bluetooth address
- Button remapping page
- Macro editor: piano-roll grid, all four slots, per-step timing, repeat
- Macro recording from live play
- Stick direction dial for macro steps, snapping to eight headings
- Saved macros page: whole-controller setups, open in editor or send to pad
- Trigger travel zones and response curves
- Idle shutdown timer, 1–30 minutes or never
- Sensor calibration
- Factory reset, behind a confirmation
- Battery level in the header
- Live connection state per controller, with a prompt when one goes quiet
- Vibration preview: the pad buzzes at the strength being set
- Update check against GitHub releases
- Simple and Advanced modes
- CLI: `sleep`, `remap`, `macro --read`, `calibrate`, `factory-reset`,
  `curve --gear`, `curve --preset`
- Console-free launcher (`x20ctl.bat`)

### Changed

- App opens on the controller roster instead of the macro editor
- Sidebar navigation replaces the tab strip
- Every page that edits the controller has a Save button
- Vibration saves itself; no Apply needed
- Discovery returns every controller found, not just the first
- Higher-contrast text throughout
- Window opens at 1280×820

### Fixed

- Macros capped at 47 steps, where the chunk index runs out, instead of failing
  inside the packet builder
- Macro recording captures the left stick, not only buttons
- Key lists stop at their declared count instead of overrunning a repeated
  record
- Recording nothing no longer crashes
- Select and Start refused as remap sources; the controller accepts and ignores
  them

### Protocol

Decoded and verified on hardware:

- Idle shutdown timer lives inside the motor record, as 5 ms ticks
- Trigger zones are deadzone pairs; response curves are Hermite control points
- Sensor calibration is `SET_MODE 03 DF AB 10`
- Factory reset is `RECOVER 03 DF A9 02`; the wrong generation byte is ignored
  silently
- Macros read back via `HOST_MACRO`, not `READ_MACRO`
  (found by [chriss80](https://github.com/chriss80/x20ctl))
- Select and Start are key codes 93 and 94, not 9 and 10

### Known limitations

- Output mode (XInput / DirectInput) changes only by button combination on the
  controller
- Sensor calibration sends correctly; its effect is unverified
- Lighting and turbo are not configurable over this protocol
- Trigger settings are chosen by name, not drawn

## 0.2.1

Command line tool and single-controller GUI. Development releases.
