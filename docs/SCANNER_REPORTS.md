# Controller compatibility reports

The desktop app creates reports for an explicitly selected Bluetooth LE peripheral. For the X20 workspace, connect the controller first; X20ctl disconnects its configuration session before launching the helper so the helper can read that same address. For the Pro workspace, select a row returned by the current BLE discovery. A row is an observed peripheral, not proof of its model. If the selected device cannot be reached or standard GATT characteristics are unavailable, the scan fails locally. The scanner never broadens to nearby devices or Windows HID inventory.

## Trust and data flow

1. The packaged `x20ctl.exe` has the helper version, filename, and SHA-256 embedded at build time. It hashes the complete adjacent `x20ctl-scanner.exe` before every launch. A missing, changed, linked, or unreadable helper fails closed. Source checkouts deliberately have no approved hash.
2. X20ctl creates `%LOCALAPPDATA%\x20ctl\Reports\<random-id>\output` and passes that directory and the selected BLE address as structured process arguments. The helper connects only to that address, reads only allowlisted standard GATT values, and writes diagnostic files there. It has no upload implementation.
3. X20ctl validates the files and sizes, writes `system.json` with informational provenance, then creates the ZIP. USB descriptors, HID descriptors, VID/PID, and input captures remain unavailable unless they can be associated safely with the selected controller in a future release. No serial number is collected.
4. The user previews the controller name, VID/PID availability, app and scanner versions, files, size, and diagnostic details. A separate consent checkbox enables Send. The desktop app alone posts the ZIP to the public X20ADMIN report endpoint. X20ADMIN must continue validating it as untrusted input.

Successful local packages remain under Reports. On an upload error, the original `report.zip` and UUIDv4 `clientSubmissionId` remain there. Retry sends those same bytes, UUID, and metadata; it does not regenerate the ZIP. The app also rechecks the current helper hash before sending. The user can reopen an unsent local report from either workspace.

## Release build

On Windows, run `python tools/build_exe.py` with the project's build dependencies installed. It builds the dedicated scanner first in `dist/<version>/`, hashes its actual bytes, embeds that value in the host build, and restores the source placeholder. The build creates `dist/x20ctl-<version>-win-x64.zip` with both executables, extracts it to a temporary directory, then invokes the packaged host's `--verify-scanner` check against the adjacent helper. A mismatch fails the build. Scanner changes require a new official paired release; neither runtime state nor a sidecar file can approve a new hash. Normal users need neither Python nor a terminal.

## Limits

SHA-256 proves equality to the helper bytes approved by this host build. It does not establish publisher identity or protect a host executable that an attacker can patch. The hash and launch are separate Windows operations; another process with write access to the installation directory could replace the helper between them. Keeping the pair in a non-user-writable installation directory and adding Authenticode publisher verification in a future signed release would reduce that risk. No administrator rights are requested by this workflow.

The current scanner is Bluetooth LE only. Report provenance fields are client claims for troubleshooting, not server trust evidence. Hardware collection, the installed Windows launch path, and live X20ADMIN acceptance require final manual verification on the target hardware and release package.
