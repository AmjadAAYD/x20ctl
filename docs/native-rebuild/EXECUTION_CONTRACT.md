# Accepted execution contract

Date: 8 October 2026. Inputs: complete master prompt (2,930 lines), accepted review and owner's execution correction attachment. Supersedes conflicting older local plans.

## Binding rules

| Rule | Requirement | Future evidence |
| --- | --- | --- |
| Final runtime | C++20 engine + WPF .NET 10; no Python/web production runtime | Package dependency and clean-machine checks |
| Prototype | Preserved migration artifact; disposable presentation; file-by-file audit | Exact source hashes and disposition table |
| Dual baselines | Python for X20; exact C# Scanner 2.0.0 for scanner | Separate differential suites plus independent hardware fixtures |
| Streaming | RPC and subscriptions; sequence/source/time/loss; bounded queues | Framing/lifecycle/backpressure/overflow/export tests |
| Engine authorization | Reject wrong/ambiguous device/model/transport/command/capability | Direct-engine negative tests independent of UI |
| Ambiguous write | Timeout/crash -> indeterminate -> reread -> controlled decision | Applied-with-lost-ACK/disconnect/no-replay cases |
| Capabilities | Availability/read/write/persistence/evidence/applicability separated | Model/firmware/revision-specific gates |
| Failure parity | Delay/duplicate/CRC/chunks/disconnect/rollover/restart/partial operations | Behavioral comparison and captured evidence |
| Persistence | Readback/reconnect/safe appropriate power cycle are distinct | Per-feature persistence evidence |
| Input ownership | Capture/record/remap suppress navigation; fresh input on resume | Each context blocks activation/tabs; scoped escape |
| Migration | Backup/versioned/atomic/idempotent; source and unknown fields intact | Crash/repeat/corrupt-store/round-trip tests |
| Visual gate | Controller Zone only, actual WPF screenshots | Owner approval before any other page |

These are implementation requirements, not claims that a native engine already exists.

## First deliverable

Native frame/new visual system, dark center/blue left/amber right; Player 1 EasySMX X20 connected MOCK; Players 2–4 empty. Mouse/keyboard focus and simulated gamepad focus only. Target 1220 x 800; minimum 1040 x 700 DIPs. Review 100/125/150% DPI contexts, resizing, visible keyboard focus and reduced-motion fallback.

Actual window capture is distinct from visual-tree renders. If available monitors cannot exercise all OS scales safely, state which WPF DPI contexts were tested and which actual monitor/WM_DPICHANGED transitions remain unverified. Never label pixel resampling as OS DPI acceptance.

## Stop

Audit -> reconciliation -> design system -> zone -> screenshots -> STOP FOR APPROVAL. No other page/backend integration, live input, write, destructive cleanup, push/publish/tag/upload/stable replacement.

## Latest interaction and access override

The dynamic focus brief replaces the static P1-mock screenshot state. All-empty 2x2 overview is default; any player can expand/retract while retaining assignment/artwork. Follow DYNAMIC_ZONE_STATUS.md. Desktop automation is revoked; GUI/screenshots/recording remain unverified until the user reviews or explicitly reauthorizes them. No next X20CTL page before visual approval.
