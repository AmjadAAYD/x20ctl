---
status: active
feature: native-2.0-desktop
specification: ../specs/native-2.0-desktop.md
base_commit: 4bb2560
git_policy: required
execution_mode: continuous
current_checkpoint: CP-03
checkpoint_status: In Progress
lifecycle_owner: Plumbline Execute
last_verified_commit: 4bb2560
next_safe_action: Finish isolated packaging, bundled runtime and executable acceptance; reconcile documentation and publish only the validated build.
delegation_roles: Direct - no project-local roles configured
delegation_status: direct
ready_for_acceptance: false
---

# Native 2.0 implementation

The approved specification controls product behavior. Preserve React's visual direction, restore the verified Python engine, and deliver a locally packaged Windows application. The user's September 18 overnight instruction authorizes commits, pushes and a 2.0.0 GitHub release after validation. Do not block on the powered-off controller; disclose the absence of fresh physical write testing.

## CP-01: Desktop and hardware foundation

Status: Complete. Restored the proven engine and regression baseline. Added a serialized, allowlisted native bridge. Native startup, handshake rejection, persistence, read-back failure and disconnected writes have focused tests. Full suite: 500 passing; isolated desktop tests: 13 passing.

## CP-02: Real configuration and polished UI

Status: Complete. Native configuration/UI wiring, real XInput recording, curves, profiles and corrected macro semantics are implemented. Synthetic recording and unsupported controls removed. Two frontend curve tests pass. Expanded native-window smoke exercises six pages, edits, persistence, invalid import, BLE discovery, disconnected-write rejection and external-navigation blocking. Real capture review identified and corrected startup readiness and layout defects. No supported hardware was active during verification.

## CP-03: Packaged application and truthful evidence

Status: In Progress. Initial 25.6 MB executable passed native-window smoke. Final build will use a clean, pinned environment and include a Microsoft-signed WebView2 fallback for machines without the runtime. Finish documentation, full executable acceptance, genuine release screenshots and actual artifact hashes before publication.

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

- Residual risk: no fresh physical-controller write/replay test is possible with the controller off. The user explicitly requested continued release preparation without waiting for hardware. Publish this limitation clearly and retain 1.2.0 as a rollback option.
- Publication authority: September 18 overnight request allows commit, push and release after software/executable validation. Never claim zero bugs or invent hardware proof.
