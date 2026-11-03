"""X20CTL interface sound prototypes, two families, seven events each. All in E (the intro's key), short, low-mid register,
soft attacks, a small shared room, no noise sources. Synthesised additively; no samples. python ui_sounds.py outdir"""
import sys, wave
import numpy as np
from pathlib import Path
from scipy.signal import fftconvolve
from protos import R, hz, lp, hp, sine, mallet, pluck, tp


def room(rt=.55, seed=5):
    rng = np.random.default_rng(seed); n = int(R * rt * 1.3); t = np.arange(n) / R
    return [np.concatenate([np.zeros(int(.008 * R)), (lambda ir: ir / np.sqrt((ir ** 2).sum()))(hp(lp(rng.standard_normal(n), 5000) * np.exp(-6.9 * t / rt), 200))]) for _ in range(2)]


IR = room()


class Snd:
    def __init__(s, dur): s.n = int(R * dur); s.d = np.zeros((s.n, 2)); s.w = np.zeros((s.n, 2))

    def add(s, x, at=0, g=1, pan=0, wet=.25):
        a = int(at * R); x = x[:s.n - a]; L = np.cos((pan + 1) * np.pi / 4); Rr = np.sin((pan + 1) * np.pi / 4)
        s.d[a:a + len(x), 0] += x * g * L; s.d[a:a + len(x), 1] += x * g * Rr; s.w[a:a + len(x), 0] += x * g * L * wet; s.w[a:a + len(x), 1] += x * g * Rr * wet

    def done(s, peak_db):
        y = s.d + np.stack([fftconvolve(s.w[:, c], IR[c])[:s.n] for c in range(2)], 1)
        fi = int(.004 * R); fo = int(.04 * R); y[:fi] *= np.linspace(0, 1, fi)[:, None]; y[-fo:] *= np.linspace(1, 0, fo)[:, None]
        return y * 10 ** (peak_db / 20) / np.abs(y).max()


def bell(f, dur, decay, att=.003, h=(1, .35, .12), bright=4000):
    n = int(R * dur); t = np.arange(n) / R; x = sum(g * np.sin(2 * np.pi * f * k * t) * np.exp(-t / (decay / k ** .7)) for k, g in zip((1, 2, 3), h))
    return lp(x, bright) * np.minimum(1, t / att)


def thump(f, dur=.12, drop=.02, sweep=1.6):
    return sine(f, dur, att=.004, rel=dur * .8, f0=f * sweep, drop=drop) * np.exp(-np.arange(int(R * dur)) / R / (dur * .35))


def glide(f0, f1, dur, att=.01):
    n = int(R * dur); t = np.arange(n) / R; ff = f0 * (f1 / f0) ** (t / dur)
    return np.sin(2 * np.pi * np.cumsum(ff) / R) * np.minimum(1, t / att) * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.2


# ---------------------------------------------------------------- family A: soft glass
def A_tab():
    s = Snd(.4); s.add(glide(420, 760, .16), 0, .5, wet=.2); s.add(bell(hz("B4"), .3, .09), .07, .55, pan=.2, wet=.35); s.add(thump(hz("E2"), .1), 0, .35, wet=.05); return s.done(-9)
def A_click():
    s = Snd(.25); s.add(bell(hz("B4"), .2, .05, h=(1, .25, .06)), 0, .7, wet=.2); s.add(thump(hz("E2"), .08), 0, .45, wet=.05); return s.done(-8)
def A_hover_game():
    s = Snd(.18); s.add(bell(hz("E5"), .15, .035, h=(1, .15, .03)), 0, 1, wet=.15); return s.done(-17)
def A_hover_controller():
    s = Snd(.4); s.add(bell(hz("B3"), .35, .11, att=.012, h=(1, .4, .1)), 0, .8, pan=-.1, wet=.35); s.add(bell(hz("E4"), .3, .09, att=.012), .02, .55, pan=.1, wet=.35); s.add(thump(hz("E2"), .12, sweep=1.3), 0, .25, wet=.05); return s.done(-14)
def A_hover_tool():
    s = Snd(.2); s.add(bell(hz("G#5"), .16, .04, h=(1, .2, .05)), 0, .8, pan=.15, wet=.2); s.add(bell(hz("B4"), .14, .04), .018, .45, pan=-.15, wet=.2); return s.done(-16)
def A_pick_controller():
    s = Snd(1.2)
    for i, nm in enumerate(["E4", "B4", "E5", "G#5"]): s.add(bell(hz(nm), 1.0, .28, att=.006), .055 * i, .55 - .08 * i, pan=-.3 + .2 * i, wet=.45)
    s.add(thump(hz("E2"), .22, sweep=1.5), 0, .6, wet=.08); s.add(bell(hz("E3"), 1.0, .4, att=.02), 0, .35, wet=.4); return s.done(-7)
def A_button():
    s = Snd(.3); s.add(thump(hz("E2"), .13, sweep=1.7), 0, .75, wet=.06); s.add(bell(hz("B4"), .22, .07, att=.002), .012, .55, wet=.2); s.add(bell(hz("E5"), .15, .04), .03, .25, wet=.2); return s.done(-8)


# ---------------------------------------------------------------- family B: wooden mallet
def B_tab():
    s = Snd(.4); s.add(mallet(hz("G#4"), .25, .08, bright=2800), 0, .6, pan=-.2, wet=.25); s.add(mallet(hz("B4"), .3, .1, bright=2800), .07, .65, pan=.2, wet=.3); return s.done(-9)
def B_click():
    s = Snd(.2); s.add(mallet(hz("E4"), .15, .035, bright=2500, h2=.6), 0, .8, wet=.15); s.add(thump(hz("E2"), .07, sweep=1.3), 0, .35, wet=.05); return s.done(-8)
def B_hover_game():
    s = Snd(.15); s.add(pluck(hz("B4"), .12, 2200) * np.exp(-np.arange(int(R * .12)) / R / .03), 0, 1, wet=.12); return s.done(-17)
def B_hover_controller():
    s = Snd(.35); s.add(pluck(hz("E3"), .3, 1500) * np.exp(-np.arange(int(R * .3)) / R / .09), 0, .9, pan=-.1, wet=.3); s.add(pluck(hz("B3"), .3, 1600) * np.exp(-np.arange(int(R * .3)) / R / .08), .025, .6, pan=.1, wet=.3); return s.done(-14)
def B_hover_tool():
    s = Snd(.18); s.add(mallet(hz("G#5"), .14, .03, bright=3000, h2=.2), 0, .8, pan=.15, wet=.15); return s.done(-16)
def B_pick_controller():
    s = Snd(1.1)
    for i, nm in enumerate(["E3", "B3", "E4", "G#4"]): s.add(pluck(hz(nm), .9, 2400) * np.exp(-np.arange(int(R * .9)) / R / .35), .05 * i, .6 - .06 * i, pan=-.3 + .2 * i, wet=.4)
    s.add(thump(hz("E2"), .24, sweep=1.5), 0, .65, wet=.08); s.add(mallet(hz("B4"), .8, .25, bright=2600), .17, .3, wet=.4); return s.done(-7)
def B_button():
    s = Snd(.28); s.add(thump(hz("E2"), .12, sweep=1.5), 0, .8, wet=.05); s.add(mallet(hz("B4"), .2, .05, bright=2600, h2=.5), .01, .6, wet=.15); return s.done(-8)


EVENTS = [("tab-switch", "tab"), ("click", "click"), ("hover-game", "hover_game"), ("hover-controller", "hover_controller"),
          ("hover-tool", "hover_tool"), ("pick-controller", "pick_controller"), ("button", "button")]


def save(p, x):
    with wave.open(str(p), "wb") as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(R); w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for fam, label in [("A", "glass"), ("B", "wood")]:
        for name, fn in EVENTS:
            y = globals()[f"{fam}_{fn}"](); p = out / f"ui-{fam}-{label}-{name}.wav"; save(p, y)
            print(p.name, f"{len(y) / R * 1000:.0f} ms", f"peak {20 * np.log10(np.abs(y).max()):.1f} dBFS", f"TP {tp(y):.1f}")
