"""Collect a compatibility report for a controller, so it can be shared.

Everything here decoded from one EasySMX X20. Whether it applies to any other
controller is unknown, and that is exactly what this is for: point it at a pad,
paste the output into an issue, and the differences become visible.

    python tools/report.py

What it does
------------

**It only reads.** Every opcode it sends is on the read-only allowlist in
`ble_probe.py`, which is the same list the other query tools use. There is no
write path in this file, no firmware access, and nothing that changes a setting.
If the pad is not a KeyLinker device it will say so and stop.

What ends up in the report
--------------------------

Only things about the controller:

- what it advertises over Bluetooth, and which GATT services it exposes
- vendor and product ids, firmware version, device name
- the capability descriptor, which is how the pad states what it supports
- the raw bytes of each readable settings record
- whether Windows sees a gamepad on XInput, and the button layout it reports

MAC addresses are shortened to their vendor prefix, since the rest identifies
your specific unit and is no use for compatibility. Pass `--full-address` if you
would rather include it. Nothing about your PC, your account, or your files is
read or written.
"""

from __future__ import annotations

import argparse
import asyncio
import os
import platform
import sys
from datetime import datetime, timezone

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from bleak import BleakClient, BleakScanner
from bleak.exc import BleakError

from x20ctl import __version__, protocol as p
from x20ctl.client import CONFIG_SERVICE, X20
from x20ctl.diagnose import diagnose
from tools.ble_probe import READ_ONLY

# Records worth dumping whole. Anything the pad refuses simply reports as no
# reply, which is itself a useful difference between models.
RECORDS = [
    ("stick", p.Op.HOST_STICK), ("trigger", p.Op.HOST_TRIGGER),
    ("motor", p.Op.HOST_MOTOR), ("turbo", p.Op.HOST_TURBO),
    ("macro", p.Op.HOST_MACRO), ("changekey", p.Op.HOST_CHANGEKEY),
    ("lighting", p.Op.HOST_LIGHTING), ("button_mode", p.Op.READ_BUTTON_MODE),
    ("two_in_one", p.Op.TWO_IN_ONE_STATE), ("guid", p.Op.HOST_GUID),
]

MENUS = [("menu", p.MenuKind.MENU), ("all_keys", p.MenuKind.GAMEPAD_ALL_KEYS),
         ("changekey_support", p.MenuKind.CHANGEKEY_SUPPORT),
         ("turbo_support", p.MenuKind.TURBO_SUPPORT),
         ("macro_support", p.MenuKind.MACRO_SUPPORT)]


def short_address(address: str, full: bool) -> str:
    """Vendor prefix only. The rest identifies one particular unit."""
    if full:
        return address
    parts = address.replace("-", ":").split(":")
    return ":".join(parts[:3]) + ":xx:xx:xx" if len(parts) >= 3 else "hidden"


class Report:
    def __init__(self) -> None:
        self.lines: list[str] = []

    def add(self, text: str = "") -> None:
        self.lines.append(text)
        print(text)

    def section(self, title: str) -> None:
        self.add()
        self.add(f"## {title}")
        self.add()


async def scan(report: Report, full: bool, seconds: float) -> list:
    report.section("Bluetooth scan")
    found: dict = {}

    def seen(device, adv) -> None:
        found.setdefault(device.address, (device, adv))

    scanner = BleakScanner(detection_callback=seen)
    await scanner.start()
    try:
        await asyncio.sleep(seconds)
    finally:
        await scanner.stop()

    if not found:
        report.add("nothing advertising. Is Bluetooth on and the pad awake?")
        return []

    # Only controllers are listed. A Bluetooth scan sees whatever is nearby,
    # including other people's phones and watches, and this report is meant to
    # be pasted in public. Everything else is a count.
    candidates, others = [], 0
    for address, (device, adv) in sorted(found.items()):
        name = adv.local_name or device.name or ""
        uuids = [u.lower() for u in (adv.service_uuids or [])]
        keylinker = CONFIG_SERVICE in uuids
        looks_like_a_pad = keylinker or any(
            word in name.lower()
            for word in ("xpert", "gamepad", "controller", "easysmx", "pad"))
        if not looks_like_a_pad:
            others += 1
            continue
        candidates.append((address, name))
        report.add(f"- {short_address(address, full)}  name={name!r}"
                   f"  services={len(uuids)}"
                   + ("  <- KeyLinker config service" if keylinker else ""))

    if others:
        report.add(f"({others} other Bluetooth device(s) nearby, not listed)")
    if not candidates:
        report.add("no controller-looking device found")
    return candidates


async def gatt_table(report: Report, address: str) -> None:
    report.section("GATT services")
    async with BleakClient(address) as client:
        for service in client.services:
            report.add(f"- {service.uuid}  {service.description}")
            for ch in service.characteristics:
                props = ",".join(ch.properties)
                report.add(f"    {ch.uuid}  [{props}]")


async def interrogate(report: Report, address: str) -> None:
    async with X20(address) as pad:
        report.section("Device")
        info = await pad.device_info()
        report.add(f"vid/pid          {info.vid} / {info.pid}")
        report.add(f"firmware         {info.version}")
        report.add(f"device_id        {info.device_id}")
        report.add(f"model            {info.model}")
        report.add(f"sensor           {info.sensor}")
        report.add(f"reported name    {await pad.name()!r}")
        report.add(f"product guess    {p.product_name('', info.vid, info.pid)!r}")

        report.section("Capabilities")
        try:
            caps = await pad.capabilities()
            for field in ("sticks", "triggers", "motors", "turbo", "macros",
                          "changekey", "lighting", "eq", "nfc", "sensor"):
                value = getattr(caps, field)
                report.add(f"{field:<12} 0x{value:02x}"
                           + ("" if value else "   not exposed"))
        except Exception as exc:
            report.add(f"capability query failed: {exc}")

        report.section("HOST_MENU sub-queries")
        for label, kind in MENUS:
            body = await pad.read_body(p.Op.HOST_MENU, bytes([0, kind]))
            report.add(f"{label:<18} "
                       + (body.data.hex(" ") if body else "no reply"))

        report.section("Settings records, raw")
        for label, op in RECORDS:
            assert op in READ_ONLY, f"{op} is not on the read-only list"
            body = await pad.read_body(op, bytes([0]))
            report.add(f"{label:<12} "
                       + (body.data.hex(" ") if body else "no reply"))

        report.section("Decoded, if the layout matches an X20")
        for label, op, scale in (("sticks", p.Op.HOST_STICK, p.STICK_MAX_PROGRESS),
                                 ("triggers", p.Op.HOST_TRIGGER, p.TRIGGER_MAX_PROGRESS)):
            body = await pad.read_body(op, bytes([0]))
            if not body:
                report.add(f"{label:<10} no reply")
                continue
            try:
                for side, curve in zip(("left", "right"), p.parse_curves(body, scale)):
                    report.add(f"{label:<10} {side:<6} {curve}")
            except ValueError as exc:
                report.add(f"{label:<10} does not parse as an X20 record: {exc}")

        battery = await pad.battery()
        report.add()
        report.add(f"battery      {battery if battery else 'no reply'}")


def xinput_section(report: Report) -> None:
    report.section("XInput, what Windows sees")
    try:
        from x20ctl.input import XInputReader
    except Exception as exc:
        report.add(f"could not load the XInput reader: {exc}")
        return
    reader = XInputReader()
    if not reader.available:
        report.add("XInput not available on this system")
        return
    state = reader.poll()
    if state is None:
        report.add("no controller on any XInput slot")
        return
    report.add(f"slot         {state.slot}")
    report.add(f"sticks       L{state.left_stick}  R{state.right_stick}")
    report.add(f"triggers     L{state.left_trigger}  R{state.right_trigger}")
    report.add(f"buttons now  {', '.join(state.pressed_names) or 'none held'}")


async def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--address", help="skip the scan and use this one")
    parser.add_argument("--seconds", type=float, default=8.0, help="scan duration")
    parser.add_argument("--full-address", action="store_true",
                        help="include the whole MAC rather than the vendor prefix")
    parser.add_argument("--out", default="controller-report.txt")
    args = parser.parse_args()

    report = Report()
    report.add("# Controller compatibility report")
    report.add()
    report.add(f"generated    {datetime.now(timezone.utc).isoformat(timespec='seconds')}")
    report.add(f"x20ctl       {__version__}")
    report.add(f"python       {platform.python_version()} on {platform.system()} "
               f"{platform.release()}")

    xinput_section(report)

    address = args.address
    if not address:
        # The scan is the most likely thing to fail on a tester's machine, and
        # a traceback here used to kill the run before the file was written --
        # losing the XInput section too. Report the reason and carry on.
        try:
            candidates = await scan(report, args.full_address, args.seconds)
        except BleakError as exc:
            # Distinct from "scanned and found nothing": the radio never
            # listened, so say that rather than blaming the pad.
            finding = diagnose(exc)
            report.add()
            report.add(f"Bluetooth scan could not run: {finding.headline}.")
            report.add(finding.advice)
            report.add()
            report.add("The XInput section above is still worth sharing; only "
                       "the Bluetooth sections are missing.")
            candidates = None

        if candidates is None:
            address = None
        elif not candidates:
            report.add()
            report.add("No pad exposing the KeyLinker configuration service was "
                       "found, so there is nothing further to read. The XInput "
                       "section above is still worth sharing.")
            address = None
        else:
            address, name = candidates[0]
            report.add()
            report.add(f"using {short_address(address, args.full_address)} ({name})")

    if address:
        try:
            await gatt_table(report, address)
            await interrogate(report, address)
        except BleakError as exc:
            report.add(f"\nconnection failed: {exc}")
        except Exception as exc:
            report.add(f"\nstopped early: {type(exc).__name__}: {exc}")

    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write("\n".join(report.lines) + "\n")
    print(f"\nwritten to {args.out} - paste it into an issue at "
          "https://github.com/AmjadAAYD/x20ctl/issues")
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
