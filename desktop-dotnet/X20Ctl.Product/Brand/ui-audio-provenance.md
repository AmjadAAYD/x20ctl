# Interface sounds

`Brand/ui/tab.wav` (0.9 s) and `Brand/ui/pick.wav` (2 s) are 48 kHz 16-bit stereo, made entirely by `tools/build_ui_sounds.py`, and running the script again rebuilds them byte for byte. They contain no samples, recordings, sound kits or noise sources, and nothing taken from another product.

Each note is built from sine partials: two voices tuned 5 cents apart, a smooth fade-in, nothing below about 250 Hz and one small shared room. The notes are from E major, the key of the opening film's sound. They are quiet on purpose: tab.wav peaks at -22 dBFS and pick.wav at -16 dBFS.

| file | when |
|---|---|
| `tab.wav` | the tab switch, and (owner, 25 October 2026) every click, button and hover; hovers play it at half volume |
| `pick.wav` | choosing a controller |

They follow the player's Sound switch (Tools, Opening intro), stay silent while the app is in the background and in review runs, and a hover cannot repeat faster than every 70 ms overall or 250 ms per kind. The owner chose them from prototypes by description and measurement; nobody has yet reported hearing them in the app.
