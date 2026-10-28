"""One sound family per X20CTL theme, all synthesised from scratch (nothing sampled, nothing measured from anyone's sounds).
Every theme has its own key, scale, instrument voice, tempo and room; the same 27 interface roles are rendered through each, plus one intro sound timed to
that theme's intro beats. Hover stays silent. Rules kept from the approved set: nothing audible above ~2.5 kHz, soft attacks on swells, no glides, no reversed
envelopes, no stacked chorus on very short sounds, low peak ceilings. python ui_themes.py outdir [theme ...]"""
import sys
import numpy as np
from pathlib import Path
from scipy.signal import butter, sosfilt
import ui_dense as D
from ui_dense import voice, sub, Mix, R, hz
from ui_dense2 import air
from ui_sounds import save, thump

RNG = np.random.default_rng(20261026)

# partial tables: (ratio, gain, decay divisor: higher = dies faster)
PARTS = {
    "pluck": [(1, 1, 1), (2, .5, 1.6), (3, .25, 2.2), (4, .12, 3)],
    "koto": [(1, 1, 1), (2, .6, 1.4), (3, .35, 2), (4, .2, 2.6), (5, .1, 3.2)],
    "marimba": [(1, 1, 1), (3.93, .35, 4), (9.9, .08, 9)],
    "bell": [(1, 1, 1), (2.76, .4, 1.5), (5.4, .15, 2.2)],
    "glass": [(1, 1, 1), (2.0, .35, 1.3), (3.01, .12, 1.8), (4.17, .05, 2.4)],
    "epiano": [(1, 1, 1), (2, .45, 1.3), (3, .1, 2)],
    "stab": [(k, 1 / k, 1 + .12 * k) for k in range(1, 9)],
    "synth": [(k, 1 / k, 1 + .08 * k) for k in range(1, 11)],
    "wood": [(1, 1, 1), (2.0, .4, 2.2), (2.76, .25, 3.5)],
    "clean": [(1, 1, 1), (2, .18, 1)],
}
DETUNE = {"stab": (-8, 0, 8), "synth": (-9, 9), "epiano": (-3, 3), "glass": (-4, 4), "bell": (-3, 3)}


def note(kind, f, dur, att, dec, bright=.8):
    """One note of the theme's voice as a stereo array. `soft` is the dense detuned stack; the rest are additive with per-partial decay."""
    if kind != "clean":  # one voice for every theme: Default's dark, dense, wide stack, never a click-sharp attack
        return voice(f, dur, max(att, .035), max(dec, .1), voices=9, bright=.8)
    n = int(R * dur); t = np.arange(n) / R; tau = dec / 2.3; out = np.zeros((n, 2))
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a)
    cents = DETUNE.get(kind, (0,))
    for side in (0, 1):
        y = np.zeros(n)
        for c in cents:
            ff = f * 2 ** ((c + (3 if side else -3) * (kind not in DETUNE)) / 1200)
            for r, g, dv in PARTS[kind]:
                if ff * r > 3600: continue
                y += g * np.sin(2 * np.pi * ff * r * t + RNG.uniform(0, 6.28)) * np.exp(-t / (tau / dv))
        out[:, side] = y / len(cents)
    env = a
    if kind == "epiano": env = env * (1 + .12 * np.sin(2 * np.pi * 4.5 * t))
    return out * env[:, None]


def noise_burst(dur, lo, hi, peak_t, width, seed=1):
    return air(dur, lo, hi, peak_t, width, seed)


def tick(dur=.03, fc=1400, seed=3):  # a very short soft noise tick (a finger or a pick touching the string)
    n = int(R * dur); t = np.arange(n) / R; z = np.random.default_rng(seed).standard_normal(n)
    return sosfilt(butter(2, fc, fs=R, output="sos"), z) * np.exp(-t / (dur / 4)) * (1 - np.exp(-t / .002))


# ----------------------------------------------------------------------------------------------------- themes
# root note, scale (semitones), voice, base attack and decay for a navigation sound, time scale, room wetness and length, low-pass, level, bass amount
def mk(**kw):
    d = dict(kind="soft", att=.06, dec=.13, wet=.35, rt=.45, fc=2300, lvl=-23, bass=.4); d.update(kw); return d


THEMES = {
    "default": None,  # Default keeps the approved sets 15 and 16
    "pulse": mk(root="A3", scale=[0, 2, 4, 6, 7, 9, 11], tm=1.1, wet=.45),
    "spotlight": mk(root="D3", scale=[0, 2, 4, 7, 9], tm=1.15, wet=.42),
    "rebel": mk(root="F#3", scale=[0, 3, 5, 6, 7, 10], tm=.85),
    "hybrid": mk(root="C4", scale=[0, 2, 4, 7, 9], tm=.9),
    "blossom": mk(root="D4", scale=[0, 1, 5, 7, 8], tm=1.05, wet=.42),
    "nexus": mk(root="G3", scale=[0, 2, 4, 5, 7, 9, 11], tm=.95),
    "tide": mk(root="B3", scale=[0, 3, 5, 7, 10], tm=1.05, wet=.48),
    "dusk": mk(root="A3", scale=[0, 2, 3, 5, 7, 9, 10], tm=1.2),
    "volt": mk(root="E3", scale=[0, 2, 4, 5, 7, 9, 10], tm=.9),
}


def deg_hz(th, d):
    sc = th["scale"]; return hz(th["root"]) * 2 ** ((sc[d % len(sc)] + 12 * (d // len(sc))) / 12)


# name: (notes (degree, at, duration multiplier, gain, pan), attack multiplier, decay multiplier, level offset dB, bass (0 none, 1 yes))
ROLES = {}
for nm, (a, b) in {"a": (1, 3), "b": (2, 4), "c": (3, 5)}.items():
    ROLES[f"nav-next-{nm}"] = ([(a, 0, 1, 1, -.2), (b, .075, 1, .9, .2)], 1, 1, 0, 0)
    ROLES[f"nav-previous-{nm}"] = ([(b, 0, 1, 1, .2), (a, .075, 1, .9, -.2)], 1, 1, 0, 0)
ROLES.update({
    "enter-1": ([(0, 0, 1, 1, -.3), (2, .09, 1, .9, 0), (4, .2, 1, .7, .3)], 2, 2, 1, 1),
    "enter-2": ([(1, 0, 1, 1, -.3), (3, .09, 1, .9, 0), (5, .2, 1, .7, .3)], 2, 2, 1, 1),
    "back": ([(4, 0, 1, 1, .2), (0, .11, 1, 1, -.2)], 1.5, 1.8, 0, 1),
    "confirm": ([(0, 0, 1, 1, -.3), (2, .08, 1, .9, -.1), (4, .17, 1, .8, .1), (5, .26, 1, .7, .3)], 2, 2, 1, 1),
    "cancel": ([(4, 0, 1, 1, .2), (3, .1, 1, .95, 0), (0, .21, 1, .9, -.2)], 2, 2, 1, 1),
    "panel-open": ([(0, 0, 1, .9, -.25), (2, .1, 1, .9, 0), (4, .22, 1, .8, .25)], 2.5, 2.4, 1, 1),
    "panel-close": ([(4, 0, 1, .8, .25), (2, .1, 1, .9, 0), (0, .21, 1, .95, -.25)], 2.5, 2.4, 1, 1),
    "notify": ([(5, 0, 1, 1, -.25), (7, .13, 1, .85, 0), (9, .26, 1, .6, .25)], 2, 2.6, 0, 1),
    "toast": ([(2, 0, 1, 1, -.25), (4, .18, 1, .85, .25), (9, .5, .6, .16, .3)], 2.5, 2.6, 0, 0),
    "key-1": ([(2, 0, .6, 1, -.1)], .5, .45, -7, 0),
    "key-2": ([(4, 0, .5, 1, .12)], .5, .4, -6, 0),
    "key-3": ([(1, 0, .7, 1, -.15)], .6, .6, -6, 0),
    "key-4": ([(3, 0, .6, 1, .15)], .7, .5, -6, 0),
    "key-delete": ([(-3, 0, .8, 1, 0)], .7, .7, -6, 0),
    "button-1": ([(0, 0, 1, 1, -.3), (2, .008, 1, .8, 0), (4, .016, 1, .5, .3)], 1.2, 1.2, -3, 1),
    "button-2": ([(1, 0, 1, 1, -.3), (3, .09, 1, 1, .3)], 1, 1.4, -3, 0),
    "button-3": ([(7, 0, .8, .7, .3), (0, .09, 1, .9, -.1)], .8, 1.1, -3, 1),
    "button-4": ([(2, 0, 1, .8, -.15), (2, .085, 1, 1, .15)], 1, 1.1, -3, 1),
    "message-sent": ([(7, 0, 1, .8, 0), (9, .09, 1, .65, 0)], 1.5, 1.8, -2, 0),
    "unlock-chime": ([(4, 0, 2, 1, -.1), (6, .04, 1.6, .6, .15), (11, .8, .5, .22, -.3), (12, .875, .5, .22, -.1), (13, .95, .5, .22, .1), (14, 1.025, .5, .22, .3)], 2, 3.5, 0, 1),
    "voice-panel": ([(7, 0, 1, 1, 0)], .5, 1.8, -1, 0),
})


def build_role(th, name):
    notes, am, dm, lv, bass = ROLES[name]; tm = th["tm"]; kind = th["kind"]
    last = max(at * tm for _, at, *_ in notes); dur_note = th["dec"] * dm * 2.6 + th["att"] * am
    m = Mix(last + dur_note + .15)
    for i, (d, at, dmult, g, pan) in enumerate(notes):
        k = "clean" if name.startswith("key") else th.get("bell") if (th.get("bell") and d >= th.get("bell_min", 7) and name in ("notify", "message-sent", "unlock-chime", "voice-panel", "toast")) else kind
        if k == "clean": m.add(note("clean", deg_hz(th, d + 2), dur_note * dmult, .012, .1 * dmult), at * tm, g, pan)
        else: m.add(note(k, deg_hz(th, d), dur_note * dmult, th["att"] * am, th["dec"] * dm * min(dmult, 1.5)), at * tm, g, pan)
        if th.get("tick") and i == 0 and name.startswith(("nav", "key", "button")): m.add(tick(.025, 1500), at * tm, .12, pan)
        if th.get("ripple") and name.startswith(("nav", "enter", "confirm")) and i == 0:
            m.add(note(kind, deg_hz(th, d + 4), dur_note * .6, th["att"] * am, th["dec"] * dm * .7), at * tm + .07, .22, -pan)
    if bass and th["bass"] > 0:
        b0 = notes[0][1] * tm
        if False and name.startswith(("enter", "button", "confirm", "panel", "back")): m.add(thump(hz(th["root"]) / 2, .14, sweep=1.4), b0, th["bass"] * .8)
        else: m.add(sub(hz(th["root"]) / 2, dur_note, max(.02, th["att"] * am), th["dec"] * dm), b0, th["bass"])
    return m.done(th["lvl"] + lv - 0, wet=th["wet"], rt=th["rt"], fc=th["fc"])


# ----------------------------------------------------------------------------------------------------- intros (timed to intros.py BEATS)
def arp(m, th, degs, at, step, g=1.0, dur=.5, am=1, dm=1, kind=None, pan0=-.3):
    for i, d in enumerate(degs): m.add(note(kind or th["kind"], deg_hz(th, d), dur, th["att"] * am, th["dec"] * dm), at + i * step, g, pan0 + i * .6 / max(1, len(degs) - 1))


def intro_hybrid(th):  # beats .4 .62 1.0 1.5 2.2 2.9: halves click together, spark drops, lights count 1-2-3-4, halves part like doors
    m = Mix(3.5)
    m.add(note("pluck", deg_hz(th, 0), .5, .004, .2), .40, .9, -.5); m.add(note("pluck", deg_hz(th, 2), .5, .004, .2), .62, 1, .5)
    m.add(note("pluck", deg_hz(th, 4), .9, .004, .3), 1.0, .9, 0); m.add(note("pluck", deg_hz(th, 7), .9, .004, .35), 1.02, .5, 0)
    for i, d in enumerate((0, 2, 4, 7)): m.add(note("pluck", deg_hz(th, d), .5, .004, .22), 1.5 + .17 * i, .8, -.3 + .2 * i)
    m.add(voice(deg_hz(th, 0), 1.3, .5, .6, voices=8), 2.2, .8, -.4); m.add(voice(deg_hz(th, 4), 1.2, .5, .6, voices=8), 2.25, .6, .4)
    m.add(note("pluck", deg_hz(th, 9), 1.0, .004, .5), 2.9, .5, 0); m.add(sub(hz(th["root"]) / 2, 1.6, .2, .8), 1.0, .5)
    return m.done(-23, wet=.3, rt=.5, fc=2400)


def intro_blossom(th):  # .4 petal falls, .6-.8 katana sweep, held beat, 1.3 petal burst, 2.1 halves bloom into logo, 3.05 garden fades in
    m = Mix(3.8)
    m.add(note("bell", deg_hz(th, 9), .6, .01, .25), .40, .35, .3)
    m.add(noise_burst(.4, 700, 2300, .2, .09, 5), .55, .16, 0)
    m.add(note("koto", deg_hz(th, 4), .8, .004, .4), .80, .9, -.1); m.add(tick(.03, 1600), .80, .15, 0)
    rng = np.random.default_rng(7)
    for i in range(9): m.add(note("bell", deg_hz(th, int(rng.integers(5, 12))), .5, .008, .22), 1.3 + .05 * i + rng.uniform(0, .03), .26, rng.uniform(-.7, .7))
    arp(m, th, (0, 2, 4), 2.1, .08, g=.9, dur=1.1, dm=2.2, kind="koto"); m.add(sub(hz(th["root"]) / 2, 1.8, .15, 1.0), 2.1, .4)
    m.add(voice(deg_hz(th, 0), 1.6, .8, .9, voices=9), 3.05 - .3, .5, -.3); m.add(voice(deg_hz(th, 2), 1.6, .8, .9, voices=9), 3.05 - .25, .4, .3)
    return m.done(-23, wet=.5, rt=.7, fc=2200)


def intro_rebel(th):  # .3 card thrown, .6 torn in two, 1.0 halves slam, 1.3 star stabs, 1.5-2.2 ransom letters, 2.2 screen tears open
    m = Mix(3.6)
    m.add(noise_burst(.3, 900, 2200, .1, .06, 3), .25, .12, 0)
    m.add(noise_burst(.25, 800, 2300, .08, .04, 9), .60, .16, -.3); m.add(noise_burst(.25, 800, 2300, .08, .04, 10), .65, .12, .3)
    m.add(thump(hz("F#2"), .18, sweep=1.4), 1.0, .9); m.add(note("stab", deg_hz(th, 0), .35, .008, .16), 1.0, .9, -.3); m.add(note("stab", deg_hz(th, 2), .35, .008, .16), 1.0, .6, .3)
    m.add(note("stab", deg_hz(th, 7), .3, .006, .1), 1.3, .85, 0)
    for i, d in enumerate((2, 4, 3, 5, 4, 6, 5)): m.add(note("stab", deg_hz(th, d + 2), .12, .004, .05), 1.55 + .09 * i, .45, -.5 + .16 * i)
    m.add(noise_burst(.7, 500, 2400, .35, .18, 12), 2.1, .2, 0)
    for d in (0, 2, 4): m.add(note("stab", deg_hz(th, d), .5, .008, .24), 2.9, .85, 0)
    m.add(thump(hz("F#2"), .2, sweep=1.4), 2.9, .8)
    return m.done(-22, wet=.15, rt=.3, fc=2000)


def intro_pulse(th):  # .2 sand on a plate, .75/1.3/1.9 three pulses, 2.5 sand sets and the spark lights, 3.05 last wave
    m = Mix(3.9)
    m.add(note("glass", deg_hz(th, 7), 1.0, .02, .4), .2, .12, .3)
    for i, (t, d, g) in enumerate(((.75, 0, 1), (1.3, 2, .9), (1.9, 4, .9))):
        m.add(sub(hz(th["root"]) / 2 * (1 + .12 * i), 1.0, .06, .5), t, .9)
        m.add(note("glass", deg_hz(th, d), 1.0, .05, .45), t + .03, g * .7, -.2 + .2 * i); m.add(note("glass", deg_hz(th, d + 4), .9, .06, .4), t + .06, g * .35, .3)
    arp(m, th, (4, 6, 9, 11), 2.5, .09, g=.5, dur=1.2, am=1.5, dm=2.5)
    m.add(voice(deg_hz(th, 0), 1.4, .6, .8, voices=10), 3.05 - .2, .7, -.3); m.add(voice(deg_hz(th, 4), 1.4, .6, .8, voices=10), 3.05 - .15, .6, .3)
    m.add(noise_burst(.9, 600, 2300, .5, .25, 21), 2.85, .1, 0)
    return m.done(-23, wet=.6, rt=.8, fc=2400)


def intro_spotlight(th):  # .5 follow spot searches, 1.25 settles, 1.6 curtain flies out, 2.1 logo lowered on wires, 2.35 star ignites, 3.1 house lights up
    m = Mix(3.9)
    m.add(voice(hz("D2"), 1.4, .6, .8, voices=8), .5, .55, 0); m.add(voice(deg_hz(th, 0) , 1.3, .7, .8, voices=8), .6, .3, -.3)
    m.add(note("bell", deg_hz(th, 4), 1.2, .01, .5), 1.25, .6, .1)
    m.add(noise_burst(.6, 500, 2000, .3, .14, 31), 1.5, .17, 0)
    for i in range(5): m.add(note("bell", deg_hz(th, 9 + (i % 3)), .4, .01, .18), 1.75 + .07 * i, .12, -.6 + .3 * i)
    arp(m, th, (4, 2, 0), 2.1, .09, g=.7, dur=.7, dm=1.6, kind="marimba")
    m.add(note("bell", deg_hz(th, 7), 1.4, .008, .6), 2.35, .65, .2); m.add(note("bell", deg_hz(th, 9), 1.2, .008, .5), 2.38, .35, -.2)
    for d in (0, 2, 4): m.add(voice(deg_hz(th, d), 1.2, .45, .7, voices=9), 3.1 - .15, .6, -.3 + d * .08)
    m.add(sub(hz("D2"), 1.5, .2, .9), 3.0, .5)
    return m.done(-23, wet=.5, rt=.7, fc=1900)


def intro_nexus(th):  # .45 power on, .95-1.75 ring lights the tiles, 2.3 tiles flip, 3.1 flips into the focused tile
    m = Mix(3.8)
    m.add(sub(hz("G2"), 1.0, .2, .5), .45, .7); m.add(voice(deg_hz(th, 0), 1.0, .3, .4, voices=8), .45, .6, -.3)
    for i in range(7): m.add(note("wood", deg_hz(th, [0, 2, 4, 5, 7, 9, 11][i]), .3, .005, .1), .95 + .12 * i, .55, -.6 + .2 * i)
    m.add(note("wood", deg_hz(th, 4), .5, .005, .2), 2.3, .8, -.3); m.add(note("wood", deg_hz(th, 7), .5, .005, .2), 2.4, .8, .3)
    for d in (0, 2, 4): m.add(note("wood", deg_hz(th, d), .9, .008, .35), 3.1, .75, -.3 + d * .1)
    m.add(sub(hz("G2"), 1.0, .05, .6), 3.1, .5)
    return m.done(-23, wet=.35, rt=.45, fc=2000)


def intro_tide(th):  # .5 wave, .93 freezes into glass, 1.15 shatters, 1.6 shards lock, 2.22 harpoon, 3.0 tide drains away
    m = Mix(4.0)
    m.add(noise_burst(.9, 300, 1800, .45, .22, 41), .0, .22, 0)
    m.add(note("glass", deg_hz(th, 7), 1.0, .006, .5), .93, .8, 0); m.add(note("glass", deg_hz(th, 9), 1.0, .006, .5), .95, .5, .3)
    rng = np.random.default_rng(11)
    for i in range(10): m.add(note("glass", deg_hz(th, int(rng.integers(6, 12))), .4, .004, .16), 1.15 + .035 * i + rng.uniform(0, .02), .3, rng.uniform(-.8, .8))
    arp(m, th, (0, 2, 4, 5), 1.6, .07, g=.8, dur=.8, dm=1.8)
    m.add(thump(hz("B2"), .2, sweep=1.3), 2.22, .7); m.add(note("glass", deg_hz(th, 4), 1.0, .006, .4), 2.22, .5, 0)
    m.add(noise_burst(1.1, 250, 1500, .3, .3, 43), 2.9, .15, 0); m.add(voice(deg_hz(th, 0), 1.4, .6, .8, voices=8), 3.0, .5, 0)
    return m.done(-23, wet=.6, rt=.8, fc=2000)


def intro_dusk(th):  # .3 sunset in fast-forward, 1.85 the spark catches the sun, 2.3 night falls, 3.2 the home rises
    m = Mix(4.2)
    for d, at, g in ((0, .3, .9), (2, .55, .7), (4, .8, .6), (5, 1.05, .5)): m.add(voice(deg_hz(th, d), 2.0, .8, .9, voices=9, bright=.5), at, g, d * .08 - .2)
    m.add(sub(hz("A2"), 2.2, .5, 1.0), .3, .5)
    m.add(note("bell", deg_hz(th, 9), 1.4, .01, .6), 1.85, .55, .2)
    m.add(note("epiano", deg_hz(th, 0) / 2, 1.2, .01, .6), 2.3, .8, -.2)
    for d in (0, 2, 4, 7): m.add(note("epiano", deg_hz(th, d), 1.4, .012, .55), 3.2 + .0 * d, .6, -.3 + d * .06)
    return m.done(-24, wet=.5, rt=.7, fc=1800)


def intro_volt(th):  # .4 and .57 lightning, 1.0-1.65 charge fills the logo, 2.15 spark flares, 3.2 the home flickers on
    m = Mix(4.0)
    for t, g in ((.4, 1), (.57, .8)):
        m.add(thump(hz("E2"), .16, sweep=1.4), t, .8 * g); m.add(noise_burst(.25, 500, 2200, .06, .05, int(t * 100)), t - .03, .22 * g, 0)
    for i in range(8): m.add(note("synth", deg_hz(th, i), .22, .006, .08), 1.0 + .085 * i, .5 + .04 * i, -.4 + .1 * i)
    for d in (0, 2, 4): m.add(note("synth", deg_hz(th, d), 1.0, .01, .45), 2.15, .8, -.3 + d * .1)
    m.add(sub(hz("E2"), 1.2, .02, .6), 2.15, .6)
    for i, t in enumerate((3.2, 3.29, 3.4, 3.46, 3.6)): m.add(note("synth", deg_hz(th, 7 + (i % 3)), .15, .004, .07), t, .35, 0)
    return m.done(-23, wet=.3, rt=.4, fc=1900)


INTROS = {"hybrid": intro_hybrid, "blossom": intro_blossom, "rebel": intro_rebel, "pulse": intro_pulse, "spotlight": intro_spotlight,
          "nexus": intro_nexus, "tide": intro_tide, "dusk": intro_dusk, "volt": intro_volt}

if __name__ == "__main__":
    out = Path(sys.argv[1]); which = sys.argv[2:] or [k for k, v in THEMES.items() if v]
    for tn in which:
        th = THEMES[tn]; od = out / tn; od.mkdir(parents=True, exist_ok=True)
        for rn in ROLES: save(od / f"{tn}-{rn}.wav", build_role(th, rn))
        save(od / f"{tn}-intro.wav", INTROS[tn](th)); print(tn, "done")
