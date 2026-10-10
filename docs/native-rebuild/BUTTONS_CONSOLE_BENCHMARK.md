# Buttons console benchmark specification

Status: benchmark implementation plan approved, and resulting Buttons visual architecture approved by owner on 2026-10-09. See APPROVED_BUTTONS.md for the frozen baseline and final punch list. Controller Zone remains approved/frozen. Reference snapshot is outside the MIT project at C:/Users/amjad/.codex/references/ApexSenseBridge-stable, stable commit fe7fa3a5432f3fd31ea5dc2b3cc97a88b30d061a.

## Inspected reference and scope

ConsoleTheme.xaml (885 lines), MainWindow.xaml (558), GameListWindow.xaml (1112; full XML hierarchy/attributes parsed), ControllerTestWindow.xaml (690), GamepadNavigationService.cs (528), FocusRingAdorner.cs (100), ThemeManager.cs (147), ControllerTestService.cs (826), ApexSenseBridgeTray.csproj (272). Also inspected test-window input/render/tab code, game-list transition/focus locations, controller/overlay resource declarations and asset inventory. Public code/assets are reference-only; no GPL implementation/template/image is imported into X20CTL.

## Measured reference properties

| Property | Reference evidence | X20CTL application |
| --- | --- | --- |
| Console window | GameList 1224x804, min 1044x704; ControllerTest 1120x780; tray MainWindow 460x540 | Preserve approved 1220x800 / 1040x700 native chrome. Tray utility size is not the Studio model. |
| Frame | External 40 DIP shadow margin; radius22; 1 DIP translucent edge; blur34/shadow8 | Preserve approved opaque native frame; adapt console content composition inside it, not copy transparency/chrome implementation. |
| Primary content margins | 26 DIP; header18 top; tabs8-10 top and12-14 bottom | Content inset26; compact context/header; stop allocating empty bands. |
| Split | ControllerTest left480 + gap18 + remaining490 within988DIP body | Balanced workspace about49/51; hardware region plus secondary controls, not oversized art plus form. |
| Major surfaces | Radius20, alpha B3 (70% opacity) #111A31, edge alpha14 white, padding18/16 | Two major console surfaces with subordinate16-radius sections; restrained edges and lifted focus. |
| Secondary rows/cards | Radius14/16, row14x10 padding, vertical8/12 gaps, subtle white10-alpha fill | Compact mapping sections and assignment rows with clear grouping. |
| Typography | Header17; nav15 (test14); body13/13.5; fields12; value13; large feature30-34 | Header/context17-20, selected control/output glyphs32-44, body14-15, metadata12. No giant selected-letter-plus-empty-form composition. |
| Tabs | Margin30 (test22), 3 DIP short26 underline, white selected text | Narrow underline tabs plus bumper hints; preserve X20CTL seven Studio labels. |
| Actions | Primary/secondary min36, pill radius18; secondary white1A fill |40 DIP rounded actions, glyph-like quick target selection. Keyboard remains accessible rather than reusing reference Focusable=false. |
| Badges | Status30/34 high; feature26 high/radius13; input glyph20; labels12 | One coherent working-context strip and compact action glyph footer. |
| Controller layering |896x512 base + same-size transparent per-control overlays; live stick caps separate | Owned1536x1024 artwork + same-coordinate model paths; consistent view-specific hit testing, focus/selection layers. |
| Focus | Detached ring5-6 outside target, white2.5, lifted #2C3F72, blue glow18/0.45 | Independent X20CTL adorner/geometry focus. Minimal mouse hover; strong keyboard/simulated-gamepad focus. |
| Motion |220ms shelf/page entrance; opacity+14DIP movement; support hint260ms |160ms control/inspector transition,220ms view transition; reduced-motion immediate path. No decorative continuous pulse. |
| Footer |26 DIP inset; compact contextual A/B/LB/RB/RS badges,12DIP captions | Contextual select/back/view/assignments hints and Apply truth, not a large utility status bar. |
| Scroll | Reference shelves hide scroll bars; detail/settings panels use auto scroll, no global large custom thumb in ConsoleTheme | Keep normal editing surface non-scrolling; expanded assignment view uses a small auto-reveal rail. Avoid permanent full-height thumb. |

## Physical inventory and geometry source

Owned source: protocol.py Key table, controller catalog, src/assets/controllers/control-shapes.json (1536x1024 paths), x20/geometry.json and owner-approved rear-geometry.json.

Front inventory: A/B/X/Y, D-pad four directions, two stick axis areas and L3/R3 click roles, LB/RB/LT/RT, View/Back, Menu/Start, Home, Capture C and Turbo T. C/T/Home/system and analog roles are selectable for information without inventing remapping support. Rear: four M slots and visible shoulder/trigger surfaces.

The two owned rear maps disagree on label order: rear-geometry maps M1 left paddle, M2 right paddle, M3 left inner, M4 right inner; control-shapes macros enumerate outer-left, inner-left, inner-right, outer-right. Keep owner-selected rear mapping. Match smoother paths by physical location, never convert artwork ordering into hardware macro slot proof.

Model layout stores artwork reference, view, logical input/interaction role, normalized bounds/path/transform and label anchor. Reuse OWN exact ABXY/D-pad and rear shoulder paths. Stick axes/click share physical cap geometry with separate inspectable roles. Add independently calibrated own-art Home/C/T outlines. Rear labels go inside controls, not floating tags.

Shoulder geometry is authored against rear artwork. Keep it registered there; front editing may expose a small shoulder detail surface rather than falsely placing a full rear trigger on the front shell. Selection stays available for the required front-stage review states.

## Composition to build

- Approved frame and atmosphere retained; compact Studio header, short tabs with bumper badges.
- Working context: model thumbnail, exact Player, local Working Draft, disconnected status, change count; save/export only if implemented safely.
- Balanced hardware/assignment workspace. Hardware: registered image/paths, first-class front/back switch, stick/trigger/source placeholders clearly unknown, complete selectable inventory.
- Inspector: physical control/role, CURRENT -> OUTPUT glyphs, common targets in face/shoulder/dpad/stick groups; full assignment picker is secondary.
- Multiple useful subordinate sections: preview/support state, current draft list/reset, full assignment overview accessible without selecting each control.
- Rear mode: illuminated actual paddle shapes, labels inside, macro/system role information, no speculative writer.
- Reference-like detached focus and contextual glyph footer, with mouse handoff and ownership gates preserved.
- Save/local draft persistence stays separate from controller-side Apply. No hardware state confirmation without engine authorization/readback.

## Gate

Produce the requested17 states, including no-selection front/back, all special controls, all four M contours, full assignments, strong focus, minimum/maximized. Produce a state-sequence recording/GIF and label it honestly. Compare with a reference declarative-layout render if available; never present a fixture render as the full running reference app. Native/hardware integration is still deferred. STOP for owner review before Curves.

