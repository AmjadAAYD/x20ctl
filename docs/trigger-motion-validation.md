# Trigger motion and controller rim repair

## Changes

- The macro toolbar places Remove immediately beside Add Step. It removes the selected step and disables itself when the sequence is empty. The separate Piano Roll retains its own deletion action.
- All seven models (X20, X20 Pro, X05, X05 Pro, X10, D10, X15) use photo-derived front and back silhouette paths. The extra static outline was removed; the animated sweep is clipped inside the shell. Original photographs were preserved.
- LT and RT now have separate photographic caps above reconstructed sockets. The original static caps are excluded from the body and ambient layers. Partial input controls cap travel directly and holds that position; disconnect returns them to rest. The moving selection highlight follows the cap.
- Each model has its own hinge geometry, including the upright X15 triggers. Offline trigger travel sliders are available under Keyboard preview. A rising trigger press opens Back View; the existing shoulder selection behavior remains.

This is a visual approximation using photo layers, not a mechanically calibrated 3D model. X20 uses its existing input feed. The other models remain offline previews; no new hardware commands, detection, or polling were introduced.

## Evidence

- Frontend production build and lint passed.
- Eight trigger unit tests passed, covering every model, intermediate positions, clamping, and asset dimensions.
- Eight focused macro layout/deletion cases passed at 1400 and 800 pixels.
- Existing control selection review passed for all seven models.
- Isolated headless production UI review passed all seven models: 23%, 50%, 100%, and released positions for both triggers; fourteen resize cases; X20 fixture input and disconnect; original photo hash preservation. No device connection or write occurred.
- Rendered rim comparison found no escaping pixels above an 8/255 difference threshold beyond a three-pixel antialiasing/screenshot-rounding margin. Released trigger composites differed from the original reference above a 20/255 threshold in at most 0.176% of pixels, concentrated around sprite seams. Motion changed the rendered pixels on all seven models.

Reports and screenshots: `artifacts/controller-trigger-motion/`. The archive report `local-desktop-build.json` records the new executable's hash and verifies its bundled frontend and catalog against the current files.

The real desktop window and physical input were **not** inspected, respecting the request not to use the user's screen or controller. Headless browser results and archive verification do not prove native WebView2 rendering or physical compatibility.

## Image provenance

Socket bases are additive assets at `src/assets/controllers/<model>/controller-rear-trigger-base.png`. Only pixels inside the trigger socket masks are used; the original photo supplies the body and moving caps. Edits used the built-in `image_gen` image editor. Exact prompts, source paths, generated paths, and saved paths are in [trigger-layer-image-prompts.json](trigger-layer-image-prompts.json). Canva and Figma were not used.

No release, push, or hardware write was performed.
