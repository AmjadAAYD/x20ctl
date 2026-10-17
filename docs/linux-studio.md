# Linux Studio

The Windows app is WPF, which is Windows-only. The Linux Studio is a separate Avalonia front end that reuses the shared, UI-free libraries.

## Sizes (measured)

| Project | Lines | Notes |
|---|---|---|
| X20Ctl.Zone | 5,428 | net10.0, no Windows dependencies: reused as-is |
| X20Ctl.Product | 17,087 | WPF, 88 of 98 files use `System.Windows`: the part to port |
| X20Ctl.Desktop | 685 | host glue |

## Status

- `X20Ctl.Linux` (Avalonia 12.1.4) builds, starts on Windows, and publishes self-contained for `linux-x64`.
- It shows the Controller Zone with the X20 Pro. Nothing has been run on Linux yet: there is no Linux machine on hand.

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
