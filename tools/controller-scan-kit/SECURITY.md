# Scanner security policy

Scope: this standalone kit, not the X20ctl desktop app. No code path is intended to upload
data, modify firmware/configuration, install drivers, elevate, pair, subscribe to BLE
notifications, request serial strings, or record keyboard/mouse input.

Review `src/`, `CAPABILITIES.json` and tests. CI uses an import allowlist/API regression
guard, behavior tests for consent and privacy, bounded archive validation, and CodeQL.
These are useful evidence, not proof that all malicious behavior is impossible.

Unknown archives are validated without extraction: strict relative filenames, hashes,
entry/expanded/compressed size limits, no symlinks, duplicate names, encrypted entries,
ZIP64, nested ZIPs or executable payloads. An archive being structurally valid does not
mean its data is trustworthy or anonymous. Do not execute received files.

Official binaries are unsigned. Verify the SHA-256 and GitHub build attestation. Defender
must pass before publication; a clean antivirus result does not prove universal safety.
Do not disable protection to run the kit. Use the source path if preferred.

Report vulnerabilities privately through the repository owner's existing private contact
or GitHub private vulnerability reporting if enabled. Do not post real captures, identifiers,
credentials or full bug reports publicly. We do not claim private reporting is enabled
merely because this file exists.
