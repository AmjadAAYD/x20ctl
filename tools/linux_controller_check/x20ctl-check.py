#!/usr/bin/env python3
"""X20CTL Controller Check for Linux - the same guided, read-only controller scan as the Windows app.

Runs on any Linux distro with Python 3.8+ (Ubuntu, Debian, Mint, Pop!_OS, Fedora, Nobara, openSUSE, Arch, Garuda,
Manjaro, EndeavourOS, SteamOS, ...). No packages are needed: devices are found through /sys, input is read from
/dev/input/event* directly (python-evdev is used only if it happens to be installed), HID descriptors come from
/sys/class/hidraw, the battery from /sys/class/power_supply.

What it does (read-only throughout; nothing is ever written to the controller):
  * lists game controllers with their USB/Bluetooth IDs, and which known controllers they resemble
  * parses each controller's HID report descriptor: stick bit depth, vendor-defined channels, report sizes
  * reads the battery level where the kernel exposes one
  * guides you through every button, the D-pad, both triggers and both sticks, step by step, showing each hold live
    ("Holding B 1.32 s"), and records which Linux code each control sends, its range and its timing
  * measures resting drift and how many reports per second arrive while the sticks move
  * saves a ZIP report under ~/X20CTLInputReports and, if you agree at the start, sends a compact summary to
    X20CTLADMIN. Send the ZIP to @mistermajid on Discord to help add support for your controller.
Not part of the scan, by design: RGB lighting (lives in firmware), macro M paddles (they send ordinary buttons),
gyro and turbo.

Reading /dev/input needs permission: run it as a user in the "input" group, or with sudo. The script never changes
permissions itself.
"""
from __future__ import annotations

import fcntl, glob, hashlib, json, os, platform, select, struct, sys, termios, time, tty, uuid, zipfile
from datetime import datetime, timezone
from pathlib import Path
from urllib import request, error

VERSION = "2.0.0-local"
HOST = "x20ctl-linux-controller-check"
ENDPOINT = "https://x20-admin.vercel.app/api/controller-report"
DISCORD = "mistermajid"
EVENT = struct.Struct("llHHi")  # struct input_event on 64-bit and 32-bit glibc/musl
EV_KEY, EV_ABS, EV_SYN = 1, 3, 0
ABS_NAMES = {0x00: "ABS_X", 0x01: "ABS_Y", 0x02: "ABS_Z", 0x03: "ABS_RX", 0x04: "ABS_RY", 0x05: "ABS_RZ", 0x09: "ABS_GAS", 0x0A: "ABS_BRAKE", 0x10: "ABS_HAT0X", 0x11: "ABS_HAT0Y"}
KEY_NAMES = {0x130: "BTN_SOUTH", 0x131: "BTN_EAST", 0x132: "BTN_C", 0x133: "BTN_NORTH", 0x134: "BTN_WEST", 0x135: "BTN_Z", 0x136: "BTN_TL", 0x137: "BTN_TR",
             0x138: "BTN_TL2", 0x139: "BTN_TR2", 0x13A: "BTN_SELECT", 0x13B: "BTN_START", 0x13C: "BTN_MODE", 0x13D: "BTN_THUMBL", 0x13E: "BTN_THUMBR",
             0x220: "BTN_DPAD_UP", 0x221: "BTN_DPAD_DOWN", 0x222: "BTN_DPAD_LEFT", 0x223: "BTN_DPAD_RIGHT"}
KNOWN = {(0x045E, 0x028E): "Xbox 360-compatible identity. Most pads in XInput mode, EasySMX included, use it; it proves nothing about the model.",
         (0x045E, 0x02E0): "Xbox Wireless (Bluetooth) identity, shared by many pads.",
         (0x0079, 0x181C): "Seen with the EasySMX X20 in DInput mode.",
         (0x1D57, 0xFA60): "Seen with the EasySMX X20's 2.4 GHz receiver (Xenta chip).",
         (0x1A34, 0xF517): "Reported for the EasySMX X15's 2.4 GHz receiver (research notes, not yet seen here).",
         (0x2345, 0xE062): "Reported for the EasySMX D10's receiver, a vendor-defined HID device (research notes, not yet seen here)."}
# guided steps: (id, what to do, kind)
STEPS = [("neutral", "Put the controller down and don't touch it. This measures stick drift at rest.", "rest")] + \
    [(b, f"Press and hold {b} for 2 seconds, then let go. Do it twice.", "button") for b in ["A", "B", "X", "Y", "LB", "RB"]] + \
    [(d, f"Press and hold D-pad {a} for 2 seconds, then let go. Twice.", "dpad") for d, a in [("dpad_up", "up"), ("dpad_right", "right"), ("dpad_down", "down"), ("dpad_left", "left"),
                                                                                         ("dpad_up_right", "up-right (both together)"), ("dpad_down_right", "down-right"), ("dpad_down_left", "down-left"), ("dpad_up_left", "up-left")]] + \
    [(b, f"Press and hold {n} for 2 seconds, then let go. Twice.", "button") for b, n in [("Start", "Start / Menu"), ("Back", "Back / View"), ("L3", "the left stick click"), ("R3", "the right stick click")]] + \
    [(t, f"Pull {t} slowly all the way in, hold it 2 seconds, let it out. Twice.", "trigger") for t in ["LT", "RT"]] + \
    [(s, f"Roll the {s.replace('_', ' ')} slowly round its edge, twice, then let it centre.", "stick") for s in ["left_stick", "right_stick"]]

C = {"b": "\033[1m", "d": "\033[2m", "g": "\033[32m", "y": "\033[33m", "c": "\033[36m", "r": "\033[31m", "x": "\033[0m"} if sys.stdout.isatty() else {k: "" for k in "bdgycrx"}


def say(text=""): print(text, flush=True)


def read(path, default=""):
    try: return Path(path).read_text(errors="replace").strip()
    except OSError: return default


# ---------------- discovery through /sys ----------------
def bits(hexmask):
    """/sys capability masks: space-separated hex words, most significant first."""
    out, words = set(), hexmask.split()
    for i, word in enumerate(reversed(words)):
        value = int(word, 16)
        for b in range(64):
            if value >> b & 1: out.add(i * 64 + b)
    return out


def gamepads():
    pads = []
    for node in sorted(glob.glob("/sys/class/input/event*")):
        dev = node + "/device"
        keys, axes = bits(read(dev + "/capabilities/key", "0")), bits(read(dev + "/capabilities/abs", "0"))
        if not (0x130 in keys and 0 in axes) and not (0x120 in keys and 0 in axes):  # BTN_SOUTH (or BTN_JOYSTICK) and ABS_X
            continue
        pads.append({"event": "/dev/input/" + os.path.basename(node), "name": read(dev + "/name"), "phys": read(dev + "/phys"), "uniq": "",
                     "bustype": int(read(dev + "/id/bustype", "0"), 16), "vendor": int(read(dev + "/id/vendor", "0"), 16), "product": int(read(dev + "/id/product", "0"), 16),
                     "version": int(read(dev + "/id/version", "0"), 16), "keys": sorted(keys), "axes": sorted(axes), "sysfs": os.path.realpath(dev)})
    return pads


BUS = {0x03: "USB", 0x05: "Bluetooth", 0x06: "Virtual", 0x19: "Host"}


# ---------------- HID descriptors from /sys/class/hidraw ----------------
def parse_descriptor(data: bytes):
    """A small HID report-descriptor walker: input values with their usage, bit size and range, and collections on
    vendor-defined usage pages. Enough to read stick resolution; not a full HID parser."""
    i, page, size, count, lmin, lmax, usages, umin, values, vendor, collection_page = 0, 0, 0, 0, 0, 0, [], None, [], [], []
    while i < len(data):
        prefix = data[i]
        if prefix == 0xFE: i += 3 + (data[i + 1] if i + 1 < len(data) else 0); continue  # long item
        n = (0, 1, 2, 4)[prefix & 3]; kind = (prefix >> 2) & 3; tag = prefix >> 4
        raw = data[i + 1:i + 1 + n]; i += 1 + n
        u = int.from_bytes(raw, "little", signed=False) if raw else 0
        s = int.from_bytes(raw, "little", signed=True) if raw else 0
        if kind == 1:  # global
            if tag == 0: page = u
            elif tag == 1: lmin = s
            elif tag == 2: lmax = s if s >= lmin else u
            elif tag == 7: size = u
            elif tag == 9: count = u
        elif kind == 2:  # local
            if tag == 0: usages.append((u >> 16, u & 0xFFFF) if n == 4 else (page, u))
            elif tag == 1: umin = u
            elif tag == 2 and umin is not None: usages.extend((page, x) for x in range(umin, u + 1)); umin = None
        elif kind == 0:  # main
            if tag == 0xA:  # collection
                p, us = (usages[0] if usages else (page, 0))
                if p >= 0xFF00: vendor.append({"usagePage": p, "usage": us})
                collection_page.append(p)
            elif tag == 0xC and collection_page: collection_page.pop()
            elif tag == 0x8 and not (u & 1):  # input, not constant
                for k in range(count):
                    p, us = usages[min(k, len(usages) - 1)] if usages else (page, 0)
                    values.append({"usagePage": p, "usage": us, "bits": size, "logicalMin": lmin, "logicalMax": lmax, "variable": bool(u & 2)})
            usages, umin = [], None
    names = {0x30: "X", 0x31: "Y", 0x32: "Z", 0x33: "Rx", 0x34: "Ry", 0x35: "Rz", 0x39: "Hat switch", 0xC4: "Accelerator", 0xC5: "Brake"}
    axes = [v | {"name": names.get(v["usage"], f"0x{v['usagePage']:04X}/0x{v['usage']:02X}")} for v in values if v["usagePage"] in (1, 2) and v["usage"] in names]
    return {"axes": axes, "vendorCollections": vendor, "inputValues": len(values)}


def hidraw_devices():
    out = []
    for node in sorted(glob.glob("/sys/class/hidraw/hidraw*")):
        uevent = dict(line.split("=", 1) for line in read(node + "/device/uevent").splitlines() if "=" in line)
        try: desc = Path(node + "/device/report_descriptor").read_bytes()
        except OSError: desc = b""
        hid_id = uevent.get("HID_ID", "0:0:0").split(":")
        entry = {"hidraw": "/dev/" + os.path.basename(node), "name": uevent.get("HID_NAME", ""), "bus": int(hid_id[0], 16) if hid_id[0] else 0,
                 "vendor": int(hid_id[1], 16) if len(hid_id) > 1 else 0, "product": int(hid_id[2], 16) if len(hid_id) > 2 else 0,
                 "descriptorBytes": len(desc), "descriptorHex": desc.hex(), "sysfs": os.path.realpath(node + "/device")}
        if desc: entry.update(parse_descriptor(desc))
        out.append(entry)
    return out


def batteries():
    out = []
    for node in sorted(glob.glob("/sys/class/power_supply/*")):
        if read(node + "/scope") != "Device" and "hid" not in os.path.basename(node).lower() and "controller" not in read(node + "/model_name").lower():
            continue
        out.append({"name": os.path.basename(node), "model": read(node + "/model_name"), "capacity": read(node + "/capacity"), "level": read(node + "/capacity_level"),
                    "status": read(node + "/status"), "sysfs": os.path.realpath(node + "/device")})
    return out


# ---------------- reading /dev/input ----------------
def ioc(direction, nr, size): return (direction << 30) | (size << 16) | (ord("E") << 8) | nr


def absinfo(fd, code):
    buf = bytearray(24)
    try: fcntl.ioctl(fd, ioc(2, 0x40 + code, 24), buf); return dict(zip(("value", "min", "max", "fuzz", "flat", "res"), struct.unpack("6i", buf)))
    except OSError: return None


class Pad:
    def __init__(self, info):
        self.info, self.fd = info, os.open(info["event"], os.O_RDONLY | os.O_NONBLOCK)
        self.abs = {c: absinfo(self.fd, c) for c in info["axes"]}
        self.value = {c: (a or {}).get("value", 0) for c, a in self.abs.items()}
        self.down, self.reports = {}, []

    def poll(self, timeout=0.01):
        """Returns the input events read (time, type, code, value)."""
        ready, _, _ = select.select([self.fd], [], [], timeout)
        if not ready: return []
        try: data = os.read(self.fd, EVENT.size * 128)
        except BlockingIOError: return []
        events = []
        for k in range(0, len(data) - EVENT.size + 1, EVENT.size):
            sec, usec, kind, code, value = EVENT.unpack_from(data, k)
            t = sec + usec / 1e6
            if kind == EV_SYN: self.reports.append(time.monotonic()); continue
            if kind == EV_ABS: self.value[code] = value
            if kind == EV_KEY:
                if value: self.down.setdefault(code, time.monotonic())
                else: self.down.pop(code, None)
            events.append((time.monotonic(), kind, code, value))
        return events

    def norm(self, code):
        a = self.abs.get(code)
        if not a or a["max"] <= a["min"]: return 0.0
        return max(-1.0, min(1.0, (self.value.get(code, 0) - (a["max"] + a["min"]) / 2) / ((a["max"] - a["min"]) / 2)))

    def close(self): os.close(self.fd)


# ---------------- the guided scan ----------------
class Keys:
    """Single-key reading without Enter (s = skip, q = quit), restored on exit."""
    def __enter__(self):
        self.tty = sys.stdin.isatty()
        if self.tty: self.old = termios.tcgetattr(sys.stdin); tty.setcbreak(sys.stdin.fileno())
        return self
    def key(self):
        if self.tty and select.select([sys.stdin], [], [], 0)[0]: return sys.stdin.read(1).lower()
        return ""
    def __exit__(self, *_):
        if self.tty: termios.tcsetattr(sys.stdin, termios.TCSADRAIN, self.old)


def run_step(pad, step, prompt, kind, keys):
    say(f"\n{C['b']}{prompt}{C['x']}  {C['d']}(s = skip, q = quit){C['x']}")
    for n in (3, 2, 1):
        sys.stdout.write(f"\r  {C['y']}Get ready… {n}{C['x']}   "); sys.stdout.flush(); end = time.monotonic() + 1
        while time.monotonic() < end:
            pad.poll(0.02); k = keys.key()
            if k == "s": say("\n  skipped"); return {"step": step, "status": "skipped"}
            if k == "q": raise KeyboardInterrupt
    start, events, holds, ranges = time.monotonic(), [], {}, {}
    pad.reports.clear()
    while time.monotonic() - start < 8:
        for t, kind_, code, value in pad.poll(0.01):
            events.append({"t_ms": round((t - start) * 1000, 1), "type": "key" if kind_ == EV_KEY else "abs", "code": code,
                           "name": KEY_NAMES.get(code) if kind_ == EV_KEY else ABS_NAMES.get(code, f"ABS_0x{code:02X}"), "value": value})
            if kind_ == EV_KEY and value == 0 and code in holds.get("_down", {}):
                holds.setdefault(code, []).append(round((t - holds["_down"].pop(code)) * 1000))
            if kind_ == EV_KEY and value: holds.setdefault("_down", {})[code] = t
            if kind_ == EV_ABS: lo, hi = ranges.get(code, (value, value)); ranges[code] = (min(lo, value), max(hi, value))
        k = keys.key()
        if k == "s": say("\n  skipped"); return {"step": step, "status": "skipped", "events": events}
        if k == "q": raise KeyboardInterrupt
        held = [(c, time.monotonic() - t) for c, t in pad.down.items()]
        if kind in ("button", "dpad") and held:
            c, d = max(held, key=lambda x: x[1]); live = f"Holding {KEY_NAMES.get(c, hex(c))} · {d:.2f} s"
        elif kind == "trigger":
            live = "  ".join(f"{ABS_NAMES.get(c, hex(c))} {pad.value.get(c, 0)}" for c in (2, 5, 9, 10) if c in pad.value) or "pull the trigger"
        elif kind in ("stick", "rest"):
            live = f"left {pad.norm(0):+.2f},{pad.norm(1):+.2f}  right {pad.norm(3):+.2f},{pad.norm(4):+.2f}"
        else: live = "waiting for the control…"
        sys.stdout.write(f"\r  {C['g']}RECORDING {8 - (time.monotonic() - start):4.1f} s{C['x']}  {live:<52}"); sys.stdout.flush()
    rate = 0
    if pad.reports:
        stamps = pad.reports; j = 0
        for k_, t in enumerate(stamps):
            while t - stamps[j] > 1: j += 1
            rate = max(rate, k_ - j + 1)
    holds.pop("_down", None)
    codes = sorted({e["name"] or str(e["code"]) for e in events if e["type"] == "key"})
    moved = {ABS_NAMES.get(c, hex(c)): {"min": lo, "max": hi} for c, (lo, hi) in ranges.items() if hi != lo}
    observed = bool(codes) if kind in ("button",) else bool(codes or moved) if kind in ("dpad", "trigger", "stick") else not events or all(abs(pad.norm(c)) < .24 for c in (0, 1, 3, 4) if c in pad.abs)
    say(f"\r  {(C['g'] + 'seen' if observed else C['y'] + 'not seen')}{C['x']}: {', '.join(codes) or ', '.join(moved) or 'no change'}" +
        (f"   holds {', '.join(f'{v / 1000:.2f} s' for vs in holds.values() for v in vs)}" if holds else "") + " " * 20)
    return {"step": step, "status": "observed" if observed else "not_observed", "keysSeen": codes, "axesMoved": moved,
            "holdsMs": {KEY_NAMES.get(c, hex(c)): v for c, v in holds.items()}, "reportsPerSecondMax": rate, "events": events[:4000]}


def findings(pad_info, hid, power, results, rest):
    f = []
    cap = next((b for b in power if b["capacity"]), None)
    f.append({"area": "Battery", "value": (cap["capacity"] + "%") if cap else "not reported", "detail": "From the kernel's power_supply entry for the controller." if cap else "The kernel exposes no battery for this connection (normal for wired pads and many receivers)."})
    vid, pid = pad_info["vendor"], pad_info["product"]
    f.append({"area": "Similar controllers", "value": KNOWN.get((vid, pid), "no known fingerprint"), "detail": f"Linux identity {vid:04X}:{pid:04X} over {BUS.get(pad_info['bustype'], hex(pad_info['bustype']))}. Matching IDs never prove a model."})
    mine = [h for h in hid if (h["vendor"], h["product"]) == (vid, pid)]
    axes = [a for h in mine for a in h.get("axes", []) if a["name"] in ("X", "Y", "Z", "Rx", "Ry", "Rz")]
    f.append({"area": "Stick resolution", "value": " · ".join(sorted({f"{a['bits']}-bit {a['name']}" for a in axes})) or "not described", "detail": "Bit sizes declared in the HID report descriptor."})
    vendor = [v for h in mine for v in h.get("vendorCollections", [])]
    f.append({"area": "Vendor channels", "value": f"{len(vendor)} vendor-defined collection(s)" if vendor else "none visible", "detail": "Where a configuration protocol would live; nothing was sent to them."})
    rates = [r.get("reportsPerSecondMax", 0) for r in results if r["step"] in ("left_stick", "right_stick")]
    f.append({"area": "Report rate", "value": f"{max(rates)} reports/s" if rates and max(rates) else "not measured", "detail": "Most reports the kernel delivered in one second while a stick moved. Host-side; not a verified polling rate."})
    if rest: f.append({"area": "Resting drift", "value": rest, "detail": "Largest distance from centre while untouched."})
    seen = sum(r["status"] == "observed" for r in results); f.append({"area": "Controls", "value": f"{seen} of {len(results)} recognised", "detail": "Each control pressed and held on this device."})
    f.append({"area": "Button codes", "value": "; ".join(f"{r['step']} → {', '.join(r['keysSeen'])}" for r in results if r.get("keysSeen")) or "none", "detail": "Which Linux input code each control sent: the controller's mapping as the kernel sees it."})
    f.append({"area": "Macro paddles", "value": "seen as ordinary buttons", "detail": "M paddles send the button they're set to. Record their macros on the Macros page."})
    f.append({"area": "Lighting (RGB)", "value": "not readable", "detail": "Lighting lives in the controller's firmware and isn't exposed through Linux input."})
    return f


def write_report(folder: Path, files: dict):
    folder.mkdir(parents=True, exist_ok=False)
    for name, value in files.items():
        (folder / name).write_text(value if isinstance(value, str) else json.dumps(value, indent=2), encoding="utf-8")
    listed = [{"path": p.name, "size": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(folder.iterdir())]
    (folder / "manifest.json").write_text(json.dumps({"schema": "input-diagnostic/1", "files": listed}, indent=2), encoding="utf-8")
    archive = folder.with_suffix(".zip")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as z:
        for p in sorted(folder.iterdir()): z.write(p, p.name)
    return archive


def upload(compact: dict, name: str, transport: str) -> str:
    buffer = Path("/tmp") / f"x20ctl-{uuid.uuid4().hex}.zip"
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as z:
        for n, v in compact.items(): z.writestr(n, v if isinstance(v, str) else json.dumps(v, indent=2))
    data = buffer.read_bytes(); buffer.unlink()
    if len(data) > 2 * 1024 * 1024: raise ValueError("report exceeds the receiver's 2 MiB limit")
    boundary = "x20ctl-" + uuid.uuid4().hex
    meta = json.dumps({"controllerName": name, "appVersion": "linux-check", "scannerVersion": VERSION, "clientTimestamp": datetime.now(timezone.utc).isoformat(), "connectionType": transport})
    body = b"".join(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode() for k, v in (("clientSubmissionId", str(uuid.uuid4())), ("metadata", meta)))
    body += f"--{boundary}\r\nContent-Disposition: form-data; name=\"report\"; filename=\"report.zip\"\r\nContent-Type: application/zip\r\n\r\n".encode() + data + f"\r\n--{boundary}--\r\n".encode()
    req = request.Request(ENDPOINT, body, {"Content-Type": f"multipart/form-data; boundary={boundary}"}, method="POST")
    with request.urlopen(req, timeout=20) as r:
        result = json.loads(r.read(4096))
    if not result.get("success") or not str(result.get("submissionId", "")).startswith("CR-"): raise ValueError("receiver returned no receipt")
    return result["submissionId"]


def main():
    if not sys.platform.startswith("linux"): sys.exit("This is the Linux Controller Check. On Windows, use Tools > Controller not working? in X20CTL.")
    say(f"{C['b']}{C['c']}X20CTL Controller Check for Linux{C['x']}  {C['d']}Scanner {VERSION} · read-only{C['x']}\n")
    say("Before you scan:")
    for line in ("Reads, never writes: no settings, lighting, vibration or firmware are touched.",
                 "Every button is pressed and held so its timing is recorded; sticks and triggers are swept for their range.",
                 "Battery: only some wireless connections report one. Stick resolution comes from the controller's HID description.",
                 "Lighting (RGB) will most likely be unreadable: it lives in the controller's firmware.",
                 "Not part of this scan: macro M paddles (they send ordinary buttons), gyro and turbo."):
        say(f"  • {line}")
    pads = gamepads()
    if not pads:
        say(f"\n{C['r']}No game controller found.{C['x']} Plug it in or pair it, wake it with Home, close other controller apps, then run this again."); return 1
    say(f"\n{C['b']}Controllers found{C['x']}")
    for i, p in enumerate(pads, 1): say(f"  {i}. {p['name'] or 'Unnamed'}  ({p['vendor']:04X}:{p['product']:04X} · {BUS.get(p['bustype'], 'bus ' + hex(p['bustype']))} · {p['event']})")
    choice = input(f"\nWhich one? [1-{len(pads)}, default 1] ").strip() or "1"
    info = pads[max(0, min(len(pads) - 1, int(choice) - 1 if choice.isdigit() else 0))]
    model = input("Printed model on the controller (e.g. X20, X15, D10; Enter if unsure): ").strip() or "Unknown"
    transport = input("Connected by [usb / receiver / bluetooth / not sure]: ").strip().lower() or "not sure"
    send = input("Send a compact summary to X20CTLADMIN (the X20CTL team's report site) when the scan ends? [y/N] ").strip().lower() == "y"
    try:
        pad = Pad(info)
    except PermissionError:
        say(f"\n{C['r']}No permission to read {info['event']}.{C['x']} Run with sudo, or add yourself to the input group (sudo usermod -aG input $USER, then log in again)."); return 1
    hid, power = hidraw_devices(), batteries()
    results, rest = [], ""
    try:
        with Keys() as keys:
            for step, prompt, kind in STEPS:
                r = run_step(pad, step, prompt, kind, keys); results.append(r)
                if step == "neutral":
                    offs = {s: max((abs(e["value"]) for e in r.get("events", []) if e["type"] == "abs" and e["code"] in c), default=0) for s, c in (("left", (0, 1)), ("right", (3, 4)))}
                    rest = f"left {abs(pad.norm(0)) * 100:.1f}% · right {abs(pad.norm(3)) * 100:.1f}% from centre"
    except KeyboardInterrupt:
        say("\nStopped early; the partial report is saved.")
    finally:
        pad.close()
    found = findings(info, hid, power, results, rest)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    folder = Path.home() / "X20CTLInputReports" / f"input-{stamp}-{uuid.uuid4().hex[:8]}"
    meta = {"collectorVersion": VERSION, "collectorHost": HOST, "claimedModel": model, "claimedTransport": transport, "modelDetected": False, "source": "linux_evdev",
            "device": {k: info[k] for k in ("name", "event", "bustype", "vendor", "product", "version")}, "configurationWrites": False, "automaticUpload": send,
            "hostTimingIsPollingRate": False, "os": platform.platform(), "distro": read("/etc/os-release").splitlines()[:4]}
    archive = write_report(folder, {"metadata.json": meta, "steps.json": results, "linux-inventory.json": {"inputDevices": pads, "hidraw": hid, "powerSupplies": power},
                                     "scan-summary.json": found, "README.txt": "Local input evidence only (Linux). Model/transport are owner claims. No configuration, vibration, firmware or hidden upload.\n"})
    say(f"\n{C['b']}Scan complete{C['x']}")
    for f in found: say(f"  {C['b']}{f['area']}{C['x']}: {f['value']}\n    {C['d']}{f['detail']}{C['x']}")
    say(f"\nSaved: {archive}")
    if send:
        name = info["name"] if model == "Unknown" else "EasySMX " + model
        try:
            receipt = upload({"device.json": {"controllerName": name, "claimedModel": model, "connection": transport, "vid": info["vendor"], "pid": info["product"], "hidraw": [{k: h.get(k) for k in ("name", "vendor", "product", "axes", "vendorCollections", "descriptorBytes")} for h in hid]},
                              "input-captures.json": [{k: r.get(k) for k in ("step", "status", "keysSeen", "axesMoved", "holdsMs", "reportsPerSecondMax")} for r in results],
                              "input-mapping.json": found, "system.json": {"reportSchemaVersion": 1, "collector": HOST, "collectorVersion": VERSION, "os": platform.platform()},
                              "scanner-version.txt": f"X20CTL Controller Check for Linux {VERSION}"}, name, transport)
            say(f"{C['g']}Sent to X20CTLADMIN · receipt {receipt}{C['x']}")
        except (OSError, ValueError, error.URLError) as e:
            say(f"{C['y']}Not sent to X20CTLADMIN ({e}). The ZIP is saved; share it by hand.{C['x']}")
    say(f"\n{C['c']}Please send the ZIP to @{DISCORD} on Discord. It helps add full support for your controller.{C['x']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
