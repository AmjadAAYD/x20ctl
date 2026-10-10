# Standalone scanner rebuild - local validation, 8 October 2026

## Scope

A separate Windows desktop scanner was created in tools/input_diagnostic. The main X20CTL application and its existing working-tree changes were not replaced. The supplied rebuild prompt ends at the Live input source-label section; this document covers the received requirements.

## Implemented

- Welcome, setup acknowledgement, explicit connection context, optional printed-model selection and device detection wizard. Unknown is available; connection/model claims do not become detected identity.
- HID/USB SetupAPI metadata, product/manufacturer strings, VID/PID, paths, parsed HID report sizes/usages, optional serial strings and Raw Input controller inventory. Groups use shared nonempty Windows container IDs. Matching VID/PID never merges unrelated devices. Companion interfaces remain in private export.
- A continuously running documented XInput reader, all four live slots and alternative system DLL selections. No per-action process startup. Driver-reported capabilities remain separate from physical acceptance.
- Individual button highlights, monotonic held duration in milliseconds, persistent last-release duration, stick plots/raw/normalized values and trigger bars/raw/percent values. The selected input source remains explicit.
- Full guided input pass gated by released baseline, A press/release and both trigger cycles above the standard trigger threshold. Countdown starts after readiness. Source loss invalidates verification and preserves partial data. Wrong/noisy/empty results remain inconclusive.
- Optional rear/turbo ordinary-output observations without independent M-key/configuration claims.
- Separate focused Raw Input monitor using HID parser declared usages. Numbered buttons do not become guessed A/B/X/Y; undeclared or unsupported usages remain unavailable. Keyboard and mouse usages are not registered. Raw bytes require a separate checkbox.
- Reviewable local results folder and ZIP under Documents/X20CTLInputReports, SHA-256 file manifest, bounded input counts/raw size, retained failures and explicit limitations. Source changes save existing local evidence before resetting. Nothing is uploaded.
- Windows inventory waits are bounded at 15 seconds, after which live input remains available with inventory limitations recorded.

## Executed verification

- Windows x64 .NET Framework compilation passed.
- Actual packaged EXE offline self-test passed 46 checks with hardware_access=false.
- Coverage includes native structure layouts and synthetic bytes, source verification positives/negatives, held/released duration edges, trigger-noise rejection, wrong source/button, grouped/unrelated interfaces, raw frame bounds, raw-size limit, bounded inventory timeout, ordinary rear aliases, and exact export ZIP bytes/file digests.
- Builder Ruff checks and git diff --check passed.
- Offscreen WinForms render generated with synthetic data; no shown window or user screen access. Native widget painting in offscreen rendering is limited, so this is not desktop GUI acceptance.
- Build source/compiler/EXE hashes and check result are in artifacts/input-diagnostic-20261008/build.json.

## Limits and next acceptance

Update from the owner on 8 October 2026: Scanner 2.0.0 subsequently produced successful wired X15/X05 Pro captures, including standard buttons, full stick ranges, trigger endpoints and intermediate values, press/release durations, and exports with passing file hashes. These are owner-reported hardware results; the earlier development verification below did not run that hardware. Rear-button output and Turbo remain unresolved. The native migration must preserve this exact 2.0.0 capture baseline. See [Scanner 2.0.0 preservation contract](plans/scanner-2.0.0-preservation.md) for the source audit, frozen checkpoint, fresh packaged self-test, known limits and WPF parity gates.

During the original development verification, no actual controller, Windows live-device enumeration, HID parser report, desktop interaction or motor response was tested. The earlier X05 Pro zero-input cause remains unproven; per-action startup timing was a plausible issue eliminated in this scanner, not a confirmed diagnosis. If input fails in a future run, first watch A/LT/RT live and export a short failed verification and Raw Input observation instead of repeating a full scan.

Original USB/HID descriptor bytes, separate DirectInput enumeration and BLE GATT discovery remain explicitly unavailable. Parsed caps are not original descriptors. Configuration interfaces are metadata/research only; no vendor commands or remapping backends were enabled. No release, upload, or public publication occurred.

## Windows API references

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdeviceinfow
- https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_capabilities
