"""Regenerates the X20CTL interface sounds (the 15 files in desktop-dotnet/X20Ctl.Product/Brand/ui) into a folder you name.

Everything is synthesised here from sine partials and shaped noise: no samples, recordings, sound kits or outside material. The voice is
a dense stack of slightly detuned sines with a slow shimmer, a soft attack, a small shared room, a low-pass near 2 kHz and a quiet synth
bass (ui_dense.py); navigation and enter sounds are in ui_dense2.py, keys and buttons in ui_dense3.py. The shimmer phases are drawn from
one random stream, so a rebuild sounds the same but is not byte-identical to the committed WAVs, which are the owner-approved renders.

    python tools/ui_sounds/build.py <output folder>
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import resample_poly

import ui_dense
import ui_dense2
import ui_dense3


GAIN_DB = 9  # the renders peak between -31 and -22 dBFS, too quiet to hear in the app; this lifts them to between -22 and -13


def write(path, y):  # 24 kHz 16-bit stereo: nothing in these sounds is above ~2.5 kHz
    y = np.clip(resample_poly(y, 1, 2, axis=0) * 10 ** (GAIN_DB / 20), -.95, .95)
    with wave.open(str(path), "wb") as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(24000); w.writeframes((np.clip(y, -1, 1) * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    roles = {f"nav-next-{k}": (lambda k=k: ui_dense2.nav_var(k, True)) for k in "abc"}
    roles |= {"enter-1": lambda: ui_dense2.enter_var(0), "enter-2": lambda: ui_dense2.enter_var(1), "back": ui_dense.back}
    for name, fn in ui_dense3.SET.items(): roles[name] = fn
    for name, fn in roles.items(): write(out / f"{name}.wav", fn())
    print(len(roles), "sounds ->", out)
