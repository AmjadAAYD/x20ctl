# Controller Scan Kit implementation plan — 2026-10-01

User approved implementation after the contributor research/handoff and supplied a second scan-kit specification. Build a separate console wizard and packaged Windows x64 executable; preserve the app registry and inactive Pro workspace.

1. Implement bounded, versioned local evidence creation and validation, privacy-safe exports and selected-device correlation. Test malformed archives, identifiers, false input mappings and changed hardware inventory first.
2. Add native Windows metadata/HID read-only input, standard USB hub descriptor reads, documented XInput sampling and standard BLE discovery. Do not reuse unsafe report sweeps or configuration clients. Test native boundaries with isolated fixtures; no unsolicited hardware operation during automated verification.
3. Build a console wizard: claimed model; disconnected baseline; selected correlated device; separate transport/mode sessions; optional BLE and guided input; privacy preview; local ZIP. Unknown/failed/skipped artifacts remain explicit.
4. Provide source scripts, START_HERE.bat, instructions, validator, source/build provenance and dependency notices. Bundle Python/Bleak in an executable so volunteers need no installation. Never silently install dependencies or capture drivers.
5. Run focused tests, a scripted fixture-driven wizard, cancellation/partial export and packaged no-hardware self-check/validation. Verify ZIP contents and SHA-256. Live hardware acceptance remains pending unless actual device evidence is available.

Scope: tools/controller_scan_kit/, tools/controller_scan.py, tools/build_controller_scan_kit.py, tests/test_controller_scan_kit.py, docs/controller-scan-kit/, and this plan. Generated files stay under ignored artifacts/dist/captures paths. No app/UI changes, backend/upload changes, Git staging/commit, release or messages.

Corrections to supplied specification: no public HidD_GetReportDescriptor; no DFU/version probing; no vendor reads/notify/writes; XInput states are not HID reports; USBPcap does not capture arbitrary wireless traffic; no privacy claim for broad captures; unique IDs omitted rather than persistent unsalted hashes. Research model claims remain separate from supported models.

## Delivered and checked

Implemented all five local delivery stages. The final distribution is `artifacts/controller-scan-kit/verified/x20ctl-controller-scan-kit-v1.0.0-windows-x64-20261001.zip`; its adjacent SHA-256 file and `verification-report.json` record the checked artifact. The first build in the parent folder is superseded by this final build.

24 focused tests passed. Source and packaged no-hardware self-checks passed; the EXE accepted a valid synthetic report, rejected a tampered report, preserved a marked partial ZIP on cancellation, and dispatched a worker without exposing exception details. All 47 kit file hashes, source hashes against current working files, 22 local documentation links and ZIP bytes were verified. Build generation also checks its own ZIP against staged files. No existing app files were changed, staged or published.

Physical Windows USB/HID/XInput/BLE acceptance remains unperformed. Begin with a controlled X20 pilot, privacy inspection and a clean Windows machine; obtain Pro/other-model hardware evidence separately. Original HID report descriptors and official-app command captures remain explicitly separate advanced collection work.

## Volunteer simplification requested by Amjad

The new starting artifact is `artifacts/controller-scan-kit/simple-final/X20ctl-Volunteer-Kit.zip`. It has three short TXT guides and one numbered launcher at the top level; the executable/legal metadata live in `tool/`. The detailed research/source/advanced material is in the separate `X20ctl-Maintainer-Kit.zip`. `1_READ_ME_AMJAD.txt` provides the exact introductory message and first action for the maintainer.

Collector 1.0.1 defaults to one beginner discovery: printed model, everyday connection, explicit unplug/reconnect instructions, device confirmation, short human-readable review and local report ZIP. It does not ask for a technical mode, input source, button tests, optional BLE scan, additional sessions or free-form technical notes. Those operations remain available through `--advanced`. Beginner reports explicitly mark unrequested input/BLE stages and unknown mode; they do not assume hardware support. The launcher opens the results folder. No manufacturer button combinations, driver installs or app changes were added.

25 focused tests pass, including an end-to-end fixture that refuses any unexpected advanced collection call. Physical controller acceptance remains pending as before.

## Restored full guided kit and optional official-app experiment

Amjad requested all collection capabilities retained with one instruction TXT and no Markdown in the distribution. The full guided 1.0.2 kit restored intake, separate Windows/raw input rounds, optional Bluetooth checks and multiple sessions. Its 28 focused tests and packaged checks passed; the old simple and split-kit distributions are superseded.

Amjad then explicitly requested a guided EasySMX app capture step in the same kit. Version 1.0.3 adds a guided single-setting original/test/restore experiment, UTC action brackets, Android logging instructions, Windows Wireshark/USBPcap instructions, and bounded local capture import. The launcher offers a full run or app-capture-only run. Recordings are started/stopped by the contributor in Android or an existing capture tool; no drivers or apps are installed by this kit. Android bug-report ZIP import reads only one full BTSnoop log without exporting the full report. Text-only or ambiguous phone reports remain a recorded limitation.

Only explicitly approved raw traces are included. Captures are not anonymized; manifest privacy fields mark identifier presence unknown, and the review highlights this. Container validation does not prove target identity, decoded commands or actual support. All 41 focused tests pass, including complete app-only collection, privacy provenance, cancellation at the first prompt and mid-change, invalid/truncated captures and phone ZIP extraction. Physical controller/phone/Wireshark recording remains untested locally; Wireshark/USBPcap are not installed here.

Current distribution: artifacts/controller-scan-kit/app-capture-verified/X20ctl-Controller-Kit.zip. It retains one README.txt, START_HERE.bat, executable, source and extensionless legal notices. Preserve earlier artifacts and unrelated application changes.
