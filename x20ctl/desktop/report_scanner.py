"""Constrained, offline diagnostics for one explicitly selected BLE address."""

from __future__ import annotations

import argparse
import asyncio
import json
import re
import sys
from pathlib import Path

from bleak import BleakClient
from bleak.exc import BleakError

from x20ctl.desktop._scanner_identity import SCANNER_VERSION

STANDARD = {
    "00002a24-0000-1000-8000-00805f9b34fb": "modelNumber",
    "00002a26-0000-1000-8000-00805f9b34fb": "firmwareRevision",
    "00002a27-0000-1000-8000-00805f9b34fb": "hardwareRevision",
    "00002a28-0000-1000-8000-00805f9b34fb": "softwareRevision",
    "00002a29-0000-1000-8000-00805f9b34fb": "manufacturerName",
}
INFO_SERVICE = "0000180a-0000-1000-8000-00805f9b34fb"
BATTERY_SERVICE = "0000180f-0000-1000-8000-00805f9b34fb"
BATTERY_CHAR = "00002a19-0000-1000-8000-00805f9b34fb"


def status(code: int, label: str) -> int:
    print(json.dumps({"schemaVersion": 1, "status": label, "scannerVersion": SCANNER_VERSION}, separators=(",", ":")))
    return code


async def collect(address: str, name: str) -> dict:
    values: dict[str, str | int] = {}
    async with BleakClient(address, timeout=12) as client:
        for service in client.services:
            service_id = service.uuid.lower()
            if service_id not in {INFO_SERVICE, BATTERY_SERVICE}:
                continue
            for char in service.characteristics:
                char_id = char.uuid.lower()
                label = STANDARD.get(char_id) if service_id == INFO_SERVICE else (
                    "batteryLevel" if char_id == BATTERY_CHAR else None
                )
                if not label or "read" not in char.properties:
                    continue
                try:
                    raw = bytes(await client.read_gatt_char(char))[:128]
                    values[label] = raw[0] if label == "batteryLevel" and len(raw) == 1 else raw.decode("utf-8", "replace").rstrip("\x00")[:120]
                except Exception:
                    # A single unavailable characteristic must not broaden discovery.
                    pass
    return {
        "controllerName": name,
        "connectionType": "Bluetooth LE",
        "vid": None,
        "pid": None,
        "standardGatt": values,
        "unavailable": ["USB descriptors", "HID report descriptor", "input captures"],
    }


def main() -> int:
    class StrictParser(argparse.ArgumentParser):
        def error(self, message):
            raise ValueError(message)

    parser = StrictParser(add_help=False)
    parser.add_argument("--address")
    parser.add_argument("--name")
    parser.add_argument("--output")
    try:
        args = parser.parse_args()
        if not args.address or not re.fullmatch(r"(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}|[0-9A-Fa-f-]{36}", args.address):
            return status(20, "invalid_arguments")
        if not args.name or len(args.name) > 120 or any(ord(c) < 32 for c in args.name):
            return status(20, "invalid_arguments")
        output = Path(args.output)
        if not output.is_dir() or output.is_symlink() or any(output.iterdir()):
            return status(20, "invalid_arguments")
        try:
            report = asyncio.run(collect(args.address, args.name))
        except BleakError:
            return status(10, "controller_unavailable")
        except Exception:
            return status(30, "internal_failure")
        if not report["standardGatt"]:
            return status(11, "capability_unavailable")
        (output / "device.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        (output / "scanner-version.txt").write_text(SCANNER_VERSION + "\n", encoding="utf-8")
        (output / "README.txt").write_text(
            "Read-only standard Bluetooth LE diagnostics for the selected controller. "
            "USB/HID association and input captures were unavailable; no serial number was collected.\n",
            encoding="utf-8",
        )
        return status(0, "complete")
    except ValueError:
        return status(20, "invalid_arguments")
    except OSError:
        return status(30, "internal_failure")


if __name__ == "__main__":
    sys.exit(main())
