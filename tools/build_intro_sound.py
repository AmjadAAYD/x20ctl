"""Synthesises X20CTL's intro sound (owner direction 10 Oct 2026: an aura sound like Steam's opening, "fwaaah ... ba pa!").

Original and fully procedural: nothing is sampled or copied. Timed to IntroView's storyboard:
  0.30 / 0.52 / 0.74 s  the three LED eyes blink on       -> glassy pings, left / centre / right
  0.95 - 1.85 s         the outline draws itself         -> a warm pad swells up under a rising "fwaaah" of breath
  1.90 / 2.10 s         the heartbeat, lub-dub           -> two sub thumps
  2.55 - 3.45 s         the line re-routes into the X    -> a riser
  3.05 - 3.52 s         the X lands and fills            -> a breath drawn in, then a deep soft "thoom" resolving into one warm low tone
Writes desktop-dotnet/X20Ctl.Product/Brand/intro.wav (48 kHz, 16-bit stereo).
"""
from pathlib import Path
import numpy as np

SR = 48000
DUR = 6.3
N = int(SR * DUR)
t = np.arange(N) / SR
rng = np.random.default_rng(20261010)
L = np.zeros(N); R = np.zeros(N)


def env(start, attack, hold, release, shape=2.0):
    """0..1 envelope: linear-ish attack, hold, exponential-ish release."""
    e = np.zeros(N)
    a0, a1 = int(start * SR), int((start + attack) * SR)
    h1 = int((start + attack + hold) * SR)
    r1 = min(N, int((start + attack + hold + release) * SR))
    if a1 > a0:
        e[a0:a1] = np.linspace(0, 1, a1 - a0) ** shape
    e[a1:h1] = 1
    if r1 > h1:
        e[h1:r1] = np.linspace(1, 0, r1 - h1) ** 2.2
    return e


def lowpass(x, cutoff):
    """One-pole low-pass with a cutoff that may change every sample (array or scalar)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    y = np.zeros_like(x); s = 0.0
    k = 1 - np.exp(-2 * np.pi * cutoff / SR)
    for i in range(len(x)):
        s += k[i] * (x[i] - s); y[i] = s
    return y


def add(sig, pan=0.0, gain=1.0):
    global L, R
    L += sig * gain * np.sqrt((1 - pan) / 2)
    R += sig * gain * np.sqrt((1 + pan) / 2)


# 1. LED pings: two bell partials with a fast decay
for at, pan in ((.30, -.6), (.52, 0.0), (.74, .6)):
    e = np.where(t >= at, np.exp(-(t - at) / .09), 0)
    ping = (np.sin(2 * np.pi * 1760 * t) * .6 + np.sin(2 * np.pi * 2637 * t) * .35 + np.sin(2 * np.pi * 4186 * t) * .12) * e
    add(ping, pan, .22)

# 2. the swell: a detuned D-major-add9 pad opening its filter, under a rising breath of noise ("fwaaah")
chord = [73.4, 146.8, 220.0, 293.7, 370.0, 440.0, 659.3]
pad = np.zeros(N)
for f in chord:
    for detune in (-.35, .0, .4):
        ph = 2 * np.pi * (f + detune) * t + rng.uniform(0, 6.28)
        pad += (np.sin(ph) + .5 * np.sin(2 * ph) + .25 * np.sin(3 * ph)) / len(chord)
swell = env(.9, .95, .1, 1.1, 1.6)
cut = 250 + 5200 * np.clip((t - .9) / 1.0, 0, 1) ** 1.5 * np.where(t < 2.4, 1, np.exp(-(t - 2.4) * 2))
pad = lowpass(pad * swell, cut)
add(pad, -.15, .55); add(np.roll(pad, 220), .15, .55)
breath = lowpass(rng.normal(0, 1, N), 600 + 7000 * np.clip((t - .95) / .9, 0, 1) ** 2) * env(.95, .85, .05, .5, 2.4)
add(breath - lowpass(breath, 300), 0, .28)

# 3. heartbeat: sub thumps, the second softer
for at, g in ((1.9, 1.0), (2.1, .7)):
    tt = np.clip(t - at, 0, None)
    pitch = 45 + 85 * np.exp(-tt / .04)
    ph = 2 * np.pi * np.cumsum(pitch) / SR
    add(np.sin(ph) * np.where(t >= at, np.exp(-tt / .16), 0), 0, .9 * g)

# 4. the riser while the outline re-routes into the X
rise = env(2.5, .95, 0, .05, 3.0)
riser = rng.normal(0, 1, N); riser = riser - lowpass(riser, 900 + 6000 * np.clip((t - 2.5) / .95, 0, 1))
add(riser * rise, -.3, .16); add(np.roll(riser, 480) * rise, .3, .16)
tone = np.sin(2 * np.pi * np.cumsum(330 + 900 * np.clip((t - 2.5) / .95, 0, 1) ** 2) / SR) * rise
add(tone, 0, .07)

# 5. the X lands (owner direction 10 Oct 2026: no "tadaa" chord). A breath is drawn in, then a deep, soft "thoom"
#    resolves into one warm low tone that slowly closes, with a little air drifting over the aura.
inhale = rng.normal(0, 1, N); inhale = lowpass(inhale, 400 + 3800 * np.clip((t - 3.05) / .45, 0, 1) ** 2)
add(inhale * env(3.05, .45, 0, .08, 2.8), -.2, .22); add(np.roll(inhale, 300) * env(3.05, .45, 0, .08, 2.8), .2, .22)
tt = np.clip(t - 3.52, 0, None)
thoom = np.sin(2 * np.pi * np.cumsum(41 + 34 * np.exp(-tt / .09)) / SR) * np.where(t >= 3.52, np.exp(-tt / .7) * (1 - np.exp(-tt / .012)), 0)
add(thoom, 0, 1.0)
tone = np.zeros(N)
for f, g in ((73.4, 1.0), (146.8, .55), (220.0, .22)):  # D2, D3 and a soft fifth: one warm note, not a chord
    for detune in (-.25, .25):
        tone += g * (np.sin(2 * np.pi * (f + detune) * t) + .3 * np.sin(4 * np.pi * (f + detune) * t))
tone_env = np.where(t >= 3.52, (1 - np.exp(-tt / .18)) * np.exp(-tt / 1.6), 0)
tone = lowpass(tone * tone_env, 1600 * np.exp(-tt / 1.2) + 220)
add(tone, -.1, .42); add(np.roll(tone, 260), .1, .42)
air = rng.normal(0, 1, N); air = air - lowpass(air, 5000)
add(air * np.where(t >= 3.6, np.exp(-(t - 3.6) / 1.1) * (1 - np.exp(-(t - 3.6).clip(0) / .3)), 0), -.6, .05)
add(np.roll(air, 1100) * np.where(t >= 3.6, np.exp(-(t - 3.6) / 1.1) * (1 - np.exp(-(t - 3.6).clip(0) / .3)), 0), .6, .05)
# room: a short stereo reverb from decaying noise
def reverb(x, seconds=1.8, seed=1):
    r = np.random.default_rng(seed); n = int(seconds * SR)
    ir = r.normal(0, 1, n) * np.exp(-np.arange(n) / SR / (seconds / 5)); ir /= np.sqrt((ir ** 2).sum())
    size = 1 << int(np.ceil(np.log2(len(x) + n)))
    return np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:len(x)]

L = L + .35 * reverb(L, seed=1); R = R + .35 * reverb(R, seed=2)
fade = np.clip((DUR - t) / .6, 0, 1); L *= fade; R *= fade
peak = max(np.abs(L).max(), np.abs(R).max()); L, R = L / peak * .89, R / peak * .89
pcm = (np.stack([L, R], 1) * 32767).astype("<i2")

out = Path(__file__).resolve().parents[1] / "desktop-dotnet" / "X20Ctl.Product" / "Brand" / "intro.wav"
import wave
with wave.open(str(out), "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
print("wrote", out, f"{len(pcm) / SR:.2f}s")
