# Scanner 2.0.0 preservation contract and implementation audit

Date: 8 October 2026. Status: baseline inspected and preserved; WPF migration not started.

## Authority and scope

The owner's additional scanner requirement makes **Controller Scanner 2.0.0-local the functional baseline for the native rebuild**. Preserve its capture engine first; modernize its UI second. A simpler scanner, the older Python scanner, or an independently written XInput-only reader is not an acceptable replacement. Prefer 2.0.0 behavior when older behavior conflicts, unless deterministic comparison proves a regression.

At the initial preservation-only request, the supplied `X20CTL_NATIVE_REBUILD_MASTER_CODEX_PROMPT.md` was rebuild reference material, not independent authorization of the entire rebuild. The owner subsequently supplied the mission text explicitly as the request. That later request authorizes the staged native rebuild; this preservation contract remains controlling for scanner functionality. See `docs/native-rebuild/IMPLEMENTATION_PLAN.md` for the audited design and implementation checkpoints. The existing scanner, application, and dirty working-tree changes remain intact. No release or upload occurred.

This contract supplements the master brief: its proposed generic input architecture must accommodate every working 2.0.0 path below. Architectural cleanup cannot justify dropping functionality.

## Exact baseline and current verification

Supplied package: `artifacts/input-diagnostic-20261008/X20CTL-Controller-Scanner-2.0.0-local.zip`.

| Item | SHA-256 |
| --- | --- |
| Supplied package | `9b148f22bbba091ed0a30a7bce39423e9354fb997f00ec084f0f8c5d6c5bba98` |
| Packaged `X20CTL-Input-Scan.exe` | `b41713918187eb85a71524d49bea80b824e9b9f24631f8a25d3591f51363ba97` |

Recoverable local checkpoint: `artifacts/scanner-2.0.0-preservation-20261008T185956Z/`. It contains the supplied ZIP, exact packaged EXE, all eight C# sources, README, builder, original build record, source archive, fresh offline self-test output, and `preservation.json` with provenance and digests.

Executed in this audit:

- All eight current C# source digests match the original `build.json` source digests.
- Both files listed in the package's `SHA256.txt` match the archived bytes; the packaged EXE also matches `build.json`.
- The exact packaged EXE passed **46 checks**, reporting `hardware_access: false`.
- No live hardware, screen interaction, or WPF runtime was tested. No compiler build was rerun; this verification exercised the supplied executable and matched current sources to its recorded build provenance.

## Newly supplied hardware evidence

The owner reports recent successful **wired X15/X05 Pro input captures** with Scanner 2.0.0:

- Standard face buttons, bumpers, D-pad, Start/Back, and both stick clicks.
- Full signed stick ranges `-32768..32767`.
- Trigger ranges `0..255`, including intermediate values.
- Button press/release durations.
- Exported scan files with all recorded file hashes passing integrity verification.

These are owner-tested results supplied on 8 October 2026, not a fresh hardware run performed by this audit. The underlying result ZIPs were not supplied here. The package ZIP contains the scanner distribution, not those hardware sessions. The earlier endpoints-only capture does not override this newer evidence from a different run. Keep model, transport, firmware/revision, and input source tied to each session's evidence.

Rear-button output and Turbo remain separate research questions. Standard input acceptance does not establish independent M1/M2 identities, Turbo frequency/duty cycle, configuration support, persistent remapping, motor response, or write authorization.

## Complete capture and diagnostic path inventory

| Path | Actual implementation | Behavior that must survive |
| --- | --- | --- |
| Continuous XInput | `Core.cs`: `WindowsReader`; `App.cs`: `StartMonitoring`, `TickInput` | Documented `XInputGetState`; all four slots polled on the existing 20 ms UI timer; one reader spans actions instead of starting a worker per control. Retain native `Pad`/`State` ABI and all packet/button/trigger/stick fields. |
| Alternative XInput implementations | `Core.cs`: `WindowsReader`; `App.cs`: library selection, `NewSource` | Allowlisted `XInput1_4.dll`, `XInput9_1_0.dll`, and `XInput1_3.dll`, loaded by absolute Windows system path. The owner compares readers and chooses the changing slot. A missing reader produces an explicit error; this is manual fallback, not automatic DLL switching. |
| Source verification and guided captures | `App.cs`: `Begin`, `TickInput`, `UpdateControls`; `Core.cs`: `Analysis` | Confirm selected-slot readiness before countdown; three-second countdown, 20-second preflight, eight-second guided action windows. Require a released baseline and A/LT/RT press/release cycles on one slot. Preserve failed/partial evidence, skip events, and action-specific outcomes. |
| Raw Input fallback | `RawInput.cs`: `RawObserver`, `RawForm.WndProc`; `App.cs`: fallback entry | Focused, explicitly started, separate `WM_INPUT` stream. Register Generic Desktop joystick/gamepad/multi-axis usages 4/5/8 only. Preserve device path, report framing, all decoded rows, and optional raw bytes. Do not fold it into the selected XInput slot without evidence. |
| HID usage parsing within Raw Input | `RawInput.cs`: `RawObserver.Read` | Use device preparsed data and `HidP_GetUsages` for button page 9; `HidP_GetUsageValue` for usages `0x30..0x36` and `0x39`. Retain numbered buttons and declared axis values; do not guess A/B/X/Y from arbitrary HID bytes. This baseline has no separate direct-HID `ReadFile` capture loop. |
| HID/USB metadata | `Inventory.cs`: `WindowsInventory.Read` | SetupAPI interfaces, paths, hardware IDs, container IDs, product/manufacturer, VID/PID, HID usages and parsed input/output/feature lengths. Read HID metadata with zero requested access and shared handles; free preparsed data/handles. Serials are opt-in. |
| Raw Input inventory | `Inventory.cs`: `ReadRaw` | Enumerate HID controller collections with path, VID/PID and usage data. Retain the explicit unverified XInput-slot association. |
| Interface grouping/correlation | `Inventory.cs`: `Inventory.Group`, controller-container filtering; `Wizard.cs`: detection | Group by shared nonempty Windows container ID, otherwise individual interface path. Preserve companion interfaces belonging to retained controller containers. Matching VID/PID or names does not merge unrelated devices or prove model identity. |
| Enumeration failure fallback | `AsyncInventory.cs`: `Bound`; `Wizard.cs`: detection continuation | A 15-second inventory wait limit or inventory failure leaves live XInput usable, records inventory limitations, and retains completed inventory. Timeout bounds waiting; it does not cancel the underlying enumeration task. |
| XInput diagnostic capabilities | `Core.cs`: `WindowsReader.ReportedCapabilities`; `App.cs`: `StartMonitoring` | Export all four slots' driver-reported capability data, reader identity, unavailable results, and explicit absence of physical motor/configuration verification. |
| Press/release and hold duration | `Core.cs`: `PressTracker`; `App.cs`: `TickInput`; `RawInput.cs`: `RawForm.WndProc` | Monotonic XInput hold timing per slot, press/release edges and completed release durations. Persist selected-slot edges during active recording after countdown; keep last-release display. Disconnect clears unfinished holds without manufacturing release success. Raw Input separately tracks numbered-button holds by interface path. |
| Analog samples and visualization | `Core.cs`: `Sample`, `Analysis.Summary`; `App.cs`: `TickInput`; `ControllerView.cs` | Preserve full signed 16-bit stick and unsigned 8-bit trigger samples, including intermediate values; ranges, raw values, normalized stick display, trigger bars/percent, packet counters and timestamps. Never discard values to fit a visualizer. |
| Session export and integrity | `Core.cs`: `Evidence`; `App.cs`: `Save`, `NewSource`, close handler | Reviewable local report folder plus ZIP, metadata/events/summaries, action JSONL, Windows inventory, optional capabilities and Raw Input JSONL, file sizes and SHA-256 manifest. Retain local save on source reset/dirty close, incomplete evidence, and save-failure protection. |

The guided action inventory is: neutral; A/B/X/Y; LB/RB; four cardinal and four diagonal D-pad directions; Start/Back; L3/R3; LT/RT; left/right stick; optional rear_left/rear_right/turbo. Diagonal acceptance requires simultaneous bits. Preserve action-specific cycle checks and inconclusive outcomes for wrong controls, drift, trigger noise, empty data and source changes.

## Export, timing and safety details

- Export version remains `collectorVersion: 2.0.0-local`, with manifest schema `input-diagnostic/1`. Action files retain `timestamp`, `action`, `source`, `elapsed_ms`, and all existing `values`. Preserve every current metadata, event, inventory, capability and Raw Input field. Additions that alter semantics require versioning and a compatibility path.
- `Evidence.Export` generates the file digests; the offline self-test validates manifest sizes/digests and exact folder/ZIP bytes. There is no separate user-facing import/integrity validator in this engine. Preserve these actual validation checks and the owner's successful exported-session integrity evidence.
- Capture bounds: 30,000 XInput samples total; 10,000 Raw Input records and 4 MiB serialized raw evidence; Raw Input buffers up to 65,536 bytes, report stride 1..4,096 and report count 1..1,024, with length checks. Preserve bound/error handling and resource cleanup.
- Sample interval summaries are host observations; neither the 20 ms timer nor those statistics establish hardware polling rate or latency. Preserve monotonic capture timing independently of future WPF rendering cadence.
- Preserve released/pressed/released cycle behavior and the existing trigger acceptance threshold above 30; live trigger hold display currently activates above zero. These are different existing semantics.
- Exports stay under `Documents/X20CTLInputReports` unless the owner selects an evolution of this behavior. Serials and raw report bytes retain their independent opt-ins; no automatic upload or controller configuration writes are introduced.

## Current limitations that must not become fabricated capabilities

The actual baseline does not automatically correlate XInput slots to HID/Raw Input interfaces; discover original USB/HID descriptor bytes; separately enumerate DirectInput; discover BLE GATT; write settings; program macros; test motor response; or verify persistent remapping.

Raw Input duration text is currently computed in `RawForm`, while exported Raw Input rows contain timestamps and decoded usages, not explicit duration events. XInput duration events are persisted during active recording. Preserve both behaviors; making Raw Input durations explicit in a future schema is an extension, not proof that 2.0.0 already exports them.

Rear/Turbo summaries only label ordinary logical output and keep `independent_rear_control_verified: false`. The current capture engine has no Turbo-frequency analyzer. The master brief's richer diagnostics are future additions and cannot replace these working paths.

## WPF migration order and acceptance gates

1. **Retain the frozen baseline.** Compare against the supplied distribution and source checkpoint, not the old Python scanner. Keep the existing executable available until parity is accepted.
2. **Extract existing behavior before replacing presentation.** Reuse the proven `WindowsReader`, `Analysis`, `PressTracker`, inventory and export behavior. Move capture/session logic out of `ScanForm` and `RawForm` without changing its decisions. These forms contain engine behavior, not just presentation. Keep the Windows Raw Input message hook/parser, reader lifecycle, and timer/monotonic-clock semantics when replacing the HWND host.
3. **Prove engine equivalence.** Carry the 46 existing checks forward and compare baseline/new behavior on recorded or synthetic streams for every action, all slots, signed stick extremes, trigger endpoints/intermediates, press/release timing, disconnects, wrong sources/controls, timeouts, grouping, bounds and export hashes. Add focused checks for form-contained lifecycle and Raw Input behaviors currently outside the self-test. This is future migration work, not verification completed by this audit.
4. **Connect WPF as the visual frontend.** Render engine state through WPF bindings; UI redraws must not truncate analog streams, change timing or gate off fallback monitors. Preserve access to alternative readers, all slots, separate Raw Input evidence, inventory details, skip/failure export and incomplete-session retention.
5. **Accept hardware parity.** Replay owner-supplied sessions when available, then compare wired X15 and X05 Pro acceptance against the reported controls/ranges/durations/integrity. Retest relevant transports/revisions individually; synthetic checks do not establish physical equivalence. Rear/Turbo remain unresolved unless new targeted evidence establishes them.

Any lost 2.0.0 capture path, field, fallback, duration, analog fidelity, export integrity check, or source-confidence distinction blocks declaring scanner migration complete. A behavioral correction is allowed only with deterministic regression evidence and preserved original diagnostic data. A cleaner architecture alone is insufficient.
