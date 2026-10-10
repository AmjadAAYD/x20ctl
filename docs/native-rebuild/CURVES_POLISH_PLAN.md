# Curves review corrections — 2026-10-09

Owner accepted the core Curves design and requested targeted corrections before final approval. Preserve its controller/graph composition, channel shelf, presets, point editing, deadzone layout, visual identity and motion. Buttons remains frozen. Licensing remains a separate discussion.

- Hide All assignments and Front/Back controls in Curves; physical channel selection still chooses the correctly registered front/rear artwork automatically.
- Label both INPUT % and OUTPUT % on the plot.
- Retain the base preset independently from custom edits. Display Base preset or Custom / based on the named preset, with an explicit modified state.
- Use human-facing preview copy; keep the unverified firmware interpolation explanation in a tooltip and review documentation.
- Use L Stick / R Stick instead of LS / RS, without reducing target size.
- Provide a contextual selected-point readout with separate Input/Output values and arrow-key guidance. Preserve precise source coordinates; display percentages with two decimals.
- Keep numeric deadzone readouts and keyboard steps; provide an explicit step hint.
- Expose Reset curve for the selected channel. Move Reset all curves to a secondary menu, with scope clear in its label.

Verify preset provenance and reset isolation, the scoped controls, point editing, deadzone keyboard adjustments, minimum/maximized and reduced motion, then refresh motion evidence. Stop for Curves review before Macros.

Later integration only: real input/output overlays sourced from the native engine, separate stick radial/X-Y context versus trigger travel context, and firmware interpolation verification. No fabricated live dot or hardware state in this pass.
