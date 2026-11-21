# Security Policy

## Scope

The active release line is **V0.025**. Older v1/v2 preview lines are superseded and do not receive backports.

## Reporting a vulnerability

If you find a security issue, unsafe privilege boundary, credential/data leak, or malformed-archive handling bug:

1. **Do not** open a public GitHub issue with exploit payloads or private device captures.
2. Report it privately through GitHub **Security Advisories** on [`AmjadAAYD/x20ctl`](https://github.com/AmjadAAYD/x20ctl/security/advisories), or contact the maintainer privately first.
3. Include reproduction steps, affected component (Windows Studio, Linux Studio, Python CLI, updater, or Controller Scan Kit), and any crash/diagnostic excerpt with personal usernames or serials removed.

## Release verification & runtime boundaries

- **Build provenance & checksums:** Official release artifacts are built cleanly from source in GitHub Actions (`.github/workflows/release.yml`) and published with `SHA256SUMS.txt` covering both Windows (`-windows-x64-setup.exe`, `-windows-x64.exe`) and Linux (`.AppImage`, `.flatpak`, `-linux-x64.tar.gz`, `-linux-musl-x64.tar.gz`, `-linux-check.zip`) assets.
- **Hardware write safety:** Native configuration writes are restricted to explicitly confirmed EasySMX X20 / X20 Pro paths and verify model identity before applying. X20CTL **never flashes firmware**.
- **PC Mode drivers & permissions:** On Windows, virtual controller output uses user-approved installation of pinned `ViGEmBus` (`1.22.0`) and `HidHide` (`1.5.230`), with SHA-256 verification before launch. On Linux, PC Mode uses `/dev/uinput` (`uinput` + `evdev`) with standard user udev rules and never auto-elevates.
- **Offline Agent & privacy:** Handheld Agent XCTL executes locally on your device without external network requests, telemetry, or remote LLM calls.
- **No warranty on universal hardware behavior:** Physical-device verification status for each model is documented transparently in [`README.md`](README.md) and [`CHANGELOG.md`](CHANGELOG.md).
