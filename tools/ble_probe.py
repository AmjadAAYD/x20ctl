"""Query a setting, log the reply, and diff it against the previous reading.

This is the core of the mapping loop: read a setting, change one thing on the
controller, read it again, and see exactly which bytes moved. Every reading is
appended to captures/probe_log.jsonl so the history is reproducible.

Only read-only opcodes are accepted. This tool cannot write settings.

Usage:
    python ble_probe.py 98:B6:ED:E3:15:C4 HOST_LIGHTING --label "baseline"
    ... change one setting on the pad ...
    python ble_probe.py 98:B6:ED:E3:15:C4 HOST_LIGHTING --label "solid green"

    python ble_probe.py --history HOST_LIGHTING      replay the log, no hardware
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
import time
from datetime import datetime, timezone

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from bleak import BleakClient

from x20ctl import protocol as p

CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

LOG = os.path.join(os.path.dirname(__file__), "..", "captures", "probe_log.jsonl")

READ_ONLY = {
    p.Op.READ_NAME, p.Op.READ_VID_PID_VERSION, p.Op.HOST_GUID,
    p.Op.READ_BUTTON_MODE, p.Op.LOAD_BUTTON, p.Op.READ_3D,
    p.Op.READ_PRESS_GUN, p.Op.READ_TURBO, p.Op.READ_SLIDE_SCREEN,
    p.Op.READ_MACRO, p.Op.TWO_IN_ONE_STATE, p.Op.HOST_MENU,
    p.Op.HOST_STICK, p.Op.HOST_TRIGGER, p.Op.HOST_MOTOR,
    p.Op.HOST_TURBO, p.Op.HOST_MACRO, p.Op.HOST_CHANGEKEY, p.Op.HOST_LIGHTING,
}

RED = "\033[31m"
GREEN = "\033[32m"
DIM = "\033[2m"
BOLD = "\033[1m"
RESET = "\033[0m"


def load_log() -> list[dict]:
    if not os.path.exists(LOG):
        return []
    with open(LOG, encoding="utf-8") as fh:
        return [json.loads(line) for line in fh if line.strip()]


def append_log(entry: dict) -> None:
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    with open(LOG, "a", encoding="utf-8") as fh:
        fh.write(json.dumps(entry) + "\n")


def render_diff(before: bytes, after: bytes, label_a: str, label_b: str) -> None:
    print(f"\n{BOLD}diff: {label_a!r} -> {label_b!r}{RESET}")
    if before == after:
        print(f"  {DIM}identical, nothing changed{RESET}")
        return

    width = max(len(before), len(after))
    idx_row, a_row, b_row = [], [], []
    changed = []
    for i in range(width):
        x = before[i] if i < len(before) else None
        y = after[i] if i < len(after) else None
        mark = x != y
        if mark:
            changed.append(i)
        idx_row.append(f"{i:>3}")
        a_row.append(("--" if x is None else f"{x:02x}").rjust(3))
        cell = ("--" if y is None else f"{y:02x}").rjust(3)
        b_row.append(f"{GREEN}{BOLD}{cell}{RESET}" if mark else cell)

    print("  offset " + "".join(idx_row))
    print("  before " + "".join(a_row))
    print("  after  " + "".join(b_row))
    print(f"\n  {len(changed)} byte(s) changed at offset(s): {changed}")
    for i in changed:
        x = before[i] if i < len(before) else None
        y = after[i] if i < len(after) else None
        xs = "--" if x is None else f"0x{x:02x} ({x:>3})"
        ys = "--" if y is None else f"0x{y:02x} ({y:>3})"
        print(f"    [{i:>3}]  {xs}  ->  {GREEN}{ys}{RESET}")


async def probe(address: str, opcode: p.Op, index: int | None, label: str) -> None:
    serial = p.SerialCounter()
    packet = (
        p.build_query(opcode, index, serial=serial.next())
        if index is not None
        else p.build(opcode, b"", serial=serial.next())
    )

    replies: list[bytes] = []

    def on_notify(_sender, data: bytearray) -> None:
        replies.append(bytes(data))

    print(f"querying {opcode.name} (0x{opcode:02X})"
          + (f" index={index}" if index is not None else "")
          + f"  label={label!r}")

    async with BleakClient(address) as client:
        await client.start_notify(CONFIG_NOTIFY, on_notify)
        await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
        await asyncio.sleep(3.0)
        await client.stop_notify(CONFIG_NOTIFY)

    if not replies:
        print("no reply")
        return

    pkt = p.parse(replies[0])
    if not pkt.crc_valid:
        print(f"{RED}warning: reply failed CRC, not logging{RESET}")
        return

    payload = pkt.payload
    print(f"payload ({len(payload)} bytes): {payload.hex(' ')}")

    history = [e for e in load_log()
               if e["opcode"] == int(opcode) and e.get("index") == index]

    append_log({
        "time": datetime.now(timezone.utc).isoformat(),
        "label": label,
        "opcode": int(opcode),
        "opcode_name": opcode.name,
        "index": index,
        "payload": payload.hex(),
        "raw_reply": replies[0].hex(),
        "extra_replies": [r.hex() for r in replies[1:]],
    })

    if history:
        previous = history[-1]
        render_diff(bytes.fromhex(previous["payload"]), payload,
                    previous["label"], label)
    else:
        print(f"{DIM}first reading for this opcode; change one setting and run again{RESET}")


def show_history(opcode_name: str) -> None:
    entries = [e for e in load_log() if e["opcode_name"] == opcode_name]
    if not entries:
        print(f"no readings logged for {opcode_name}")
        return
    print(f"{len(entries)} reading(s) for {opcode_name}:\n")
    for i, entry in enumerate(entries):
        payload = bytes.fromhex(entry["payload"])
        print(f"  [{i}] {entry['time'][:19]}  {entry['label']!r}")
        print(f"      {payload.hex(' ')}")
    for a, b in zip(entries, entries[1:]):
        render_diff(bytes.fromhex(a["payload"]), bytes.fromhex(b["payload"]),
                    a["label"], b["label"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address", nargs="?")
    parser.add_argument("opcode", nargs="?")
    parser.add_argument("--index", type=int, default=0)
    parser.add_argument("--label", default=lambda: time.strftime("%H:%M:%S"))
    parser.add_argument("--history", metavar="OPCODE", help="replay the log offline")
    args = parser.parse_args()

    if args.history:
        return show_history(args.history)
    if not args.address or not args.opcode:
        parser.error("address and opcode are required unless --history is used")

    try:
        opcode = p.Op[args.opcode]
    except KeyError:
        raise SystemExit(f"unknown opcode {args.opcode!r}")
    if opcode not in READ_ONLY:
        raise SystemExit(f"{opcode.name} is not read-only; this tool only queries")

    label = args.label if isinstance(args.label, str) else time.strftime("%H:%M:%S")
    asyncio.run(probe(args.address, opcode, args.index, label))


if __name__ == "__main__":
    main()
