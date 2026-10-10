# Outward trigger direction correction

Superseded: this experiment misinterpreted the user's direction request. They wanted to retain the downward press and have its free edge approach the viewer. The angular reversal below was reverted; see [trigger-viewer-validation.md](trigger-viewer-validation.md) for the replacement.

The preceding fixed-hinge implementation turned the face inward toward the shaft. The user requested the reverse direction. The angular sign is now reversed for every model: the free edge moves outward, away from the body-side shaft, while the shaft remains stationary.

The static aperture now clips only the socket plate. The moving cap has its own transformed photographic contour and can extend beyond its resting outline. Selection and depth feedback move with it. The previously repaired LT geometry and independent input routing remain in place.

Verification:

- New outward-distance assertions failed on all seven preceding model transforms before the change. All eight unit tests pass after the correction, including shaft anchoring, outward direction, partial positions, mirrored LT/RT poses, and release registration.
- Frontend type checking and production build passed.
- Isolated production UI review passed all seven models, intermediate/full/released positions, both trigger cycles, fourteen resize cases, and fixture X20 input/disconnect.
- All fourteen independent-side pixel checks passed with labels hidden. Only the active trigger region changed beyond the documented antialiasing allowance. Original photo hashes and rim checks passed.
- Released/half/full screenshots for every model were inspected. The supplied GIF is synthetic slider playback, not physical-controller playback.

Evidence: `artifacts/trigger-outward-review/`. Executable archive verification is recorded in `local-desktop-build.json`. Previous builds and evidence were preserved. No native window or physical controller was accessed, and nothing was published or pushed. Geometry remains a photo-based visual approximation.
