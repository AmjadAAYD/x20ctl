# Scanner native evolution

Date: 8 October 2026. Preserved baseline; C++20 port deferred until after product visual approval. The earlier final C# capture-library architecture is superseded.

## Non-regression contract

Use [the complete Scanner 2.0.0 audit](../plans/scanner-2.0.0-preservation.md) as the capture-path and export contract. The exact distribution and matching source archive were preserved in `artifacts/scanner-2.0.0-preservation-20261008T185956Z/`. Its supplied EXE passed 46 offline checks during the preceding preservation task; these were not rerun during this planning audit.

The owner reported successful wired X15/X05 Pro standard controls, stick ranges `-32768..32767`, trigger ranges `0..255` with intermediate values, hold/release durations, and valid exported hashes. Preserve those results as owner-tested evidence; underlying session ZIPs and model/revision details were not supplied for replay here. Rear/Turbo remain unresolved. Do not downgrade to Python scanner 1.x behavior or make XInput-only capture the completed scanner.

## Extraction risks found in source

1. `Core.cs` contains the XInput ABI/reader, cycle analysis, tracker and exporter, but `App.cs` also owns all-slot polling, readiness/countdown, sampling windows, action sequence, duration-event persistence, failure/disconnect invalidation, dirty close and source reset. Carry these behaviors into the session engine before discarding any form.
2. `RawInput.cs` contains parser and Windows calls alongside a WinForms `WndProc`/display. Port the HWND message hookup to WPF without changing framing, declared-usage parsing, path separation, opt-ins, resource cleanup or bounded logging. Per-path Raw Input holds currently display locally; explicit raw duration events are a future schema extension.
3. `System.Web.Script.Serialization.JavaScriptSerializer` is used by the .NET Framework scanner. Moving to .NET 10 requires a serialization compatibility adapter. Public fields, anonymous objects, case, nulls, numeric ranges, UTC timestamps, JSONL and file sizes/digests must be checked explicitly. A default new serializer configuration must not silently drop public fields.
4. The existing sampling timer runs on the WinForms UI thread. Moving capture to an independent worker can improve resilience but changes scheduling. Preserve monotonic timing, raw sample fidelity and session decisions with differential fixtures; a WPF rendering timer must not become the sole evidence clock.

## Boundaries

The C++20 scanner module owns samples/actions, analysis, duration tracking, authoritative recording, versioned export and session lifecycle. The native Windows platform layer owns XInput, SetupAPI/HID and Raw Input with a native HWND/message loop. WPF provides presentation and explicit choices; it does not own capture. Keep deterministic clocks and injected streams for tests. Compare against the independent C# Scanner 2.0.0 oracle; Python parity cannot establish scanner parity.

The IPC stream contract separates recording from presentation delivery. Bounded recording overflow marks evidence incomplete with source/time/drop counts; subscription loss is recorded separately. WPF render throttling cannot discard evidence. No Scanner port/UI precedes the Controller Zone gate.

Capture is independent from model configuration and foreground gamepad navigation. During a guided test the scanner owns control input and navigation yields. Retain every candidate stream with explicit provenance. A changing source may be recommended, but user selection remains reviewable and ambiguous associations stay unknown. Do not infer physical identity or slot correlation from activity alone.

## Preserve first, extend second

Baseline: continuous all-four-slot XInput; three manual reader alternatives; readiness/A+LT+RT verification; standard and diagonal button cycles; analog sampling; hold/release events; separate focused Raw Input/HID declared usages; SetupAPI/USB/HID/Raw Input inventory; nonempty-container grouping; 15-second wait fallback; driver capabilities; local partial/failure export; existing SHA-256/ZIP validation and all limits.

Not currently present in 2.0.0: direct-HID read loop, original report descriptors, automatic XInput/HID association, independent M-key verification, Turbo frequency analysis, BLE discovery or persistent writes. New activity/source preference, three-press auto-advance, rear comparisons, macro playback observation, calibration statistics, timing statistics, original descriptors and safe BLE research must be added behind tested, honest statuses without replacing baseline paths.

Older integrated research code contains useful extended passive capture and official-app guidance. Catalog and retain it as separately attributed research functionality; do not replace the proven 2.0.0 generic capture engine with per-action workers. Existing upload/report helpers must not become automatic native scanner transfer.

## Export and parity

Maintain the baseline `input-diagnostic/1` report schema and all existing files/fields through a compatible exporter. Introduce an explicitly versioned richer session schema for added normalized events, timestamps/durations, per-interface activity, test results and comparison. Preserve baseline raw data in the richer export; do not rename/reinterpret old fields silently.

Every transport/source transition gets a new session with parent comparison metadata. Keep failed/partial sessions and source-loss events. Source changes save dirty evidence before reset; save failure retains recoverable state. Validate each exported member's size/SHA-256 against the exact ZIP bytes. Inspect paths safely and reject unsafe or duplicate members in a future importer.

Gate extraction on the 46 existing checks plus differential fixtures for all actions/slots, raw parser frames/limits, grouping, duration edges, analog values, preflight failure, source changes, disconnect/error/close, opt-ins and export field fidelity. Compare semantic output while excluding generated session IDs/time-of-export, not recorded control timing. Hardware parity then requires supplied-session replay and owner comparison on wired X15/X05 Pro; simulation alone is insufficient.

Configuration commands, unknown vendor reports and hidden uploads remain outside Scanner. Standard rumble testing is a later explicit known-API test with short bounded pulses and shutdown-to-zero, recorded as commanded versus owner-felt. No independent rear/Turbo or persistence success is asserted from standard gameplay output.
