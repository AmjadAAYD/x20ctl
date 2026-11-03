"""Keys and buttons, each its own design (no shared recipe, no pitch-shifted copies). Same family: dark, dense, soft, key of E, own notes.
KEYS are tiny and fatigue-proof; BUTTONS are a little bigger and more satisfying. python ui_dense3.py outdir"""
import sys
import numpy as np
from pathlib import Path
import ui_dense as D
from ui_dense import voice, sub, Mix, R, hz
from ui_dense2 import glide_voice
from ui_sounds import save, thump


def clean(f, dur, att, dec, partner=None, g2=0.0):
    """One clean round note: a sine (plus an optional soft partner partial), smooth attack, smooth exponential decay. No chorus, no slide."""
    n = int(R * dur); t = np.arange(n) / R; x = np.sin(2 * np.pi * f * t)
    if partner: x = x + g2 * np.sin(2 * np.pi * f * partner * t)
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a); return x * a * np.exp(-t / (dec / 2.3))


# ---------------- keys (typing): clean, round, single notes, 110-200 ms, peak about -30 dBFS. Different in pitch, attack, decay and partner partial.
def key_1():  # round pop: E4, soft, a faint octave
    m = Mix(.3); m.add(clean(hz("E4"), .2, .010, .09, 2, .18), 0, 1, -.1); return m.done(-30, wet=.2, fc=2000)
def key_2():  # small dot: G#4, quicker, pure
    m = Mix(.25); m.add(clean(hz("G#4"), .16, .008, .06), 0, 1, .12); return m.done(-31, wet=.2, fc=2000)
def key_3():  # soft knock: B3, a touch slower to open, with a fifth above it
    m = Mix(.35); m.add(clean(hz("B3"), .25, .016, .12, 1.5, .15), 0, 1, -.15); return m.done(-30, wet=.25, fc=1800)
def key_4():  # hollow tock: C#4 with its twelfth, a touch slower to open, nothing on top
    m = Mix(.3); m.add(clean(hz("C#4"), .22, .02, .1, 3, .3), 0, 1, .15); m.add(sub(hz("E2"), .1, .01, .05), 0, .15); return m.done(-30, wet=.25, fc=1700)
def key_delete():  # low and plain: E3, a bit longer, no partner
    m = Mix(.35); m.add(clean(hz("E3"), .25, .014, .13), 0, 1, 0); m.add(sub(hz("E2"), .12, .01, .06), 0, .25); return m.done(-30, wet=.25, fc=1600)


# ---------------- buttons: 250-450 ms, peak about -27 dBFS
def button_1():  # pad pat: three notes patted in together as a soft chord, no thump
    m = Mix(.5); m.add(voice(hz("E3"), .4, .07, .16, voices=9), 0, 1, -.3); m.add(voice(hz("B3"), .4, .07, .16, voices=9), .008, .8, 0); m.add(voice(hz("G#4"), .4, .07, .16, voices=7, bright=.3), .016, .5, .3); return m.done(-27, wet=.4)
def button_2():  # step up: two high-middle notes climbing, airy, no bass
    m = Mix(.55); m.add(voice(hz("B3"), .3, .04, .1, voices=8), 0, 1, -.3); m.add(voice(hz("E4"), .4, .05, .2, voices=9), .09, 1, .3); return m.done(-27, wet=.5)
def button_3():  # glint then knock: one clear high note first, the low knock landing just after it
    m = Mix(.65); m.add(voice(hz("B4"), .45, .006, .2, voices=6, bright=.3), 0, .7, .3); m.add(thump(hz("E2"), .14, sweep=1.5), .09, .8); m.add(voice(hz("E3"), .25, .01, .07, voices=6), .09, .6, -.1); return m.done(-27, wet=.4)
def button_4():  # double pat: the same soft low note twice, a quick step apart
    m = Mix(.55); m.add(voice(hz("B3"), .3, .035, .1, voices=8), 0, .8, -.15); m.add(voice(hz("B3"), .35, .035, .13, voices=8), .085, 1, .15); m.add(sub(hz("E2"), .3, .03, .1), .085, .4); return m.done(-27, wet=.4)


SET = {"key-1": key_1, "key-2": key_2, "key-3": key_3, "key-4": key_4, "key-delete": key_delete, "button-1": button_1, "button-2": button_2, "button-3": button_3, "button-4": button_4}
if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for name, fn in SET.items(): y = fn(); save(out / f"ui16-{name}.wav", y); print(f"{name:11s} {len(y) / R * 1000:4.0f} ms")
