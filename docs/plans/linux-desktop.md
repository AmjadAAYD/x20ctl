# Native Linux desktop build

Build a local x86-64 Linux desktop bundle from the current working tree. Preserve the Windows executable/build path and all controller artwork. Nothing is published.

- Use Ubuntu 22.04 / glibc 2.35 as the build baseline, GTK 3 and WebKitGTK 4.1 for the native window. Include an executable folder and tar.gz with launch instructions; no Windows .exe emulation.
- Keep WebView2 and the Windows mutex on Windows. Add a Linux file lock, GTK navigation protection, graceful tray fallback and platform-specific startup diagnostics.
- Keep X20 BLE commands unchanged. Add read-only Linux gamepad input through evdev; do not grab devices, change permissions or generate rumble/input. All five preview models retain their hardware-operation blocks.
- Bundle the separately built, hash-pinned Linux scanner. Build inside Docker using an explicit source whitelist and a read-only source mount so Windows files and the scanner identity cannot be overwritten.
- Verify focused Python tests and the frontend build, then use Xvfb inside the container to check the real GTK desktop window and bridge, player assignment, rear macro views, haptics and navigation restrictions. No host input devices, Bluetooth socket or screen are exposed to the container.
- Document verified distributions and dependencies honestly. Physical BLE/input functionality requires later Linux hardware testing.
