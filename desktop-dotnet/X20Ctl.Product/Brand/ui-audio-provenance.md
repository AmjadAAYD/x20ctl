# Interface sounds

`Brand/ui/*.wav` (seven files, 48 kHz 16-bit stereo, 0.4 to 2 s) are made entirely by `tools/build_ui_sounds.py`, and running the script again rebuilds them byte for byte. They contain no samples, recordings, sound kits or noise sources, and nothing taken from another product.

Each note is built from sine partials: two voices tuned 5 cents apart, a smooth fade-in, nothing below about 250 Hz and one small shared room. The notes are from E major, the key of the opening film's sound. They are quiet on purpose: clicks peak at -14 dBFS, hovers at -22 to -29 dBFS, tab switch at -22 and choosing a controller at -16.

| file | when |
|---|---|
| `tab.wav` | switching a tab |
| `click.wav` | clicking a game or a tool |
| `button.wav` | any other button |
| `hover-game.wav` | pointer over a game |
| `hover-controller.wav` | pointer over a controller in the Controller Zone |
| `hover-tool.wav` | pointer over a tool |
| `pick.wav` | choosing a controller |

They follow the player's Sound switch (Tools, Opening intro), stay silent while the app is in the background and in review runs, and a hover cannot repeat faster than every 70 ms overall or 250 ms per kind. The owner has chosen them from prototypes by description and measurement; nobody has yet reported hearing them in the app.
