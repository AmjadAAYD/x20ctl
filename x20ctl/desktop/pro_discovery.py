"""X20 Pro preparation: discovery only, with no vendor command channel."""

from __future__ import annotations

import asyncio
import json
import subprocess

from bleak import BleakClient, BleakScanner

BATTERY_SERVICE = "0000180f-0000-1000-8000-00805f9b34fb"
DEVICE_INFO_SERVICE = "0000180a-0000-1000-8000-00805f9b34fb"
STANDARD_READS = {
    BATTERY_SERVICE: {"00002a19-0000-1000-8000-00805f9b34fb": "batteryLevel"},
    DEVICE_INFO_SERVICE: {
        "00002a24-0000-1000-8000-00805f9b34fb": "modelNumber",
        "00002a26-0000-1000-8000-00805f9b34fb": "firmwareRevision",
        "00002a27-0000-1000-8000-00805f9b34fb": "hardwareRevision",
        "00002a28-0000-1000-8000-00805f9b34fb": "softwareRevision",
        "00002a29-0000-1000-8000-00805f9b34fb": "manufacturerName",
    },
}


async def scan(seconds: float = 5.0) -> list[dict]:
    """Return observed BLE advertisements without identifying a device model."""
    found: dict[str, dict] = {}

    def seen(device, advertisement):
        found[device.address] = {
            "address": device.address,
            "name": advertisement.local_name or device.name or "Unnamed peripheral",
            "services": sorted(uuid.lower() for uuid in advertisement.service_uuids),
            "rssi": advertisement.rssi,
        }

    async with BleakScanner(detection_callback=seen):
        await asyncio.sleep(seconds)
    return list(found.values())


async def inspect(address: str) -> dict:
    """Enumerate GATT and read only allowlisted standard information."""
    services = []
    values = {}
    async with BleakClient(address, timeout=15.0) as client:
        for service in client.services:
            uuid = service.uuid.lower()
            characteristics = []
            for characteristic in service.characteristics:
                char_uuid = characteristic.uuid.lower()
                properties = sorted(characteristic.properties)
                characteristics.append({"uuid": char_uuid, "properties": properties})
                label = STANDARD_READS.get(uuid, {}).get(char_uuid)
                if label and "read" in properties:
                    try:
                        raw = bytes(await client.read_gatt_char(characteristic))
                        if label == "batteryLevel" and len(raw) == 1:
                            values[label] = raw[0]
                        elif label != "batteryLevel":
                            values[label] = raw.decode("utf-8", "replace").rstrip("\x00")
                    except Exception as exc:
                        values[label] = {"error": str(exc)}
            services.append({"uuid": uuid, "characteristics": characteristics})
    return {"address": address, "verifiedModel": None, "standardValues": values,
            "services": services}


def hid_inventory() -> list[dict]:
    """Read present Windows HID/gamepad PnP metadata, never feature reports."""
    command = (
        "Get-CimInstance Win32_PnPEntity | "
        "Where-Object { $_.PNPClass -eq 'HIDClass' -or $_.PNPClass -eq 'XboxComposite' } | "
        "Select-Object Name,PNPDeviceID,Manufacturer | ConvertTo-Json -Compress"
    )
    completed = subprocess.run(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", command],
        capture_output=True, text=True, timeout=20,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    if completed.returncode:
        raise RuntimeError("Windows HID inventory is unavailable")
    if not completed.stdout.strip():
        return []
    data = json.loads(completed.stdout)
    return data if isinstance(data, list) else [data]
