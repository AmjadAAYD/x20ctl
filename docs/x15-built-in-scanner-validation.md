# X15 input and built-in scanner — 7 October 2026

Implemented locally from the approved plan, preserving the existing intentionally dirty working tree and controller artwork.

## App behaviour

- X15 has a dedicated experimental, read-only input Studio with Buttons, Input tester and Device & connection. Native discovery compares disconnected/connected devices, then the user explicitly selects a candidate HID/XInput source for the active player. Inputs use the observed 10-byte layout or documented XInput data. Unknown frames are rejected, source loss clears cached input and a source cannot be simultaneously owned by another player.
- The live display uses actual trigger values, without inventing intermediate readings. HID stick orientation and physical receiver link status remain explicitly qualified. Player 2 is not relabelled Player 1. Independent M1/M2 indicators, RGB, vibration strength and unverified hardware configuration editors are absent from the X15 Studio.
- X05, X05 Pro, X10 and D10 open their controller preview with a popup offering Scan now / Scan later. Scan later/Escape leaves the preview without starting collection. Scan now explains collection and website submission before opening the guided scanner.
- X20's existing configuration path and the X20 Pro preview remain available. All model sections and the player landing screen have scanner access. Existing theme, four-player startup and centered launch policy are retained.
- The scanner runs its Windows collection worker from the same x20ctl executable. No adjacent scanner EXE or scanner download is needed. The published 1.1.0 collector was imported into a new `x20ctl/scanning` package; existing local kit files were preserved. Original upstream file/blob hashes are in PROVENANCE.json; worker routing and capture progress/durability are deliberate integration adaptations.
- Guided stages cover target metadata, standard USB/HID information, buttons/D-pad/clicks/extra controls, individual stick directions, slow partial/full LT/RT pulls, combined inputs, optional rear-button outputs/turbo/motion, BLE standard battery/device information, owner observations, non-RGB app experiments, traces and photos. Every coverage category has an outcome/reason. Unsupported vendor values and the original HID descriptor remain unavailable rather than fabricated.
- Raw input/trace/media are scoped and reviewed. Review can open local files and remove optional attachments. Cancelling keeps partial files locally without submission. Finish requires review and creates a validated/hash-backed schema-2 ZIP.
- Website submission requires upfront authorization plus completed review. It preflights receiver schema support, sends the immutable reviewed bytes, records a real receipt or preserves the ZIP with an explicit failure, and retries only on user action. Local-only scope never submits. Manual email/Discord sharing is offered after the outcome, with the supplied contact strings, compose/launch and copy actions. Opening a draft does not claim delivery.

## Receiver changes and deployment boundary

Local companion changes in `../X20ADMIN` add schema-2 capability advertisement and private intake with 8 MiB compressed / 32 MiB expanded / 256-entry limits, per-file size/type/path/CRC/hash checks, consent/review and raw-scope checks. The old compatibility-report contract remains supported. Retry identity remains idempotent; tests confirm a stable receipt and one stored record for identical retries.

**Not deployed.** The live `https://x20-admin.vercel.app/api/controller-report` GET returned **405** during this implementation. It does not advertise the new schema. The app therefore preserves the report and explains that the receiver needs its update; no report was posted during verification. Live automatic submission remains blocked until the prepared receiver changes are deployed and real storage/receipt behaviour is verified. No push, deployment or public release was performed.

## Executed verification

- Focused Python regression run: **271 passed, 1 skipped** across research scanner, desktop adapter, reports, registry, launch geometry and Linux boundaries. Includes fixture end-to-end collection/review/upload/failure/retry, per-player ownership, unknown input rejection, and cancellation/declined scope.
- Companion receiver: **29 tests passed**, including research archive/consent/hash handling and the actual HTTP route with private fake storage and idempotent receipt.
- TypeScript checking and final Vite production build passed. Existing large-JavaScript-chunk warning remains.
- Companion Next.js production build passed; exit code and log are under `artifacts/x15-scanner-review/receiver-build.json` and `receiver-build.log`.
- Scoped Ruff, review-script syntax and whitespace checks passed.
- Final isolated production GUI review passed **eight flow groups**: X15 Player 2 input, removed controls, wide/compact sizes, all four unavailable-model popups and no-scan Scan later paths, review-gated Finish, failed-submission ZIP/manual email, and preserved X20/X20 Pro wide/compact navigation. Bridge calls were fixtures; no external email, Discord or website submission happened.
- PyInstaller built the final Windows GUI executable. That **windowed executable itself** successfully ran `--research-worker` over redirected pipes, reporting `hardware_access:false` and `same_executable:true`. This confirms the packaged internal worker starts without a helper or native UI.
- Extracted code objects for thirteen app/collector modules equal current source. All **34** embedded frontend files and the registry match the final production files. All **28** controller PNG assets match the pre-change checkpoint.

No user screen, physical controller or live native window was inspected. These checks do not establish physical X15 compatibility, all connection modes, BLE service availability, real HID cancellation behaviour or a deployed receiver's storage configuration.

## Deliverable and evidence

Final executable: `dist/local-x15-scanner-final-20261007/x20ctl.exe`.
Size: **83,118,027 bytes**.
SHA-256: `9ed19e20985441569b0e73e07067c9179d74ff26be6767495782fec71f275918`.

Evidence directory: `artifacts/x15-scanner-review`; final packaging record: `final-build.json`; GUI report: `report.json`. Earlier builds remain available.

App checkpoint: `C:/Users/amjad/.codex/automations/context-recovery-previous-x20ctl-session-called-build-easysmx-controller-p/checkpoints/x15-scanner-20261007T113524Z` (476 source/built-frontend files, hashes, Git diffs/index). Companion receiver checkpoint: `.../checkpoints/receiver-20261007T130932Z`.

Before labelling X15 hardware support verified, obtain a physical reconnect/button/stick-direction/trigger run in the intended mode. Configuration, firmware macro/curve writes, RGB and vibration-strength programming remain unsupported for X15.
