"""Read, and optionally change, vibration motor strength.

Format from HostActivity's call to writeMotroData:

    [8, m1, m2, m3, m4, b5, b6, b7, b8]

where each motor byte is a UI percentage scaled to 0-255, and bytes 5 to 8 are
copied verbatim out of the preceding read not interpreted. This tool does
the same: it always reads first and reuses those trailing bytes, so it can never
invent a value for a field nobody has decoded.

Usage:
    python set_vibration.py 98:B6:ED:E3:15:C4                   read only
    python set_vibration.py 98:B6:ED:E3:15:C4 --percent 100     read, show, write
    python set_vibration.py 98:B6:ED:E3:15:C4 --restore 4c      write a raw byte back

Recovery if anything looks wrong: hold C on the pad for 5 seconds.
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

FIRST_HOST_COUNTER = 7
FIRST_SERIAL_COUNTER = 1

BOLD = "\033[1m"
GREEN = "\033[32m"
RED = "\033[31m"
DIM = "\033[2m"
RESET = "\033[0m"


def describe_motor(payload: bytes) -> None:
    body = p.unwrap(payload)
    d = body.data
    print(f"    declared {body.declared}, data {d.hex(' ')}")
    for i in range(4):
        if i < len(d):
            pct = round(d[i] * 100 / 255)
            print(f"    motor {i + 1}   0x{d[i]:02x} = {d[i]:>3}  ~{pct}%")
    if len(d) > 4:
        print(f"    trailing {d[4:].hex(' ')}   {DIM}(preserved verbatim){RESET}")


def build_motor_write(current: bytes, m1: int, m2: int) -> bytes:
    """Build a motor write, reusing the undecoded trailing bytes from `current`."""
    body = p.unwrap(current)
    d = bytearray(body.data)
    if len(d) < 8:
        raise ValueError(f"unexpected motor record length {len(d)}")
    d[0], d[1] = m1, m2          # motors 1 and 2
    payload = bytes([body.declared]) + bytes(d)
    return p.build(
        p.Op.WRITE_MOTOR,
        payload,
        serial=p.save_button_serial(slot=0, counter=FIRST_SERIAL_COUNTER),
        length_field=p.host_length(len(payload), FIRST_HOST_COUNTER),
        nonce=0x33,
    )


async def run(address: str, value: int | None) -> None:
    replies: dict[int, list[bytes]] = {}

    def on_notify(_sender, data: bytearray) -> None:
        raw = bytes(data)
        try:
            pkt = p.parse(raw)
        except Exception:
            return
        replies.setdefault(pkt.serial, []).append(raw)

    serial_gen = p.SerialCounter()

    async def read_motor(client) -> bytes | None:
        s = serial_gen.next()
        replies.clear()
        await client.write_gatt_char(
            CONFIG_WRITE, p.build_query(p.Op.HOST_MOTOR, 0, serial=s), response=True
        )
        await asyncio.sleep(2.5)
        got = replies.get(s)
        return p.parse(got[0]).payload if got else None

    async with BleakClient(address) as client:
        await client.start_notify(CONFIG_NOTIFY, on_notify)

        print(f"{BOLD}current{RESET}")
        current = await read_motor(client)
        if current is None:
            print("no reply to the read; aborting")
            return
        print(f"    payload  {current.hex(' ')}")
        describe_motor(current)

        if value is None:
            await client.stop_notify(CONFIG_NOTIFY)
            print(f"\n{DIM}read only, nothing written{RESET}")
            return

        packet = build_motor_write(current, value, value)
        plain = p.unscramble(packet)
        print(f"\n{BOLD}proposed write{RESET}")
        print(f"    plain    {plain.hex(' ')}")
        print(f"    wire     {packet.hex(' ')}")
        print(f"    opcode   0x{plain[0]:02X}  WRITE_MOTOR")
        print(f"    payload  {plain[4:-1].hex(' ')}")
        print(f"    change   motors 1 and 2: 0x{p.unwrap(current).data[0]:02x} -> 0x{value:02x}")

        replies.clear()
        await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
        print(f"\n  {BOLD}write sent{RESET}")
        await asyncio.sleep(2.5)
        for rs in replies.values():
            for r in rs:
                print(f"  ack      {p.parse(r)!r}")
        if not replies:
            print(f"  {DIM}no acknowledgement{RESET}")

        after = await read_motor(client)
        await client.stop_notify(CONFIG_NOTIFY)

    print(f"\n{BOLD}after{RESET}")
    if after is None:
        print("    no reply")
        return
    print(f"    payload  {after.hex(' ')}")
    describe_motor(after)

    if after == current:
        print(f"\n{RED}unchanged. the write was acknowledged but had no effect,{RESET}")
        print("or this setting is not stored where we are reading it.")
    else:
        print(f"\n{GREEN}{BOLD}the setting changed.{RESET}")
        print(f"  before {current.hex(' ')}")
        print(f"  after  {after.hex(' ')}")
        print(f"\n{DIM}restore with: --restore 4c   or hold C for 5 seconds{RESET}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--percent", type=int, help="strength 0-100 for motors 1 and 2")
    group.add_argument("--restore", help="raw hex byte to write back, e.g. 4c")
    args = parser.parse_args()

    value = None
    if args.percent is not None:
        if not 0 <= args.percent <= 100:
            raise SystemExit("percent must be 0-100")
        value = round(args.percent * 255 / 100)
    elif args.restore:
        value = int(args.restore, 16)
        if not 0 <= value <= 255:
            raise SystemExit("restore value must be a single byte")

    asyncio.run(run(args.address, value))


if __name__ == "__main__":
    main()
