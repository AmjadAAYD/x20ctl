"""Connect to a BLE peripheral and print its GATT table. Discovery only.

By default this performs service discovery and nothing else: no characteristic is
read, and nothing is ever written. Pass --read to additionally read the
characteristics that advertise the read property, which is still non-destructive,
but every OTA service in OTA_SERVICES is skipped unconditionally.

"Known" is the operative word there. Until 2026-08-16 this file guarded a single
OTA UUID while the X20 exposes a different one, so --read read an OTA
characteristic on an X20 while this docstring promised it did not. A pad exposing
OTA under a UUID still not listed would be read the same way.

Usage:
    python ble_enum.py 98:B6:ED:E3:15:C4
    python ble_enum.py 98:B6:ED:E3:15:C4 --read
"""

from __future__ import annotations

import argparse
import asyncio

from bleak import BleakClient

CONFIG_SERVICE = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"
OTA_SERVICE = "2de516f0-50f5-45d5-a1b2-c3565de543ae"

# KeyLinker ships two OTA implementations and a pad may expose either. The X20
# exposes the ff12 one, from fota/bluetooth/BleOtaManager, NOT the stock Telink
# service in util/b.java. Guarding only one of them meant --read happily read an
# OTA characteristic on an X20 while this file's docstring promised it did not.
TELINK_OTA_SERVICE = "00010203-0405-0607-0809-0a0b0c0d1912"
FF12_OTA_SERVICE = "0000ff12-0000-1000-8000-00805f9b34fb"

OTA_SERVICES = {OTA_SERVICE, TELINK_OTA_SERVICE, FF12_OTA_SERVICE}

KNOWN = {
    CONFIG_SERVICE: "KeyLinker configuration service",
    CONFIG_WRITE: "config write",
    CONFIG_NOTIFY: "config read/notify",
    OTA_SERVICE: "OTA service -- DO NOT TOUCH",
    TELINK_OTA_SERVICE: "Telink OTA service -- DO NOT TOUCH",
    FF12_OTA_SERVICE: "OTA service (BleOtaManager) -- DO NOT TOUCH",
    "0000ff13-0000-1000-8000-00805f9b34fb": "OTA control -- DO NOT TOUCH",
    "0000ff14-0000-1000-8000-00805f9b34fb": "OTA dataIn -- DO NOT TOUCH",
    "0000ff15-0000-1000-8000-00805f9b34fb": "OTA dataOut -- DO NOT TOUCH",
    "0000180f-0000-1000-8000-00805f9b34fb": "Battery service",
    "00002a19-0000-1000-8000-00805f9b34fb": "Battery level",
    "0000180a-0000-1000-8000-00805f9b34fb": "Device Information",
    "00001812-0000-1000-8000-00805f9b34fb": "HID over GATT",
}

BOLD = "\033[1m"
GREEN = "\033[32m"
RED = "\033[31m"
RESET = "\033[0m"


async def enumerate_device(address: str, do_read: bool) -> None:
    print(f"connecting to {address} (discovery only, no writes)\n")
    async with BleakClient(address) as client:
        print(f"connected: {client.is_connected}\n")

        found_config = False
        for service in client.services:
            uuid = service.uuid.lower()
            label = KNOWN.get(uuid, "")
            colour = ""
            if uuid == CONFIG_SERVICE:
                colour = GREEN + BOLD
                found_config = True
            elif uuid in OTA_SERVICES:
                colour = RED + BOLD

            print(f"{colour}service {service.uuid}{RESET}")
            if label:
                print(f"    {colour}{label}{RESET}")

            for char in service.characteristics:
                props = ",".join(char.properties)
                note = KNOWN.get(char.uuid.lower(), "")
                suffix = f"   <- {note}" if note else ""
                print(f"    char {char.uuid}  [{props}]{suffix}")

                for desc in char.descriptors:
                    print(f"        descriptor {desc.uuid}")

                if uuid in OTA_SERVICES:
                    print("        (not read: OTA characteristic)")
                elif do_read and "read" in char.properties:
                    try:
                        value = await client.read_gatt_char(char)
                        printable = value.decode("utf-8", "replace").strip("\x00")
                        print(f"        value: {value.hex(' ')}"
                              + (f"   ascii {printable!r}" if printable.isprintable() and printable else ""))
                    except Exception as exc:
                        print(f"        value: <unreadable: {exc}>")
            print()

        if found_config:
            print(f"{GREEN}{BOLD}CONFIRMED: the KeyLinker configuration service is present.{RESET}")
        else:
            print(f"{RED}The configuration service was not found on this peripheral.{RESET}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("address", help="BLE MAC address")
    parser.add_argument("--read", action="store_true",
                        help="also read readable characteristics (still no writes)")
    args = parser.parse_args()
    asyncio.run(enumerate_device(args.address, args.read))


if __name__ == "__main__":
    main()
