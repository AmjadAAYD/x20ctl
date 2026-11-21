# Privacy Notice

X20CTL is **local first**: it requires no account, collects no background telemetry, and keeps your controller profiles and setups on your own computer.

## What stays on your computer

- **Setups, drafts & PC Mode profiles:** Button mappings, stick/trigger curves, macros, RGB lighting presets, keyboard/mouse bindings, vibration levels, and preferences are stored locally in your user configuration folder (`%LocalAppData%\X20CTL` on Windows; `~/.config/x20ctl` on Linux).
- **Game library shelf:** X20CTL reads local launcher manifests (Steam, Epic, GOG, Riot on Windows; Steam, Heroic, Lutris on Linux) locally to display your installed games for per-game PC Mode profile switching. Game lists are never uploaded.
- **100% offline Handheld Agent XCTL:** Phrase matching, typo correction, help search (`README.md`, `IF-YOUR-CONTROLLER-ISNT-WORKING.md`, `PRIVACY.md`), lighting suggestions, and telemetry Check-up run entirely on your machine. No external chat service or downloaded AI model is contacted.
- **Controller Check-up & local diagnostics:** Drift checks, trigger sweeps, and button tests happen locally. Diagnostic reports saved to `~/X20CTLInputReports/` (or your chosen folder) stay on disk unless you choose to share them.

## Optional network requests (user-controlled)

X20CTL makes network requests only in these three cases:

1. **Update check (toggleable):** When **Keep X20CTL up to date** is enabled in **Tools**, startup queries GitHub's public releases API (`AmjadAAYD/x20ctl`) to check for a newer non-prerelease tag and verifies downloaded installers/bundles against `SHA256SUMS.txt`. Turn this toggle off to disable all automatic update checks.
2. **Opt-in Controller Check & Feedback uploads:** Choosing **Scan and send** in Controller Check or **Send to X20CTL** in Feedback shows you the sanitized summary/fields on screen first and uploads only after your confirmation.
3. **External links you click:** Links to GitHub, troubleshooting docs, or third-party pages open only when clicked in your default browser.

## What is stripped from exports & reports

Diagnostic and Controller Check exports omit full device instance paths, Windows/Linux user paths (`C:\Users\...` / `/home/...`), controller serial numbers, and Bluetooth MAC addresses by default. Review any optional raw trace (`BEFORE_YOU_SEND.txt`) before sharing files from troubleshooting or the standalone `tools/controller-scan-kit` tool.

## Contact & removal

All local X20CTL data can be deleted anytime from your local config folder (`%LocalAppData%\X20CTL` or `~/.config/x20ctl`) or via **Tools -> Reset** inside the app.
