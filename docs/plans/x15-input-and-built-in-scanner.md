# X15 input support and a built-in controller research scanner

Date: 7 October 2026
Status: implemented as a local Windows build; see `../x15-built-in-scanner-validation.md` for checks, precise scope, physical-test limits and the pending receiver deployment.

## 1. Intended outcome

1. X15 gets an input-focused Studio with a real, explicitly selected gameplay-input connection: live buttons, D-pad, sticks, triggers, connection status and input testing.
2. X15 does not display independent M1/M2 detection, RGB controls, vibration-strength controls or configuration editors that cannot write to this hardware. Remap writes, firmware macro programming and response-curve writes are also unavailable; removing just RGB and vibration would leave other unsupported controls looking functional.
3. X20 retains its existing verified configuration backend. X20 Pro retains its current explicitly labelled preview Studio; its configuration protocol is still unverified.
4. X05, X05 Pro, X10 and D10 open their controller section with a **Not available yet** popup and **Scan now** / **Scan later**. The section remains a read-only preview with unsupported configuration actions disabled. Keep their existing artwork/geometry/code for future work.
5. A **Scan my controller** action inside x20ctl performs discovery, guided input tests and optional research stages, previews the evidence and saves a shareable ZIP. Users do not need to download or launch a separate scanner executable.
6. After upfront disclosure/authorization and report review, results are automatically submitted to X20CTLADMIN. Completion then offers optional manual Discord/email sharing using **mistermajid** or **aaydamjad@gmail.com**, including fallback when the website submission fails.
7. RGB-specific information requests, tests, settings experiments and controls are excluded from the new scanner workflow.

Removal scope is interpreted as the unsupported X15 functions; it does not remove working X20 features. Physical RGB rings in existing photographs and the x20ctl blue/amber visual theme are not hardware-control features and stay intact.

“Fully working X15” must mean the implemented input features work for the tested unit/mode. It cannot mean verified configuration support or every connection mode. Until live testing, label the new input backend experimental and state its tested scope.

## 2. Current evidence and code constraints

- The X15-labelled submission has 25 recordings and 6,075 validated Windows HID samples. The observed collection is VID `0079`, PID `181C`, usage page 1 / usage 5, input length 10, output length 5, feature length 0. This identity also appears in documented X20 DInput evidence: it is an input-format candidate, not automatic X15 detection.
- Captures correlate face buttons, shoulders, D-pad, stick clicks and stick pairs. LT/RT only show 0/255. The M1-labelled capture emits A, and M2 emits D-pad Right. No independent paddle identifiers or configuration commands were established.
- Per-axis orientation needs isolated directional captures; hardware revision and firmware are unknown. A fresh slow-trigger-check result has not been provided in this chat.
- `ModelWorkspace.tsx` currently uses `emptyInput()`, disables connection and exposes pages using physical hardware flags. Physical features and software functionality must become separate capabilities.
- `DeviceService.dispatch` currently blocks non-X20 gameplay input and most scanner/report operations whenever `backend` is null. A global scanner cannot use this configuration-backend gate.
- `ReportWorkflow` invokes a separately pinned scanner executable. The current report uploader accepts a different, limited report layout. A full research ZIP must not be silently pushed through that legacy upload contract.
- The source checkout contains an older local collector under `tools/controller_scan_kit`; a newer standalone kit exists at the published `scanner-v1.1.0` tag. Review and reconcile their code rather than replacing intentional working files or bundling the older version accidentally.

Verified public scanner fallback:

- Release: https://github.com/AmjadAAYD/x20ctl/releases/tag/scanner-v1.1.0
- ZIP: https://github.com/AmjadAAYD/x20ctl/releases/download/scanner-v1.1.0/ControllerScanKit-1.1.0-win-x64.zip
- Published source commit: `b9bf6534dab8ea90bde6a84cfa2780fc72e61c1d`.
- Collection policy: https://github.com/AmjadAAYD/x20ctl/blob/scanner-v1.1.0/tools/controller-scan-kit/CAPABILITIES.json
- Data table: https://github.com/AmjadAAYD/x20ctl/blob/scanner-v1.1.0/tools/controller-scan-kit/DATA_COLLECTED.md

The release and asset existence were checked through the GitHub API for this plan. No downloaded binary was executed and no release was created or modified.

## 3. Model availability and X15 Studio

Use explicit registry fields instead of treating `backend != null` as both configuration and input support:

- `availability`: configuration Studio / input Studio / preview Studio / not available yet.
- `inputBackend`: separately names a supported read-only adapter or none.
- `settingsBackend`: X20's verified driver or none.
- `uiCapabilities`: live input, configuration reads, remap writes, macro writes, curves, vibration strength, lighting and local-only editor features.
- Preserve accurate physical `hardware` metadata; do not set a physical capability false merely to hide a software tab.

X15's default tabs are **Buttons**, **Input tester**, and **Device & connection**. Scanner access is available from the header/sidebar and the controller landing page. Buttons shows live physical inputs and their details, not a remap editor that cannot apply changes.

Remove X15's Lighting, Vibration strength, hardware Macros, Response curves and Apply-to-controller actions. Do not render interactive M1/M2 macro-navigation hotspots or independent paddle activity indicators in X15's rear view. Real rear artwork remains visible. A paddle that emits A will illuminate A as the received output; the app cannot identify which physical control produced that same packet.

Keep global/local macro-library data and other models' existing drafts intact. Do not assign X20's 47-entry firmware limit to X15 or imply a local draft is a programmed X15 macro.

For X15 show **Gameplay input connected/disconnected** rather than a red configuration connection that the model cannot establish. Choosing an X15 player card still does not connect hardware.

### Input adapter and player ownership

- Add a shared read-only Windows input adapter for the narrowly observed HID profile. Reuse reviewed collector/native-read mechanics; do not reuse X20 BLE commands.
- Decode the known button bits, hat states, stick pairs and trigger fields into the existing frontend input DTO. Reject unknown report lengths/IDs/layouts. Preserve raw readings for diagnostics.
- Keep axis orientation explicit in the adapter. Add individual left/right/up/down calibration checks before claiming model-specific orientation is verified.
- Report actual trigger values. With the current data they are endpoint-only; do not interpolate a partial reading that was never received. A later confirmed analog mode can use its real intermediate values.
- A standard XInput source can be explicitly selected as a generic PC input path. Its availability does not establish that the X15 has been tested in that mode.
- Store input bindings by player and model, with an opaque selected-source token. Never choose an arbitrary first controller or silently attach another device sharing VID/PID after disconnect.
- Reject a source already owned by another player unless the user explicitly reassigns it. Clear stale pressed inputs immediately on disconnect, model switch or source loss. Stop/release the reader on exit.
- Keep configuration read/write/reset operations separately guarded against X15, even if a caller bypasses the UI.

## 4. Other-controller notice

X05, X05 Pro, X10 and D10 use this message:

> This controller is not available in x20ctl yet. I do not have enough verified data to support it. You can help by scanning your controller and sending me the result privately.

The same popup must disclose the submission step before collection:

> Scan now opens a guided controller scan. After you review the result, it will be sent to X20CTLADMIN to help research controller support. Optional raw captures need their own opt-in and review.

Buttons:

- **Scan now** — records per-session authorization for the disclosed scan/submission scope and opens the built-in workflow, prefilled with the chosen model as an owner claim. The user still follows its target-selection and physical-input instructions.
- **Scan later** — closes the popup and leaves the read-only controller section visible. No discovery, input recording or submission starts.

Keep secondary text links for the standalone scan-kit download and manual contact instructions. Show the popup once each time the user enters an unavailable controller section, not on every tab render. Existing assignments and drafts remain intact. Escape/close is equivalent to Scan later. X20 Pro remains an explicitly labelled preview, not a newly verified backend.

## 5. Built-in scanner experience

Entry points: player landing screen, unavailable-model notice, X15 Device & connection, and a global **Controller scanner** action. A functional configuration backend is not required to scan a controller.

Expose **Controllers** and **Scanner** as two views of the same desktop app. Startup still opens the four-player landing screen after the existing intro; choosing Scanner does not assign or connect a model. Returning to Controllers preserves player assignments and drafts. Preserve the centered launch-size policy and current theme.

Workflow:

1. **Choose scope** — explain the stages, RGB exclusion and local results. The complete workflow exposes every relevant stage; extra raw captures remain opt-in. Record decisions explicitly.
2. **Controller details** — printed model, connection, claimed mode, already-known firmware/revision, settings-app name/version if any. Allow unknown; do not ask for serial numbers.
3. **Identify the target** — compare disconnected/connected inventory, ask the user to select and confirm the target. USB receiver, direct USB and Bluetooth are separate sessions, not assumed equivalent.
4. **Standard device scan** — selected Windows metadata, USB descriptors and HID collections/caps.
5. **Guided gameplay checks** — visual prompts, progress, live input feedback, repeat/skip controls and bounded recordings.
6. **Optional additional research** — BLE services/standard values, sensor checks, rear-button/turbo output observations, known safe reads or a user-provided non-RGB settings capture, and optional identifying photos.
7. **Review** — stage status, omissions/reasons, collected file list and raw-data warning where applicable. Let users inspect/remove optional material.
8. **Save result ZIP** — user confirms the review; write an immutable, checksum-backed report. Keep unfinished local folders when cancelled.
9. **Submit and handoff** — if upfront website-submit authorization is present and review is complete, send the exact reviewed ZIP to X20CTLADMIN. Confirm the server receipt, then offer optional manual Discord/email sharing. On failure keep the ZIP and expose Retry/manual sharing.

Long operations show progress and Cancel. Failed stages leave a partial result and a reason; they do not erase previously collected evidence or block useful remaining stages. Scan/model assignments never imply a hardware connection or verified support.

## 6. Scanner coverage — everything relevant except RGB

Each stage has `observed`, `owner_reported`, `skipped`, `unavailable`, `failed` or `cancelled` status with a reason and evidence reference. An unavailable field is not a negative capability claim.

| Area | Capture / test | Limits and result interpretation |
| --- | --- | --- |
| Provenance | App/scanner versions, OS/architecture, UTC timeline, session IDs, model claim, connection/mode, already-known firmware/revision | Separate owner claims from machine-observed values; no automatic model verdict |
| Target identity | Disconnect/reconnect correlation, selected VID/PID, product/manufacturer/class, driver service and available provider/version/date, hardware/compatible IDs | Export the selected controller only; keep full paths/addresses transient |
| USB structure | Standard device/configuration descriptors, interfaces, alternate settings, endpoints, transfer types, report descriptor lengths | Read standard descriptors where available; retain original bytes and provenance |
| Original HID descriptor | Original descriptor when legitimately obtainable, otherwise explicit missing status and guided supplemental capture route | Parsed caps are not an original descriptor; never reconstruct and label it original |
| HID collections | Usage page/usage, input/output/feature lengths, parsed button/value caps where available | Enumerate metadata, including relevant vendor collections; do not probe arbitrary feature IDs or read keyboard/mouse payloads |
| XInput | Correlated slot and documented buttons/sticks/triggers/packet readings | Capture only the confirmed source; packet numbers are not latency measurements |
| Raw gameplay HID | Selected gamepad input report bytes, lengths, timestamps, report IDs, changed fields/offsets | Explicit opt-in; keep unknown formats raw rather than inventing a decoder |
| Neutral/release | Hands-off baseline and release at the end of each action | Capture noise/centres/stuck inputs as observations, not a hardware fault diagnosis |
| Ordinary controls | A/B/X/Y, LB/RB, Start/Back, L3/R3, D-pad cardinal/diagonal directions, Home/extra controls if actually accessible | Mark Home unavailable if the selected API does not expose it; no undocumented fallback solely to claim completeness |
| Sticks | Individual up/right/down/left, slow axis sweeps, circles, centre returns, clicks | Record ranges, candidate orientation, observed noise and independence; do not claim measured sensor accuracy/resolution from host samples alone |
| Triggers | Neutral, slow partial pulls/holds, full travel and release separately for LT and RT | Report intermediate vs endpoint-only values; no fabricated interpolation |
| Combined input | Both triggers and selected simultaneous button/stick combinations as separate labelled tests | Keep these out of isolated-trigger contamination checks; verify fields do not mask each other |
| Rear/programmable controls | Optional owner-labelled rear-button presses and their resulting ordinary inputs; optional before/after owner-set mapping evidence | Record output association/repetition only; no independent M1/M2 identifiers or firmware programming assumption |
| Turbo/repeats | Optional normal hold vs owner-enabled turbo/repetition recording, with owner instructions/setting noted | No generic toggle commands; host transition counts/intervals are observations, not exact device turbo rate |
| Motion/sensors | Optional tilt/rotation or other known sensor action with raw gamepad reports and safe decoded fields where established | If the selected mode does not expose sensor data, state that; never activate an unknown sensor protocol |
| Other model features | Owner-provided touchpad/touch controls, display, audio/speaker, physical trigger-stop/mode-switch or other non-RGB feature observations; corresponding safe metadata/input when accessible | Use declared source/provenance and model-specific prompts. Do not manufacture display/audio/adaptive-trigger commands or call an owner statement verified support |
| Battery/device information | Available standard battery and device-information reads; owner notes if unavailable | Standard firmware/hardware/software revision/manufacturer values allowed; serial-number characteristic excluded |
| Power/reconnection | Optional owner-led sleep/wake, reconnect and normal charging observations; correlate input/source return without setting changes | Capture mode/transport and observed status; no power-management, reset, pairing or charging-control commands |
| BLE metadata | Target-confirmed advertisement name, UUIDs, RSSI, payload-presence flags; services/characteristics/descriptors/properties | Nearby devices only transiently observed; no forced pairing, vendor characteristic sweep or arbitrary notification subscriptions |
| Vibration/motor evidence | Reported capability/interface metadata, owner-observed feedback from a normal game/existing app; optional correlated non-RGB trace | No X15 strength slider, independent-motor claim or guessed HID motor command |
| Existing configuration | Optional non-RGB known-safe read snapshot through a verified backend, or evidence from an owner's already-working app | Only established reads for the confirmed model; X15 unknown configuration fields remain unknown |
| Settings/protocol research | User-led single non-RGB setting: baseline → change → restore, app/version/mode notes and action timeline | The owner performs the action externally; record restoration. Do not replay packets or change firmware |
| Supplemental traces | Explicitly selected USBPcap PCAP/PCAPNG or Android Bluetooth HCI log, with structural validation and hashes | Import only; raw captures may require external tools and contain identifiers/unrelated traffic. Not automatic live bus sniffing |
| Physical references | Optional user-chosen front/rear/shoulder photos and control-location notes | Separate media permission; review identifying stickers/EXIF. No camera/screen capture by default |
| Reliability | Source loss, cancellation, failed reads, unsupported mode/layout and stage duration | Preserve partial evidence and explain the omission; do not claim controller polling rate/latency from this scanner |
| Integrity | Per-file sizes/SHA-256, schema/version, source provenance, stage status and overall completion | Validate output and ZIP bounds; no nested archives/executables or path traversal masquerading as evidence |

RGB has no dedicated intake questions, read/write tests, effects editor or settings-capture experiment. Complete input/descriptor/trace bytes can contain incidental unknown fields; do not falsely claim those raw bytes were stripped of every RGB-related bit.

“Complete” cannot mean automatically obtaining every proprietary value. Windows user-mode APIs do not always provide original HID descriptors, sensors, vendor configuration data or full USB/Bluetooth traffic. Those stages must state what is missing and provide the next collection route. No administrative driver installation or firmware/protocol experimentation is hidden inside the ordinary scan.

## 7. Truly integrated architecture

- Add a packaged shared collection layer, proposed under `x20ctl/scanning/`, based on the reviewed 1.1.0 policy/collector and current trigger-check lessons. Preserve the older local collector and received evidence. Reconcile source/layout/dependency policy explicitly.
- Add an app-native coordinator, proposed `x20ctl/desktop/research_scan.py`, and a frontend wizard, proposed `src/components/ControllerResearchScanner.tsx`.
- Start a hidden, bounded worker from the **same bundled app executable**, e.g. `x20ctl.exe --scan-worker`. The launcher handles this flag before loading WebView/tray/device services. A source run uses the corresponding module worker entry.
- Prefer one worker per scan session instead of unpacking the large single-file desktop executable once per button. Use a bounded JSON protocol for allowlisted operations and progress, with a hard timeout/kill path for stuck native calls.
- Keep hardware paths, transient device identities, selected handles and consent state inside the coordinator/worker. The frontend uses opaque target/session/report tokens, never arbitrary file paths, executable names or shell commands.
- Add model-independent research operations such as `research_capabilities`, `research_start`, `research_status`, `research_select_target`, `research_capture`, `research_cancel`, `research_preview` and `research_export`. Validate operation transitions, target ownership, scope consent and report ID on every call.
- Scanner availability is independent of a model's settings backend. Enabling it for unknown models must not relax their configuration write guards.
- Coordinate input-reader ownership: pause the selected Studio reader during capture, prevent simultaneous settings writes to that target, then restore the prior input binding/status. Do not silently scan or capture another player's controller.
- Handle report export/contact actions through native dialogs and fixed URL allowlists, using report IDs mapped to known paths. Save data under the app's local Reports directory, not the public source/assets tree.
- Reuse installed application dependencies where possible. The built-in scanner is included at build time and works without a scanner download; the standalone 1.1.0 kit remains a fallback, not a runtime dependency.
- Introduce a versioned full-research report workflow. Preserve pending legacy reports; do not upload the new multi-file/raw-trace bundle through the old 2 MiB, nine-file Vercel contract.
- Update/verify the X20CTLADMIN receiver contract before enabling automatic research submissions. The current desktop endpoint is `https://x20-admin.vercel.app/api/controller-report`; the new report schema and size budget need matching server validation/private storage, not an assumed drop-in upload. Use report-ID idempotency and exact reviewed-byte hashes; an acknowledgement must identify a durably stored submission. No administrator credential belongs in the client.
- Preserve the current Linux host. Declare platform capabilities honestly: the Windows-specific collector is Windows-only initially. Do not import Win32 bindings on Linux or claim the same scan coverage without a tested Linux provider.

### Packaging proof

The distributed desktop must run ordinary scans when no adjacent `x20ctl-scanner.exe` or `ControllerScanKit.exe` exists and Internet access is absent. Worker/core files, policy and provenance must be included in the native package. Update Windows build manifests, scanner identity/integrity handling and documentation so they do not retain a hidden dependency on a downloaded helper.

## 8. Sending the result to Amjad

### Website submission and consent order

The user's follow-up requests automatic X20CTLADMIN submission followed by a manual-sharing prompt. Implement the automation with authorization **before** transfer: the initial popup describes the destination and scope, Scan now authorizes that session, and the report review completes before the upload. Asking permission after an undisclosed upload would not authorize the data already transferred.

After the user finishes/reviews the scan, automatically send only the approved report bytes. Optional raw HID data, protocol traces and media require their own explicit inclusion scope and review. Cancelling, Scan later, revoked authorization, or incomplete review means no upload. Do not persist a global authorization that silently covers future controllers/sessions.

Record request scope/version/time, report hash, submitted/failed/cancelled status and the server receipt ID. On timeout or rejection, preserve the report and offer Retry or manual sharing; do not report success or run unbounded/background retries. Match repeat submissions by report ID/hash to avoid duplicate cases.

Then show the follow-up prompt:

> Would you also like to send this report manually to Amjad on Discord or by email?

Offer **Send manually** / **Not now**. If automatic submission failed, clearly say the report has not reached X20CTLADMIN and offer manual sharing as the fallback. If it succeeded, include the receipt/report ID in the manual message as an optional backup, without implying manual delivery happened already.

Completion screen:

> Your controller report is ready. Review the files, then send the ZIP privately to Amjad. This helps add support for your controller.

Show exact filename, saved location, report ID, size, hash, completion/missing-stage summary and these actions:

1. **Save ZIP** / **Open report folder** — user can locate the actual attachment.
2. **Email Amjad** — open an email draft addressed to `aaydamjad@gmail.com`, with subject `x20ctl controller report — <model> — <report ID>`. Prefill non-identifying connection/mode/app/scanner metadata and a reminder to attach the ZIP. A normal `mailto:` draft does not attach the file or send it; the user attaches and presses Send.
3. **Discord** — show/copy username `mistermajid`, open Discord, and instruct the user to add/message that username and attach the ZIP. Do not invent a profile URL from the username or send a friend request/DM automatically.
4. **Copy report details** / **Copy email** / **Copy Discord username** — fixed, verified strings and a useful handoff summary.
5. **Send later** — report remains accessible from scanner history.

Status text must be truthful: **Report saved**, **Submitting to X20CTLADMIN**, **Submitted — receipt ID**, **Submission failed**, **Email draft opened**, **Discord opened**. Opening a compose window does not establish delivery. Automatic email/Discord upload would need a separate receiving integration and user-confirmed sending contract; no account credentials, SMTP password or bot token belongs in the app.

The contact feature prepares handoff; no agent/tool sends existing captures during implementation or testing.

References for this design:

- Discord username-based friend request flow: https://support.discord.com/hc/en-us/articles/218344397-How-do-I-add-friends-on-Discord
- Windows email URI behaviour: https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-app

## 9. Result layout and validation

Proposed full-research bundle schema 2:

```text
manifest.json                 schema/version, per-file hashes/sizes, collection status
intake.json                   owner claims; no serial-number request
system.json                   minimal OS/app/scanner/runtime context
privacy.json                  consent scopes, raw-data flags, review confirmation
sessions/session01/
  session.json                model claim, mode, transport, target/source, stage outcomes
  device.json                 selected metadata and HID caps
  usb/                        original standard descriptor bytes and parsed summaries
  ble/                        selected advertisement/GATT inventory and standard reads
  input/neutral.jsonl
  input/buttons/*.jsonl
  input/sticks/*.jsonl
  input/triggers/*.jsonl
  input/rear-output/*.jsonl
  input/combined/*.jsonl
  input-summary.json          correlations, ranges and trigger findings, with limits
  observations.json           owner-observed motor/turbo/sensor behaviour
  experiments/               optional non-RGB action timeline and selected trace
  media/                     optional reviewed photos with provenance/permission
SUMMARY.txt                   readable findings, omissions and sharing instructions
```

Optional directories exist only if that scope was selected. Set limits before collecting: suggested maximum 256 files, 4 MiB per ordinary JSON/JSONL/binary input file, 32 MiB ordinary evidence total. Separate bounded raw-trace/media limits and opt-in flags; target ordinary report ZIPs below 8 MiB compressed. Show the actual size and account for the chosen sharing service's attachment limit rather than promising any arbitrary ZIP fits. If too large, offer removal of optional attachments or a separate supplemental bundle and explain the trade-off; never silently truncate evidence. Preserve large originals privately, retain their hashes/references, and do not claim a reduced export is complete.

Revalidate the actual bytes before ZIP creation: allowed file categories, relative paths, no links/reparse traversal, schema/value/timestamp bounds, per-stage target/source consistency, hashes and file count. Raw captures/media are not anonymous merely because the standard metadata is filtered.

## 10. Implementation sequence

1. **Preservation checkpoint and capability split.** Snapshot the intentionally dirty app outside the checkout. Add model availability/input/settings/UI capabilities; route the unavailable notices and fixed contacts/download link.
2. **X15 input backend and Studio.** Add and test the decoder/reader, explicit source selection, per-player ownership and disconnect handling. Remove unsupported X15 actions and replace empty inputs with live native data.
3. **Shared scanner core and worker.** Reconcile the published 1.1.0 collector with local work, package it into the app, implement the session/state/consent/timeout protocol and new report schema.
4. **In-app wizard.** Build all coverage stages, progress/repeat/skip/cancel, stage reasons, preview/history and result ZIP export. Make it reachable even for unavailable models.
5. **Website submission and private handoff.** Confirm/extend the X20CTLADMIN receiver schema/storage, implement upfront-authorized submission with receipts/idempotency/failure handling, then add the optional manual-sharing prompt, contact copy actions, report-folder access, mail draft and Discord launch instructions. Preserve pending reports and prevent accidental legacy upload routing.
6. **Verification and local Windows package.** Complete focused tests, production/type checks, isolated GUI acceptance and package-content checks. Physical X15 acceptance is a separate mandatory gate before calling hardware support verified. No release/push is part of this plan.

## 11. Acceptance criteria

- X15 selected for Player 2 or Player 3 connects to that player's explicitly chosen source and displays the actual input values. It never binds Player 1 by default or captures another assigned controller.
- All captured buttons/hat states/stick fields/trigger values match recorded/synthetic fixtures. Unknown frames are rejected; disconnect clears state and closes handles.
- X15 has no independent paddle-detection UI, RGB control, vibration-strength control or misleading configuration Apply/read/reset flow. Backend bypass attempts also fail.
- X20's verified configuration and current X20 Pro preview behaviour remain intact.
- Each other model opens its read-only controller section with the unavailable popup, Scan now/Scan later, working built-in scanner, public standalone-kit fallback and supplied contacts. Scan later/Escape produces no scan or submission.
- A packaged, offline x20ctl runs the scanner without an adjacent helper. No separate download, console questionnaire or Python installation is needed by the user.
- Every coverage row has an outcome/reason. Tests exercise all ordinary controls, each stick direction, slow/combined triggers, rear output association, cancellation/source loss and optional BLE/trace/media paths.
- Review/removal/export, stage/file/hash validation, interrupted sessions and too-large attachments are tested. No skipped/failed item becomes a positive capability claim.
- Email/Discord actions prepare the correct handoff and do not claim receipt or send automatically. Contact strings are exact.
- Website submission follows the disclosed per-session authorization and review, uses the exact approved bytes, and records a real receipt. No consent/decline/cancel path uploads data. Receiver mismatch, timeout, rejection and duplicate Retry are tested. The optional manual-sharing prompt appears after the website outcome.
- All UI/native bridge/worker tests use mocks/fixtures under the existing no-user-screen/no-controller restriction. Native WebView2 and physical acceptance are reported separately, not inferred from headless tests.
- A tester subsequently supplies real reconnect/input/directional/trigger evidence before the X15 scope is labelled hardware-verified. Other modes remain unverified until independently tested.

## 12. Required test groups and implementation files

Existing integration points: `src/App.tsx`, `src/controllers.ts`, `src/components/ControllerHub.tsx`, `src/components/ModelWorkspace.tsx`, `src/components/ReportPanel.tsx`, `src/native.ts`, `x20ctl/controllers/catalog.json`, `x20ctl/desktop/service.py`, `x20ctl/desktop/launcher.py`, `x20ctl/desktop/reports.py`, `x20ctl/desktop/scanner_integrity.py`, `x20ctl.spec` and platform packaging/docs.

Proposed new modules: shared scanner core/worker, research-session coordinator, X15 HID input adapter, registry capability tests, input decoder/ownership tests, scanner state/worker/report tests and isolated scanner/UI reviews. Reuse existing collector/trigger/research-boundary checks where applicable.

Run proportional checks after each implementation step. Plan creation itself does not require rebuilding the application or rerunning unrelated tests. The plan is based on inspected current code, the private capture review and the verified published kit/source/contacts; application implementation has not started in this planning step.
