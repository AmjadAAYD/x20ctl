# X20CTL on Linux: User Guide

<p><img src="../assets/readme/header-linux.svg" alt="X20CTL Linux User Guide header" width="100%"></p>

X20CTL Studio for Linux mirrors the Windows Studio using Avalonia (`linux-x64`). This guide covers installation, the one-time `/dev/uinput` rule for PC Mode, and known limitations.

> **Verification status:** Linux Studio packages are built automatically on CI and headlessly rendered/verified (`X20Ctl.Linux.Review`), but live runtime behavior on physical Linux hardware (AppImage, Flatpak, `evdev`/`uinput`, and rumble forwarding) has not yet been tested end-to-end. Please report Linux distribution findings via **Feedback** or GitHub Issues.

## Install (64-bit x86 Linux)

All Linux bundles are published on the [GitHub Releases](https://github.com/AmjadAAYD/x20ctl/releases/latest) page with `SHA256SUMS.txt`:

| Package | For | How to run / install |
|---|---|---|
| `X20CTL-<version>-linux-x86_64.AppImage` | Most distributions | `chmod +x X20CTL-*.AppImage && ./X20CTL-*.AppImage` |
| `X20CTL-<version>-linux-x86_64.flatpak` | Flatpak-enabled distros | `flatpak install --user ./X20CTL-*.flatpak && flatpak run --user io.github.AmjadAAYD.x20ctl` (see [`FLATPAK.md`](../tools/linux-studio/FLATPAK.md)) |
| `X20CTL-<version>-linux-x64.tar.gz` | glibc distros (Ubuntu, Fedora, Arch, Debian, openSUSE) | Extract, then run `./install.sh` (per-user install) or `./x20ctl-studio` |
| `X20CTL-<version>-linux-musl-x64.tar.gz` | musl distros (Alpine, Chimera) | Extract, then run `./install.sh` or `./x20ctl-studio` |
| `X20CTL-<version>-linux-check.zip` | Headless Python 3 CLI | Extract and run `x20ctl-check.py` / `x20ctl-pcmode.py` |

To uninstall a per-user tarball install:

```bash
rm -rf ~/.local/opt/x20ctl ~/.local/bin/x20ctl-studio ~/.local/share/applications/x20ctl.desktop
```

## Updates

**Tools -> Keep X20CTL up to date** is on by default and queries GitHub releases for stable (non-prerelease) updates:

- **AppImage:** Downloads the new AppImage, checks `SHA256SUMS.txt`, and stages replacement for next launch.
- **Flatpak:** Downloads into user cache, checks `SHA256SUMS.txt`, and invokes host `flatpak install --user --or-update --bundle` (see permission details in [`tools/linux-studio/FLATPAK.md`](../tools/linux-studio/FLATPAK.md)).
- **Tarballs:** Notifies you of a new version and links to the release page.

## Your controller & PC Mode

1. Plug in your controller or pair it in your desktop Bluetooth settings.
2. Open X20CTL, click **Add controller** on a player slot, then **Enter Studio**.
3. On the **PC mode** tab, choose that player's **input source** explicitly (`evdev` sources are session-only and never fall back silently to a different pad).

PC Mode reshapes your buttons, curves, macros, turbo, and mouse pointer on this computer and outputs a virtual Xbox 360 gamepad via `/dev/uinput`. It never alters controller firmware or onboard memory.

### One-time `/dev/uinput` udev rule

Most distributions restrict `/dev/uinput` to root. Grant your local desktop user access once:

```bash
echo 'KERNEL=="uinput", SUBSYSTEM=="misc", TAG+="uaccess", OPTIONS+="static_node=uinput"' | sudo tee /etc/udev/rules.d/60-x20ctl-uinput.rules
sudo udevadm control --reload-rules && sudo udevadm trigger
```

Then replug your controller or log out and back in. If `uinput` is not loaded: `sudo modprobe uinput`.

### Button check & rumble

- Standard controllers (Xbox, PlayStation, Switch, EasySMX XInput) work immediately. Non-standard DirectInput pads prompt for a guided **Check controller** walkthrough first so axes and buttons align cleanly (`~/.config/x20ctl/pad-layouts.json`).
- Hold **View + Menu** on your controller to toggle PC Mode on/off in-game, or **View + Y** to toggle Mouse mode.
- Game force-feedback rumble forwarding requires write access to your pad's `/dev/input/event*` node (standard on modern desktop sessions) and hardware rumble support.

## Local config paths & audio note

- **Config & learned layouts:** `~/.config/x20ctl/pc-mode.json`, `~/.config/x20ctl/pad-layouts.json`
- **Scan reports:** `~/X20CTLInputReports/`
- **Bluetooth read-only inspection:** The Device tab's **Read controller** invokes the Python `x20` CLI if installed and paired via BlueZ; writing settings to controller flash over Bluetooth is not supported on Linux.
- **Audio policy:** Linux Studio is intentionally silent (no UI sound effects or audio assets are packaged).

## Troubleshooting

| Symptom | Fix |
|---|---|
| `"/dev/uinput isn't writable for you"` | Apply the one-time udev rule above and replug or relogin. |
| `"No controller found"` | Verify `ls /dev/input/event*` shows your controller and your user can read it; switch wireless receiver pads to XInput mode. |
| `"needs a quick button check first"` | Complete **Check controller** on the PC Mode tab. |
| `"the kernel refused exclusive access"` | Close another remapper, game, or Steam Input client grabbing the pad. |
| Double input in games | Disable Steam Input for the physical pad while PC Mode is active. |
| Need bug report details | Use **Diagnostics -> Copy report** or open **Feedback** (see [`linux-studio.md`](linux-studio.md)). |
