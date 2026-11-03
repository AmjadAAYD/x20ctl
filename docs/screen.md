# Screen artwork

Open **Device → Screen artwork** on X20 Pro. Uploads go to the controller. Tick **Simulate** to use local memory instead.

To upload: switch the controller off, hold **M** and plug it in by USB. The white light comes on and Windows adds a "USB Serial Device" port (`VID_EEEF&PID_268A`). Press **Upload**; X20CTL finds that port itself, waits for it if needed, then gives the screen chip three seconds before connecting. After a stopped transfer, unplug and plug in again holding M before retrying.

Choose or drop a PNG, JPEG, BMP or GIF up to 2 MiB, or choose one of six built-in wallpapers. Fit/Fill, rotation, zoom, horizontal/vertical position, brightness and contrast edit a 160×80 image. The live meter shows the generated asset against the 3 MiB limit. Preview is decoded from the generated binary in display orientation.

Wallpapers are static by default. Enable Animation for GIFs, choose FPS and frame count, then Play. The default is 5 FPS. All retained frames use the selected uniform delay; unequal source delays are normalized. Transparency is flattened on black. Playback stops after 30 seconds or when leaving Screen. Upload sends one finite asset; it does not stream images or poll the pad in the background. Brightness here edits pixels, not the pad backlight or RGB lights.

**Save project** stores the source and edit settings in `.x20screen`; **Open** restores it. **Export .bin** saves the actual encoded asset. Binary files do not carry playback delay: use a project to retain it. Opening a binary permits editing/re-encoding; it is not an arbitrary flash-file loader. Six procedural wallpapers and the first-run test pattern are our own generated pixels.

## First controller run

Uploads were verified on the owner's X20 Pro on 3 Nov 2026 (screen chip LT165, flash 0x5E4017): every packet acknowledged, both CRCs read back on a fresh connection, and the GIF seen on the pad. **Restore stock** stays simulate-only: it has never been sent to a Pro. No driver installation is performed by Screen. The steps below are the original first-run record.

1. Open **First-run check**, which prepares a static native-size test pattern. Record the current screen and the controller firmware/mode. Turn Simulate off.
2. Press **Detect**. Present controller-like interfaces and available COM names are listed. Type the controller's COM name explicitly. Device names and ports are not automatically associated or selected.
3. Press **Read flash ID**. This negotiates the captured connection and reads identity, without an image-program request. The ID remains visible. Only captured NOR ID `0xEF4018` is enabled; others are refused. It uses the captured LT7589 handshake profile. This is not a new Pro identity claim.
4. Press **Dry run**. No port is opened by this action. The exact opening and upload frame list is saved locally. The nonce is retained for the next upload so the planned bytes match the sender; changing artwork, port or mode invalidates the plan.
5. Check **Before**, then **Upload test image**. Inspect the pad: top-left red, top-right green, bottom-left blue, bottom-right white. Confirm orientation, colors and persistence after restart. Check **After** and Record result only after observing the physical screen.
6. **Restore stock** remains visible. Select Confirm reset, then press it. It performs the original tool's Reset: four FF content bytes and FF metadata, followed by CRC checks. Restart and inspect the stock fallback. It does not write a backup or packaged factory image.

Local first-run plans, identities, results and native COM traces are saved under `%LOCALAPPDATA%/X20CTL/Screen/FirstRun`. Traces contain timestamped `tx-attempt`, `tx-complete`, and received chunks. A failed write may have sent a partial buffer even without `tx-complete`. Review them before sharing.

## Failure and restore

No data command is retried automatically. A timeout or cancellation is indeterminate. **Check transfer** opens a new connection and queries CRCs of the attempted asset and metadata without programming. Matching data completes the check. A valid readback mismatch enables **Retry once**; unavailable readback leaves retry disabled. Changing artwork does not remove the check requirement. Restore is always an explicit Confirm reset / Restore stock action. Reset failure does not cause an automatic reset or upload.

No asset-dump command was identified in the supplied tool. CRC readback cannot save the old asset bytes. Photograph the current screen, preserve legitimate stock material and use the documented Reset fallback; do not call an uploaded asset a backup. The exact physical erase extent and fallback behavior belong to the first-device check.

The offline simulator exercises software behavior; only a physical run establishes native reply framing, on-pad effect, persistence, restoration and battery use. See `audit/DIYTool-reconstruction-2026-10-29/HARDWARE-TEST-PLAN.md` and `PHASE-A-VERIFICATION.md` for evidence and the ordered capture checklist.
