"""Builds the shipped X20CTL interface sounds for every theme into desktop-dotnet/X20Ctl.Product/Brand/ui/<theme>/<role>.wav.

Everything is synthesised in this folder from sine partials and shaped noise: no samples, recordings, sound kits or outside material.
Each theme has its own key, scale, tempo and room but the same voice (dark, dense, wide, soft), so every theme feels like Default.
`default` is the owner-approved set (ui_dense, ui_dense2, ui_dense3); its WAVs are committed as generated and are not rebuilt here.
Run order matters (one shared random stream): themes in THEMES order, every role, then the intro, exactly as ui_themes.py does,
so running this again rebuilds the themes byte for byte.

    python tools/ui_sound_themes/build.py [output folder] [--intros]     (intros: also writes <theme>/intro.wav; held back until the app has themes)
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import resample_poly

import ui_themes as T

SHIP = ["nav-next-a", "nav-next-b", "nav-next-c", "enter-1", "enter-2", "back", "button-1", "button-2", "button-3", "button-4", "key-1", "key-2", "key-3", "key-4", "key-delete"]
args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = Path(args[0]) if args else Path(__file__).resolve().parents[2] / "desktop-dotnet/X20Ctl.Product/Brand/ui"


def write(path, y):  # 24 kHz 16-bit stereo: nothing in these sounds is above ~2.5 kHz
    y = resample_poly(y, 1, 2, axis=0)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(24000); w.writeframes((np.clip(y, -1, 1) * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    for tn, th in T.THEMES.items():
        if not th: continue
        for rn in T.ROLES:
            y = T.build_role(th, rn)
            if rn in SHIP: write(OUT / tn / f"{rn}.wav", y)
        y = T.INTROS[tn](th)
        if "--intros" in sys.argv: write(OUT / tn / "intro.wav", y)
        print(tn, "done")
