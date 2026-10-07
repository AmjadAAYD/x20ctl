"""Observed 10-byte gameplay layout. Not a model detector or configuration driver."""

import uuid


def known_profile(row):
    return (
        row.get("vid") == 0x0079
        and row.get("pid") == 0x181C
        and row.get("usage_page") == 1
        and row.get("usage") == 5
        and row.get("input_len") == 10
        and row.get("output_len") == 5
        and row.get("feature_len") == 0
    )


def axis(value):
    return (value - 128) / (128 if value < 128 else 127)


def decode_hid(frame):
    if len(frame) != 10 or frame[0] != 0 or frame[3] not in {*range(8), 15}:
        raise ValueError("Unrecognized gameplay report; no input inferred")
    buttons = [
        name
        for offset, mask, name in [
            (1, 1, "A"),
            (1, 2, "B"),
            (1, 8, "X"),
            (1, 16, "Y"),
            (1, 64, "LB"),
            (1, 128, "RB"),
            (2, 1, "LT"),
            (2, 2, "RT"),
            (2, 4, "SELECT"),
            (2, 8, "START"),
            (2, 32, "L3"),
            (2, 64, "R3"),
        ]
        if frame[offset] & mask
    ]
    hat = frame[3]
    for name, states in {
        "DPAD_UP": (0, 1, 7),
        "DPAD_RIGHT": (1, 2, 3),
        "DPAD_DOWN": (3, 4, 5),
        "DPAD_LEFT": (5, 6, 7),
    }.items():
        if hat in states:
            buttons.append(name)
    return {
        "slot": 0,
        "buttons": buttons,
        "leftStick": {"x": axis(frame[4]), "y": axis(frame[5])},
        "rightStick": {"x": axis(frame[6]), "y": axis(frame[7])},
        "leftTrigger": frame[8] / 255,
        "rightTrigger": frame[9] / 255,
        "source": "HID candidate",
        "axisOrientation": "candidate",
    }


def decode_xinput(values):
    mapping = {
        1: "DPAD_UP",
        2: "DPAD_DOWN",
        4: "DPAD_LEFT",
        8: "DPAD_RIGHT",
        0x10: "START",
        0x20: "SELECT",
        0x40: "L3",
        0x80: "R3",
        0x100: "LB",
        0x200: "RB",
        0x1000: "A",
        0x2000: "B",
        0x4000: "X",
        0x8000: "Y",
    }
    return {
        "slot": values["slot"],
        "buttons": [name for bit, name in mapping.items() if values["buttons"] & bit],
        "leftStick": {"x": values["lx"] / 32768, "y": -values["ly"] / 32768},
        "rightStick": {"x": values["rx"] / 32768, "y": -values["ry"] / 32768},
        "leftTrigger": values["lt"] / 255,
        "rightTrigger": values["rt"] / 255,
        "source": "XInput",
        "axisOrientation": "standard",
    }


class InputBindings:
    def __init__(self, backend, worker_factory=None):
        from .scan_worker import WorkerSession

        self.backend = backend
        self.worker_factory = worker_factory or WorkerSession
        self.before = None
        self.sources = {}
        self.source_owner = None
        self.bindings = {}

    def discover(self, phase):
        if phase == "before":
            self.before = self.backend.inventory()
            self.sources = {}
            return []
        if phase != "after" or self.before is None:
            raise ValueError(
                "First disconnect the target and capture the before inventory"
            )
        after = self.backend.inventory()
        previous = {r["_key"] for r in self.before["devices"]}
        rows = self.backend.details(
            [r for r in after["devices"] if r["_key"] not in previous]
        )
        self.sources = {}
        for row in rows:
            if row.get("kind") == "hid" and known_profile(row):
                self.sources[uuid.uuid4().hex] = {
                    "source": "hid_input",
                    "selected": row,
                    "key": row["_key"],
                    "label": "Gameplay HID 0079:181C (candidate layout)",
                }
        for slot in sorted(
            set(after["xinput_slots"]) - set(self.before["xinput_slots"])
        ):
            self.sources[uuid.uuid4().hex] = {
                "source": "xinput_state",
                "slot": slot,
                "key": f"xinput:{slot}",
                "label": f"XInput player slot {slot + 1}",
            }
        return [
            {"token": token, "label": item["label"]}
            for token, item in self.sources.items()
        ]

    def attach(self, player, token):
        if type(player) is not int or not 1 <= player <= 4 or token not in self.sources:
            raise ValueError("Select an input source from this discovery")
        item = self.sources[token]
        if any(
            number != player and binding["key"] == item["key"]
            for number, binding in self.bindings.items()
        ):
            raise ValueError("This source is assigned to another player")
        self.detach(player)
        worker = self.worker_factory()
        try:
            worker.call(
                {
                    "operation": "input_attach",
                    **{
                        k: v
                        for k, v in item.items()
                        if k in {"source", "slot", "selected"}
                    },
                }
            )
        except Exception:
            worker.close()
            raise
        self.bindings[player] = {
            "worker": worker,
            "key": item["key"],
            "label": item["label"],
            "last": None,
        }
        return {"connected": True, "source": item["label"]}

    def poll(self, player):
        binding = self.bindings.get(player)
        if not binding:
            return {"connected": False, "input": None}
        try:
            value = binding["worker"].call({"operation": "input_read"}, timeout=2)
            if value is not None:
                binding["last"] = value
            return {
                "connected": True,
                "input": binding["last"],
                "source": binding["label"],
            }
        except Exception:
            self.detach(player)
            return {"connected": False, "input": None}

    def detach(self, player):
        binding = self.bindings.pop(player, None)
        if binding:
            binding["worker"].close()

    def close(self):
        for player in list(self.bindings):
            self.detach(player)
