"""Allowlisted standard USB hub queries, never controller vendor requests."""
import ctypes as C
from ctypes import wintypes as W
import struct
from .windows import HUB_GUID


def parse_configuration(raw):
    if len(raw) < 9 or raw[0] != 9 or raw[1] != 2: raise ValueError("Invalid configuration header")
    length = int.from_bytes(raw[2:4], "little")
    if length != len(raw) or length > 65535: raise ValueError("Truncated configuration")
    out = {"configuration_value": raw[5], "interface_count": raw[4], "interfaces": []}
    offset = 0; interface = None
    while offset < length:
        if offset + 2 > length or raw[offset] < 2 or offset + raw[offset] > length:
            raise ValueError("Invalid USB descriptor length")
        data = raw[offset:offset + raw[offset]]; kind = data[1]
        if kind == 4:
            if len(data) < 9: raise ValueError("Truncated interface")
            interface = {"number": data[2], "alternate_setting": data[3], "class": data[5],
                         "subclass": data[6], "protocol": data[7], "endpoints": []}
            out["interfaces"].append(interface)
        elif kind == 5:
            if len(data) < 7 or interface is None: raise ValueError("Invalid endpoint")
            interface["endpoints"].append({"address": f"{data[2]:02X}", "direction": "IN" if data[2] & 128 else "OUT",
                "transfer_type": ("control", "isochronous", "bulk", "interrupt")[data[3] & 3],
                "max_packet_size": int.from_bytes(data[4:6], "little"), "interval": data[6]})
        elif kind == 0x21 and interface is not None:
            if len(data) >= 9: interface["hid_report_descriptor_length"] = int.from_bytes(data[7:9], "little")
        offset += len(data)
    return out


class Connection(C.Structure):
    _fields_ = [("index", W.DWORD), ("descriptor", C.c_ubyte * 18), ("config", C.c_ubyte),
                ("speed", C.c_ubyte), ("hub", C.c_ubyte), ("address", W.WORD),
                ("pipes", W.DWORD), ("status", W.DWORD)]


def query(native, handle, function, payload, size):
    if function not in (258, 260, 264, 274): raise ValueError("USB operation not allowlisted")
    if not len(payload) <= size <= 65547: raise ValueError("Invalid USB buffer size")
    buf = C.create_string_buffer(size); C.memmove(buf, payload, len(payload))
    actual = W.DWORD()
    if not native.kernel.DeviceIoControl(handle, (0x22 << 16) | (function << 2), buf, size,
                                         buf, size, C.byref(actual), None):
        raise OSError("Standard USB query unavailable")
    if actual.value > size: raise OSError("Invalid USB response size")
    return buf.raw[:actual.value]


def descriptor(native, handle, port, config_index, size):
    # Standard GET_DESCRIPTOR(CONFIGURATION), no string/serial/report/vendor reads.
    if not 9 <= size <= 65535 or not 0 <= config_index < 8: raise ValueError("Invalid configuration request")
    request = struct.pack("<IBBHHH", port, 0x80, 6, (2 << 8) | config_index, 0, size)
    reply = query(native, handle, 260, request, 12 + size)
    if len(reply) < 12: raise OSError("Missing USB descriptor")
    return reply[12:]


def inspect(native, selected):
    chain = selected.get("_chain", [])
    physical_index = next((i for i, node in enumerate(chain) if node["_instance"].casefold() == selected.get("_parent")), None)
    if physical_index is None or physical_index + 1 >= len(chain):
        return {"status": "unavailable", "reason": "No correlated USB parent"}, {}
    physical = chain[physical_index]; parent = chain[physical_index + 1]["_instance"].casefold()
    driver = physical.get("_driver_key")
    hub = next((h for h in native.interfaces(HUB_GUID) if h["_instance"].casefold() == parent), None)
    if hub is None or not driver: return {"status": "unavailable", "reason": "USB hub association unavailable"}, {}
    handle = native.open(hub["_path"])
    try:
        hub_info = query(native, handle, 258, b"\0" * 4, 1024)
        if len(hub_info) < 7: raise OSError("Hub information incomplete")
        matched = None
        for port in range(1, hub_info[6] + 1):
            try:
                name = query(native, handle, 264, struct.pack("<II", port, 0), 4096)
                actual = int.from_bytes(name[4:8], "little")
                if actual > len(name): continue
                key = name[8:actual].decode("utf-16-le", "replace").rstrip("\0")
                if key.casefold() == driver.casefold(): matched = port; break
            except OSError: continue
        if matched is None: return {"status": "unavailable", "reason": "Target hub port not confirmed"}, {}
        data = query(native, handle, 274, struct.pack("<I", matched), 4096)
        if len(data) < C.sizeof(Connection): raise OSError("Connection descriptor incomplete")
        conn = Connection.from_buffer_copy(data[:C.sizeof(Connection)])
        raw = bytes(conn.descriptor)
        if conn.status != 1 or raw[:2] != b"\x12\x01": raise OSError("Target USB device not connected")
        vid, pid = struct.unpack_from("<HH", raw, 8)
        if selected.get("vid") != vid or selected.get("pid") != pid:
            return {"status": "unavailable", "reason": "Target identity changed; repeat selection"}, {}
        out = {"status": "observed", "source": "Windows USB hub standard descriptor query",
               "vid": f"{vid:04X}", "pid": f"{pid:04X}", "bcdUSB": f"{int.from_bytes(raw[2:4], 'little'):04X}",
               "bcdDevice": f"{int.from_bytes(raw[12:14], 'little'):04X}", "class": raw[4], "subclass": raw[5], "protocol": raw[6],
               "serial_present": bool(raw[16]), "configuration_count": raw[17], "configurations": []}
        binaries = {"usb-device-descriptor.bin": raw}
        for index in range(min(raw[17], 8)):
            try:
                head = descriptor(native, handle, matched, index, 9)
                if len(head) < 9: raise OSError("Configuration header missing")
                size = int.from_bytes(head[2:4], "little")
                config = descriptor(native, handle, matched, index, size)
                parsed = parse_configuration(config)
                out["configurations"].append(parsed)
                binaries[f"usb-config-{index}.bin"] = config
            except (ValueError, OSError):
                out["configurations"].append({"index": index, "status": "unavailable"})
        return out, binaries
    finally: native.kernel.CloseHandle(handle)
