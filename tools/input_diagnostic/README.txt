X20CTL Controller Scanner - local standalone build

Open X20CTL-Input-Scan.exe. No terminal or installation is needed. Windows x64 with .NET Framework 4.5 or newer is required.

1. Follow Welcome and Before you start. Use one connection method.
2. Select your connection context and printed model (Unknown is fine).
3. Detect devices. Review friendly groups and optional technical details.
4. Start the live check. Watch all four player slots and choose the one that responds.
5. Verify A + LT + RT. Wait for READY/countdown, release everything, press/release A, pull/release LT, then RT. The longer scan stays locked until this works.
6. Record next control only when ready. Buttons highlight, hold durations appear in milliseconds, and last durations remain after release. Skip absent controls.
7. If XInput does not respond, try Raw Input fallback with that window focused. HID buttons are descriptor-declared numbered usages, not guessed A/B/X/Y. Raw bytes are optional.
8. Save results. The ZIP and reviewable folder are under Documents\X20CTLInputReports. Review before sending the result ZIP. New source retains previous evidence before resetting. Closing saves incomplete evidence locally. Nothing is uploaded.

Privacy: interface paths can contain identifiers. Serial strings are opt-in. Raw Input excludes keyboard/mouse usages. No configuration, firmware, vibration commands or automatic sharing.

Limitations: original USB/HID descriptors, separate DirectInput enumeration and BLE GATT discovery are unavailable in this version. Parsed HID capabilities are not original descriptors. Motor capabilities are driver-reported, not physically verified. Rear tests record ordinary output only. Model/revision, calibration, sensor type, persistence and configuration support remain unverified. Host timing is not hardware latency or polling rate.

This uses a continuously running input reader, eliminating per-action worker startup. X05 Pro acceptance still requires the owner test.
