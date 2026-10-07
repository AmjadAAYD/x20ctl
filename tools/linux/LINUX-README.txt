x20ctl - native Linux desktop preview (x86-64)

Extract the complete archive, then double-click launch.sh or run:
  ./launch.sh
The ELF executable is ./x20ctl (Linux executables do not use .exe).
Keep the _internal directory and x20ctl-scanner beside it.
No Python, Node.js, Wine or Windows runtime is needed.

Build baseline: Ubuntu 22.04, glibc 2.35, GTK 3, WebKitGTK 4.1.
Requires an x86-64 Linux desktop with glibc >= 2.35 and these system libraries.
Other distributions, ARM devices, musl/Alpine, and Steam Deck have not been verified.

Ubuntu/Debian runtime packages:
  sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libgirepository-1.0-1 \
    libgdk-pixbuf-2.0-0 libcairo2 libffi8 libgl1 bluez
Fedora equivalents (untested):
  sudo dnf install gtk3 webkit2gtk4.1 gobject-introspection gdk-pixbuf2 cairo libffi mesa-libGL bluez

Use a normal desktop session, not sudo. If your file manager opens launch.sh
as text, choose Run as program or open a terminal in this folder.
If execute permissions were lost while copying:
  chmod +x launch.sh x20ctl x20ctl-scanner

X20 configuration uses the existing BLE protocol through the system BlueZ
service. Enable Bluetooth and allow the normal user to access that service.
Gameplay testing/recording uses Linux evdev (the gamepad's /dev/input/event
device must be readable in your logged-in desktop session). The app does not
change device permissions, grab input devices or install drivers.

X20 Pro, X05, X05 Pro, X10 and D10 remain UI-only previews. Selecting a model
does not connect hardware. No unverified commands have been added.

This is a local Linux preview, not a published release. Real Linux controller
connection, configuration writes and input mapping still require physical
hardware testing. No controller was accessed during the isolated build review.

Settings/logs: $XDG_DATA_HOME/x20ctl (default ~/.local/share/x20ctl).
If a tray icon is unavailable, closing the window exits the app.
Verify the paired scanner without opening a window:
  ./x20ctl --verify-scanner
