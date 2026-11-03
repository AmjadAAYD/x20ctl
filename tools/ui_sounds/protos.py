"""Four different prototypes of the X20CTL intro sound. All 7.1 s, 48 kHz, strike at 4.03 s (8 beats at ~119 BPM).
Synthesised additively; no samples, recordings or kits. python protos.py outdir"""
import sys, wave, numpy as np
from pathlib import Path
from scipy.signal import butter, sosfilt, fftconvolve, lfilter, resample_poly

R = 48000; TOTAL = 7.1; N = int(R * TOTAL); STRIKE = 4.03


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


def pad(f, dur, att=.6, rel=.8, bright=1500, voices=3, harm=6, det=4):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n)
    for v in range(voices):
        ff = f * 2 ** ((v - (voices - 1) / 2) * det / 1200); ph = np.random.default_rng(int(f) + v).uniform(0, 6.28)
        for h in range(1, harm + 1): x += np.sin(2 * np.pi * ff * h * t + ph * h) / h ** 1.6
    return lp(x, bright) * env_ar(n, att, rel) / voices


def sine(f, dur, att=.05, rel=.5, f0=None, drop=.08):
    n = int(R * dur); t = np.arange(n) / R; ff = f if f0 is None else f + (f0 - f) * np.exp(-t / drop)
    return np.sin(2 * np.pi * np.cumsum(ff) / R) * env_ar(n, att, rel)


def glint(f, dur=2.0, att=.08, tau=.8):
    n = int(R * dur); return sine(f, dur, att=att, rel=1.2) * np.exp(-np.arange(n) / R / tau)


def mallet(f, dur=2.2, decay=.7, bright=3000, h2=.3, h3=.1):
    n = int(R * dur); t = np.arange(n) / R
    x = np.sin(2 * np.pi * f * t) + h2 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t / (decay * .5)) + h3 * np.sin(2 * np.pi * 3.01 * f * t) * np.exp(-t / (decay * .3))
    return lp(x, bright) * np.minimum(1, t / .006) * np.exp(-t / decay)


def piano(f, dur=3.2, vel=1.0):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n); dec = 1.9 * (220 / f) ** .35
    for h in range(1, 9):
        x += np.sin(2 * np.pi * f * h * np.sqrt(1 + .0004 * h * h) * t) * np.exp(-t / (dec / (1 + .55 * (h - 1)))) / h ** 1.15
    return lp(x, 1500 + 2600 * vel) * np.minimum(1, t / .004) * vel


def pluck(f, dur=2.6, bright=3200):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n)
    for h in range(1, 10): x += np.sin(2 * np.pi * f * h * t) * np.exp(-t / (1.5 / (1 + .8 * (h - 1)))) / h ** 1.3
    return lp(x, bright) * np.minimum(1, t / .003)


def strings(f, dur, att=1.0, rel=1.5, bright=1700, vib=5.2):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n); rng = np.random.default_rng(int(f * 10))
    for c in (-9, -4, 0, 4, 9):
        lfo = 1 + .0035 * np.sin(2 * np.pi * (vib + rng.uniform(-.4, .4)) * t + rng.uniform(0, 6.28)) * np.minimum(1, t / 1.2)
        x += 2 * ((np.cumsum(f * 2 ** (c / 1200) * lfo) / R + rng.uniform(0, 1)) % 1) - 1
    return lp(x, bright, 2) * env_ar(n, att, rel, 1.5) / 5


def ramp(dur, lo, hi=1.0, power=2.2):
    return lo + (hi - lo) * (np.arange(int(R * dur)) / (R * dur)) ** power


def variant_A():  # glass pad in D: Dm - Bb - Gm - A swell, resolved on D add9
    m = Mix(rt=2.8, room_lp=4000, seed=11)
    m.add(pad(hz("D2"), 3.95, att=1.4, rel=.5, bright=500, harm=4) * ramp(3.95, .15), .18, .5, wet=.2)
    prog = [(.18, ["D3", "A3", "E4", "F4"], 1.3), (1.15, ["Bb2", "F3", "A3", "D4"], 1.5), (2.2, ["G2", "D3", "Bb3", "A3"], 1.6),
            (3.1, ["A2", "E3", "A3", "D4"], 1.2), (3.65, ["A2", "E3", "A3", "C#4"], .8)]
    for i, (t, ns, d) in enumerate(prog):
        for j, nm in enumerate(ns): m.add(pad(hz(nm), d + .5, att=.55, rel=.6, bright=1300 + i * 200), t, .16 + .045 * i, pan=(j / 3 - .5) * 1.2, wet=.45)
    for t, nm in zip([1.25, 1.5, 1.72, 1.93, 2.12, 2.3], ["D4", "F4", "G4", "A4", "C5", "D5"]):
        m.add(mallet(hz(nm), bright=2400), t, .07, pan=np.random.default_rng(int(t * 100)).uniform(-.5, .5), wet=.7)
    m.add(glint(hz("A5"), 1.8, .06, .7), 3.4, .035, wet=.8)
    m.add(sine(hz("D2"), 3.4, att=.012, rel=2.2, f0=hz("D2") * 1.8, drop=.07), STRIKE, .55, wet=.1)
    for j, nm in enumerate(["D3", "A3", "D4", "F#4", "A4", "E5"]): m.add(pad(hz(nm), 4, att=.014, rel=3, bright=2300, harm=5), STRIKE, .17 - .01 * j, pan=(j / 5 - .5) * 1.3, wet=.55)
    for nm in ["D3", "A3"]: m.add(pad(hz(nm), 2.6, att=.6, rel=1.6, bright=800), 4.5, .11, wet=.4)
    for t, nm in [(5.12, "E4"), (5.46, "D4")]: m.add(mallet(hz(nm), dur=2, decay=.9, bright=2200), t, .08, pan=.25, wet=.7)
    return m.render()


def variant_B():  # felt piano in A: rising arpeggio, E (V) into A major add9
    m = Mix(rt=2.2, room_lp=3800, seed=23)
    shapes = [["A2", "E3", "A3", "C4"], ["F2", "C3", "F3", "A3"], ["C3", "G3", "C4", "E4"], ["E2", "B2", "E3"]]
    t = .25; sp = (STRIKE - .25) / 15; k = 0
    for sh in shapes:
        for nm in sh:
            m.add(piano(hz(nm), vel=.35 + .5 * k / 15), t, .16 + .05 * k / 15, pan=-.3 + .6 * k / 15, wet=.4); t += sp; k += 1
    m.add(sine(hz("A1"), 3.6, att=.012, rel=2.4, f0=hz("A1") * 1.7, drop=.06), STRIKE, .5, wet=.1)
    for j, nm in enumerate(["A2", "E3", "A3", "C#4", "E4", "B4"]): m.add(piano(hz(nm), dur=4.5, vel=.9), STRIKE + .012 * j, .2, pan=(j / 5 - .5) * .9, wet=.5)
    m.add(piano(hz("E5"), dur=3, vel=.4), 3.4, .06, pan=.3, wet=.7)
    for t, nm in [(5.12, "B4"), (5.5, "A4")]: m.add(piano(hz(nm), dur=3, vel=.5), t, .12, pan=.2, wet=.6)
    m.add(piano(hz("A2"), dur=3, vel=.45), 6.0, .12, wet=.5); m.add(piano(hz("E3"), dur=3, vel=.4), 6.0, .09, pan=.1, wet=.5)
    m.add(pad(hz("A2"), 3, att=.5, rel=2, bright=600, harm=3), 4.3, .08, wet=.3)
    return m.render()


def variant_C():  # cinematic strings in E: long bowed chords, no plucks, one big bloom
    m = Mix(rt=3.4, room_lp=3500, seed=37)
    for t, d, ns, g in [(.2, 3.0, ["E2", "B2", "E3", "G3", "B3"], .5), (1.7, 2.2, ["C3", "G3", "E4", "B3"], .65),
                        (2.9, 1.3, ["A2", "E3", "C4", "E4"], .8), (3.5, .9, ["B2", "F#3", "B3", "E4"], .95)]:
        for j, nm in enumerate(ns): m.add(strings(hz(nm), d + .7, att=min(1.1, d * .55), rel=.7, bright=1200 + 500 * g), t, .09 + .07 * g, pan=(j / (len(ns) - 1) - .5) * 1.4, wet=.5)
    m.add(sine(hz("E1"), 4, att=1.9, rel=.5) * ramp(4, .2, 1, 2), .15, .4, wet=.05)
    m.add(glint(hz("B5"), 2.2, .12, .9), 3.4, .03, wet=.9)
    m.add(sine(hz("E1"), 3.6, att=.02, rel=2.4, f0=hz("E1") * 2, drop=.09), STRIKE, .55, wet=.12)
    for j, nm in enumerate(["E2", "B2", "E3", "G#3", "B3", "E4", "G#4"]): m.add(strings(hz(nm), 4.2, att=.35, rel=2.6, bright=2400), STRIKE - .25, .1, pan=(j / 6 - .5) * 1.5, wet=.55)
    for t, d, nm in [(5.12, .9, "F#4"), (5.5, 1.4, "E4")]: m.add(strings(hz(nm), d + 1, att=.25, rel=.9, bright=1500), t, .1, pan=.2, wet=.6)
    for nm in ["E3", "B3"]: m.add(strings(hz(nm), 2.2, att=.8, rel=1.2, bright=900), 4.9, .08, wet=.5)
    return m.render()


def variant_D():  # harp run in G that quickens toward the strike, warm hum underneath
    m = Mix(rt=3.0, room_lp=4200, seed=51)
    m.add(pad(hz("G2"), 4.0, att=1.8, rel=.4, bright=450, harm=3) * ramp(4, .1), .15, .45, wet=.2)
    m.add(pad(hz("D3"), 4.0, att=1.8, rel=.4, bright=500, harm=3) * ramp(4, .1), .15, .3, wet=.3)
    scale = ["G3", "A3", "B3", "D4", "E4", "G4", "A4", "B4", "D5", "E5"]; t = .3; gap = .55; i = 0
    while t < STRIKE - .06:
        idx = i % 18; idx = idx if idx < 10 else 18 - idx
        m.add(pluck(hz(scale[idx]), bright=2800), t, .05 + .08 * (t / STRIKE), pan=-.6 + 1.2 * (idx / 9), wet=.55)
        t += gap; gap = max(.13, gap * .88); i += 1
    m.add(glint(hz("D6"), 2.0, .1, .8), 3.4, .025, wet=.9)
    m.add(sine(hz("G1"), 3.6, att=.02, rel=2.4, f0=hz("G1") * 1.9, drop=.08), STRIKE, .55, wet=.1)
    for j, nm in enumerate(["G2", "D3", "G3", "B3", "D4", "A4", "D5", "G5"]): m.add(pluck(hz(nm), dur=4.5, bright=3200), STRIKE + .035 * j, .17, pan=(j / 7 - .5) * 1.6, wet=.5)
    for nm, g in [("G3", .09), ("D4", .07)]: m.add(pad(hz(nm), 2.8, att=.5, rel=1.8, bright=700, harm=3), 4.4, g, wet=.4)
    for t, nm in [(5.12, "A4"), (5.5, "G4")]: m.add(pluck(hz(nm), dur=2.4, bright=2400), t, .1, pan=.25, wet=.7)
    return m.render()


# loudness: ITU BS.1770 K-weighting with gating; true peak by 4x oversampling
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


if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for name, fn in [("A-glass-pad-D", variant_A), ("B-felt-piano-A", variant_B), ("C-strings-bloom-E", variant_C), ("D-harp-run-G", variant_D)]:
        y = finish(fn()); save(out / f"intro-proto-{name}.wav", y)
        print(name, f"{lufs(y):.1f} LUFS  TP {tp(y):.1f} dBTP")
