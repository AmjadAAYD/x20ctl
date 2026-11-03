"""The rest of X20CTL's interface sound family, plus variant pools for the sounds heard most. Same recipe as ui_dense.py (dark, dense, wide, soft, short;
key of E; our own notes), one equivalent per remaining kind of sound in the reference recording, each built to that sound's measured traits (length, attack,
brightness, width), never to its notes or partials. python ui_dense2.py outdir"""
import sys
import numpy as np
from pathlib import Path
from scipy.signal import butter, sosfilt
import ui_dense as D
from ui_dense import voice, sub, Mix, sound, R, hz
from ui_sounds import save, thump
from ui_console import pop


def glide_voice(f0, f1, dur, att, voices=7, bright=.7):
    """A dense stack gliding in pitch (the launch swell): detuned layers, per-channel phases, slow shimmer."""
    n = int(R * dur); t = np.arange(n) / R; out = np.zeros((n, 2)); g = (f1 / f0) ** (t / dur)
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a)
    for side in (0, 1):
        y = np.zeros(n)
        for v in range(voices):
            ff = f0 * g * 2 ** (D.RNG.uniform(-12, 12) / 1200); lfo = 1 + D.RNG.uniform(.07, .15) * np.sin(2 * np.pi * D.RNG.uniform(3, 6.5) * t + D.RNG.uniform(0, 6.28)); ph = 2 * np.pi * np.cumsum(ff) / R
            for k, w in ((1, 1), (2, .5 * bright), (3, .2 * bright)): y += w * lfo * np.sin(k * ph + D.RNG.uniform(0, 6.28))
        out[:, side] = y / voices
    return out * a[:, None]


def air(dur, lo, hi, peak_t, width, seed=1):  # a faint breath of filtered air that follows a swell
    n = int(R * dur); t = np.arange(n) / R; z = np.random.default_rng(seed).standard_normal(n); y = np.zeros(n)
    for k in range(5):
        c = k / 4; f0 = lo * (hi / lo) ** c; y += sosfilt(butter(2, [f0 * .8, f0 * 1.25], "bandpass", fs=R, output="sos"), z) * np.exp(-((t - dur * (.2 + .6 * c)) / (dur * .2)) ** 2)
    return y * np.exp(-((t - peak_t) / width) ** 2)


def tap(note, level=1.0, up=1.12, dur=.16, dec=.045, bright=2200):  # a very short soft pop that settles into its note
    return pop(hz(note) * up, hz(note), dur, dec, settle=.02, partials=((1, 1), (2, .2)), att=.004, bright=bright) * level


# ------------------------------------------------ one equivalent for each remaining reference sound
def launch_hum():      # 3.2 s, attack 615 ms, centroid ~260 Hz, half the energy under 200 Hz: a low breathing drone
    return sound([("E3", 0, .6, 1.5, .9, -.3), ("B3", .15, .6, 1.4, .7, .3), ("E4", .3, .7, 1.2, .35, 0)], 3.2, [("E2", 0, .5, 1.6, .9), ("B2", .2, .5, 1.3, .35)], tr=(1.5, 3.5))
def rise():            # 2.1 s, attack ~700 ms, centroid ~450 Hz: notes of an E pentatonic climbing in slow swells
    return sound([("E3", 0, .55, .6, .8, -.3), ("B3", .3, .55, .6, .8, -.1), ("E4", .6, .55, .6, .85, .1), ("G#4", .9, .55, .6, .8, .2), ("B4", 1.2, .55, .6, .7, .3)], 2.1, [("E2", 0, .45, .8, .5)])
def unlock_chime():    # 1.45 s, attack 25 ms, then four tiny rising ticks: a clear chime that settles
    m = Mix(1.5); m.add(voice(hz("E4"), 1.3, .025, .5, voices=8, bright=1.0), 0, 1, -.1); m.add(voice(hz("B4"), 1.0, .03, .4, voices=6, bright=.8), .04, .6, .15)
    for i, nm in enumerate(("E6", "F#6", "G#6", "B6")): m.add(tap(nm, .22, up=1.04, dur=.2, dec=.07, bright=3000), .8 + .075 * i, 1, -.3 + .2 * i)
    m.add(sub(hz("E2"), 1.2, .05, .5), 0, .35); return m.done(-22)
def home_ambience():   # 3.1 s, attack 235 ms, quarter of the energy under 200 Hz: a calm slow-shimmer chord with two sparkle notes
    m = Mix(3.2)
    for nm, at, g, pan in (("E3", 0, 1, -.3), ("B3", .1, .9, .3), ("E4", .25, .7, 0), ("G#4", .4, .6, .2)): m.add(voice(hz(nm), 3.0, .3, 1.1, voices=12, tr=(1.5, 4)), at, g, pan)
    m.add(voice(hz("B5"), .8, .05, .25, voices=6), 1.95, .22, -.4); m.add(voice(hz("E6"), .8, .05, .25, voices=6), 2.2, .15, .4); m.add(sub(hz("E2"), 2.8, .3, 1.0), 0, .5); return m.done(-23, rt=.7)
def toast():           # 1.25 s, attack 230 ms, centroid ~630 Hz: a soft double swell with one faint high ping
    m = Mix(1.35); m.add(voice(hz("B3"), 1.1, .23, .55, voices=10), 0, 1, -.25); m.add(voice(hz("E4"), 1.0, .23, .55, voices=10), .18, .85, .25); m.add(voice(hz("G#5"), .6, .02, .2, voices=5, bright=.5), .55, .16, .3)
    m.add(sub(hz("E2"), 1.0, .2, .4), 0, .3); return m.done(-23)
def hub_move_1():      # 0.66 s, attack 45 ms, decay 395 ms: one soft note with a long, gentle tail
    return sound([("G#3", 0, .045, .4, 1, -.15), ("E4", .03, .06, .3, .45, .2)], .7, [("E2", 0, .05, .3, .4)])
def hub_move_2():      # 1.1 s: a small blip, then a held note arrives
    m = Mix(1.2); m.add(tap("B3", 1.0, up=1.2, dur=.3, dec=.08), 0, 1, -.2); m.add(voice(hz("E4"), .8, .1, .5, voices=10), .38, .9, .15); m.add(sub(hz("E2"), .7, .1, .4), .38, .35); return m.done(-23)
def menu_open():       # 0.75 s, centroid ~370 Hz, 43% under 200 Hz: a flat, held low tone with a quick release
    return sound([("E3", 0, .1, 2.2, 1, -.2), ("B3", 0, .12, 2.2, .7, .2)], .75, [("E2", 0, .1, 2.0, .8)])
def page_open():       # 0.54 s: a soft thump, then a low tone that holds
    m = Mix(.6); m.add(thump(hz("E2"), .16, sweep=1.4), 0, .6); m.add(voice(hz("E3"), .5, .12, 1.5, voices=8), .02, 1, -.1); m.add(voice(hz("B3"), .45, .12, 1.4, voices=6), .03, .5, .2); m.add(sub(hz("E2"), .5, .08, 1.4), 0, .5); return m.done(-23)
def dialog_open():     # 0.77 s: a soft click, then a chord swells in with a faint high shimmer
    m = Mix(.9); m.add(tap("E4", .8, up=1.2, dur=.1, dec=.03), 0, 1); m.add(voice(hz("E3"), .7, .35, .5, voices=9), .12, 1, -.2); m.add(voice(hz("B3"), .7, .35, .5, voices=9), .14, .8, .2)
    m.add(voice(hz("B5"), .5, .04, .22, voices=5, bright=.4), .32, .12, 0); m.add(sub(hz("E2"), .6, .3, .4), .12, .4); return m.done(-23)
def voice_panel():     # 0.56 s, near mono, centroid ~1.5 kHz: a clearer chime with a ladder of harmonics
    m = Mix(.65); m.add(voice(hz("E5"), .5, .006, .27, voices=3, bright=1.3), 0, 1, 0); m.add(sub(hz("E3"), .4, .02, .2), 0, .25); return m.done(-24, wet=.2)
def message_sent():    # 0.89 s, near mono, then a soft upward breath: two quick rising notes that carry away
    m = Mix(1.0); m.add(voice(hz("E5"), .5, .03, .3, voices=5, bright=.5), 0, .8, 0); m.add(voice(hz("B5"), .6, .03, .35, voices=5, bright=.4), .09, .65, 0); m.add(air(.6, 700, 2400, .5, .16, 9), .3, .05, 0)
    m.add(sub(hz("E3"), .4, .03, .25), 0, .25); return m.done(-24, wet=.25)
def launch_whoosh():   # 0.84 s, attack ~640 ms: a dense swell gliding up an octave, a breath of air, a clean release
    m = Mix(.95); m.add(glide_voice(hz("E3"), hz("E4"), .85, .6), 0, 1, -.1); m.add(glide_voice(hz("B3"), hz("B4"), .85, .6, bright=.4), 0, .5, .2); m.add(air(.85, 600, 3000, .6, .2, 4), 0, .07, 0)
    m.add(sub(hz("E2"), .8, .5, .6), 0, .45); return m.done(-22, fc=2600)


# ------------------------------------------------ variant pools (so repeated actions do not repeat)
PAIRS = {"a": ("E4", "B4"), "b": ("F#4", "C#5"), "c": ("G#4", "D#5"), "d": ("B3", "F#4"), "e": ("C#4", "G#4")}
ROOT = {"a": "E3", "b": "F#3", "c": "G#3", "d": "B2", "e": "C#3"}
def nav_var(key, up):
    a, b = PAIRS[key]; a, b = (a, b) if up else (b, a)
    return sound([(a, 0, .055, .13, 1, -.2), (b, .075, .06, .15, .9, .2)], .5, [(ROOT[key], 0, .05, .13, .3)])
def enter_var(i):
    chords = [("E3", "B3", "G#4"), ("F#3", "C#4", "A4"), ("G#3", "D#4", "B4")] ; c = chords[i]
    return sound([(c[0], 0, .15, .26, 1, -.3), (c[1], .09, .15, .26, .9, 0), (c[2], .2, .15, .28, .7, .3)], .85, [({0: "E2", 1: "F#2", 2: "G#2"}[i], 0, .12, .3, .5)])
def button_var(i):
    nm = ["E4", "F#4", "G#4", "B4"][i]; m = Mix(.3); m.add(tap(nm, 1.0, up=1.1, dur=.2, dec=.06), 0, 1, -.1 + .07 * i); m.add(sub(hz("E2"), .15, .02, .08), 0, .25); return m.done(-26, wet=.3)
def key_var(i):
    nm = ["E4", "G#4", "B4", "C#5"][i]; m = Mix(.2); m.add(tap(nm, 1.0, up=1.06, dur=.12, dec=.03, bright=2600), 0, 1, -.15 + .1 * i); return m.done(-28, wet=.2)
def key_delete():
    m = Mix(.22); m.add(tap("B3", 1.0, up=.9, dur=.14, dec=.04), 0, 1); m.add(sub(hz("E2"), .12, .01, .06), 0, .2); return m.done(-27, wet=.2)


SET = {"launch-hum": launch_hum, "rise": rise, "unlock-chime": unlock_chime, "home-ambience": home_ambience, "toast": toast, "hub-move-1": hub_move_1, "hub-move-2": hub_move_2,
       "menu-open": menu_open, "page-open": page_open, "dialog-open": dialog_open, "voice-panel": voice_panel, "message-sent": message_sent, "launch-whoosh": launch_whoosh}
for k in "abcde":
    SET[f"nav-next-{k}"] = (lambda k=k: nav_var(k, True)); SET[f"nav-previous-{k}"] = (lambda k=k: nav_var(k, False))
for i in range(3): SET[f"enter-{i + 1}"] = (lambda i=i: enter_var(i))
for i in range(4): SET[f"button-{i + 1}"] = (lambda i=i: button_var(i)); SET[f"key-{i + 1}"] = (lambda i=i: key_var(i))
SET["key-delete"] = key_delete

if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for name, fn in SET.items():
        y = fn(); save(out / f"ui16-{name}.wav", y); print(f"{name:16s} {len(y) / R * 1000:5.0f} ms")
