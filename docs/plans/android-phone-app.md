# X20CTL on Android: plan

Status: revised 2026-10-22 after Amjad tried the first APK. The goal is the X20CTL app itself on a phone (intro, Controller
Zone, Studio, motion, features), not a separate scanner.

## Route

The Linux Studio (`desktop-dotnet/X20Ctl.Linux`) is already a faithful Avalonia copy of the Windows WPF app: the intro
film and its hand-over, the Controller Zone, the chooser, every Studio tab, Tools, About, Feedback, Controller Check and
Write scan, with the Windows app's motion values. Avalonia also runs on Android, so the phone app compiles those same
source files into an Android shell (`desktop-dotnet/X20Ctl.Android`) instead of rebuilding the screens.

The first scanner-only MAUI app is removed.

## What the Android shell adds

- `PhoneApp` / `PhoneHost`: the app view laid out 680 units tall and as wide as the screen's shape (at least 1100, the
  desktop window's minimum), scaled to fit. Landscape, full screen; Back goes one screen up, as Escape does.
- `ContentUnpacker`: Data/ (catalog, framing, vibration outlines) and Brand/ (intro film frames and sound) ship as APK
  assets and are copied into the app folder once per install.
- `PhoneSound`: the intro sound through Android's MediaPlayer.
- `PadInput`: a controller paired with the phone feeds the Studio's Tester, Diagnostics, Buttons test and live curve
  input (read-only gameplay input).
- Links open in the phone's browser.

## Small changes to the shared Linux code

Behaviour on Linux is unchanged: `MainWindow` now holds a `MainView` with the same content; bundled files are found
through `AppContent.Root`; platform hooks for sound, controller input and links; `Phone.Active` hides PC mode and
Mouse (a phone drives no virtual pad or pointer) and the desktop update offer.

## Limits

- Configuration writes stay locked, as on every platform except the verified X20 path. Reading the controller over
  Bluetooth on Linux runs the Python client; that does not exist on a phone, so Device and stored-macro reads say so.
- The Dashboard's game shelf reads PC game libraries and is empty on a phone.
- Screens are the desktop layout scaled down; text is small on a narrow phone.

## Success criteria

| # | Criterion | How it is checked |
|---|---|---|
| 1 | The shared screens still build and render after the refactor | CI: `X20Ctl.Linux.Review --shots` at 1430x680, screenshots uploaded |
| 2 | The APK builds | CI: `dotnet publish desktop-dotnet/X20Ctl.Android`, APK artifact present |
| 3 | Domain tests unchanged | CI: `dotnet test X20Ctl.Zone.Tests` |
| 4 | App starts with the intro and reaches the Zone and Studio on a real phone | Amjad installs the APK; not claimed until reported |
