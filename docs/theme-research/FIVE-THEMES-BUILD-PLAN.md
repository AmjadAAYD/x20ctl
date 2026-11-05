# Five console themes: build plan (5 Nov 2026)

Owner: "we will only do 5 including default, its 6 / now start building".
The themes are Persona 4 (P4), Persona 3 "Undertow", Alert, Slate and Spider-Verse "Multiverse", plus Default.
The approved mockups are in `previews/console-concepts/themes/out/sheet-*.png`.

## Approach

The brief is "hyperly similar to Default in layout, engine, dashboard and animation", while the buttons, background, shapes, form and colour change.
The approved mockups are Default's own pages restyled. So the five themes are **native skins on Default's layout**, not the old fixed-canvas illustrated pages.

What each theme supplies:
- **Colours, type and corners:** an `AppTheme` with Void, Deep, Accent, OnAccent, Text, Display font and Radius. `ThemeCatalog.Recolor` maps every Default colour onto it.
- **Wallpaper:** one per page (all 18 pages), rendered headless from the approved mockup's static decoration.
  - P4: halftone and noise band.
  - P3: sea strata and caustics.
  - Alert: armour plates, hex grid and rulers.
  - Slate: relief map and circuitry.
  - Spider-Verse: slashes, other worlds bleeding in, rails and pixels.
  - Wallpapers carry decoration only, no UI text.
- **Control faces:** each theme draws its own button, tab and key shape around the real WPF controls (`IllustratedControlFace`).
  - P4: black keys with a pressed edge, solid yellow when chosen.
  - P3: skewed blades, white when chosen, with a red notch.
  - Alert: cut corners, lime when chosen.
  - Slate: chamfered plates, orange when chosen, with bracket corners.
  - Spider-Verse: red misregistration offset, solid red when chosen.
- **Panel faces:** framed cards get the theme's panel shape, drawn behind their real content.
  - P4: viewfinder corners.
  - P3: tide plates with a cyan rule.
  - Alert: cut-corner armour with a lime tick.
  - Slate: chamfered tablet plates with a header strip and node.
  - Spider-Verse: a different world for each panel.
- **Code reuse:** the illustrated engine is reused (spec, wallpaper paths, class-handler skinning). A `Native` flag keeps the fixed-canvas zone, buttons, curves, tools and shell layouts out of these five themes.

The seven old illustrated themes (Vaporwave … Crimson Rebellion) leave the catalogue, as the owner has kept only these six.

## Unchanged

- **Logo and intro:** the owner's own logo and the Default intro stay.
  - His logo is final.
  - Theme intros need concepts that he picks first ("each one its own story").
- **Controller art:** never distorted, tinted or ghosted.
- **Behaviour:** layout, pages, strings and behaviour stay as they are.
- **Hardware:** no hardware writes.

## Success criteria (verifiable)

1. `dotnet build` of X20Ctl.Product succeeds with no new warnings in the changed files.
2. `ThemeCatalog.All` lists exactly Default, P4, P3 Undertow, Alert, Slate and Spider-Verse. The Themes page lets you pick them again, with the maintenance note removed.
3. Each theme has 18 wallpapers at `Brand/illustrated/<id>/wallpapers/`, and the build copies them to the output.
4. `--review-themes <dir> <id>` runs headless for each of the five without exceptions. It renders Zone, Dashboard, Buttons and the other pages.
5. Inspecting those renders shows:
   - the theme's background, control shapes, panel shapes, type and colours on Default's layout;
   - no clipped or unreadable text in the main states;
   - controller art untouched.
6. Default renders exactly as before, with nothing gated on a theme leaking into it.
