# Intro audio

## The opening film - 19 October 2026

`intro.mp4` is the Windows app's opening: 1920x1080, 60 fps, 7.1 s, with AAC stereo sound at 48 kHz. Its picture is rendered from code in the intro project (`logo-preview/intro-remotion`, not part of this repository). The logo it ends on is the same art as `x20ctl-logo.png`.

Its sound is synthesised in the same way as the score below, by a later revision of `tools/build_intro_sound.py` (18 October). That revision uses frosted-glass tones, resonant air tuned to a chord, a warm pad on open fifths in D, and a stereo room. Like the earlier score, it uses no samples, recordings or kits. That revision has not yet been committed to this repository, so running the script here rebuilds the 17 October score, not the film's sound.

The sound is off with reduced motion (no film plays), in development fixtures, and when the player mutes it. Tools → Opening intro → Sound turns it off for every launch.

## The Linux score - 17 October 2026


`intro.wav` (stereo, 48 kHz, 16-bit, 7.1 s) is made entirely by `tools/build_intro_sound.py`, and running the script again rebuilds the file. It contains no samples, recordings or sound kits, and nothing taken from another product.

The script builds every sound itself:

- **A soft FM electric piano.**
- **Plucked strings** (Karplus-Strong). Each starts from a short random burst inside a tuned loop.
- **A pad** whose filter opens and closes while its pitch stays put.
- **A stereo room reverb**, made from decaying filtered random noise. The noise is only the room; it is never heard as a sound of its own.

It is in D minor and stays in the middle register.

What it plays, in time with the picture:

- **The LED eyes** (0.18 to 0.54 s): D4, F4 and A4 on the piano.
- **The outline being drawn**: a quiet plucked figure, alternating left and right.
- **The blades sliding in**: one soft plucked A that speeds up a little, set back in the room. The lock chord swells in backwards out of its own reverb.
- **The lock** (4.03 s): D minor with a ninth, rolled on the piano, a soft low body and the pad opening.
- **The second heartbeat** (4.23 s): a low A. The mark swells with it.
- **The slogan**: E4, then D4.
- **The landing**: a quiet open fifth, D3 and A3, on the piano.

The sound is off with reduced motion, in development fixtures and when the player mutes it (M, Y or the button beside Skip). Tools → Opening intro → Sound turns it off for every launch.

Earlier scores, including the 16 October licensed SND kit edit, are not packaged and are not used.
