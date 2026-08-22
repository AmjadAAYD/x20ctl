# EasySMX X20: reverse engineering log

Target: build an open configuration library for the EasySMX X20 gamepad, covering
lighting, gyro, back-button mapping, turbo and stick/trigger parameters.

Status: **Phase 1, mapping the attack surface.** No protocol bytes recovered yet.

---

## 1. Hardware summary

| Property | Value |
|---|---|
| Sticks | Hall effect, detachable, height-adjustable, 12-bit, no inner deadzone |
| Triggers | Hall effect, dual-position, trigger lock |
| Buttons | Mechanical ABXY / shoulders / D-pad, 4 rear buttons M1-M4 |
| Motion | 6-axis gyro, mappable to either stick |
| Lighting | RGB, 3 modes, 4 brightness levels, independent ABXY toggle |
| Polling | 1000 Hz wired and 2.4 GHz |
| Modes | XInput, DInput, Switch |
| Transports | Wired USB, 2.4 GHz dongle, Bluetooth |
| Latency | XInput 3 ms wired / 12 ms 2.4G; DInput 6 ms / 18 ms |

### Settings reachable from the pad itself

These define the minimum feature set the protocol must cover.

| Setting | Combination |
|---|---|
| **RGB colour** | hold `C` + `R3` |
| **RGB mode, breathing or constant** | hold `C` + `L3`, twice |
| RGB brightness, cycles 0 to 3 | hold `C` + `L3` |
| ABXY lighting toggle | `Menu` + `D-pad Right` |
| Map gyro to left stick | hold `C` + `BACK` + `L3` |
| Map gyro to right stick | hold `C` + `BACK` + `R3` |
| Turbo, manual | `T` + `A` once |
| Turbo, automatic | `T` + `A` twice |
| Turbo off | `T` + `A` three times |
| Turbo rate | `C` + left stick up/down |
| Factory reset | hold `C` for 5 seconds |
| Enter firmware upgrade mode | hold `L3` while connecting USB |

All verified working on hardware, 2026-08-16. The colour and mode combinations
were missing from earlier versions of this table, which listed only brightness —
and their absence sent a whole day of protocol work chasing something that has
no protocol path at all. **Colour is on-pad only**; see 01-protocol 4b.

Factory reset is the recovery path for any settings-level mistake.

---

## 2. Analysis of the official updater

File: `vendor/EasySMX X20 Controller-V2.22.exe`
SHA-256: `31727C8BCE8FC3664AE65E286FB0B40979960D5DD992CCB0F29B8B1B30E4DEB2`

| Property | Value |
|---|---|
| Real name | `UsbUpdateAppX.exe`, version 2.0 |
| Build | PE32 x86, MFC, GUI subsystem, unpacked |
| Signature | **unsigned** |
| Sections | `.text` `.rdata` `.data` `.rsrc` `.reloc` |

### Embedded DLL

A complete PE is stored as a resource at file offset `0x1F20E0`, inside `.rsrc`.
Extracted to `vendor/DeviceUsb.dll`, 8704 bytes. The updater drops it at runtime.
Its strings include `Open DeviceUsb.dll fail!` and `Operate DeviceUsb.dll fail!`.

Exports, the entire protocol surface:

```
deviceUsb_Open
deviceUsb_OpenByDrv
deviceUsb_Close
deviceUsb_GetDrvID
deviceUsb_GetInfo
deviceUsb_Request
```

Imports: `KERNEL32.dll` (`CreateFileW`, `DeviceIoControl`, `CloseHandle`, `Sleep`,
`GetLastError`, `MultiByteToWideChar`) and `MSVCR90.dll`. Notably **no `hid.dll`
and no `setupapi.dll`**, so it doesn't speak HID and doesn't enumerate devices
itself.

Leaked PDB path:

```
d:\PROJ\Tools\Downloader\Downloader\bin\Release\dlls\DeviceUsb.pdb
```

### Conclusion: the updater is a mass-storage bootloader

Device path format strings recovered from the parent exe:

```
\\.\%c:
\\.\PHYSICALDRIVE%i
\\.\CDROM%i
```

Combined with `DeviceIoControl` and the absence of any HID import, this means the
pad enumerates as a **removable disk** in upgrade mode and the tool writes
firmware through SCSI pass-through. `deviceUsb_GetDrvID` locates the drive letter;
`deviceUsb_Request` issues the commands.

**This channel is out of scope and must never be touched.** It's the only path
that can brick the device. It's also not where configuration lives.

The generic project name `Downloader` and the absence of EasySMX branding show
this is a chip-vendor tool reused across brands.

### Relevant UI strings

```
The hardware version[ver:%d.%2.2d] is up to date!
Do you want to upgrade from ver:%d.%2.2d to ver:%d.%2.2d?
Upgrade code and hardware mismatch!
The USB is disconnected! please try again!
Please let the gamepad enter the upgrade mode first.
Open update device fail!
```

`Upgrade code and hardware mismatch!` implies a hardware model id is checked
before flashing. **That id is now known**: the updater's first command is a
read-direction vendor CDB with opcode `0xF1`, and it returns the same 18-byte
GUID as `HOST_GUID` (0x93) over BLE. See 01-protocol 4j.

### The updater carries no firmware, and cannot fetch any

Checked 2026-08-16 on this exact binary (SHA-256 above):

- **No network imports.** No WinINet, WinHTTP, ws2_32 or urlmon, so it cannot
  download an image.
- **One large resource**, `IDR_DLL` id 130 at 8704 bytes — that is
  `DeviceUsb.dll`. There is no firmware resource of any kind.
- **No region in the 2.2 MB file has the byte profile of 8051 code.** The control
  case works: the known x86 DLL resource scores 4% on that profile.
- It imports `COMDLG32.dll`, consistent with expecting the **user** to supply a
  firmware file through a browse dialog.

Combined with the vendor server returning `404 匹配失败` for vid 1013 / pid 2009,
and the pad having no read command on any transport, **no firmware image for this
pad exists anywhere we can reach**. Running the updater would read the version and
then ask for a file that does not exist. This is why flashing is out of scope: not
a risk judgement, simply nothing to write.

---

## 3. Live device, wired, XInput mode

Enumeration via `tools/hid_scan.py`.

The pad **clones the Microsoft Xbox 360 controller identity**:

```
VID_045E  PID_028E  rev 0110
BusReportedDeviceDesc: Controller
```

Two USB interfaces, no more:

| Interface | Class | Driver | Purpose |
|---|---|---|---|
| 0 | `FF / 5D / 01` | `xusb22` | XInput control interface |
| 1 | `03` (HID) | `HidUsb` | Generic gamepad |

HID collection:

```
\\?\hid#vid_045e&pid_028e&ig_01#8&1878642d&0&0000#{4d1e55b2-...}
    usage        : page 0x0001 usage 0x05     (Generic Desktop / Gamepad)
    report bytes : input=15 output=0 feature=0
```

### Conclusion: no configuration channel in XInput mode

Zero output reports and zero feature reports. There is no vendor-defined
collection. Every vendor collection present on the test machine belongs to the
host laptop (ASUSTek `VID_0B05` N-KEY keyboard, `ASUF1205` touchpad) and is
unrelated.

Consequence for the library: **never identify this pad by VID/PID alone.**
`045E:028E` is a widely cloned identity and matching on it would cause the
software to attempt configuration of genuine Xbox controllers. Fingerprint on the
interface layout or on the vendor collection once found.

---

## 4. Where the configuration protocol lives

The official configuration app is **KeyLinker**:

| Property | Value |
|---|---|
| Android package | `com.pulsenet.inputset` |
| Publisher | ShenZhen ZhiXu Technology Co., Ltd. |
| iOS | App Store id `1472490585` |
| Latest version seen | 3.70 |
| Device name it connects to | `Xpert2` |
| Advertised scope | keys, lighting, vibration, joystick, trigger, macros, over ten functions |

The `pulsenet` package prefix matches the white-label `Downloader` tooling, so
KeyLinker is a **chip-vendor protocol, not an EasySMX one**. A library that
implements it plausibly works across many unrelated gamepad brands. That is the
main reason this project is worth more than one controller.

### Key deduction

KeyLinker ships on iOS. iOS can't speak USB HID to a gamepad. Therefore the
protocol **must be reachable over Bluetooth LE**, against a custom GATT service,
on a peripheral advertising as `Xpert2`.

Two candidate doors, in priority order:

1. **BLE GATT.** Guaranteed to exist by the iOS deduction. Reachable from Windows
   through WinRT, so `bleak` works.
2. **A vendor HID collection in DInput or Switch mode.** XInput mode hides it, but
   mode changes alter the USB descriptor set. Cheap to test.

### Prior art

None. Searches for reverse engineering of KeyLinker, Xpert2, `com.pulsenet.inputset`
or the `DeviceUsb.dll` interface return nothing. This is unexplored.

---

## 5. Open questions

- [x] **Does DInput mode expose a vendor HID collection with feature reports?**
      **No.** One collection, `0079:181C`, usage page `0x0001` usage `0x05`,
      `feature=0`. Nothing to probe. See 01-protocol 4g.
- [x] **Does Switch mode expose one?** **No.** One collection, `057E:2009`,
      `feature=0`. It is a Pro Controller clone with a 64-byte output report, so
      there is a real command channel, but not a feature-report one.
- [ ] What GATT services does `Xpert2` advertise, and what are the characteristic UUIDs?
- [ ] What is the packet framing: header, opcode, length, payload, checksum?
- [ ] Is there a handshake or unlock sequence before settings writes are accepted?
- [ ] Are settings written to volatile RAM or committed to flash, and is there a
      separate commit command?
- [ ] What is the hardware model id checked by `Upgrade code and hardware mismatch!`?
- [x] **What are key codes 93, 94, 95, 96, 97 and 104?** Answered from the app's
      `ic_big_code_*` drawable table: **95 = `cap`, the `C` button; 96 = `tooble`,
      the `T` button**; 97 = M-left; 104 = M2. **93 and 94 are Select and Start**,
      which the missing drawable did not show: 9 and 10 carry those icons as
      legacy vocabulary while the firmware reports 93 and 94. See 01-protocol 4c.
- [x] Why are `SELECT` (9) and `START` (10) absent from every support sub-query
      despite being physical buttons? They aren't; the pad lists them as 93 and
      94 and rejects 9 and 10. Verified on hardware: a macro setting bit 22 makes
      XInput report `BACK`, bit 23 reports `START`. `C` (95) and `T` (96) really
      are excluded from the changekey and macro lists, so `C` and `T` combinations
      remain impossible to encode.
- [x] **Is there an on-pad combination for RGB colour or mode?** **Yes, both.**
      `C` + `R3` cycles colour; `C` + `L3` twice switches breathing/constant.
      Confirmed working on hardware. They are now in the section 1 table.
- [x] **Can lighting be controlled from software?** **No, and not by the vendor
      either.** The record is writable (chunked, 15 + 10) and the write lands and
      reads back, but the LED code never reads it — proven in both directions on
      two units, and under held page modes 8 and 4. `caps.lighting = 0x00` and the
      app gates its own lighting page on that byte, so KeyLinker cannot do it on
      an X20 either. Every transport is eliminated: config service, BLE OTA
      (write-only), USB bootloader (no read command, so no backup and no
      recoverable write), and the 2.4 GHz dongle (its own 8051 code, no command
      interface). The combos cannot be synthesised either, since `C` (95) and `T`
      (96) are excluded from the macro and changekey lists. See 01-protocol 4b.
- [ ] Does the X20 map the Switch Pro LED subcommands (`0x30` player lights,
      `0x38` HOME light) onto its RGB hardware? The one untested write channel.
      Ceiling is brightness or on/off, not per-zone colour. Spare hardware only:
      `0x11`/`0x12` SPI write and erase share that command space.
- [x] **Do two X20 units report the same capability descriptor?** **Yes**, byte
      for byte, including `lighting`, `turbo` and `sensor` all zero. Settled by
      running `tools/report.py` against a second unit. The question turned out not
      to matter: the pad accepted a lighting write with the byte at zero, so the
      descriptor gates the *app's UI*, not what the firmware will accept.
- [ ] **`READ_PRESS_GUN` (`0xA4`) is undecoded.** `ZXBTHelper.parsingPressGun`
      takes `copyOfRange(cache, 1, 7)` and `setPressGunData` takes three bytes per
      button, so it is a per-button record. "Press gun" is 压枪, recoil control.
      Nothing else is known and nothing in `x20ctl` reads it.
- [ ] **EQ is unimplemented.** `eq = 0x02` is the one enabled capability the
      library ignores. The app draws it as vertical seek bars, so it is a real
      audio equaliser with a real record somewhere.

---

## 6. Safety rules for this project

1. Never **write** over the mass-storage/SCSI path, and never run the vendor
   updater during development. Refined 2026-08-16: read-direction
   `IOCTL_SCSI_PASS_THROUGH_DIRECT` with an opcode allowlist is safe and was used
   to recover the command set (01-protocol 4j). The prohibition is on writes,
   because the bootloader has no read-back and therefore no backup is possible.
   Raw `\\.\PHYSICALDRIVE` access stays off-limits regardless: a wrong index
   damages the host machine, not the pad.
2. Never enter upgrade mode (`L3` held while connecting) except deliberately.
   Entering and leaving it without writing is harmless and has been done.
3. Read before writing. Feature-report probes are read-only and safe.
4. Record the full settings state before the first write, so it can be restored.
5. Recovery path for settings damage: hold `C` for 5 seconds.
6. Change one variable at a time when diffing captures.

---

## 7. Other controllers checked

### EasySMX X05: no software configuration channel found

Checked 2026-08-19 against a physical unit, Bluetooth-paired to the test machine.
Requested feature: bring the X05 under the same configuration this library gives
the X20 (macros, vibration, RGB, curves).

**The user manual describes every setting as an on-pad button combination, with
no companion app mentioned at all:**

| Setting | Combination |
|---|---|
| Enter Bluetooth mode | mode switch to Bluetooth, hold `Home` 5s |
| RGB adjustment mode | double-click `M` |
| RGB mode (steady / breathing / dazzling / gradient / off) | left stick up/down, in RGB mode |
| RGB colour | left stick left/right, in RGB mode |
| RGB brightness | `M` + `L3` |
| Macro programming (M1 or M2 only, not four slots) | hold `M` + `M1`/`M2`, LED flashes red, then press the keys to record |
| Vibration, 0/25/50/75/100% | `M` + left stick up/down |

Three independent checks, all negative:

1. **BLE scan.** `tools/ble_scan.py --all`, run twice (20s and 60s) with the pad
   woken by button presses in between, heard 8-11 nearby Bluetooth devices and
   none of them advertised the KeyLinker config service
   (`d7f010e0-660d-46e9-96c3-19c4148bdab5`), the `Xpert2` name, or the vendor MAC
   prefix `98:B6:E`. `x20 scan` and `tools/report.py` agree: no controller
   exposing that service was found.
2. **XInput.** `tools/report.py` confirms Windows sees the pad on XInput slot 0,
   so the identity-cloning behaviour matches the X20, but that tells us nothing
   about a config channel: XInput never carried one for the X20 either.
3. **HID feature reports.** `tools/hid_scan.py`, no filter, found exactly two
   collections belonging to this pad:

   ```
   VID_045E PID_028E   "Controller (USB Controller)"        feature=0
   VID_045E PID_02E0   "Xbox Bluetooth Gamepad" (Microsoft)  feature=0
   ```

   The second is the real Microsoft Xbox Wireless Controller BLE HOGP profile
   (PID `02E0`), not a vendor collection: the pad clones it over Bluetooth LE
   the way it clones `045E:028E` over USB. Both interfaces declare a feature
   report length of zero, so there is nothing for `hid_scan.py --probe` to read;
   unlike the X20's lighting record, there isn't even a channel that answers and
   does nothing.

**Conclusion: the X05 does not implement the pulsenet/KeyLinker protocol this
library speaks.** Nothing found here contradicts the manual: RGB, macros and
vibration all appear to be firmware-local, driven by the on-pad combinations
above, with no digital read-back or write path discovered on any transport.
There is no protocol left to reverse engineer, so `x20ctl` has nothing to add
for this model. The input tester and battery/connection detection, which are
generic XInput/PnP code rather than KeyLinker-specific, are unaffected and
should work regardless.

If a future firmware revision or a different X05 variant does answer one of the
scans above, that would be worth reopening.

---

## Sources

- <https://www.easysmx.com/pages/support-about-easysmx-x20-controller>
- <https://www.easysmx.com/pages/download-driver>
- <https://www.easysmx.com/pages/app-use>
- <https://gamepadla.com/easysmx-x20.html>
- <https://www.hlplanet.com/easysmx-x20-review/>
- <https://play.google.com/store/apps/details?id=com.pulsenet.inputset>
- Static analysis of `vendor/EasySMX X20 Controller-V2.22.exe`
- Live enumeration on Windows 11, wired, XInput mode
- EasySMX X05 user manual: <https://manuals.plus/easysmx/x05-wireless-controller-manual>
- Live BLE scan and HID enumeration against a physical X05, Windows 11, Bluetooth
