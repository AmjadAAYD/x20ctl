# Security

Report vulnerabilities privately through this repository's **Security > Report a vulnerability** when available. Avoid posting controller identifiers or pairing information publicly. Response times are not guaranteed for this volunteer project.

## Trust boundaries

- Only bundled local UI is loaded in the desktop view. Unexpected navigation is blocked.
- The native bridge allowlists named operations. There is no shell, arbitrary Python evaluation, raw-packet or caller-selected filesystem-path endpoint.
- Profile file access uses Windows file dialogs. Imported values are validated in Python before storage or hardware use.
- Controller operations are serialized on one event loop. Invalid requests and disconnected writes fail closed.
- The UI reads XInput. It does not inject game input or pretend to measure controller polling frequency.
- Settings use the recovered KeyLinker configuration protocol. The app has no firmware-flashing workflow. This is not a guarantee that every configuration operation is risk-free.
- Optional updates query `api.github.com/repos/AmjadAAYD/x20ctl/releases/latest`. No controller telemetry or profiles are sent. Update checks can be disabled.

## Release verification

Download only from [this repository's releases](https://github.com/AmjadAAYD/x20ctl/releases). Compare the executable with the checksum file attached to that exact release:

```powershell
Get-FileHash .\x20ctl.exe -Algorithm SHA256
```

The executable is not Authenticode-signed by the project and no GitHub/SLSA attestation is claimed. A checksum verifies integrity against the published artifact, not safety. Treat antivirus findings seriously; do not disable protection based on a generic reassurance.

The full EXE includes a Microsoft-signed fixed WebView2 runtime as an offline fallback. The normal system runtime is preferred for serviced security updates. Maintainers must refresh the bundled fallback in subsequent releases. Source and build instructions are in the README.

## Scope of support

2.0 is the desktop recovery line. 1.2.0 remains available as a rollback. No promise of backported fixes, universal controller compatibility or zero bugs is made. Physical-device acceptance limitations are listed in [validation](docs/desktop-validation.md).
