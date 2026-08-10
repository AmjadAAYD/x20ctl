"""Write a simple macro to one of the M1-M4 slots.

Only digital button presses are supported. Analog stick entries occupy nibbles
in the step mask and are deliberately not handled.

The macro is expressed as a sequence of button names. Each becomes a press step
followed by a release step, so `--keys A,B` produces A down, A up, B down, B up.

    python set_macro.py <addr> --slot 1 --keys A
    python set_macro.py <addr> --slot 1 --keys A,B --hold 50
    python set_macro.py <addr> --slot 1 --clear
    python set_macro.py <addr> --slot 1 --dry-run --keys A

Slots are given as 1-4 for M1-M4 and converted to the zero-based wire value.

Recovery: hold C on the pad for 5 seconds to factory reset.
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


def build_payload(key_names: list[str], hold_ms: int, gap_ms: int,
                  loop_ms: int = 0) -> bytes:
    steps: list[p.MacroStep] = []
    for name in key_names:
        try:
            key = p.Key[name.strip().upper()]
        except KeyError:
            valid = ", ".join(k.name for k in p.MACRO_MASK_BIT)
            raise SystemExit(f"unknown key {name!r}. supported: {valid}")
        steps.append(p.MacroStep(mask=p.mask_for([key]), duration_ms=hold_ms))
        # Must be released(), not mask=0. A zero mask commands both sticks.
        steps.append(p.MacroStep.released(gap_ms))
    # loop_ms is the loop INTERVAL, not a total duration. Zero means the
    # macro fires once. Anything else makes it repeat forever.
    return p.build_macro_payload(steps, loop_interval_ms=loop_ms)


def describe(packets: list[bytes], payload: bytes, slot0: int, key_names: list[str]) -> None:
    plural = "s" if len(packets) > 1 else ""
    print(f"{BOLD}{len(packets)} packet{plural}{RESET}")
    for i, packet in enumerate(packets):
        plain = p.unscramble(packet)
        print(f"  chunk {i}")
        print(f"    plain    {plain.hex(' ')}")
        print(f"    wire     {packet.hex(' ')}")
        print(f"    length   0x{plain[1]:02X}  -> slot {plain[1] >> 5} (M{(plain[1] >> 5) + 1}), "
              f"length {plain[1] & 0x1F}")
        print(f"    serial   0x{plain[2]:02X}  -> chunk {plain[2] >> 3 & 0x0F}")
        print(f"    size     {len(packet)} bytes (limit {p.MAX_PACKET})  "
              f"crc valid={p.parse(packet).crc_valid}")
    print(f"\n{BOLD}macro payload{RESET}  {payload.hex(' ')}")
    print(f"    header   {payload[:3].hex(' ')}")
    for i in range(3, len(payload), p.MACRO_STEP_SIZE):
        step = p.MacroStep.parse(payload[i:i + p.MACRO_STEP_SIZE])
        pressed = [k.name for k, b in p.MACRO_MASK_BIT.items() if step.mask >> b & 1]
        label = "+".join(pressed) if pressed else "(released)"
        print(f"    step     {payload[i:i + p.MACRO_STEP_SIZE].hex(' ')}  "
              f"{label}, {step.duration_ms}ms")
    if key_names:
        print(f"\n    sequence: {' then '.join(key_names)}  ->  M{slot0 + 1}")


async def run(address: str, slot0: int, payload: bytes, key_names: list[str]) -> None:
    packets = p.build_macro_writes(payload, slot=slot0)
    describe(packets, payload, slot0, key_names)

    replies: dict[int, list[bytes]] = {}

    def on_notify(_sender, data: bytearray) -> None:
        raw = bytes(data)
        try:
            pkt = p.parse(raw)
        except Exception:
            return
        replies.setdefault(pkt.serial, []).append(raw)

    serial_gen = p.SerialCounter()

    async def read_macro(client) -> bytes | None:
        s = serial_gen.next()
        replies.clear()
        await client.write_gatt_char(
            CONFIG_WRITE, p.build(p.Op.READ_MACRO, bytes([slot0, 0]), serial=s), response=True
        )
        await asyncio.sleep(2.5)
        got = replies.get(s)
        return p.parse(got[0]).payload if got else None

    print(f"\nconnecting to {address}\n")
    async with BleakClient(address) as client:
        await client.start_notify(CONFIG_NOTIFY, on_notify)

        before = await read_macro(client)
        print(f"  before   {before.hex(' ') if before else '(no reply)'}")

        replies.clear()
        for i, packet in enumerate(packets):
            await client.write_gatt_char(CONFIG_WRITE, packet, response=True)
            print(f"  {BOLD}chunk {i} sent{RESET} ({len(packet)} bytes)")
            await asyncio.sleep(0.3)
        await asyncio.sleep(2.5)
        for rs in replies.values():
            for r in rs:
                print(f"  ack      {p.parse(r)!r}")
        if not replies:
            print(f"  {DIM}no acknowledgement{RESET}")

        after = await read_macro(client)
        print(f"  after    {after.hex(' ') if after else '(no reply)'}")
        await client.stop_notify(CONFIG_NOTIFY)

    print()
    if before is not None and after is not None and before != after:
        print(f"{GREEN}{BOLD}the macro slot changed.{RESET}")
        print(f"  now press M{slot0 + 1} on a controller test page to see what it does")
    else:
        print(f"{RED}slot reads the same as before.{RESET}")
        print("the write may have been rejected, or macro state may not be")
        print("readable through this opcode. nothing was damaged either way.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address")
    parser.add_argument("--slot", type=int, default=1, help="1-4 for M1-M4")
    parser.add_argument("--keys", help="comma separated, e.g. A or A,B")
    parser.add_argument("--clear", action="store_true", help="write an empty macro")
    parser.add_argument("--hold", type=int, default=50, help="press duration ms")
    parser.add_argument("--gap", type=int, default=50, help="release duration ms")
    parser.add_argument("--loop-ms", type=int, default=0,
                        help="loop interval in ms; 0 fires once, >0 repeats forever")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not 1 <= args.slot <= 4:
        raise SystemExit("slot must be 1-4")
    if not args.keys and not args.clear:
        raise SystemExit("give --keys or --clear")

    slot0 = args.slot - 1
    names = [] if args.clear else [k for k in args.keys.split(",") if k.strip()]
    # Clearing sends a bare [0], which is what writeMacroData emits when the
    # step list is empty. An empty header is not the same thing.
    payload = p.MACRO_CLEAR if args.clear else \
        build_payload(names, args.hold, args.gap, args.loop_ms)

    if args.dry_run:
        describe(p.build_macro_writes(payload, slot=slot0), payload, slot0, names)
        print(f"\n{DIM}dry run: nothing transmitted{RESET}")
        return

    asyncio.run(run(args.address, slot0, payload, names))


if __name__ == "__main__":
    main()
