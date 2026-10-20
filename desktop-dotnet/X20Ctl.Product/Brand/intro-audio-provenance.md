# Intro audio

## The opening film - 19 October 2026

`intro.mp4` is the Windows app's opening: 1920x1080, 60 fps, 7.1 s, with AAC stereo sound at 48 kHz. Its picture is rendered from code in the intro project (`logo-preview/intro-remotion`, not part of this repository). The logo it ends on is the same art as `x20ctl-logo.png`.

Its sound is synthesised in the same way as the score below, by a later revision of `tools/build_intro_sound.py` (18 October). That revision uses frosted-glass tones, resonant air tuned to a chord, a warm pad on open fifths in D, and a stereo room. Like the earlier score, it uses no samples, recordings or kits. That revision has not yet been committed to this repository, so running the script here rebuilds the 17 October score, not the film's sound.

Camera: after 19 October the camera zooms out gradually while the outline is drawn (1.2 to 2.28 s), so it no longer swings down and back up at about 2 s. The film is Draft 7 (`x20ctl-intro-v5-draft7-impact.mp4`), whose strike has a bolder impact frame (4.03 to 4.10 s). It differs from Draft 6 only in those frames, and its sound track is byte-identical to the earlier film's.

The sound is off with reduced motion (no film plays), in development fixtures, and when the player mutes it. Tools → Opening intro → Sound turns it off for every launch.

## The film on Linux - 20 October 2026

The Linux Studio plays the same film, picture and sound. `tools/linux-studio/build_intro_film.py` makes two files from `intro.mp4`, kept in `desktop-dotnet/X20Ctl.Linux/Brand`:

- `intro-film.frames`: the picture as 426 standalone WebP stills in a zip (60 fps, 1600x900), so no video codec is needed on the system.
- `intro-film.wav`: the film's own sound, unchanged, as 48 kHz 16-bit stereo PCM.

Nothing is added to or taken from the film. The Linux app no longer packages `intro.wav` below.

## The Linux score - 17 October 2026


Until 20 October the Linux Studio played this score over an intro drawn in code; it now plays the film above. `intro.wav` (stereo, 48 kHz, 16-bit, 7.1 s) is made entirely by `tools/build_intro_sound.py`, and running the script again rebuilds the file. It contains no samples, recordings or sound kits, and nothing taken from another product.

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
