"""Verify that the controller accepts a write, using a provable no-op.

The only packet this tool can send is the changekey write the official app
itself emits when the user changed nothing:

    CodeHelper.writeHostChangeKeyData(new int[]{0}, 0)   ->  payload [0]

A count of zero means "no remaps". The pad already reports zero remaps, so this
can't alter its configuration. What it does prove is that our write framing,
bit-packed length field and serial encoding are accepted.

The procedure is read, write, read, compare. If the two reads differ, the write
wasn't the no-op we believed and that's reported loudly.

Usage:
    python verify_write.py 98:B6:ED:E3:15:C4 --dry-run
    python verify_write.py 98:B6:ED:E3:15:C4
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from bleak import BleakClient

from x20ctl import protocol as p

CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

BOLD = "\033[1m"
GREEN = "\033[32m"
RED = "\033[31m"
DIM = "\033[2m"
RESET = "\033[0m"

# The app keeps two independent counters: countHost feeds the length field and
# starts at 8, decrementing before first use; countSaveButtons feeds the serial
# and starts at 0, incrementing before first use. Replicated exactly.
FIRST_HOST_COUNTER = 7
FIRST_SERIAL_COUNTER = 1


def build_noop() -> bytes:
    payload = bytes([0])
    return p.build(
        p.Op.WRITE_CHANGEKEY,
        payload,
        serial=p.save_button_serial(slot=0, counter=FIRST_SERIAL_COUNTER),
        length_field=p.host_length(len(payload), FIRST_HOST_COUNTER),
        nonce=0x5A,
    )


def describe(packet: bytes) -> None:
    plain = p.unscramble(packet)
    print(f"{BOLD}the only packet this tool will send{RESET}")
    print(f"    plain      {plain.hex(' ')}")
    print(f"    on wire    {packet.hex(' ')}")
    print(f"    opcode     0x{plain[0]:02X}  WRITE_CHANGEKEY")
    print(f"    length     0x{plain[1]:02X}  -> counter {plain[1] >> 5}, length {plain[1] & 0x1F}")
    print(f"    serial     0x{plain[2]:02X}")
    print(f"    nonce      0x{plain[3]:02X}")
    print(f"    payload    {plain[4:-1].hex(' ')}   <- count 0, meaning no remaps")
    print(f"    crc        0x{plain[-1]:02X}  valid={p.parse(packet).crc_valid}")


async def read_changekey(client: BleakClient, serial_gen: p.SerialCounter,
                         replies: dict) -> bytes | None:
    s = serial_gen.next()
    replies.clear()
    await client.write_gatt_char(
        CONFIG_WRITE, p.build_query(p.Op.HOST_CHANGEKEY, 0, serial=s), response=True
    )
    await asyncio.sleep(2.5)
    got = replies.get(s)
    return p.parse(got[0]).payload if got else None


async def run(address: str) -> None:
    packet = build_noop()
    describe(packet)

    replies: dict[int, list[bytes]] = {}

    def on_notify(_sender, data: bytearray) -> None:
        raw = bytes(data)
        try:
            pkt = p.parse(raw)
        except Exception:
            return
        replies.setdefault(pkt.serial, []).append(raw)

    serial_gen = p.SerialCounter()
    print(f"\nconnecting to {address}\n")
    async with BleakClient(address) as client:
        await client.start_notify(CONFIG_NOTIFY, on_notify)

        before = await read_changekey(client, serial_gen, replies)
        print(f"  before   HOST_CHANGEKEY -> {before.hex(' ') if before else '(no reply)'}")

        replies.clear()
        await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
        print(f"  {BOLD}write sent{RESET}")
        await asyncio.sleep(2.5)
        acks = [p.parse(r) for rs in replies.values() for r in rs]
        if acks:
            for ack in acks:
                status = "ok" if ack.crc_valid else "BAD CRC"
                print(f"  ack      {ack!r}  [{status}]")
        else:
            print(f"  {DIM}no acknowledgement{RESET}")

        after = await read_changekey(client, serial_gen, replies)
        print(f"  after    HOST_CHANGEKEY -> {after.hex(' ') if after else '(no reply)'}")

        await client.stop_notify(CONFIG_NOTIFY)

    print()
    if before is None or after is None:
        print(f"{RED}inconclusive: a read did not come back{RESET}")
    elif before == after:
        print(f"{GREEN}{BOLD}state unchanged, as intended.{RESET}")
        print("the write path is exercised; whether the pad accepted or ignored it")
        print("is told by the acknowledgement above, not by this comparison.")
    else:
        print(f"{RED}{BOLD}STATE CHANGED. This was supposed to be a no-op.{RESET}")
        print(f"  before {before.hex(' ')}")
        print(f"  after  {after.hex(' ')}")
        print("recover with: hold C for 5 seconds")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address")
    parser.add_argument("--dry-run", action="store_true", help="print the packet, send nothing")
    args = parser.parse_args()

    if args.dry_run:
        describe(build_noop())
        print(f"\n{DIM}dry run: nothing transmitted{RESET}")
        return
    asyncio.run(run(args.address))


if __name__ == "__main__":
    main()
