# x20ctl

An open configuration library for the EasySMX X20 gamepad, and for any other pad
speaking the same KeyLinker protocol.

The X20 has no desktop configuration software. The vendor ships only a manual, a
driver, and a firmware updater. Settings such as lighting, gyro mapping, rear
button assignment and turbo are only reachable through button combinations on the
pad or through a mobile-only app. This project closes that gap.

**Status: Phase 1, mapping. No protocol bytes recovered yet.** See
[docs/00-findings.md](docs/00-findings.md).

---

## Safety contract

This is the most important section in the repository.

The controller has two completely separate command channels:

| Channel | Mechanism | Risk | Policy |
|---|---|---|---|
| Bootloader | USB mass storage, SCSI pass-through | **can brick the device** | never touched |
| Configuration | HID or BLE GATT, to be confirmed | recoverable | the only target |

### Hard rules

1. **This project never issues a SCSI command, never opens `\\.\PHYSICALDRIVE`,
   never opens a drive letter, and never runs the vendor updater.** The bootloader
   is the only path that can destroy the pad and it is permanently out of scope.
2. **Never put the pad in upgrade mode** (hold `L3` while connecting USB) while
   working on this project.
3. **Read before writing.** Enumeration and feature-report reads are non-destructive.
   Every discovery step is exhausted before any write is attempted.
4. **No write happens without a documented reason.** Every byte sent to the device
   must be traceable to a captured packet from the official app or to a
   deliberately designed experiment recorded in the findings log.
5. **Capture full state before the first write**, so the pad can be restored.
6. **Recovery path for any settings-level mistake: hold `C` for 5 seconds** for a
   factory reset.

Following rules 1 and 2, the realistic worst case is a controller that needs a
factory reset. Settings live in a different place from firmware.

---

## Layout

```
docs/       reverse engineering log and protocol notes
tools/      discovery utilities, all read-only
vendor/     vendor binaries kept for analysis, never redistributed
captures/   USB and BLE packet captures
x20ctl/     the library itself, once there is a protocol to implement
```

## Tools

All read-only.

```bash
python tools/hid_scan.py
```

List every HID collection on the system.

```bash
python tools/hid_scan.py --vendor
```

Show only vendor-defined collections, usage page `0xFF00` or above. This is how
a hidden configuration channel announces itself.

```bash
python tools/hid_scan.py --vid 045E
```

Filter to one vendor id. The X20 clones `045E:028E` in XInput mode.

```bash
python tools/hid_scan.py --probe "\\?\hid#..."
```

Sweep feature report ids on one collection. Issues reads only, never writes.

```bash
python tools/hid_scan.py --watch "\\?\hid#..."
```

Print input reports as they change, for mapping buttons to bits.

## Findings so far

- The vendor updater is a rebadged `UsbUpdateAppX.exe` that drops a `DeviceUsb.dll`
  and drives the pad as a **mass storage device over SCSI**. That is the bootloader.
- In XInput mode the pad exposes exactly two USB interfaces and **no vendor HID
  collection, no feature reports**. No configuration channel there.
- The real configuration app is **KeyLinker**, Android package
  `com.pulsenet.inputset`, by ShenZhen ZhiXu Technology. It is a **chip-vendor
  protocol**, not an EasySMX one, so this library should generalise across brands.
- KeyLinker ships on iOS, which cannot speak USB HID to a gamepad. The protocol
  must therefore be reachable **over Bluetooth LE**, on a peripheral named `Xpert2`.

## Licence and redistribution

Code here is intended for MIT release. The contents of `vendor/` are the
manufacturer's copyrighted binaries, kept locally for interoperability analysis
only, and are excluded from version control. Do not redistribute them.

Reverse engineering hardware you own for the purpose of interoperability is
well-precedented; see OpenRGB, DS4Windows, and dekuNukem's Nintendo Switch work.
