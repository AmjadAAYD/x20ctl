# X20CTL on Linux: user guide

X20CTL Studio for Linux is a separate app from the Windows one, built to look and work the same way. This guide covers installing it, the one permission PC mode needs, and what to do when something doesn't work.

**Status:** the Linux Studio has been built and checked on Windows only. It has not yet been run on a Linux machine. Everything below describes how it is designed to work. If something differs on your machine, please tell us (Feedback tab) and include your distribution.

## Install

Pick one from the release page. All are for 64-bit x86.

| Package | For | How |
|---|---|---|
| `X20CTL-<version>-linux-x86_64.AppImage` | Most distros | `chmod +x` the file and run it. |
| `X20CTL-<version>-linux-x64.tar.gz` | glibc distros (Ubuntu, Fedora, Arch, Debian, openSUSE) | Unpack, then run `./install.sh` (installs for your user only), or run `./x20ctl-studio` in place. |
| `X20CTL-<version>-linux-musl-x64.tar.gz` | musl distros (Alpine, Chimera) | Same as the tarball above. |

To remove a tarball install:

```bash
rm -r ~/.local/opt/x20ctl ~/.local/bin/x20ctl-studio ~/.local/share/applications/x20ctl.desktop
```

A Flatpak manifest exists in the repository, but it is untested and not part of releases. The AppImage keeps itself up to date (see Updates).

## Updates

Tools has a **Keep X20CTL up to date** switch, on by default. Each launch asks GitHub's public releases API whether a newer version exists.

- **AppImage:** the app downloads the new AppImage from the project's GitHub release, checks it against that release's `SHA256SUMS.txt`, and replaces itself. The new version starts the next time you open it. Nothing is uninstalled.
- **Tarball (glibc or musl):** the app tells you a newer version is out and opens the release page. Unpack the new tarball over the old one, or run its `install.sh` again.
- **Flatpak:** would update through Flatpak itself.

Turn the switch off to stop the check. This is new and has not run on a Linux machine yet.

## Your controller

The Studio finds any game controller the kernel exposes, whatever its make. Plug it in, or pair it in your system's Bluetooth settings first. Open the app, pick **Add controller** on a player slot, then **Enter Studio**.

Most tabs (Buttons, Curves, Macros, Vibration, Setups) edit a draft on this computer. They do not write to the controller.

## PC mode

PC mode reshapes your controller's input (remaps, curves, turbo, macros) and hands the result to games as a virtual Xbox 360 controller. It never writes to the controller itself.

### One-time permission

PC mode creates the virtual controller through `/dev/uinput`, which most distros don't let normal users write to. The PC mode tab shows the exact commands when it needs them. They are:

```bash
echo 'KERNEL=="uinput", SUBSYSTEM=="misc", TAG+="uaccess", OPTIONS+="static_node=uinput"' | sudo tee /etc/udev/rules.d/60-x20ctl-uinput.rules
sudo udevadm control --reload-rules && sudo udevadm trigger
```

Then unplug and replug the controller, or log out and back in. X20CTL never changes permissions by itself.

If the module isn't loaded, `sudo modprobe uinput` loads it (add `uinput` to `/etc/modules-load.d/` to keep it).

### The button check

Standard controllers (Xbox, PlayStation, Switch and most others) work straight away. Some generic pads report their buttons and sticks in an unusual order. PC mode won't take those until you have run **Check controller** once, so nothing gets mapped to the wrong button.

1. Open **PC mode**, find your pad under **Check your controller**, press **Check controller**.
2. Follow each instruction: press the named input, let go, wait for the next. It listens only.
3. It takes about a minute. Use **Skip this one** for an input your pad doesn't have.

If your pad isn't standard, the check also saves a small report in `~/X20CTLInputReports/`. Sending that file to the X20CTL team gets your model added for everyone.

### Turning it on

Switch **PC mode** on. Up to four controllers are supported, one profile each. While it's on, the real controller is held exclusively, so games see only the virtual one. Hold **View + Menu** on the pad to turn your settings off and on without leaving the game.

### Rumble

A game's rumble is passed on to your real controller's motors, when the controller has any. This is new and has not been tried on real hardware yet, so it may not work on every pad. Rumble also needs your user to be allowed to write to the controller's input device, which standard desktop setups allow.

## Controller as mouse

The **Mouse** tab turns a stick into the pointer. PC mode steps aside while the mouse is on. It uses the same `/dev/uinput` permission as PC mode. Hold the shortcut on the pad to switch it off and on.

## Where things are saved

| What | Where |
|---|---|
| PC mode settings | `~/.config/x20ctl/pc-mode.json` |
| Button layouts learned by the check | `~/.config/x20ctl/pad-layouts.json` |
| Check and scan reports | `~/X20CTLInputReports/` |

## Bluetooth

The Device tab's **Read controller** asks the pad over Bluetooth by running the project's Python client, read-only. It needs that client installed (`pip install git+https://github.com/AmjadAAYD/x20ctl`) and the pad paired in BlueZ. Writing settings to the controller over Bluetooth is not offered on Linux.

## Troubleshooting

| You see | Do this |
|---|---|
| "/dev/uinput isn't writable for you" | Do the one-time permission above. |
| "No controller found" | Check it shows up with `ls /dev/input/event*` and that your user can read it. Replug, or re-pair it. |
| "needs a quick button check first" | Run the button check on the PC mode tab. |
| "the kernel refused exclusive access" | Another program has the pad. Close it (a game, another remapper) and try again. |
| Controller works in PC mode but a game ignores it | Make sure the game isn't also reading the real pad. Steam Input can double up: turn Steam Input off for the game, or for the physical pad. |
| Rumble does nothing | The pad may have no rumble motors, or its input device isn't writable for your user. Say so in Feedback with the pad's name. |
| Not sure what to report | Open **Diagnostics → Copy report**, or use the Feedback tab. |

## What is and isn't verified

- Verified: the app builds and packs for Linux (glibc and musl) from Windows, its screens render, and the controller-layout logic and rumble byte layouts pass unit tests. The kernel constants and struct sizes were checked against the Linux headers.
- Not verified: running on a real Linux system, reading a real controller, the virtual controller being accepted by games, rumble, the AppImage launching, the Flatpak.
- Not supported: pads Linux doesn't expose through evdev at all (no driver), and writing settings to a controller over Bluetooth.
