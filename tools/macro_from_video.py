"""Turn a stick movement in a gamepad-overlay video into a macro.

For video showing an on-screen input display: a dot moving inside a circle.
It tracks the dot, buckets each frame into one of the pad's eight directions,
merges runs, and prints a sequence string you can paste into a slot.

    python tools/macro_from_video.py clip.mp4 --debug

Read this before trusting the output
------------------------------------

**It cannot be one to one, and that is the hardware.** A macro step stores a
stick as one of eight compass directions, bucketed from x/y by the pad's own
getDirectionBycoordinateXY. There is no analog position in the format at all.
How *far* the stick was pushed is not recorded and cannot be replayed. A smooth
arc comes back as a staircase of eight headings.

**A macro holds about 25 steps.** The record states its own length in one byte,
so 50 entries is the ceiling and a press with its release is two of them. A few
seconds of stick waggle will produce far more transitions than that, so this
merges runs and then keeps the longest steps, dropping the briefest. What you
get is the shape of the movement, not every flicker in it.

**Check the tracking before you trust the macro.** `--debug` writes annotated
frames showing what was found each frame. If the dot is not where the marker
is, the macro is fiction, and it will look plausible either way.
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

try:
    import cv2
    import numpy as np
except ImportError:
    print("this needs opencv: pip install opencv-python")
    raise SystemExit(1)

from x20ctl import protocol as p

# Below this fraction of the circle's radius the stick counts as centred, and
# centred is not a direction. Matches the idea of an inner deadzone.
CENTRE_FRACTION = 0.22

# Anything shorter than this is a flicker rather than an input, and spending
# one of ~25 steps on it is a bad trade.
MIN_STEP_MS = 40

TIMING_RESOLUTION = 5       # the controller's own, everything snaps to it


def snap(ms: float) -> int:
    return max(TIMING_RESOLUTION, int(round(ms / TIMING_RESOLUTION)) * TIMING_RESOLUTION)


def direction_for(dx: float, dy: float, radius: float) -> p.Direction | None:
    """Bucket an offset from the circle's centre into one of eight, or None.

    Screen y grows downwards, so it is flipped here to get compass headings
    that match what the pad means by up.
    """
    distance = (dx * dx + dy * dy) ** 0.5
    if radius <= 0 or distance < radius * CENTRE_FRACTION:
        return None
    angle = np.degrees(np.arctan2(-dy, dx)) % 360        # 0 = right, ccw
    # Eight 45-degree sectors centred on each compass point.
    index = int(((angle + 22.5) % 360) // 45)
    return [p.Direction.RIGHT, p.Direction.UP_RIGHT, p.Direction.UP,
            p.Direction.UP_LEFT, p.Direction.LEFT, p.Direction.DOWN_LEFT,
            p.Direction.DOWN, p.Direction.DOWN_RIGHT][index]


def find_dot(frame, centre, radius, lower, upper):
    """Locate the stick dot as the largest blob of the tracked colour.

    Restricted to the circle, so a same-coloured element elsewhere on screen
    cannot pull the reading. Returns its centroid, or None if nothing matched.
    """
    mask = cv2.inRange(cv2.cvtColor(frame, cv2.COLOR_BGR2HSV), lower, upper)
    ring = np.zeros(mask.shape, dtype=np.uint8)
    cv2.circle(ring, centre, int(radius * 1.15), 255, -1)
    mask = cv2.bitwise_and(mask, ring)

    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return None
    blob = max(contours, key=cv2.contourArea)
    if cv2.contourArea(blob) < 4:
        return None
    moments = cv2.moments(blob)
    if moments["m00"] == 0:
        return None
    return (moments["m10"] / moments["m00"], moments["m01"] / moments["m00"])


def sample(path, centre, radius, lower, upper, debug_dir=None):
    """Walk the video and return (direction, timestamp_ms) per frame."""
    capture = cv2.VideoCapture(path)
    if not capture.isOpened():
        raise SystemExit(f"could not open {path}")
    fps = capture.get(cv2.CAP_PROP_FPS) or 60.0

    readings, index, missed = [], 0, 0
    while True:
        ok, frame = capture.read()
        if not ok:
            break
        if centre is None:
            height, width = frame.shape[:2]
            centre = (width // 2, height // 2)
        if radius is None:
            radius = min(centre) * 0.9

        found = find_dot(frame, centre, radius, lower, upper)
        if found is None:
            missed += 1
            direction = None
        else:
            direction = direction_for(found[0] - centre[0],
                                      found[1] - centre[1], radius)

        readings.append((direction, index * 1000.0 / fps))

        if debug_dir and index % 3 == 0:
            marked = frame.copy()
            cv2.circle(marked, centre, int(radius), (80, 80, 80), 1)
            if found:
                cv2.circle(marked, (int(found[0]), int(found[1])), 6, (0, 165, 255), 2)
            cv2.putText(marked, f"{index} {direction.name if direction else 'centre'}",
                        (8, 22), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 165, 255), 1)
            cv2.imwrite(os.path.join(debug_dir, f"f{index:05d}.png"), marked)
        index += 1

    capture.release()
    return readings, fps, missed, centre, radius


def to_steps(readings, budget):
    """Merge runs of one direction into steps, then fit them into the budget.

    Dropping the briefest steps rather than truncating keeps the shape of the
    movement: the end of a gesture matters as much as its start, so cutting it
    off at 25 would be worse than losing the flickers in the middle.
    """
    runs = []
    for direction, when in readings:
        if runs and runs[-1][0] == direction:
            runs[-1][2] = when
        else:
            runs.append([direction, when, when])

    steps = [(d, end - start) for d, start, end in runs
             if d is not None and (end - start) >= MIN_STEP_MS]
    dropped = len([r for r in runs if r[0] is not None]) - len(steps)

    if len(steps) > budget:
        keep = sorted(sorted(range(len(steps)), key=lambda i: -steps[i][1])[:budget])
        dropped += len(steps) - budget
        steps = [steps[i] for i in keep]
    return steps, dropped


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("video")
    parser.add_argument("--stick", choices=("LS", "RS"), default="LS")
    parser.add_argument("--centre", type=int, nargs=2, metavar=("X", "Y"),
                        help="circle centre in pixels, defaults to frame centre")
    parser.add_argument("--radius", type=float, help="circle radius in pixels")
    parser.add_argument("--hue", type=int, nargs=2, metavar=("LO", "HI"),
                        default=(0, 179), help="hue range of the dot")
    parser.add_argument("--min-saturation", type=int, default=90)
    parser.add_argument("--min-value", type=int, default=90)
    parser.add_argument("--budget", type=int, default=p.MAX_MACRO_ENTRIES // 2,
                        help="steps to keep, default is the hardware ceiling")
    parser.add_argument("--debug", metavar="DIR", nargs="?", const="debug_frames",
                        help="write annotated frames to check the tracking")
    args = parser.parse_args()

    if args.debug:
        os.makedirs(args.debug, exist_ok=True)

    lower = np.array([args.hue[0], args.min_saturation, args.min_value])
    upper = np.array([args.hue[1], 255, 255])
    centre = tuple(args.centre) if args.centre else None

    readings, fps, missed, centre, radius = sample(
        args.video, centre, args.radius, lower, upper, args.debug)

    print(f"\n  {len(readings)} frames at {fps:.1f} fps, "
          f"circle centre {centre}, radius {radius:.0f}")
    if missed:
        share = missed * 100 / max(len(readings), 1)
        print(f"  dot not found in {missed} frames ({share:.0f}%)"
              + ("  <- tune --hue, and check --debug frames" if share > 10 else ""))

    steps, dropped = to_steps(readings, args.budget)
    if not steps:
        print("\n  no stick movement found. Wrong colour range, or wrong circle.\n")
        return 1

    sequence = ", ".join(f"{args.stick}_{d.name}:{snap(ms)}" for d, ms in steps)
    print(f"\n  {len(steps)} steps, {dropped} dropped as too brief or over budget\n")
    print(f"  {sequence}\n")

    # Prove it actually encodes before claiming it is usable.
    try:
        parsed = p.parse_sequence(sequence, 100, 0)
        payload = p.build_macro_payload(parsed, loop_interval_ms=0)
        print(f"  encodes to {len(payload)} bytes, "
              f"{len(parsed)}/{p.MAX_MACRO_ENTRIES} entries\n")
    except ValueError as exc:
        print(f"  will not encode: {exc}\n")
        return 1

    print("  Paste it into a slot, or:")
    print(f'    x20 macro M1 "{sequence}"\n')
    print("  Remember the pad stores eight directions, not stick positions:")
    print("  how far it was pushed is not in there.\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
