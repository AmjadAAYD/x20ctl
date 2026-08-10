# Changelog

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
  that needs no Python. A controller that is off or out of range explains itself
  there the same way it does in the app, rather than printing a library
  traceback.
- **A version number**, reported by `x20 --version`, by `x20 status` alongside
  the controller's own firmware version, in the app's sidebar, and in the
  executable's file properties. All four read the one value in the package, and
  packaging reads it back out of there too.
- **Stick and trigger deadzones and response curves.** Both records decode to an
  inner and outer deadzone, two control points and a flag byte, and both are
  editable in the app and on the command line. Writing is **confirmed on
  hardware for both records**: a stick deadzone written from 8 to 10 and a
  trigger deadzone from 4 to 6 each read back changed, with the other channel
  untouched, and restoring put each record back byte for byte. Every write reads
  the record back and reports whether the controller took it rather than
  assuming, and the values read at connection are kept so anything can be put
  back.
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
