"""Prove, or disprove, that the pad accepts a stick or trigger write.

`verify_write.py` proved the write *framing* using a changekey no-op. It could
not prove that a settings write changes anything, because the value it wrote was
the value already there. This goes further, on one channel, and puts it back.

    read baseline
    1. no-op    write the record back byte for byte      safety check
    2. change   inner deadzone + 2 on the left channel   the actual question
    3. restore  write the baseline back                  leave no trace

Step 1 alone can never answer the question: an ignored write and an accepted one
both leave the record unchanged. Only a value that differs tells them apart,
which is what step 2 is for. Two is the smallest change the record can carry,
and on a 0-100 scale it is well under anything a hand would notice.

Every step reads the record back and prints it, so the evidence is the pad's own
reply rather than an assumption about what the write did. If the restore fails,
that's reported loudly along with the bytes needed to put it right by hand.

Usage:
    python tools/verify_curve_write.py sticks
    python tools/verify_curve_write.py triggers --address 98:B6:ED:E3:15:C4
    python tools/verify_curve_write.py sticks --read-only
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from bleak.exc import BleakError

from x20ctl import protocol as p
from x20ctl import ui
from x20ctl.client import CURVE_KINDS, X20, find_controller
from x20ctl.config import load_address
from x20ctl.diagnose import diagnose

PROBE_STEP = 2


def record(channels: list[p.Curve]) -> str:
    return b"".join(channel.to_bytes() for channel in channels).hex(" ")


async def run(kind: str, address: str, read_only: bool) -> int:
    _read_op, write_op, _scale = CURVE_KINDS[kind]

    async with X20(address) as pad:
        caps = await pad.capabilities()
        exposed = caps.sticks if kind == "sticks" else caps.triggers
        print(ui.header(kind.title(), f"capability descriptor 0x{exposed:02x}"))

        baseline = await pad.curves(kind)
        print(ui.field("baseline", record(baseline)))
        for side, curve in zip(("left", "right"), baseline):
            print(ui.field(side, str(curve)))
        if read_only:
            print(ui.label("\n  --read-only, nothing written\n"))
            return 0

        # -- 1 -----------------------------------------------------------
        # Sent by hand rather than through set_curves so the pad's reply to the
        # write itself is visible, not only the state afterwards.
        payload = p.build_curves(baseline)
        serial = p.save_button_serial(slot=0, counter=pad._next_save_counter())
        packet = p.build(
            write_op, payload, serial=serial,
            length_field=p.host_length(len(payload), pad._next_host_counter()))
        print(ui.header("1. no-op", f"{len(packet)} bytes on the wire"))
        reply = await pad._send(packet, serial)
        print(ui.field("reply", str(reply) if reply else "none within the timeout"))
        await asyncio.sleep(0.4)

        after = await pad.curves(kind)
        print(ui.field("read back", record(after)))
        if after == baseline:
            print(ui.ok("  the record is intact"))
        else:
            print(ui.bad("  the record changed, which a no-op must not do"))
            print(ui.label(f"  restore by hand with: x20 curve {kind} "
                           f'--restore "{record(baseline)}"'))
            return 2

        # -- 2 -----------------------------------------------------------
        probe = baseline[0].inner_deadzone + PROBE_STEP
        wanted = [baseline[0].with_deadzones(inner=probe)] + baseline[1:]
        print(ui.header("2. change",
                        f"left inner deadzone {baseline[0].inner_deadzone} "
                        f"to {probe}"))
        reported = await pad.set_curves(kind, wanted)
        print(ui.field("sent", record(wanted)))
        print(ui.field("read back", record(reported)))
        took = reported[0].inner_deadzone == probe
        print(ui.ok(f"  the pad accepts {kind} writes") if took
              else ui.warn("  the pad ignored it and kept its own value"))
        print(ui.field("right untouched", str(reported[1:] == baseline[1:])))

        # -- 3 -----------------------------------------------------------
        print(ui.header("3. restore"))
        restored = await pad.set_curves(kind, baseline)
        print(ui.field("read back", record(restored)))
        if restored == baseline:
            print(ui.ok("  back exactly as found\n"))
            return 0 if took else 1
        print(ui.bad("  NOT restored"))
        print(ui.label(f'  put it back with: x20 curve {kind} '
                       f'--restore "{record(baseline)}"\n'))
        return 2


async def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kind", choices=sorted(CURVE_KINDS))
    parser.add_argument("--address", help="defaults to the remembered address")
    parser.add_argument("--read-only", action="store_true",
                        help="show the record and stop")
    args = parser.parse_args()

    address = args.address or load_address() or await find_controller()
    if not address:
        print(ui.bad("\n  no controller found"))
        print(ui.label("  turn it on, press a button to wake it, and check "
                       "Bluetooth is enabled\n"))
        return 1
    try:
        return await run(args.kind, address, args.read_only)
    except BleakError as exc:
        # A remembered address the pad has since gone quiet on is the usual way
        # here, and a bleak traceback says nothing about what to do about it.
        finding = diagnose(exc)
        print(ui.bad(f"\n  {finding.headline}"))
        print(ui.label(f"  {finding.advice}\n"))
        return 1


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
