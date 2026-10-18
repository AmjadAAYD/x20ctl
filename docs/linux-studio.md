# Linux Studio

The Windows app is WPF, which is Windows-only. The Linux Studio is a separate Avalonia front end that reuses the shared, UI-free libraries.

## Sizes (measured)

| Project | Lines | Notes |
|---|---|---|
| X20Ctl.Zone | 5,428 | net10.0, no Windows dependencies: reused as-is |
| X20Ctl.Product | 17,087 | WPF, 88 of 98 files use `System.Windows`: the part to port |
| X20Ctl.Desktop | 685 | host glue |

## Status

Built and rendered on Windows only. Nothing has been run on Linux yet: there is no Linux machine on hand.

- Done: Controller Zone, controller chooser, and the Studio tabs Dashboard, Buttons, Curves, Macros, Vibration, PC mode, Device, Mouse, Setups, Tester and Diagnostics, on the shared `X20Ctl.Zone` drafts.
- The PC mode engine (evdev in, exclusive grab, uinput Xbox 360 out) and the controller-as-mouse engine are C# ports of the tested Python in `tools/linux_controller_check`. The ports themselves have not touched a real pad.
- Packaging is in `tools/linux-studio`: a tarball with an installer, an AppImage, and an untested Flatpak manifest. `release.yml` builds and attaches the first two. The binary is built for glibc; a musl distribution (Alpine, Chimera) needs a `linux-musl-x64` build.
- Also done: Tools, About, Feedback, Controller Check, Write scan, the intro (silent, simplified), the update popup at launch, tray icon, four-player PC mode with per-game profiles, macro recording and timeline preview, Test mapping, live curve input with a keyboard test, "Use as dead zone" from Diagnostics, and a game shelf (Steam, Heroic, Lutris, games added by hand).
- Not done: native Bluetooth (the Studio reads the pad by running the Python `x20` client, read-only; a C# BlueZ client is not written). Configuration writes over Bluetooth are not offered.

## Pieces that need a Linux equivalent

| Windows | Linux |
|---|---|
| XInput / native engine | evdev and hidraw (the existing `tools/linux_controller_check` scripts show the device paths) |
| ViGEmBus virtual pad | uinput virtual Xbox pad (`x20ctl-pcmode.py` already does this) |
| HidHide | exclusive grab of the real device (`EVIOCGRAB`) |
| Tray icon | StatusNotifierItem via Avalonia `TrayIcon` |
| Game library (Steam, Epic, GOG, Riot) | Steam and Heroic/Lutris library files |
| Updater | none: the package manager or AppImageUpdate does it |
| Intro video | Avalonia has no built-in video; use LibVLCSharp or a frame sequence |

## Order of work

1. Zone page (done as a spike), Choose controller, Studio shell with the tabs.
2. Buttons, Curves, Macros, Vibration, driven by `X20Ctl.Zone` drafts.
3. Input reading and the Tester and Diagnostics pages.
4. PC mode through uinput.
5. Packaging: AppImage and Flatpak, attached to the release next to the Windows exe.

## Packaging

- AppImage: `dotnet publish -r linux-x64 --self-contained`, then `appimagetool` over an AppDir.
- Flatpak: manifest on the `org.freedesktop.Platform` runtime with the dotnet extension; needs `--device=all` for evdev and uinput.
- `release.yml` gains an Ubuntu job for both.
