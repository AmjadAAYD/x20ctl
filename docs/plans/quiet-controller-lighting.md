# Shared controller lighting and glass navigation

Apply to X20, X20 Pro, X05, X05 Pro, X10 and D10 without changing hardware
capabilities, protocol boundaries, player assignments or original photo files.

1. Separate the photographic shell from its baked background using model-specific
   silhouette clips in source-image coordinates. Keep the shell opaque and the
   surrounding ambient render quiet. Increase ambient lighting only for hover or
   an explicitly selected supported model; previews remain subdued.
2. Make the picker a two-column selection grid, with a narrow-window list fallback
   and explicit Supported/Preview badges. Model selection remains separate from
   Connect. All preview models remain selectable without hardware operations.
3. Add a thin, slow branded-color sweep around each front silhouette in Buttons.
   Rear views use local paddle outline feedback instead of an ambient color cycle.
4. Retain traced grip/trigger haptics, with one broad color packet, restrained
   brightness, and nonlinear speed from about five seconds at low strength to
   one second at maximum. At zero retain a faint static rim only.
5. Use a glass appearance on shared navigation/view controls with clear keyboard
   focus, reduced-motion/transparency fallbacks and a stable moving active pill.
6. Calm the Macro background, reduce rear-preview size 15%, improve the empty
   state, and add a local run-once sequence preview. Preserve rear-only artwork;
   show active face inputs as input labels, never fake rear face buttons.
7. Verify all six models in an isolated headless production preview. Check artwork
   geometry, page navigation, picker assignments, macro links/local playback,
   haptics strengths, resize and accessibility preferences. Type-check/build and
   create a separate local Windows bundle. Do not use the host screen/controller
   or overwrite existing builds.
