"""The X20CTL opening film's sound, synthesised here: strings in E and a low piano on the strike.

Owner requests 21-25 Oct 2026: the old sound was too loud (-16.3 LUFS), had a silent gap then a big surge before the strike, and a
2.3 kHz screech. This replaces it with one piece in one key (E), picked from prototypes by the owner ("strings in E", then the low piano boom).
Everything is made in this file from sine and saw partials, with one shared room: no samples, recordings, kits or third-party material.

The story, in time with the film (the strike is at 4.03 s, eight beats at about 119 BPM):
  0.2-4.0    bowed strings in E: Em, then Cmaj9, Am, B, each longer and fuller, over a low E that grows (no gap before the strike)
  3.4        the gem: one soft B5 glint
  4.03       the strike: a low piano chord on E major (E1 E2 B2 E3 G#3 B3), rolled 6 ms apart, with a soft low E and the strings' bloom
  5.12 5.50  the slogan: two soft falling string notes, F#4 then E4
  4.9-7.1    an open fifth on E fading out

Loudness: -19 LUFS integrated, true peak below -3.5 dBTP, a fade in at both ends. Deterministic: running it again rebuilds the same samples.

    python tools/build_intro_sound_strings.py [output.wav]
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve, lfilter, resample_poly

R = 48000; TOTAL = 7.1; N = int(R * TOTAL); STRIKE = 4.03
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "intro-sound-strings-e.wav"

def hz(n):
    s = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[n[0]]; i = 1
    if n[1] == "#": s += 1; i = 2
    elif n[1] == "b": s -= 1; i = 2
    return 440 * 2 ** ((s + 12 * (int(n[i:]) + 1) - 69) / 12)


def lp(x, c, o=2): return sosfilt(butter(o, c, fs=R, output="sos"), x)


def hp(x, c, o=2): return sosfilt(butter(o, c, "high", fs=R, output="sos"), x)


class Mix:
    def __init__(s, rt=2.4, room_lp=4500, seed=7):
        s.dry = np.zeros((N, 2)); s.send = np.zeros((N, 2)); s.rt = rt; s.rlp = room_lp; s.seed = seed

    def add(s, x, at, gain=1, pan=0, wet=.3):
        a = int(at * R)
        if a >= N: return
        x = x[:N - a]; L = np.cos((pan + 1) * np.pi / 4); Rr = np.sin((pan + 1) * np.pi / 4)
        s.dry[a:a + len(x), 0] += x * gain * L; s.dry[a:a + len(x), 1] += x * gain * Rr
        s.send[a:a + len(x), 0] += x * gain * L * wet; s.send[a:a + len(x), 1] += x * gain * Rr * wet

    def room(s):
        # one shared reverb bus: a decaying, low-passed tail (the room only, never heard as a sound of its own)
        rng = np.random.default_rng(s.seed); n = int(R * s.rt * 1.3); t = np.arange(n) / R
        env = np.exp(-6.9 * t / s.rt); out = []
        for c in range(2):
            ir = hp(lp(rng.standard_normal(n), s.rlp) * env, 120) * np.minimum(1, t / .02)
            out.append(np.concatenate([np.zeros(int(.022 * R)), ir / np.sqrt((ir ** 2).sum())]))
        return np.stack([o[:n] for o in out], 1)

    def render(s):
        ir = s.room(); w = np.stack([fftconvolve(s.send[:, c], ir[:, c])[:N] for c in range(2)], 1)
        return s.dry + w


def env_ar(n, a, rel, curve=2.0):
    t = np.arange(n) / R; e = np.minimum(1, t / max(a, 1e-3)) ** 1.5
    return e * np.clip((n / R - t) / rel, 0, 1) ** curve


def sine(f, dur, att=.05, rel=.5, f0=None, drop=.08):
    n = int(R * dur); t = np.arange(n) / R; ff = f if f0 is None else f + (f0 - f) * np.exp(-t / drop)
    return np.sin(2 * np.pi * np.cumsum(ff) / R) * env_ar(n, att, rel)


def glint(f, dur=2.0, att=.08, tau=.8):
    n = int(R * dur); return sine(f, dur, att=att, rel=1.2) * np.exp(-np.arange(n) / R / tau)


def piano(f, dur=3.2, vel=1.0):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n); dec = 1.9 * (220 / f) ** .35
    for h in range(1, 9):
        x += np.sin(2 * np.pi * f * h * np.sqrt(1 + .0004 * h * h) * t) * np.exp(-t / (dec / (1 + .55 * (h - 1)))) / h ** 1.15
    return lp(x, 1500 + 2600 * vel) * np.minimum(1, t / .004) * vel


def strings(f, dur, att=1.0, rel=1.5, bright=1700, vib=5.2):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n); rng = np.random.default_rng(int(f * 10))
    for c in (-9, -4, 0, 4, 9):
        lfo = 1 + .0035 * np.sin(2 * np.pi * (vib + rng.uniform(-.4, .4)) * t + rng.uniform(0, 6.28)) * np.minimum(1, t / 1.2)
        x += 2 * ((np.cumsum(f * 2 ** (c / 1200) * lfo) / R + rng.uniform(0, 1)) % 1) - 1
    return lp(x, bright, 2) * env_ar(n, att, rel, 1.5) / 5


def ramp(dur, lo, hi=1.0, power=2.2):
    return lo + (hi - lo) * (np.arange(int(R * dur)) / (R * dur)) ** power


def lufs(x):
    pre = ([1.53512485958697, -2.69169618940638, 1.19839281085285], [1, -1.69065929318241, .73248077421585]); rlb = ([1, -2, 1], [1, -1.99004745483398, .99007225036621])
    y = np.stack([lfilter(*rlb, lfilter(*pre, x[:, c])) for c in range(2)], 1); h = int(.4 * R); s = int(.1 * R)
    z = np.array([np.mean(y[i:i + h] ** 2, 0).sum() for i in range(0, len(y) - h, s)]); L = -.691 + 10 * np.log10(z + 1e-12)
    g = z[L > -70]; ab = -.691 + 10 * np.log10(g.mean()) - 10; return -.691 + 10 * np.log10(z[L > ab].mean())


def tp(x): return 20 * np.log10(np.abs(resample_poly(x, 4, 1, axis=0)).max() + 1e-12)


def finish(x, target=-19, ceiling=-3.5):
    x = x.copy(); fi = int(.25 * R); fo = int(1.0 * R)
    x[:fi] *= np.linspace(0, 1, fi)[:, None] ** 2; x[-fo:] *= np.linspace(1, 0, fo)[:, None] ** 2
    for _ in range(3):
        x *= 10 ** ((target - lufs(x)) / 20)
        if tp(x) > ceiling: x *= 10 ** ((ceiling - tp(x)) / 20)   # lower the whole piece instead of clipping
    return x


def save(p, x):
    with wave.open(str(p), "wb") as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(R); w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def base(seed=37):
    """C's strings build, the slogan answer and the ending, without any strike layer."""
    m = Mix(rt=3.4, room_lp=3500, seed=seed)
    for t, d, ns, g in [(.2, 3.0, ["E2", "B2", "E3", "G3", "B3"], .5), (1.7, 2.2, ["C3", "G3", "E4", "B3"], .65),
                        (2.9, 1.3, ["A2", "E3", "C4", "E4"], .8), (3.5, .9, ["B2", "F#3", "B3", "E4"], .95)]:
        for j, nm in enumerate(ns): m.add(strings(hz(nm), d + .7, att=min(1.1, d * .55), rel=.7, bright=1200 + 500 * g), t, .09 + .07 * g, pan=(j / (len(ns) - 1) - .5) * 1.4, wet=.5)
    m.add(sine(hz("E1"), 4, att=1.9, rel=.5) * ramp(4, .2, 1, 2), .15, .4, wet=.05)
    m.add(glint(hz("B5"), 2.2, .12, .9), 3.4, .03, wet=.9)
    for t, d, nm in [(5.12, .9, "F#4"), (5.5, 1.4, "E4")]: m.add(strings(hz(nm), d + 1, att=.25, rel=.9, bright=1500), t, .1, pan=.2, wet=.6)
    for nm in ["E3", "B3"]: m.add(strings(hz(nm), 2.2, att=.8, rel=1.2, bright=900), 4.9, .08, wet=.5)
    return m


def bloom(m, gain=1.0, lead=.25):
    """The original strings bloom that carries the strike, scaled."""
    for j, nm in enumerate(["E2", "B2", "E3", "G#3", "B3", "E4", "G#4"]):
        m.add(strings(hz(nm), 4.2, att=.35, rel=2.6, bright=2400), STRIKE - lead, .1 * gain, pan=(j / 6 - .5) * 1.5, wet=.55)


def strike():
    """The piano boom: a low E major chord, rolled, over a soft sub, with the strings' bloom under it."""
    m = base(53); bloom(m, .6)
    for i, nm in enumerate(["E1", "E2", "B2", "E3", "G#3", "B3"]):
        m.add(piano(hz(nm), dur=5.2, vel=1.0 if i < 3 else .8), STRIKE + .006 * i, .46 if i < 2 else .28, pan=(i / 5 - .5) * .7, wet=.35)
    m.add(sine(hz("E1"), 3.0, att=.015, rel=2.0, f0=hz("E1") * 1.4, drop=.05), STRIKE, .3, wet=.08)
    return m.render()


if __name__ == "__main__":
    y = finish(strike()); save(OUT, y)
    print(f"{OUT.name}  {lufs(y):.1f} LUFS  true peak {tp(y):.1f} dBTP")
