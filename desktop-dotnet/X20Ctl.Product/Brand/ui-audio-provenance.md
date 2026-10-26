# Interface sounds

`Brand/ui/<theme>/<role>.wav` are 24 kHz 16-bit stereo, 15 roles for each of ten themes (default, pulse, spotlight, rebel, hybrid, blossom, nexus, tide, dusk, volt). Nine themes are built entirely by `tools/ui_sound_themes/build.py`, and running it again rebuilds them byte for byte. They contain no samples, recordings, sound kits or outside material; nothing is taken from another product.

Every theme uses one voice: many sine layers slightly detuned against each other with a slow shimmer, a soft attack, a small shared room, a low-pass near 2 kHz and a quiet synth bass underneath. A theme changes only the key, the scale, the tempo and the room. Keys (typing) are single clean round notes. The generator design follows the style traits of a calm console interface (dark, dense, wide, soft, short); it never uses, measures note by note or resynthesises anyone's sounds.

`default/` is the owner-approved base family. Its WAVs were generated with the same modules (`ui_dense.py`, `ui_dense2.py`, `ui_dense3.py`) and are committed as generated, not rebuilt by the script.

| role | when |
|---|---|
| `nav-next-a/b/c` | switching a tab (one of three, never the same twice in a row) |
| `button-1..4` | buttons and clicks on games and tools |
| `enter-1/2` | choosing a controller |
| `back` | the Back button |
| `key-1..4`, `key-delete` | typing in a text box, and Backspace or Delete |

Hover is silent. The sounds follow the player's Sound switch (Tools, Opening intro), stay silent while the app is in the background and in review runs, and typing is limited to one sound every 45 ms. Each theme also has an intro sound (build.py `--intros`) that is held back until the app has selectable themes. Checked by measurement only (peaks, clicks, spectrum, length); nobody has yet reported hearing them in the app.
