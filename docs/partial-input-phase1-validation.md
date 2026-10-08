# Phase 1 local implementation and validation

Date: 2026-10-08. Local only: no release, push, website deployment, screen use or physical-controller operation.

## Implemented

The existing D10, X15, X05 and X10 profiles now offer experimental input Studios with explicit disconnect/reconnect source selection and per-player ownership. Model/player changes invalidate discoveries. Known/Missing information lists actual limitations. All unverified remap, macro, curves, saved-device-settings and advanced vibration/configuration operations remain disabled.

| Model | Input Tester path | Identification | Unknown / disabled |
| --- | --- | --- | --- |
| D10 | Correlated Windows XInput, if exposed | 2345:E062 identifies the published receiver; it does not verify the wireless controller link | Native/DInput layouts, independent M inputs, vendor reports 6/7 semantics, macro/trigger/vibration configuration and bootloader identities |
| X15 | Correlated XInput; strictly matched 0079:181C usage 1/5, input 10/output 5/feature 0 candidate HID layout | 1A34:F517 is experimental owner-reported receiver evidence; 0079:181C and 045E:028E are not model detectors | Mode/revision confirmation, proportional trigger evidence, independent M1/M2, native descriptors and configuration transport/commands |
| X05 | Correlated Windows XInput, if exposed | Generic 045E:028E never identifies X05 | Native/receiver/Bluetooth fingerprints, raw trigger layout, configuration/vibration and firmware-mode identity |
| X10 | Correlated Windows XInput, if exposed | Generic 045E:028E never identifies X10 | Native/receiver/Bluetooth fingerprints, QMacro commands, rear-button independence, trigger/vibration/macro configuration and firmware identity |

The standard XInput mapping includes A/B/X/Y, D-pad directions, LB/RB, L3/R3, Back/View and Start/Menu, plus both stick axes and LT/RT values. Home/Guide and independent M keys have no documented fields in this standard API. Rear controls may emit ordinary assigned buttons instead. Unsupported HID layouts are raw scanner evidence, never guessed live mappings. Physical controller behavior has not been tested here.

## Scanner changes

- Existing selected-device USB configuration/device descriptors and parsed HID caps are retained. Caps are explicitly marked as not original descriptor bytes. Original HID descriptors can remain unavailable through the Windows collector; a reconnect-time external USB trace is the practical fallback.
- Existing timestamped raw input and trigger phases now include smooth up/down sweeps.
- Optional repeated ordinary/rear-button presses, long/short physical trigger comparisons, simultaneous pulls, and decoded representation summaries. Unknown raw layouts stay unknown.
- Optional owner-programmed M=A, M=A/B and clear tests for each known slot; original assignments/restoration records are required. Playback is input evidence, not programming-protocol capture.
- D10 vendor input is logged only with raw-data authorization plus explicit confirmation and an exact VID/PID/usage/length match. No vendor output/feature reports are sent.
- Optional standard XInput off/low/medium/high/maximum pulses and separate low left/right channels. Each pulse is 0.35 seconds and stops afterward, including interruption cleanup. API success is distinct from owner-observed physical feedback. Trigger motors are not independently addressed.
- Optional owner-confirmed identities in wired XInput, native/DInput, receiver, Bluetooth and documented firmware modes; no updater execution or firmware write.
- New input/button-map.json, input/trigger-modes.json, input/protocol-capture-status.json, device/hid-caps.json, optional device/mode-identities.json, experiments/macro-playback.json and experiments/vibration.json. Raw reports retain timestamps and remain covered by the existing reviewed schema-2 ZIP export.
- No lighting tasks or RGB commands were added. No automatic protocol sniffer, capture driver or command replay was introduced.

## Verification

- 274 distinct focused backend tests passed: the 272-test regression run plus two subsequently added optional-session/generic-model tests. The complete new Phase 1 test file passes all 26 tests.
- TypeScript check and Vite production build passed; existing large-chunk warning remains.
- 38 frontend unit tests passed.
- Eight isolated headless flow groups passed: the four input profiles, unavailable X05 Pro, scanner review/manual fallback, and X20/Pro regression; wide/compact layout checks included.
- Scoped Ruff and git whitespace checks passed.
- PyInstaller Windows build passed. Actual windowed EXE internal-worker capabilities returned scanner version 1.2.0-app-dev and hardware_access=false. Ten affected compiled modules, the controller catalog and all 34 bundled frontend assets match current source.
- No native GUI acceptance, physical input/rumble test, fresh controller report, external trace capture or actual report transmission was performed.

## Local artifact

Executable: dist/local-phase1-20261008/x20ctl.exe
SHA-256: 11af5467914db63fa459eabe74be195b1886355ccfcca20a76cd151420829d07
Size: 83881135 bytes
The desktop version remains 4.1.0-preview.1, but this is a distinct unreleased local development build. The published executable was not replaced.

Build evidence: artifacts/partial-input-phase1-review/build.json
UI evidence: artifacts/partial-input-phase1-review/report.json

## Main files changed

- x20ctl/controllers/catalog.json, __init__.py and compatibility.py: evidence metadata/receiver classification.
- x20ctl/desktop/service.py, x15_input.py and scan_worker.py: shared guarded input sources and worker operations.
- x20ctl/desktop/research_scan.py and rumble.py: scanner integration and standard bounded motor API.
- x20ctl/scanning/guided.py, analysis.py, windows.py, backend.py, __init__.py and PROVENANCE.json: guided sessions, conservative analysis, input allowlists and provenance.
- src/controllers.ts, App.tsx, components/InputWorkspace.tsx, ControllerResearchScanner.tsx and research-scanner.css: shared read-only UI and status/capture guidance.
- tests/test_partial_input_phase1.py and tools/x15_scanner_review.cjs: backend and isolated UI verification.

[External capture research and primary sources](optional-external-protocol-capture.md) documents USBPcap/Wireshark, Windows BTVS, Android HCI logs and optional BLE hardware, with tradeoffs and a proposed later pilot. It is not required for Phase 1 and was not installed or exercised.

## Remaining physical evidence

Owners must still establish correct model/mode/firmware identities, physical button correspondence, independent rear-button visibility, long/short trigger behavior, actual motor feedback and proprietary configuration traffic. The implementation does not infer these from public IDs or fixture tests. Native DInput/Bluetooth HID live decoding outside the existing X15 candidate layout is not enabled.
