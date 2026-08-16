# Changelog

## 1.0.0 — 2026-08-15

First stable release. Verified against two EasySMX X20 controllers.

### Application

The app opens on a controller roster rather than a settings page.

- Add up to four controllers, one per player slot (P1–P4)
- Controllers are identified by Bluetooth address, so two identical pads keep
  separate save files
- Scanning runs off the UI thread; a failed scan reports and stays usable
- Live presence: green when a controller answers a sweep, red when it stops
- A controller that goes quiet prompts to **Remove** or **Reconnect** rather
  than vanishing or lingering silently
- Battery level in the header, four bars, red at the lowest
- Left sidebar navigation with **Simple** and **Advanced** modes; Advanced is a
  strict superset
- Launches without a console window (`x20ctl.bat`, or `pythonw -m x20ctl.gui`)

### Settings pages

- **Buttons** — remap per button, laid out in controller order (LT/RT, LB/RB,
  L3/R3, d-pad against face buttons)
- **Sticks** — deadzones and response curves
- **Triggers** — four travel zones and five response shapes, with a live meter
- **Vibration** — saves itself, and pulses the motors at the chosen strength so
  the setting can be felt rather than guessed
- **Macros** — piano-roll editor across all four slots
- **Saved macros** — whole-controller setups: open in the editor, send to the
  controller, or delete
- **Power** — idle shutdown timeout, 1–30 minutes or never (Advanced)
- **Device** — sensor calibration and reported device details (Advanced)
- **Test** — live view of every button, both sticks and both triggers
- Factory reset in the header, behind a confirmation naming what it clears

### Macro editor

- One grid column is one hardware step, so a macro read off a controller
  appears in the editor unchanged
- Record a sequence by playing it, or draw it by hand
- Stick directions chosen with a dial that snaps to the eight headings the
  hardware stores
- Per-step durations snap to the controller's 5 ms resolution
- Repeat interval for looping macros
- Refuses to send an empty macro or one past the 47-step ceiling, with the
  reason
- A 30% pulse confirms a macro reached the controller

### Protocol

Settings decoded and verified on hardware this release:

- **Idle shutdown timeout** — four bytes inside the motor record, a 32-bit
  count of 5 ms ticks; all ones means never
- **Trigger zones** — deadzone pairs: `(0,0)`, `(4,34)`, `(20,40)`, `(30,60)`
- **Response curves** — five presets as Hermite control points, shared between
  sticks and triggers
- **Sensor calibration** — `SET_MODE 03 DF AB 10`; does not affect the sticks
- **Factory reset** — `RECOVER 03 DF A9 02`; the trailing byte selects a
  protocol generation, and the wrong one is ignored silently
- **Macro read-back** — `HOST_MACRO` returns stored macros where `READ_MACRO`
  answers with zeros (found by [chriss80](https://github.com/chriss80/x20ctl))
- **Button remapping** — `HOST_CHANGEKEY` write path, with read-back
- **Select and Start are key codes 93 and 94**, not 9 and 10; they work as
  remap targets and are ignored as remap sources
- `decode_key_list` stops at the declared count: the controller answers the
  turbo query with its record twice over

### Command line

- `x20 sleep` — read or set the idle shutdown timeout
- `x20 remap` — read or set button remapping
- `x20 macro --read` — read stored macros back
- `x20 curve triggers --gear` / `--preset` — trigger zones and response shapes
- `x20 calibrate` — sensor calibration
- `x20 factory-reset` — clear stored settings, behind `--yes`

### Fixed

- Macro payloads are capped at 47 entries, the point where the four-bit chunk
  index runs out, instead of failing inside the packet builder
- Recording a macro captures the left stick, not only buttons
- Reading a key list no longer overruns into a repeated record
- Recording nothing no longer raises

### Known limitations

- Output mode (XInput / DirectInput) can only be changed with the controller's
  own button combination; the protocol command for it is not yet understood
- Sensor calibration is sent correctly but its effect is unverified
- Lighting and turbo are not configurable over this protocol; the controller
  reports both as unavailable
- Trigger zones and curves are chosen by name rather than drawn

## 0.2.1

Command line tool and single-controller GUI. Development releases.
