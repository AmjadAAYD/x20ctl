# Trigger motion correction after recording review

The supplied 10.9-second recording showed the X20 cap flattening and revealing a dark crescent below it. The previous implementation scaled the face vertically by up to 16% (24% for X15), rotated it, and allowed it to move beyond its aperture. Its checks proved input-driven transforms, not convincing mechanical motion.

## Correction across all seven models

- Remove cap scaling and rotation. Preserve the original face proportions.
- Use restrained rigid travel into a fixed, photo-matched aperture, with each model's travel sized to its trigger face. Both the original socket and moving cap are confined to that opening; the shell and collar do not move.
- Add a gradual, localized depth shadow under the upper lip instead of a colored press fill.
- Identify active or selected triggers with LT/RT labels. RT appears on the left in the rear view, and LT on the right.
- Retain continuous analog positions, release/reset behavior, existing X20 input, offline previews, and the corrected perimeter paths. No image regeneration, original artwork changes, or hardware commands.

This remains a photo-based 2D illustration. The direction and distance are visual approximations, not measured trigger mechanics. Exact perspective changes require registered multi-angle renders or a corresponding 3D source; no such source is available in this checkout.

## Verification

- New proportion-preservation checks failed for all seven old model transforms before the correction. All eight trigger tests now pass.
- Frontend type checking and production build passed.
- Isolated production UI review passed all seven models: 0/23/50/100% holds, fixed aperture clips, labels, release, fourteen resize cases, fixture X20 input/disconnect, and unchanged original photo hashes. No screen or physical controller was accessed.
- Reviewed rendered released/half/full states across all models and a nine-frame single-RT cycle. Pixel diagnostics passed with the documented antialiasing and difference thresholds; the maximum released-reference difference remains 0.176% above 20/255.
- Current evidence is in `artifacts/trigger-motion-correction/`, separate from the preceding attempt. `x20-trigger-travel.gif` illustrates synthetic slider-driven travel, not physical-controller playback.
- Windows executable archive verification is recorded in `local-desktop-build.json`. The native window and real physical input remain unverified, respecting the user's screen/controller restriction.

Previous local builds and intentional working-tree changes were preserved. Nothing was pushed or released.
