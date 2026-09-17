---
status: active
feature: native-2.0-desktop
specification: ../specs/native-2.0-desktop.md
base_commit: 4bb2560
git_policy: required
execution_mode: continuous
current_checkpoint: CP-01
checkpoint_status: In Progress
lifecycle_owner: Plumbline Execute
last_verified_commit: 4bb2560
next_safe_action: Restore the proven backend and build a local desktop bridge.
delegation_roles: Direct - no project-local roles configured
delegation_status: direct
ready_for_acceptance: false
---

# Native 2.0 implementation

The approved specification controls product behavior. Preserve React's visual direction, restore the verified Python engine, and deliver a locally packaged Windows application. No remote publication is authorized at this stage.

## CP-01: Desktop and hardware foundation

Status: In Progress. Restore `x20ctl`, tests, tools and packaging metadata from `94cd363`; retain the legacy GUI as regression evidence. Introduce the desktop launcher and restricted asynchronous bridge. Validate disconnected startup, API validation, device identity, serialization, connection loss and shutdown. Join condition: Python regression/bridge tests pass and local desktop assets load.

## CP-02: Real configuration and polished UI

Status: Pending. Depends on CP-01. Wire discovery, hardware reads, category-scoped writes, macros, curves, profiles, live inputs and reset to native services. Remove fabricated data and simulations. Keep errors and partial writes visible. Improve layout while retaining the dark visual style, controller diagram and curve/macro editors. Proof: frontend tests, bridge failure scenarios and actual window interaction.

## CP-03: Packaged application and truthful evidence

Status: Pending. Depends on CP-02. Package the executable, launch it, exercise disconnected flows and available hardware, capture real window screenshots, reconcile README/changelog/security/release draft and restore CI. Proof: executable launch, screenshot inspection, artifact hash, full tests and documented hardware acceptance status.

## Risk and proof

| Scenario | Required proof |
|---|---|
| No device or lost device | No success or fabricated telemetry; writes fail closed |
| Invalid settings | Reject the entire requested category before any device write |
| Write/read-back mismatch | Error with draft retained; never report verified |
| Native API concurrency | One event-loop owner and serialized device operations |
| Profile persistence | Validated data, atomic save, 1.x import compatibility |
| Desktop content boundary | Only bundled local UI; allowlisted API; no raw packets or shell bridge |
| Packaging | Launch exact built artifact; no Python/Node/browser prerequisite |
| Public claims | Screenshots from real window; hashes from artifact; 2.0 remains unreleased |

## Residuals

- Acceptance blocker: physical-controller proof depends on available supported hardware. Safe discovery and reads may run; hardware writes for acceptance require a connected user-owned test device and deliberate test selection.
- Operational follow-up: publishing or pushing requires the final user decision.
