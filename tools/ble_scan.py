"""Passively listen for BLE advertisements and flag anything that looks like a
KeyLinker configuration endpoint.

This tool is receive-only. It never connects, pairs, or writes. It just reports
what the air already carries, so it's safe to leave running while the controller
is cycled through its modes.

Usage:
    python ble_scan.py                 scan for 30 seconds
    python ble_scan.py --seconds 120   scan for longer while cycling pad modes
    python ble_scan.py --all           show every device, not just candidates
"""

from __future__ import annotations

import argparse
import asyncio
import time

from bleak import BleakScanner

# From com.pulsenet.inputset.config.AgreementConfig
CONFIG_SERVICE = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
OTA_SERVICE = "2de516f0-50f5-45d5-a1b2-c3565de543ae"

INTERESTING_NAMES = ("xpert", "keylinker", "easysmx", "x20", "gamepad")

# AgreementConfig lists 98:B6:E9 / EC / ED. The X20 on hand is 98:B6:EA, so match
# the shared vendor range rather than the app's hardcoded list.
MAC_PREFIX = "98:B6:E"

RESET = "\033[0m"
BOLD = "\033[1m"
GREEN = "\033[32m"
YELLOW = "\033[33m"


def why_interesting(name: str, address: str, uuids: list[str]) -> list[str]:
    reasons = []
    lowered = (name or "").lower()
    for token in INTERESTING_NAMES:
        if token in lowered:
            reasons.append(f"name contains {token!r}")
    if address.upper().startswith(MAC_PREFIX):
        reasons.append(f"MAC in vendor range {MAC_PREFIX}")
    for uuid in uuids:
        if uuid.lower() == CONFIG_SERVICE:
            reasons.append("ADVERTISES THE CONFIG SERVICE")
        elif uuid.lower() == OTA_SERVICE:
            reasons.append("advertises the OTA service (do not touch)")
    return reasons


async def scan(seconds: int, show_all: bool) -> None:
    seen: dict[str, tuple] = {}
    started = time.monotonic()

    def callback(device, adv) -> None:
        name = adv.local_name or device.name or ""
        uuids = [u.lower() for u in (adv.service_uuids or [])]
        fingerprint = (name, tuple(sorted(uuids)), tuple(sorted(adv.manufacturer_data)))
        if seen.get(device.address) == fingerprint:
            return
        first_time = device.address not in seen
        seen[device.address] = fingerprint

        reasons = why_interesting(name, device.address, uuids)
        if not reasons and not show_all:
            return

        elapsed = time.monotonic() - started
        tag = "NEW" if first_time else "CHANGED"
        header = f"[{elapsed:6.1f}s] {tag:7} {device.address}  rssi {adv.rssi:>4}  {name or '(no name)'}"
        print(f"{BOLD}{GREEN if reasons else ''}{header}{RESET}")

        for reason in reasons:
            marker = GREEN + BOLD if "CONFIG SERVICE" in reason else YELLOW
            print(f"            {marker}-> {reason}{RESET}")
        for uuid in uuids:
            print(f"            service  {uuid}")
        for company, payload in (adv.manufacturer_data or {}).items():
            print(f"            mfr 0x{company:04X}  {payload.hex(' ')}")
        for uuid, payload in (adv.service_data or {}).items():
            print(f"            svcdata  {uuid}  {payload.hex(' ')}")

    scanner = BleakScanner(detection_callback=callback)
    print(f"scanning {seconds}s, receive-only, no connections will be made")
    print("cycle the controller through its modes now (Bluetooth/Switch/Android, pairing mode)\n")

    await scanner.start()
    try:
        await asyncio.sleep(seconds)
    finally:
        await scanner.stop()

    print(f"\n{len(seen)} distinct device(s) heard")
    candidates = {
        addr: fp for addr, fp in seen.items()
        if why_interesting(fp[0], addr, list(fp[1]))
    }
    if candidates:
        print("candidates:")
        for addr, fp in candidates.items():
            print(f"  {addr}  {fp[0] or '(no name)'}")
    else:
        print("no candidates. If the pad never advertised, it is not in a BLE")
        print("configuration mode; try a different pad mode and scan again.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seconds", type=int, default=30)
    parser.add_argument("--all", action="store_true", help="show every device heard")
    args = parser.parse_args()
    asyncio.run(scan(args.seconds, args.all))


if __name__ == "__main__":
    main()
