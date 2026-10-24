"""What has actually been proven, per controller, firmware and operating mode.

A controller's capability descriptor says what it offers; it does not prove
that our write for that feature is the right one for it. This table is the
second half: what we have measured on real hardware. Writes go through only
when both agree, and a controller missing from this table is identified and
logged, never configured.

Rows are added from evidence, never from a descriptor. Each feature records
four separate facts, because they fail separately: the pad answers a read; it
acknowledges a write; reading back returns what was written; the setting
survives a power cycle.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from . import protocol as p


@dataclass(frozen=True)
class Proof:
    read: bool = False
    write: bool = False
    readback: bool = False
    persistence: bool = False
    note: str = ""

    @property
    def level(self) -> str:
        for name in ("persistence", "readback", "write", "read"):
            if getattr(self, name):
                return name
        return "unknown"


@dataclass(frozen=True)
class KnownController:
    product: str
    vid: str
    pid: str
    modes: frozenset[int]
    firmware: frozenset[str]
    features: dict[str, Proof] = field(default_factory=dict)


# Features, and the opcodes that read and write each one. A request the client
# sends is checked against the feature it touches.
FEATURES = ("remaps", "stick_curves", "trigger_curves", "vibration", "idle_timeout",
            "macros", "turbo", "lighting", "battery", "calibrate", "factory_reset")

READS = {
    p.Op.HOST_STICK: "stick_curves",
    p.Op.HOST_TRIGGER: "trigger_curves",
    p.Op.HOST_MOTOR: "vibration",
    p.Op.HOST_TURBO: "turbo",
    p.Op.READ_TURBO: "turbo",
    p.Op.HOST_MACRO: "macros",
    p.Op.READ_MACRO: "macros",
    p.Op.HOST_CHANGEKEY: "remaps",
    p.Op.HOST_LIGHTING: "lighting",
}

WRITES = {
    p.Op.WRITE_STICK: "stick_curves",
    p.Op.WRITE_TRIGGER: "trigger_curves",
    # the motor record also carries the idle timeout; both are proven on the X20
    p.Op.WRITE_MOTOR: "vibration",
    p.Op.WRITE_TURBO: "turbo",
    p.Op.WRITE_MACRO: "macros",
    p.Op.WRITE_CHANGEKEY: "remaps",
    p.Op.WRITE_LIGHTING: "lighting",
    p.Op.RECOVER: "factory_reset",
}

#: Sent to any controller: they are how the official app learns what it is
#: talking to, before it knows anything else.
IDENTIFY = frozenset({p.Op.READ_NAME, p.Op.READ_VID_PID_VERSION, p.Op.HOST_MENU})


_X20_SETTING = Proof(read=True, write=True, readback=True,
                     note="Round trip on an owner's X20; survival across power-off not recorded.")

KNOWN = (
    KnownController(
        product="EasySMX X20",
        vid="1013", pid="2009", modes=frozenset({2}), firmware=frozenset({"1.23"}),
        features={
            "remaps": _X20_SETTING,
            "stick_curves": _X20_SETTING,
            "trigger_curves": _X20_SETTING,
            "vibration": _X20_SETTING,
            "idle_timeout": _X20_SETTING,
            "macros": Proof(read=True, write=True, readback=True,
                            note="M4 round trip with chords and per-step timings."),
            "battery": Proof(read=True, note="Power query; the pad is returned to normal mode after."),
            "calibrate": Proof(write=True, note="Accepted; leaves the sticks alone. Effect on the gyro unverified."),
            "factory_reset": Proof(write=True, note="Generation 2 payload; settings returned to defaults."),
            # Turbo and lighting: the X20 reports neither, and neither write is proven.
        },
    ),
    KnownController(
        product="EasySMX X20 Pro",
        vid="1020", pid="2006", modes=frozenset({6}), firmware=frozenset({"1.44"}),
        features={
            "remaps": _X20_SETTING,
            "stick_curves": _X20_SETTING,
            "trigger_curves": _X20_SETTING,
            "vibration": _X20_SETTING,
            "idle_timeout": _X20_SETTING,
            "macros": Proof(read=True, write=True, readback=True,
                            note="M1-M6 round trip with chords and per-step timings."),
            "battery": Proof(read=True, note="Power query; the pad is returned to normal mode after."),
            "turbo": Proof(read=True, write=True, readback=True, note="Turbo supported on X20 Pro."),
        },
    ),
)


@dataclass(frozen=True)
class Assessment:
    """How far a connected controller may be trusted."""

    #: "validated": product, operating mode and firmware all proven.
    #: "known_product": proven product and mode on firmware not yet tested;
    #:   proven features stay available, each still verified by read-back.
    #: "known_product_other_mode": proven product in an operating mode never
    #:   tested; the mode can change the protocol, so reads only.
    #: "unknown": identified and logged; nothing beyond identification is sent.
    status: str
    product: str | None
    known: KnownController | None
    reason: str

    def may_read(self) -> bool:
        """Reads beyond identification. Proven products only: the official app
        sends these to the X20, and nobody has watched what another pad does
        with them."""
        return self.status != "unknown" and self.known is not None

    def may_write(self, feature: str) -> bool:
        if self.status not in ("validated", "known_product") or self.known is None:
            return False
        proof = self.known.features.get(feature)
        return bool(proof and proof.write)

    def table(self) -> dict[str, dict]:
        """Every feature with its evidence, for a UI or a report."""
        features = self.known.features if self.known else {}
        return {name: {"level": (features.get(name) or Proof()).level,
                       "read": (features.get(name) or Proof()).read,
                       "write": (features.get(name) or Proof()).write,
                       "readback": (features.get(name) or Proof()).readback,
                       "persistence": (features.get(name) or Proof()).persistence,
                       "allowed": self.may_write(name),
                       "note": (features.get(name) or Proof()).note}
                for name in FEATURES}

    def summary(self) -> dict:
        return {"status": self.status, "product": self.product, "reason": self.reason,
                "writable": [name for name in FEATURES if self.may_write(name)],
                "features": self.table()}


def assess(info: p.DeviceInfo) -> Assessment:
    """Look a controller's reported identity up in the evidence table.

    Matches on the technical identity only. The advertised name, the USB ids and
    descriptor flags such as the "Pro" bit are never used to decide this.
    """
    for known in KNOWN:
        if (info.vid, info.pid) != (known.vid, known.pid):
            continue
        if info.mode not in known.modes:
            return Assessment("known_product_other_mode", known.product, known,
                              f"{known.product} in operating mode {info.mode}, which has not been tested. "
                              "Settings can be read but not changed.")
        if info.version not in known.firmware:
            return Assessment("known_product", known.product, known,
                              f"{known.product} on firmware {info.version}; proven on "
                              f"{', '.join(sorted(known.firmware))}. Every change is checked by reading it back.")
        return Assessment("validated", known.product, known,
                          f"{known.product}, firmware {info.version}: proven.")
    return Assessment("unknown", None, None,
                      f"Unrecognised controller ({info.vid}:{info.pid}, firmware {info.version}, "
                      f"mode {info.mode}). It has been identified and its features logged; "
                      "nothing will be changed on it until it is validated.")


def request_feature(opcode: int, payload: bytes) -> tuple[str, str | None]:
    """Classify one outgoing request as ("identify" | "read" | "write", feature)."""
    if opcode in IDENTIFY:
        return "identify", None
    if opcode == p.Op.SET_MODE and bytes(payload) == p.POWER_QUERY_PAYLOAD:
        return "read", "battery"
    if opcode == p.Op.NORMAL_MODE and bytes(payload) == p.NORMAL_MODE_PAYLOAD:
        # sent after every battery read to stop the input stream it starts
        return "read", "battery"
    if opcode == p.Op.SET_MODE:
        return "write", "calibrate" if bytes(payload) == p.CALIBRATE_PAYLOAD else None
    if opcode in WRITES:
        return "write", WRITES[opcode]
    if 0x90 <= opcode <= 0xB7:
        return "read", READS.get(opcode)
    return "write", None
