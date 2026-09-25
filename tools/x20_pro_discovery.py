"""Read-only X20 Pro discovery for Windows, before its protocol is known.

This tool never sends HID output/feature reports, vendor BLE packets, firmware
commands, or configuration commands. GATT discovery enumerates services and
characteristics; ``--standard`` reads only the standard Battery and Device
Information characteristics listed in STANDARD_READS.

Examples:
    python tools/x20_pro_discovery.py ble-scan --seconds 15 --output captures/pro-scan.json
    python tools/x20_pro_discovery.py ble-gatt ADDRESS --standard --output captures/pro-gatt.json
    python tools/x20_pro_discovery.py hid-list --output captures/pro-hid.json

Capture files can contain hardware identifiers. Keep them local and redact
addresses, serials, and device paths before sharing them publicly.
"""

from __future__ import annotations

import argparse
import asyncio
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


BATTERY_SERVICE = "0000180f-0000-1000-8000-00805f9b34fb"
DEVICE_INFO_SERVICE = "0000180a-0000-1000-8000-00805f9b34fb"
STANDARD_READS = {
    BATTERY_SERVICE: {"00002a19-0000-1000-8000-00805f9b34fb": "battery_level"},
    DEVICE_INFO_SERVICE: {
        "00002a24-0000-1000-8000-00805f9b34fb": "model_number",
        "00002a26-0000-1000-8000-00805f9b34fb": "firmware_revision",
        "00002a27-0000-1000-8000-00805f9b34fb": "hardware_revision",
        "00002a28-0000-1000-8000-00805f9b34fb": "software_revision",
        "00002a29-0000-1000-8000-00805f9b34fb": "manufacturer_name",
    },
}


def decode_standard(name: str, value: bytes) -> Any:
    if name == "battery_level" and len(value) == 1:
        return value[0]
    return value.decode("utf-8", errors="replace").rstrip("\x00")


async def ble_scan(seconds: float) -> dict[str, Any]:
    from bleak import BleakScanner

    if not 1 <= seconds <= 120:
        raise ValueError("scan duration must be between 1 and 120 seconds")
    found: dict[str, dict[str, Any]] = {}

    def seen(device: Any, adv: Any) -> None:
        found[device.address] = {
            "address": device.address,
            "name": adv.local_name or device.name or "",
            "rssi": adv.rssi,
            "service_uuids": sorted(u.lower() for u in adv.service_uuids),
            "manufacturer_data": {
                f"0x{key:04x}": value.hex()
                for key, value in adv.manufacturer_data.items()
            },
            "service_data": {
                key.lower(): value.hex() for key, value in adv.service_data.items()
            },
        }

    async with BleakScanner(detection_callback=seen):
        await asyncio.sleep(seconds)
    return {"mode": "ble-scan", "seconds": seconds, "devices": list(found.values())}


async def ble_gatt(address: str, standard: bool) -> dict[str, Any]:
    from bleak import BleakClient

    services: list[dict[str, Any]] = []
    async with BleakClient(address, timeout=15.0) as client:
        for service in client.services:
            service_uuid = service.uuid.lower()
            chars = []
            for char in service.characteristics:
                char_uuid = char.uuid.lower()
                record: dict[str, Any] = {
                    "uuid": char_uuid,
                    "properties": sorted(char.properties),
                    "descriptors": [desc.uuid.lower() for desc in char.descriptors],
                }
                label = STANDARD_READS.get(service_uuid, {}).get(char_uuid)
                if standard and label and "read" in char.properties:
                    try:
                        value = bytes(await client.read_gatt_char(char))
                        record["standard_name"] = label
                        record["value_hex"] = value.hex()
                        record["value"] = decode_standard(label, value)
                    except Exception as exc:
                        record["read_error"] = str(exc)
                chars.append(record)
            services.append({"uuid": service_uuid, "characteristics": chars})
    return {"mode": "ble-gatt", "address": address, "standard_reads": standard,
            "services": services}


def hid_list(match: str | None, vid: int | None) -> dict[str, Any]:
    # This module opens collections with zero access for descriptor queries.
    # It does not invoke its separate feature-report sweep or input watcher.
    from hid_scan import describe, interface_paths

    devices = []
    for path in interface_paths():
        info = describe(path)
        if info is None:
            continue
        if vid is not None and info["vid"] != vid:
            continue
        if match and match.casefold() not in " ".join(
            (info["path"], info["product"], info["manufacturer"])
        ).casefold():
            continue
        devices.append(info)
    return {"mode": "hid-list", "devices": devices}


def capture_diff(before: Any, after: Any, path: str = "") -> list[dict[str, Any]]:
    """Compare saved captures; timestamps and signal strength are noise."""
    if isinstance(before, dict) and isinstance(after, dict):
        changes = []
        for key in sorted(set(before) | set(after)):
            if key in {"captured_at_utc", "rssi"}:
                continue
            child = f"{path}.{key}" if path else key
            if key not in before or key not in after:
                changes.append({"path": child, "before": before.get(key),
                                "after": after.get(key)})
            else:
                changes.extend(capture_diff(before[key], after[key], child))
        return changes
    if isinstance(before, list) and isinstance(after, list):
        changes = []
        for index in range(max(len(before), len(after))):
            child = f"{path}[{index}]"
            if index >= len(before) or index >= len(after):
                changes.append({"path": child,
                                "before": before[index] if index < len(before) else None,
                                "after": after[index] if index < len(after) else None})
            else:
                changes.extend(capture_diff(before[index], after[index], child))
        return changes
    if before != after:
        return [{"path": path, "before": before, "after": after}]
    return []


def compare_captures(before_path: str, after_path: str) -> dict[str, Any]:
    before = json.loads(Path(before_path).read_text(encoding="utf-8"))
    after = json.loads(Path(after_path).read_text(encoding="utf-8"))
    if before.get("mode") != after.get("mode"):
        raise ValueError("captures must come from the same discovery mode")
    return {"mode": "compare", "source_mode": before["mode"],
            "before_file": before_path, "after_file": after_path,
            "changes": capture_diff(before, after)}


def save_capture(result: dict[str, Any], output: str | None) -> None:
    result["captured_at_utc"] = datetime.now(timezone.utc).isoformat()
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if output:
        path = Path(output)
        if path.exists():
            raise FileExistsError(f"capture already exists: {path}")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(rendered + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    scan = sub.add_parser("ble-scan", help="passively capture BLE advertisements")
    scan.add_argument("--seconds", type=float, default=15.0)
    gatt = sub.add_parser("ble-gatt", help="enumerate one device's GATT table")
    gatt.add_argument("address", help="address printed by ble-scan")
    gatt.add_argument("--standard", action="store_true",
                      help="read only allowlisted standard battery/device-info values")
    hid = sub.add_parser("hid-list", help="read Windows HID descriptors")
    hid.add_argument("--match", help="filter path, product, or manufacturer text")
    hid.add_argument("--vid", type=lambda value: int(value, 16),
                     help="filter hexadecimal USB vendor ID")
    compare = sub.add_parser("compare", help="diff two saved captures offline")
    compare.add_argument("before")
    compare.add_argument("after")
    for command in sub.choices.values():
        command.add_argument("--output", help="new local JSON capture path")
    args = parser.parse_args()

    if args.command == "ble-scan":
        result = asyncio.run(ble_scan(args.seconds))
    elif args.command == "ble-gatt":
        result = asyncio.run(ble_gatt(args.address, args.standard))
    elif args.command == "hid-list":
        result = hid_list(args.match, args.vid)
    else:
        result = compare_captures(args.before, args.after)
    save_capture(result, args.output)


if __name__ == "__main__":
    main()
