# X20CTL phone version: plan (draft for approval)

Status: Android APK confirmed by Amjad (2026-10-22). First version being built with sending locked. Written 2026-10-22 against public `main` at `87a72d4`.
Local-only work on Amjad's PC (`fix/intro-cover-crossfade`: `ff7f3bd`, `09ae340`, `ebc60ff`) is not in this checkout.
The phone work would branch from `main` and not touch the files that branch changes, so the two merge cleanly later.

## What I found in the code

- `desktop-dotnet/X20Ctl.Zone` (domain logic: setups, curves, macros, vibration drafts, Agent offline mode, KeyLinker codec, BTSnoop/ATT capture parsing, write-scan reports, diagnostics) targets plain `net10.0` and has **no Windows dependencies** (no WPF, no P/Invoke). It can be referenced from a phone app as is.
- `X20Ctl.Product` (WPF UI, XInput, HidHide, ViGEm PC mode, WinRT battery probe) is Windows-only and is **not** reusable on a phone. The UI must be rebuilt.
- The X20 configuration protocol is **BLE GATT** (service `d7f010e0-…`), recovered from the official **Android** app (KeyLinker / `com.pulsenet.inputset`). A phone is the controller's native configuration environment.
- Today the only working X20 BLE client is the Python `x20ctl/client.py` (bleak). The .NET product's own audit says: "BLE configuration reference; native transport not connected", writes locked. So a phone app would be the first .NET BLE transport.

## Recommendation

**Platform: Android first.** Android exposes BLE GATT and Bluetooth gamepads to apps and is where the official app runs. iOS blocks custom HID, only allows BLE to non-MFi devices in limited ways, needs a Mac and a paid developer account to install. iOS can come later if the BLE path proves out.

**Framework: .NET MAUI (Android target, `net10.0-android`).** It references `X20Ctl.Zone` directly, so setups, validation, codec and capture parsing are shared with the desktop instead of rewritten. Alternative (Kotlin/Compose) would mean re-implementing and re-verifying all domain logic; not recommended.

## MVP scope (V0 phone, "diagnostics first")

1. **Controller list and identity**: passive BLE scan; show name, MAC prefix match (`98:B6:E9/ED/EC`), RSSI. Label as "seen", never "supported".
2. **Input tester**: the controller paired as an Android gamepad; live buttons, sticks, triggers drawn on the existing X20 artwork. Uses the Zone `PadDiagnostics` / drift observation for deadzone and drift readout.
3. **Read scan (read-only)**: connect GATT, enumerate services, allowlisted reads only (Battery 0x180F and Device Information strings except the serial number). The KeyLinker service is reported present/absent only: reading X20 settings means writing request frames, so it waits for the write phase. No notification subscriptions (those are descriptor writes). Shared as plain text.
4. **Write scan on the phone**: import the phone's own Bluetooth HCI log (btsnoop) of KeyLinker changing one setting, decoded with the existing `BtSnoop` / `KeyLinkerAnalysis`. This is easier on a phone than on the PC.
5. **Setups (offline)**: moved to the next round, so the first APK can be tried sooner. "Send to controller" stays **locked**.
6. Local export only; nothing uploads unless the existing feedback/report path is explicitly used.

**Out of scope for MVP:** PC mode (virtual Xbox pad, HidHide, mouse mode: these are PC concepts and don't apply on a phone), the private Qwen/Ollama Agent, firmware/OTA (service `2de516f0-…` is never touched), writers for any model other than X20.

**Phase 2 (only after MVP and separate approval):** X20 write path ported from the Python client, gated behind the same evidence bar as desktop: matched reply, independent read-back, physical effect, power-cycle persistence, restore. Timeouts treated as indeterminate (re-read before retry).

## Project layout

- `desktop-dotnet/X20Ctl.Mobile/` MAUI app (Android), references `X20Ctl.Zone`.
- `desktop-dotnet/X20Ctl.Mobile.Core/` plain `net10.0` logic (BLE allowlist, advertisement labels, gamepad mapping, read scan text) and `X20Ctl.Mobile.Core.Tests/`.
- CI: `.github/workflows/android.yml` runs the phone logic tests and builds a Release APK signed with the SDK debug key (installable for testing, not a store build). No release/publish without approval.

## Success criteria (verifiable)

| # | Criterion | How it is checked |
|---|---|---|
| 1 | Existing `X20Ctl.Zone` builds and its tests still pass unchanged | `dotnet test X20Ctl.Zone.Tests` |
| 2 | Android APK builds | `dotnet build -f net10.0-android` in CI, APK artifact present |
| 3 | No GATT write is reachable in MVP | unit test on the transport allowlist + code search for write calls |
| 4 | OTA service never accessed | unit test: allowlist rejects `2de516f0-…` |
| 5 | Write-scan import decodes the existing sample btsnoop fixtures the same as desktop | shared fixture test |
| 6 | App launches and screens render | Android emulator screenshots (emulator has no real BLE) |
| 7 | Real phone + X20 behaviour | **Only you can check this**: install APK, run input tester and read scan. Not claimed until you report it. |

## What I can and can't verify from here

Checked 2026-10-22: this cloud environment has no .NET SDK, and its network policy refused `builds.dotnet.microsoft.com`, so I cannot install one here yet. Builds would run in GitHub Actions or on your PC, unless that host (plus `api.nuget.org` and the Android SDK hosts) is allowed in the environment's network settings. I cannot test with a real phone or controller. Everything hardware-related stays "untested" until you try it.
