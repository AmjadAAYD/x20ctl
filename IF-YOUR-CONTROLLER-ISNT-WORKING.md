# Connection and recovery guide

## The input tester works, but settings do not

Gameplay and configuration are separate. USB or the receiver provides XInput; configuration uses Bluetooth LE. Enable PC Bluetooth, wake the X20 and select **Connect controller**. The separate configuration peripheral is normally named **Xpert2**.

Do not use a browser's Bluetooth dialog. This version discovers controllers through the native desktop app.

## Nothing appears in the scan

- Check Windows Bluetooth is enabled and the controller is awake and nearby.
- Disconnect a phone configuration app that may already hold the BLE connection.
- Rescan. If Windows reports an adapter error, correct that first.
- X05 is not compatible with this protocol. See [findings](docs/00-findings.md#7-other-controllers-checked).

## The app connects, but a category is unavailable

The controller may not expose that category, or its read may have timed out. The app reports the failed read. Reconnect or select **Read controller** before applying. Unread settings are not silently replaced with invented hardware values.

## A write failed

Read-back is checked where supported. A timeout or mismatch means success is not established. Earlier categories may already have applied, so review the status messages and reread the controller. Do not repeatedly reset or write blindly.

## The tester shows no input

Use an XInput-compatible gameplay mode. The app reads the first available Windows XInput slot and labels its player number. A BLE configuration connection alone does not provide gameplay input. Browser/HID-only devices are not automatically XInput-compatible.

## I closed the app, but it is still running

Window close hides it to the system tray. Choose **Open x20ctl** to return or **Quit** to exit and disconnect. A second launch directs you to the running instance.

## Startup or saved-profile error

Use the full `x20ctl.exe` release download. Give the self-contained executable time to unpack. Source and smaller development builds require WebView2 to be installed separately. Diagnostic logs are in `%LOCALAPPDATA%\x20ctl\desktop.log`.

Profiles are in `%APPDATA%\x20ctl\desktop\profiles.json`. If this file is malformed, the app preserves it and blocks overwriting it. Back it up before repairing it. Older profiles are still in `%APPDATA%\x20ctl\profiles` and can be imported.

## Reset and reporting

Factory reset is destructive to the controller's settings and requires confirmation. Saved local setups remain. If hardware controls are unusable, consult the manufacturer's instructions for your exact model rather than assuming a universal reset combination.

For a [bug report](https://github.com/AmjadAAYD/x20ctl/issues), include app version, Windows version, exact controller model, firmware if readable, steps to reproduce and the error shown. Remove addresses, serials and personal paths from logs. State separately whether gameplay input and BLE configuration work.
