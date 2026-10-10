# Linux desktop preview

Local artifacts are in `dist/local-linux-webkit-20261002/`:

- `x20ctl-linux-x86_64.run`: single executable download. On Linux run `chmod +x x20ctl-linux-x86_64.run`, then `./x20ctl-linux-x86_64.run`. It extracts a private temporary bundle, runs the real ELF desktop application and removes that temporary bundle on exit.
- `x20ctl-linux-x86_64.tar.gz`: permanent folder alternative. Extract on Linux and run `./x20ctl-linux-x86_64/launch.sh`.
- `SHA256SUMS.txt`, `source-manifest.json`, `build-report.json`: identity and build evidence.

This is an x86-64 / glibc build based on Ubuntu 22.04 (glibc 2.35). GTK 3 and WebKitGTK 4.1 remain distribution-serviced system dependencies. See `tools/linux/LINUX-README.txt` for packages. No Python, Node or Wine installation is needed to run the packaged application. Other Linux distributions and ARM/musl systems are not verified.

## Changes

Windows retains WebView2, its named mutex and its tray behavior. Linux uses GTK/WebKit, a per-user file lock and XDG data storage. Closing the Linux window exits the application. The help text and input-source labels reflect the native platform.

Linux gameplay input uses evdev's standard gamepad button/axis mappings, normalizes stick/trigger ranges, supports analog/digital triggers and D-pad events, and handles inaccessible/disconnected devices. Discovery is deferred until X20 live input is requested. No device grabs, input injection, permission changes or vibration writes were added. The existing BLE protocol is unchanged. All five preview models retain their hardware-operation blocks.

The Linux scanner is built separately and its filename/hash embedded only into a temporary Linux build copy. The host checkout's scanner identity and existing Windows executables are preserved. Custom PyInstaller hooks include WebKit2/JavaScriptCore introspection data; a required-file assertion prevents silently falling back to a developer machine's installed bindings.

## Verification

- Windows focused Python tests: 210 passed, one Linux-only lock test skipped.
- Linux focused Python tests: 211 passed.
- Frontend type check and production build passed.
- Packaged ELF launched in an isolated Ubuntu 22.04 / Xvfb desktop. Verified the real native bridge, startup intro Skip, four-player landing, independent X20/Player 1 and X20 Pro/Player 2 assignments, rear macro navigation (M2/M6), rear-only macro editor, six preview Studios, matching rear-control counts, all model haptics zones, 0.4-second maximum animation advancing, shared slider fill/value, compact 1060×760 alignment, return/switch flow, and native blocking of foreign-document navigation.
- Repeated packaged acceptance in a clean Ubuntu runtime image with no Python or Node installed. Paired scanner integrity passed there too. Evidence: `artifacts/linux-clean-runtime-review/report.json`.
- The single `.run` launcher passed the same native acceptance in that clean environment. Its paired scanner check and temporary-folder cleanup passed. Evidence: `artifacts/linux-single-executable-review/report.json`. The final download is about 67.7 MiB.
- Nothing was released, pushed or published. Host screen and physical controllers were not accessed.

Physical Linux BLE pairing/configuration and real evdev gamepad mapping still need hardware testing. X20 Pro and the other preview protocols remain unverified. Windows native UI was not reopened during this Linux task; Windows source behavior was covered by the focused tests and its existing local executable was left intact.

Rebuild locally with `python tools/build_linux.py --docker` (Docker and the installed Node dependencies are required on the build machine). Use a fresh output directory for each build; the builder refuses to overwrite an existing bundle. The container reads an explicit source whitelist, has no host devices/system Bluetooth socket and runs the UI review without network access.
