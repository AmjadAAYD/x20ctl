# Changelog

## Unreleased

Small changes since 0.2.0, plus the repo going public for testing.

- **An intro animation.** The logo and name fly in, hold, then leave to the left
  before the interface shows. Click, press a key, or set `X20CTL_NO_SPLASH=1` to
  skip it.
- **A security policy**, and three ways to check the executable: the published
  SHA-256, scanning it yourself, or skipping the binary and running from source.
  One-file builds set off antivirus heuristics on their own, so there needed to
  be a way to verify rather than just my word.
- **Says it's Windows only**, and why. The input tester uses XInput directly,
  save files go to `%APPDATA%`, and the taskbar icon needs a Windows call.
- **Tests run on push**, 3.10 and 3.12, on Windows runners.
- **`tools/macro_from_video.py`**, which turns a stick movement in a
  gamepad-overlay video into a macro.
- **Wrote down what the macro format can't do**, measured against a real game
  rather than guessed: eight stick headings, no magnitude, so anything needing a
  specific stick angle won't work. Includes the one partial workaround and where
  it stops working. See [docs/01-protocol.md](docs/01-protocol.md).

## 0.2.0

Deadzones and response curves, which were decoded but unreachable in 0.1.0, and
a controller header that tells the truth while you watch it.

### Added

- **Stick and trigger deadzones and response curves.** Each of the four channels
  has an inner deadzone, an outer deadzone and two response-curve control
  points. They get a page of their own in the app, where the points are dragged
  directly, and an `x20 curve` command for exact values.

  **Writing is confirmed on hardware for both records.** A stick deadzone was
  written from 8 to 10 and a trigger deadzone from 4 to 6, each read back
  changed with the other channel untouched, and each restored byte for byte.
  This is worth stating precisely because the earlier changekey proof wrote a
  value that was already there, so it could show the framing was right but never
  that a write changes anything. `tools/verify_curve_write.py` is that
  procedure, kept so it can be repeated on another pad.

  Every write re-reads the record and reports whether the controller took it,
  saying the pad kept its own values rather than claiming success. The app holds
  the values read at connection so anything can be put back, and the command
  line prints a `--restore` line before each write.

  The line drawn between the two control points is this app's own smooth
  interpolation. The points are read and written exactly, but nothing documents
  how the firmware joins them, and both the app and the docs say so rather than
  implying the curve is the hardware's.

- **A version number**, from one place in the package: `x20 --version`,
  `x20 status` beside the controller's own firmware version, the app header and
  sidebar, and the executable's file properties. Packaging reads it back out of
  the same module.

### Changed

- **Edits save themselves.** Typing, recording, clearing and vibration changes
  are written to disk on a short debounce. The Save button stayed, showing
  whether anything is pending, since a control that reports state is worth more
  than one that's merely the only way to act.

- **The one interruption left is destructive.** Clearing a slot that held a
  macro asks first and offers to put it back, because a macro already written to
  the controller can't be read off it again. Everything else is silent.

- **Battery is re-read every 20 seconds** rather than once at connection, so the
  gauge and the charging flag reflect the pad rather than whatever was true when
  the app started. A poll that comes back empty leaves the last reading alone,
  since one unanswered reply isn't news. A poll that fails means the link is
  gone, and the window now says so instead of showing a controller that walked
  away.

- **The controller is named**, mapped by vendor and product id and falling back
  to the reported name, so the header reads EasySMX X20 rather than `Xpert2`.

- **Save files are confirmed to live with you, not with the app**, in
  `%APPDATA%\x20ctl\profiles`. Replacing the executable with a newer download
  leaves them untouched. There's a test naming the three paths that would break
  that, because a one-file build unpacks to a temporary directory that's deleted
  on exit: anything stored beside the executable would appear to work and then
  lose the lot.

### Fixed

- **The header and the footer disagreed.** A reconnect set the header to
  "Connecting…" and left the footer still reading "connected", because only one
  of the two was being updated.
- **A pad that was off or out of range printed a library traceback** on the
  command line. It now explains itself the way the app already did.
- **A help string was broken by an editing slip**, leaving the module unable to
  import. The test suite caught it and it was committed anyway; that's a process
  failure worth recording rather than quietly fixing.
- **The screenshot tool raced the app's auto-connect**, so a real controller
  answering mid-render replaced the staged values and the pictures depended on
  whether the pad happened to be switched on. It also still had `Xpert2` typed
  into it long after the app stopped saying that, so the README showed a name
  the app would never show.

## 0.1.0

First release. The protocol was reverse engineered from scratch; no public
documentation of KeyLinker, `Xpert2`, or `com.pulsenet.inputset` appears to
exist.

### The protocol

Recovered by decompiling the vendor's Android app and confirmed against
hardware. Full detail in [docs/01-protocol.md](docs/01-protocol.md).

- **Transport.** BLE GATT on a peripheral advertising as `Xpert2`, separate from
  the gamepad interface, so both are live at once.
- **Framing.** `[opcode][length][serial][nonce][payload][crc8]`, capped at 20
  bytes, then passed through a scrambling pass.
- **Checksum.** Reflected CRC-8, polynomial `0xEB`. The table is generated from
  the polynomial and asserted equal to the one shipped in the vendor app on
  every test run.
- **Payloads.** Byte 0 is a length prefix. Records longer than one packet are
  chunked and fetched by index.
- **Identification.** The pad clones Microsoft controller ids on every link, so
  nothing here matches on VID/PID.

### Added

- **Macros on M1 to M4.** Sequences, chords, and thumbstick directions.
  `A,B` plays in turn, `A+B` presses together, `LS_UP+A` pushes the stick while
  holding a button. Fourteen buttons plus both sticks.
- **Per-step timing.** `A:150` holds for 150ms, `A:150/40` waits 40ms
  afterwards. The hardware stores a duration per step and this exposes it.
- **Multi-packet macros.** Sequences too long for one packet are chunked
  automatically.
- **Macro recording.** Press buttons on the controller and the app captures them
  with the timing you actually played.
- **Vibration strength**, 0 to 100 percent. The pad's own controls can't set
  this, and can't silence rumble at all.
- **Battery level**, a four step gauge with a charging flag.
- **Save files.** Named sets of macros and vibration, stored as JSON. Applying
  one makes the controller match it exactly.
- **Input tester.** Every button lights while held, both sticks draw a position
  trail, triggers show their analog value.
- **Polling rate meter.** Counts the reports per second that actually reach
  Windows, so comparing links is meaningful. Reads 1002 peak on a 2.4GHz
  receiver, matching an independent tool on the same hardware.
- **Transport detection.** Reports whether you're playing over Bluetooth or
  USB.
- **Command line interface** alongside the app, and a **standalone executable**
  that needs no Python.
- **Explanations on every control**, including why there are three millisecond
  fields.

### Not available on the X20

The pad publishes a capability descriptor stating what it will accept, and an
X20 reports zero for these. They exist in the hardware but are driven by button
combinations on the pad and aren't reachable through this protocol. Another
controller on the same chip may report them as available, and the library gates
on the descriptor, so it will simply work.

- RGB lighting
- Turbo
- Gyro

### Deliberately out of scope

- **Firmware flashing.** That runs through the mass storage bootloader, which is
  the only path that can destroy the controller. Use the manufacturer's updater.
  `x20 status` reports the installed version so you can tell when you're behind.

### Fixed during development

Kept because each one records something learned about the hardware.

- **Macros drove both thumbsticks.** An untouched analog entry encodes as
  `0b1000`, not `0b0000`. A zeroed nibble is direction *up*, not centre, so an
  early macro swept both sticks until it was interrupted.
- **The same fault, one line later.** Fixing the mask builder wasn't enough;
  release steps were built with a bare zero mask and recreated it.
- **Macros repeated forever.** The header field is a loop *interval*, not a
  duration. Zero disables looping; anything else repeats until another macro
  button is pressed.
- **Save files didn't switch.** Applying left slots the file didn't define
  alone, so the previous file's macros stayed live while the app showed the new
  one.
- **Recordings were lost.** Recording filled the card but saved nothing, so
  clicking elsewhere discarded it.
- **Removing a save file put it back.** Deleting reloaded the list, which fired
  the selection handler, which autosaved the deleted profile to disk.
- **The recorder discarded its own timing**, averaging every step into a single
  hold and gap.
- **The polling meter undercounted**, reading about 900Hz where the true figure
  was 1002. It counted whether the packet number changed, not by how
  much it changed.
- **Transport detection claimed "wired" on a receiver.** The receiver is
  transparent: same ids, same revision, same driver as a cable. The two are
  indistinguishable, and the app now says so instead of guessing.
- **The windowed launcher died silently.** Under `pythonw` there's no
  `sys.stdout`, and terminal code was being imported during startup.
- **The taskbar icon didn't appear.** The app identity was being set after the
  icon, and Windows had already chosen by then.
- **Clicking a save file did nothing** while the input tester was open.
- **Bluetooth being off looked like a fault.** Settings need it; playing does
  not.
