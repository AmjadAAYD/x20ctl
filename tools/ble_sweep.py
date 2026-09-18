"""Issue a batch of queries over one connection and tabulate the replies.

Reconnecting per query is slow, so this opens a single link and walks a range of
index values, or a set of opcodes, correlating each reply to its request by the
serial byte. Read-only opcodes only.

Usage:
    python ble_sweep.py 98:B6:ED:E3:15:C4 HOST_LIGHTING --indices 0-7
    python ble_sweep.py 98:B6:ED:E3:15:C4 --opcodes HOST_LIGHTING,HOST_MENU,HOST_STICK
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from bleak import BleakClient

from x20ctl import protocol as p
from tools.ble_probe import READ_ONLY  # single source of truth for the allowlist

CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

DIM = "\033[2m"
BOLD = "\033[1m"
GREEN = "\033[32m"
RESET = "\033[0m"


def parse_range(text: str) -> list[int]:
    out: list[int] = []
    for part in text.split(","):
        if "-" in part:
            lo, hi = part.split("-")
            out.extend(range(int(lo), int(hi) + 1))
        else:
            out.append(int(part))
    return out


async def sweep(address: str, requests: list[tuple[p.Op, bytes]], wait: float) -> None:
    serial = p.SerialCounter()
    replies: dict[int, list[bytes]] = {}
    pending: dict[int, tuple[p.Op, bytes]] = {}

    def on_notify(_sender, data: bytearray) -> None:
        raw = bytes(data)
        try:
            pkt = p.parse(raw)
        except Exception:
            return
        replies.setdefault(pkt.serial, []).append(raw)

    print(f"connecting to {address}, {len(requests)} queries\n")
    async with BleakClient(address) as client:
        await client.start_notify(CONFIG_NOTIFY, on_notify)

        for opcode, payload in requests:
            s = serial.next()
            pending[s] = (opcode, payload)
            packet = p.build(opcode, payload, serial=s)
            await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
            await asyncio.sleep(wait)

        await asyncio.sleep(1.5)
        await client.stop_notify(CONFIG_NOTIFY)

    print(f"{BOLD}{'opcode':<16}{'sent':>10}   reply payload{RESET}")
    print("-" * 78)
    seen_payloads: dict[bytes, str] = {}
    for s, (opcode, sent) in pending.items():
        got = replies.get(s, [])
        label = f"{opcode.name:<16}{sent.hex() or '-':>10}"
        if not got:
            print(f"{label}   {DIM}(no reply){RESET}")
            continue
        for raw in got:
            pkt = p.parse(raw)
            flag = "" if pkt.crc_valid else "  <BAD CRC>"
            payload = pkt.payload
            dup = seen_payloads.get(payload)
            note = f"   {DIM}same as {dup}{RESET}" if dup else ""
            if not dup:
                seen_payloads[payload] = f"{opcode.name}[{sent.hex()}]"
            print(f"{label}   {payload.hex(' ')}{flag}{note}")
            label = " " * 26

    distinct = len(seen_payloads)
    print(f"\n{distinct} distinct payload(s) across {len(pending)} queries")
    if distinct == 1 and len(pending) > 1:
        print(f"{DIM}every index returned the same bytes, so this opcode's index"
              f" byte may not select what we assumed{RESET}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address")
    parser.add_argument("opcode", nargs="?")
    parser.add_argument("--indices", default="0", help="e.g. 0-7 or 0,2,5")
    parser.add_argument("--payloads", help="explicit hex payloads, e.g. 0001,0002,0003")
    parser.add_argument("--opcodes", help="comma-separated opcode names, index 0 each")
    parser.add_argument("--wait", type=float, default=0.6, help="seconds between queries")
    args = parser.parse_args()

    requests: list[tuple[p.Op, bytes]] = []
    if args.opcodes:
        for name in args.opcodes.split(","):
            try:
                op = p.Op[name.strip()]
            except KeyError:
                raise SystemExit(f"unknown opcode {name!r}")
            requests.append((op, b"\x00"))
    elif args.opcode:
        try:
            op = p.Op[args.opcode]
        except KeyError:
            raise SystemExit(f"unknown opcode {args.opcode!r}")
        if args.payloads:
            # explicit payloads, hex, comma separated, e.g. 0001,0002,0003
            requests = [(op, bytes.fromhex(h.strip())) for h in args.payloads.split(",")]
        else:
            requests = [(op, bytes([i])) for i in parse_range(args.indices)]
    else:
        parser.error("give an opcode or --opcodes")

    for op, _ in requests:
        if op not in READ_ONLY:
            raise SystemExit(f"{op.name} is not read-only; this tool only queries")

    asyncio.run(sweep(args.address, requests, args.wait))


if __name__ == "__main__":
    main()
