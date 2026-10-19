"""The X20CTL opening sound, synthesised here (owner requests 17 Oct 2026: "a good sound effect", then "cleaner,
smoother, high quality, more life, an intro feel - not VMMMM, DOK DOK, BMMMM, and not happy, 90s retro or high pitched").

Everything is made in this file: soft FM electric-piano tones, plucked strings (Karplus-Strong), a pad whose filter
breathes, and a stereo room. No samples, recordings or third-party kits, nothing copied from another product. The key
is D minor, kept in the middle register (no lead above D5), so it reads as a cinematic opening, not a jingle.

The story, in time with IntroView and IntroBladeFormation:
  0.18-0.54  the three LED eyes: D4, F4, A4 on the electric piano, left to right
  1.45-2.45  the white outline: eight soft climbing plucks placed where the smooth line passes, a last as it closes
  2.95-4.03  the blades sliding in: one repeated pluck that speeds up and brightens, and the lock chord swelling in
             backwards out of its own reverb
  4.03       the lock: the chord (D minor with a ninth) on the piano, a soft low body under it, the pad opening up
  4.23       the second heartbeat: a low A, as a note, not a thump
  5.12-5.34  the slogan: two soft falling notes, E4 then D4
  6.85       the landing: a quiet open fifth, D3 and A3, on the piano

    python tools/build_intro_sound.py [output.wav]
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, fftconvolve, lfilter, sosfilt

RATE = 48000
TOTAL = 7.1
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "desktop-dotnet/X20Ctl.Product/Brand/intro.wav"
rng = np.random.default_rng(20261017)

N = int(RATE * TOTAL)
dry = np.zeros((N, 2))
send = np.zeros((N, 2))  # what goes to the room


def hz(name):
    """A note name ("D4", "F#3") as a frequency."""
    steps = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    semis = steps[name[0]] + (1 if "#" in name else -1 if name[1] == "b" else 0)
    octave = int(name[-1])
    return 440.0 * 2 ** ((semis + 12 * (octave + 1) - 69) / 12)


def span(seconds):
    return np.arange(int(seconds * RATE)) / RATE


def lowpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, fs=RATE, output="sos"), x)


def highpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="high", fs=RATE, output="sos"), x)


def place(signal, at, gain=1.0, pan=0.0, wet=.3):
    """Mix a mono signal in at a time: equal-power pan (-1 left, +1 right), and a share of it into the room."""
    start = int(at * RATE)
    if start >= N:
        return
    end = min(N, start + len(signal))
    angle = (pan + 1) * np.pi / 4
    part = signal[: end - start] * gain
    for channel, weight in ((0, np.cos(angle)), (1, np.sin(angle))):
        dry[start:end, channel] += part * weight
        send[start:end, channel] += part * weight * wet


def piano(note, length=2.4, velocity=.6):
    """A soft FM electric piano: the modulation (the bark) blooms with velocity and fades fast, leaving a round tone that
    rings out; low notes ring longer. A touch of octave body under it, a lowpass that keeps it warm."""
    f = hz(note) if isinstance(note, str) else note
    s = span(length)
    index = (.25 + 1.5 * velocity) * np.exp(-s / .18) + .06
    tone = np.sin(2 * np.pi * f * s + index * np.sin(2 * np.pi * f * s))
    tone += .18 * np.sin(2 * np.pi * 2 * f * s) * np.exp(-s / .12)
    # a quick fall after the strike, then a long quiet ring, the way a struck tine dies away; low notes ring longer
    ring = 1.1 * (220 / f) ** .4
    envelope = np.minimum(s / .004, 1) * (.6 * np.exp(-s / .28) + .4 * np.exp(-s / ring)) * np.clip((length - s) / .08, 0, 1)
    return lowpass(tone * envelope, 2600 + 1400 * velocity) * velocity


def pluck(note, length=1.4, brightness=.5, sustain=.996):
    """A plucked string (Karplus-Strong): a short burst through a tuned feedback loop that loses a little of its top on
    every pass, so it starts woody and mellows. Brightness shapes the burst; the loop does the rest."""
    f = hz(note) if isinstance(note, str) else note
    delay = max(2, int(round(RATE / f - .5)))
    burst = lowpass(rng.standard_normal(delay + 1), 600 + 5000 * brightness, 1)
    burst -= burst.mean()
    x = np.zeros(int(length * RATE))
    x[: delay + 1] = burst * np.hanning(delay + 1)
    a = np.zeros(delay + 2)
    a[0], a[delay], a[delay + 1] = 1, -sustain / 2, -sustain / 2
    y = lfilter([1], a, x)
    s = span(length)
    y *= np.minimum(s / .002, 1) * np.clip((length - s) / .1, 0, 1)
    return lowpass(y / (np.max(np.abs(y)) + 1e-9), 1800 + 3000 * brightness)


def pad(notes, length, opening, attack, release):
    """A pad built from harmonics, each one let through by a filter that opens and closes over time (opening(s) gives the
    cutoff in Hz), so the sound breathes without its pitch ever moving. A slow, slight swell in volume keeps it alive."""
    s = span(length)
    cutoff = opening(s)
    out = np.zeros_like(s)
    for note in notes:
        f = hz(note)
        for k in range(1, 40):
            if k * f > 5000:
                break
            gate = 1 / (1 + np.exp(-(cutoff - k * f) / 120))
            out += gate * np.sin(2 * np.pi * k * f * s + k * 1.7) / k ** 1.3
    envelope = np.clip(s / attack, 0, 1) ** 2 * np.clip((length - s) / release, 0, 1) ** 1.5
    breathe = 1 + .08 * np.sin(2 * np.pi * .35 * s)
    return highpass(out * envelope * breathe / len(notes), 180)


def room(seconds=2.6, size=.32):
    """A stereo room: a few early reflections, then a smooth tail that darkens as it fades, different in each ear."""
    s = span(seconds)
    ir = np.zeros((len(s), 2))
    for channel in range(2):
        tail = rng.standard_normal(len(s)) * np.exp(-6.9 * s / seconds)
        dark = lowpass(tail, 2400, 2) * np.exp(-s / 1.2) + lowpass(tail, 900, 2) * (1 - np.exp(-s / 1.2))
        dark = highpass(dark, 160)
        dark[: int(.018 * RATE)] = 0  # pre-delay
        for at, gain in ((.011, .5), (.019, .35), (.027, .28), (.041, .2)):
            ir[int((at + .004 * channel) * RATE), channel] += gain
        ir[:, channel] += dark * size
    return ir / np.sqrt(np.sum(ir ** 2, axis=0, keepdims=True))


# 1. the eyes open: D4, F4, A4, left to right, soft
for at, note, pan in ((.18, "D4", -.35), (.36, "F4", 0), (.54, "A4", .35)):
    place(piano(note, 2.2, .42), at, .22, pan, wet=.5)

# 2. the outline (owner 17 Oct: a line drawn on a beat felt forced, so the music follows the drawing instead): the white
#    line is one smooth stroke, 1.45 s to 2.45 s, slow to start, quick through the middle, easing as it closes
#    (the Linux app's IntroView.cs, keep in step). A pluck sounds as the line passes each eighth of the controller, heard
#    from where it is, climbing as it goes, so the notes gather where the line is quick and part where it slows, like a
#    hand drawn across a koto. The last rings out as the line closes.
OUTLINE_FROM, OUTLINE_FOR = 1.45, 1.0


def drawn_at(fraction):
    """When the smooth stroke has drawn this much of the outline (inverting x*x*(3-2x) by bisection)."""
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if mid * mid * (3 - 2 * mid) < fraction else (lo, mid)
    return OUTLINE_FROM + OUTLINE_FOR * lo


climb = ["D3", "F3", "G3", "A3", "C4", "D4", "F4", "G4"]
for k, note in enumerate(climb):
    where = (k + .5) / 8  # 0 top centre, .25 left grip, .5 bottom, .75 right grip
    place(pluck(note, 1.8, .22, .998), drawn_at(where), .085, -.65 * np.sin(2 * np.pi * where), wet=.6)
place(pluck("A4", 2.2, .25, .998), OUTLINE_FROM + OUTLINE_FOR, .07, 0, wet=.7)
place(piano("D4", 2.2, .25), OUTLINE_FROM + OUTLINE_FOR + .01, .08, 0, wet=.7)
# under it, a pad on D minor that opens as the controller is drawn and settles as it leans in
place(pad(["A3", "D4", "F4"], 2.4, lambda s: 250 + 900 * np.sin(np.pi * np.clip(s / 2.4, 0, 1)) ** 2, .7, .7), .7, .045, 0, wet=.7)

# 3. the blades slide in: one soft A, plucked a little faster each time and set back in the room; it stays under the
#    swell (owner 17 Oct: the louder, octave-jumping version was too loud and unappealing)
at, gap, i = 2.98, .2, 0
while at < 3.9:
    progress = (at - 2.98) / 1.0
    place(pluck("A3", .6, .15 + .2 * progress), at, .05 + .06 * progress, (-.2 if i % 2 else .2) * (1 - progress), wet=.6)
    at += gap
    gap = max(.085, gap * .9)
    i += 1

# the lock chord, D minor with a ninth, voiced low to middle
chord = ["D3", "A3", "E4", "F4"]


def strike(velocity):
    """The chord, rolled bottom to top by 12 ms a note, the way a hand plays it."""
    out = np.zeros(int(3.1 * RATE))
    for k, n in enumerate(chord):
        tone = piano(n, 3.0, velocity * (1 - .08 * k))
        offset = int(k * .012 * RATE)
        out[offset:offset + len(tone)] += tone
    return out / len(chord) * 2


# ... swelling in backwards out of its own reverb, so the room breathes in before the lock
mono_room = room(2.2, .4)[:, 0]
swell = fftconvolve(strike(.5), mono_room)[: int(1.1 * RATE)][::-1]
swell *= np.linspace(0, 1, len(swell)) ** 2.2
place(swell / np.max(np.abs(swell)), 4.03 - len(swell) / RATE, .22, 0, wet=.2)

# 4. the lock: the chord, a soft low body under it (felt more than heard), and the pad opening wide
place(strike(.75), 4.03, .55, 0, wet=.4)
body = span(1.2)
place(np.sin(2 * np.pi * hz("D2") * body) * np.minimum(body / .006, 1) * np.exp(-body / .35), 4.03, .22, 0, wet=.05)
place(pad(["A3", "D4", "E4"], 3.07, lambda s: 300 + 1500 * np.clip(s / .9, 0, 1) ** .7 * np.exp(-np.clip(s - .9, 0, None) / 1.6), .25, 1.4), 4.03, .06, 0, wet=.7)

# the second heartbeat, as a note: a low A an octave under, with the mark's swell
place(piano("A2", 2.0, .45), 4.23, .32, -.1, wet=.4)
place(piano("A3", 2.0, .3), 4.235, .14, .15, wet=.5)

# 5. the slogan: two soft falling notes
place(piano("E4", 1.8, .35), 5.12, .2, .2, wet=.55)
place(piano("D4", 1.8, .32), 5.34, .2, -.15, wet=.55)

# 6. the landing: a quiet open fifth on the piano, mostly room (owner 17 Oct: the plucked D was funny, not cool)
place(piano("D3", 1.0, .22), 6.85, .1, -.1, wet=.75)
place(piano("A3", 1.0, .2), 6.87, .08, .1, wet=.75)

# the room, then the master: clean lows, a little warmth, gentle edges, headroom
ir = room()
wet = np.stack([fftconvolve(send[:, c], ir[:, c])[:N] for c in range(2)], axis=1)
mix = dry + .55 * wet
mix = np.stack([highpass(mix[:, c], 32) for c in range(2)], axis=1)
mix /= np.max(np.abs(mix))
mix = np.tanh(1.15 * mix) / np.tanh(1.15)  # soft saturation: glues the layers, rounds the peaks
mix = np.stack([lowpass(mix[:, c], 9000, 2) for c in range(2)], axis=1)
mix *= .7 / np.max(np.abs(mix))
s = np.arange(N) / RATE
mix *= (np.minimum(s / .01, 1) * np.clip((TOTAL - s) / .25, 0, 1))[:, None]

OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(RATE)
    w.writeframes((np.clip(mix, -1, 1) * 32767).astype("<i2").tobytes())


def rms(a, b):
    part = mix[int(a * RATE):int(b * RATE)]
    return float(np.sqrt(np.mean(part ** 2)))


print(f"{OUT} {TOTAL}s peak {np.max(np.abs(mix)):.2f}")
for name, a, b in (("eyes", .15, .75), ("outline", .75, 2.6), ("slide", 2.95, 4.03), ("lock", 4.03, 5.0), ("slogan", 5.1, 6.0), ("land", 6.85, 7.05)):
    print(f"  {name:8} rms {rms(a, b):.3f}")
