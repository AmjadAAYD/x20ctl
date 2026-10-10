# Changes

## 1.1.0

- Publish the supplied 1.0.3 scanner source in the X20ctl repository as a standalone tool.
- Add hardware-free `--capabilities`, `--privacy` and `--dry-run` commands.
- Keep raw settings-app capture in the explicit `--app-capture` mode.
- Cancellation no longer creates an unreviewed ZIP.
- Raw HID manifests disclose possible identifiers; validation requires those flags.
- Extend metadata text redaction for UNC and device-instance paths.
- Add consent/privacy/archive/USB/BLE/policy tests and scoped security guards.
- Add hash-pinned Windows runtime/build dependencies and source execution instructions.
- Build scanner releases on GitHub-hosted Windows with CodeQL, packaged checks,
  Defender, source/build metadata, checksums, a scoped CycloneDX SBOM and provenance.

Existing controller research/read-only behavior is retained. No firmware/configuration
write support or physical-controller acceptance is claimed by this release.
