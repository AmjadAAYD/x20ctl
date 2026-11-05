# Seven illustrated themes: implementation plan

> Superseded composition: the initial shared layout below was rejected. The current implementation follows the separate designs in `illustrated-theme-concepts.md`; see `illustrated-themes-native-review.md` for the implemented scope and verification.

## Intended result

Implement Vaporwave, Frutiger Aero, Gothic, Manga, Tokyo Night, Synthwave and Crimson Rebellion in the existing WPF application. The artwork-only runner is not an application implementation. APP-WALKTHROUGH-SPEC.md supplies the real controls, navigation, labels, capability limits and actions; the seven image briefs supply visual direction. The user's current request authorizes implementation and autonomous continuation. Existing themes and dirty documents stay intact.

## Design

Use existing Zone docks, model selector, Studio editors and Tools controls. Add a presentation kit for the new themes with native interactive button, tab, input, slider, toggle and card styling, plus correct page-specific artwork. Distinguish geometry, typography, borders and surface treatments: soft lavender/chrome, aqua gel, pointed iron/bone, thin ink, restrained blue glass, neon bevels, and thick cut-paper respectively. Keep semantic status colours and accessible keyboard focus. Route every editor to its own backdrop, including RGB/Screen capabilities and Agent; do not replace live editors with rendered pictures. No controller protocol changes.

Create application-sized local derivatives from the preserved artwork, retaining provenance; avoid shipping a gigabyte of source PNGs. Package assets as ordinary build content. Use native finite intro stories and the existing skip/handoff contract; reduced motion crossfades, idle backgrounds have no render loop.

## Work

1. Establish baseline build and missing-theme review failure. Read existing navigation, resources, editors and windowless review contracts.
2. Add the seven catalog entries and a testable artwork/page manifest. Verify 18 distinct routes per theme, capability gating and unknown-theme fallback.
3. Build/copy lightweight application assets. Add themed control templates and presentation hooks for Zone, selector, Studio, Tools and Agent.
4. Implement silent short intros using each theme's own visual motif, keeping skip and header-logo handoff.
5. Extend offscreen review to assert page/backdrop routing and real interaction state (draft mapping, selected tabs, editor actions), plus disabled/hover/focus/pressed states, without hardware writes or visible windows.
6. Build, run relevant tests, render all seven themes and inspect images. Include compact/DPI and existing Default regression checks where relevant. Record every unresolved item honestly.

## Evidence

- Baseline product build: succeeded, 32 warnings, zero errors.
- Baseline `--review-themes ... vaporwave`: exits 1 because Vaporwave is not in the catalog. This is the integration failure to resolve.
- No application source changes existed at the start of this pass; untracked APP-WALKTHROUGH-SPEC.md and image-brief documents belong to the user and are preserved.

## Completion gate

All seven themes are selectable and persist. Every walkthrough surface uses real controls and the correct art; buttons, tabs and editor actions remain wired. Builds and relevant automated checks pass; offscreen renders are inspected. Hardware and native desktop interaction remain unverified unless actually exercised; no review run may write to a controller or change the owner's preferences.
