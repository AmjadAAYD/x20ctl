"""Send a KeyLinker query to a controller and print the reply.

Guarded by design: only opcodes on the read-only allowlist can be sent. Anything
that writes settings is refused unless --i-know-what-im-doing is passed, so this
tool cannot casually change the controller's configuration.

Usage:
    python ble_query.py 98:B6:ED:E3:15:C4 READ_VID_PID_VERSION
    python ble_query.py 98:B6:ED:E3:15:C4 HOST_LIGHTING --index 0
    python ble_query.py 98:B6:ED:E3:15:C4 --dry-run READ_NAME
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from bleak import BleakClient

from x20ctl import protocol as p

CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

# Opcodes that only ask the device to report. None of these carry settings.
READ_ONLY = {
    p.Op.READ_NAME,
    p.Op.READ_VID_PID_VERSION,
    p.Op.HOST_GUID,
    p.Op.READ_BUTTON_MODE,
    p.Op.LOAD_BUTTON,
    p.Op.READ_3D,
    p.Op.READ_PRESS_GUN,
    p.Op.READ_TURBO,
    p.Op.READ_SLIDE_SCREEN,
    p.Op.READ_MACRO,
    p.Op.TWO_IN_ONE_STATE,
    p.Op.HOST_MENU,
    p.Op.HOST_STICK,
    p.Op.HOST_TRIGGER,
    p.Op.HOST_MOTOR,
    p.Op.HOST_TURBO,
    p.Op.HOST_MACRO,
    p.Op.HOST_CHANGEKEY,
    p.Op.HOST_LIGHTING,
}

BOLD = "\033[1m"
GREEN = "\033[32m"
DIM = "\033[2m"
RESET = "\033[0m"


def show(label: str, raw: bytes) -> None:
    plain = p.unscramble(raw)
    pkt = p.parse(raw)
    print(f"{BOLD}{label}{RESET}")
    print(f"    wire   {raw.hex(' ')}")
    print(f"    plain  {plain.hex(' ')}")
    print(f"    {pkt!r}")
    if pkt.payload:
        printable = "".join(chr(b) if 32 <= b < 127 else "." for b in pkt.payload)
        print(f"    payload {pkt.payload.hex(' ')}   ascii {printable!r}")


async def run(address: str, opcode: p.Op, index: int | None, dry_run: bool) -> None:
    serial = p.SerialCounter()
    packet = (
        p.build_query(opcode, index, serial=serial.next())
        if index is not None
        else p.build(opcode, b"", serial=serial.next())
    )

    show(f"OUT  {opcode.name} (0x{opcode:02X})", packet)
    if dry_run:
        print(f"\n{DIM}dry run: nothing was transmitted{RESET}")
        return

    replies: list[tuple[float, bytes]] = []
    started = time.monotonic()

    def on_notify(_sender, data: bytearray) -> None:
        replies.append((time.monotonic() - started, bytes(data)))

    print(f"\nconnecting to {address} ...")
    async with BleakClient(address) as client:
        print(f"connected: {client.is_connected}")
        await client.start_notify(CONFIG_NOTIFY, on_notify)
        print(f"listening on {CONFIG_NOTIFY[:8]}...")

        await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
        print(f"sent {len(packet)} bytes, waiting 3s for a reply\n")

        await asyncio.sleep(3.0)
        await client.stop_notify(CONFIG_NOTIFY)

    if not replies:
        print("no reply. The pad may need a handshake first, or this opcode may")
        print("not apply to this model. Nothing was changed either way.")
        return

    for elapsed, data in replies:
        show(f"{GREEN}IN   +{elapsed * 1000:.0f}ms{RESET}", data)
        print()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address")
    parser.add_argument("opcode", help="an Op name, e.g. READ_VID_PID_VERSION")
    parser.add_argument("--index", type=int, default=None, help="payload index byte")
    parser.add_argument("--dry-run", action="store_true", help="build and print, transmit nothing")
    parser.add_argument("--i-know-what-im-doing", action="store_true",
                        help="permit a non-read-only opcode")
    args = parser.parse_args()

    try:
        opcode = p.Op[args.opcode]
    except KeyError:
        valid = ", ".join(sorted(o.name for o in READ_ONLY))
        raise SystemExit(f"unknown opcode {args.opcode!r}.\nread-only opcodes: {valid}")

    if opcode not in READ_ONLY and not args.i_know_what_im_doing:
        raise SystemExit(
            f"{opcode.name} is not a read-only opcode and could change settings.\n"
            "Refusing. Pass --i-know-what-im-doing only with a documented reason."
        )

    asyncio.run(run(args.address, opcode, args.index, args.dry_run))


if __name__ == "__main__":
    main()
