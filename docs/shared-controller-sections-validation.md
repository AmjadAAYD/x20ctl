# Shared controller sections and scanner cleanup — 7 October 2026

All controller workspaces expose Buttons, Response curves, Macros, Vibration, Power & device, Input tester and Saved setups. The preview/input-only workspaces use a shared section definition. Selecting a section without verified support opens a feature-specific Scan now / Scan later popup. Scan later leaves its preview available; Scan now opens the guided collector with the model prefilled. X15's unverified settings remain placeholders, not hardware commands or independent paddle detection.

Photo collection and its attachment prompt/import path were removed. The Photos and Privacy coverage cards were removed. Upfront authorization and final report review remain enforced. Existing older reports are preserved.

The scanner now starts its coverage list with Trigger readings and Vibration feedback. Slow LT/RT stages are retained. Vibration asks for feedback observed using a game/already-working app; it does not issue guessed motor commands. The remaining scanner coverage has 24 categories.

Verification: 248 relevant Python tests passed; scoped Ruff and whitespace checks passed; TypeScript checking and production frontend build passed. The isolated fixture GUI review passed the shared X15 seven-section menu, missing-data popups, Player 2 input, all four unavailable-model notices, Scan later no-collection paths, review-gated Finish, manual fallback and preserved X20/X20 Pro wide/compact navigation. No user's screen/controller or real report submission was used.

Updated Windows package: `dist/local-shared-sections-20261007/x20ctl.exe`. Package verification is recorded in `artifacts/x15-scanner-review/shared-sections-build.json`. The existing large-JavaScript-chunk warning remains.

Physical controller/native GUI acceptance remains unverified. The live website receiver still needs its previously prepared schema-2 update deployed; this change does not deploy it or change that limitation. No release, push or deployment was performed.
