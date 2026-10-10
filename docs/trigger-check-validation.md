# Small X15 trigger capture kit — 6 October 2026

Standalone Windows x64 console utility, separate from the x20ctl application. It supports documented XInputGetState and the selected 0079:181C / gamepad usage 1:5 / 10-byte input, 5-byte output, zero-feature HID profile from the received X15-labelled capture. This profile is shared and is not model detection. Unknown layouts and mixed input sources are rejected rather than guessed.

The tester consents to local raw-input capture, identifies the target by disconnect/reconnect and explicit selection, then records neutral and separate LT/RT tests. Timed released/quarter/half/three-quarter/full/release instructions encourage slow pulls and holds. About 35 seconds of input is captured. Reports distinguish intermediate values, endpoints-only observations, missing samples, unreleased baseline, mixed-trigger movement and incomplete full pulls. Partial observations do not establish physical sensor type or a configuration protocol.

Results are fresh per-run folders/ZIPs containing JSONL samples, selected-source metadata, mode/model claims, summary and SHA-256 manifest. Ctrl+C/capture failures preserve collected input with incomplete status. The host sample sleep is 20 ms; this is not a latency/polling-rate measurement. HID reads use managed async buffer ownership and bounded cancellation. No vendor output/feature/configuration API, firmware operation, network call, full device-path export or serial-number request exists.

Executed checks:

- 10 focused offline tests passed: synthetic gradual/binary HID and XInput values, missing samples, unknown layouts, invalid ranges, cross-trigger contamination, held baseline, mixed sources, and actual result ZIP content/hash verification.
- Python build/test helpers passed scoped Ruff checks.
- The compiled shipped EXE passed `--self-test` with `hardware_access:false`. A declined interactive run exited without enumeration or result creation.
- The shipped EXE's `--analyze` reproduced endpoints-only LT/RT conclusions from the previously reviewed X15 files, with 244 LT and 243 RT samples. No received recordings or personal submission metadata were included in the kit.
- All seven distribution ZIP entries byte-match the built files. Source, compiler and distribution hashes are recorded in the build artifacts.

Kit: `artifacts/trigger-check-20261006/X15-Trigger-Check.zip`, **26,924 bytes**.
SHA-256: `5c2a653256b4b832f34b07ccc73c0cd2621ea202d7c7fd5ab64d614ab0829f39`.
Evidence: `verification.json`, `offline-replay.json` and packaged `technical/BUILD_INFO.json`.
Rebuild: `python tools/build_trigger_check.py --output NEW_OUTPUT_DIRECTORY`.

Live HID/XInput collection, reconnect selection, device cancellation, and clean-machine runtime availability remain untested with physical hardware. No user's screen/controller was accessed. The existing app, previous builds and scan kit were preserved; nothing was pushed or publicly released.
