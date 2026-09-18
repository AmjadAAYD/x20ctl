"""Record a live performance off the pad and print it as a macro.

    python tools/record_macro.py                  20 seconds, or press Start
    python tools/record_macro.py --seconds 8

Entirely read-only. XInputGetState asks the driver for the current state and
changes nothing, and this never opens a Bluetooth connection at all.

Why record rather than write a macro by hand
--------------------------------------------

Because hand-written timings are wrong in ways that are invisible until you
watch the car. Three designed attempts at a Rocket League wave dash produced a
backflip, a barrel roll and a tumble; the first one built from a recording was
a single parameter away from correct. The airtime alone was out by a factor of
fifteen.

The pad is on XInput over USB or the 2.4 GHz receiver, so this reads it while
you play. It does not need the configuration link, which is Bluetooth.

Read this before trusting the output
------------------------------------

**The stick is bucketed to eight headings.** That is all a macro step can
store. The `played` column is the angle you actually held and `stored` is what
survives, so you can see the loss rather than discover it later. Mechanics on
cardinal directions come through intact; anything needing a true diagonal does
not.

**About 23 steps fit.** A press and its release are two entries against a
ceiling of MAX_MACRO_ENTRIES, so record one mechanic, not a rally.

**Sub-10 ms holds get flagged.** Rocket League samples at 120 Hz, one tick every
8.33 ms, so an input held for less than a tick can fall between two samples and
never be seen. The 5 ms grid is for placing an edge in time, not for how long
to hold it.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from x20ctl import protocol as p
from x20ctl.input import (LEFT_STICK_DEADZONE, XInputReader, _quantise,
                          stick_direction)

# A press and its release are two entries each.
MAX_ITEMS = p.MAX_MACRO_ENTRIES // 2

# One physics tick in Rocket League, and the floor for a hold that must register.
TICK_MS = 1000 / 120


def true_angle(x: int, y: int) -> float | None:
    """The angle actually held, before the eight-way bucket rounds it off."""
    if math.hypot(x, y) < LEFT_STICK_DEADZONE:
        return None
    return math.degrees(math.atan2(x, y)) % 360


def label(inputs) -> str:
    return "+".join(i.name for i in inputs)


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Record live pad input and print it as a macro string.")
    ap.add_argument("--seconds", type=float, default=20.0,
                    help="stop after this long if Start is never pressed")
    ap.add_argument("--countdown", type=int, default=3,
                    help="seconds before recording starts, to tab into the game")
    args = ap.parse_args()

    reader = XInputReader()
    if not reader.available:
        print("no XInput dll found; this tool is Windows only")
        return 1
    if reader.poll() is None:
        print("no controller on XInput. Wake the pad, and check it is on USB or "
              "the 2.4 GHz receiver rather than Bluetooth only.")
        return 1

    for n in range(args.countdown, 0, -1):
        print(f"  {n}...", flush=True)
        time.sleep(1)
    print("  GO  (press Start to finish)\n", flush=True)

    def sample():
        s = reader.poll()
        if s is None:
            return None, None, None
        return (tuple(sorted(s.macro_inputs, key=lambda i: i.name)),
                s.left_direction, true_angle(*s.left_stick))

    timeline = []
    last, _, last_angle = sample()
    changed_at = time.perf_counter()
    deadline = changed_at + args.seconds

    while time.perf_counter() < deadline:
        raw = reader.poll()
        if raw is not None and p.Key.START in raw.buttons:
            break
        current, _, angle = sample()
        if current is None:
            continue
        if current != last:
            now = time.perf_counter()
            timeline.append((last, _quantise((now - changed_at) * 1000), last_angle))
            last, changed_at = current, now
        last_angle = angle
        time.sleep(0.0005)

    timeline.append((last, _quantise((time.perf_counter() - changed_at) * 1000),
                     last_angle))

    while timeline and not timeline[0][0]:
        timeline.pop(0)
    while timeline and not timeline[-1][0]:
        timeline.pop()
    if not timeline:
        print("nothing recorded")
        return 1

    print(f"{len(timeline)} state changes\n")
    print(f"  {'at':>6} {'held':>6}  {'played':>9} {'stored':>8}  inputs")
    at = 0
    for inputs, ms, angle in timeline:
        played = f"{angle:7.1f}deg" if angle is not None else "       --"
        stored = next((f"{(i.direction - 1) * 45:6d}deg" for i in inputs
                       if isinstance(i, p.StickInput)), "      --")
        warn = "  <tick" if 0 < ms < TICK_MS else ""
        print(f"  {at:>6} {ms:>6}  {played:>9} {stored:>8}  "
              f"{label(inputs) or '(nothing held)'}{warn}")
        at += ms

    # An empty state is not a key group, so it becomes the gap on the step
    # before it. Everything else carries an explicit /0 so holds survive.
    items, i = [], 0
    while i < len(timeline):
        inputs, ms, _ = timeline[i]
        if not inputs:
            i += 1
            continue
        gap = 0
        if i + 1 < len(timeline) and not timeline[i + 1][0]:
            gap = timeline[i + 1][1]
            i += 1
        items.append(f"{label(inputs)}:{ms}/{gap}")
        i += 1

    keys = ", ".join(items)
    print(f"\n{len(items)} steps -> {len(items) * 2} entries, "
          f"cap {p.MAX_MACRO_ENTRIES} so {MAX_ITEMS} steps")
    if len(items) > MAX_ITEMS:
        print(f"  TOO LONG by {len(items) - MAX_ITEMS} steps. Printed anyway; "
              "trim the least interesting ones before writing.")

    print("\nmacro string\n")
    print(keys)

    try:
        steps = p.parse_sequence(keys, 100, 0)
    except ValueError as exc:
        print(f"\ndoes not parse: {exc}")
        return 1
    print(f"\nparses to {len(steps)} entries, "
          f"{sum(s.duration_ms for s in steps)} ms total")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
