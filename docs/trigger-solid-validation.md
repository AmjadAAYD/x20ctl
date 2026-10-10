# Solid trigger rotation

The previous implementation transformed a flat photograph. Camera depth changed, but there were no side walls and the trigger lost visible thickness at full press. The replacement renders only LT/RT as actual, textured 3D meshes; the body and collars remain in the saved controller artwork.

## Geometry and rendering

- All fourteen saved trigger contours are sampled into 96-point paths. No controller photo was edited.
- Each cap has a closed extrusion and five bevel segments, with a photo-derived outline and approximately 26–32 source-pixel depth. This thickness is a visual approximation, not a measured hardware dimension.
- [Three.js ExtrudeGeometry](https://threejs.org/docs/pages/ExtrudeGeometry.html) supplies triangulation and beveled side walls. Version 0.186.1 and its type package are pinned in the lockfile.
- The source contour is unprojected onto a tilted release plane. Every mesh vertex then undergoes a rigid 3D rotation around the fixed shaft, before perspective division. The shader rotates normals for lighting and preserves the original photo on the cap's front face. Side walls have their own shading and depth-tested occlusion.
- The selected outline follows the projected front contour. Existing click targets, analog inputs and model capability boundaries remain intact.
- Rendering happens on input/resize changes instead of an idle animation loop. Texture, materials, geometry, observers and GPU context are released on unmount. Canvas resolution follows display density, capped at 2x.
- If WebGL is unavailable or its context is lost, the original photo remains still. The flat deformation is no longer used as an animation fallback.

## Evidence

The browser regression first failed against the preceding implementation because it had no solid renderer. Seven new geometry tests prove nonzero thickness, face/side triangles, exact released contour registration, fixed hinges, downward travel, motion toward the viewer, and preservation of 3D distances through partial/full press. All seven passed; the eight retained input/legacy projection tests also passed. Frontend type checking and the production build passed. The build reports a larger JavaScript chunk after adding Three.js; this is a size warning, not a failed build.

The isolated production review passed all seven models, both independent inputs, partial/full/released states, mock X20 input/disconnect and fourteen resizing cases. The GPU reports no GL errors and both cap meshes contain side triangles. The original photographic cap is removed when the GPU cap is ready, preventing duplicate layers. Independent-side pixel checks and rim/released-image checks are kept in `artifacts/trigger-solid-review/`. Released/half/full images for every model were visually reviewed. The initial extrusion was reduced after the first visual inspection because it was too bulky at partial press.

A separate X20 review at 2x display density supplies the close-up preview in `artifacts/trigger-solid-closeup/`. Explicitly disabling WebGL passed the static-photo fallback check in `artifacts/trigger-solid-fallback/`.

The Windows executable is packaged separately; its embedded frontend and catalog are checked in `local-desktop-build.json`. The native window and physical controllers are not accessed, honoring the user's screen/controller restriction. Exact mechanical dimensions and physical-controller correspondence remain unverified. No hardware protocol, support claim, release, push or publication was added.
