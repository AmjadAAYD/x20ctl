"""The X20CTL interface sounds ("velvet"), synthesised here. Owner requests 24-25 Oct 2026: console-like, smooth, not loud, easy on
the ears, "not BLUP BADUM", lower and softer for tab switch, hover over a game and picking a controller.

Everything is made in this file from sine partials: no samples, recordings, sound kits or noise sources. Each note is a pair of
voices tuned 5 cents apart (a slow, soft shimmer), faded in with a smooth-step so nothing clicks, and nothing is left below about
250 Hz, so there is no thump. One small shared room gives every sound the same space. The notes come from E major, the key of the
opening film's sound.

  tab.wav   the tab switch ("Settle"): two soft notes stepping down (B4 then E4). The app plays it for tabs, clicks, buttons and hovers
            (hovers at half volume)
  pick.wav  choosing a controller: a rising E3 B3 E4 G#4 roll over a very soft B2 bed (about 2 s)

    python tools/build_ui_sounds.py [output folder]     default: desktop-dotnet/X20Ctl.Product/Brand/ui
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, fftconvolve, sosfilt

R = 48000
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "desktop-dotnet/X20Ctl.Product/Brand/ui"


def hz(name):
    s = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[name[0]]; i = 1
    if name[1] == "#": s += 1; i = 2
    elif name[1] == "b": s -= 1; i = 2
    return 440 * 2 ** ((s + 12 * (int(name[i:]) + 1) - 69) / 12)


def lp(x, c, o=2): return sosfilt(butter(o, c, fs=R, output="sos"), x)
def hp(x, c, o=2): return sosfilt(butter(o, c, "high", fs=R, output="sos"), x)


def room(rt=.9, seed=9):
    """The shared room: a decaying, low-passed tail, heard only as space around the notes."""
    rng = np.random.default_rng(seed); n = int(R * rt * 1.3); t = np.arange(n) / R; out = []
    for _ in range(2):
        ir = hp(lp(rng.standard_normal(n), 5000) * np.exp(-6.9 * t / rt), 200)
        out.append(np.concatenate([np.zeros(int(.008 * R)), ir / np.sqrt((ir ** 2).sum())]))
    return out


IR = room()


class Snd:
    def __init__(s, dur): s.n = int(R * dur); s.d = np.zeros((s.n, 2)); s.w = np.zeros((s.n, 2))

    def add(s, x, at=0, g=1, pan=0, wet=.25):
        a = int(at * R); x = x[:s.n - a]; left = np.cos((pan + 1) * np.pi / 4); right = np.sin((pan + 1) * np.pi / 4)
        s.d[a:a + len(x), 0] += x * g * left; s.d[a:a + len(x), 1] += x * g * right
        s.w[a:a + len(x), 0] += x * g * left * wet; s.w[a:a + len(x), 1] += x * g * right * wet

    def done(s, peak_db):
        y = s.d + np.stack([fftconvolve(s.w[:, c], IR[c])[:s.n] for c in range(2)], 1)
        fi = int(.004 * R); fo = int(.04 * R); y[:fi] *= np.linspace(0, 1, fi)[:, None]; y[-fo:] *= np.linspace(1, 0, fo)[:, None]
        return y * 10 ** (peak_db / 20) / np.abs(y).max()


def tone(f, dur, decay, att=.012, h=(1, .22, .06), bright=3600, det=2.5):
    n = int(R * dur); t = np.arange(n) / R; x = np.zeros(n)
    for c in (-det, det):
        for k, g in enumerate(h, 1): x += g * np.sin(2 * np.pi * f * 2 ** (c / 1200) * k * t + c) * np.exp(-t / (decay / k ** .6))
    a = np.minimum(1, t / att)
    return hp(lp(x, bright), 250) / 2 * a * a * (3 - 2 * a)


def swell(f, dur, att=.09, bright=2400, h=(1, .15)):
    n = int(R * dur); t = np.arange(n) / R
    return tone(f, dur, 99, att=att, h=h, bright=bright) * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.4


def soft(name, dur, decay, att, bright=1500): return tone(hz(name), dur, decay, att=att, h=(1, .06), bright=bright)
def mild(name, dur, decay, att, bright, h2=.1): return tone(hz(name), dur, decay, att=att, h=(1, h2), bright=bright)


def tab():
    s = Snd(.9); s.add(soft("B4", .5, .15, .04, 1600), 0, .5, pan=.2, wet=.5); s.add(soft("E4", .7, .24, .045, 1500), .12, .65, pan=-.2, wet=.55); return s.done(-22)


def pick():
    s = Snd(2.0)
    for i, nm in enumerate(["E3", "B3", "E4", "G#4"]): s.add(soft(nm, 1.6, .55, .05, 1500), .1 * i, .5 - .05 * i, pan=-.3 + .2 * i, wet=.6)
    s.add(swell(hz("B2"), 1.6, att=.2, bright=900, h=(1, .05)), 0, .4, wet=.5); return s.done(-16)


SOUNDS = {"tab": tab, "pick": pick}


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for name, make in SOUNDS.items():
        y = make()
        with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
            w.setnchannels(2); w.setsampwidth(2); w.setframerate(R); w.writeframes((np.clip(y, -1, 1) * 32767).astype("<i2").tobytes())
        print(f"{name}.wav  {len(y) / R * 1000:.0f} ms  peak {20 * np.log10(np.abs(y).max()):.1f} dBFS")
