"""Controller state, read before and after a change, and what differs.

This is the phone-free half of a Write scan. The owner saves the pad's settings,
changes one thing in the official app (or on the pad itself), saves them again,
and the two are compared record by record:

    x20 state save before.json --label before
    (change one setting in the official app, then close it)
    x20 state save after.json --label after
    x20 state diff before.json after.json

What it shows is what the pad held at each read: which record changed, which
bytes, and what those bytes mean where that is known. What it cannot show is
how the app got there: the messages it sent, their order, retries, the pad's
replies. Several different write sequences end in the same state. Nor does one
session prove a change survives a power-off. So a diff is evidence about the
stored record, never about a write packet, and it never enables a writer.

Only reads go to the pad, and only through the client's validation gate, so an
unproven controller is refused before anything is sent. Records the descriptor
says the pad doesn't expose are not asked for, as the official app doesn't.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone

from . import protocol as p

FORMAT = "x20ctl-state"
VERSION = 1
LABELS = ("before", "after", "restored", "other")

# (record name, read opcode, descriptor field, slot or None). The descriptor
# field is the HOST_MENU kind 1 byte that switches the matching page on.
RECORDS = (
    ("stick", p.Op.HOST_STICK, "sticks", None),
    ("trigger", p.Op.HOST_TRIGGER, "triggers", None),
    ("motor", p.Op.HOST_MOTOR, "motors", None),
    ("turbo", p.Op.HOST_TURBO, "turbo", None),
    ("changekey", p.Op.HOST_CHANGEKEY, "changekey", None),
    ("lighting", p.Op.HOST_LIGHTING, "lighting", None),
    *((f"macro{slot}", p.Op.HOST_MACRO, "macros", slot) for slot in range(1, 7)),
)

#: A record longer than this many continuation packets is refused as incomplete.
MAX_CHUNKS = 20


@dataclass
class Record:
    """One setting record as the pad returned it, size byte removed."""

    opcode: int
    #: "read", "not offered" (the descriptor switches it off), "no reply", or
    #: "incomplete" (fewer bytes arrived than the record declared).
    status: str
    declared: int | None = None
    data: bytes = b""

    def to_json(self) -> dict:
        out = {"opcode": f"{self.opcode:02X}", "status": self.status}
        if self.declared is not None:
            out["declared"] = self.declared
            out["data"] = self.data.hex()
        return out

    @classmethod
    def from_json(cls, d: dict) -> "Record":
        return cls(opcode=int(d["opcode"], 16), status=d["status"],
                   declared=d.get("declared"), data=bytes.fromhex(d.get("data", "")))


@dataclass
class State:
    label: str
    taken_at: str
    #: What the pad is and how it is set up: identity, descriptor and the
    #: remappable source order. If any of it differs, a comparison is void.
    context: dict
    records: dict[str, Record] = field(default_factory=dict)
    note: str = ""

    def to_json(self) -> dict:
        return {"format": FORMAT, "version": VERSION, "label": self.label,
                "takenAt": self.taken_at, "note": self.note, "context": self.context,
                "records": {name: r.to_json() for name, r in self.records.items()}}

    @classmethod
    def from_json(cls, d: dict) -> "State":
        if d.get("format") != FORMAT:
            raise ValueError("this is not a saved controller state")
        if d.get("version") != VERSION:
            raise ValueError(f"saved state version {d.get('version')} is not one this X20CTL reads")
        return cls(label=d.get("label", "other"), taken_at=d.get("takenAt", ""),
                   context=d.get("context", {}), note=d.get("note", ""),
                   records={n: Record.from_json(r) for n, r in d.get("records", {}).items()})

    def save(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(self.to_json(), f, indent=2)
            f.write("\n")

    @classmethod
    def load(cls, path: str) -> "State":
        with open(path, encoding="utf-8") as f:
            return cls.from_json(json.load(f))


async def _read_record(pad, opcode: int, slot: int | None) -> Record:
    """Read one record whole. Continuations keep the payload's shape: a bare
    index for most records, [position, slot] for a macro."""
    def ask(position: int) -> bytes:
        return bytes([position]) if slot is None else bytes([position, slot - 1])

    first = await pad.query(opcode, ask(0))
    if first is None or not first.crc_valid or not first.payload:
        return Record(opcode, "no reply")
    body = p.unwrap(first.payload)
    data = bytearray(body.data)
    position = 0
    while len(data) < body.declared and position < MAX_CHUNKS:
        position += 1
        more = await pad.query(opcode, ask(position))
        if more is None or not more.crc_valid:
            break
        data.extend(more.payload)
    if len(data) < body.declared:
        return Record(opcode, "incomplete", body.declared, bytes(data))
    return Record(opcode, "read", body.declared, bytes(data[:body.declared]))


async def capture(pad, label: str = "other", note: str = "") -> State:
    """Read every exposed setting record off a connected, proven pad."""
    if label not in LABELS:
        raise ValueError(f"label must be one of {', '.join(LABELS)}")
    verdict = await pad.assessment()
    if not verdict.may_read():
        # the client would refuse each read anyway; say why once, before any is sent
        raise PermissionError(verdict.reason)
    info = await pad.device_info()
    caps = await pad.capabilities()
    sources = await pad.changekey_sources() if caps.changekey else []
    context = {
        "product": verdict.product, "status": verdict.status,
        "vid": info.vid, "pid": info.pid, "firmware": info.version, "mode": info.mode,
        "rawGeneration": info.raw_generation, "identity": info.record,
        "descriptor": caps.raw, "changekeySources": [f"{s:02X}" for s in sources],
    }
    records: dict[str, Record] = {}
    for name, opcode, page, slot in RECORDS:
        offered = getattr(caps, page)
        # the macro byte is one bit per slot (0x0F on the X20: M1 to M4)
        if not offered or (slot is not None and not offered >> (slot - 1) & 1):
            records[name] = Record(int(opcode), "not offered")
            continue
        records[name] = await _read_record(pad, int(opcode), slot)
    return State(label, datetime.now(timezone.utc).isoformat(timespec="seconds"),
                 context, records, note)


# -- comparing -------------------------------------------------------------

CONTEXT_KEYS = (("vid", "vendor id"), ("pid", "product id"), ("firmware", "firmware"),
                ("mode", "operating mode"), ("rawGeneration", "protocol generation"),
                ("descriptor", "descriptor"), ("changekeySources", "remappable button order"))

CURVE_FIELDS = ("inner deadzone", "outer deadzone (stored subtracted)", "point 1 input",
                "point 1 output", "point 2 input", "point 2 output",
                "flags (invert X 1, invert Y 2, swap 4)")


def _key(code: int) -> str:
    try:
        return p.Key(code).name
    except ValueError:
        return f"0x{code:02X}"


def field_name(record: str, offset: int, sources: list[int]) -> str:
    """What one byte of a record is, where that is known."""
    if record in ("stick", "trigger"):
        side = ("left", "right")[offset // p.CHANNEL_SIZE] if offset < 2 * p.CHANNEL_SIZE else "extra"
        return f"{side} {record}: {CURVE_FIELDS[offset % p.CHANNEL_SIZE]}"
    if record == "motor":
        if offset in (0, 1):
            return f"motor {offset + 1} strength"
        if p.SHUTDOWN_OFFSET <= offset < p.SHUTDOWN_OFFSET + p.SHUTDOWN_WIDTH:
            return f"idle shutdown, byte {offset - p.SHUTDOWN_OFFSET + 1} of {p.SHUTDOWN_WIDTH}"
    if record == "changekey" and offset < len(sources):
        return f"what {_key(sources[offset])} sends"
    return "meaning not known"


def _value(record: str, offset: int, value: int) -> str:
    if record == "changekey":
        return "unchanged" if value == 0 else _key(value)
    if record == "motor" and offset in (0, 1):
        return f"{value} ({round(value * 100 / 255)}%)"
    return str(value)


@dataclass
class RecordDiff:
    name: str
    #: "changed", "no change", "not recorded" (missing or incomplete on either
    #: side, so nothing can be said), or "not offered" on both.
    outcome: str
    before: Record
    after: Record
    bytes: list[tuple[int, int, int]] = field(default_factory=list)   # offset, was, now
    fields: list[str] = field(default_factory=list)


@dataclass
class Comparison:
    first: State
    second: State
    #: Why the two states can't be compared; empty when they can.
    void: list[str]
    records: list[RecordDiff]

    @property
    def comparable(self) -> bool:
        return not self.void

    @property
    def changed(self) -> list[RecordDiff]:
        return [r for r in self.records if r.outcome == "changed"]

    def text(self) -> str:
        a, b = self.first, self.second
        lines = [f"{a.label} ({a.taken_at}) -> {b.label} ({b.taken_at})"]
        if self.void:
            lines.append("Inconclusive: the controller's identity or setup differs between the two reads, so they can't be compared.")
            lines += [f"  {why}" for why in self.void]
            return "\n".join(lines)
        for r in self.records:
            if r.outcome == "changed":
                lines.append(f"{r.name}: changed")
                lines += [f"  {line}" for line in r.fields]
            elif r.outcome == "not recorded":
                lines.append(f"{r.name}: not recorded ({r.before.status} / {r.after.status}), so not known")
            elif r.outcome == "no change":
                lines.append(f"{r.name}: no change")
        if not self.changed:
            lines.append("Nothing readable changed.")
        lines.append("This is what the pad held at each read. It doesn't show the messages the app sent, "
                     "and one session doesn't prove the change survives a power-off.")
        return "\n".join(lines)

    def to_json(self) -> dict:
        return {"first": self.first.label, "second": self.second.label,
                "comparable": self.comparable, "void": self.void,
                "records": [{"name": r.name, "outcome": r.outcome,
                             "bytes": [{"offset": o, "was": w, "now": n} for o, w, n in r.bytes],
                             "fields": r.fields} for r in self.records]}


def compare(first: State, second: State) -> Comparison:
    void = []
    for key, what in CONTEXT_KEYS:
        if first.context.get(key) != second.context.get(key):
            void.append(f"The {what} differs: {first.context.get(key)} then {second.context.get(key)}.")
    sources = [int(s, 16) for s in first.context.get("changekeySources", [])]
    diffs = []
    for name in dict.fromkeys([*first.records, *second.records]):
        a = first.records.get(name) or Record(0, "not recorded")
        b = second.records.get(name) or Record(0, "not recorded")
        if a.status == "not offered" and b.status == "not offered":
            diffs.append(RecordDiff(name, "not offered", a, b))
            continue
        if a.status != "read" or b.status != "read":
            diffs.append(RecordDiff(name, "not recorded", a, b))
            continue
        d = RecordDiff(name, "no change", a, b)
        for i in range(max(len(a.data), len(b.data))):
            was = a.data[i] if i < len(a.data) else None
            now = b.data[i] if i < len(b.data) else None
            if was != now:
                d.bytes.append((i, -1 if was is None else was, -1 if now is None else now))
        if len(a.data) != len(b.data):
            d.fields.append(f"the record is {len(a.data)} bytes, then {len(b.data)}")
        for i, was, now in d.bytes:
            if was < 0 or now < 0:
                continue
            d.fields.append(f"byte {i}, {field_name(name, i, sources)}: "
                            f"{_value(name, i, was)} -> {_value(name, i, now)}")
        if name == "motor" and any(p.SHUTDOWN_OFFSET <= i < p.SHUTDOWN_OFFSET + p.SHUTDOWN_WIDTH for i, _, _ in d.bytes):
            try:
                minutes = [p.parse_shutdown(r.data) for r in (a, b)]
                d.fields.append("idle shutdown: " + " -> ".join("never" if m is None else f"{m} min" for m in minutes))
            except ValueError:
                pass
        if d.bytes or len(a.data) != len(b.data):
            d.outcome = "changed"
        diffs.append(d)
    return Comparison(first, second, void, diffs)
