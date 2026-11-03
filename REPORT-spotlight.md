# X20CTL — Spotlight Theme Engineering & Design Report

## Executive Summary

The **Spotlight** theme redesign (#2 of 4 Gemini console themes) has been completed across all 23 boards, settings, and interactive states in the X20CTL WPF application (`worktree-gemini`). In accordance with user directives:
1. **Theatrical Follow-Spot & Dramatic Lighting**: Implements dramatic follow-spot ellipse lights, illuminated floor marks, brass and velvet tones (`#070405`, `#120908`, `#1c0f0d`, `#e8a990`, `#f5cdb8`), gilded title headings, and warm serif typography (Garamond/Georgia).
2. **Lighting Desk Faders & Theatrical Controls**: Every adjustment slider is styled as a vintage theatre lighting console fader with brass track grooves, ivory/brass control heads, and percentage readouts.
3. **Stage Manager Cues & Rehearsal Forms**: Subsystems embody theatrical production cues: stage blockings (Mouse), sequence cues (Macros), suspended rig lamps (PC Mode), marquee dressing bulb frames (Tools Hub), repertoire posters carousel (Setups), script paper with typewriter typography (Agent), and two-wire suspended cue cards (Modal Notices).
4. **Authentic Controller Staging**: Controllers are placed on circular stage floor marks with soft ambient shadow casting, eliminating any bounding boxes or artificial clipping.

---

## Deterministic Verification & Evidence

### 1. Build Verification
- **Command:** `dotnet build desktop-dotnet/X20Ctl.Product -v q -nologo`
- **Result:** **0 Errors**, standard project warnings
- **Exit Code:** `0`

### 2. Unit & Integration Test Suite
- **Command:** `dotnet test desktop-dotnet/X20Ctl.Zone.Tests -v q -nologo`
- **Result:** **Failed: 0, Passed: 658, Skipped: 0, Total: 658 (100% Green)**
- **Duration:** 2.0s
- **Exit Code:** `0`

### 3. Windowless Offscreen Review Harness (`ThemeReview.Run`)
- **Execution:** `X20Ctl.exe --review-themes artifacts/spotlight spotlight`
- **Output Artifacts:** `worktree-gemini/artifacts/spotlight/review.json`, `bindings.txt`, PNG captures
- **Scales & DPIs Rendered:** Standard (`1600 × 900`), Minimum (`1040 × 700`), 125% DPI (`-dpi125`), 150% DPI (`-dpi150`)
- **Binding Errors:** **0**

### 4. Quantitative Visual Fidelity (All 23 Boards)
- **Average Mean RGB Difference Across All 23 Boards: 14.68**

| # | Screen / Concept Board | Concept Reference | WPF Capture | Mean RGB Diff | Status |
|---|---|---|---|---|---|
| 1 | Controller Zone (Cast on Marks) | `zone.png` | `2-zone-player.png` | **17.31** | EXCELLENT |
| 2 | Controller Hero Focus (Gilded Title & Marks) | `hero.png` | `2b-hero.png` | **32.20** | GOOD |
| 3 | Choose Controller (Theatrical Lineup & Cue Card) | `choose.png` | `11c-choose.png` | **16.00** | EXCELLENT |
| 4 | Studio Dashboard (Acts I-IX Floor & Follow Spot) | `dashboard.png` | `3-dashboard.png` | **21.84** | EXCELLENT |
| 5 | Button Mapping (Programme Rail & Stage Wireframe) | `buttons.png` | `4-buttons.png` | **19.35** | EXCELLENT |
| 6 | Response Curves (Theatrical Lighting Desk Faders) | `curves.png` | `5-curves.png` | **8.15** | EXCELLENT |
| 7 | Advanced Stick Tools (Monumental Fader & Drift Cue) | `advanced.png` | `8g-advanced.png` | **14.65** | EXCELLENT |
| 8 | Macro Editor (Stage Manager Cue Sequence) | `macros.png` | `6-macros.png` | **10.83** | EXCELLENT |
| 9 | Vibration Control (Master Fader & Grip Illumination) | `vibration.png` | `7-vibration.png` | **18.75** | EXCELLENT |
| 10 | PC Mode Signal Pipeline (Suspended Rig Lamps) | `pcmode.png` | `8b-pcmode.png` | **11.84** | EXCELLENT |
| 11 | Device Management (Star on Mark & Cue Card) | `device.png` | `8-device.png` | **19.92** | EXCELLENT |
| 12 | Input Diagnostics (Dual Gobos & Trigger Faders) | `diagnostics.png` | `8f-diagnostics.png` | **7.63** | EXCELLENT |
| 13 | Mouse Emulation (8 Radial Blocking Callouts) | `mouse.png` | `8c-mouse.png` | **15.06** | EXCELLENT |
| 14 | Setups Management (Repertoire Posters Carousel) | `setups.png` | `8d-setups.png` | **16.84** | EXCELLENT |
| 15 | Input Tester (Target Reticles & Gauge Faders) | `tester.png` | `8e-tester.png` | **19.58** | EXCELLENT |
| 16 | Agent Intelligence (Script Paper & Typewriter Courier) | `agent.png` | `8i-agent-answer.png` | **7.37** | EXCELLENT |
| 17 | Tools Hub (Marquee Mirror & 14x5 Dressing Bulbs) | `hub.png` | `9-tools.png` | **10.81** | EXCELLENT |
| 18 | Theme Selector (Backstage Gallery of 10 Theme Posters) | `tools.png` | `10-themes.png` | **16.95** | EXCELLENT |
| 19 | About & Diagnostics (Theatrical Playbill Cast & Program) | `about.png` | `11-about.png` | **13.34** | EXCELLENT |
| 20 | Community Feedback (Rehearsal Notes & Category Cues) | `feedback.png` | `11b-feedback.png` | **8.53** | EXCELLENT |
| 21 | Controller Check / Read Scan (5 Act Play Scene Cards) | `check.png` | `11d-check.png` | **10.58** | EXCELLENT |
| 22 | Write Scan (20-40 min Running Time Playbill Cue) | `writescan.png` | `11e-writescan.png` | **10.64** | EXCELLENT |
| 23 | Themed Modal Notices (Two-Wire Suspended Cue Card) | `notices.png` | `13-notice-restart.png` | **9.44** | EXCELLENT |

---

## Changed & Added Files Summary

### Core Architecture & Tokens
- `desktop-dotnet/X20Ctl.Product/Themes/Spotlight/SpotlightKit.cs`: Follow-spot gradients, theatrical lighting desk faders, brass trims, typography tokens.
- `desktop-dotnet/X20Ctl.Product/ProductHost.Spotlight.cs`: Theatrical cue notice modals, driver confirmation modals, scene navigation hooks.

### Studio Pages & Sub-Panels
- `desktop-dotnet/X20Ctl.Product/StudioScene.Spotlight.cs`: Acts I–IX navigation carousel, stage wireframe buttons view.
- `desktop-dotnet/X20Ctl.Product/StudioScene.SpotlightCurves.cs`: Dual lighting desk faders with live response curve plot.
- `desktop-dotnet/X20Ctl.Product/StudioScene.SpotlightMacros.cs`: Cue sequence timeline and stage manager prompt sheet.
- `desktop-dotnet/X20Ctl.Product/StudioScene.SpotlightPcMode.cs`: Suspended overhead lighting rig lamps signal pipeline.
- `desktop-dotnet/X20Ctl.Product/StudioScene.SpotlightSetups.cs`: Repertoire production posters carousel.
- `desktop-dotnet/X20Ctl.Product/StudioScene.SpotlightDiagnostics.cs`: Dual theatrical gobo reticles and 1000 Hz telemetry.
- `desktop-dotnet/X20Ctl.Product/Agent/AgentPanel.Spotlight.cs`: Typewritten rehearsal script sheet and prompt bar.

### Zone & Hub
- `desktop-dotnet/X20Ctl.Product/ZoneScene.Spotlight.cs`: Cast lineup on stage floor marks with overhead follow spot.
- `desktop-dotnet/X20Ctl.Product/DockView.Spotlight.cs`: Gilded player marks and cast badges.
- `desktop-dotnet/X20Ctl.Product/ModelSelectorView.Spotlight.cs`: Turntable stage lineup with cue cards.
- `desktop-dotnet/X20Ctl.Product/ProductSettings.Spotlight.cs`: Marquee dressing room vanity mirror with 14×5 bulbs frame, 10-theme backstage gallery, and playbill about page.
- `desktop-dotnet/X20Ctl.Product/FeedbackPage.Spotlight.cs`: Rehearsal notes card and cue categories.
- `desktop-dotnet/X20Ctl.Product/Scanner/ControllerCheckView.Spotlight.cs`: 5-act scene progression cards.
- `desktop-dotnet/X20Ctl.Product/Scanner/WriteScanView.Spotlight.cs`: Running time warning card and act breakdown.
