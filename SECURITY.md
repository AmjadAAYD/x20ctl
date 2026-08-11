# Security

## Reporting a problem

Use **Security → Report a vulnerability** on this repository. That opens a
private advisory rather than a public issue.

If you're not sure whether something counts, report it privately anyway and
I'll tell you. This is a personal project so I can't promise a response time,
but I'll acknowledge it.

## Supported versions

Only the latest release. Fixes go into a new release rather than being
backported.

## What this can and can't reach

The controller has two separate command channels and the difference matters
more than any bug in this code.

| Channel | Mechanism | Risk | Used here |
|---|---|---|---|
| Bootloader | USB mass storage, SCSI pass-through | can permanently brick the pad | never |
| Configuration | BLE GATT | recoverable by factory reset | yes, only this |

There is no code path from this project to the bootloader. It speaks BLE GATT to
the configuration service and nothing else. No `\\.\PHYSICALDRIVE`, no drive
letters, no SCSI, and it never enters upgrade mode. If you find a path that does
reach the bootloader, that is the most serious thing you could report.

Any settings mistake is undone by holding `C` for five seconds. Settings live
separately from firmware.

## Checking the executable

`x20ctl.exe` is a PyInstaller build of the source here. You can check what you
downloaded instead of trusting it.

Each release lists a SHA-256:

```powershell
Get-FileHash .\x20ctl.exe -Algorithm SHA256
```

If it doesn't match the release notes, don't run it, and tell me.

You can also skip the binary and run from source:

```bash
git clone https://github.com/AmjadAAYD/x20ctl.git
cd x20ctl
pip install -e ".[gui]"
x20ctl
```

`tools/build_exe.py` is the script that produces the released executable.

## Antivirus warnings

A one-file PyInstaller build bundles a Python interpreter and unpacks itself to
a temp folder when it starts. Several engines treat that as suspicious on its
own. The binary is also unsigned, since I don't have a code signing certificate,
which makes a warning more likely.

That doesn't prove anything either way, so don't take my word for it. Check the
hash, or build from source, or run it through VirusTotal and look at what the
engines actually claim to have found. If something reports specific behaviour
rather than a generic heuristic name, please tell me.

## Out of scope

- **Firmware flashing.** It goes through the mass storage bootloader, which is
  the one thing that can destroy the pad. Use the manufacturer's updater.
- **The vendor's app and firmware.** This documents an interoperable protocol.
  It doesn't distribute or modify vendor code.
- **Antivirus false positives** on the unsigned build, unless you have evidence
  of actual malicious behaviour rather than a heuristic detection name.
