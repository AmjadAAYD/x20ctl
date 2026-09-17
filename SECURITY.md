# Security Policy

## Reporting a Problem

Use **Security → Report a vulnerability** on this repository. That opens a private security advisory rather than a public issue.

This is an open-source project; disclosures will be reviewed, acknowledged, and addressed promptly.

## Supported Versions

Only the latest release is actively supported:

| Version | Supported | Notes |
|---|---|---|
| **2.0.0** | :white_check_mark: | Active release (Web & Desktop Suite) |
| < 2.0.0 | :x: | Legacy Python desktop scripts (deprecated) |

---

## Architecture & Hardware Safety

The controller hardware features two completely isolated command interfaces:

| Channel | Mechanism | Hardware Risk | Project Policy |
|---|---|---|---|
| **Bootloader** | USB mass storage / SCSI pass-through | **Can permanently brick device** | **NEVER TOUCHED** |
| **Configuration** | BLE GATT / KeyLinker protocol | Non-destructive & fully recoverable | **The ONLY target** |

There is **zero** code path from this project to the bootloader. The suite strictly utilizes standard Bluetooth Low Energy GATT and the browser-standard Gamepad API. No SCSI commands, no mass-storage flashing, and no low-level firmware flashing are implemented.

Any settings-level modification can be instantly cleared by holding the **`C` button** on the gamepad for 5 seconds to initiate a hardware factory reset.

---

## Verifying Release Artifacts

Every published executable and release artifact is cryptographically hashed with SHA-256 and attested via GitHub SLSA Provenance.

To verify your downloaded binary on Windows (PowerShell):
```powershell
Get-FileHash .\x20ctl.exe -Algorithm SHA256
```

To verify on Linux / macOS:
```bash
sha256sum x20ctl.exe
```

Expected hash for **2.0.0**:
```
a3f9e2b17c80459d8e12b77c590ef4a2c14589d701b2a95c32468f7b99c851de
```

If the SHA-256 hash does not match the published release notes, do not run the executable and report it immediately.

You can also run the suite directly from source:
```bash
git clone https://github.com/AmjadAAYD/x20ctl.git
cd x20ctl
npm install
npm run dev
```
