# Separate visual systems for seven X20CTL themes

The previous shared-layout implementation is not accepted. Leave that uncommitted work intact while developing replacement compositions. This design pass creates independent Controller Zone, Buttons, Curves and Tools screens for every theme, using the existing artwork and real controller art.

## Invariants

Keep the app's actual navigation names, four equal player slots, model capabilities and draft/apply distinction. Preview interactions change local prototype state only; they do not claim to configure a device. No fabricated connection, battery, firmware, measurements or completed hardware actions. Existing controller logic will be reused during WPF implementation; visual page construction will be separate per theme.

## Distinct systems

| Theme | Spatial composition | Navigation | Control construction | Motion |
|---|---|---|---|---|
| Vaporwave | Airy showroom and suspended inspector panes | Curved chrome dock below the workspace | Chrome rails, broad glass sheets, extended geometric type | Slow horizontal glides and short dissolves |
| Frutiger Aero | Buoyant circular work areas on a waterline | Rounded navigation capsules around the workspace | Gel caps, glass bubbles, liquid slider tracks | Small buoyant lifts, expanding panes |
| Gothic | Tall cathedral bays and engraved instrument tablets | Vertical iron index and pointed section markers | Chamfered iron, bone rules, lancet arches, readable serif headings | Restrained shutter reveals |
| Manga | Asymmetric editorial panels and large gutters | Margin index and ink chapter labels | Thin ink frames, solid red selection stamp, halftone fills | Directional panel cuts |
| Tokyo Night | Compact, calm desktop workspace | Dense left navigation and contextual inspector | Square fields, restrained separators, compact switches | Short quiet crossfades |
| Synthwave | Slanted instrument console and arcade command deck | Angled cartridge rail and bumper controls | Bevelled keys, segmented selectors, cyan/magenta outlines | Directional neon tray slides |
| Crimson Rebellion | Staggered collage and overlapping poster sections | Cut-paper index strips | Thick stencil controls, torn edges, hard offset shadows | Hard snaps and small paper shifts |

## Deliverables and review

- Seven independently authored HTML/CSS compositions, with four working local preview routes each.
- Twenty-eight offscreen screenshots at 1600×900 and an overview comparing the seven Buttons screens.
- Verify route changes, selection, draft remapping, local curve edits, keyboard focus, reduced motion and overflow. Inspect the actual screenshots.
- Review shape, hierarchy and layout differences in addition to colour. No production-app completion claim from these concepts.
- Continue native WPF work only after the separate design direction is established; the rejected common screen structure is not the implementation template.
