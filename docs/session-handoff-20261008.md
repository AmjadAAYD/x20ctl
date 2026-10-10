# X20CTL session handoff — 8 October 2026

You are continuing an existing project. Treat the CURRENT WORKING TREE as the source of truth. Inspect git status, diff, relevant implementation and tests before edits. Do not reset, revert, checkout over, discard or overwrite intentional changes. Do not rebuild the app from scratch.

## Workspace and publication state

- Windows/PowerShell workspace: `C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl`.
- Branch: `codex/native-2.0-recovery`, tracking origin with the same name.
- HEAD: `f2c67d68d48158607bf3ec353d5acbceb8f9515f` — Release 4.1.0 Preview 2 with partial input support and guided research.
- Origin: `https://github.com/AmjadAAYD/x20ctl.git`; fork: `https://github.com/chriss80/x20ctl.git`.
- Desktop version: `4.1.0-preview.2`; integrated scanner `APP_VERSION = 1.2.0-app`. Imported standalone scanner provenance remains version 1.1.0.
- Public release, explicitly approved by the user: https://github.com/AmjadAAYD/x20ctl/releases/tag/v4.1.0-preview.2
- Direct download: https://github.com/AmjadAAYD/x20ctl/releases/download/v4.1.0-preview.2/x20ctl.exe
- Only uploaded asset: `x20ctl.exe`, 83,887,124 bytes. No application ZIP.
- SHA-256: `a2c9a08cf56e3a3a3b1636adedd3fe6de41e5ee828201f5a2339fcb54d5837e5`.
- Local release binary: `dist/4.1.0-preview.2/x20ctl.exe`.
- Release is a prerelease and immutable. Stable Latest remains 4.0.1. Earlier Preview 1 EXE is also public; its original ZIP release was removed at the user's request.
- Release source is on the branch/tag above. Do not assume default main contains the complete latest application implementation; main separately received a screenshot/README update.

## User instructions and priorities

- This is a WINDOWS DESKTOP APPLICATION. React/browser rendering is an implementation/preview mechanism, not the product.
- Do not use the user's screen. Prior work also avoided physical controller operations. Do not use either without clear new authorization; no physical controller is available for the agent's acceptance tests.
- Public app downloads must be EXE-only.
- Do not publish/release further changes without explicit new user approval. The last approval covered Preview 2, which is already published.
- Preserve the existing dark/navy visual language and intentional controller artwork. Do not redesign the entire app or restart earlier work.
- Do not invent hardware support or vendor commands, copy X20 packets to other models, fuzz HID/BLE outputs, flash firmware or add RGB work to this research phase.
- Keep updates concise. The user objected to long execution times; verify proportionally and avoid unrelated/repeated checks.

## Product and model scope

Visible models: X20, X20 Pro, X05, X05 Pro, X10, D10, X15.

The earlier X05/X05 Pro removal request was superseded: the user later approved additional previews and partial input support. Do not remove them based on old notes.

- X20 retains the existing configuration backend.
- X20 Pro remains preview/research; its protocol is unverified.
- D10, X15, X05 and X10 offer experimental read-only input Studios with Known/Missing information and explicit per-player source selection.
- X05 Pro remains unavailable/preview with Scan now / Scan later.
- Shared seven navigation sections: Buttons, Response curves, Macros, Vibration, Power & device, Input tester, Saved setups.
- Switch Controller returns directly to the four-player controller zone. Do not restore a separate Players navigation action.

## Important unresolved remapping issue

The user wanted actual button remapping for D10/X15/X05/X10. We explained that reading inputs is not writing controller assignments. Their remapping/configuration backends remain disabled, and this limitation was stated before publishing Preview 2.

- D10 receiver 2345:E062 has a public USB/HID dump. Vendor usage page FFA0 has input Report 6 and output Report 7. Their configuration semantics are unknown. Never synthesize Report 7 writes.
- X15 receiver 1A34:F517 is experimental owner evidence. The shared 0079:181C candidate HID layout and generic 045E:028E do not identify X15 uniquely.
- X15 owners report persistent KeyLinker changes and published d7f010e0/e1/e2 plus FF12/FF13/FF14/FF15 GATT attributes. This establishes transport/feature leads, not an X15 remap payload. Do not send X20 opcode 0x36 based on matching UUIDs.
- X10 manufacturer documentation describes Android app mapping via QMacro. Its exact GATT transaction/key encoding/ACK/readback protocol remains missing.
- X05 historical physical testing found no KeyLinker/feature-report configuration route on that tested unit. This is bounded negative evidence, not proof about every revision.
- X05 rear-button claims conflict: older findings describe M1/M2, current artwork/catalog has zero macro slots, and retrieved manufacturer support text did not corroborate M1/M2. Artwork/slots were not guessed or changed. Scanner asks owners to confirm their revision before those tests.

Before a future persistent write backend: require real model/revision/firmware/mode-specific baseline/change/restore transactions, source/target encoding, framing/counters/checksums, ACK/readback, and physical button/disconnect/power-cycle/persistence/restoration acceptance. Passing fixtures alone is insufficient.

## Implemented in Preview 2

Input:
- Reused explicit disconnect/reconnect inventory correlation and per-player binding; models/player changes invalidate discovery tokens.
- All four partial models accept correlated Windows XInput sources. X15 alone additionally accepts its narrowly matched candidate HID layout. Unknown HID layouts are raw evidence, not guessed live decoders.
- Standard controls map to input-test visualization: A/B/X/Y, D-pad, LB/RB, L3/R3, Back/View, Start/Menu, sticks and LT/RT values.
- Standard XInput exposes no documented independent M fields or Home/Guide field. Rear controls may emit ordinary assignments; independence remains unknown overall.
- Added negative guard excluding explicitly named X15/QMacro/X10/D10/X05 and Pro names from the legacy X20 writer path before driver construction. It is not positive device identification.

Scanner:
- Same-EXE worker mode `--research-worker` bypasses GUI/tray/normal device service.
- Selected-device USB descriptors where available, parsed HID caps, timestamped input reports and trigger sweeps.
- Original HID descriptor bytes may be unavailable through the Windows collector; parsed caps are explicitly not original descriptors. External enumeration traces remain a fallback.
- Repeated controls, baseline/cleared/A/B rear comparisons, optional macro playback/restoration and physical long/short trigger comparisons.
- Exact-layout, explicitly opted-in D10 vendor INPUT logging; no vendor output/feature reports.
- Short, separately confirmed standard XInput vibration pulses: 0.35 seconds, reset afterward, off/low/medium/high/max and low separate left/right. No stored strength writes or independent trigger-motor control.
- Optional owner-confirmed identities across modes, including documented firmware mode enumeration without an updater/flash.
- X15/QMacro discovery hints, standard-only GATT reads and dynamic characteristic handles; no unknown vendor reads/writes or subscriptions.
- Owner-led X15/X10 official-app baseline/change/readback/phone-disconnect/PC-check/power-cycle/restore timeline. Owner confirmations are not decoded protocol or completed hardware acceptance.
- Firmware/revision/app context associated with selected USBPcap, Windows HCI PCAP/PCAPNG (link types 187/201) and Android BTSnoop imports. Containers are validated, command meaning and target identity remain unverified.
- Consent and review precede sharing. Raw traces may contain sensitive data; nothing is automatically decoded into authorization to configure hardware.

## Current uncommitted work — NOT IN PREVIEW 2

Latest status inspection found additional work that was not created/verified during the release task:

Modified:
- `src/components/ControllerResearchScanner.tsx`
- `src/components/research-scanner.css`
- `tools/x15_scanner_review.cjs`
- `x20ctl/desktop/research_scan.py`

Untracked new files:
- `x20ctl/scanning/capability_discovery.py`
- `tests/test_capability_discovery.py`

The module describes passive analysis of captured KeyLinker-family exchanges. It correlates request serials/responses, CRC/length checks and menu continuations, and reports captured identity/features/source/destination codes. It contains HCI/GATT trace interpretation. It explicitly leaves model identification, configuration authorization and command verification false.

ResearchScanner currently calls `analyze_trace` after trace import, writes `attachments/capability-profile.json`, exposes detectedCapabilities to UI and clears derived claims when the trace/profile is removed. The UI and headless review script have corresponding additions.

Inspect this diff/module/tests before continuing. Do not claim it was tested, packaged or released based on Preview 2 verification. Preserve it; do not revert it or replace it with the tagged release state. Many older untracked plans/visual-validation documents also remain intentionally in the workspace.

## Verification actually completed for released state

- 294 focused Python tests passed, including 20 dossier-follow-up tests.
- TypeScript check and Vite production build passed; existing >500 kB chunk warning remains.
- Eight isolated headless flow groups passed, covering four input Studios, X05 Pro unavailable flow, scanner review/manual fallback and X20/Pro preservation at wide/compact sizes.
- Scoped Ruff and whitespace checks passed.
- Versioned PyInstaller build passed. Actual EXE worker capabilities returned hardware_access=false and scanner 1.2.0-app.
- Bundled affected Python modules, catalog and all frontend assets matched source. Windows FileVersion/ProductVersion were 4.1.0-preview.2.
- Uploaded GitHub digest matched the local EXE; published release contains exactly one asset, is non-draft/prerelease/immutable.
- No native GUI acceptance, actual controller input/remapping/motor test, real BLE discovery, external protocol capture or actual report transmission was performed.

## Relevant files

Architecture/source:
- `x20ctl/controllers/catalog.json`, `__init__.py`, `compatibility.py`
- `x20ctl/desktop/service.py`, `x15_input.py`, `research_scan.py`, `scan_worker.py`, `rumble.py`
- `x20ctl/scanning/ble.py`, `backend.py`, `windows.py`, `app_capture.py`, `analysis.py`, `guided.py`, `model_evidence.py`, `protocol_session.py`, `PROVENANCE.json`
- `src/controllers.ts`, `src/App.tsx`, `src/components/InputWorkspace.tsx`, `ControllerResearchScanner.tsx`, `StudioSections.tsx`
- Existing X20 protocol reference: `x20ctl/client.py`, `protocol.py`, `docs/00-findings.md`, `docs/01-protocol.md`. Other-model compatibility cannot be inferred from these alone.

Tests/evidence:
- `tests/test_partial_input_phase1.py`, `test_remapping_dossier_followup.py`, `test_research_scanner.py`, `test_controller_registry.py`, `test_desktop.py`, `test_reports.py`
- `tools/x15_scanner_review.cjs` (currently modified beyond release)
- `artifacts/release-preview2.json`
- `artifacts/remapping-dossier-followup/build.json`
- `artifacts/partial-input-phase1-review/`
- `docs/partial-input-phase1-validation.md`
- `docs/remapping-dossier-followup-validation.md`
- `docs/optional-external-protocol-capture.md`
- `RELEASES/4.1.0-preview.2.md`
- `docs/prompts/remapping-deep-research.txt`

Most-recent supplied texts:
- Execution instructions, read FIRST: `C:\Users\amjad\.codex\attachments\e69fd6a4-a49c-47d7-82b0-f7f94fe0ab32\Pasted text.txt`
- Research dossier, read SECOND: `C:\Users\amjad\.codex\attachments\125b474e-40ff-4869-a669-b8bc45d01eba\Pasted text.txt`
- Earlier research file: `C:\Users\amjad\Downloads\X20CTL_EasySMX_Controller_Research_and_Codex_Prompt.txt`

## Submission website and data boundaries

Companion repo: `C:\Users\amjad\Desktop\Study\Projects\Personal\X20ADMIN`.
Schema-2 receiver changes were prepared locally and passed 29 tests/build earlier, but were not deployed in these app releases. Automatic submission requires that receiver update. The previous live capability check returned 405; do not present that as refreshed current state. The app retains local ZIP/manual fallback.

Contacts: Discord `mistermajid`, email `aaydamjad@gmail.com`. Opening a composer/contact does not send a message. Do not send messages without authorization.

Never publish raw owner captures, private protocol research, vendor binaries or credentials. Received private X15 data is outside the public tree. Use `tools/check_research_boundary.py` before staging/publication. Snapshots exist under the prior automation's `checkpoints` directory, including phase1 and dossier follow-up source backups. Do not restore an old snapshot over the current intentional tree.

## Resume instructions

1. Read current git status/diff and the uncommitted capability-discovery implementation/tests.
2. Reconcile it with the dossier and the no-write/model-identification boundaries; no fresh task to enable remapping has been authorized.
3. If continuing that work is requested, verify against real capture provenance or mark fixtures as synthetic; preserve the frozen-public-release distinction.
4. Run only relevant tests/typecheck/build after changes. Do not reuse the 294-test release result as evidence for currently uncommitted changes.
5. Do not release/publish again until the user explicitly approves the new result.

Latest user request was to create this handoff for a new session. No additional feature scope was approved in that request.
