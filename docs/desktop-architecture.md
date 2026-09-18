# Desktop architecture

## Runtime

`app.py` starts `x20ctl.desktop.launcher`. pywebview creates a Windows WinForms/WebView2 window and serves only the compiled `dist-ui` assets on a loopback interface. The frontend is not hosted online. A navigation guard and content security policy restrict the renderer; only `DesktopApi.request` is exposed.

The API accepts named operations and bounded object payloads. A dedicated asyncio loop owns the BLE client. A lock serializes device operations; XInput reads are independent, read-only and sampled without overlapping frontend requests. UI success is conditional on the native result.

`settings.py` translates percentages and macro rows through the restored protocol, preserving curve flags. `service.py` owns discovery, handshake, reads, category writes, profiles and recording. Existing CLI/legacy GUI code remains for regression coverage; Qt is excluded from the new EXE.

The normal system WebView2 runtime is preferred. The full EXE includes an app-private fixed-version fallback. No renderer installation or registry change is made. Windows 10 requires read/execute permissions on that extracted runtime directory per Microsoft's documented deployment rules; no unrelated directory is modified.

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

`tools/build_exe.py --bundled-runtime` installs the locked frontend dependencies, type-checks/builds the UI, verifies Microsoft's pinned runtime download/hash/signature, and runs PyInstaller. The release build uses the pinned Python environment in `requirements-build.txt`.

`x20ctl.exe --smoke-test DIRECTORY --bundled-runtime` forces the included renderer, uses isolated profile storage, interacts with the actual UI and writes a JSON report. Screenshots use WebView2 `CapturePreviewAsync` because Windows `PrintWindow` can return blank GPU surfaces. Captures are unretouched native client-area pixels; no generated hardware state or image-generation service is involved.
