"""X20CTL interface sounds, built to the measured *feel* of a premium console UI (dark, dense, wide, soft, short) with our own notes:
key of E, the rising-fifth E -> B motif, voices made of many detuned sine layers with slow shimmer (no samples, nothing measured note-by-note from anyone's sounds).
Targets (style only): centroid ~450 Hz, nothing audible above ~2 kHz, ~10% below 200 Hz, side/mid about -3 dB, soft attack 60-200 ms, -20 dB after ~130-400 ms.
python ui_dense.py outdir"""
import sys
import numpy as np
from pathlib import Path
from scipy.signal import butter, sosfilt, fftconvolve
import ui_sounds as U
from ui_sounds import save, room
from protos import R, hz

RNG = np.random.default_rng(26102026)


def voice(f, dur, att, dec, voices=10, bright=.85, tr=(3.0, 6.5)):
    """One note as a dense stack: `voices` detuned layers x 4 harmonics, per-channel phases and detune (wide), each layer with its own slow shimmer."""
    n = int(R * dur); t = np.arange(n) / R; tau = dec / 2.3; out = np.zeros((n, 2))
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a); d = np.where(t > att, np.exp(-(t - att) / tau), 1.0); env = a * d
    for side in (0, 1):
        y = np.zeros(n)
        for v in range(voices):
            cents = RNG.uniform(-11, 11); rate = RNG.uniform(*tr); lfo = 1 + RNG.uniform(.07, .16) * np.sin(2 * np.pi * rate * t + RNG.uniform(0, 6.28))
            ff = f * 2 ** (cents / 1200)
            for k, g in ((1, 1), (2, .5 * bright), (3, .22 * bright), (4, .08 * bright)):
                y += g * lfo * np.sin(2 * np.pi * ff * k * t + RNG.uniform(0, 6.28) * (1 + side))
        out[:, side] = y / voices
    return out * env[:, None]


def sub(f, dur, att, dec):  # warm synth bass under the sound: detuned saws through a gentle filter plus a sine, so small speakers still carry it
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n)
    for c in (-7, 0, 7): x += 2 * ((np.cumsum(np.full(n, f * 2 ** (c / 1200))) / R) % 1) - 1
    x = sosfilt(butter(2, 420, fs=R, output="sos"), x / 3) + .7 * np.sin(2 * np.pi * f * t) + .3 * np.sin(4 * np.pi * f * t)
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a); d = np.where(t > att, np.exp(-(t - att) / (dec / 2.3)), 1.0); return x * a * d


class Mix:
    def __init__(s, dur): s.n = int(R * dur); s.y = np.zeros((s.n, 2))
    def add(s, x, at, g=1.0, pan=0.0):
        a = int(at * R); x = x if x.ndim == 2 else np.stack([x, x], 1); x = x[:s.n - a]; L = np.cos((pan + 1) * np.pi / 4) * 1.414; Rr = np.sin((pan + 1) * np.pi / 4) * 1.414
        s.y[a:a + len(x), 0] += x[:, 0] * g * L; s.y[a:a + len(x), 1] += x[:, 1] * g * Rr
    def done(s, db, wet=.35, rt=.45, fc=2300):
        IR = room(rt, seed=33); w = np.stack([fftconvolve(s.y[:, c], IR[c])[:s.n] for c in (0, 1)], 1); y = s.y + wet * w
        sos = butter(2, fc, fs=R, output="sos"); y = np.stack([sosfilt(sos, y[:, c]) for c in (0, 1)], 1)
        fo = int(.09 * R); y[-fo:] *= (np.cos(np.linspace(0, np.pi / 2, fo)) ** 2)[:, None]; return y * 10 ** (db / 20) / np.abs(y).max()


def sound(notes, dur, bass=None, db=-22, **kw):
    m = Mix(dur)
    for nm, at, att, dec, g, pan in notes: m.add(voice(hz(nm), min(dur - at, dec * 2.6 + att), att, dec, **kw), at, g, pan)
    if bass:
        for nm, at, att, dec, g in bass: m.add(sub(hz(nm), min(dur - at, dec * 2.6 + att), att, dec), at, g)
    return m.done(db)


# name: (notes (note, start, attack, decay-20dB, gain, pan)), length, bass
def nav(up): a, b = (("E4", "B4") if up else ("B4", "E4")); return sound([(a, 0, .06, .13, 1, -.2), (b, .075, .06, .15, .9, .2)], .5, [("E3" if up else "B2", 0, .05, .13, .35)])
def enter(): return sound([("E3", 0, .15, .26, 1, -.3), ("B3", .09, .15, .26, .9, 0), ("G#4", .2, .15, .28, .7, .3)], .85, [("E2", 0, .12, .3, .4), ("E3", .1, .12, .25, .2)])
def back(): return sound([("B3", 0, .16, .2, 1, .2), ("E3", .11, .2, .24, 1, -.2)], .88, [("B2", 0, .1, .15, .4), ("E2", .1, .15, .3, .6)])
def prompt(): return sound([("E4", 0, .09, .17, 1, -.15), ("B4", .12, .1, .2, .8, .15)], .7, [("B2", 0, .08, .15, .4)])
def confirm(): return sound([("E3", 0, .16, .22, 1, -.3), ("B3", .08, .16, .22, .9, -.1), ("E4", .17, .16, .24, .8, .1), ("G#4", .26, .16, .26, .7, .3)], .9, [("E2", 0, .12, .3, .38), ("B2", .09, .12, .25, .18)])
def cancel(): return sound([("B3", 0, .17, .26, 1, .2), ("G3", .1, .17, .3, .95, 0), ("E3", .21, .18, .33, .9, -.2)], .9, [("B2", 0, .1, .2, .18), ("G2", .1, .14, .3, .2)])
def panel_open(): return sound([("E3", 0, .135, .33, .9, -.25), ("B3", .1, .15, .34, .9, 0), ("E4", .22, .16, .36, .8, .25)], .95, [("E2", 0, .15, .36, .55)])
def panel_close(): return sound([("E4", 0, .13, .3, .8, .25), ("B3", .1, .14, .34, .9, 0), ("E3", .21, .16, .38, .95, -.25)], .95, [("B2", 0, .1, .25, .15), ("E2", .15, .14, .36, .22)])
def notify(): return sound([("E4", 0, .1, .4, 1, -.25), ("B4", .13, .1, .4, .85, 0), ("E5", .26, .1, .4, .6, .25)], 1.05, [("E2", 0, .1, .4, .4)])


SET = {"nav-next": lambda: nav(True), "nav-previous": lambda: nav(False), "enter": enter, "back": back, "prompt": prompt, "confirm": confirm, "cancel": cancel,
       "panel-open": panel_open, "panel-close": panel_close, "notify": notify}

if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for name, fn in SET.items():
        y = fn(); save(out / f"ui15-{name}.wav", y); print(name, f"{len(y) / R * 1000:.0f} ms")
