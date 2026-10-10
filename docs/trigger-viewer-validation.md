# Downward trigger press toward the viewer

Superseded for moving caps: the user correctly identified that the surface still looked like a photographic sheet. See [trigger-solid-validation.md](trigger-solid-validation.md) for the solid, beveled 3D cap renderer. This implementation is retained as historical evidence.

The user clarified that the original downward action was wanted. The last outward-distance reversal was removed, restoring the original positive press angle for all seven models. The independent LT/RT paths and body-side hinges remain intact.

The preceding renderer projected the cap into a flat SVG group. Orthographic projection alone cannot communicate whether a face approaches or recedes. Each photographic cap now has a separate SVG plane, with positive camera depth increasing toward its free edge as it presses downward. CSS perspective preserves that depth instead of flattening it inside a group. The camera distance scales with the controller plane through container units; the shaft stays fixed. Free-edge lighting replaces the previous darkening of the front face. The body, collar and socket do not move.

Both cap planes retain their own compositor layers even while released. Without this, promoting LT during travel changed antialiasing on the inactive RT edge. Independent pixel comparisons reproduced that rendering issue; after the layer fix both inactive regions remain within the existing allowance.

Verification:

- The restored downward-distance regression initially failed for all seven models against the intermediate outward implementation. All eight trigger tests now pass, including downward travel, positive viewer depth, fixed shafts, mirror symmetry, partial positions, safe input clamping and exact release registration.
- Frontend type checking and production build passed.
- Isolated production UI review checks the actual browser's 3D matrix: free-edge homogeneous W is below one (approaching), vertical travel remains downward, and the shaft retains its position/depth. Both triggers are driven independently with fixture sliders; no real input device is used.
- Independent-side pixel checks passed all fourteen cases with labels hidden. Original source photo hashes and released-image/rim checks remain covered.
- The review also checks perspective scaling at wide and narrow sizes with both triggers partially pressed. Reports and saved renders are in `artifacts/trigger-viewer-review/`.

The GIF is synthetic slider playback. The local Windows build is checked against the current frontend and catalog in `local-desktop-build.json`. The native window and physical controllers are not accessed, respecting the user's restrictions. These photo-based visual angles are not mechanical measurements; no hardware commands, support claims, release or push were added.
