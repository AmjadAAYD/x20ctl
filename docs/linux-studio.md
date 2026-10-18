# Linux Studio

The Windows app is WPF, which is Windows-only. The Linux Studio is a separate Avalonia front end that reuses the shared, UI-free libraries.

For installing and using it, see [linux-user-guide.md](linux-user-guide.md).

## Sizes (measured)

| Project | Lines | Notes |
|---|---|---|
| X20Ctl.Zone | 5,428 | net10.0, no Windows dependencies: reused as-is |
| X20Ctl.Product | 17,087 | WPF, 88 of 98 files use `System.Windows`: the part to port |
| X20Ctl.Desktop | 685 | host glue |

## Status

Built and rendered on Windows only. Nothing has been run on Linux yet: there is no Linux machine on hand.

- Done: Controller Zone, controller chooser, and the Studio tabs Dashboard, Buttons, Curves, Macros, Vibration, PC mode, Device, Mouse, Setups, Tester and Diagnostics, on the shared `X20Ctl.Zone` drafts.
- Written, not yet run on a real pad: rumble pass-through in PC mode. A game's rumble on the virtual Xbox pad (uinput force feedback) is forwarded to the real pad's own motors, if it has any. Needs the event device to be writable; a pad without rumble is simply left alone.
- The PC mode engine (evdev in, exclusive grab, uinput Xbox 360 out) and the controller-as-mouse engine are C# ports of the tested Python in `tools/linux_controller_check`. The ports themselves have not touched a real pad.
- Packaging is in `tools/linux-studio`: a tarball with an installer, an AppImage, and an untested Flatpak manifest. `release.yml` builds and attaches the first two. The binary is built for glibc; a musl distribution (Alpine, Chimera) needs a `linux-musl-x64` build.
- Also done: Tools, About, Feedback, Controller Check, Write scan, the intro (silent, simplified), the update popup at launch, tray icon, four-player PC mode with per-game profiles, macro recording and timeline preview, Test mapping, live curve input with a keyboard test, "Use as dead zone" from Diagnostics, and a game shelf (Steam, Heroic, Lutris, games added by hand).
- Not done: native Bluetooth (the Studio reads the pad by running the Python `x20` client, read-only; a C# BlueZ client is not written). Configuration writes over Bluetooth are not offered.

## Which controllers PC mode takes

Any controller the kernel exposes as a gamepad, whatever its make: judged by what it reports (axes plus gamepad or joystick buttons), not by name. Each pad is read through a layout (`PadLayout.cs`):

- **standard**: the kernel's gamepad layout (Xbox, PlayStation, Switch and most others). Used straight away.
- **guessed**: worked out from what the pad reports (generic DirectInput pads, odd stick axes, triggers that rest high). PC mode will not take such a pad until its owner runs the button check once, so nothing is mapped to the wrong button.
- **learned**: taught by the check (PC mode tab, Check your controller). Saved per vendor and product in `pad-layouts.json`.

The check asks for each input in turn, listens only, and saves what it learned. When a pad isn't standard it also writes `~/X20CTLInputReports/layout-<vendor>-<product>.json` (what the pad reports and what it sent), which can be sent to add the model for everyone.

Not covered: a pad Linux doesn't expose through evdev at all (no driver), and a user without write access to `/dev/uinput`.

## Pieces that need a Linux equivalent

| Windows | Linux |
|---|---|
| XInput / native engine | evdev and hidraw (the existing `tools/linux_controller_check` scripts show the device paths) |
| ViGEmBus virtual pad | uinput virtual Xbox pad (`x20ctl-pcmode.py` already does this) |
| HidHide | exclusive grab of the real device (`EVIOCGRAB`) |
| Tray icon | StatusNotifierItem via Avalonia `TrayIcon` |
| Game library (Steam, Epic, GOG, Riot) | Steam and Heroic/Lutris library files |
| Updater | the AppImage replaces itself from the GitHub release (checked against `SHA256SUMS.txt`); other packages are pointed to the release page |
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
