# Desktop validation

This document records what was actually tested for x20ctl 3.0.0 and separates software evidence from hardware evidence.

## Packaged application acceptance

On September 19, 2026, the self-contained Windows x64 executable was launched with its bundled Microsoft WebView2 fallback forced on. The process exited with code 0 and its native-window smoke suite passed all 23 checks:

- loaded the bundled interface through the restricted native bridge;
- opened Buttons, Response curves, Macros, Vibration, Power & device, Input tester and Saved setups;
- verified minimum-window layouts and live metallic screenshots from every primary page;
- edited a macro draft, vibration, power, stick and trigger settings without writing hardware;
- saved a setup through the UI and read it back from isolated native storage;
- rejected an invalid profile without changing saved setups;
- ran real BLE discovery and displayed the result;
- rejected disconnected writes in both the interface and native API;
- rejected an unknown bridge operation;
- blocked external navigation from the embedded renderer;
- verified close-to-tray, hide, restore and quit behavior;
- verified macro chords, both stick directions, 5 ms timing, remapping and curve-guide keyboard behavior.

The accepted candidate was `dist/x20ctl.exe`, 332,176,864 bytes, with SHA-256:

```text
672175AA2BC88D109795362912C7AF0518F2E59A68AF907E24E1A598852F491F
```

This hash identifies the executable used for the screenshots below. `SHA256SUMS.txt` attached to the GitHub release is authoritative for the downloadable file.

## Screenshot provenance

The files in `assets/screenshots/desktop` are direct pixel captures of that running packaged executable's WebView2 surface. The smoke harness called WebView2's native capture API after navigating and interacting with the real interface. It also rejected blank captures using image variance checks.

No image generator, mock webpage, browser developer preview or compositing tool was used. The controller graphic visible inside the UI is a deliberately stylized interface illustration, not a photograph of a physical controller.

## Automated source validation

- 503 Python regression tests passed, including the restored protocol, profile, compatibility and desktop bridge coverage.
- 170 focused desktop, protocol, profile and compatibility tests passed.
- Frontend lint passed.
- Frontend curve tests passed.
- `npm test`, `npm run lint` and `npm run build` passed.

## Hardware boundary

The fresh v3.0.0 packaged run had no connected BLE configuration peripheral and no XInput gamepad. BLE discovery displayed the actual Windows adapter error (`The device is not ready for use`) and the UI stayed in a safe disconnected state. No physical configuration write or controller read-back was performed in this run.

The protocol engine retains its regression and simulated-transport coverage from the native 1.x application, but that is not presented as fresh physical-device evidence. Version 1.2.0 remains available as a rollback. Hardware-specific problems should be reported with the controller firmware, connection mode and the application log.

## Live controller follow-up (2.0.1)

Later on September 18, the user powered on the controller for a live test. The native service connected to Xpert2, firmware 9.01, and read remappings, both stick and trigger curves, motor strengths, idle timeout, M1-M4 and battery without warnings. Repeated reads were stable.

The timer was changed from 10 to 11 minutes through the desktop service's normal Apply operation, verified by controller read-back, then restored to 10 in a cleanup block. A full reread confirmed every settings category matched the initial snapshot. No reset or macro playback occurred.

This revealed a missed firmware case: M2-M4 returned neutral zero-duration macro entries. The 2.0.0 adapter turned them into invalid editable holds, preventing setup saves. The 2.0.1 adapter omits only entries with no input and no duration. Two regression tests demonstrated the failure before the fix and passed afterward, including preservation of a real initial delay.

The corrected source application was then launched in its actual Windows desktop window with isolated test storage. UI interaction connected the controller, read its settings, saved the setup, reread native storage and opened all six pages. The controller settings remained unchanged. These are source-window hardware checks, distinct from the packaged acceptance tests recorded above.

Live XInput sampling captured left-stick motion, D-pad, A, B, X, Select and Start. A further 30-second sample remained connected throughout but contained only neutral controls. Right-stick movement, trigger movement, unobserved buttons, remapping writes, curve writes and macro playback are not claimed tested.
