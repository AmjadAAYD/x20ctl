"""Passive interpretation of captured configuration-family exchanges.

This module has no transport, device, network, or write API. Reported features
are evidence for research, never permission to configure an unidentified pad.
"""
import struct
from uuid import UUID

from x20ctl import protocol as p
from .app_capture import trace_info

SERVICE = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"


def _profile(stream):
    return {
        "stream": stream, "family": "keylinker_family",
        "evidenceType": "captured_reported_capabilities",
        "modelIdentified": False, "configurationWritesAuthorized": False,
        "commandProtocolVerified": False, "identity": None,
        "reportedFeatures": None, "supportedSourceCodes": None,
        "supportedDestinationCodes": None,
    }


def discover(events):
    """Decode normalized, ordered GATT events using captured request serials.

    Only RESPONSE frames paired with a valid outgoing request are interpreted.
    HOST_MENU continuations must follow positions 0, 1, ... for the same kind.
    Identity follows the app's length-prefixed 0x91 cache, not USB identifiers.
    """
    contexts, profiles = {}, {}
    rejected = 0
    for event in events:
        stream = str(event["stream"])
        direction = event.get("direction")
        expected = WRITE if direction == "write" else NOTIFY if direction == "notify" else None
        if expected is None or event.get("characteristicUuid", "").lower() != expected:
            continue
        raw = event.get("data", b"")
        if not isinstance(raw, bytes) or not 5 <= len(raw) <= p.MAX_PACKET:
            rejected += 1
            continue
        packet = p.parse(raw)
        if not packet.crc_valid or packet.declared_length != len(raw):
            if stream in contexts:
                contexts[stream]["pending"].clear()
                contexts[stream]["chunks"].clear()
            rejected += 1
            continue
        context = contexts.setdefault(stream, {"pending": {}, "chunks": {}})
        if direction == "write":
            # Every write replaces this serial, including commands we ignore.
            # A later ACK must never be mistaken for an earlier menu response.
            context["pending"].pop(packet.serial, None)
            if packet.opcode == p.Op.READ_VID_PID_VERSION and not packet.payload:
                context["pending"][packet.serial] = ("identity", 0)
            elif packet.opcode == p.Op.HOST_MENU and len(packet.payload) == 2:
                position, kind = packet.payload
                if kind in {1, 3, 7} and position < 18:
                    if position == 0:
                        context["chunks"].pop(kind, None)
                    context["pending"][packet.serial] = (kind, position)
            continue
        request = context["pending"].pop(packet.serial, None)
        if packet.opcode != p.Op.RESPONSE or request is None:
            rejected += 1
            continue
        kind, position = request
        try:
            if position == 0:
                body = p.unwrap(packet.payload)
                declared, data = body.declared, body.data
            else:
                previous = context["chunks"].pop(kind, None)
                if previous is None or previous[2] != position:
                    raise ValueError("Missing continuation")
                declared, data, _ = previous
                data += packet.payload
            if len(data) > declared:
                raise ValueError("Extra body bytes")
            if len(data) < declared:
                context["chunks"][kind] = (declared, data, position + 1)
                continue
            profile = profiles.get(stream, _profile(stream))
            if kind == "identity":
                if len(data) not in {6, 7, 8}:
                    raise ValueError("Unknown identity layout")
                info = p.parse_device_info(data)
                identity = {"internalVendorId": info.vid, "internalProductId": info.pid,
                            "version": info.version, "deviceFamily": info.device_id,
                            "mode": info.model, "protocolGeneration": info.version_family}
                if profile["identity"] is not None and profile["identity"] != identity:
                    # Do not blend two identities if a normalized stream was reused.
                    profile = _profile(stream)
                profile["identity"] = identity
            elif kind == 1:
                if len(data) != 10:
                    raise ValueError("Unknown capability layout")
                caps = p.parse_capabilities(p.Body(declared, data))
                profile["reportedFeatures"] = {
                    "leftStick": caps.has_left_stick, "rightStick": caps.has_right_stick,
                    "leftTrigger": caps.has_left_trigger, "rightTrigger": caps.has_right_trigger,
                    "motorCount": caps.motor_count, "macroSlots": caps.macro_slots,
                    "remapping": bool(caps.changekey), "turbo": bool(caps.turbo),
                }
            else:
                keys = p.decode_key_list(data)
                field = "supportedSourceCodes" if kind == 3 else "supportedDestinationCodes"
                profile[field] = keys
            profiles[stream] = profile
        except (ValueError, IndexError):
            context["chunks"].pop(kind, None)
            rejected += 1
    for profile in profiles.values():
        profile["missing"] = [
            label for field, label in (
                ("identity", "Controller identity reply"),
                ("reportedFeatures", "Feature menu reply"),
                ("supportedSourceCodes", "Remappable source-button list"),
                ("supportedDestinationCodes", "Destination-button list"),
            ) if profile[field] is None
        ] + ["Verified model association", "Model-specific write, ACK, read-back, persistence and restoration"]
    return {"status": "reported" if profiles else "no_complete_exchange",
            "profiles": list(profiles.values()), "rejectedFrames": rejected,
            "configurationWritesAuthorized": False,
            "message": "Reported features need model-specific verification before configuration can be enabled."
            if profiles else "No complete supported exchange found. Capture GATT discovery and the app's feature queries."}


def _uuid(raw):
    if len(raw) == 16:
        return str(UUID(bytes=raw[::-1]))
    if len(raw) == 2:
        return f"0000{int.from_bytes(raw, 'little'):04x}-0000-1000-8000-00805f9b34fb"
    return ""


def _btsnoop_events(data):
    """Extract ATT on fixed CID 4; handles come only from this connection.

    UART (1002) captures are supported. ACL fragments are reassembled per
    handle and direction, with bounded L2CAP lengths. Disconnects and new
    connection events discard handle maps. Cached discovery is not guessed.
    """
    epochs, contexts, buffers = {}, {}, {}
    offset = 16
    while offset < len(data):
        original, included, flags = struct.unpack_from(">III", data, offset)
        raw = data[offset + 24:offset + 24 + included]
        offset += 24 + included
        if not raw:
            continue
        if raw[0] == 4 and len(raw) >= 7 and len(raw) == raw[2] + 3:
            # Disconnection Complete; LE Connection/Enhanced Connection Complete.
            handle = None
            if raw[1] == 5 and len(raw) == 7 and raw[3] == 0:
                handle = int.from_bytes(raw[4:6], "little")
            elif raw[1] == 0x3E and len(raw) >= 8 and raw[3] in {1, 10} and raw[4] == 0:
                handle = int.from_bytes(raw[5:7], "little")
            if handle is not None:
                epochs[handle] = epochs.get(handle, 0) + 1
                contexts.pop(handle, None)
                for key in [(handle, False), (handle, True)]:
                    buffers.pop(key, None)
            continue
        if raw[0] != 2 or len(raw) < 5:
            continue
        handle_flags, length = struct.unpack_from("<HH", raw, 1)
        handle, boundary = handle_flags & 0xFFF, (handle_flags >> 12) & 3
        key = (handle, bool(flags & 1))
        if included != original or len(raw) != length + 5 or handle_flags & 0xC000:
            buffers.pop(key, None)
            contexts.pop(handle, None)
            epochs[handle] = epochs.get(handle, 0) + 1
            continue
        chunk = raw[5:]
        if boundary in {0, 2}:
            buffers.pop(key, None)
            if len(chunk) < 4:
                continue
            expected, cid = struct.unpack_from("<HH", chunk)
            if cid != 4 or not 0 < expected <= 512:
                continue
            buffers[key] = (expected, chunk[4:])
        elif boundary == 1 and key in buffers:
            expected, previous = buffers[key]
            buffers[key] = (expected, previous + chunk)
        else:
            buffers.pop(key, None)
            continue
        expected, att = buffers[key]
        if len(att) < expected:
            continue
        buffers.pop(key, None)
        if len(att) != expected:
            continue
        context = contexts.setdefault(handle, {"services": {}, "chars": {}, "query": None})
        incoming = bool(flags & 1)
        if not incoming and att[0] % 2 == 0 and att[0] < 0x20:
            context["query"] = None
            if len(att) == 7 and att[0] in {8, 16}:
                first, last, kind = struct.unpack_from("<HHH", att, 1)
                if (att[0], kind) in {(8, 0x2803), (16, 0x2800)} and 0 < first <= last:
                    context["query"] = (att[0] + 1, first, last)
            elif len(att) == 5 and att[0] == 4:
                first, last = struct.unpack_from("<HH", att, 1)
                if 0 < first <= last:
                    context["query"] = (5, first, last)
        if incoming and att[0] in {0x09, 0x11, 0x05} and len(att) >= 2:
            query = context["query"]
            context["query"] = None
            if query is None or query[0] != att[0]:
                continue
            size = att[1] if att[0] != 5 else {1: 4, 2: 18}.get(att[1], 0)
            valid_sizes = {0x09: {7, 21}, 0x11: {6, 20}, 0x05: {4, 18}}
            if size not in valid_sizes[att[0]] or (len(att) - 2) % size:
                continue
            for start in range(2, len(att), size):
                record = att[start:start + size]
                if not query[1] <= int.from_bytes(record[:2], "little") <= query[2]:
                    continue
                if att[0] == 0x11:
                    first, last = struct.unpack_from("<HH", record)
                    if _uuid(record[4:]) == SERVICE and 0 < first <= last:
                        context["services"][first] = last
                elif att[0] == 0x09:
                    declaration = int.from_bytes(record[:2], "little")
                    value = int.from_bytes(record[3:5], "little")
                    if 0 < declaration < value:
                        context["chars"][value] = _uuid(record[5:])
                else:
                    context["chars"][int.from_bytes(record[:2], "little")] = _uuid(record[2:])
            continue
        if len(att) < 3 or att[0] not in {0x12, 0x52, 0x1B, 0x1D}:
            continue
        value_handle = int.from_bytes(att[1:3], "little")
        uuid = context["chars"].get(value_handle)
        if not any(first <= value_handle <= last for first, last in context["services"].items()):
            continue
        direction = "notify" if incoming and att[0] in {0x1B, 0x1D} else "write" if not incoming and att[0] in {0x12, 0x52} else None
        if uuid == (NOTIFY if direction == "notify" else WRITE if direction == "write" else None):
            yield {"stream": f"connection-{handle}-{epochs.get(handle, 0)}",
                   "direction": direction, "characteristicUuid": uuid, "data": att[3:]}


def analyze_trace(data):
    """Analyze a bounded imported trace without opening any device or socket."""
    route = "android" if data.startswith(b"btsnoop\0") else "windows"
    info = trace_info(data, route)
    if info["format"] != "android_btsnoop" or struct.unpack_from(">I", data, 12)[0] != 1002:
        return {"status": "unsupported_capture", "profiles": [],
                "configurationWritesAuthorized": False,
                "message": "Feature decoding currently needs a full Android Bluetooth HCI UART log. This trace remains available for manual protocol research."}
    result = discover(_btsnoop_events(data))
    result["captureFormat"] = info["format"]
    return result
