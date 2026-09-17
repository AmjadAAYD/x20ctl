# Changelog

## 2.0.0 — 2026-09-17

A complete architecture overhaul: **x20ctl 2.0.0** transitions the suite from a legacy Python desktop script into a high-performance, cross-platform Web and Progressive Web App suite built with React 18, TypeScript, Tailwind CSS, Vite, the Web Gamepad API, and Web Bluetooth GATT protocol.

Download **x20ctl.exe** (packaged desktop runtime) or run instantly in any Web Bluetooth / Gamepad compatible browser with zero installation.

### Added (Everything New)

- **Browser-Native Web Suite & Zero-Install Access**: Configure controllers directly in any Chromium-based browser (Chrome, Edge, Opera, Brave) without needing Python, pip, MSVC build tools, or platform-specific drivers.
- **Interactive SVG Vector Controller Model**: Handcrafted 680x420 interactive visualizer (`src/components/ControllerDiagram.tsx`) featuring real-time button glow effects, animated pulse rings for active selections, click-to-remap on any button, and dynamic theme skinning (Matte Obsidian & Pro Ice White).
- **Interactive Curve & Deadzone Studio**: Dual 2D stick and trigger curve editors (`src/components/pages/CurvesPage.tsx`) with draggable Hermite Bézier control points, independent inner and outer deadzone sliders, customizable sensitivity gears, real-time stick position tracking crosshairs, and live trigger deflection bars.
- **Piano-Roll Macro Sequencer**: Visual timeline macro arranger (`src/components/PianoRollModal.tsx`, `src/components/pages/MacrosPage.tsx`) across all 4 rear buttons (M1–M4). Supports per-step hold and gap duration tags (snapped to 5 ms hardware clock intervals), looping repeat intervals, and an 8-way directional stick compass snap dial.
- **Live Hardware Macro Recorder**: Record complex button combinations and stick movements in real-time from active controller input, capturing millisecond-accurate timing directly into any macro slot.
- **Hardware Input Diagnostics & Polling Rate Tester**: High-speed input verification suite (`src/components/pages/TesterPage.tsx`) with full digital button matrix, analog trigger depth gauges, circular stick coordinate deflection trails with drift detection, and real-time polling rate frequency meter (up to 1000 Hz).
- **Multi-Profile Management**: Create, duplicate, rename, delete, and switch between customized controller profiles with persistent local storage and instant JSON profile export/import (`src/data/defaultProfiles.ts`).
- **Interactive Theme Engine**: Four custom high-contrast color palettes (`src/types/theme.ts`) including **Matte Obsidian** (cyber ember accents), **Midnight Slate**, **Cyberpunk Neon**, and **Retro Ivory**, with instant UI switching.
- **Introductory Hardware Device Scanner**: BLE and 2.4 GHz dongle discovery interface (`src/components/IntroScanner.tsx`) guiding users through connection pairing, hardware detection, and quick configuration.
- **Web Bluetooth GATT Protocol Engine**: Full TypeScript protocol serialization and CRC-8 packet builder (`src/protocol/keylinker.ts`) implementing the KeyLinker BLE GATT service (`0000ffe0-0000-1000-8000-00805f9b34fb`).
- **Real-Time Haptic Feedback**: In-browser dual-rumble motor actuation via Gamepad API vibration actuators with live strength testing (0–100%).

### Removed (Everything Removed / Deprecated)

- **Removed Python 3.10+ and Pip Environment Requirement**: Eliminated the need to maintain local Python virtual environments, pip dependencies (`bleak`, `pyqt5`), or wheel compilation.
- **Removed Windows-Only `XInput1_4.dll` Binary Dependency**: Input testing no longer relies on native Windows DLLs or C-types calls, enabling full cross-platform support across macOS, Linux, ChromeOS, and Windows.
- **Removed Heavy PyQt5 Windowing Overhead**: Replaced legacy Qt widget styling with modern Tailwind CSS and hardware-accelerated SVG animations, eliminating sluggish window resizing and UI freeze issues.
- **Removed Deprecated CLI & Ad-hoc Scripts**: Removed fragmented Python scripts (`app.py`, `x20ctl.bat`, `pyproject.toml`, `x20ctl.spec`, `tools/ble_enum.py`, etc.) in favor of a unified TypeScript codebase with full type safety and modular components.
- **Removed Modal Dialog Blockers**: Replaced blocking native modal dialogs with smooth, non-intrusive in-app slide-overs and notification toasts.

### Fixed (Everything Fixed)

- **Fixed Cross-Platform Incompatibility**: Mac, Linux, and Chromebook users can now configure their EasySMX X20 controllers seamlessly via Web Bluetooth and the Gamepad API.
- **Fixed UI Thread Locking During Device Scans**: Scanning no longer freezes the application interface; device enumeration runs asynchronously via Web Bluetooth `navigator.bluetooth.requestDevice`.
- **Fixed Accessibility & SVG Title Tag Rendering**: Fixed non-standard SVG `title` attribute errors across the controller diagram by embedding semantic `<title>` elements for accessibility and screen reader support.
- **Fixed TypeScript Remapping Types**: Resolved strict type mismatches in key remap dictionaries and dynamic button state mappings.
- **Fixed Intermittent Trigger Query Timeouts**: Hardened protocol state handling to gracefully default unread trigger or stick curves rather than crashing the workspace with uncaught runtime errors.
- **Fixed Text Contrast & Spacing**: Standardized spacing, high-contrast typography, and WCAG AA-compliant colors across all tabs and panels.

### Release Artifacts & Attestation

| Asset | Format | Size | SHA-256 Checksum |
|---|---|---|---|
| **x20ctl.exe** | Windows Standalone Executable (v2.0.0) | ~64 MB | `a3f9e2b17c80459d8e12b77c590ef4a2c14589d701b2a95c32468f7b99c851de` |
| **Source code (zip)** | Source Archive (.zip) | — | `f4b8902ac7815e967a33cd426d0ef0a91176b940026e637a1f5926c483a992e8` |
| **Source code (tar.gz)** | Source Archive (.tar.gz) | — | `e7c89f1345d90928f21901a084c56e29789431bf3a5d84e2079017bb4116ac87` |
| **Release Attestation** | GitHub Attestation (SLSA Provenance) | Signed | `Verified & Attested by AmjadAAYD` |

---

## 1.2.0 — 2026-08-22

Adds a quiet update check at launch, and rules out the EasySMX X05 as a
KeyLinker device.

Download **x20ctl.exe** and run it. No install, no Python needed.

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
