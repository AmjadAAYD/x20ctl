# X20 Pro: read-only discovery and feature plan

Status: preparatory work, 2026-09-25. No X20 Pro unit was available to test.
The current x20ctl release supports the X20 configuration protocol and must
not identify X20 Pro as compatible until its interface is verified.

## Published comparison

| Area | X20 | X20 Pro | What x20ctl can infer now |
|---|---|---|---|
| Sticks | Detachable Hall sticks | TMR sticks with adjustable physical tension | New hardware; no evidence of a software tension control. |
| Haptics | Five levels of vibration | Four motors with instant brake | Motor channel layout and configuration protocol unknown. |
| Triggers | Two-position lock and Hall analog mode | Hall analog or microswitch, with preload | Detectable behavior may depend on gameplay mode; configuration protocol unknown. |
| Extra buttons | Four back buttons, M1–M4 in the X20 app | Six programmable buttons: two mini shoulders, two back and two removable back buttons | Assignment and macro data format unknown. Do not reuse X20's four-slot limit. |
| Lighting/display | RGB lighting with three modes | Smart display for settings, battery and connection mode | Display customization, image transfer and GIF playback are unconfirmed. RGB controls for Pro are also unconfirmed. |
| Receiver | Separate 2.4 GHz receiver | Magnetic 2.4 GHz receiver, charging dock and stand | Charging or receiver telemetry is not promised. |
| Connection | Wired, Bluetooth, 2.4 GHz | Wired, Bluetooth, 2.4 GHz | Each mode may expose different USB HID and BLE interfaces. |

Both official listings claim a 1000 mAh battery. The Pro listing gives a 600 g
net weight, which may include accessories; this is not a measured controller
weight. The published Pro page does not specify a polling rate, GIF support,
screen file format, macro capacity, companion app, or configuration UUIDs.

Sources: [X20 Pro listing](https://www.easysmx.com/products/easysmx%C2%AE-x20-pro-wireless-multiplatform-gaming-controller),
[X20 listing](https://www.easysmx.com/es/products/easysmx-x20-multiplatform-gaming-controller-with-trigger-lock-and-hall-effect-joysticks),
[X20 support FAQ](https://www.easysmx.com/pages/support-about-easysmx-x20-controller),
[official manual/download index](https://www.easysmx.com/pages/download-driver).

## Discovery order when the controller arrives

1. Photograph the box and manual, and record firmware version and connection
   mode shown on the display. Review any official software or manual published
   by then. Keep firmware tools closed.
2. For USB-C and the magnetic receiver separately, run `hid-list` and compare
   VID/PID, usage pages and report lengths. Observe normal gameplay input with
   `tools/hid_scan.py --watch` only after choosing the correct collection.
3. In Bluetooth mode, run `ble-scan`. If a plausible configuration peripheral
   appears, enumerate its GATT table with `ble-gatt`. The optional `--standard`
   flag reads only standard Battery and Device Information values.
4. Compare X20 Pro captures against the X20 protocol description in
   `docs/01-protocol.md`. Matching a UUID is evidence of a shared transport,
   not proof that X20 commands or data layouts have the same meanings.
5. Only after the command map is understood should a separate, explicit
   read-only protocol client be written for settings, macros, lighting and
   display metadata. Decode one field at a time using repeated captures while
   changing one setting on the controller itself. Never send settings writes
   during this phase.
6. The 3.1.0 app already has an isolated Pro illustration and generic read-only
   discovery page. Add Pro **settings controls** only after device-specific
   identification and read contracts are confirmed. Preserve the X20 path
   untouched and gate each Pro feature by measured capabilities.

Safe starter commands from the repository root:

```powershell
.venv\Scripts\python tools\x20_pro_discovery.py hid-list --match EasySMX --output captures\pro-usb-hid.json
.venv\Scripts\python tools\x20_pro_discovery.py ble-scan --seconds 20 --output captures\pro-ble-scan.json
.venv\Scripts\python tools\x20_pro_discovery.py ble-gatt ADDRESS --standard --output captures\pro-ble-gatt.json
.venv\Scripts\python tools\x20_pro_discovery.py compare captures\pro-gatt-before.json captures\pro-gatt-after.json
```

Repeat captures for wired, receiver and Bluetooth modes with different file
names. `captures/` is ignored by Git because addresses and serials can appear
there. The script refuses to overwrite an existing capture. `ble-gatt` connects
for service discovery but does not send any vendor command. No bootloader or
OTA characteristics are read, subscribed to, or written. `compare` runs wholly
offline and ignores capture timestamps and advertisement signal strength.

## Feature plan, ordered by evidence and value

1. **X20 Pro device inspector:** exact model and mode detection, firmware,
   battery only if reported, interface inventory and an exportable diagnostic
   report. This can ship first and remain read-only.
2. **Six-button control map:** show the two mini shoulders and four rear
   controls on a Pro-specific controller illustration. Read assignments and
   macros only if their storage protocol is confirmed. Keep X20's four-button
   model separate.
3. **TMR tuning assistant:** sample live stick center, range and repeatability
   over USB and the receiver, then suggest a deadzone and curve for each game.
   The on-device tension remains a manual adjustment; the app only measures
   the resulting input.
4. **Trigger lab:** visualize raw analog travel and microswitch transitions in
   each connection mode. If the trigger mechanism itself is mechanical, show
   its observed state rather than a non-existent software switch.
5. **Haptic topology viewer:** report only channels exposed by the device;
   support independent motor editing later if the firmware proves it exists.
6. **Display and lighting explorer:** inventory available settings and transfer
   endpoints. GIF upload becomes a candidate only if documentation or captures
   prove an image-transfer protocol, screen dimensions, storage capacity and a
   safe rollback path. Otherwise show the actual display settings supported.
7. **Mode comparison dashboard:** show which input fields and configuration
   features are actually present in wired, receiver and Bluetooth modes. This
   avoids a misleading universal compatibility badge.
8. **Capability-aware profile migration:** offer X20-to-Pro import after both
   models' mappings are known, previewing lost or remapped controls before any
   write. Never silently assume the old macro binary format applies.

## Verification gates

- A name, photo or cloned VID/PID is insufficient to assert X20 compatibility.
- No feature UI may present simulated hardware readings as live data.
- A known X20 query opcode is not sent to Pro merely because a BLE UUID matches.
- No bootloader, firmware update, reset, flash, OTA or descriptor-write code is
  part of this work.
- Hardware feature claims require a capture, repeatable decoding and a second
  read under changed on-device state. A physical write requires a later,
  separate review after a complete backup and read-back strategy.
