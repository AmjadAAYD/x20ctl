# Receiving evidence and deciding the next experiment

The delivered EXE is a standalone collector, not an x20ctl controller implementation. The current app catalog remains X20 with its existing backend and X20 Pro with a null backend/inactive placeholder. Other model claims are research labels. Do not add a registry entry or lift a placeholder gate merely because discovery succeeds.

## Ordered workflow

1. Send the short intake in `HOW_TO_SEND.txt` or the model-specific [research message](research/03-MESSAGES-TO-VOLUNTEERS.md).
2. Send this kit once model, OS and available connections are clear. Ask for one short controller/mode session, not every optional stage at once.
3. Save the received original ZIP privately. Validate it offline before interpreting it. Do not execute anything received from a volunteer. Validation checks structure/hashes; it does not prove honest provenance or absence of private information.
4. Read the claim, session selection/mode/transport and failed/skipped limits first. Then selected PnP/HID metadata, USB descriptors, BLE topology, and labeled input. Compare evidence within the same session before combining devices/modes.
5. Assign one guided original-descriptor or manufacturer-app capture only when the first report identifies a plausible channel.
6. Derive a model/mode-specific command from repeated controlled evidence, then implement an isolated, gated backend separately. Test a narrowly defined reversible feature on physical hardware and confirm restoration before claiming support.

Use the two D10 owners to compare independent hardware/firmware evidence. Keep X05 and X05 Pro independent until their observed descriptors/configuration exchanges justify sharing implementation. The X20 owner supplies a control case, not proof that another model accepts X20 commands. Investigate X10's actual official configuration route rather than assuming a Windows HID configuration interface exists.

## Commands

```powershell
.\ControllerScanKit.exe --validate .\received\scan-EXAMPLE.zip
.\ControllerScanKit.exe --self-check
.\ControllerScanKit.exe --advanced
```

Validation reads in place and never extracts files. It rejects traversal/absolute/backslash names, duplicate case-insensitive names, links, encryption, ZIP64 entries, oversized data, nested ZIP/executable headers, unlisted/missing/hash-mismatched files, unsupported manifest versions and malformed input records. Reports permit 128 entries, 4 MiB/file expanded, 32 MiB total expanded and 16 MiB compressed. Supplemental captures/photos are deliberately separate. Do not bypass a failed validator to auto-import evidence.

## Schema version 1

Each run has a random `submissionId`, `collectorVersion`, `createdAtUtc`, `claimedModel`, `modelDetected: false`, `sessions`, `files` and `privacy`. This is an implemented schema; the older example manifest in `research/templates/` was a design sketch and is not the validator's contract.

| Artifact | Actual contents |
|---|---|
| `manifest.json` | Run metadata, per-session summaries, immutable file byte counts/SHA-256; excludes its own hash |
| `system.json` | OS/version/architecture/collector version; no hostname or username |
| `device/sessionNN/windows-device.json` | Selected physical parent's interfaces, allowed public metadata; no instance path/serial |
| `device/sessionNN/usb-descriptors.json` | Status, VID/PID/revisions, interfaces/endpoints from standard USB descriptors, or explicit limit |
| `device/sessionNN/usb-device-descriptor.bin` | Actual 18-byte USB device descriptor when available |
| `device/sessionNN/usb-config-N.bin` | Actual configuration descriptor bytes when available; not HID report descriptor |
| `device/sessionNN/ble-advertisement.json` | Selected name, service UUIDs/RSSI and payload-presence flags; no address/raw adverts |
| `device/sessionNN/ble-gatt.json` | Selected service/characteristic/descriptor UUIDs/properties and allowlisted standard values |
| `device/sessionNN/session.json` | Mode/transport/selection, successes/failures, input action summaries; may be absent for interrupted session |
| `input/sessionNN/NN-action.jsonl` | One UTC/elapsed-time/action/source record per observed host sample |
| `input/sessionNN/mapping.json` | Tentative control correlations; changed fields/button bits/ranges or raw-byte offsets; timing/Home limitations |
| `docs/volunteer-notes.txt` | Optional sanitized notes requiring manual privacy review |

Input `source: xinput_state` contains documented API values: slot, packet counter, button mask, byte triggers, signed 16-bit sticks. Packet/slot changes are excluded from control mapping. `source: hid_input` contains exactly the read-only selected gameplay collection bytes as hex, report length and framing. It is not USBPcap, not an output/feature report, and not XInput normalized data. Each source remains explicit.

The current byte-difference summary groups by frame length and first byte (normally Windows' report-ID position). It is only a heuristic for tentative correlations; without the actual descriptor it cannot decode layouts, distinguish sequence counters/drift or prove a control mapping. Host sampling sleeps about 20 ms after each observed input; Windows/backend buffering and read time affect observations. No latency, polling-rate, debounce or precision claim follows.

## Native operations and scope

Disconnected/connected PnP inventories use SetupAPI/ConfigManager for USB-device and HID interfaces. Only selected related HID interfaces receive product/manufacturer, attributes and preparsed-capability reads. Grouping uses the confirmed USB physical parent; it is not a VID/PID grouping. Bluetooth HID interfaces may lack a shared USB parent and remain separate.

Standard USB queries use functions 258 (hub info), 264 (driver-key association), 274 (connection info) and 260 (configuration GET_DESCRIPTOR) with initialized full request buffers, based on [Microsoft's USBView example](https://github.com/microsoft/Windows-driver-samples/blob/main/usb/usbview/enum.c). The handle has metadata access, not a generic-write fallback. Lack of access/association is unavailable; no driver or privilege changes. The selected VID/PID must still match the observed descriptor. Root-hub port numbers and driver-key names stay private.

Documented XInputGetState has no documented Home/Guide field. HID input requires Generic Desktop page 0x01 and joystick/gamepad/multiaxis usage 0x04/0x05/0x08 with a bounded input buffer; no keyboard/mouse reads. The kit does not bind output, SetFeature, serial-string, undocumented ordinal or vibration APIs.

BLE exports topology only for a volunteer-confirmed new off/on candidate. It reads Battery 0x180F/0x2A19 and Device Information 0x180A/{0x2A24,26,27,28,29}. Serial 0x2A25, vendor/DFU/bootloader characteristics, descriptor values, notification subscriptions, configuration writes and automatic pairing are absent. A readable vendor characteristic with the same short UUID is still excluded because the containing service must match. Scan absence is scoped to the tested conditions, not an assertion that BLE is unsupported. Service results can reflect Windows cache; record that limitation before assuming freshness.

Process timeouts preserve the console from stalled native calls. All hardware workers are bounded; a cancelled/timed-out action may retain partial JSONL. Plain `failed`, `cancelled`, `limit_reached`, `no_samples`, `no_change_observed` and `change_observed` are evidence states, not support verdicts. Exceptions are summarized by stage/type rather than dumping private paths.

## Acceptance boundary

Automated synthetic tests verify association, privacy export, USB parsing/query boundary, BLE read allowlist, raw/API distinctions, changing packet counters, archive integrity/limits and complete/cancelled wizard paths. Packaged `--self-check` confirms imports, structure layout and archive round-trip without hardware. These do not prove native Windows calls work with a specific controller.

Before broad volunteer distribution, pilot one X20 direct USB session and its normal BLE configuration mode, inspect actual exported privacy, repeat with X20 Pro hardware when available, and obtain at least one ordinary unknown-model report. Exercise timeout/disconnect and review paths on a clean Windows 10/11 x64 machine without Python. Record actual report files and observed results. This is the remaining hardware acceptance work, not something the build claims completed.

## Specification reconciliation

The supplied executive summary is design input, not an authority to invent API support. There is no public `HidD_GetReportDescriptor` call; modern HIDAPI's reconstructed Windows descriptor, when available, would need a distinct provenance label. This kit does not manufacture one. It does not read every BLE value or enter DFU, persist serial/address hashes, automatically install libraries/drivers, or guarantee privacy for whole-hub captures. Configuration traffic is a separately reviewed advanced route.
