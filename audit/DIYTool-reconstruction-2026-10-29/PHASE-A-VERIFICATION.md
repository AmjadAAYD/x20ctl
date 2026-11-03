# Phase A — Screen first-run verification

Started 1 November 2026; final review 2 November 2026. Scope: close the first-upload gaps on `codex/diytool-screen-20261101`; retain the previous 24 full conversion matches and five compression captures without rerunning the vendor matrix. Owner authorization permits local commits and isolated vendor execution, not physical hardware writes in this task. No vendor program was relaunched in this phase. The task VM remains stopped. A fresh present-device query found no matching EasySMX device.

## Baseline and preserved work

- `d743ad8` Add Screen codec and captured protocol tests.
- `ac1c673` Add gated Screen artwork editor.
- Fresh baseline: **698 .NET tests passed**, **28 Python tests passed** (20 original + 8 capture checks). Logs: `execution-2026-11-01/phase-a-baseline-{dotnet,python}.log`.
- Transport/decoder changes are committed as `ee268a8` Verify Screen streams and guard uncertain transfers. All commits use Amjad AAYD, with no assistant/co-author trailer. No pushes.

## Claim register and first-run gate

| Gap | Status and evidence | Safe default | First physical check |
|---|---|---|---|
| Crop / scaling | **Derived:** 0x424960 uses inclusive selected bounds `(end-start)+1`, separate X/Y scale, translation by negative crop origin, QPainter render hint 4, and rounded zoomed draw dimensions. Full Qt tie-breaking and arbitrary WPF smooth-scaling equivalence are **unknown**. **Observed original output + measured WPF comparison:** all 24 retained source cases / 735 frames pass mean RGB error ≤3/255. | Native 160×80 procedural PNG-equivalent test pattern, no scaling, neutral adjustments. | Confirm four quadrants, orientation, edge placement, then test a scaled image separately. |
| Alpha / GIF | **Derived/implemented:** WIC rectangle composition, disposal 2 clear and 3 restore previous, black flatten, uniform centisecond delay. Original equal/unequal/disposal vectors remain retained. WPF transparent-PNG import is exercised offscreen. | Static frame; optional 5 FPS; explicit normalization notice. | Inspect transparency, later frame disposal and real timing on a short animation after static restore succeeds. |
| Odd padding beyond raw | **Unknown:** no original beyond-allocation odd-padding byte is claimed. A targeted 12,800-word vector yields a 38,311-byte odd LT candidate against 25,600 raw bytes. Regression requires raw type-0 fallback instead of inventing padding. The optional encoding diagnostic callback identifies it. | Raw little-endian GIF frame with supported descriptor; no unobserved padding used. | Export `.bin` and dry-run trace; compare the exact generated asset if the first pad trace exposes another representation. |
| Native RX fragmentation | **Implemented and tested:** the same stream parser used by Windows COM accepts 1/7/63/126-byte fragments and merged CRC progress/final replies. Fixed 63-byte framing is **derived**, not native-device observed. | Bounded exchange, wrong opcode/short/closed/bad CRC stops; no automatic retry. | Capture native RX lengths, trailing bytes, progress and final status. Stop on framing disagreement. |
| Timeout / cancellation | **Implemented:** indeterminate failure locks Upload; a new connection reads attempted asset and metadata CRC before Retry once. Unavailable readback does not enable retry. Editing does not clear this state. | At most one explicit retry after valid mismatch; Restore is explicit. | Verify actual command readback, disconnect behavior and cancellation boundaries. |
| Flash ID / branches | **Derived from supplied Flash.ini:** 38 rows; page-size column × page-count column supplies nominal size at object+F4 via 0x4044D0. **Observed:** captured EF4018 path uses 4B/6D/6C/6E. Other IDs/MCU/NAND/address branches are unsupported. | Fail before any 6C on an unknown ID. Keep captured model magic. | Show and save actual flash ID and handshake; do not substitute an emulated identity. |
| Reset / restore | **Observed:** Reset sends four FF bytes at 0x410000 and 4096 FF metadata at 0x400000, 11 calls, CRC query quirk retained. **Derived:** 6C mode alternates every 2048 bytes. **Inferred:** erase granularity is 4096 bytes. **Unknown:** bootloader physical erase extent and stock fallback. | No automatic Reset, explicit confirmation and visible Restore. No factory-image backup claimed. | Capture Reset, verify metadata cleared, restart/default screen, neighboring data and persistence. |
| Backup | **Unknown/unavailable:** supplied helper map contains flash ID and CRC query; no asset-byte read/dump routine was identified. 6E returns region CRC rather than bytes. | Do not pretend CRC is a backup. Save artwork/project, photograph old screen, preserve legitimate stock material; Reset is the documented restore workflow. | Confirm a manufacturer dump command if one exists; otherwise validate Reset fallback. |
| Independent re-derivation | **Passed:** fresh MSB polynomial long division 0x04C11DB7 with LSB-first input and reversed final register, no reconstructed-code imports, reproduces raw CRC; independently packed 4096 metadata, first data packet, CRC query and handshake checks match captures. | Any mismatch blocks; none found. | Compare these same bytes to the native trace. |

Emulator success proves software behavior only; it does not establish physical controller behavior.

## Image fidelity measurements

`phase-a/measure_fidelity.py` compares offscreen WPF RGBA crops to every frame of the original tool's saved cropped outputs. `phase-a/fidelity-24-cases.json` records per-case mean error, max error, PSNR and within-three-level percentage. `phase-a/fidelity-24-cases.png` shows original / WPF / difference ×8 for each case's first frame. The comparison uses black-flattened RGB before RGB565 quantization.

Acceptance was mean absolute RGB channel error ≤3 on each retained case, not maximum pixel error ≤3. Twenty cases are exact. Worst mean across all 735 frames is 1.9853/255; the JPEG case has maximum channel error 59 at edges and mean 1.7802, so it is not byte-identical JPEG decoding. The three long GIFs differ through source-vs-cropped palette conversion. The retained matrix has native-size images: this tolerance does not prove arbitrary scaled/cropped inputs or every future JPEG/GIF. The first-run pattern avoids both JPEG and interpolation. Re-derive new tolerance before calling a different crop/scaler Qt-identical.

## Native COM transport and exact planning

The Windows adapter delegates negotiation and parsing to `ScreenStreamTransport`, tested with the emulator byte channel. It does not clear input before each packet, so queued merged replies survive. It accumulates until 63 bytes, preserves excess bytes, rejects unexpected opcodes, accepts bounded CRC progress 7E30, and returns final status. Handshakes validate model-derived CRC; 6C validates status; 6E compares independently calculated asset/metadata CRC. Individual data ACKs have no invented response CRC.

Default exchanges are 5 seconds; CRC checks allow max(5, 25×ceil(regionBytes/1,000,000)) seconds. Underlying Windows reads have 100 ms polling timeouts and 5-second write timeout; deadline cancellation closes the handle. First-run identity action has a 10-second bound, readback recovery 100 seconds. Failure injection covers silent timeout, nine-byte short reply, closed port and corrupt readback CRC. The parser tests are run on Windows with an emulated byte channel; no native COM driver/device was opened in this offline task.

Dry run is pure construction and never opens a port. It includes the two opening 4B30 packets and the full upload pair of region transactions. Its frozen nonce is passed to both the COM opening negotiation and sender. A regression compares every planned byte to the complete emulator-channel write list. Changing artwork, mode or COM name invalidates the plan. Local JSONL serial traces distinguish attempted writes, completed API writes and actual RX chunks; they do not invent Windows syscall fragmentation.

## Reset extent and restore semantics

No standalone sector-erase command is inserted by the new feature. The original common-path program helper 0x4141B0 alternates mode 0/1 with 2048-byte payloads. Its default two halves imply a 4096-byte erase/program cadence; the emulator models this. For Reset this predicts 0x410000–0x410FFF plus 0x400000–0x400FFF affected sectors, although only four content bytes are explicitly sent. The exact controller-side erase extent cannot be proven from a host executable or emulator and remains a first-run check.

Reset does not send a packaged stock wallpaper or dump the old one. Its success text requests restart/default content; the actual fallback belongs to controller firmware. The UI's Restore stock action is that explicit Reset workflow. If physical fallback fails, stop and preserve logs; do not issue unrelated erase/firmware commands.

## Supplied flash table and supported path

Only EF4018 is enabled. Other ordinary NOR IDs might share host framing, but that is insufficient to allow writes. The original capacity branch requests 6F parameter 1 for 32/64 MiB and parameter 2 for larger/NAND profiles on eligible MCUs; those branches and alternative VCOM programming remain disabled. The same-ID model/profile gate also remains explicit. The table below is static configuration evidence, not connected-chip detection.

| Flash ID | Declared name | Numeric columns | App status |
|---|---|---|---|
| 0xEF4013 | W25Q40CLE | 256 × 2048 | Unsupported: unobserved NOR ID |
| 0xEF4014 | W25Q80DV | 256 × 4096 | Unsupported: unobserved NOR ID |
| 0xEF4015 | W25Q16DV | 256 × 8192 | Unsupported: unobserved NOR ID |
| 0xBA6015 | W25Q16DV | 256 × 8192 | Unsupported: unobserved NOR ID |
| 0xEF4016 | W25Q32FV | 256 × 16384 | Unsupported: unobserved NOR ID |
| 0x856015 | P25Q32H | 256 × 8192 | Unsupported: unobserved NOR ID |
| 0x856016 | P25Q32H | 256 × 16384 | Unsupported: unobserved NOR ID |
| 0x221752 | T25S64 | 256 × 16384 | Unsupported: unobserved NOR ID |
| 0xEF4017 | W25Q64FV | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0xBA3217 | ZD25Q64B | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0xF83217 | FM25Q64B | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0xEF6017 | W25Q64 | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0x856017 | P25Q64H | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0x5E4017 | ZB25VQ64 | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0x684017 | ZB25VQ64 | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0xEF4018 | W25Q128FV | 256 × 65536 | Captured NOR |
| 0xEF6018 | W25Q128JW | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x856018 | W25Q128JW | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x5E4018 | ZB25VQ128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0xC84018 | GD25VQ128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x0B4018 | XT25VQ128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x852018 | ZB25VQ128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x201985 | PY25Q256HB | 256 × 131072 | Unsupported: 32-bit address branch |
| 0x684018 | BY25Q128AS | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x522217 | NM25Q64 | 256 × 32768 | Unsupported: unobserved NOR ID |
| 0x522118 | NM25Q128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0x1C7018 | EN25QH128 | 256 × 65536 | Unsupported: unobserved NOR ID |
| 0xEF4019 | W25Q256FV | 256 × 131072 | Unsupported: 32-bit address branch |
| 0xC84019 | GD25Q256 | 256 × 131072 | Unsupported: 32-bit address branch |
| 0xEF4020 | W25Q512JV | 256 × 262144 | Unsupported: 32-bit address branch |
| 0x0B4019 | XT25Q256 | 256 × 131072 | Unsupported: 32-bit address branch |
| 0xC22019 | MX25Q256 | 256 × 131072 | Unsupported: 32-bit address branch |
| 0xEFAA21 | W25N01GV | 2048 × 65536 | Unsupported: NAND/address mode |
| 0xEFAB21 | W25N02GV | 2048 × 131072 | Unsupported: NAND/address mode |
| 0xEFAA23 | W25N04GV | 2048 × 262144 | Unsupported: NAND/address mode |
| 0xC891C8 | W25N01GV | 2048 × 65536 | Unsupported: NAND/address mode |
| 0xC892C8 | W25N02GV | 2048 × 131072 | Unsupported: NAND/address mode |
| 0xC895C8 | GD25F4G | 2048 × 262144 | Unsupported: NAND/address mode |

## Finished product scope

The X20 Pro profile explicitly declares `HasScreen`. Device entry and page navigation use it. The page now includes file/drop import, six own procedural wallpapers, fit/fill, rotation, zoom/pan, brightness/contrast, low-default FPS, frame count, live input/asset size, preview decoded from actual encoder output, binary export/import, versioned source/settings projects, user-accessible Simulate, progress/cancel, CRC-check recovery and one explicit retry. Restore is pinned and requires confirmation. First-run steps preserve the visible flash ID, list devices/ports without selecting one, save exact dry runs, use a native test pattern, record before/after and elapsed time, and retain local byte traces. There is one documented owner setting, off by default: X20CTL_SCREEN_WRITES=1.

No additional dependency was introduced in this phase. System.IO.Ports was already part of the preserved baseline. No AULA or vendor implementation code, firmware, executables or DLLs were copied into the product.

## Checks and artifacts

- Final domain suite: **718 passed, zero failed**, `phase-a/dotnet-final.log`.
- Product build: **0 errors, 76 warnings**, `phase-a/product-build.log`. Warnings are existing nullable/unawaited/unused fields and WinForms DPI warning; exact messages remain in the log and warning index.
- Offscreen review: empty, edit, preview, uploading, error, restored, Simulate, first-run check at 96/144 DPI (100%/150%). Default and Rebel outputs are under the feature worktree's `artifacts/screen-feature/{default,rebel}`. The harness asserts pinned actions stay inside the viewport, actual editor visibility, project roundtrip, alpha flattening, cancellation, no automatic retry, retry cap and restore.
- Renders are WPF element renders from a hidden HwndSource, not the owner's desktop. Other themes and real monitor DPI changes were not visually certified.
- Git diff whitespace check must pass before final commits. No pushes or hardware runs.

## Only the pad can confirm

Actual model/firmware/USB mode and flash ID; native receive frame size and timing; side effects of negotiation; erase/program extent; physical orientation/colors and GIF timing; persistence; reset/default fallback; reversibility; safe cancellation; and battery consumption. The exact ordered checks and expected bytes are in HARDWARE-TEST-PLAN.md. These are the remaining physical checklist, not claims of an unimplemented offline simulator.

Final UI checks also verify that the pinned Upload action cannot bypass first-run Before/identity/dry-run gates, and that late progress reports from an earlier transaction cannot update a later one. The review completes the confirmed test-image upload in Simulate.

Final review/build output is `artifacts/screen-feature/build/`: the running normal-output app locked its executable, so it was left alone and a separate output folder was built successfully (0 errors / 76 warnings). The 32 final PNGs were inspected as state sheets plus full-size first-run/error views. `phase-a/VERIFICATION.json` and `WARNINGS.md` record exact outcomes. UI commit: `995d277` Complete Screen editing and first-run checks.
