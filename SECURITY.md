# Security

## Reporting a vulnerability

Use GitHub's private reporting: **Security → Report a vulnerability** on this
repository. That opens a private advisory only you and I can see, so nothing is
disclosed while it's being fixed.

Please don't open a public issue for a security problem. If you're unsure
whether something qualifies, report it privately anyway and I'll say so.

Expect an acknowledgement within a few days. This is a personal project, not a
staffed one, so there's no formal response-time commitment beyond that.

## Supported versions

| Version | Supported |
|---|---|
| latest release | yes |
| anything older | no, upgrade first |

Fixes go into a new release rather than being backported.

## What this project can and can't touch

The controller has two entirely separate command channels, and the distinction
matters more here than any bug in this code:

| Channel | Mechanism | Risk | This project |
|---|---|---|---|
| Bootloader | USB mass storage, SCSI pass-through | **can permanently brick the device** | never touched, at all |
| Configuration | BLE GATT | recoverable by factory reset | the only target |

There is no code path from this software to the bootloader. It speaks BLE GATT
to the configuration service and nothing else: no `\\.\PHYSICALDRIVE`, no drive
letters, no SCSI, and it never enters upgrade mode. If you find a path that
reaches the bootloader, that is the most serious thing you could report.

Any settings-level mistake is recoverable by **holding `C` for five seconds**
for a factory reset. Settings live separately from firmware.

## Verifying the executable

`x20ctl.exe` is a PyInstaller one-file build of the source in this repository.
You do not have to trust it — you can check what you downloaded, and you can
build your own.

**Check the file you downloaded matches the one that was published.** Each
release lists a SHA-256. In PowerShell:

```powershell
Get-FileHash .\x20ctl.exe -Algorithm SHA256
```

If that hash doesn't match the one in the release notes, don't run it, and tell
me.

**Or build it yourself and skip the binary entirely:**

```bash
git clone https://github.com/AmjadAAYD/x20ctl.git
cd x20ctl
pip install -e ".[gui]"
x20ctl
```

That runs the same application from source. `tools/build_exe.py` is the script
that produces the released executable, and it isn't doing anything clever.

## On antivirus warnings

A PyInstaller one-file executable bundles a Python interpreter and unpacks
itself to a temporary directory at startup. Several antivirus engines treat
that shape as suspicious on its own, so a heuristic flag on this file is
common and is not by itself evidence of anything. The binary is unsigned,
because code-signing certificates cost money this project doesn't have, which
also raises the odds of a warning.

That said, **"it's probably a false positive" is not proof, and you shouldn't
take my word for it.** Verify the hash, or build from source, or run the file
through [VirusTotal](https://www.virustotal.com/) yourself and look at which
engines complain and what they claim to have found. If a scanner reports
something specific rather than a generic heuristic name, please report it — I'd
want to know.

## Out of scope

- **Firmware flashing**, deliberately. It runs through the mass-storage
  bootloader, the one path that can destroy the controller. Use the
  manufacturer's own updater.
- **The vendor's Android application and firmware.** This project documents an
  interoperable protocol; it doesn't distribute or modify vendor code.
- **Antivirus false positives** on the unsigned build, unless you have specific
  evidence of actual malicious behaviour rather than a heuristic detection name.
