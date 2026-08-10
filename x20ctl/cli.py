"""Command line interface.

    x20 scan                          find the controller
    x20 status                        everything the pad reports
    x20 vibration 60                  set motor strength
    x20 macro M1 "A+B"                write a macro
    x20 macro M1 --clear              empty a slot

    x20 profile list
    x20 profile show  "Save file 1"
    x20 profile apply "Save file 1"
    x20 profile set   "Save file 1" M1 "A+B" --hold 100
    x20 profile capture "Save file 1"   snapshot the pad's current vibration

The address is remembered after the first successful scan, so most commands
need no arguments.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys

from . import protocol as p
from . import transport
from . import ui
from .client import X20, ControllerError, NotSupported, find_controller
from .config import load_address, save_address
from .profiles import MacroSpec, Profile, ProfileStore, SLOTS


async def resolve(explicit: str | None) -> str:
    if explicit:
        return explicit
    remembered = load_address()
    if remembered:
        return remembered
    print(ui.label("no controller remembered, scanning..."))
    found = await find_controller()
    if not found:
        raise SystemExit(ui.bad(
            "no controller found. Turn it on and make sure Bluetooth is enabled."))
    save_address(found)
    print(ui.ok(f"found {found}, remembered for next time"))
    return found


# -- commands -------------------------------------------------------------

async def cmd_scan(args) -> None:
    print(ui.header("Scanning", "listening for the configuration peripheral"))
    address = await find_controller()
    if not address:
        print(ui.bad("  nothing found"))
        print(ui.label("  check the pad is on and Bluetooth is enabled"))
        return
    save_address(address)
    print(ui.field("address", address))
    print(ui.ok("\n  remembered for next time"))


async def cmd_status(args) -> None:
    address = await resolve(args.address)
    async with X20(address) as pad:
        snap = await pad.snapshot()

    print(ui.header("Controller", snap.device.vid + " / " + snap.device.pid))
    print(ui.field("controller", p.product_name(
        snap.name, snap.device.vid, snap.device.pid)))
    print(ui.field("reports as", snap.name or "unknown"))
    print(ui.field("firmware", snap.device.version))
    print(ui.field("address", address))
    if snap.battery is not None:
        state = "charging" if snap.battery.charging else "on battery"
        print(ui.field("battery", f"{snap.battery} · {state}"))

    link = transport.detect()
    if link.connected:
        print(ui.field("playing over", str(link)))

    caps = snap.capabilities
    print(ui.header("Configurable"))
    rows = [
        ("sticks", caps.has_left_stick and caps.has_right_stick, "left and right"),
        ("triggers", caps.has_left_trigger and caps.has_right_trigger, "left and right"),
        ("vibration", bool(caps.motors), f"{caps.motor_count} motors"),
        ("macros", bool(caps.macros), ", ".join(caps.macro_slots) or "none"),
        ("remapping", bool(caps.changekey), "supported"),
        ("lighting", bool(caps.lighting), f"{caps.lighting_zones} zones"),
        ("turbo", bool(caps.turbo), "supported"),
        ("gyro", bool(caps.sensor), "supported"),
    ]
    for name, available, detail in rows:
        mark = ui.ok("yes") if available else ui.label("not exposed")
        extra = f"  {ui.label(detail)}" if available else ""
        print(f"  {ui.label(name.ljust(12))}{mark}{extra}")

    print(ui.header("Vibration"))
    left, right = snap.vibration
    print(f"  {ui.label('motor 1'.ljust(10))}{ui.bar(left)}")
    print(f"  {ui.label('motor 2'.ljust(10))}{ui.bar(right)}")

    for title, channels, scale in (
            ("Sticks", snap.sticks, p.STICK_MAX_PROGRESS),
            ("Triggers", snap.triggers, p.TRIGGER_MAX_PROGRESS)):
        if not channels:
            continue
        print(ui.header(title, "response curve"))
        for side, data in zip(("left", "right"), channels):
            try:
                curve = p.Curve.parse(data, scale)
            except ValueError:
                print(ui.field(side, data.hex(" ")))
                continue
            shape = "linear" if curve.is_linear else "curved"
            print(ui.field(side, f"deadzone {curve.inner_deadzone}, "
                                 f"points {curve.point1} {curve.point2}, {shape}"))
    print()


async def cmd_vibration(args) -> None:
    address = await resolve(args.address)
    async with X20(address) as pad:
        if args.percent is None:
            left, right = await pad.vibration()
            print(ui.header("Vibration"))
            print(f"  {ui.label('motor 1'.ljust(10))}{ui.bar(left)}")
            print(f"  {ui.label('motor 2'.ljust(10))}{ui.bar(right)}\n")
            return
        print(ui.label(f"setting vibration to {args.percent}%..."))
        left, right = await pad.set_vibration(args.percent)
        print(ui.header("Vibration"))
        print(f"  {ui.label('motor 1'.ljust(10))}{ui.bar(left)}")
        print(f"  {ui.label('motor 2'.ljust(10))}{ui.bar(right)}")
        print(ui.ok("\n  written and read back\n"))


async def cmd_macro(args) -> None:
    slot_name = args.slot.upper()
    if slot_name not in SLOTS:
        raise SystemExit(ui.bad(f"slot must be one of {', '.join(SLOTS)}"))
    index = SLOTS.index(slot_name) + 1

    address = await resolve(args.address)
    async with X20(address) as pad:
        if args.clear:
            await pad.clear_macro(index)
            print(ui.ok(f"\n  {slot_name} cleared\n"))
            return
        spec = MacroSpec(keys=args.keys, hold_ms=args.hold,
                         gap_ms=args.gap, loop_ms=args.loop)
        spec.validate()
        await pad.set_macro(index, spec.keys, hold_ms=spec.hold_ms,
                            gap_ms=spec.gap_ms, loop_ms=spec.loop_ms)

    print(ui.header("Macro written"))
    print(ui.slot_line(slot_name, spec.describe()))
    if spec.loop_ms:
        print(ui.warn("\n  this macro loops and will repeat until interrupted"))
        print(ui.label("  press another macro button to stop it"))
    print(ui.label(f"\n  press {slot_name} to test\n"))


# -- profiles -------------------------------------------------------------

def print_profile(profile: Profile) -> None:
    print(ui.header(profile.name, profile.updated or ""))
    if profile.vibration is not None:
        print(f"  {ui.label('vibration'.ljust(10))}{ui.bar(profile.vibration)}")
        print()
    for slot in SLOTS:
        spec = profile.macros.get(slot)
        print(ui.slot_line(slot, spec.describe() if spec else None))
    print()


async def cmd_profile_list(args) -> None:
    store = ProfileStore()
    profiles = store.list()
    print(ui.header("Save files", store.directory))
    if not profiles:
        print(ui.label("  none yet"))
        print(ui.label('  create one with: x20 profile set "Save file 1" M1 "A+B"\n'))
        return
    for profile in profiles:
        filled = sum(1 for s in SLOTS if profile.macros.get(s))
        detail = f"{filled}/4 macros"
        if profile.vibration is not None:
            detail += f", vibration {profile.vibration}%"
        print(f"  {ui.accent('▸')} {ui.value(profile.name.ljust(22))}{ui.label(detail)}")
    print()


async def cmd_profile_show(args) -> None:
    print_profile(ProfileStore().load(args.name))


async def cmd_profile_set(args) -> None:
    store = ProfileStore()
    try:
        profile = store.load(args.name)
    except FileNotFoundError:
        profile = Profile(name=args.name)

    slot_name = args.slot.upper()
    if slot_name not in SLOTS:
        raise SystemExit(ui.bad(f"slot must be one of {', '.join(SLOTS)}"))

    if args.clear:
        profile.macros[slot_name] = None
    else:
        profile.macros[slot_name] = MacroSpec(
            keys=args.keys, hold_ms=args.hold, gap_ms=args.gap, loop_ms=args.loop)
    if args.vibration is not None:
        profile.vibration = args.vibration

    path = store.save(profile)
    print_profile(profile)
    print(ui.label(f"  saved to {path}\n"))


async def cmd_profile_apply(args) -> None:
    store = ProfileStore()
    profile = store.load(args.name)
    address = await resolve(args.address)

    print(ui.header(f"Applying {profile.name}"))
    async with X20(address) as pad:
        await profile.apply(pad, on_step=lambda line: print(ui.bullet(line)))
    print(ui.ok("\n  done\n"))


async def cmd_profile_capture(args) -> None:
    """Snapshot what the pad currently reports into a profile."""
    address = await resolve(args.address)
    store = ProfileStore()
    try:
        profile = store.load(args.name)
    except FileNotFoundError:
        profile = Profile(name=args.name)

    async with X20(address) as pad:
        left, _right = await pad.vibration()
    profile.vibration = left
    store.save(profile)

    print_profile(profile)
    print(ui.label("  captured the pad's current vibration"))
    print(ui.label("  macros cannot be read back, so they are not captured\n"))


async def cmd_profile_delete(args) -> None:
    ProfileStore().delete(args.name)
    print(ui.ok(f"\n  deleted {args.name}\n"))


# -- entry point ----------------------------------------------------------

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="x20", description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--address", help="BLE address; remembered after first scan")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("scan", help="find the controller").set_defaults(fn=cmd_scan)
    sub.add_parser("status", help="show everything the pad reports").set_defaults(fn=cmd_status)

    vib = sub.add_parser("vibration", help="read or set motor strength")
    vib.add_argument("percent", nargs="?", type=int, help="0-100; omit to read")
    vib.set_defaults(fn=cmd_vibration)

    mac = sub.add_parser("macro", help="write a macro to a slot")
    mac.add_argument("slot", help="M1, M2, M3 or M4")
    mac.add_argument("keys", nargs="?", default="",
                     help="',' separates steps, '+' joins keys: \"A,B\" or \"A+B\"")
    mac.add_argument("--clear", action="store_true")
    mac.add_argument("--hold", type=int, default=100)
    mac.add_argument("--gap", type=int, default=60)
    mac.add_argument("--loop", type=int, default=0,
                     help="loop interval ms; 0 fires once")
    mac.set_defaults(fn=cmd_macro)

    prof = sub.add_parser("profile", help="save files")
    psub = prof.add_subparsers(dest="sub", required=True)

    psub.add_parser("list").set_defaults(fn=cmd_profile_list)

    show = psub.add_parser("show")
    show.add_argument("name")
    show.set_defaults(fn=cmd_profile_show)

    pset = psub.add_parser("set", help="set one slot in a save file")
    pset.add_argument("name")
    pset.add_argument("slot")
    pset.add_argument("keys", nargs="?", default="")
    pset.add_argument("--clear", action="store_true")
    pset.add_argument("--hold", type=int, default=100)
    pset.add_argument("--gap", type=int, default=60)
    pset.add_argument("--loop", type=int, default=0)
    pset.add_argument("--vibration", type=int)
    pset.set_defaults(fn=cmd_profile_set)

    papply = psub.add_parser("apply")
    papply.add_argument("name")
    papply.set_defaults(fn=cmd_profile_apply)

    pcap = psub.add_parser("capture", help="snapshot current pad settings")
    pcap.add_argument("name")
    pcap.set_defaults(fn=cmd_profile_capture)

    pdel = psub.add_parser("delete")
    pdel.add_argument("name")
    pdel.set_defaults(fn=cmd_profile_delete)

    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        asyncio.run(args.fn(args))
    except KeyboardInterrupt:
        print(ui.label("\ninterrupted"))
        return 130
    except FileNotFoundError as exc:
        print(ui.bad(f"\n  {exc}\n"))
        return 1
    except NotSupported as exc:
        print(ui.warn(f"\n  {exc}\n"))
        return 1
    except (ControllerError, ValueError) as exc:
        print(ui.bad(f"\n  {exc}\n"))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
