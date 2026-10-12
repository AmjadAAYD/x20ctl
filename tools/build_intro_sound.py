"""Synthesises X20CTL's intro sound (owner direction 10 Oct 2026: an aura sound like Steam's opening, "fwaaah ... ba pa!").

Original and fully procedural: nothing is sampled or copied. Timed to IntroView's storyboard:
  0.30 / 0.52 / 0.74 s  the three LED eyes blink on       -> soft rounded notes, D5 / F#5 / A5, left / centre / right
  0.95 - 1.85 s         the outline draws itself         -> a soft warm pad swells up (no breath noise)
  1.90 / 2.10 s         the heartbeat, lub-dub           -> a small, muffled lub-dub
  2.55 - 3.45 s         the line re-routes into the X    -> a low warm chord swelling in volume, no pitch glide
  3.05 - 3.52 s         the X lands and fills            -> the X's own heartbeat, "dug dug" at 3.55 / 3.75 s
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


# 1. LED blinks (owner direction 12 Oct 2026: the old glassy "ting ting ting" was harsh): three soft, rounded
#    notes rising D5, F#5, A5 inside the pad's D major. Near-pure sines with a gentle 8 ms attack, a slight pitch
#    settle like a felt mallet, and the top rolled off, so they land like drops rather than bells.
for at, pan, f in ((.30, -.45, 587.3), (.52, 0.0, 740.0), (.74, .45, 880.0)):
    tt = np.clip(t - at, 0, None)
    pitch = f * (1 + .012 * np.exp(-tt / .03))
    ph = 2 * np.pi * np.cumsum(pitch) / SR
    e = np.where(t >= at, (1 - np.exp(-tt / .008)) * np.exp(-tt / .22), 0)
    note = (np.sin(ph) + .18 * np.sin(2 * ph) + .04 * np.sin(3 * ph)) * e
    add(lowpass(note, 1800), pan, .16)

# 2. the swell (owner direction 12 Oct 2026: the noisy "fwoosh" under it was harsh): no breath noise any more, just a
#    warm D-major pad, near-pure sines, rising gently while the outline draws, its filter opening only part way
chord = [146.8, 220.0, 293.7, 370.0, 440.0]
pad = np.zeros(N)
for f in chord:
    for detune in (-.2, .2):
        ph = 2 * np.pi * (f + detune) * t + rng.uniform(0, 6.28)
        pad += (np.sin(ph) + .12 * np.sin(2 * ph)) / len(chord)
swell = env(.9, .75, 0, .7, 2.0)   # shorter (owner 12 Oct 2026): gone soon after the heartbeat
cut = 300 + 1300 * np.clip((t - .9) / 1.0, 0, 1) ** 1.5 * np.where(t < 2.4, 1, np.exp(-(t - 2.4) * 2))
pad = lowpass(lowpass(pad * swell, cut), 2000)
add(pad, -.2, .34); add(np.roll(pad, 220), .2, .34)

# 3. heartbeat, lub-dub with the glow at 1.9 and 2.1 s (owner 12 Oct 2026: the old 45 Hz thumps were too low for laptop
#    speakers to play at all). A small, muffled thud: a body near 90 Hz with its octave so small speakers carry it,
#    rolled off above 400 Hz, the second beat softer
for at, g in ((1.9, 1.0), (2.1, .65)):
    tt = np.clip(t - at, 0, None)
    ph = 2 * np.pi * np.cumsum(78 + 40 * np.exp(-tt / .025)) / SR
    beat = (np.sin(ph) + .45 * np.sin(2 * ph)) * np.where(t >= at, np.exp(-tt / .075) * (1 - np.exp(-tt / .004)), 0)
    add(lowpass(lowpass(beat, 400), 400), 0, .36 * g)

# 4. while the line re-routes into the X (owner 12 Oct 2026: the rising sine glide hurt the owner's ears). No pitch moves at all:
#    a low, warm D chord (D3, A3, D4) only grows in volume, its tone opening a little, and flows straight into the X
rise = env(2.5, 1.02, 0, .25, 2.2)
warm = np.zeros(N)
for f, g in ((146.8, 1.0), (220.0, .6), (293.7, .35)):
    for detune in (-.15, .15):
        warm += g * np.sin(2 * np.pi * (f + detune) * t + rng.uniform(0, 6.28))
warm = lowpass(lowpass(warm * rise, 250 + 450 * np.clip((t - 2.5) / 1.0, 0, 1)), 900)
add(warm, -.2, .09); add(np.roll(warm, 300), .2, .09)

# 5. the X lands (owner 12 Oct 2026: the chord "braam" at the end was not wanted). Instead the X has a heartbeat of its
#    own, "dug dug" at 3.55 and 3.75 s, the same moments the mark swells on screen: like the controller's heartbeat but a
#    little fuller, each beat a round body near 70 Hz with its octave and a muffled knock on top so it reads as "d",
#    the second beat softer. Nothing above about 700 Hz.
for at, g in ((3.55, 1.0), (3.75, .7)):
    tt = np.clip(t - at, 0, None)
    ph = 2 * np.pi * np.cumsum(66 + 46 * np.exp(-tt / .03)) / SR
    body = (np.sin(ph) + .5 * np.sin(2 * ph)) * np.where(t >= at, np.exp(-tt / .11) * (1 - np.exp(-tt / .004)), 0)
    knock = np.sin(2 * np.pi * 180 * t) * np.where(t >= at, np.exp(-tt / .018) * (1 - np.exp(-tt / .002)), 0)
    add(lowpass(lowpass(body + .35 * knock, 700), 700), 0, .55 * g)

# room: a short stereo reverb from decaying noise
def reverb(x, seconds=1.8, seed=1):
    r = np.random.default_rng(seed); n = int(seconds * SR)
    ir = r.normal(0, 1, n) * np.exp(-np.arange(n) / SR / (seconds / 5)); ir /= np.sqrt((ir ** 2).sum())
    size = 1 << int(np.ceil(np.log2(len(x) + n)))
    return np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:len(x)]

L = L + .35 * reverb(L, seed=1); R = R + .35 * reverb(R, seed=2)
fade = np.clip((DUR - t) / .6, 0, 1); L *= fade; R *= fade
peak = max(np.abs(L).max(), np.abs(R).max()); # the loudest moment sits well under full scale, so the outline keeps the level the owner approved (12 Oct 2026)
L, R = L / peak * .44, R / peak * .44
pcm = (np.stack([L, R], 1) * 32767).astype("<i2")

out = Path(__file__).resolve().parents[1] / "desktop-dotnet" / "X20Ctl.Product" / "Brand" / "intro.wav"
import wave
with wave.open(str(out), "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
print("wrote", out, f"{len(pcm) / SR:.2f}s")
