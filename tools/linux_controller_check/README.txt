X20CTL for Linux: Controller Check and PC mode
==============================================

Tests every button, stick and trigger of a controller on Linux, one guided step at a time, and saves a report you can
share. It only reads the controller; nothing on it is changed.

Run it
  1. Open a terminal in this folder.
  2. Run:   python3 x20ctl-check.py
  3. If it says it has no permission to read the controller, run it with sudo, or add yourself to the "input" group
     and log in again.

It works on Ubuntu, Debian, Mint, Pop!_OS, Fedora, Nobara, openSUSE, Arch, Garuda, Manjaro, EndeavourOS, SteamOS and
other distros with Python 3. Nothing else needs installing.

The report ZIP is saved in ~/X20CTLInputReports. If you agree at the start, a compact summary is also sent to the
X20CTL inbox; see https://github.com/AmjadAAYD/x20ctl/blob/main/PRIVACY.md for exactly what that contains.

PC mode (x20ctl-pcmode.py)
--------------------------
Remaps, turbo, stick and trigger curves and macros for any controller, applied on this PC. Games see one virtual
Xbox 360 controller instead of your real one; nothing is written to the controller.

  python3 x20ctl-pcmode.py set remap A B         A now acts as B
  python3 x20ctl-pcmode.py set turbo X 10        X repeats 10 times a second while held
  python3 x20ctl-pcmode.py show                  what's set
  python3 x20ctl-pcmode.py run                   start it; Ctrl+C stops it and gives the controller back
  python3 x20ctl-pcmode.py --help                everything else (curves, macros, learn, reset)

If your controller's buttons come out wrong, run  python3 x20ctl-pcmode.py learn  once to teach it.
"run" needs to write /dev/uinput; if it can't, it prints the one-time command that allows it.
Turbo and macros can get you banned in online games. You are responsible for the rules of the games you play.

X20CTL is free software under the GNU GPL v3 (see LICENSE and NOTICE). It is not affiliated with EasySMX.
