"""Two console-menu families, rebuilt from scratch: very short, dry, soft ticks and pops (no melodies, no long tails), each role told apart
by pitch and direction. Made of sine partials with a fast pitch settle (the 'pop') and a short exponential decay; no noise, nothing below ~200 Hz.
  steam  : small, crisp and bright, almost no room. Tab = two quick ticks rising or falling. Open = a soft pock. Back = a lower falling tick.
  ps     : rounder, warmer and airier, with a faint wide room. Tab = one soft bloop rising or falling. Open = a warm tok. Back = lower, falling.
python ui_console.py outdir"""
import sys
from pathlib import Path
import numpy as np
import ui_sounds as U
from ui_sounds import Snd, save, R, lp, hp, room


def pop(f0, f1, dur, decay, settle=.008, partials=((1, 1), (2, .25)), att=.0015, bright=3000):
    """One short tonal pop: pitch settles from f0 to f1 over `settle` seconds, then rings out for `decay`."""
    n = int(R * dur); t = np.arange(n) / R; f = f1 + (f0 - f1) * np.exp(-t / settle); ph = np.cumsum(f) / R * 2 * np.pi
    x = sum(g * np.sin(ph * k) * np.exp(-t / (decay / k ** .6)) for k, g in partials)
    a = np.minimum(1, t / att); a = a * a * (3 - 2 * a)
    return hp(lp(x, bright), 200) * a


def shimmer(f, dur, decay, att=.004, bright=5000):
    n = int(R * dur); t = np.arange(n) / R; x = sum(np.sin(2 * np.pi * f * k * t + k) * np.exp(-t / (decay / k ** .5)) / k for k in (1, 2, 3))
    a = np.minimum(1, t / att); return hp(lp(x, bright), 300) * a * a * (3 - 2 * a)


# ---------------------------------------------------------------- Steam
def S_tab(up):
    U.IR = room(.12, seed=3); a, b = (950, 1300) if up else (1300, 950); s = Snd(.2)
    s.add(pop(a * 1.25, a, .06, .012), 0, .8, wet=.08); s.add(pop(b * 1.25, b, .08, .014), .045, 1, wet=.08); return s.done(-28)
def S_open(i):
    U.IR = room(.12, seed=3); f = 700 * (1, 1.06, .95)[i]; s = Snd(.2); s.add(pop(f * 1.4, f, .12, .03, partials=((1, 1), (2, .3), (3, .08))), 0, 1, wet=.1); return s.done(-27)
def S_button():
    U.IR = room(.12, seed=3); s = Snd(.12); s.add(pop(1500, 1100, .05, .01, partials=((1, 1), (2, .15))), 0, 1, wet=.06); return s.done(-30)
def S_back():
    U.IR = room(.12, seed=3); s = Snd(.2); s.add(pop(720, 420, .12, .035, settle=.03), 0, 1, wet=.1); return s.done(-27)
def S_pick():
    U.IR = room(.2, seed=3); s = Snd(.55); s.add(pop(950, 700, .12, .03, partials=((1, 1), (2, .3))), 0, .9, wet=.1)
    s.add(shimmer(1400, .4, .09, bright=3200), .07, .55, pan=.15, wet=.2); s.add(shimmer(2100, .4, .07, bright=3200), .1, .25, pan=-.15, wet=.2); return s.done(-24)


# ---------------------------------------------------------------- PlayStation
def P_tab(up):
    U.IR = room(.28, seed=5); f0, f1 = (480, 700) if up else (700, 480); s = Snd(.35)
    s.add(pop(f0, f1, .2, .05, settle=.05, partials=((1, 1), (2, .18)), att=.006, bright=2600), 0, 1, pan=-.12 if up else .12, wet=.2); return s.done(-28)
def P_open(i):
    U.IR = room(.28, seed=5); f = 400 * (1, 1.05, .96)[i]; s = Snd(.3)
    s.add(pop(f * 1.3, f, .2, .05, settle=.02, partials=((1, 1), (2, .3), (3, .06)), att=.004, bright=2800), 0, 1, wet=.18); return s.done(-27)
def P_button():
    U.IR = room(.2, seed=5); s = Snd(.15); s.add(pop(900, 720, .08, .018, partials=((1, 1), (2, .2)), att=.003, bright=3200), 0, 1, wet=.1); return s.done(-30)
def P_back():
    U.IR = room(.28, seed=5); s = Snd(.3); s.add(pop(480, 300, .2, .055, settle=.04, partials=((1, 1), (2, .2)), att=.005, bright=2400), 0, 1, wet=.18); return s.done(-27)
def P_pick():
    U.IR = room(.4, seed=5); s = Snd(.8); s.add(pop(620, 520, .2, .05, settle=.03, partials=((1, 1), (2, .3)), att=.004, bright=2800), 0, .9, wet=.15)
    s.add(shimmer(1040, .6, .14, att=.01, bright=3600), .08, .5, pan=-.2, wet=.35); s.add(shimmer(1560, .6, .11, att=.01, bright=3600), .12, .32, pan=.2, wet=.35); return s.done(-24)


FAMILIES = {"steam": (S_tab, S_open, S_button, S_back, S_pick), "ps": (P_tab, P_open, P_button, P_back, P_pick)}

if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    for fam, (tab, opening, button, back, pick) in FAMILIES.items():
        files = {"tab-next": tab(True), "tab-previous": tab(False), "open-1": opening(0), "open-2": opening(1), "open-3": opening(2), "button": button(), "back": back(), "pick": pick()}
        for name, y in files.items():
            save(out / f"ui7-{fam}-{name}.wav", y); f = np.abs(np.fft.rfft(y.mean(1))); fr = np.fft.rfftfreq(len(y), 1 / R)
            print(f"{fam:5s} {name:13s} {len(y) / R * 1000:4.0f} ms  peak {20 * np.log10(np.abs(y).max()):.1f} dBFS  centroid {(f * fr).sum() / f.sum():.0f} Hz")
