# X20CTL native rebuild architecture

Date: 8 October 2026. Authority: the complete native rebuild master prompt and the owner's accepted execution corrections. This supersedes the earlier Tester/Scanner-first architecture; historical files remain in the verified checkpoint.

## Final ownership

- C++20 / CMake / MSVC: static x20ctl_core, model-specific protocols, physical identity, multidimensional capabilities, configuration, profiles, scanner capture, diagnostics and native tests.
- x20ctl-engine.exe: UI-owned native host, Windows XInput/Raw Input/HID/device discovery/BLE (intended C++/WinRT), local IPC, sessions and lifecycle.
- desktop-dotnet/X20Ctl.Product: C# / WPF / .NET 10 presentation, local drafts, dialogs and navigation.
- desktop-dotnet/X20Ctl.Zone: development-only zone/navigation state; no hardware access.

Final production contains no Python, React, Vite, pywebview or WebView2 runtime. WPF is managed Windows desktop presentation; the engine is native C++20. WPF never builds packets or independently authorizes hardware writes. Gameplay and BLE configuration transports remain separate.

A separate engine process provides isolation and independent tests at the cost of IPC/lifecycle/deployment complexity. Prefer a private Windows named pipe; framed redirected stdin/stdout is the allowed fallback. No local HTTP server. Model-specific modules share codec primitives without inheriting X20 authorization.

## Disposable local prototype

X20Ctl.Desktop is a migration artifact, not a visual/product source of truth. Tester-first startup, Scanner composition, old navigation and styling are superseded. See [file-by-file audit](PROTOTYPE_AUDIT.md). Salvage setup, tested navigation/simulation and frame concepts; independently author the new shell/resources. Keep the prototype intact until useful behavior has a verified replacement. Never reset/delete/overwrite unrelated work.

## Dual references and independent evidence

1. Python protocol.py/client.py/desktop settings/service/profiles are the X20 behavioral oracle: packets, CRC, scrambling, parsing, counters/nonces, reads and verified writes.
2. Exact Scanner 2.0.0 C# source under tools/input_diagnostic and its preserved package are the scanner oracle: source discovery, four-slot input, reader alternatives, HID/Raw Input evidence, timing/durations/ranges, session behavior, exports and hashes.

Python parity cannot establish scanner parity. Captured hardware evidence is independent of both implementations. If verified device evidence contradicts legacy behavior, record the discrepancy and deliberately fix it rather than reproduce a bug. Mark simulation, replay, owner reports and fresh hardware observations separately.

## Engine guarantees

Every write validates current session/generation, physical identity/confidence, model, known firmware/revision constraints, configuration transport, capability, command family and payload. Ambiguous/unknown device or unsupported capability refuses writes even if UI enables them. Serialize configuration per device; invalidate authorization on disconnect/reassignment/restart. XInput slot and similar names are not physical identity.

Read current -> change understood fields -> preserve unknown bytes -> write -> compare readback. Keep per-category results, local drafts and verified hardware state separate.

Timeout/cancellation after send is indeterminate. Reread the same freshly verified device before considering a controlled retry. Desired readback establishes current state, not a received ACK or persistence. Never replay writes on restart. Immediate readback is not persistence; reconnect/reread and appropriate safe power-cycle/reread are separate gates. No automatic destructive/unnecessary power cycles.

Capabilities separate availability/read/write/persistence/evidence/applicability; see [controller model](CONTROLLER_MODEL.md). UI-disabled controls are not safety boundaries.

## Streaming and input ownership

Engine recording is authoritative and independent of WPF render throttling. Streams carry source/subscription IDs, monotonic sequence/timestamp and explicit loss information. Bounded overflow is recorded in diagnostics/export, never silently omitted. See [IPC](IPC.md).

Contexts: UI_NAVIGATION, TESTER_CAPTURE, SCANNER_CAPTURE, MACRO_RECORDING, REMAP_CAPTURE. Capture contexts suppress gameplay-driven tabs/activation/Apply/Save/dialog dismissal. Resume requires fresh input; escape is explicitly scoped so a tested B press cannot end capture accidentally. Initial navigation uses opt-in keyboard-generated simulation only. Real gamepad input follows the native Windows input layer after visual approval.

## Persistence and retirement

Preserve legacy stores, unknown fields, model/setup names and user data. Migration is backed up, versioned, atomic and idempotent; old source stays intact. See [config migration](CONFIG_MIGRATION.md).

Python remains reference/development infrastructure until codec, BLE, verified writes/readback, profiles, scanner, migration and WPF integration meet separate parity/hardware gates. Final packaging excludes Python; the earlier proposed Python service/C# scanner final architecture is superseded. Legacy source/research/fixtures remain recoverable.

## Authorized execution

Audit -> architecture reconciliation -> new design system -> Controller Zone -> actual screenshots -> STOP FOR OWNER APPROVAL. See [execution contract](EXECUTION_CONTRACT.md), [plan](IMPLEMENTATION_PLAN.md) and [status](MIGRATION_STATUS.md). No Tester/Scanner/Studio page, protocol port or real hardware backend precedes the gate. No push/publish/tag/upload/stable replacement.

ApexSenseBridge is a separation/polish benchmark; independently implement concepts without importing reference code/XAML/assets or changing X20CTL identity/license.

## Latest Zone correction

The static large-Player-1 visual requirement is superseded by DYNAMIC_ZONE_STATUS.md: default four equal empty docks, any player can animate into hero, persistent per-player artwork/state, animated return to overview. No desktop automation is authorized following the user's stop request.
