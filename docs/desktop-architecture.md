# Desktop architecture

## Runtime

`app.py` starts `x20ctl.desktop.launcher`. pywebview creates a Windows WinForms/WebView2 window and serves only the compiled `dist-ui` assets on a loopback interface. The frontend is not hosted online. A navigation guard and content security policy restrict the renderer; only `DesktopApi.request` is exposed.

The API accepts named operations and bounded object payloads. A dedicated asyncio loop owns the BLE client. A lock serializes device operations; XInput reads are independent, read-only and sampled without overlapping frontend requests. UI success is conditional on the native result.

`settings.py` translates percentages and macro rows through the restored protocol, preserving curve flags. `service.py` owns discovery, handshake, reads, category writes, profiles and recording. Existing CLI/legacy GUI code remains for regression coverage; Qt is excluded from the new EXE.

Version 3.1.0 uses the installed Evergreen WebView2 Runtime; the fixed-version copy was removed from the EXE. If absent, a native dialog offers Microsoft's download page and Retry or Cancel. The app never downloads or installs it automatically.

The Controllers hub selects a model before opening a workspace. The native service records the active model, disconnects and clears discovery state on a switch, and refuses X20 profile, input and configuration operations while Pro is active. Pro actions cannot execute while X20 is selected. A distinct `%APPDATA%\x20ctl\desktop\x20_pro\profiles.json` is reserved for future Pro setups; its schema and writes are unavailable until hardware validation. The Pro adapter performs only BLE advertisement scans, GATT enumeration and allowlisted standard Battery/Device Information reads, plus Windows HID inventory.

## Profiles

Storage: `%APPDATA%\x20ctl\desktop\profiles.json`, atomically replaced only after all profiles validate. At most 100 setups. A malformed existing store is preserved and saving is blocked with an actionable error.

Schema version 2 has `id`, `name`, `createdAt`, `remaps`, `macros`, `macroLoops`, `vibration`, `idleTimeoutMinutes`, `stickCurves` and `triggerCurves`. Optional `categories` limits the apply scope of a migrated partial profile. Categories absent from a legacy file do not become implicit writes.

Curves use percentages for inner deadzone, outer saturation point, P1 and P2 coordinates. Macro rows have `id`, `buttons`, `leftStick`, `rightStick`, `durationMs` and `intervalMs`. Stick direction is 0 for neutral or 1-8 clockwise from up. A nonzero pause consumes an additional wire entry. Native validation, not TypeScript types, is the authority.

Imports receive fresh IDs and are validated before insertion. Legacy profile files are left untouched. Exports contain no device address or machine path. Windows file dialogs choose paths; frontend code cannot submit arbitrary paths.

## Evidence and limits

Strict desktop macro reads require valid CRCs and a complete declared record. An explicit empty record is distinct from no reply. Missing remap replies raise rather than manufacturing a stock layout. Clear/reset commands without reliable confirmation are reported as sent.

The input tester displays the first detected XInput slot, not a proven mapping between an XInput device and the selected BLE peripheral. With multiple controllers, verify the player label and the device you manipulate before recording.

The preview's monotone curve passes through the stored points. Exact firmware interpolation is unknown. XInput UI refresh is not a measurement of hardware report rate. Battery is a four-level estimate with incomplete physical validation.

## Building and capture

`tools/build_exe.py` installs locked frontend dependencies, type-checks/builds the UI, runs PyInstaller and rejects an EXE over 40 MiB. The release build uses the pinned Python environment in `requirements-build.txt`.

`x20ctl.exe --smoke-test DIRECTORY` uses isolated profile storage, interacts with the actual UI and writes a JSON report. Screenshots use WebView2 `CapturePreviewAsync` because Windows `PrintWindow` can return blank GPU surfaces. Captures are unretouched native client-area pixels; no generated hardware state or image-generation service is involved.
