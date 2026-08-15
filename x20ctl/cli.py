"""Command line interface.

    x20 scan                          find the controller
    x20 status                        everything the pad reports
    x20 vibration 60                  set motor strength
    x20 macro M1 "A+B"                write a macro
    x20 macro M1 --clear              empty a slot
    x20 macro --read                  read all four slots off the pad

    x20 remap                         show current button remappings
    x20 remap A:B X:Y                 swap A/B and X/Y
    x20 remap A:T                     put T on the A button

    x20 curve                         show stick and trigger response
    x20 curve sticks --inner 5        set the inner deadzone
    x20 curve triggers --linear       straighten the response

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

from bleak.exc import BleakError

from . import __version__
from . import protocol as p
from . import transport
from . import ui
from .client import CURVE_KINDS, X20, ControllerError, NotSupported, find_controller
from .diagnose import diagnose
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
    print(ui.field("x20ctl", __version__))
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


# -- response curves ------------------------------------------------------

SIDES = ("left", "right")


def print_curves(kind: str, channels: list[p.Curve]) -> None:
    print(ui.header(kind.title(), "deadzones and response curve"))
    for index, curve in enumerate(channels):
        side = SIDES[index] if index < len(SIDES) else f"channel {index}"
        shape = "linear" if curve.is_linear else "curved"
        flags = ", ".join(name for name, on in (
            ("invert X", curve.invert_x), ("invert Y", curve.invert_y),
            ("sticks swapped", curve.swapped)) if on)
        print(ui.field(side, f"deadzone {curve.inner_deadzone} to "
                             f"{curve.outer_deadzone} of {curve.max_progress}"))
        print(ui.field("", f"points {curve.point1} {curve.point2}, {shape}"))
        if flags:
            print(ui.field("", flags))
        print(ui.plot(curve.shape(steps=25), p.CURVE_AXIS_MAX))
        print(ui.field("", curve.to_bytes().hex(" ")))
        print()


def _parse_points(text: str) -> tuple[tuple[int, int], tuple[int, int]]:
    """Two control points, written as "82,133 229,235"."""
    try:
        pairs = [tuple(int(v) for v in part.split(",")) for part in text.split()]
    except ValueError:
        raise SystemExit(ui.bad('  points look like: --points "82,133 229,235"')) from None
    if len(pairs) != 2 or any(len(pair) != 2 for pair in pairs):
        raise SystemExit(ui.bad('  two points are needed: --points "82,133 229,235"'))
    return pairs[0], pairs[1]


def _edited(kind: str, channels: list[p.Curve], args) -> list[p.Curve]:
    """Apply the command line's changes, leaving untouched sides alone."""
    if args.restore:
        scale = CURVE_KINDS[kind][2]
        raw = bytes.fromhex(args.restore.replace(",", " "))
        if len(raw) != len(channels) * p.CHANNEL_SIZE:
            raise SystemExit(ui.bad(
                f"  --restore wants {len(channels) * p.CHANNEL_SIZE} bytes for "
                f"{kind}, got {len(raw)}"))
        return [p.Curve.parse(raw[i:i + p.CHANNEL_SIZE], scale)
                for i in range(0, len(raw), p.CHANNEL_SIZE)]

    out = []
    for index, curve in enumerate(channels):
        side = SIDES[index] if index < len(SIDES) else str(index)
        if args.side != "both" and args.side != side:
            out.append(curve)
            continue
        if args.linear:
            curve = curve.straightened()
        if args.points:
            curve = curve.with_points(*_parse_points(args.points))
        if args.inner is not None or args.outer is not None:
            curve = curve.with_deadzones(args.inner, args.outer)
        out.append(curve.with_flags(invert_x=args.invert_x, invert_y=args.invert_y,
                                    swapped=args.swap))
    return out


async def cmd_curve(args) -> None:
    changes = (args.inner, args.outer, args.points, args.invert_x, args.invert_y,
               args.swap, args.restore)
    editing = args.linear or any(change is not None for change in changes)
    if editing and not args.kind:
        raise SystemExit(ui.bad(
            "  say which to change: x20 curve sticks --inner 5"))

    address = await resolve(args.address)
    kinds = [args.kind] if args.kind else list(CURVE_KINDS)

    async with X20(address) as pad:
        for kind in kinds:
            channels = await pad.curves(kind)
            if not editing:
                print_curves(kind, channels)
                continue

            before = p.build_curves(channels)[1:]
            wanted = _edited(kind, channels, args)
            print(ui.label(f"\n  writing {kind}..."))
            reported = await pad.set_curves(kind, wanted)
            print_curves(kind, reported)

            if p.build_curves(reported) == p.build_curves(wanted):
                print(ui.ok("  written, and the pad reads back exactly that"))
            else:
                print(ui.warn("  the pad reports something other than what was sent"))
                print(ui.field("sent", p.build_curves(wanted)[1:].hex(" ")))
            print(ui.label(f"  to put it back: x20 curve {kind} "
                           f'--restore "{before.hex(" ")}"'))
    print()


async def cmd_macro(args) -> None:
    if args.read:
        await _cmd_macro_read(args)
        return
    if not args.slot:
        raise SystemExit(ui.bad(f"which slot? one of {', '.join(SLOTS)}"))

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


async def _cmd_macro_read(args) -> None:
    """Read every macro slot off the pad and print it in editor syntax."""
    address = await resolve(args.address)
    async with X20(address) as pad:
        macros = await pad.macros()

    print(ui.header("Macros on the controller"))
    for slot in SLOTS:
        program = macros.get(SLOTS.index(slot) + 1)
        print(ui.slot_line(slot, program.as_sequence() if program else None))
    print()


def button_name(code: int) -> str:
    try:
        return p.Key(code).name
    except ValueError:
        return f"0x{code:02x}"


def button_code(text: str) -> int | None:
    token = text.strip().upper().replace("-", "_")
    try:
        return int(p.Key[token])
    except KeyError:
        return None


async def cmd_remap(args) -> None:
    """Show or set button remappings, as SOURCE:TARGET pairs."""
    address = await resolve(args.address)

    async with X20(address) as pad:
        sources = await pad.changekey_sources()
        current = await pad.remappings(sources)

        if not args.pairs:
            print(ui.header("Button remapping"))
            if not current:
                print(ui.label("  no remappings configured"))
            else:
                for src, tgt in sorted(current.items()):
                    print(f"  {ui.label(button_name(src))} -> "
                          f"{ui.value(button_name(tgt))}")
            print()
            print(ui.label("  sources: " + ", ".join(button_name(s) for s in sources)))
            print(ui.label("  targets can also be C and T, which are not sources"))
            print(ui.label('  set one with: x20 remap A:B\n'))
            return

        changes: dict[int, int] = {}
        for pair in args.pairs:
            if ":" not in pair:
                raise SystemExit(ui.bad(f"'{pair}' is not SOURCE:TARGET"))
            src_text, tgt_text = pair.split(":", 1)
            src = button_code(src_text)
            tgt = button_code(tgt_text)
            if src is None:
                raise SystemExit(ui.bad(f"unknown source button '{src_text}'"))
            if tgt is None:
                raise SystemExit(ui.bad(f"unknown target button '{tgt_text}'"))
            if src not in sources:
                raise SystemExit(ui.bad(
                    f"'{src_text}' cannot be remapped on this pad. Sources are: "
                    + ", ".join(button_name(s) for s in sources)))
            changes[src] = tgt

        reported = await pad.set_remapping(changes)

    print(ui.header("Button remapping written"))
    if not reported:
        print(ui.label("  the pad reports no remappings\n"))
        return
    for src, tgt in sorted(reported.items()):
        print(f"  {ui.label(button_name(src))} -> {ui.value(button_name(tgt))}")
    print(ui.label("\n  what is shown is what the pad reports back, not what "
                   "was asked for\n"))


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
    parser.add_argument("--version", action="version", version=f"x20ctl {__version__}")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("scan", help="find the controller").set_defaults(fn=cmd_scan)
    sub.add_parser("status", help="show everything the pad reports").set_defaults(fn=cmd_status)

    vib = sub.add_parser("vibration", help="read or set motor strength")
    vib.add_argument("percent", nargs="?", type=int, help="0-100; omit to read")
    vib.set_defaults(fn=cmd_vibration)

    crv = sub.add_parser("curve", help="read or set stick and trigger response")
    crv.add_argument("kind", nargs="?", choices=sorted(CURVE_KINDS),
                     help="omit to show both")
    crv.add_argument("--side", choices=["left", "right", "both"], default="both")
    crv.add_argument("--inner", type=int, help="inner deadzone, where travel begins")
    crv.add_argument("--outer", type=int, help="outer deadzone, where it saturates")
    crv.add_argument("--linear", action="store_true",
                     help="put the response back on the diagonal")
    crv.add_argument("--points", help='control points: "82,133 229,235"')
    crv.add_argument("--invert-x", action=argparse.BooleanOptionalAction)
    crv.add_argument("--invert-y", action=argparse.BooleanOptionalAction)
    crv.add_argument("--swap", action=argparse.BooleanOptionalAction,
                     help="swap the two sticks")
    crv.add_argument("--restore", help="write a whole record back, as hex")
    crv.set_defaults(fn=cmd_curve)

    mac = sub.add_parser("macro", help="write a macro to a slot")
    mac.add_argument("slot", nargs="?", help="M1, M2, M3 or M4")
    mac.add_argument("keys", nargs="?", default="",
                     help="',' separates steps, '+' joins keys: \"A,B\" or \"A+B\"")
    mac.add_argument("--clear", action="store_true")
    mac.add_argument("--read", action="store_true",
                     help="read every slot back off the pad")
    mac.add_argument("--hold", type=int, default=100)
    mac.add_argument("--gap", type=int, default=60)
    mac.add_argument("--loop", type=int, default=0,
                     help="loop interval ms; 0 fires once")
    mac.set_defaults(fn=cmd_macro)

    remap = sub.add_parser("remap", help="show or set button remappings")
    remap.add_argument("pairs", nargs="*",
                       help="SOURCE:TARGET pairs, e.g. A:B X:Y")
    remap.set_defaults(fn=cmd_remap)

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
    except BleakError as exc:
        # A pad that is off or out of range is the commonest way to end up here,
        # and a bleak traceback says nothing about what to do about it.
        finding = diagnose(exc)
        print(ui.bad(f"\n  {finding.headline}"))
        print(ui.label(f"  {finding.advice}\n"))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
