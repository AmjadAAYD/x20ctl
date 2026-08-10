# x20ctl

Open configuration for the EasySMX X20 gamepad, and for other controllers
speaking the same KeyLinker protocol.

The X20 ships with no desktop configuration software. The vendor provides a
manual, a driver, and a firmware updater. Everything else lives in a mobile-only
app or behind button combinations on the pad itself. This project documents the
protocol and makes it usable from a PC.

**Working today:** vibration strength, verified against hardware across its full
range. See [status](#status) for exactly what is and is not proven.

The protocol was reverse engineered from scratch. No prior documentation of
KeyLinker, `Xpert2`, or `com.pulsenet.inputset` appears to exist publicly.

---

## Safety

The controller has two entirely separate command channels:

| Channel | Mechanism | Risk | Policy |
|---|---|---|---|
| Bootloader | USB mass storage, SCSI pass-through | **can brick the device** | never touched |
| Configuration | BLE GATT | recoverable | the only target |

### Rules this project follows

1. **No SCSI, ever.** No `\\.\PHYSICALDRIVE`, no drive letters, no running the
   vendor updater. The bootloader is permanently out of scope.
2. **Never enter upgrade mode** (`L3` held while connecting USB).
3. **Read before writing.** Every write is traceable to a captured read or to the
   vendor app's own code. Nothing is guessed.
4. **Tools refuse unsafe opcodes by default.** `ble_query.py` and `ble_probe.py`
   accept only read-only opcodes. `verify_write.py` can construct exactly one
   packet, a provable no-op. `set_vibration.py` always reads first and reuses
   undecoded bytes verbatim rather than inventing values.

**Recovery from any settings-level mistake: hold `C` for 5 seconds** for a
factory reset. Settings live separately from firmware.

---

## Status

| Feature | State |
|---|---|
| BLE transport, framing, CRC, scrambling | confirmed against hardware |
| Reading device info, capabilities, all settings | working |
| **Vibration strength** | **working, verified by feel at 0%, 30%, 100%** |
| Stick curves | format known, **untested** |
| Trigger curves | format known, **untested** |
| Macros | capability supported, payload format not yet traced |
| RGB lighting | **not exposed by the X20** |
| Turbo | **not exposed by the X20** |
| Gyro | **not exposed by the X20** |

### About the three "not exposed" rows

The pad reports its own capabilities in a descriptor that the official app uses to
decide which settings pages to show. An X20 reports zero for lighting, turbo and
gyro. Those features exist in the hardware but are driven entirely by on-pad
button combinations, and are not reachable through this protocol on this model.

This is a property of the controller, not a limitation of this software. Another
brand's pad on the same chip may well report them as available, and this library
gates on that descriptor, so it will simply work.

---

## Requirements

- Python 3.10+
- `bleak` for anything that talks to the controller: `pip install bleak`
- `PySide6` for the desktop app: `pip install PySide6`
- The pad paired over Bluetooth

The library core (`x20ctl/protocol.py`) is pure computation with no dependencies
and no I/O, so the whole packet layer can be exercised without a controller.

---

## The desktop app

```bash
python -m x20ctl.gui
```

Save files down the left, the four macro slots and vibration on the right.
Editing a slot validates as you type, and a macro set to loop is marked, since
that is the one setting that can surprise you.

Applying writes every setting in the save file to the controller. Settings the
pad does not expose are skipped rather than attempted, and the app says so
rather than silently omitting them.

---

## Save files

A save file is a set of up to four macros and a vibration level, stored as JSON
in `%APPDATA%\x20ctl\profiles`. The controller has four fixed macro slots and
knows nothing about save files; switching files rewrites those slots.

```json
{
  "name": "Save file 1",
  "vibration": 30,
  "macros": {
    "M1": { "keys": "A+B", "hold_ms": 100, "gap_ms": 60, "loop_ms": 0 },
    "M2": { "keys": "X,Y", "hold_ms": 120, "gap_ms": 80, "loop_ms": 0 },
    "M3": null,
    "M4": null
  }
}
```

In a key sequence, `+` presses keys together and `,` plays them one after
another, so `A+B` is a chord and `A,B` is a sequence.

`loop_ms` is an interval, not a duration: `0` fires the macro once, and anything
else repeats it forever until another macro button is pressed.

---

## Command line

```bash
x20 scan
x20 status
x20 vibration 60
x20 macro M1 "A+B" --hold 100
x20 profile set "Save file 1" M1 "A+B" --vibration 30
x20 profile apply "Save file 1"
```

Run as `python -m x20ctl <command>`. The address is remembered after the first
successful scan.

---

## Low-level tools

Find the controller. It advertises as `Xpert2`, on a MAC distinct from the one it
uses for the gamepad interface:

```bash
python tools/ble_scan.py
```

Inspect its GATT table:

```bash
python tools/ble_enum.py <address>
```

Read a setting:

```bash
python tools/ble_probe.py <address> HOST_MOTOR --label baseline
```

Read every setting at once:

```bash
python tools/ble_sweep.py <address> --opcodes HOST_STICK,HOST_TRIGGER,HOST_MOTOR
```

Read and change vibration strength:

```bash
python tools/set_vibration.py <address>
python tools/set_vibration.py <address> --percent 60
python tools/set_vibration.py <address> --restore 4c
```

Run the tests, no hardware required:

```bash
python tests/test_protocol.py
```

---

## The protocol, in brief

Full detail in [docs/01-protocol.md](docs/01-protocol.md).

**Transport.** BLE GATT. Service `d7f010e0-660d-46e9-96c3-19c4148bdab5`, write on
`...e1`, notify on `...e2`. The pad advertises this as a separate peripheral
alongside its gamepad interface, so both are live at once.

**Packets.** `[opcode][length][serial][nonce][payload][crc8]`, capped at 20 bytes
to fit a default BLE MTU, then passed through a scrambling pass. Replies use a
single `RESPONSE` opcode and are matched to requests by the serial byte.

**Checksum.** Reflected CRC-8, polynomial `0xEB`. The table is generated from the
polynomial and asserted equal to the one shipped in the vendor app on every test
run.

**Payloads.** Byte 0 of every response is a length prefix. Records longer than one
packet are chunked and fetched by index.

---

## Layout

```
docs/       protocol documentation and the reverse engineering log
tools/      discovery and configuration utilities
x20ctl/     the protocol library
tests/      offline tests, including bytes captured from real hardware
vendor/     manufacturer binaries, analysis only, never committed
captures/   packet logs, never committed
```

---

## Notes for other devices

The protocol belongs to the chip vendor (ShenZhen ZhiXu, package
`com.pulsenet.inputset`), not to EasySMX, so it likely covers controllers from
several brands. Two things to know before pointing this at other hardware:

- **Do not identify a pad by USB VID/PID.** The X20 clones Microsoft's
  `045E:028E` when wired and `045E:02FD` over Bluetooth Classic. Matching on those
  would target genuine Xbox controllers. Identify by the BLE peripheral instead.
- **Always read the capability descriptor first** and honour it. It is how the pad
  tells you which settings it will accept.

---

## Licence

MIT, see [LICENSE](LICENSE).

This is an independent interoperability project, not affiliated with or endorsed
by any manufacturer. No vendor firmware, application binaries, or decompiled
source are distributed here.
