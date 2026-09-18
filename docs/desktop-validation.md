# Desktop validation

This document records what was actually tested for x20ctl 2.0.0 and separates software evidence from hardware evidence.

## Packaged application acceptance

On September 18, 2026, the self-contained Windows x64 executable was launched with its bundled Microsoft WebView2 fallback forced on. The process exited successfully and its native-window smoke suite passed all 16 checks:

- loaded the bundled interface through the restricted native bridge;
- opened Buttons, Response curves, Macros, Vibration, Power & device and Input tester;
- edited a macro draft, vibration, power, stick and trigger settings without writing hardware;
- saved a setup through the UI and read it back from isolated native storage;
- rejected an invalid profile without changing saved setups;
- ran real BLE discovery and displayed the result;
- rejected disconnected writes in both the interface and native API;
- rejected an unknown bridge operation;
- blocked external navigation from the embedded renderer;
- verified close-to-tray, hide, restore and quit behavior.

The accepted candidate was `dist/x20ctl.exe`, 332,108,921 bytes, with SHA-256:

```text
DCD1847AC6D3E6707659AD78B840B5995071E8657F47647A11EA58FC6094978F
```

This hash identifies the executable used for the screenshots below. The published release hash may differ after final notice/version metadata is embedded; `SHA256SUMS.txt` attached to the release is authoritative for the downloadable file.

## Final release artifact

After embedding Windows file/product version 2.0.0, the complete third-party notice bundle and patched Pillow 12.3.0, the final 332,165,214-byte executable repeated the same 16-check acceptance run successfully. Its SHA-256 is:

```text
2FF0AECF0C6377521E4C3AB74A0A2D29EE691C566C919EB3760068666649D6DD
```

The final run used the bundled WebView2 fallback and exited with code 0. `SHA256SUMS.txt` attached to the GitHub release contains the same value.

## Screenshot provenance

The files in `assets/screenshots/desktop` are direct pixel captures of that running packaged executable's WebView2 surface. The smoke harness called WebView2's native capture API after navigating and interacting with the real interface. It also rejected blank captures using image variance checks.

No image generator, mock webpage, browser developer preview or compositing tool was used. The controller graphic visible inside the UI is a deliberately stylized interface illustration, not a photograph of a physical controller.

## Automated source validation

- 501 Python regression tests passed, including the restored protocol, profile, compatibility and desktop bridge coverage.
- 14 focused desktop tests passed in the isolated release environment.
- Frontend lint passed.
- Frontend curve tests passed.
- `npm audit` reported zero known vulnerabilities at validation time.

## Hardware boundary

BLE discovery found no active supported configuration peripheral during either packaged acceptance run. The screenshot-producing run detected no XInput gamepad; the final release run detected an XInput-compatible device but did not identify it as an EasySMX X20 or replay a macro. Neither run performed a physical configuration write or controller read-back.

The protocol engine retains its regression and simulated-transport coverage from the native 1.x application, but that is not presented as fresh physical-device evidence. Version 1.2.0 remains available as a rollback. Hardware-specific problems should be reported with the controller firmware, connection mode and the application log.

## Live controller follow-up (2.0.1)

Later on September 18, the user powered on the controller for a live test. The native service connected to Xpert2, firmware 9.01, and read remappings, both stick and trigger curves, motor strengths, idle timeout, M1-M4 and battery without warnings. Repeated reads were stable.

The timer was changed from 10 to 11 minutes through the desktop service's normal Apply operation, verified by controller read-back, then restored to 10 in a cleanup block. A full reread confirmed every settings category matched the initial snapshot. No reset or macro playback occurred.

This revealed a missed firmware case: M2-M4 returned neutral zero-duration macro entries. The 2.0.0 adapter turned them into invalid editable holds, preventing setup saves. The 2.0.1 adapter omits only entries with no input and no duration. Two regression tests demonstrated the failure before the fix and passed afterward, including preservation of a real initial delay.

The corrected source application was then launched in its actual Windows desktop window with isolated test storage. UI interaction connected the controller, read its settings, saved the setup, reread native storage and opened all six pages. The controller settings remained unchanged. These are source-window hardware checks, distinct from the packaged acceptance tests recorded above.

Live XInput sampling captured left-stick motion, D-pad, A, B, X, Select and Start. A further 30-second sample remained connected throughout but contained only neutral controls. Right-stick movement, trigger movement, unobserved buttons, remapping writes, curve writes and macro playback are not claimed tested.
