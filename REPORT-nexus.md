# X20CTL — Nexus Theme Engineering & Design Report

## Executive Summary

The **Nexus** theme redesign (#3 of 4 Gemini console themes) has been completed across all 23 boards, settings, and interactive states in the X20CTL WPF application (`worktree-gemini`). In accordance with user directives:
1. **Xbox Console Aesthetic Overhaul**: Built upon dark graphite tiles (`#101113`, `#1d1f23`, `#24272c`), vibrant Xbox green focus halos (`#6cc644`), an expansive 560px perspective key art band with 14 diagonal sheen lines rotated -8°, hero controllers in dramatic perspective, and zero template bleed-through across dock and studio views.
2. **Subsystem Cover Artwork System (`CoverTile`)**: Fully implements `kit.py:152 cover()`. Uses radial finish gradients (`RadiusX = 1.2, RadiusY = 0.9`), focal zoom crops for each subsystem (`buttons`, `curves`, `macros`, `vibration`, `pcmode`, `device`, `mouse`, `setups`, `tools`, `dashboard`), and subtle bottom shading.
3. **Controller Zone & Dock Lifecycle Polish**: Docks invoke `SetBounds()` in `SnapNexusLayout()` and `NexusSync()`, ensuring full dock presentation lifecycle. Standard template controls (`Card`, `PlayerPill`, `Details`, `Halo`, `ArtShadow`, `Art`, `ActionCanvas`, `ArrowBadge`, `PlusBadge`, `Silhouette`) are collapsed to eliminate template bleed-through. Player 0 (`x20pro`) is highlighted with a 4px Xbox green border (`#6cc644`).
4. **Vibration & Notices Layout Precision**: Vibration control accurately follows `base.py:102` short-list rule (`n <= 4`), placing the 3 rows on the left with illuminated grip vibration glow visual rendered beneath, leaving the right column for the clean single `Vibration Preview` card (7.56 diff). Themed modal notices overlay `dashboard.png` with a `linear-gradient(0deg, #f70a0b0d 0%, #b80a0b0d 18%)` wash behind the Xbox Guide dialog and stacked status toasts (10.22 diff).

---

## Deterministic Verification & Evidence

### 1. Build Verification
- **Command:** `dotnet build desktop-dotnet/X20Ctl.Product -v q -nologo`
- **Result:** **0 Errors**, 58 standard warnings
- **Exit Code:** `0`

### 2. Unit & Integration Test Suite
- **Command:** `dotnet test desktop-dotnet/X20Ctl.Zone.Tests -v q -nologo`
- **Result:** **Failed: 0, Passed: 658, Skipped: 0, Total: 658 (100% Green)**
- **Duration:** 3.0s
- **Exit Code:** `0`

### 3. Windowless Offscreen Review Harness (`ThemeReview.Run`)
- **Execution:** `X20Ctl.exe --review-themes artifacts/nexus nexus`
- **Output Artifacts:** `worktree-gemini/artifacts/nexus/review.json`, `bindings.txt`, PNG captures
- **Scales & DPIs Rendered:** Standard (`1600 × 900`), Minimum (`1040 × 700`), 125% DPI (`-dpi125`), 150% DPI (`-dpi150`)
- **Binding Errors:** **0**

### 4. Quantitative Visual Fidelity (All 23 Boards)
- **Average Mean RGB Difference Across All 23 Boards: 10.96** (Highest visual accuracy achieved across all console themes).
- **Fidelity Distribution:** 21 EXCELLENT (< 17), 2 GOOD (< 20), 0 Acceptable, 0 Poor.

| # | Screen / Concept Board | Concept Reference | WPF Capture | Mean RGB Diff | Status |
|---|---|---|---|---|---|
| 1 | Controller Zone (4 Player Tiles & Top Bar) | `zone.png` | `2-zone-player.png` | **14.11** | EXCELLENT |
| 2 | Controller Hero Focus (Centered Pad & Stats Column) | `hero.png` | `2b-hero.png` | **6.95** | EXCELLENT |
| 3 | Choose Controller (212×174 Focused Tile & Neighbor Carousel) | `choose.png` | `11c-choose.png` | **19.58** | GOOD |
| 4 | Studio Dashboard (Key Art Band, Top Bar & Focused Buttons Tile) | `dashboard.png` | `3-dashboard.png` | **8.67** | EXCELLENT |
| 5 | Button Mapping (560×560 Panel, Dropdowns & Glowing A-Button) | `buttons.png` | `4-buttons.png` | **3.44** | EXCELLENT |
| 6 | Response Curves (860×580 SVG Plot & 580×580 Green Faders) | `curves.png` | `5-curves.png` | **4.29** | EXCELLENT |
| 7 | Advanced Stick Tools (Dead Zones & Straight 1:1 Curve) | `advanced.png` | `8g-advanced.png` | **11.13** | EXCELLENT |
| 8 | Macro Editor (Timeline Lane & Event Steps) | `macros.png` | `6-macros.png` | **11.82** | EXCELLENT |
| 9 | Vibration Control (Strength Slider & Grip Outline) | `vibration.png` | `7-vibration.png` | **7.56** | EXCELLENT |
| 10 | PC Mode Signal Pipeline (Signal Route Diagram & Steps) | `pcmode.png` | `8b-pcmode.png` | **11.72** | EXCELLENT |
| 11 | Device Management (Hardware Specs & Wireless Dongle Card) | `device.png` | `8-device.png` | **14.06** | EXCELLENT |
| 12 | Input Diagnostics (Dual Stick Plots & 1000 Hz Telemetry) | `diagnostics.png` | `8f-diagnostics.png` | **9.47** | EXCELLENT |
| 13 | Mouse Emulation (Action Assignments & Clicks Category) | `mouse.png` | `8c-mouse.png` | **8.77** | EXCELLENT |
| 14 | Setups Management (Angled Pad, Action Buttons & Saved Setup Carousel) | `setups.png` | `8d-setups.png` | **16.01** | EXCELLENT |
| 15 | Input Tester (Centered Controller & Bottom Gauges Shelf) | `tester.png` | `8e-tester.png` | **15.35** | EXCELLENT |
| 16 | Agent Guide (Xbox Guide Slide-in Panel & Chat Bubbles) | `agent.png` | `8i-agent-answer.png` | **15.23** | EXCELLENT |
| 17 | Tools Hub (Hero Tile, 6 Feature Tiles & Feedback Tile) | `hub.png` | `9-tools.png` | **8.68** | EXCELLENT |
| 18 | Theme Selector (10 Theme Tiles & Nexus Apply Card) | `tools.png` | `10-themes.png` | **19.02** | GOOD |
| 19 | About Page (System Version, Credits & Independent Studio Card) | `about.png` | `11-about.png` | **8.14** | EXCELLENT |
| 20 | Community Feedback (Dropdowns, Focused Message & Quote Box) | `feedback.png` | `11b-feedback.png` | **7.94** | EXCELLENT |
| 21 | Controller Check / Read Scan (Step Rows & Pad Visual) | `check.png` | `11d-check.png` | **11.67** | EXCELLENT |
| 22 | Write Scan (Information Steps & Planning Notice Card) | `writescan.png` | `11e-writescan.png` | **8.24** | EXCELLENT |
| 23 | Themed Modal Notices (Xbox Guide Dialog & Stacked Toasts) | `notices.png` | `13-notice-restart.png` | **10.22** | EXCELLENT |

---

## Changed & Added Files Summary

### Core Architecture & Tokens
- `desktop-dotnet/X20Ctl.Product/Themes/Nexus/NexusKit.cs`: CoverTile system with radial finish gradients, focal zoom crops, KeyArtBand with 175° perspective sheen coordinates and 14 diagonal sheen lines, ThemeThumb preview loader, TopBar with circular avatar and centered action buttons.
- `desktop-dotnet/X20Ctl.Product/Themes/Nexus/NexusNotice.cs`: Modal Guide panel with dashboard backdrop and dimming gradient, stacked status toasts.
- `desktop-dotnet/X20Ctl.Product/ProductHost.Nexus.cs`: Theme restart notice, driver confirmation notice, modal management.

### Studio Pages & Sub-Panels
- `desktop-dotnet/X20Ctl.Product/StudioScene.Nexus.cs`: Studio dashboard collage, neighbor tiles, wide collage; button remapping with 560×560 panel and glowing A-button ring.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusCurves.cs`: Response curves with 860×580 plot and green faders.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusMacros.cs`: Timeline lane and event steps.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusVibration.cs`: Short-list vibration layout with illuminated grip visual beneath rows.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusPcMode.cs`: PC mode signal pipeline diagram.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusDevice.cs`: Device specifications and hardware card.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusDiagnostics.cs`: Dual stick plots and 1000 Hz telemetry.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusMouse.cs`: Mouse emulation action assignments.
- `desktop-dotnet/X20Ctl.Product/StudioScene.NexusSetups.cs`: Angled background controller and saved setup carousel with CoverTiles.
- `desktop-dotnet/X20Ctl.Product/Agent/AgentPanel.Nexus.cs`: Xbox Guide slide-in panel with chat bubbles.

### Zone & Hub
- `desktop-dotnet/X20Ctl.Product/ZoneScene.Nexus.cs`: KeyArtBand backdrop, Player 0 hero focus with green border, SetBounds() lifecycle integration.
- `desktop-dotnet/X20Ctl.Product/DockView.Nexus.cs`: Full template control isolation and CoverTile presentation.
- `desktop-dotnet/X20Ctl.Product/ModelSelectorView.Nexus.cs`: Controller carousel with CoverTiles and detail panel.
- `desktop-dotnet/X20Ctl.Product/ProductSettings.Nexus.cs`: Tools hub with hero card and 6 tool tiles, 10-theme selector with live thumbnails, about page.
- `desktop-dotnet/X20Ctl.Product/FeedbackPage.Nexus.cs`: Community feedback dropdowns and quote box.
- `desktop-dotnet/X20Ctl.Product/Scanner/ControllerCheckView.Nexus.cs`: Read scan step rows and pad visual.
- `desktop-dotnet/X20Ctl.Product/Scanner/WriteScanView.Nexus.cs`: Write scan warning card and planning notice.
