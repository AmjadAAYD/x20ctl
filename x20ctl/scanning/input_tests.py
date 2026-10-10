"""Timed, labeled input recording; evidence does not imply configuration support."""
import json
import time
from .evidence import MAX_FILE, utc_now

ACTIONS = [
    ("neutral", "Keep your hands off the controller"),
    *[(name, f"Press and release the button marked {name} three times") for name in ("A", "B", "X", "Y", "LB", "RB")],
    *[("dpad_" + name, f"Press {name.replace('_', ' + ').upper()} on the cross-shaped direction pad, then release three times")
      for name in ("up", "right", "down", "left", "up_right", "down_right", "down_left", "up_left")],
    ("Start", "Press and release the START/MENU button three times; skip if unsure"),
    ("Back", "Press and release the BACK/VIEW/SELECT button three times; skip if unsure"),
    ("L3", "Push the LEFT stick down until it clicks, then release three times"),
    ("R3", "Push the RIGHT stick down until it clicks, then release three times"),
    ("LT", "Slowly pull LEFT trigger fully, then release"),
    ("RT", "Slowly pull RIGHT trigger fully, then release"),
    ("left_stick", "Move LEFT stick up, right, down, left, circle, then centre"),
    ("right_stick", "Move RIGHT stick up, right, down, left, circle, then centre"),
]


def summarize(action, samples):
    result = {"action": action, "sample_count": len(samples), "status": "no_samples"}
    if not samples: return result
    source = samples[0]["source"]
    result["source"] = source
    # A slot/API change is not a button transition on one selected controller.
    if len({s["source"] for s in samples}) != 1 or (
        source == "xinput_state"
        and len({s.get("values", {}).get("slot") for s in samples}) != 1
    ):
        result.update(status="inconclusive_source_changed",
                      interpretation="Input source changed during this action; select one source and repeat.")
        return result
    requested = None
    if source == "xinput_state":
        keys = set().union(*(s.get("values", {}) for s in samples)) - {"packet", "slot"}
        fields = sorted(k for k in keys if len({str(s.get("values", {}).get(k)) for s in samples}) > 1)
        result["changed_fields"] = fields
        buttons = [s["values"].get("buttons", 0) for s in samples]
        changed_bits = 0
        for value in buttons: changed_bits |= value ^ buttons[0]
        result["changed_button_bits"] = [f"{1 << bit:04X}" for bit in range(16) if changed_bits & (1 << bit)]
        result["observed_ranges"] = {key: {"min": min(s["values"][key] for s in samples),
                                                   "max": max(s["values"][key] for s in samples)}
                                     for key in fields if all(isinstance(s["values"].get(key), int) for s in samples)}
        changed = bool(fields)
        masks = {"A": 0x1000, "B": 0x2000, "X": 0x4000, "Y": 0x8000,
                 "LB": 0x0100, "RB": 0x0200, "Start": 0x0010, "Back": 0x0020,
                 "L3": 0x0040, "R3": 0x0080, "dpad_up": 1, "dpad_down": 2,
                 "dpad_left": 4, "dpad_right": 8, "dpad_up_right": 9,
                 "dpad_down_right": 10, "dpad_down_left": 6, "dpad_up_left": 5}
        if action in masks:
            mask = masks[action]
            requested = (any(value & mask == mask for value in buttons)
                         and any(value & mask == 0 for value in buttons))
        elif action in {"LT", "RT"} or action.startswith(("LT_", "RT_")):
            requested = action[:2].lower() in fields
        elif action.startswith("left_stick"):
            requested = bool({"lx", "ly"} & set(fields))
        elif action.startswith("right_stick"):
            requested = bool({"rx", "ry"} & set(fields))
    else:
        raw = [bytes.fromhex(s["report_hex"]) for s in samples]
        # Only compare like-sized frames; report IDs can denote distinct layouts.
        groups = {}
        for data in raw: groups.setdefault((len(data), data[:1]), []).append(data)
        offsets = sorted({i for group in groups.values() for i in range(len(group[0]))
                          if len({data[i] for data in group}) > 1})
        result["changed_byte_offsets"] = offsets
        changed = bool(offsets)
    result["status"] = "change_observed" if changed else "no_change_observed"
    if requested is not None:
        result["requested_control_observed"] = requested
        if changed and not requested:
            result["status"] = "unrelated_change_observed"
            result["interpretation"] = (
                "Other inputs changed, but the requested standard control did not. "
                "Check the selected source or existing button assignments before repeating."
            )
            return result
    result["interpretation"] = "Tentative action correlation, not a decoded configuration command"
    return result


def record_action(report, filename, action, reader, duration=5.0, on_sample=None):
    if not 0.1 <= duration <= 30: raise ValueError("Action duration out of bounds")
    samples = []
    started = time.monotonic()
    total = 0
    status = None
    with report.path(filename).open("x", encoding="utf-8", newline="\n") as stream:
        try:
            while time.monotonic() - started < duration:
                sample = reader.read(timeout_ms=100)
                if sample is None: continue
                sample = {**sample, "timestamp": utc_now(), "action": action,
                          "elapsed_ms": round((time.monotonic() - started) * 1000, 3)}
                line = json.dumps(sample, separators=(",", ":")) + "\n"
                total += len(line.encode("utf-8"))
                if total > MAX_FILE or len(samples) >= 10000:
                    status = "limit_reached"
                    break
                stream.write(line)
                stream.flush()
                samples.append(sample)
                if on_sample and len(samples) % 8 == 0:
                    on_sample(sample, len(samples))
                time.sleep(0.02)
        except OSError:
            status = "failed"
        except KeyboardInterrupt:
            status = "cancelled"
    result = summarize(action, samples)
    result["duration_ms"] = round((time.monotonic() - started) * 1000)
    result["file"] = filename
    if status: result["status"] = status
    return result
