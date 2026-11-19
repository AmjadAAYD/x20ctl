# Linux input/device ownership batch

Authorized by Amjad 3 November 2026. Work only in feature/linux-parity; no commits/pushes.

1. Test UI-free four-player explicit source assignment, duplicate rejection, disconnect clearing, neutral barriers, capture ambiguity, fresh presses and navigation repeat/context guards in Zone.
2. Replace first-js UI reads with selected evdev reads using existing RealPad layout normalization in read-only/non-grabbing mode; bounded polling, per-report edges, generation guards and attach/detach restart.
3. Expose session-only source choice on the existing PC mode page; share selection with PC and mouse engines. No persistent physical/model association or automatic replacement source.
4. Central foreground/context routing for gamepad navigation; exclusive tester/recording/remap ownership, text/modal/mouse suppression. Explicit Buttons source/target capture with release barrier; cancellation preserves draft.
5. Stop transient keyboard/recording input on page/focus/device changes. Add headless integration checks and render existing screens without visible windows.
6. Linux audio is intentionally not applicable: silent intro, no packaged audio or playback path. Existing Flatpak grants stay byte-for-byte unchanged.

Success: tests and Product/Linux/Review builds pass; fake-source routing/capture/lifecycle checks pass; audit/docs distinguish source/fixture checks from unperformed Linux/pad/sandbox validation. Unmerged work, BlueZ, new dependencies, themes and hardware writers remain excluded.
