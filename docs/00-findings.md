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
and no `setupapi.dll`**, so it does not speak HID and does not enumerate devices
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

**This channel is out of scope and must never be touched.** It is the only path
that can brick the device. It is also not where configuration lives.

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
before flashing, which is worth identifying later as a fingerprinting method.

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

KeyLinker ships on iOS. iOS cannot speak USB HID to a gamepad. Therefore the
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

- [ ] Does DInput mode expose a vendor HID collection with feature reports?
- [ ] Does Switch mode expose one?
- [ ] What GATT services does `Xpert2` advertise, and what are the characteristic UUIDs?
- [ ] What is the packet framing: header, opcode, length, payload, checksum?
- [ ] Is there a handshake or unlock sequence before settings writes are accepted?
- [ ] Are settings written to volatile RAM or committed to flash, and is there a
      separate commit command?
- [ ] What is the hardware model id checked by `Upgrade code and hardware mismatch!`?

---

## 6. Safety rules for this project

1. Never invoke the mass-storage/SCSI path. No `\\.\PHYSICALDRIVE`, no
   `IOCTL_SCSI_PASS_THROUGH`, no running the vendor updater during development.
2. Never enter upgrade mode (`L3` held while connecting) except deliberately.
3. Read before writing. Feature-report probes are read-only and safe.
4. Record the full settings state before the first write, so it can be restored.
5. Recovery path for settings damage: hold `C` for 5 seconds.
6. Change one variable at a time when diffing captures.

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
