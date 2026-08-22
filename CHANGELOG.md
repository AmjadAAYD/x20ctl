# Changelog

## Unreleased

### Investigated

- The EasySMX X05: checked against a physical unit and found to have no
  KeyLinker/pulsenet protocol at all, on any transport. BLE scan, XInput, and a
  full HID feature-report sweep all came back empty; the manual confirms RGB,
  macros and vibration are on-pad-only. Documented in
  [docs/00-findings.md](docs/00-findings.md#7-other-controllers-checked) rather
  than left unexplained.

### Changed

- The update check now runs once at launch instead of only on demand, on a
  bottom-left line: a spinner and "Searching for a new update...", then
  "Currently at the latest version" and the version number.
- It stays silent when there is nothing to report. A newer release pops a
  dialog offering Cancel or Update; Update opens the release page. Nobody is
  made to update.
- A GitHub that cannot be reached says so on that line and never opens a
  dialog.
- The check runs on a worker thread. The on-demand one called urllib with an
  eight-second timeout on the UI thread, which would have frozen the window at
  launch on a slow network.

## 1.1.2 — 2026-08-16

A read-everything bug hunt over the whole codebase.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Fixed

- A controller that answered the stick query but not the trigger query crashed
  the load with `AttributeError`, leaving the workspace half filled. Reachable
  from a pad with no triggers and from a single read timing out, which the first
  query on a fresh link is known to do, so it was intermittent.
- The tray kept showing a battery reading after you left a controller, so with
  the window closed it could sit there stale or belong to a pad since switched
  off. It now clears.
- Macro slots were asked for by count rather than by bit position, so a pad with
  a gap in its macro bits would have been asked for the wrong slots.
- `transport.py` raised a `SyntaxWarning` on import from an unescaped device
  path in its docstring. That becomes an error in a future Python.
- `first_hide_to_tray` was created on first use rather than initialised.

### Verified, not changed

- Reading macros from inside the remapping callback is safe: the link releases
  before it calls back, so the chained read is not refused.
- `VibrationPage.load` takes a percentage and `client.vibration()` already
  converts from the raw 0-255 the pad stores. No unit mismatch.
- `transport.py` already documented the 2.4 GHz receiver as transparent, which
  independently confirms this release's dongle correction.

## 1.1.1 — 2026-08-16

Download **x20ctl.exe** and run it. No install, no Python needed.

### Fixed

- Closing the window quit the app and took the tray icon with it. It now hides
  and keeps running; the tray icon stays, clicking it reopens the window, and
  Quit is on its right-click menu.
- The first time the window hides, the tray says where it went.

## 1.1.0 — 2026-08-16

Acts on the first round of user feedback against 1.0.1.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Added

- Taskbar tray icon showing battery, with Show and Quit
- Swap left and right sticks, on the Sticks page
- Swap L2 and R2, on the Triggers page
- Home button in the Test page, via XInputGetStateEx
- Macros now load from the controller on connect, all four at once

### Changed

- Power moved out of Advanced: a sleep timer needs no warning
- Power page says it is reading, instead of showing a value it has not read
- Battery readings carry the raw status byte, and `report.py` prints it

### Fixed

- Idle shutdown showed 10 minutes regardless of what the controller held
- Chunked `HOST_MENU` records silently truncated: continuations were sent as a
  bare index byte, which that opcode answers with silence
- `tools/ble_enum.py` read an OTA characteristic while claiming to skip OTA

### Protocol

- `HOST_MENU` kind 6 is macro step data; this pad reports 42 steps per slot
- Kind 7 is the device can-change list, and this pad does not answer it
- The full `SET_MODE` page table, `05 DF AB 00 <a> <b>`, including which pair
  enters firmware update
- `SET_MODE 03 DF AB 0A`–`0F` are a 5 ms uptime counter, not six features
- The USB bootloader's command set: opcode `0xF1` reads the same GUID as
  `HOST_GUID` does over BLE, and there is no bulk read at all
- Lighting writes are chunkable and do land; the LEDs ignore them regardless

### Known limitations

- Colour, brightness and RGB mode are on-pad only (`C` + `R3`, `C` + `L3`).
  `caps.lighting` is zero and KeyLinker cannot change them either.
- Battery has four steps because the controller reports four; the intermediate
  decode is unconfirmed against a discharging pad.
- Rear buttons cannot appear to Steam Input as their own buttons: the firmware
  replays them as existing buttons.
- The 2.4 GHz receiver carries no configuration channel.

## 1.0.1 — 2026-08-16

First stable release. Verified on two EasySMX X20 controllers.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Added

- Standalone Windows executable
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
- The executable is unsigned, so Windows warns that the publisher is unknown

## 0.2.1

Command line tool and single-controller GUI. Development releases.
