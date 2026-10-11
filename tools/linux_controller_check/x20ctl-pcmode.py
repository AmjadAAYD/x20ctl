#!/usr/bin/env python3
"""X20CTL PC mode for Linux - remaps, turbo, stick and trigger curves and macros for any controller, applied on the PC.

The same idea as PC mode in the Windows app: the real controller is grabbed (so games stop seeing it), X20CTL applies
your settings, and games see one virtual Xbox 360 controller instead. Nothing is ever written to the controller.

Runs on any Linux distro with Python 3.8+, SteamOS included. No packages needed: it reads /dev/input/event* and
creates the virtual pad through /dev/uinput directly.

    python3 x20ctl-pcmode.py list                      controllers it can see
    python3 x20ctl-pcmode.py show                      your settings
    python3 x20ctl-pcmode.py set remap A B             A now acts as B   (names below)
    python3 x20ctl-pcmode.py set turbo X 10            X repeats 10 times a second while held (2 to 30, 0 = off)
    python3 x20ctl-pcmode.py set curve LeftStick slow --inner 10 --outer 95
                                                       presets: default quick slow smooth fine; channels LeftStick
                                                       RightStick LeftTrigger RightTrigger
    python3 x20ctl-pcmode.py set stick LeftStick --inner 4.5 --outer 97.5 --square on --ratio 80
    python3 x20ctl-pcmode.py set stick RightStick --anti 8 --max 95     (anti-deadzone floor, output ceiling; --raw on = untouched 1:1)
                                                       fine dead zones (0.1% steps), square gate, up/down speed
    python3 x20ctl-pcmode.py set macro RB "A 50 30; X+B 40 0"
                                                       RB plays: A held 50 ms, 30 ms pause, then X and B 40 ms
    python3 x20ctl-pcmode.py clear remap A | turbo X | curve LeftStick | macro RB
    python3 x20ctl-pcmode.py reset                     everything back to normal
    python3 x20ctl-pcmode.py learn                     teach it a controller whose buttons come out wrong
    python3 x20ctl-pcmode.py run                       start PC mode (Ctrl+C stops it and gives the pad back)
                                                       hold View + Menu for a second to switch your settings off/on
    python3 x20ctl-pcmode.py export [FILE.x20pc]       your settings as a code to paste in a chat (or a file)
    python3 x20ctl-pcmode.py import CODE|FILE          use settings someone shared (the same codes and files as Windows)
    python3 x20ctl-pcmode.py mouse                     use the controller as a mouse (hold View + Y to switch it on/off)
    python3 x20ctl-pcmode.py mouse set speed 1500      speed curve deadzone scroll precision pointer (left|right),
                                                       or a button: mouse set A LeftClick   (mouse show lists them)

Buttons: A B X Y LB RB LT RT L3 R3 DPAD_UP DPAD_DOWN DPAD_LEFT DPAD_RIGHT SELECT START HOME

Settings live in ~/.config/x20ctl/pc-mode.json, the same format as the Windows app's file, so a copied file works.
"run" picks them up again whenever the file changes.

Permissions: it needs to read /dev/input/event* (the "input" group, or sudo) and write /dev/uinput (usually root, or a
udev rule; "run" prints the exact rule if it's missing). It never changes permissions itself.

Use with care in online games: turbo and macros can get you banned. You are responsible for the rules of the games
you play.
"""
from __future__ import annotations

import base64, glob, json, math, os, select, struct, subprocess, sys, time, uuid, zlib
try: import fcntl  # Linux only; the engine and settings code below also load elsewhere, for tests
except ImportError: fcntl = None
from pathlib import Path

VERSION = "1.0.0-local"
CONFIG = Path(os.environ.get("XDG_CONFIG_HOME") or Path.home() / ".config") / "x20ctl"
PROFILE_FILE, LAYOUT_FILE, STATE_FILE = CONFIG / "pc-mode.json", CONFIG / "pc-mode-layouts.json", CONFIG / "pc-mode-linux.json"
MOUSE_FILE = CONFIG / "mouse.json"
PROFILE_KEY = "default"
VIRTUAL_NAME = "X20CTL virtual pad (Xbox 360)"

# ---------------------------------------------------------------------------------------------------------------
# The engine: a line-for-line port of X20Ctl.Zone/PcMode/PadProcessor.cs, tested against the same cases
# ---------------------------------------------------------------------------------------------------------------

UP, DOWN, LEFT, RIGHT, START, BACK, L3, R3, LB, RB, GUIDE, A, B, X, Y = 0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x200, 0x400, 0x1000, 0x2000, 0x4000, 0x8000
MASK = {"DPAD_UP": UP, "DPAD_DOWN": DOWN, "DPAD_LEFT": LEFT, "DPAD_RIGHT": RIGHT, "START": START, "SELECT": BACK, "L3": L3, "R3": R3,
        "LB": LB, "RB": RB, "A": A, "B": B, "X": X, "Y": Y, "HOME": GUIDE}
TRIGGER_THRESHOLD = 30
MIN_TURBO, MAX_TURBO = 2, 30
CHANNELS = ("LeftStick", "RightStick", "LeftTrigger", "RightTrigger")
PRESETS = {"default": ((85, 85), (170, 170)), "quick": ((35, 35), (136, 204)), "slow": ((35, 35), (219, 145)), "smooth": ((142, 142), (220, 157)), "fine": ((67, 67), (72, 106))}
HEADINGS = ["Neutral", "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft"]  # StickHeading, by number


def known(name): return name in ("LT", "RT") or name in MASK


def new_profile(): return {"Remap": {}, "Curves": {}, "Turbo": {}, "Macros": {}, "Sticks": {}}


STICKS = ("LeftStick", "RightStick")


def tuning_default(s): return s.get("Inner", 0) == 0 and s.get("Outer", 100) == 100 and not s.get("SquareGate", False) and s.get("AxisRatio", 100) == 100 \
    and s.get("AntiDeadzone", 0) == 0 and s.get("MaxOutput", 100) == 100 and not s.get("Raw", False)


def new_tuning(): return {"Inner": 0.0, "Outer": 100.0, "SquareGate": False, "AxisRatio": 100, "AntiDeadzone": 0.0, "MaxOutput": 100.0, "Raw": False}


def is_default(c): return c["Inner"] == 0 and c["Outer"] == 100 and c["X1"] == 85 and c["Y1"] == 85 and c["X2"] == 170 and c["Y2"] == 170


def is_neutral(p): return not p["Remap"] and not p["Turbo"] and not p["Macros"] and all(is_default(c) for c in p["Curves"].values()) and all(tuning_default(s) for s in p.get("Sticks", {}).values())


def validate(p):
    for k in ("Remap", "Curves", "Turbo", "Macros", "Sticks"):
        p.setdefault(k, {})
        if not isinstance(p[k], dict): raise ValueError(f"{k} must be an object.")
    for s, t in p["Remap"].items():
        if not known(s) or not known(t): raise ValueError("Unknown button in a remap.")
    for b, hz in p["Turbo"].items():
        if not known(b) or not isinstance(hz, int) or not MIN_TURBO <= hz <= MAX_TURBO: raise ValueError("Turbo needs a known button and 2 to 30 presses a second.")
    for name, c in p["Curves"].items():
        if name not in CHANNELS or c["Inner"] < 0 or c["Outer"] > 100 or c["Inner"] >= c["Outer"] or c["X1"] > c["X2"] or any(not 0 <= c[k] <= 255 for k in ("X1", "Y1", "X2", "Y2")):
            raise ValueError("Invalid curve.")
    for name, s in p["Sticks"].items():
        for k, v in new_tuning().items(): s.setdefault(k, v)
        if name not in STICKS or not 0 <= s["Inner"] <= 40 or not 60 <= s["Outer"] <= 100 or s["Inner"] >= s["Outer"] or not 50 <= s["AxisRatio"] <= 150 \
                or not 0 <= s["AntiDeadzone"] <= 40 or not 60 <= s["MaxOutput"] <= 100 or s["AntiDeadzone"] >= s["MaxOutput"]:
            raise ValueError("Invalid stick settings.")
    for trigger, steps in p["Macros"].items():
        if not known(trigger): raise ValueError("Unknown macro trigger.")
        if len(steps) > 64 or any(any(not known(b) for b in s["Buttons"]) or not 0 <= s["HoldMs"] <= 10000 or not 0 <= s["PauseMs"] <= 10000 for s in steps):
            raise ValueError("Invalid macro.")
    return p


def _clamp(v, lo, hi): return lo if v < lo else hi if v > hi else v


def _bezier(x, x1, y1, x2, y2):
    lo, hi = 0.0, 1.0
    for _ in range(28):
        t = (lo + hi) / 2; u = 1 - t
        cx = 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t
        if cx < x: lo = t
        else: hi = t
    tt = (lo + hi) / 2; uu = 1 - tt
    return _clamp(3 * uu * uu * tt * y1 + 3 * uu * tt * tt * y2 + tt * tt * tt, 0.0, 1.0)


def response(c, s, pushed):
    """Output (0-1) for how far a stick or trigger is pushed (0-1): dead zones (a stick's fine ones first), then the curve,
    then a stick's anti-deadzone floor and max output ceiling. Raw passes the push through."""
    if s and s.get("Raw"): return _clamp(pushed, 0.0, 1.0)
    inner = (s["Inner"] if s else c["Inner"] if c else 0) / 100.0
    outer = (s["Outer"] if s else c["Outer"] if c else 100) / 100.0
    scaled = _clamp((pushed - inner) / (outer - inner), 0.0, 1.0)
    y = scaled if c is None else _bezier(scaled, c["X1"] / 255.0, c["Y1"] / 255.0, c["X2"] / 255.0, c["Y2"] / 255.0)
    if not s or y <= 0: return y
    floor, ceiling = s.get("AntiDeadzone", 0) / 100.0, s.get("MaxOutput", 100) / 100.0
    return floor + (ceiling - floor) * y


def _shape(m, c): return response(c, None, m)


def _short(v): return int(_clamp(round(v * 32767), -32768, 32767))


def stick(x, y, c, s=None):
    """Dead zones then the curve, on how far the stick is pushed (not per axis), so diagonals keep their direction;
    then the square gate (a push to the round edge at 45 degrees gives full on both axes) and the up/down ratio."""
    if c is None and s is None or s and s.get("Raw"): return x, y
    nx, ny = x / 32768.0, y / 32768.0
    m = min(1.0, math.sqrt(nx * nx + ny * ny))
    if m <= 0: return 0, 0
    shaped = response(c, s, m); ux, uy = nx / m, ny / m
    if s and s["SquareGate"]:
        length = math.sqrt(nx * nx + ny * ny); edge = max(abs(nx), abs(ny)) / length
        ux, uy = nx / length / edge, ny / length / edge
    ratio = (s["AxisRatio"] if s else 100) / 100.0
    return _short(_clamp(ux * shaped, -1.0, 1.0)), _short(_clamp(uy * shaped * ratio, -1.0, 1.0))


def trigger(value, c): return value if c is None else int(_clamp(round(_shape(value / 255.0, c) * 255), 0, 255))


def heading(h):
    d = 0.7071
    x, y = {1: (0.0, 1.0), 2: (d, d), 3: (1.0, 0.0), 4: (d, -d), 5: (0.0, -1.0), 6: (-d, -d), 7: (-1.0, 0.0), 8: (-d, d)}.get(h, (0.0, 0.0))
    return int(round(x * 32767)), int(round(y * 32767))


class PadProcessor:
    """Turns the real pad's state (buttons mask, lt, rt, lx, ly, rx, ry; Xbox layout, Y up positive) into what the game
    should see. Keeps the turbo clock and the playing macro."""

    def __init__(self, profile):
        self.p = validate(profile)
        self.turbo_since, self.previous = {}, set()
        self.playing, self.playing_since = None, 0

    def _curve(self, ch):
        c = self.p["Curves"].get(ch)
        return None if c is None or is_default(c) else c

    def _tuning(self, name):
        s = self.p["Sticks"].get(name)
        return None if s is None or tuning_default(s) else s

    def _step_at(self, elapsed):
        at = 0
        for step in self.playing:
            if elapsed < at + step["HoldMs"]: return step, True
            at += step["HoldMs"]
            if elapsed < at + step["PauseMs"]: return step, False
            at += step["PauseMs"]
        return None, False

    def process(self, state, now):
        buttons, lt_in, rt_in, lx, ly, rx, ry = state
        down = {n: 255 for n, m in MASK.items() if buttons & m}
        if lt_in > TRIGGER_THRESHOLD: down["LT"] = lt_in
        if rt_in > TRIGGER_THRESHOLD: down["RT"] = rt_in
        for t, steps in self.p["Macros"].items():
            if t in down and t not in self.previous and self.playing is None and steps:
                self.playing, self.playing_since = steps, now
        raw = set(down)
        for t in self.p["Macros"]: down.pop(t, None)
        target = {}
        for source, value in down.items():
            to = self.p["Remap"].get(source, source)
            target[to] = max(target.get(to, 0), value)
        # a light trigger pull (below the "pressed" threshold) still reaches a trigger target, so feathering survives
        for name, value in (("LT", lt_in), ("RT", rt_in)):
            if value == 0 or value > TRIGGER_THRESHOLD or name in self.p["Macros"]: continue
            to = self.p["Remap"].get(name, name)
            if to in ("LT", "RT"): target[to] = max(target.get(to, 0), value)
        for b, hz in self.p["Turbo"].items():
            if b not in target: self.turbo_since.pop(b, None); continue
            since = self.turbo_since.setdefault(b, now)
            if int((now - since) / (500.0 / hz)) & 1: del target[b]
        lx, ly = stick(lx, ly, self._curve("LeftStick"), self._tuning("LeftStick"))
        rx, ry = stick(rx, ry, self._curve("RightStick"), self._tuning("RightStick"))
        if self.playing is not None:
            step, holding = self._step_at(now - self.playing_since)
            if step is None: self.playing = None
            elif holding:
                for b in step["Buttons"]: target[b] = 255
                if step.get("Left", 0): lx, ly = heading(step["Left"])
                if step.get("Right", 0): rx, ry = heading(step["Right"])
        self.previous = raw
        out = 0
        for n, m in MASK.items():
            if n in target: out |= m
        return (out, trigger(target.get("LT", 0), self._curve("LeftTrigger")), trigger(target.get("RT", 0), self._curve("RightTrigger")), lx, ly, rx, ry)

# ---------------------------------------------------------------------------------------------------------------
# Settings files (the Windows app's pc-mode.json format: {"version": 1, "profiles": {"default": {...}}})
# ---------------------------------------------------------------------------------------------------------------


def _read_json(path, default):
    try: return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError: return default


def _write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    json.loads(tmp.read_text(encoding="utf-8"))
    if path.exists(): path.with_name(path.name + ".bak").write_bytes(path.read_bytes())
    os.replace(tmp, path)


def load_profile(path=PROFILE_FILE):
    data = _read_json(path, {"version": 1, "profiles": {}})
    if data.get("version") != 1: raise ValueError("Unsupported PC mode file.")
    return validate(data.get("profiles", {}).get(PROFILE_KEY) or new_profile())


def save_profile(p, path=PROFILE_FILE):
    validate(p)
    data = _read_json(path, {"version": 1, "profiles": {}})
    if data.get("version") != 1: raise ValueError("The PC mode file couldn't be read, so it won't be overwritten.")
    profiles = data.setdefault("profiles", {})
    if is_neutral(p): profiles.pop(PROFILE_KEY, None)
    else: profiles[PROFILE_KEY] = p
    _write_json(path, data)


def parse_macro(text):
    steps = []
    for part in [s.strip() for s in text.replace("\n", ";").split(";") if s.strip()]:
        bits = part.split()
        names = [n for n in bits[0].upper().split("+") if n]
        if not names or any(not known(n) for n in names): raise ValueError(f'Couldn\'t read "{part}". Use buttons joined with +, then hold and pause in ms, like  A 50 30')
        hold, pause = (int(bits[1]) if len(bits) > 1 else 40), (int(bits[2]) if len(bits) > 2 else 20)
        steps.append({"Id": str(uuid.uuid4()), "Buttons": names, "Left": 0, "Right": 0, "HoldMs": _clamp(hold, 0, 10000), "PauseMs": _clamp(pause, 0, 10000)})
    return steps


def curve(preset, inner=0, outer=100):
    if preset not in PRESETS: raise ValueError("Presets: " + ", ".join(PRESETS))
    (x1, y1), (x2, y2) = PRESETS[preset]
    return {"Preset": preset, "BasePreset": preset, "Inner": inner, "Outer": outer, "X1": x1, "Y1": y1, "X2": x2, "Y2": y2}

# ---------------------------------------------------------------------------------------------------------------
# Sharing settings: the same codes and files as the Windows app (X20Ctl.Zone/PcMode/PcModeShare.cs)
# ---------------------------------------------------------------------------------------------------------------

CODE_PREFIX, SHARE_MAX = "X20PC1.", 64 * 1024


def _checked(p):
    """Shared settings are untrusted: anything malformed becomes a plain ValueError."""
    if not isinstance(p, dict): raise ValueError("That isn't PC mode settings.")
    p = dict(new_profile(), **{k: v for k, v in p.items() if k in ("Remap", "Curves", "Turbo", "Macros", "Sticks")})
    try: return validate(p)
    except (KeyError, TypeError, AttributeError) as e: raise ValueError(f"Those settings are damaged ({e}).")


def to_code(p):
    body = json.dumps({k: v for k, v in p.items() if k != "Name"}, separators=(",", ":")).encode()
    packer = zlib.compressobj(9, zlib.DEFLATED, -15)
    packed = packer.compress(body) + packer.flush()
    return CODE_PREFIX + base64.urlsafe_b64encode(packed).decode().rstrip("=")


def from_code(code):
    code = code.strip()
    if not code.startswith(CODE_PREFIX): raise ValueError("That isn't an X20CTL PC mode code. It starts with " + CODE_PREFIX)
    body = code[len(CODE_PREFIX):]
    if len(body) > SHARE_MAX: raise ValueError("That code is too long.")
    try:
        packed = base64.urlsafe_b64decode(body + "=" * (-len(body) % 4))
        unpacker = zlib.decompressobj(-15); data = unpacker.decompress(packed, SHARE_MAX + 1)
        if len(data) > SHARE_MAX or unpacker.unconsumed_tail: raise ValueError("That code is too long.")
        return _checked(json.loads(data))
    except (ValueError, zlib.error, UnicodeDecodeError) as e:
        if isinstance(e, ValueError) and str(e).startswith(("That code is too long", "Those settings", "That isn't")): raise
        raise ValueError("That code is damaged. Copy it again in one piece.")


def to_file_text(p): return json.dumps({"x20ctlPcMode": 1, "profile": {k: v for k, v in p.items() if k != "Name"}}, indent=2)


def from_file_text(text):
    if len(text.encode()) > SHARE_MAX: raise ValueError("That file is too big to be PC mode settings.")
    try: data = json.loads(text)
    except ValueError: raise ValueError("That isn't a PC mode settings file.")
    if not isinstance(data, dict) or data.get("x20ctlPcMode") != 1 or not isinstance(data.get("profile"), dict): raise ValueError("That isn't a PC mode settings file.")
    return _checked(data["profile"])


class HoldCombo:
    """A button combination held for a while (View + Menu for a second): fires once per hold."""
    def __init__(self, mask, hold_ms): self.mask, self.hold, self.since, self.fired = mask, hold_ms, -1, False
    def update(self, buttons, now_ms):
        if buttons & self.mask != self.mask: self.since, self.fired = -1, False; return False
        if self.since < 0: self.since = now_ms
        if self.fired or now_ms - self.since < self.hold: return False
        self.fired = True; return True


# ---------------------------------------------------------------------------------------------------------------
# Controller as mouse: a port of X20Ctl.Zone/Mouse/MouseMapper.cs, with the same settings file format
# ---------------------------------------------------------------------------------------------------------------

MOUSE_ACTIONS = ["None", "LeftClick", "RightClick", "MiddleClick", "Back", "Forward", "Precision", "Enter", "Escape", "Tab", "Space", "Backspace",
                 "Windows", "TaskView", "OnScreenKeyboard", "Up", "Down", "Left", "Right", "PageUp", "PageDown", "VolumeUp", "VolumeDown", "Mute", "PlayPause"]
MOUSE_INPUTS = [*MASK, "LT", "RT"]


def new_mouse():
    return {"PointerStick": "Left", "Speed": 1500, "Curve": 2.0, "Deadzone": 12, "Scroll": True, "ScrollSpeed": 18, "PrecisionPercent": 30.0, "Combo": True,
            "Buttons": {"A": "LeftClick", "B": "RightClick", "X": "MiddleClick", "Y": "OnScreenKeyboard", "LB": "Back", "RB": "Forward", "LT": "Precision", "RT": "LeftClick",
                        "DPAD_UP": "Up", "DPAD_DOWN": "Down", "DPAD_LEFT": "Left", "DPAD_RIGHT": "Right", "START": "Windows", "SELECT": "None", "L3": "Enter", "R3": "TaskView", "HOME": "None"}}


def validate_mouse(m):
    ok = (m.get("PointerStick") in ("Left", "Right") and 200 <= m.get("Speed", 0) <= 5000 and 1 <= m.get("Curve", 0) <= 3 and 0 <= m.get("Deadzone", -1) <= 40
          and 1 <= m.get("ScrollSpeed", 0) <= 60 and 10 <= m.get("PrecisionPercent", 0) <= 90 and isinstance(m.get("Buttons"), dict))
    if not ok or any(b not in MOUSE_INPUTS or a not in MOUSE_ACTIONS for b, a in m["Buttons"].items()): raise ValueError("Invalid mouse settings.")
    return m


def load_mouse(path=MOUSE_FILE):
    m = new_mouse()
    try:
        saved = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(saved, dict):
            buttons = dict(m["Buttons"], **(saved.get("Buttons") or {})); m.update(saved); m["Buttons"] = buttons
        return validate_mouse(m)
    except FileNotFoundError: return m
    except (ValueError, TypeError, AttributeError): return new_mouse()


def mouse_push(magnitude, deadzone, curve):
    dz = deadzone / 100.0
    if magnitude <= dz: return 0.0
    return _clamp((magnitude - dz) / (1 - dz), 0.0, 1.0) ** curve


class MouseMapper:
    def __init__(self, m):
        self.m = validate_mouse(m); self.cx = self.cy = self.cw = self.ch = 0.0; self.held = set()

    def precision(self): return any(a == "Precision" and b in self.held for b, a in self.m["Buttons"].items())

    def step(self, state, seconds):
        """(dx, dy, wheel, hwheel, edges): whole pixels, wheel units (120 a notch), and (action, down) button edges."""
        seconds = _clamp(seconds, 0.0, 0.1)
        buttons, lt, rt, lx, ly, rx, ry = state
        now = {n for n, mask in MASK.items() if buttons & mask}
        if lt > TRIGGER_THRESHOLD: now.add("LT")
        if rt > TRIGGER_THRESHOLD: now.add("RT")
        edges = []
        for name in MOUSE_INPUTS:
            down, was = name in now, name in self.held
            if down == was: continue
            action = self.m["Buttons"].get(name, "None")
            if action not in ("None", "Precision"): edges.append((action, down))
        self.held = now
        left = self.m["PointerStick"] == "Left"
        px, py, sx, sy = (lx, ly, rx, ry) if left else (rx, ry, lx, ly)
        nx, ny = px / 32768.0, py / 32768.0; mag = min(1.0, math.sqrt(nx * nx + ny * ny))
        speed = mouse_push(mag, self.m["Deadzone"], self.m["Curve"]) * self.m["Speed"] * (self.m["PrecisionPercent"] / 100.0 if self.precision() else 1.0)
        if mag > 0: self.cx += nx / mag * speed * seconds; self.cy -= ny / mag * speed * seconds
        dx, dy = int(self.cx), int(self.cy); self.cx -= dx; self.cy -= dy
        wheel = hwheel = 0
        if self.m["Scroll"]:
            wx, wy = sx / 32768.0, sy / 32768.0; dz = self.m["Deadzone"]
            self.cw += math.copysign(mouse_push(abs(wy), dz, 1.5), wy) * self.m["ScrollSpeed"] * 120 * seconds
            self.ch += math.copysign(mouse_push(abs(wx), dz + 10, 1.5), wx) * self.m["ScrollSpeed"] * 120 * seconds
            if abs(self.cw) >= 30: wheel = int(self.cw); self.cw -= wheel
            if abs(self.ch) >= 30: hwheel = int(self.ch); self.ch -= hwheel
            if abs(wy) < dz / 100.0: self.cw = 0.0
            if abs(wx) < dz / 100.0 + 0.1: self.ch = 0.0
        return dx, dy, wheel, hwheel, edges

    def release(self):
        edges = [(self.m["Buttons"].get(n, "None"), False) for n in self.held]
        self.held = set(); self.cx = self.cy = self.cw = self.ch = 0.0
        return [(a, d) for a, d in edges if a not in ("None", "Precision")]


# ---------------------------------------------------------------------------------------------------------------
# Linux input: evdev in, uinput out (raw ioctls, no packages)
# ---------------------------------------------------------------------------------------------------------------

EVENT = struct.Struct("llHHi")
EV_SYN, EV_KEY, EV_ABS = 0, 1, 3
ABS_X, ABS_Y, ABS_Z, ABS_RX, ABS_RY, ABS_RZ, ABS_GAS, ABS_BRAKE, ABS_HAT0X, ABS_HAT0Y = 0, 1, 2, 3, 4, 5, 9, 10, 0x10, 0x11
BTN_A, BTN_B, BTN_X, BTN_Y, BTN_TL, BTN_TR, BTN_TL2, BTN_TR2, BTN_SELECT, BTN_START, BTN_MODE, BTN_THUMBL, BTN_THUMBR = 0x130, 0x131, 0x133, 0x134, 0x136, 0x137, 0x138, 0x139, 0x13A, 0x13B, 0x13C, 0x13D, 0x13E
BTN_DPAD = {0x220: "DPAD_UP", 0x221: "DPAD_DOWN", 0x222: "DPAD_LEFT", 0x223: "DPAD_RIGHT"}


def _ioc(direction, kind, nr, size): return (direction << 30) | (size << 16) | (ord(kind) << 8) | nr


EVIOCGID = _ioc(2, "E", 0x02, 8)
EVIOCGRAB = _ioc(1, "E", 0x90, 4)
def EVIOCGNAME(n): return _ioc(2, "E", 0x06, n)
def EVIOCGBIT(ev, n): return _ioc(2, "E", 0x20 + ev, n)
def EVIOCGABS(axis): return _ioc(2, "E", 0x40 + axis, 24)
UI_DEV_CREATE, UI_DEV_DESTROY = _ioc(0, "U", 1, 0), _ioc(0, "U", 2, 0)
UI_SET_EVBIT, UI_SET_KEYBIT, UI_SET_ABSBIT = _ioc(1, "U", 100, 4), _ioc(1, "U", 101, 4), _ioc(1, "U", 103, 4)
# struct uinput_user_dev: name[80], input_id (4 x u16), ff_effects_max (u32), absmax/absmin/absfuzz/absflat [64] (s32)
USER_DEV = struct.Struct("80sHHHHI" + "64i" * 4)

# the xpad driver's layout, which most pads in XInput mode (EasySMX included) arrive in
XPAD_KEYS = {BTN_A: "A", BTN_B: "B", BTN_X: "X", BTN_Y: "Y", BTN_TL: "LB", BTN_TR: "RB", BTN_SELECT: "SELECT", BTN_START: "START", BTN_MODE: "HOME",
             BTN_THUMBL: "L3", BTN_THUMBR: "R3", BTN_TL2: "LT", BTN_TR2: "RT", **BTN_DPAD}
XPAD_AXES = {"LX": [ABS_X, False], "LY": [ABS_Y, True], "RX": [ABS_RX, False], "RY": [ABS_RY, True], "LT": [ABS_Z, False], "RT": [ABS_RZ, False]}


def _bits(fd, ev, count):
    buf = bytearray((count + 7) // 8)
    try: fcntl.ioctl(fd, EVIOCGBIT(ev, len(buf)), buf, True)
    except OSError: return set()
    return {i for i in range(count) if buf[i // 8] >> (i % 8) & 1}


def controllers():
    """Every event device that looks like a gamepad (buttons in the gamepad range plus axes), except our own."""
    found = []
    for path in sorted(glob.glob("/dev/input/event*"), key=lambda p: int(p.rsplit("event", 1)[1])):
        try: fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK)
        except OSError as e: found.append({"path": path, "error": e.strerror}); continue
        try:
            name = bytearray(256); fcntl.ioctl(fd, EVIOCGNAME(256), name, True)
            ident = bytearray(8); fcntl.ioctl(fd, EVIOCGID, ident, True)
            bus, vid, pid, ver = struct.unpack("HHHH", ident)
            keys, axes = _bits(fd, EV_KEY, 0x300), _bits(fd, EV_ABS, 0x40)
            n = name.split(b"\0", 1)[0].decode(errors="replace")
            if n != VIRTUAL_NAME and axes and any(0x130 <= k <= 0x13E for k in keys):
                found.append({"path": path, "name": n, "vid": vid, "pid": pid, "keys": keys, "axes": axes})
        finally: os.close(fd)
    return found


def absinfo(fd, axis):
    buf = bytearray(24)
    fcntl.ioctl(fd, EVIOCGABS(axis), buf, True)
    value, lo, hi, fuzz, flat, res = struct.unpack("6i", buf)
    return lo, hi


def norm_stick(v, lo, hi, invert):
    if hi <= lo: return 0
    s = (v - lo) / (hi - lo) * 65535 - 32768
    return int(_clamp(round(-s - 1 if invert else s), -32768, 32767))


def norm_trigger(v, lo, hi): return 0 if hi <= lo else int(_clamp(round((v - lo) / (hi - lo) * 255), 0, 255))


class RealPad:
    """The grabbed physical controller: while grabbed, no other program (games, Steam) gets its events."""

    def __init__(self, dev, layout):
        self.fd = os.open(dev["path"], os.O_RDONLY | os.O_NONBLOCK)
        self.keys = {int(k): v for k, v in (layout or {}).get("keys", {}).items()} or dict(XPAD_KEYS)
        self.axes = (layout or {}).get("axes") or XPAD_AXES
        self.ranges = {}
        for role, (code, _) in self.axes.items():
            try: self.ranges[code] = absinfo(self.fd, code)
            except OSError: pass
        for code in (ABS_HAT0X, ABS_HAT0Y):
            try: self.ranges[code] = absinfo(self.fd, code)
            except OSError: pass
        self.raw, self.pressed, self.hat = {}, set(), [0, 0]
        try: fcntl.ioctl(self.fd, EVIOCGRAB, 1)
        except OSError: os.close(self.fd); raise

    def read(self):
        """Drains pending events. Returns False when the controller went away."""
        try: data = os.read(self.fd, EVENT.size * 64)
        except BlockingIOError: return True
        except OSError: return False
        for off in range(0, len(data) - EVENT.size + 1, EVENT.size):
            _, _, kind, code, value = EVENT.unpack_from(data, off)
            if kind == EV_KEY and code in self.keys:
                (self.pressed.add if value else self.pressed.discard)(self.keys[code])
            elif kind == EV_ABS:
                if code == ABS_HAT0X: self.hat[0] = (value > 0) - (value < 0)
                elif code == ABS_HAT0Y: self.hat[1] = (value > 0) - (value < 0)
                else: self.raw[code] = value
        return True

    def state(self):
        buttons = 0
        for n in self.pressed:
            if n in MASK: buttons |= MASK[n]
        if self.hat[0] < 0: buttons |= LEFT
        if self.hat[0] > 0: buttons |= RIGHT
        if self.hat[1] < 0: buttons |= UP
        if self.hat[1] > 0: buttons |= DOWN
        vals = {}
        for role, (code, invert) in self.axes.items():
            lo, hi = self.ranges.get(code, (0, 0)); v = self.raw.get(code, (lo + hi) // 2 if role[1] in "XY" else lo)
            vals[role] = norm_trigger(v, lo, hi) if role in ("LT", "RT") else norm_stick(v, lo, hi, invert)
        lt = 255 if "LT" in self.pressed else vals.get("LT", 0)
        rt = 255 if "RT" in self.pressed else vals.get("RT", 0)
        return (buttons, lt, rt, vals.get("LX", 0), vals.get("LY", 0), vals.get("RX", 0), vals.get("RY", 0))

    def close(self):
        try: fcntl.ioctl(self.fd, EVIOCGRAB, 0)
        except OSError: pass
        os.close(self.fd)


OUT_KEYS = [(A, BTN_A), (B, BTN_B), (X, BTN_X), (Y, BTN_Y), (LB, BTN_TL), (RB, BTN_TR), (BACK, BTN_SELECT), (START, BTN_START), (GUIDE, BTN_MODE), (L3, BTN_THUMBL), (R3, BTN_THUMBR)]


def user_dev_bytes():
    """The legacy uinput setup record (works on every kernel): an Xbox 360 pad's identity and axis ranges, so Steam,
    SDL and Proton treat it as a normal Xbox controller."""
    absmax, absmin, absfuzz, absflat = [0] * 64, [0] * 64, [0] * 64, [0] * 64
    for a in (ABS_X, ABS_Y, ABS_RX, ABS_RY): absmin[a], absmax[a], absfuzz[a], absflat[a] = -32768, 32767, 16, 128
    for a in (ABS_Z, ABS_RZ): absmax[a] = 255
    for a in (ABS_HAT0X, ABS_HAT0Y): absmin[a], absmax[a] = -1, 1
    return USER_DEV.pack(VIRTUAL_NAME.encode(), 0x03, 0x045E, 0x028E, 0x0110, 0, *absmax, *absmin, *absfuzz, *absflat)


class VirtualPad:
    def __init__(self):
        self.fd = os.open("/dev/uinput", os.O_WRONLY | os.O_NONBLOCK)
        fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_KEY); fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_ABS)
        for _, code in OUT_KEYS: fcntl.ioctl(self.fd, UI_SET_KEYBIT, code)
        for a in (ABS_X, ABS_Y, ABS_Z, ABS_RX, ABS_RY, ABS_RZ, ABS_HAT0X, ABS_HAT0Y): fcntl.ioctl(self.fd, UI_SET_ABSBIT, a)
        os.write(self.fd, user_dev_bytes())
        fcntl.ioctl(self.fd, UI_DEV_CREATE)
        self.last = None

    def update(self, s):
        if s == self.last: return
        buttons, lt, rt, lx, ly, rx, ry = s
        events = [(EV_KEY, code, 1 if buttons & mask else 0) for mask, code in OUT_KEYS]
        hx = (1 if buttons & RIGHT else 0) - (1 if buttons & LEFT else 0); hy = (1 if buttons & DOWN else 0) - (1 if buttons & UP else 0)
        # Linux sticks are Y-down; X20CTL's state is Y-up like XInput
        events += [(EV_ABS, ABS_X, lx), (EV_ABS, ABS_Y, -ly - 1 if ly else 0), (EV_ABS, ABS_RX, rx), (EV_ABS, ABS_RY, -ry - 1 if ry else 0),
                   (EV_ABS, ABS_Z, lt), (EV_ABS, ABS_RZ, rt), (EV_ABS, ABS_HAT0X, hx), (EV_ABS, ABS_HAT0Y, hy), (EV_SYN, 0, 0)]
        os.write(self.fd, b"".join(EVENT.pack(0, 0, k, c, v) for k, c, v in events))
        self.last = s

    def close(self):
        try: fcntl.ioctl(self.fd, UI_DEV_DESTROY)
        except OSError: pass
        os.close(self.fd)

EV_REL, REL_X, REL_Y, REL_HWHEEL, REL_WHEEL, REL_WHEEL_HI_RES, REL_HWHEEL_HI_RES = 2, 0, 1, 6, 8, 0x0B, 0x0C
UI_SET_RELBIT = _ioc(1, "U", 102, 4)
MOUSE_BUTTONS = {"LeftClick": 0x110, "RightClick": 0x111, "MiddleClick": 0x112, "Back": 0x113, "Forward": 0x114}
MOUSE_KEYS = {"Enter": 28, "Escape": 1, "Tab": 15, "Space": 57, "Backspace": 14, "Windows": 125, "TaskView": 125, "Up": 103, "Down": 108, "Left": 105,
              "Right": 106, "PageUp": 104, "PageDown": 109, "VolumeUp": 115, "VolumeDown": 114, "Mute": 113, "PlayPause": 164}
MOUSE_REPEATS = {"Up", "Down", "Left", "Right", "Backspace", "PageUp", "PageDown", "VolumeUp", "VolumeDown", "Space"}


class VirtualMouse:
    """A uinput mouse with a few keyboard keys: what the controller drives while it is the mouse."""
    def __init__(self):
        self.fd = os.open("/dev/uinput", os.O_WRONLY | os.O_NONBLOCK)
        for ev in (EV_KEY, EV_REL): fcntl.ioctl(self.fd, UI_SET_EVBIT, ev)
        for code in (*MOUSE_BUTTONS.values(), *set(MOUSE_KEYS.values())): fcntl.ioctl(self.fd, UI_SET_KEYBIT, code)
        for code in (REL_X, REL_Y, REL_WHEEL, REL_HWHEEL, REL_WHEEL_HI_RES, REL_HWHEEL_HI_RES): fcntl.ioctl(self.fd, UI_SET_RELBIT, code)
        os.write(self.fd, USER_DEV.pack(b"X20CTL controller mouse", 0x03, 0, 0, 1, 0, *([0] * 256)))
        fcntl.ioctl(self.fd, UI_DEV_CREATE)
        self.notch = [0, 0]

    def send(self, events): os.write(self.fd, b"".join(EVENT.pack(0, 0, k, c, v) for k, c, v in [*events, (EV_SYN, 0, 0)]))

    def frame(self, dx, dy, wheel, hwheel):
        events = []
        if dx: events.append((EV_REL, REL_X, dx))
        if dy: events.append((EV_REL, REL_Y, dy))
        for i, (amount, hi, lo, sign) in enumerate(((wheel, REL_WHEEL_HI_RES, REL_WHEEL, 1), (hwheel, REL_HWHEEL_HI_RES, REL_HWHEEL, 1))):
            if not amount: continue
            events.append((EV_REL, hi, amount)); self.notch[i] += amount
            whole = int(self.notch[i] / 120)
            if whole: events.append((EV_REL, lo, whole)); self.notch[i] -= whole * 120
        if events: self.send(events)

    def act(self, action, value):
        """value: 1 down, 0 up, 2 a held key repeating."""
        if action in MOUSE_BUTTONS: self.send([(EV_KEY, MOUSE_BUTTONS[action], min(value, 1))])
        elif action in MOUSE_KEYS: self.send([(EV_KEY, MOUSE_KEYS[action], value)])
        elif action == "OnScreenKeyboard" and value == 1:
            for app in ("onboard", "wvkbd-mobintl", "squeekboard"):
                try: subprocess.Popen([app], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL); break
                except OSError: continue

    def close(self):
        try: fcntl.ioctl(self.fd, UI_DEV_DESTROY)
        except OSError: pass
        os.close(self.fd)


# ---------------------------------------------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------------------------------------------

NOTICE = """Before PC mode starts:
  1. Your settings only work while this is running. Stop it (Ctrl+C), or if it crashes, and your controller goes
     back to normal.
  2. Use turbo and macros with care in online games. Some games and anti-cheat systems treat them as cheating and
     may ban your account. You've been warned: you are responsible for the rules of the games you play, and X20CTL
     can't be held accountable for a ban.
  3. While it runs, games (and Steam) see one virtual Xbox 360 controller instead of your real one. If Steam Input
     also remaps it, turn Steam Input off for that game so the two don't fight."""

UDEV_RULE = 'KERNEL=="uinput", SUBSYSTEM=="misc", TAG+="uaccess", OPTIONS+="static_node=uinput"'


def pick(devs, wanted):
    if wanted:
        for d in devs:
            if d["path"] == wanted or d.get("name") == wanted: return d
        sys.exit(f"No controller called {wanted}. Run: python3 x20ctl-pcmode.py list")
    usable = [d for d in devs if "error" not in d]
    if not usable:
        denied = [d for d in devs if "error" in d]
        sys.exit("No controller found." + (" Some input devices couldn't be opened: add yourself to the \"input\" group (sudo usermod -aG input $USER, then log out and in) or run with sudo." if denied else " Plug one in and try again."))
    return usable[0]


def cmd_list(_):
    for d in controllers():
        print(f"{d['path']}  {d['error']}" if "error" in d else f"{d['path']}  {d['name']}  {d['vid']:04x}:{d['pid']:04x}")


def cmd_show(_):
    p = load_profile()
    if is_neutral(p): print("Nothing set: your controller goes straight to the game."); return
    for s, t in p["Remap"].items(): print(f"remap  {s} -> {t}")
    for b, hz in p["Turbo"].items(): print(f"turbo  {b} {hz}/s")
    for ch, c in p["Curves"].items(): print(f"curve  {ch} {c['BasePreset']} inner {c['Inner']}% outer {c['Outer']}%")
    for ch, s in p["Sticks"].items(): print(f"stick  {ch} inner {s['Inner']:.1f}% outer {s['Outer']:.1f}% square gate {'on' if s['SquareGate'] else 'off'} up/down {s['AxisRatio']}% anti-deadzone {s.get('AntiDeadzone', 0):.1f}% max {s.get('MaxOutput', 100):.1f}%" + (" RAW 1:1" if s.get('Raw') else ""))
    for t, steps in p["Macros"].items(): print(f"macro  {t}: " + "; ".join(f"{'+'.join(s['Buttons'])} {s['HoldMs']} {s['PauseMs']}" for s in steps))


def _flag(args, name, default):
    if name in args:
        i = args.index(name); value = int(args[i + 1]); del args[i:i + 2]; return value
    return default


def cmd_set(args):
    p = load_profile()
    if len(args) < 3: sys.exit("Usage: set remap A B | set turbo X 10 | set curve LeftStick slow [--inner N] [--outer N] | set stick LeftStick [--inner 4.5] [--outer 97.5] [--square on|off] [--ratio 80] [--anti 8] [--max 90] [--raw on|off] | set macro RB \"A 50 30\"")
    what, key = args[0], args[1].upper() if args[0] not in ("curve", "stick") else args[1]
    if what == "remap":
        target = args[2].upper()
        if not known(key) or not known(target): sys.exit("Buttons: " + " ".join([*MASK, "LT", "RT"]))
        if key == target: p["Remap"].pop(key, None)
        else: p["Remap"][key] = target
    elif what == "turbo":
        hz = int(args[2])
        if hz == 0: p["Turbo"].pop(key, None)
        else: p["Turbo"][key] = hz
    elif what == "curve":
        rest = args[2:]; inner = _flag(rest, "--inner", 0); outer = _flag(rest, "--outer", 100)
        if key not in CHANNELS: sys.exit("Channels: " + " ".join(CHANNELS))
        c = curve(rest[0], inner, outer)
        if is_default(c): p["Curves"].pop(key, None)
        else: p["Curves"][key] = c
    elif what == "macro":
        p["Macros"][key] = parse_macro(" ".join(args[2:]))
    elif what == "stick":
        if key not in STICKS: sys.exit("Sticks: " + " ".join(STICKS))
        s = dict(new_tuning(), **p["Sticks"].get(key, {})); rest = args[2:]
        for flag, field, kind in (("--inner", "Inner", float), ("--outer", "Outer", float), ("--ratio", "AxisRatio", int), ("--anti", "AntiDeadzone", float), ("--max", "MaxOutput", float)):
            if flag in rest: i = rest.index(flag); s[field] = round(kind(rest[i + 1]), 1) if kind is float else kind(rest[i + 1]); del rest[i:i + 2]
        for flag, field in (("--square", "SquareGate"), ("--raw", "Raw")):
            if flag in rest: i = rest.index(flag); s[field] = rest[i + 1].lower() in ("on", "1", "yes", "true"); del rest[i:i + 2]
        if tuning_default(s): p["Sticks"].pop(key, None)
        else: p["Sticks"][key] = s
    else: sys.exit("Set remap, turbo, curve, stick or macro.")
    save_profile(validate(p)); cmd_show([])


def cmd_clear(args):
    p = load_profile()
    if len(args) != 2: sys.exit("Usage: clear remap A | clear turbo X | clear curve LeftStick | clear macro RB")
    section = {"remap": "Remap", "turbo": "Turbo", "curve": "Curves", "macro": "Macros", "stick": "Sticks"}.get(args[0])
    if section is None: sys.exit("Clear remap, turbo, curve or macro.")
    p[section].pop(args[1] if section in ("Curves", "Sticks") else args[1].upper(), None)
    save_profile(p); cmd_show([])


def cmd_reset(_):
    save_profile(new_profile()); print("Everything back to normal.")


def _wait_change(fd, ranges, timeout=15.0):
    """The first button pressed, or the axis moved furthest from where it started."""
    base, start = {}, time.monotonic()
    for code, (lo, hi) in ranges.items():
        buf = bytearray(24); fcntl.ioctl(fd, EVIOCGABS(code), buf, True); base[code] = struct.unpack("6i", buf)[0]
    while time.monotonic() - start < timeout:
        r, _, _ = select.select([fd], [], [], 0.05)
        if not r: continue
        data = os.read(fd, EVENT.size * 64)
        for off in range(0, len(data) - EVENT.size + 1, EVENT.size):
            _, _, kind, code, value = EVENT.unpack_from(data, off)
            if kind == EV_KEY and value == 1: return ("key", code, 0)
            if kind == EV_ABS and code in ranges and code not in (ABS_HAT0X, ABS_HAT0Y):
                lo, hi = ranges[code]
                if hi > lo and abs(value - base[code]) > (hi - lo) * 0.4: return ("abs", code, 1 if value > base[code] else -1)
            if kind == EV_ABS and code in (ABS_HAT0X, ABS_HAT0Y) and value: return ("hat", code, value)
    return None


def cmd_learn(args):
    """Asks for each control in turn and saves which Linux code it sends, for this controller's USB/Bluetooth ID."""
    dev = pick(controllers(), args[0] if args else None)
    fd = os.open(dev["path"], os.O_RDONLY | os.O_NONBLOCK)
    ranges = {}
    for code in dev["axes"]:
        try: ranges[code] = absinfo(fd, code)
        except OSError: pass
    keys, axes = {}, {}
    print(f"Teaching X20CTL {dev['name']} ({dev['vid']:04x}:{dev['pid']:04x}). Press each control when asked; 15 s each, Ctrl+C to stop.")
    try:
        for name in ["A", "B", "X", "Y", "LB", "RB", "SELECT", "START", "HOME", "L3", "R3"]:
            print(f"  Press {name} ...", end=" ", flush=True); got = _wait_change(fd, ranges)
            if got and got[0] == "key": keys[str(got[1])] = name; print(f"code {got[1]:#x}")
            else: print("skipped")
            time.sleep(0.4); _drain(fd)
        for name, ask in [("LT", "Pull the left trigger all the way"), ("RT", "Pull the right trigger all the way")]:
            print(f"  {ask} ...", end=" ", flush=True); got = _wait_change(fd, ranges)
            if got and got[0] == "key": keys[str(got[1])] = name; print("a button")
            elif got and got[0] == "abs": axes[name] = [got[1], False]; print(f"axis {got[1]}")
            else: print("skipped")
            time.sleep(0.6); _drain(fd)
        for role, ask, want in [("LX", "Push the left stick RIGHT", 1), ("LY", "Push the left stick UP", 1), ("RX", "Push the right stick RIGHT", 1), ("RY", "Push the right stick UP", 1)]:
            print(f"  {ask} ...", end=" ", flush=True); got = _wait_change(fd, ranges)
            if got and got[0] == "abs": axes[role] = [got[1], got[2] != want]; print(f"axis {got[1]}" + (" (reversed)" if got[2] != want else ""))
            else: print("skipped")
            time.sleep(0.6); _drain(fd)
        for code, name in BTN_DPAD.items(): keys.setdefault(str(code), name)
    finally: os.close(fd)
    layouts = _read_json(LAYOUT_FILE, {})
    layouts[f"{dev['vid']:04x}:{dev['pid']:04x}"] = {"name": dev["name"], "keys": keys, "axes": axes}
    _write_json(LAYOUT_FILE, layouts)
    print(f"Saved. PC mode uses this layout whenever {dev['vid']:04x}:{dev['pid']:04x} is plugged in.")


def _drain(fd):
    try:
        while os.read(fd, EVENT.size * 64): pass
    except (BlockingIOError, OSError): pass


def cmd_run(args):
    state = _read_json(STATE_FILE, {})
    if not state.get("noticeSeen"):
        print(NOTICE)
        if input('\nType "I understand" to start: ').strip().lower() != "i understand": sys.exit("Not started.")
        state["noticeSeen"] = True; _write_json(STATE_FILE, state)
    if not os.access("/dev/uinput", os.W_OK):
        sys.exit("PC mode can't create the virtual controller: /dev/uinput isn't writable for you.\n"
                 "Either run with sudo, or allow it once for your user (then unplug and replug, or reboot):\n"
                 f"  echo '{UDEV_RULE}' | sudo tee /etc/udev/rules.d/60-x20ctl-uinput.rules\n"
                 "  sudo udevadm control --reload-rules && sudo udevadm trigger")
    wanted = args[0] if args else None
    profile_mtime, profile = None, new_profile()
    print("PC mode is running. Ctrl+C stops it and gives your controller back.")
    real = virtual = None; processor = None; status = ""; paused = False; combo = HoldCombo(BACK | START, 1000)
    def say(text):
        nonlocal status
        if text != status: status = text; print(text, flush=True)
    try:
        while True:
            try: mtime = PROFILE_FILE.stat().st_mtime
            except FileNotFoundError: mtime = 0
            if mtime != profile_mtime:
                try: profile = load_profile(); processor = None
                except (ValueError, KeyError, json.JSONDecodeError) as e: say(f"Settings file not usable ({e}); keeping the previous settings.")
                profile_mtime = mtime
            if is_neutral(profile):
                if real: real.close(); real = None
                if virtual: virtual.close(); virtual = None
                say("Ready: nothing to apply yet. Set a remap, curve, turbo or macro and it starts by itself."); time.sleep(0.25); continue
            if real is None:
                devs = [d for d in controllers() if "error" not in d]
                dev = next((d for d in devs if not wanted or d["path"] == wanted or d["name"] == wanted), None)
                if dev is None: say("Ready: no controller found. Plug one in and it starts by itself."); time.sleep(0.5); continue
                layout = _read_json(LAYOUT_FILE, {}).get(f"{dev['vid']:04x}:{dev['pid']:04x}")
                try: real = RealPad(dev, layout)
                except OSError as e: say(f"Couldn't take {dev['name']}: {e.strerror}. Is another program holding it?"); time.sleep(1); continue
                if virtual is None: virtual = VirtualPad()
                processor = None
                say(f"Active: {dev['name']} is hidden from games; they see one Xbox controller with your settings.")
            if processor is None: processor = PadProcessor(profile); clock = time.monotonic()
            select.select([real.fd], [], [], 0.001)
            if not real.read():
                real.close(); real = None; say("The controller went away. Plug it back in and it starts again."); continue
            now_ms = int((time.monotonic() - clock) * 1000); state = real.state()
            if combo.update(state[0], now_ms):
                paused = not paused
                say("Your settings are off for now: the controller works as it normally does. Hold View + Menu again to switch them on." if paused else "Your settings are back on.")
            virtual.update(state if paused else processor.process(state, now_ms))
    except KeyboardInterrupt: pass
    finally:
        if real: real.close()
        if virtual: virtual.close()
        print("PC mode stopped. Your controller is back to normal.")


def cmd_export(args):
    p = load_profile()
    if args:
        Path(args[0]).write_text(to_file_text(p), encoding="utf-8"); print(f"Saved to {args[0]}. Open it in X20CTL on Windows, or import it here.")
    else: print(to_code(p))


def cmd_import(args):
    if not args: sys.exit("Usage: import CODE  or  import FILE.x20pc")
    source = " ".join(args)
    if source.strip().startswith(CODE_PREFIX): p = from_code(source)
    else:
        path = Path(source)
        if not path.is_file(): sys.exit("That's neither an X20CTL code nor a file.")
        if path.stat().st_size > SHARE_MAX: sys.exit("That file is too big to be PC mode settings.")
        p = from_file_text(path.read_text(encoding="utf-8"))
    save_profile(p); print("Settings imported."); cmd_show([])


def cmd_mouse(args):
    m = load_mouse()
    if args and args[0] == "show":
        print(json.dumps({k: v for k, v in m.items() if k != "Buttons"}, indent=2))
        for b in MOUSE_INPUTS: print(f"  {b:<11} {m['Buttons'].get(b, 'None')}")
        print("Actions: " + " ".join(MOUSE_ACTIONS)); return
    if args and args[0] == "set":
        if len(args) != 3: sys.exit("Usage: mouse set speed 1500 | curve 2 | deadzone 12 | scroll 18 | precision 30 | pointer left | combo on | A LeftClick")
        what, value = args[1], args[2]
        fields = {"speed": ("Speed", int), "curve": ("Curve", float), "deadzone": ("Deadzone", int), "scroll": ("ScrollSpeed", int), "precision": ("PrecisionPercent", float)}
        if what.lower() in fields: field, kind = fields[what.lower()]; m[field] = kind(value)
        elif what.lower() == "pointer": m["PointerStick"] = value.capitalize()
        elif what.lower() == "combo": m["Combo"] = value.lower() in ("on", "1", "yes", "true")
        elif what.lower() == "scrolling": m["Scroll"] = value.lower() in ("on", "1", "yes", "true")
        elif what.upper() in MOUSE_INPUTS: m["Buttons"][what.upper()] = next((a for a in MOUSE_ACTIONS if a.lower() == value.lower()), value)
        else: sys.exit("Settings: speed curve deadzone scroll precision pointer combo scrolling, or a button name.")
        validate_mouse(m); _write_json(MOUSE_FILE, m); print("Saved."); return
    if not os.access("/dev/uinput", os.W_OK):
        sys.exit("The controller mouse needs /dev/uinput. Run with sudo, or allow it once for your user:\n"
                 f"  echo '{UDEV_RULE}' | sudo tee /etc/udev/rules.d/60-x20ctl-uinput.rules\n  sudo udevadm control --reload-rules && sudo udevadm trigger")
    devs = [d for d in controllers() if "error" not in d]
    if not devs: sys.exit("No controller found. Plug one in and try again.")
    dev = devs[0]; layout = _read_json(LAYOUT_FILE, {}).get(f"{dev['vid']:04x}:{dev['pid']:04x}")
    real = RealPad(dev, layout); mouse = VirtualMouse(); mapper = MouseMapper(m); combo = HoldCombo(BACK | Y, 1000)
    active, repeat, last, start = True, {}, time.monotonic(), time.monotonic()
    print(f"{dev['name']} is the mouse. The {m['PointerStick'].lower()} stick moves the pointer. Hold View + Y to give it back to games; Ctrl+C stops.")
    try:
        while True:
            select.select([real.fd], [], [], 0.004)
            if not real.read(): print("The controller went away."); break
            now = time.monotonic(); dt, last = now - last, now
            state = real.state()
            if m["Combo"] and combo.update(state[0], int((now - start) * 1000)):
                active = not active
                for action, down in mapper.release(): mouse.act(action, 0)
                repeat.clear()
                try: fcntl.ioctl(real.fd, EVIOCGRAB, 1 if active else 0)
                except OSError: pass
                print("The controller is the mouse again." if active else "The controller is a controller again: games see it. Hold View + Y to make it the mouse.")
            if not active: continue
            if state[0] & BACK: state = (state[0] & ~Y,) + tuple(state[1:])
            dx, dy, wheel, hwheel, edges = mapper.step(state, dt)
            mouse.frame(dx, dy, wheel, hwheel)
            for action, down in edges:
                mouse.act(action, 1 if down else 0)
                if down and action in MOUSE_REPEATS: repeat[action] = now + 0.4
                else: repeat.pop(action, None)
            for action, at in list(repeat.items()):
                if now >= at: mouse.act(action, 2); repeat[action] = now + 0.06
    except KeyboardInterrupt: pass
    finally:
        for action, _ in mapper.release(): mouse.act(action, 0)
        mouse.close(); real.close(); print("Controller mouse stopped. Your controller is back to normal.")


def main(argv):
    commands = {"list": cmd_list, "show": cmd_show, "set": cmd_set, "clear": cmd_clear, "reset": cmd_reset, "learn": cmd_learn, "run": cmd_run,
                "export": cmd_export, "import": cmd_import, "mouse": cmd_mouse}
    if argv and argv[0] == "--version": print(VERSION); return
    if not argv or argv[0] in ("-h", "--help", "help") or argv[0] not in commands: print(__doc__); return
    try: commands[argv[0]](argv[1:])
    except ValueError as e: sys.exit(str(e))
    except PermissionError as e: sys.exit(f"Permission denied: {e.filename}. See the Permissions note in --help.")
    except OSError as e: sys.exit(f"PC mode stopped: {e.strerror or e} ({e.filename or 'a controller device'}). Your controller is back to normal.")


if __name__ == "__main__":
    if not sys.platform.startswith("linux"): sys.exit("This is the Linux version. On Windows, PC mode is in X20CTL → Tools.")
    main(sys.argv[1:])
