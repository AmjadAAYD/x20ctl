# Dynamic Controller Zone interaction

Date: 8 October 2026. Latest owner brief supersedes the static large-Player-1 layout. Desktop automation was revoked during this task; do not control windows, launch the GUI, capture the desktop or record screen video without renewed user authorization.

## Behavior

Genuine first-launch preview starts with four empty, equally sized 2x2 docks. No automatic assignment or initial hero. Saved assignments remain a future persistence integration; this milestone does not read/migrate real configuration.

Every player owns a persistent DockView. Click/Enter focuses that exact player, empty or assigned. Its container moves/scales to the hero position while the other three move to secondary docks. Changing focus retains each player's model/art/state; content is not copied into a single interchangeable hero view. Escape/Back returns the same four views to their equal overview positions.

Normal keyboard: Tab/arrows navigate, Enter expands, Escape collapses. F6 enables labelled simulated gamepad input; arrows navigate, A/Enter expands, B/Escape collapses; mouse movement returns to normal navigation. No real controller polling.

F9 cycles explicit empty/one-controller/multiple-controller development fixtures. One-controller overview does not auto-focus Player 1. Multiple fixture: P1 X20, P2 X15, P3 empty, P4 X05 Pro. These are mock assignments and do not establish device identity or write support.

## Motion implementation

150 ms hero-details retract, 240 ms position/scale movement, 160 ms artwork/details settle: 550 ms total. Each persistent view is laid out at its destination and given an inverse scale/translation from its currently rendered bounds, then those transforms animate to identity. Both old and new hero move; opacity is secondary. A subdued blue focus bloom travels with the selected dock while global blue-left/amber-right atmosphere remains stable.

Rapid requests cancel pending phases and retarget from currently rendered transforms. Resize cancels movement and snaps to the current requested layout. Reduced motion completes the same state changes immediately. No cartoon bounce or continuous ambient animation.

## Scope and evidence

Implemented in X20Ctl.Zone/ZoneState.cs, X20Ctl.Product/ZoneScene.xaml(.cs) and DockView.xaml(.cs). Studio/selector actions only show checkpoint feedback; no next page/backend implemented.

19 state/layout/input-ownership tests pass. Release WPF compilation passes. The new GUI/transition has NOT been executed, screenshot-reviewed or recorded following the user's stop-PC-control request. Earlier static-zone screenshots do not prove this interaction works. Manual visual review remains required before another page.
