# Linux Studio

The Windows app uses WPF (Windows-only). **Linux Studio** (`desktop-dotnet/X20Ctl.Linux`) is a separate **Avalonia** desktop application for x64 Linux that reuses the UI-free core libraries in `X20Ctl.Zone` and `X20Ctl.Simulation`.

For installation, udev permissions, and troubleshooting, see [`docs/linux-user-guide.md`](linux-user-guide.md) and [`tools/linux-studio/FLATPAK.md`](../tools/linux-studio/FLATPAK.md).

## Current Status (V0.025)

Built, packaged, and headlessly rendered (`X20Ctl.Linux.Review`) on Windows and GitHub Actions CI (`ubuntu-latest` for `.flatpak` / `.AppImage` / tarballs). Live runtime execution on physical Linux desktop hardware and real `evdev` / `/dev/uinput` hardware has **not yet been verified** end-to-end.

- **Implemented Studio surfaces:** Zone switcher, controller chooser (all 7 EasySMX models), Dashboard, Buttons, Curves, Macros, Vibration, PC Mode, Device, Mouse, Setups, Tester, Diagnostics, Tools, About, Feedback, Controller Check, silent intro, update notifier, tray icon, 4-player per-game PC Mode profiles, macro recording/timeline, and local Steam/Heroic/Lutris game library detection.
- **PC Mode & rumble pass-through:** Exclusive `evdev` source routing (`EVIOCGRAB`) per player slot and virtual Xbox 360 output (`uinput`), with force-feedback rumble forwarding written from Linux kernel headers and verified via unit/review fixtures (awaiting physical pad verification).
- **Input ownership invariants:** Explicit per-player source assignments with stale-generation rejection, release barriers on capture/test/recording, and yielding while foreground UI or Mouse mode owns input (`X20Ctl.Linux.Review --input-ownership` verifies 21 invariants).
- **Intentionally silent:** Linux Studio packages no audio playback classes or sound files (`X20Ctl.Linux.Review` verifies `X20Ctl.Linux.IntroSound == null`).
- **Packaging (`tools/linux-studio` & `.github/workflows/release.yml`):** Automated release builds produce:
  - `X20CTL-<version>-linux-x86_64.AppImage` (with SHA-256 self-update against `SHA256SUMS.txt`)
  - `X20CTL-<version>-linux-x86_64.flatpak` (with sandboxed updater — see [`tools/linux-studio/FLATPAK.md`](../tools/linux-studio/FLATPAK.md))
  - `X20CTL-<version>-linux-x64.tar.gz` (glibc) and `X20CTL-<version>-linux-musl-x64.tar.gz` (musl)
  - `X20CTL-<version>-linux-check.zip` (`tools/linux_controller_check` standalone Python 3 scripts)
- **Not supported natively over C#:** Direct BlueZ configuration writes on Linux are not offered; read-only inspection on Device delegates to the `x20` Python client if installed and paired.

## Controller Layout Modes in PC Mode

Any gamepad exposed by Linux `evdev` is supported through `PadLayout`:

- **standard:** Standard Linux gamepad event codes (Xbox, PlayStation, Switch, etc.) work immediately.
- **guessed:** Non-standard Generic/DirectInput axis/button layouts require a one-time **Check controller** walkthrough in the PC Mode tab so controls map accurately before virtual output activates.
- **learned:** Saved per VID/PID in `~/.config/x20ctl/pad-layouts.json` (plus optional report in `~/X20CTLInputReports/`).
