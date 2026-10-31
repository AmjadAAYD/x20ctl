# Console themes in the app

The themes from `previews/console-concepts/themes` (Default, Pulse, Spotlight, Nexus, Rebel, Tide, Dusk,
Volt, Yozakura) brought into X20CTL itself. Hybrid was retired on 1 Nov 2026.

## This pass

1. **Theme catalog** (`X20Ctl.Product/Themes/ThemeCatalog.cs`): each theme's palette, type and corner radii, applied
   to the shared resources at startup (`DS.*`, `Zone.*`, the legacy `Ink/Text/Blue/...` brushes, the primary button)
   before any window is built. Default changes nothing.
2. **Saved choice**: `ProductPreferences.Theme` (string key `theme`, same atomic write and baseline check as the
   other settings). Unknown or missing values fall back to Default.
3. **Tools → Themes**: a tile on the Tools hub opening a page of theme cards (name, swatches, In use); choosing one
   saves it and offers a restart, since the whole interface is rebuilt from the new resources.
4. **Backdrop**: the Zone's two moving light pools take the theme's colours.
5. **Intros**: each theme plays its own silent film (from `intros.py`, cut where the logo has settled, before the
   preview's own home reveal); the cover laid over the last frame is that theme's logo and flies to the header mark
   exactly as the shipped intro does. Default keeps the shipped film and sound untouched.
6. **Review**: `--review-themes DIRECTORY THEME` renders the Zone, Studio and Tools offscreen (no window shown) with
   the theme applied, plus `review.json` with the resolved colours.

## Not in this pass

The per-page relayouts in the previews (23 pages per theme with their own compositions) are not reproduced: pages keep
their current layout and take the theme's colours, type and radii. Colours hard-coded in C# (about 180) stay as they
are unless they sit on a themed surface that looks wrong in review.

## Success criteria

- `dotnet build` of X20Ctl.Product succeeds; `X20Ctl.Zone.Tests` pass, including a new preferences test for `theme`.
- For every theme, `--review-themes` writes Zone, Studio and Tools renders with no binding errors, and the renders
  visibly carry that theme's background, surfaces, accent and type (inspected, not assumed).
- Default renders are unchanged in colour from `main`.
- Each themed intro film's last frame matches the cover logo's position (checked by overlay).
- No window is shown on the owner's screen during any check.
