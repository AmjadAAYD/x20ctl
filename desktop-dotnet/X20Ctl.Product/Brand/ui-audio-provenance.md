# Interface sounds

`Brand/ui/*.wav` are 15 sounds, 24 kHz 16-bit stereo, the same for every theme. They are made entirely by the modules in `tools/ui_sounds/`: sine partials and shaped noise, no samples, recordings, sound kits or outside material, and nothing taken from another product. The WAVs are the owner-approved renders; `tools/ui_sounds/build.py` rebuilds them to a folder you name. A rebuild sounds the same but is not byte-identical, because the slow shimmer of each layer is drawn from one random stream.

The voice is many sine layers slightly detuned against each other, a soft attack, one small shared room, a low-pass near 2 kHz and a quiet synth bass underneath, in the key of E like the opening film. Keys (typing) are single clean round notes. The design follows the style traits of a calm console interface (dark, dense, wide, soft, short); it never uses, measures note by note or resynthesises anyone's sounds.

| file | when |
|---|---|
| `nav-next-a/b/c` | switching a tab (one of three, never the same twice in a row) |
| `button-1..4` | buttons and clicks on games and tools |
| `enter-1/2` | choosing a controller |
| `back` | the Back button |
| `key-1..4`, `key-delete` | typing in a text box, and Backspace or Delete |

Hover is silent. The sounds follow the player's Sound switch (Tools, Opening intro), stay silent while the app is in the background and in review runs, and typing is limited to one sound every 45 ms. Checked by measurement only (peaks, clicks, spectrum, length); nobody has yet reported hearing them in the app.
