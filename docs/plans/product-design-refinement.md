# X20CTL product design refinement

## Baseline and preservation
- Current branch: `codex/native-2.0-recovery`, HEAD `f2c67d6`.
- Preserve existing scanner, capability-discovery and documentation work. External checkpoint: `C:/Users/amjad/.codex/checkpoints/x20ctl-product-design-20261008/snapshot.zip`.
- Current catalog is authoritative: X20 configuration; X20 Pro preview; D10, X15, X05 and X10 experimental input; X05 Pro unavailable. No model or backend changes.
- Native request boundary and platform window/tray handling stay unchanged.

## Design and implementation
1. Introduce a token-based product surface as the final appearance layer; retain existing geometry and controller-specific CSS, including previous fit corrections. Avoid deleting legacy sheets with still-used structural rules.
2. Quiet static navy environment, crisp focus, shared cards, compact status, restrained blue selection and warm warnings.
3. Four player cards retain local assignment truth. Large artwork, clear support state, understated entry actions.
4. Use compact horizontal console navigation for seven sections beneath a unified top line. Short visual labels retain full accessible names. Animate one selection underline; arrows/Home/End move focus.
5. Show macro slot summaries first; reveal all existing sequencer/library/timing/recording capabilities on Edit/Create.
6. Opt-in X20 navigation uses existing read-only input samples. Suspend during tester, recording, dialogs, busy operations and input editing; A activates only explicitly marked navigation actions. No automatic writes.
7. Preview truth becomes concise primary copy with technical details retained.
8. Add design inspiration credit without suggesting code contribution or endorsement.
9. Recording reference reviewed: 56.5-second local MP4, eighteen scene samples. Settings uses broad option rows, diagnostics pairs a stable controller visualization with a focused inspector, and the library separates selection from details. Adapt these into horizontal studio sections, soft option rows, a selected-control inspector, and local setup selection. Use 210–220 ms page/dialog/selection transitions with reduced-motion equivalents.

## Verification
Run npm lint, tests and build; Python pytest; isolated browser fixture capture of all ten requested views at normal and compact window sizes. Review images and check navigation/recording/tester boundaries. Browser fixtures are not native or physical hardware acceptance. No version, tag, publication or release changes.

## Reference
Reviewed ApexSenseBridge stable MainWindow.xaml and ConsoleTheme.xaml for structure only: separated status and context, low-contrast card surfaces, compact navigation, clear focus and restrained transitions. Source: https://github.com/ReynArts/ApexSenseBridge. Reddit showcase is a video; no assets transplanted.
