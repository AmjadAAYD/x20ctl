# Trigger hinge and LT repair

The original downward press was restored after clarification. The current moving caps use solid 3D geometry; see [trigger-solid-validation.md](trigger-solid-validation.md). The intermediate flat/outward-direction experiments were superseded.

## Research and mechanical model

The earlier motion translated the whole cap. That does not represent a lever. The replacement projects rotation around a stationary, body-side shaft: the free/finger edge changes angle and travels most, while points on the shaft stay fixed. The source photo is retained, the shell/collar stays stationary, and input directly determines the angle.

Primary reference photographs in [iFixit's Series controller disassembly, step 7](https://www.ifixit.com/Guide/Xbox+Series+X%7CS+Wireless+Controller+(Model+1914)+Full+Disassembly/148234) show the trigger's shaft and return spring. [EasySMX's X20 product information](https://www.easysmx.com/products/easysmx-x20-multiplatform-gaming-controller-with-trigger-lock-and-hall-effect-joysticks) confirms Hall-effect analog travel and two trigger-lock positions. These support the lever mechanism and analog representation; they do not provide measured EasySMX hinge locations or travel angles. The photo-space shafts and angles in this UI remain approximations. No hardware protocol or physical calibration is claimed.

## Independent LT defect

Six models stored LT as an RT path plus a mirror transform. The renderer ignored that transform. Both sprite masks therefore pointed at RT, and duplicate even-odd cutout paths cancelled each other, leaving the original cap underneath. X15 already had separate paths.

The six LT paths are now normalized into their correct source-photo coordinates. Cutouts, sprite masks, feedback, and physical click targets use the same geometry. Original controller images and socket plates were not changed.

## Verification

- New independent-side image comparisons reproduced the old failure on all six affected models: LT changed zero pixels on its own side. Evidence is retained in `artifacts/trigger-hinge-before/`.
- All eight trigger tests passed. They verify stationary hinge endpoints and midpoint, increasing free-edge displacement, reduced movement near the shaft, released registration, mirrored movement, clamping, and independent cutout paths.
- Frontend type checking and production build passed.
- Isolated production UI review passed all seven models: intermediate positions, full/released states, both sliders, X20 fixture input/disconnect, fourteen resize cases, and original photo hashes.
- Existing control-selection review also passed all seven models after normalizing LT geometry, including shoulder selection and macro navigation.
- All fourteen LT-only/RT-only pixel cases passed. Labels are hidden during these checks so a changing badge cannot substitute for cap movement. At a difference threshold above 8/255, each active region changes more than ten pixels and each inactive region stays within the two-pixel antialiasing allowance.
- Rendered released/half/full states were reviewed for all models; separate RT and LT cycles were captured for X20. The demonstration GIF is synthetic slider playback, not physical controller playback.
- Rim and released-photo checks passed with their documented thresholds. Original photo comparison differs above 20/255 in at most 0.178% of released pixels, mostly at clipped sprite seams.

Current reports, screenshots and demonstration: `artifacts/trigger-hinge-review/`. The local Windows executable's bundled frontend/catalog and hash are checked in `local-desktop-build.json`.

The native window and actual hardware were not accessed, honoring the user's screen/controller restriction. Other models remain offline previews; no speculative writes, release, push, or publication occurred.
