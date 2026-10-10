# Rear controller views — 2 October 2026

Implemented Front View / Back View buttons on the Buttons page. The Macros page
is fixed to Back View, with no view switch or front artwork, following the user's
latest request.
All six existing models have a full 1536×1024 rear artwork plate with blue-left
and amber-right lighting. X05 Pro uses a slightly pitched rear view to expose
its two top programmable buttons; the other models retain symmetrical rear views.
X20 Pro's edit uses the user's supplied rear image.

| Model | Macro hotspots | Rear orientation |
| --- | --- | --- |
| X20 | M1–M4 | Straight rear |
| X20 Pro | M1–M6 | Supplied rear reference |
| X05 | None | Straight rear |
| X05 Pro | M1–M2 | Slight tilt, top controls visible |
| X10 | M1–M2 | Straight rear |
| D10 | M1–M2 | Straight rear |

HTML buttons follow normalized polygon outlines traced against each final image.
The photograph and hotspots share one image plane. Hover, focus, selection and
pointer hit areas follow the physical control's outline and angle. Clicking a
rear control on Buttons opens that player's Macros page with the corresponding
slot selected; clicking one on Macros selects that editor slot. Preview models
retain local drafts and unavailable hardware application. No native operation
was added for rear views, navigation or slot selection.

## Verification

- Production UI build: `npm run build` passed.
- Type check: `npm run lint` passed.
- Existing front input preview tests: 2 passed.
- Rear asset, polygon and label orientation tests: 7 passed.
- Controller registry/backend boundary tests: 167 passed.
- `git diff --check` passed.
- Isolated headless production UI review: all six models passed at 1400×940 and
  1060×760. All 16 rear controls were clicked through to their matching macro
  slots. Keyboard selection passed, and Macros was verified to have only rear
  artwork with no Front View / Back View switch. Buttons retains both views.
- No horizontal overflow or runtime errors occurred. Maximum measured hotspot
  position discrepancy against the shared image plane was below 0.03 CSS pixels.
  This measures responsive placement; visual outlines were separately inspected
  in offscreen renders against the edited artwork.
- SHA-256 checks confirmed all 18 protected front complete/base/geometry files
  were unchanged.

Evidence: `artifacts/rear-views-review/report.json`, per-model normal/compact
rear screenshots, selected-macro screenshots and `x20_pro-macro-layout.png`.
The review uses a mocked bridge: existing X20 polling receives disconnected
fake input, and preview models make no input/bootstrap requests. It accesses
neither a physical controller nor the user's screen.

The actual Windows desktop runtime was not launched, controlled or captured,
following the user's prohibition on screen access. This does not establish
WebView2 runtime behavior or any physical controller/protocol functionality.
X20 Pro and the other preview models still require verified hardware research.
No release, push, commit or publication was performed.

## Local Windows executable

A separate local desktop build is now available at
`dist/local-macros-back-only-20261002/x20ctl.exe`, with its newly built
`x20ctl-scanner.exe` beside it. Existing release executables and archives were
not replaced. PyInstaller completed successfully; inspection of the executable
archive confirmed all six rear PNGs are bundled. The packaged host's
`--verify-scanner` check passed without opening a window or executing a scan.
The source scanner identity was restored byte-for-byte after packaging.

Evidence: `artifacts/rear-views-review/local-desktop-build.json` and its two build
logs. The local executable is 59,009,218 bytes (about 56.3 MiB), exceeding the
existing release workflow's 40 MiB ceiling. It is a local preview, not a validated
release package. Desktop UI/runtime verification still requires opening this
build manually; no screen or controller access was performed during packaging.

The back-only follow-up passed type checking, the production UI build and the
six-model offscreen UI review. All macro-capable models retain their clickable
rear controls with no front artwork or view switch on Macros. Existing rear
asset/geometry tests remain unchanged from the preceding passing run.

Reproduce the offscreen review with a locally installed Playwright package and
browser, for example:

```powershell
node tools/rear_views_review.cjs --playwright C:/Users/amjad/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright --browser 'C:/Program Files/Google/Chrome/Application/chrome.exe'
```

## Artwork and references

Images are edited illustrations based on source references, not untouched product
photographs. Runtime assets and geometry are stored together in
`src/assets/controllers/<model>/controller-rear.png` and `rear-geometry.json`.
Exact built-in imagegen prompts: [rear-controller-image-prompts.json](rear-controller-image-prompts.json).
Original references and source JSON are retained locally in
`artifacts/rear-controller-sources/` and `artifacts/front-controller-sources/`.

- [X20 manufacturer reference](https://www.easysmx.com/products/easysmx-x20-multiplatform-gaming-controller-with-trigger-lock-and-hall-effect-joysticks)
- [X20 Pro manufacturer reference](https://www.easysmx.com/products/easysmx%C2%AE-x20-pro-wireless-multiplatform-gaming-controller)
- [X05 manufacturer reference](https://www.easysmx.com/products/easysmx%C2%AE-x05-multiplatform-gaming-controller-with-hall-effect-joysticks)
- [X05 Pro manufacturer reference](https://www.easysmx.com/products/easysmx-x05pro-multiplatform-wireless-gaming-controller-with-noise-canceling-button)
- [X10 manufacturer support diagram](https://www.easysmx.com/pages/support-about-easysmx-x10-controller)
- [D10 black manufacturer reference](https://www.easysmx.com/products/easysmx-d10-multiplatform-gaming-controller-black-version-tmr-sticks-trigger-lock-and-charging-dock)

[Editable Figma artwork and outlines](https://www.figma.com/design/ZGgmvZAyNmSuSGdSt5YGxT)
use the same six PNGs and normalized button geometry as the app.
The separate [Canva X20 Pro lighting draft](https://www.canva.com/M/MAHW27CSJiY?utm_source=OC-AaBlKrqBJJNF&utm_campaign=agent_connector_create_image_asset_opened)
was reviewed; the app uses the full-resolution built-in imagegen edit.
