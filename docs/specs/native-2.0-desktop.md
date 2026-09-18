# x20ctl 2.0 Native Desktop Specification

## Source and Status

- Status: approved by the user on 2026-09-17; implementation authorized.
- Execution amendment, 2026-09-18: user authorizes overnight autonomous testing, commit/push and 2.0.0 publication after validation, without waiting for the controller to be powered on. Fresh hardware-write acceptance remains explicitly unverified, not fabricated.
- Source: chat-approved product direction from 2026-09-17, repository evidence from remote `main` at `b626cdc`, and the last verified native baseline at `94cd363`.
- Product decision: preserve the visual direction of the React 2.0 interface, but ship x20ctl as a real downloadable Windows desktop application backed by the proven native controller implementation.
- Current behavior: `main` contains a React browser prototype, fabricated hardware state and diagnostics, an unused invented TypeScript protocol, misleading release documentation, synthetic marketing images, and a separate Tkinter mock backend added in `b626cdc`. It does not provide an end-to-end hardware configuration path.
- Target behavior: one Windows desktop application whose React interface communicates only through a narrow native Python bridge to verified x20ctl device, protocol, profile, input, and update services.

## Product Outcome

x20ctl 2.0 gives EasySMX X20 and confirmed-compatible KeyLinker-controller owners a polished desktop experience with the visual character of the current 2.0 prototype and the hardware reliability of the verified 1.2.0 implementation. Users download `x20ctl.exe` from GitHub Releases, run it without opening a browser or installing Node.js or Python, and can trust every displayed device value and successful-write message.

GitHub release assets remain the sole official binary distribution channel so release download counts remain meaningful and verifiable.

## Users and Workflows

### Controller owner

1. Downloads `x20ctl.exe` from a real GitHub release.
2. Launches a normal desktop window without a console or browser tab.
3. Sees a disconnected state until a supported controller is actually discovered.
4. Selects a discovered controller and connects through a transport supported by the restored native engine.
5. Reads current device configuration and hardware status before editing.
6. Changes remaps, curves, macros, vibration, or supported power settings.
7. Applies changes and receives success only after the native operation completes without error.
8. Tests real controller inputs and saves or loads local profiles.
9. Can recover supported settings through the existing guarded reset flow.

### Maintainer

1. Runs the Python protocol and service tests without hardware.
2. Runs frontend and bridge-contract tests with a deterministic fake backend explicitly limited to tests and development.
3. Builds the React assets and packages them with the Python application into a Windows executable.
4. Performs the hardware acceptance checklist before publishing a release.
5. Captures screenshots from the actual packaged application.
6. Publishes the verified executable, SHA-256 digest, release notes, and screenshots together.

## Scope

- Preserve and refine the current React 2.0 visual system, navigation, controller illustration, themes, responsive desktop layout, curve editor, macro editor, remapping surface, tester, and profile experience.
- Restore the verified Python package and protocol behavior from `94cd363` as the authoritative hardware foundation.
- Add a desktop shell that embeds the compiled React assets in a native application window.
- Add a narrow asynchronous JavaScript-to-Python API for controller discovery, connection, reads, writes, profile operations, tester state, and application lifecycle operations.
- Replace browser-owned hardware and persistence behavior with native services.
- Restore the established updater, diagnostics, packaging, test suite, CI, documentation boundaries, and controller compatibility statements where still applicable.
- Produce a downloadable Windows executable through a reproducible build path.
- Correct all 2.0 documentation and visual evidence before release.

## Non-Goals

- Hosting or promoting x20ctl as a public website.
- Reimplementing the KeyLinker protocol in TypeScript, Rust, or a new speculative Python module.
- Claiming macOS, Linux, ChromeOS, PWA, Web Bluetooth, WebHID, WebUSB, wired-configuration, or 2.4 GHz configuration support without separately verified implementations.
- Claiming support for the EasySMX X05 or any controller not confirmed by protocol evidence and physical testing.
- Firmware flashing, bootloader access, SCSI operations, or mass-storage writes.
- Publishing `v2.0.0`, executable hashes, attestations, screenshots, or performance numbers before the corresponding artifacts and evidence exist.
- Preserving the Tkinter mock application or its invented protocol from `b626cdc`.
- Treating visual interaction simulation as real hardware input outside an explicitly labeled developer/test mode.

## Domain Language and Invariants

- **Desktop shell:** the native window that loads packaged local React assets. It must not depend on a hosted site.
- **Bridge:** the restricted asynchronous interface between React and native Python services.
- **Device state:** information read from an actual connected controller or an explicit unavailable/unknown state. It is never fabricated.
- **Draft profile:** editable configuration in application memory or local storage that has not necessarily been written to hardware.
- **Applied configuration:** configuration whose native write operation completed successfully. If supported by the protocol, read-back is used to verify it.
- **Test backend:** deterministic non-hardware implementation used only by automated tests or an explicitly labeled development mode.
- The mature Python protocol implementation and its tests are authoritative. Frontend code never constructs controller packets.
- The application starts disconnected unless a real connection is restored and validated.
- Unknown values render as unknown or unavailable, never as plausible defaults.
- UI success states follow native completion; clicking a button alone cannot produce success.
- Destructive or reset operations require confirmation and expose recoverability guidance.
- The X05 remains unsupported unless new physical evidence proves otherwise.
- A release claim must point to a real artifact or reproducible observation.

## Required Behavior

### Desktop runtime and bridge

- The packaged application opens one native Windows window using local compiled assets.
- The production window cannot navigate to arbitrary remote content.
- The bridge exposes an allowlisted API rather than arbitrary Python evaluation, shell execution, filesystem access, or packet construction.
- Every bridge request returns a structured result with success state, data when applicable, and a user-safe error code/message.
- Long-running discovery, connect, read, write, and test operations do not block the UI thread.
- Window close, tray behavior, single-instance behavior, and update checks preserve the established native 1.2.0 expectations unless a later approved amendment changes them.

### Discovery and connection

- Discovery displays only devices returned by the real native transport layer.
- Connection controls accurately describe which transport is used for configuration and which connection is used only for live input testing.
- A connection becomes active only after the native client completes its handshake or equivalent validation.
- Disconnect events clear device-bound values and disable writes.
- Connection failures preserve the user's draft edits and provide actionable recovery guidance.

### Device state

- Name, model identifiers, battery, charging state, firmware/version bytes, transport, and configuration values come from supported native reads.
- Unsupported or unreadable fields show `Unknown` or `Unavailable` with no invented fallback.
- The interface distinguishes live device values from local profile values.

### Configuration editing and writes

- Remaps, stick curves, trigger zones/curves, macros, vibration, and power settings use the validation and encoding rules already proven by the restored Python engine.
- Unsupported targets such as the previously removed `C` and `T` remap destinations remain unavailable.
- Macro limits follow actual protocol capacity, including the established practical 47-entry boundary and supported eight-way stick representation.
- Apply operations send only changed, supported settings through the native client.
- Partial failure identifies which category failed and does not mark the whole profile as applied.
- Where reliable read-back exists, the application verifies the write before reporting completion.
- Where read-back does not exist, the interface reports `Sent` rather than claiming stronger verification.

### Profiles

- Profiles are stored through the native profile service in a user-appropriate application-data location.
- Existing supported 1.x profile files remain importable or receive an explicit migration error with no silent data loss.
- Import validates schema and values before changing active state.
- Export creates a portable, documented profile without secrets or machine-specific paths.
- Browser `localStorage` is not the production source of truth.

### Input tester and diagnostics

- Live buttons, sticks, and triggers come from the restored native input implementation.
- Clicking diagrams may select or explain controls but cannot masquerade as hardware input.
- Polling or timing metrics are displayed only when the measurement method supports them and are labeled with their actual meaning.
- No lower bound, default, or visual treatment may manufacture a favorable polling rate.
- Packet loss and latency are omitted unless a defensible measurement is implemented and tested.

### Updates and releases

- The desktop application checks the GitHub latest-release API in the background using the existing quiet, optional update behavior.
- Updates are offered, never forced.
- Release downloads link to the real GitHub release asset.
- The application version, Git tag, release title, binary version, README badge, changelog, and release notes agree.

### Documentation and screenshots

- `CHANGELOG.md` describes 2.0 as unreleased until acceptance and publication are complete.
- `RELEASES/2.0.0.md` is either an honest draft clearly marked unreleased or is generated/finalized only after the release artifact exists.
- Fake hashes, nonexistent download links, false attestations, unsupported platform claims, and fake performance claims are removed.
- Product screenshots are captured from the packaged executable at the release candidate commit.
- Concept art, if ever retained, is labeled as concept art and is not used in the release screenshots section.
- Contributor credit and prior protocol findings remain intact.

## Failure and Recovery Behavior

- No controller: show a calm disconnected state and a real rescan action; all device writes are disabled.
- Unsupported controller: identify it without implying configurability and link to compatibility guidance.
- Connection loss during a write: stop the operation, retain the draft, clear connected state, and state that the result is unknown unless read-back proves otherwise.
- Invalid imported profile: reject it without replacing existing profiles and explain the first actionable validation error.
- Native exception: convert it to a bounded bridge error, log diagnostic detail locally, and avoid exposing stack traces as normal UI text.
- Frontend load failure: show a native recovery message with diagnostic-log location rather than a blank window.
- Update-check failure: remain silent or non-blocking and continue normal operation.
- Build or release verification failure: do not publish or update release-facing documentation.

## Compatibility, Data, Privacy, and Security Constraints

- Initial supported product: Windows 10/11 x64 downloadable desktop executable.
- Initial hardware compatibility is limited to controllers supported by the verified restored Python protocol and transport code.
- The application operates locally. Controller data and profiles are not uploaded.
- Network access is limited to the documented optional GitHub update check unless a later approved feature adds another endpoint.
- The desktop view loads packaged local content and blocks unexpected remote navigation.
- Bridge input is schema-validated in Python before it reaches protocol code.
- No arbitrary command execution, arbitrary file access, dynamic Python evaluation, or generic raw-packet bridge endpoint is exposed to React.
- Logs exclude secrets and avoid unnecessary device identifiers.
- Builds use pinned Python and JavaScript dependency inputs and run in CI from a clean checkout.

## Acceptance Criteria

- Launching the release candidate opens a desktop window and does not open or require a browser tab, Node.js, or a Python installation.
- With no controller present, the application displays disconnected/unknown values and cannot report a successful apply.
- With a confirmed supported controller, discovery, connect, configuration reads, and disconnect behavior match the actual hardware state.
- At least one supported operation in each shipped configuration category is exercised on physical hardware and produces the expected controller behavior.
- Failed writes and interrupted connections never produce a success toast.
- Profiles survive application restart and valid 1.x profiles follow the documented compatibility behavior.
- Tester visuals respond to real controller input; pointer interaction cannot be mistaken for device input.
- The restored protocol/service suite and new bridge/frontend tests pass from a clean checkout.
- The executable is produced by the documented build command and launches successfully on a clean supported Windows environment.
- The release candidate's SHA-256 is calculated from the actual built artifact and matches the published value.
- All release-facing links resolve to existing GitHub objects.
- All published screenshots visibly correspond to the packaged release candidate.
- Repository-wide searches find no fabricated connection values, fake release hashes, false SLSA claims, unsupported X05 claims, or production simulation paths.

## Testing and Acceptance Strategy

- Restore and run the pre-migration Python unit and GUI/service tests as the regression baseline.
- Add contract tests for each bridge method, including validation, disconnected state, native exceptions, cancellation, and result serialization.
- Add frontend tests against a deterministic test adapter for disconnected, connecting, connected, write-success, write-failure, and disconnect-during-write states.
- Test profile migration, invalid imports, persistence, and round-trip export.
- Run TypeScript type checking, production asset build, Python tests, bridge tests, packaging, executable smoke launch, and link/document checks in CI where practical.
- Perform hardware acceptance using a written matrix tied to the exact release-candidate commit and device model.
- Inspect the packaged UI and capture release screenshots only after functional acceptance.
- Treat a successful static frontend build as necessary but insufficient evidence.

## Canonical Documentation Impact

- Rewrite `README.md` around the real desktop installation and verified capabilities.
- Reconcile `CHANGELOG.md` with an unreleased 2.0 development state and retain accurate historical entries.
- Replace or finalize `RELEASES/2.0.0.md` only at release readiness.
- Correct `SECURITY.md` supported-version, artifact, attestation, and safety statements.
- Update `IF-YOUR-CONTROLLER-ISNT-WORKING.md` for the native connection workflow.
- Keep `docs/00-findings.md` and `docs/01-protocol.md` authoritative where they reflect verified research; update only when implementation evidence requires it.
- Restore developer build and test instructions for the hybrid desktop architecture.

## Decisions and Rejected Alternatives

- Chosen: React presentation embedded in a Windows desktop shell with a narrow Python bridge and the restored mature Python backend. This preserves the approved 2.0 design while minimizing protocol and hardware regression risk.
- Rejected: public browser application. It conflicts with the downloadable-product requirement and cannot honestly provide the current native hardware surface.
- Rejected: the Tkinter mock application in `b626cdc`. It neither preserves the approved design nor performs real hardware writes.
- Rejected: rewriting the UI entirely in PyQt widgets. It would discard the approved React design and substantially extend delivery time.
- Rejected for this release: Tauri plus a Python sidecar. It adds Rust, a second executable lifecycle, and more packaging complexity without improving the required Windows outcome.
- Rejected: direct Web Bluetooth/WebHID protocol implementation. It duplicates proven logic and creates browser compatibility and trust problems.

## Assumptions and Residual Questions

- Technical default: use a maintained Windows webview wrapper with a restricted JavaScript/Python bridge, subject to a small packaging proof before full integration. `pywebview` is the leading candidate because it supports a two-way bridge without requiring a local HTTP API and documents PyInstaller packaging.
- Technical default: target Windows first. Other operating systems remain future work and receive no 2.0 compatibility claim.
- The exact subset of the 1.2.0 GUI modules retained versus replaced will be decided during planning; protocol, client, transport, profiles, input, diagnostics, updates, and their tests are preservation candidates.
- Physical hardware is required for final acceptance. Automated tests may use an explicit test adapter but cannot substitute for release hardware proof.
- `v2.0.0` remains unpublished until every applicable acceptance criterion passes.
