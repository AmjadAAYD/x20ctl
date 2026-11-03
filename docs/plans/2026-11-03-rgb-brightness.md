# RGB brightness control

Base: b1e9227 on codex/input-contract-20261103. Preserve the new controller input contract, verified BLE lighting writer, battery-level colors, screen upload and unrelated settings.

Add a keyboard-accessible 0–100% Brightness slider to the shared RGB page. It edits the existing HSV value, synchronizes with the square, palette, hex input and controller readback, and updates the selected battery-level color. Hold hue/saturation at zero brightness so raising it restores the chosen color. Preview stays local until Apply; no new native field or automatic write is introduced. Brightness scales the selected RGB triplet, not an unverified master control for Cycle.

Verify actual offscreen RGB interactions at 0/25/50/100 percent, hex synchronization, zero-to-full recovery and independent battery-level editing. Render Default and Rebel using the existing hidden review harness; run focused lighting/color/battery tests and build the product. No hardware writes or owner desktop windows.


## Verification

The offscreen check first failed because no RGB brightness slider existed. The implemented control passed 0/25/50/100 percent, hue recovery after committing black, dark-hex synchronization and independent battery-level editing in Default and Rebel. Focused domain tests: 48 passed. Product builds with 0 errors; the existing build emits nullable/unused/DPI warnings. No controller IO was performed.

The broader feature-tabs run passed Default but failed Rebel's factory-reset confirmation check after the RGB checks had passed. Reset behavior was not changed in this task; the brightness-only review remains independently runnable as `--review-rgb-brightness <output> <theme>`.
