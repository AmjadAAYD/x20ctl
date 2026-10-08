"""Standard BLE inventory only; no vendor queries, subscriptions or pairing."""
import asyncio
from .evidence import clean_text

BATTERY = "0000180f-0000-1000-8000-00805f9b34fb"
INFO = "0000180a-0000-1000-8000-00805f9b34fb"
STANDARD = {
    (BATTERY, "00002a19-0000-1000-8000-00805f9b34fb"): "battery_level",
    **{(INFO, f"00002a{code}-0000-1000-8000-00805f9b34fb"): name for code, name in
       (("24", "model_number"), ("26", "firmware_revision"), ("27", "hardware_revision"),
        ("28", "software_revision"), ("29", "manufacturer_name"))},
}


async def scan(seconds=10):
    from bleak import BleakScanner
    if not 1 <= seconds <= 20: raise ValueError("Scan duration out of bounds")
    found = await asyncio.wait_for(BleakScanner.discover(timeout=seconds, return_adv=True), seconds + 10)
    return [{"_key": address, "_device": device, "name": clean_text(adv.local_name or device.name or "Unnamed"),
             "service_uuids": list(adv.service_uuids)[:64], "rssi": adv.rssi,
             "manufacturer_data_present": bool(adv.manufacturer_data),
             "service_data_present": bool(adv.service_data)}
            for address, (device, adv) in list(found.items())[:128]]


async def inspect_client(client):
    result = {"status": "observed", "services": [], "standard_values": {}}
    for service in client.services:
        if len(result["services"]) >= 64: raise ValueError("GATT service limit exceeded")
        record = {"uuid": service.uuid.lower(), "characteristics": []}
        for char in service.characteristics:
            if len(record["characteristics"]) >= 128: raise ValueError("GATT characteristic limit exceeded")
            item = {"uuid": char.uuid.lower(), "properties": list(char.properties),
                    "handle": getattr(char, "handle", None),
                    "descriptor_uuids": [d.uuid for d in char.descriptors][:32]}
            label = STANDARD.get((service.uuid.lower(), char.uuid.lower()))
            if label and "read" in char.properties:
                try:
                    raw = bytes(await asyncio.wait_for(client.read_gatt_char(char), 3))
                    if len(raw) > 128: raise ValueError("Standard value exceeds limit")
                    if label == "battery_level":
                        if len(raw) != 1 or raw[0] > 100: raise ValueError("Invalid battery value")
                        value = raw[0]
                    else: value = clean_text(raw.decode("utf-8", "replace").rstrip("\0"))
                    result["standard_values"][label] = value
                except (TimeoutError, OSError, ValueError):
                    item["standard_read_status"] = "unavailable"
                except Exception:
                    item["standard_read_status"] = "failed"
            record["characteristics"].append(item)
        result["services"].append(record)
    return result


async def inspect(selected):
    from bleak import BleakClient
    async def collect():
        async with BleakClient(selected.get("_device") or selected["_key"], timeout=12, pair=False) as client:
            return await inspect_client(client)
    return await asyncio.wait_for(collect(), 35)
