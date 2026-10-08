# Remapping dossier follow-up

2026-10-08. Read the execution text first, then the complete supplied dossier. The dossier preserves a truncated earlier answer and unresolved citations; its wording is not blanket verification of the underlying claims. No release, push, deployment, physical-controller operation or user-screen access occurred.

## Changes beyond the existing Phase 1 implementation

- BLE capture/export now records dynamically discovered characteristic handles. X15's d7f010e0/e1/e2 and FF12-family attributes and X10's QMacro name are discovery hints only. They identify no model and authorize no configuration writes. Unknown vendor-readable values remain unread; no notifications are subscribed or vendor characteristics written.
- Model-specific Known/Missing text distinguishes public transport/feature evidence from missing remapping transactions.
- Rear/M testing now records original baseline, cleared behavior, A assignment, another clear, B assignment, optional A/B playback, and final clear/restoration. X05's conflicting rear-button descriptions require an owner confirmation; its artwork and software macro slots are not altered by that report.
- X15/X10 owners can record a working official-app session: baseline, one reversible mapping/save, read-back, phone disconnect and PC check, power-cycle/persistence, restoration/read-back and a second power-cycle. These are owner confirmations, not decoded packets or completed physical acceptance. X20CTL sends no setting commands.
- Scanner intake records optional controller/receiver firmware, hardware revision and app name/version. Imported trace associations include model/mode/transport, firmware/app context, SHA-256 and action-timeline references. Context is explicitly owner reported and target/protocol verification remains false.
- Trace import accepts complete USBPcap, Android BTSnoop and Bluetooth HCI PCAP/PCAPNG containers (HCI link types 187/201). Network traces remain rejected. This validates containers and packet boundaries, not device identity or command semantics. It does not automatically launch USBPcap, BTVS or a phone logger.
- Found a negative-guard gap in legacy X20 discovery: explicitly named X15/QMacro/X10/D10/X05 peripherals could reach the X20 connection list. They are now excluded and rejected before X20 driver construction. This is a negative name guard, not positive identity proof; existing X20 discovery behavior is otherwise retained.

## Per-model decision

| Model | Evidence / current support | Persistent write decision | Next useful capture |
| --- | --- | --- | --- |
| D10 | Public 2345:E062 receiver dump; selected XInput input testing; exact-layout vendor INPUT collection logging | NO-GO: Report 7 semantics, opcodes, key encoding, ACK/readback and firmware/mode limits missing | Receiver USBPcap plus raw input during local cleared/A/B M-button experiments. Import may contain Report 7; this code does not decode or synthesize it |
| X15 | Experimental 1A34:F517 receiver; shared 0079:181C candidate input layout; owner GATT/persistence evidence; discovery hints | NO-GO: shared UUIDs do not authorize X20 opcode 0x36 or its payload | KeyLinker Android HCI baseline/change/readback/disconnect/power-cycle/restore plus GATT and firmware/app metadata |
| X05 | Selected-source input support; historical tested-unit negative configuration evidence | NO-GO: no verified host write channel; generic IDs not unique | Revision/firmware, original descriptors/GATT and owner-confirmed rear-button comparison if physically present |
| X10 | Selected-source input support; manufacturer Android/QMacro configuration instructions | NO-GO: GATT/command framing, key encoding, ACK/readback and persistence transactions missing | EasySMX/QMacro Android HCI session with the same differential and restoration sequence |

All four reject valid A-to-B apply requests before unverified hardware configuration access. Input testing does not constitute remapping. No host-side remapper was added. Independent rear inputs remain unknown overall; standard XInput has no documented M fields. All new workflows exclude lighting and firmware writes.

## Sources checked

- [X15 owner GATT dump](https://www.reddit.com/r/EasySMX/comments/1pkk06r/easysmx_x15_arduino_ble_hid_support_nimble/): retrieved reported attributes; its arbitrary-write anecdote was not copied or replayed.
- [X15 owner persistence reports](https://www.reddit.com/r/EasySMX/comments/1cm2u4m/easysmx_x15_please_tell_me_how_to_disable_the_m1/): KeyLinker changes retained after switching from phone to wired PC. This does not establish an X20CTL remap payload or universal power-cycle acceptance.
- [X10 manufacturer support](https://www.easysmx.com/pages/support-about-easysmx-x10-controller): QMacro app connection and local programming.
- [X05 manufacturer support](https://www.easysmx.com/pages/support-about-easysmx-x05-controller) did not corroborate M1/M2 in the retrieved text; older repository findings describe them. Treat this discrepancy as revision/identification work, not automatic support.
- [D10 public receiver evidence](https://github.com/paroj/xpad/issues/344) and [driver proposal](https://github.com/paroj/xpad/pull/345) remain input/descriptor evidence rather than configuration semantics.

## Verification

294 focused backend tests passed, including 20 dossier-follow-up tests. TypeScript and Vite build passed (existing large-chunk warning remains). Eight isolated headless UI flow groups passed. Scoped Ruff and whitespace checks passed. Windows PyInstaller build passed; its internal worker returned hardware_access=false. Eight affected frozen modules, the frontend assets and controller catalog match current source.

No native GUI acceptance, physical mapping/motor test, actual BLE discovery, external protocol capture or live submission was performed. Remap byte framing, source/target encoding, sessions/counters/checksums, ACK/readback, persistence/restoration and revision/mode boundaries remain prerequisites for future write backends. See [external capture research](optional-external-protocol-capture.md).

## Local build

Executable: dist/local-dossier-20261008/x20ctl.exe
SHA-256: 3393ec0529eb043d18abce351a3be5944a70ae8c0a11a46105990dd387134b8f
Desktop version remains 4.1.0-preview.1; this is an unreleased development build, not the public EXE. Packaging verification: artifacts/remapping-dossier-followup/build.json.

Main files: scanning/model_evidence.py, protocol_session.py, ble.py, guided.py, app_capture.py; desktop/research_scan.py and service.py; controllers/compatibility.py and catalog.json; ControllerResearchScanner.tsx; tests/test_remapping_dossier_followup.py.
