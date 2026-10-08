"""Discovery clues never authorize a configuration backend or identify a model."""

KEYLINKER = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
X15_FF12 = "0000ff12-0000-1000-8000-00805f9b34fb"


def ble_hints(model, advertisement, gatt=None):
    model = model.strip().lower().replace(" ", "_")
    services = {row["uuid"].lower() for row in (gatt or {}).get("services", [])}
    services.update(
        str(uuid).lower() for uuid in advertisement.get("service_uuids", [])
    )
    families = []
    if model == "x15":
        if KEYLINKER in services:
            families.append("keylinker_family")
        if X15_FF12 in services or "ff12" in services or "0xff12" in services:
            families.append("x15_ff12_owner_observed")
    if model == "x10" and advertisement.get("name", "").strip().casefold() == "qmacro":
        families.append("qmacro_name_candidate")
    return {
        "families": families,
        "modelIdentified": False,
        "configurationWritesAuthorized": False,
        "commandProtocolVerified": False,
        "scope": "Discovered attributes and reported names only; handles are dynamic",
        "evidenceClass": "owner_report"
        if model == "x15"
        else "manufacturer_side_channel",
        "missing": "Model-specific remap framing, key encoding, ACK/readback, persistence and restoration",
    }
