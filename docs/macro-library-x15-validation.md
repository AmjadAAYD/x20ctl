# Macro library and X15 preview — 2 October 2026

## Delivered

The shared Macro Studio now has a named sequence library independent from complete setups. It saves a copy, loads into the selected slot, copies into another slot, imports/exports versioned JSON, removes a library entry, and applies fixed or zero pauses while preserving holds and inputs. Desktop imports/exports use native file dialogs. The separate Windows library file is `%APPDATA%/x20ctl/desktop/macro-library.json`; Linux uses the existing app data directory. The browser-only development fallback uses a separate local-storage key.

Imports and saved sequences require the existing 5 ms grid and no more than 47 encoded entries (nonzero pauses consume entries). Invalid inputs, repeated keys, unsupported options such as toggle modes, unknown source models, nonfinite values, bad directions, and oversized files are rejected. A corrupt library is reported instead of being overwritten. Each copied/loaded step receives a fresh identity. These operations never detect or write hardware.

X20 recording now targets the selected macro slot, keeps that destination if the user changes slots during recording, captures both eight-way stick headings, and exposes Select/Start in the shared editor. Updating a loop interval no longer overwrites newly loaded/copied steps. Native X20 macro encoding and write commands are unchanged.

X15 is a backend-free preview in the player selector and shared Studio. It includes full front/rear artwork, a stickless base with opaque stick caps, button-shaped input regions, shoulder selection that switches to the back, M1/M2 macro navigation, grip-contour vibration with the existing speed curve, restrained lighting, shared sliders, Support/help actions, and the new macro library. Rear M2 is on the viewer's left, M1 on the right. The sticker was removed; existing labels are retained without duplicate overlaid text. Navigation now has accessible labels and hover titles when narrow layouts hide the text.

Official references:
- https://www.easysmx.com/products/easysmx-x15-pc-controller-with-rgb-light-and-hall-joysticks
- https://www.easysmx.com/pages/support-about-easysmx-x15-controller

The official documentation establishes physical controls and advertised features, not an X20CTL software protocol. X15 transport/configuration writes remain unavailable. The 47-entry preview editor format does not establish the X15 firmware's capacity. Hold/toggle/on-release playback, conditional macros, arbitrary analog playback, host game-input output, and cross-model hardware compatibility remain unverified and were not implemented.

## Artwork

Built-in image-generation/editing tool used with official X15 references. Saved assets:
- `src/assets/controllers/x15/controller.png` — complete front render
- `src/assets/controllers/x15/controller-base.png` — two stick caps removed, original rings retained
- `src/assets/controllers/x15/controller-rear.png` — rear render with central sticker removed

All three are 1536×1024. Geometry/framing/lighting share that source plane. Prompts and provenance are recorded in `docs/x15-image-prompts.json`. Other controller photographs were preserved; the outline review verified the existing front-art hashes.

## Verification

- Frontend typecheck and production build passed.
- 247 focused Python tests initially passed across library, desktop adapter, and model registry guards; the final library suite has 30 passing tests after adding strict unsupported-option rejection. The existing protocol macro subset also passed (15 tests).
- 19 focused JavaScript tests passed for macro validation/copy/pause behavior and all seven models' rear/haptics definitions.
- The isolated outline review passed all seven models, 172 control/macro checks, and 45 layout cases, including measured photo-plane alignment, complete shell margins and existing-art preservation.
- The isolated library review passed all six macro-capable Studios, assigning Player 3, saving/loading/copying/importing/exporting/deleting, preserving hold/loop values, and checking 1400/800px dialogs. It verifies X20 recording destination retention using a fixture, plus opaque glow-free X15 cap layers and X15 100%/0% vibration states. The bridge records no scan/connect/apply operations. Reports/screenshots: `artifacts/macro-library-x15` and `artifacts/control-outline-repair`.
- Separate Windows build: `dist/local-macro-library-x15-20261002/x20ctl.exe`. Its archive is compared with the current production frontend and registry, and checked for the native macro-library module. Evidence: `artifacts/macro-library-x15/local-desktop-build.json`.

No host screen, native application window, physical controller, release, push, or publication was used. Actual WebView2 playback and physical firmware execution are not verified in this run. The earlier Linux installer was preserved and was not rebuilt with these additions.
