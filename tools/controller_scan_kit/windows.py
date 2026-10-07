"""Windows SetupAPI/HID metadata and cancellable input reads. No output API."""
import ctypes as C
from ctypes import wintypes as W
import re
import sys
import uuid

INVALID = C.c_void_p(-1).value
HID_GUID = "4d1e55b2-f16f-11cf-88cb-001111000030"
USB_GUID = "a5dcbf10-6530-11d2-901f-00c04fb951ed"
HUB_GUID = "f18a0e88-c30c-11d0-8815-00a0c906bed8"


class GUID(C.Structure):
    _fields_ = [("data", C.c_ubyte * 16)]

    @classmethod
    def from_text(cls, text):
        return cls((C.c_ubyte * 16).from_buffer_copy(uuid.UUID(text).bytes_le))


class Interface(C.Structure):
    _fields_ = [("size", W.DWORD), ("guid", GUID), ("flags", W.DWORD), ("reserved", C.c_size_t)]


class DevInfo(C.Structure):
    _fields_ = [("size", W.DWORD), ("guid", GUID), ("devinst", W.DWORD), ("reserved", C.c_size_t)]


class Attributes(C.Structure):
    _fields_ = [("size", W.DWORD), ("vid", W.WORD), ("pid", W.WORD), ("version", W.WORD)]


class Caps(C.Structure):
    _fields_ = [("usage", W.WORD), ("usage_page", W.WORD), ("input_len", W.WORD),
                ("output_len", W.WORD), ("feature_len", W.WORD), ("reserved", W.WORD * 17),
                ("counts", W.WORD * 10)]


class Overlapped(C.Structure):
    _fields_ = [("internal", C.c_size_t), ("internal_high", C.c_size_t),
                ("offset", W.DWORD), ("offset_high", W.DWORD), ("event", W.HANDLE)]


class Native:
    def __init__(self):
        if sys.platform != "win32": raise OSError("Windows required")
        self.setup = C.WinDLL("setupapi", use_last_error=True)
        self.hid = C.WinDLL("hid", use_last_error=True)
        self.kernel = C.WinDLL("kernel32", use_last_error=True)
        self.cm = C.WinDLL("cfgmgr32", use_last_error=True)
        self._bind(self.setup, "SetupDiGetClassDevsW", W.HANDLE, [C.POINTER(GUID), W.LPCWSTR, W.HWND, W.DWORD])
        self._bind(self.setup, "SetupDiCreateDeviceInfoList", W.HANDLE, [C.c_void_p, W.HWND])
        self._bind(self.setup, "SetupDiOpenDeviceInfoW", W.BOOL, [W.HANDLE, W.LPCWSTR, W.HWND, W.DWORD, C.POINTER(DevInfo)])
        self._bind(self.setup, "SetupDiEnumDeviceInterfaces", W.BOOL,
                   [W.HANDLE, C.c_void_p, C.POINTER(GUID), W.DWORD, C.POINTER(Interface)])
        self._bind(self.setup, "SetupDiGetDeviceInterfaceDetailW", W.BOOL,
                   [W.HANDLE, C.POINTER(Interface), C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.POINTER(DevInfo)])
        self._bind(self.setup, "SetupDiGetDeviceRegistryPropertyW", W.BOOL,
                   [W.HANDLE, C.POINTER(DevInfo), W.DWORD, C.POINTER(W.DWORD), C.c_void_p, W.DWORD, C.POINTER(W.DWORD)])
        self._bind(self.setup, "SetupDiDestroyDeviceInfoList", W.BOOL, [W.HANDLE])
        self._bind(self.cm, "CM_Get_Device_IDW", W.DWORD, [W.DWORD, W.LPWSTR, W.DWORD, W.DWORD])
        self._bind(self.cm, "CM_Get_Parent", W.DWORD, [C.POINTER(W.DWORD), W.DWORD, W.DWORD])
        self._bind(self.kernel, "CreateFileW", W.HANDLE,
                   [W.LPCWSTR, W.DWORD, W.DWORD, C.c_void_p, W.DWORD, W.DWORD, W.HANDLE])
        self._bind(self.kernel, "CloseHandle", W.BOOL, [W.HANDLE])
        self._bind(self.kernel, "CreateEventW", W.HANDLE, [C.c_void_p, W.BOOL, W.BOOL, W.LPCWSTR])
        self._bind(self.kernel, "ResetEvent", W.BOOL, [W.HANDLE])
        self._bind(self.kernel, "WaitForSingleObject", W.DWORD, [W.HANDLE, W.DWORD])
        self._bind(self.kernel, "ReadFile", W.BOOL,
                   [W.HANDLE, C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.POINTER(Overlapped)])
        self._bind(self.kernel, "CancelIoEx", W.BOOL, [W.HANDLE, C.POINTER(Overlapped)])
        self._bind(self.kernel, "GetOverlappedResult", W.BOOL,
                   [W.HANDLE, C.POINTER(Overlapped), C.POINTER(W.DWORD), W.BOOL])
        self._bind(self.kernel, "DeviceIoControl", W.BOOL,
                   [W.HANDLE, W.DWORD, C.c_void_p, W.DWORD, C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.POINTER(Overlapped)])
        self._bind(self.hid, "HidD_GetAttributes", W.BOOL, [W.HANDLE, C.POINTER(Attributes)])
        self._bind(self.hid, "HidD_GetPreparsedData", W.BOOL, [W.HANDLE, C.POINTER(C.c_void_p)])
        self._bind(self.hid, "HidD_FreePreparsedData", W.BOOL, [C.c_void_p])
        self._bind(self.hid, "HidP_GetCaps", W.LONG, [C.c_void_p, C.POINTER(Caps)])
        for name in ("HidD_GetManufacturerString", "HidD_GetProductString"):
            self._bind(self.hid, name, W.BOOL, [W.HANDLE, C.c_void_p, W.DWORD])

    @staticmethod
    def _bind(dll, name, result, args):
        fn = getattr(dll, name); fn.restype = result; fn.argtypes = args

    def open(self, path, read=False, overlapped=False):
        handle = self.kernel.CreateFileW(path, 0x80000000 if read else 0, 3, None, 3,
                                         0x40000000 if overlapped else 0, None)
        if handle == INVALID: raise OSError(C.get_last_error(), "Device access unavailable")
        return handle

    def instance(self, devinst):
        value = C.create_unicode_buffer(2048)
        if self.cm.CM_Get_Device_IDW(devinst, value, len(value), 0) != 0: return ""
        return value.value

    def properties(self, devinst):
        instance = self.instance(devinst)
        result = {"_instance": instance}
        handle = self.setup.SetupDiCreateDeviceInfoList(None, None)
        if handle == INVALID: return result
        try:
            info = DevInfo(); info.size = C.sizeof(info)
            if not self.setup.SetupDiOpenDeviceInfoW(handle, instance, None, 0, C.byref(info)): return result
            for prop, name in ((0, "description"), (1, "hardware_ids"), (2, "compatible_ids"),
                               (4, "driver_service"), (7, "class"), (9, "_driver_key"), (11, "manufacturer")):
                buf = C.create_string_buffer(8192); typ = W.DWORD(); size = W.DWORD()
                if self.setup.SetupDiGetDeviceRegistryPropertyW(handle, C.byref(info), prop,
                        C.byref(typ), buf, len(buf), C.byref(size)) and size.value <= len(buf):
                    text = buf.raw[:size.value].decode("utf-16-le", "replace").rstrip("\0")
                    result[name] = text.split("\0") if typ.value == 7 else text
            return result
        finally: self.setup.SetupDiDestroyDeviceInfoList(handle)

    def ancestry(self, devinst):
        out = []
        for _ in range(16):
            out.append(self.properties(devinst))
            parent = W.DWORD()
            if self.cm.CM_Get_Parent(C.byref(parent), devinst, 0) != 0: break
            devinst = parent.value
        return out

    def interfaces(self, guid_text):
        guid = GUID.from_text(guid_text)
        handle = self.setup.SetupDiGetClassDevsW(C.byref(guid), None, None, 0x12)
        if handle == INVALID: raise OSError("Enumeration unavailable")
        result = []
        try:
            for index in range(1024):
                interface = Interface(); interface.size = C.sizeof(interface)
                if not self.setup.SetupDiEnumDeviceInterfaces(handle, None, C.byref(guid), index, C.byref(interface)):
                    if C.get_last_error() != 259: raise OSError("Interface enumeration failed")
                    break
                size = W.DWORD()
                self.setup.SetupDiGetDeviceInterfaceDetailW(handle, C.byref(interface), None, 0, C.byref(size), None)
                if not 8 <= size.value <= 8192: continue
                buf = C.create_string_buffer(size.value)
                W.DWORD.from_buffer(buf).value = 8 if C.sizeof(C.c_void_p) == 8 else 6
                info = DevInfo(); info.size = C.sizeof(info)
                if self.setup.SetupDiGetDeviceInterfaceDetailW(handle, C.byref(interface), buf, len(buf), C.byref(size), C.byref(info)):
                    path = C.wstring_at(C.addressof(buf) + 4)
                    result.append({"_key": path.casefold(), "_path": path, "_devinst": info.devinst,
                                   "_instance": self.instance(info.devinst)})
            else: raise OSError("Interface limit exceeded")
        finally: self.setup.SetupDiDestroyDeviceInfoList(handle)
        return result

    def hid_info(self, row):
        handle = self.open(row["_path"])
        try:
            attr = Attributes(); attr.size = C.sizeof(attr)
            if self.hid.HidD_GetAttributes(handle, C.byref(attr)):
                row.update(vid=attr.vid, pid=attr.pid, version=attr.version)
            for fn, label in (("HidD_GetProductString", "product"), ("HidD_GetManufacturerString", "manufacturer")):
                buf = C.create_unicode_buffer(256)
                if getattr(self.hid, fn)(handle, buf, C.sizeof(buf)): row[label] = buf.value
            data = C.c_void_p()
            if self.hid.HidD_GetPreparsedData(handle, C.byref(data)):
                try:
                    caps = Caps()
                    if self.hid.HidP_GetCaps(data, C.byref(caps)) == 0x110000:
                        row.update({name: getattr(caps, name) for name in
                                    ("usage", "usage_page", "input_len", "output_len", "feature_len")})
                finally: self.hid.HidD_FreePreparsedData(data)
        finally: self.kernel.CloseHandle(handle)

    def inventory(self):
        result = []
        for guid, kind in ((USB_GUID, "usb"), (HID_GUID, "hid")):
            for row in self.interfaces(guid):
                chain = self.ancestry(row["_devinst"])
                row.update(chain[0], kind=kind, _chain=chain)
                physical = next((node for node in chain if node["_instance"].upper().startswith("USB\\VID_")
                                 and "&MI_" not in node["_instance"].upper().split("\\")[1]), chain[0])
                row["_parent"] = physical["_instance"].casefold()
                match = re.search(r"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", row["_instance"], re.I)
                if match: row.update(vid=int(match[1], 16), pid=int(match[2], 16))
                result.append(row)
        return result


class HidReader:
    def __init__(self, native, row):
        if row.get("usage_page") != 1 or row.get("usage") not in (4, 5, 8):
            raise ValueError("Only selected gameplay HID collections may be recorded")
        self.length = row.get("input_len", 0)
        if not 1 <= self.length <= 4096: raise ValueError("Invalid input length")
        self.native = native
        self.handle = native.open(row["_path"], read=True, overlapped=True)
        self.event = native.kernel.CreateEventW(None, True, False, None)
        if not self.event:
            native.kernel.CloseHandle(self.handle)
            raise OSError("Input event unavailable")
        self.pending = None

    def read(self, timeout_ms=100):
        k = self.native.kernel
        buf = C.create_string_buffer(self.length)
        received = W.DWORD()
        overlap = Overlapped(); overlap.event = self.event
        k.ResetEvent(self.event)
        ok = k.ReadFile(self.handle, buf, self.length, C.byref(received), C.byref(overlap))
        if not ok:
            if C.get_last_error() != 997: raise OSError("HID input unavailable/disconnected")
            self.pending = (buf, overlap)
            try:
                if k.WaitForSingleObject(self.event, timeout_ms) == 258:
                    k.CancelIoEx(self.handle, C.byref(overlap))
                    if k.WaitForSingleObject(self.event, 1000) != 0:
                        raise OSError("Input cancellation did not complete")
                    return None
                if not k.GetOverlappedResult(self.handle, C.byref(overlap), C.byref(received), False):
                    raise OSError("HID input failed/disconnected")
            finally:
                # Retain the buffer until close if a driver did not complete cancellation.
                if k.WaitForSingleObject(self.event, 0) == 0: self.pending = None
        return {"source": "hid_input", "report_hex": buf.raw[:received.value].hex(),
                "report_length": received.value, "framing": "Windows ReadFile collection bytes"}

    def close(self):
        self.native.kernel.CancelIoEx(self.handle, None)
        self.native.kernel.CloseHandle(self.handle)
        self.native.kernel.CloseHandle(self.event)


class Gamepad(C.Structure):
    _fields_ = [("buttons", W.WORD), ("lt", C.c_ubyte), ("rt", C.c_ubyte),
                ("lx", C.c_short), ("ly", C.c_short), ("rx", C.c_short), ("ry", C.c_short)]


class State(C.Structure):
    _fields_ = [("packet", W.DWORD), ("pad", Gamepad)]


class XInput:
    def __init__(self):
        self.fn = None
        if sys.platform != "win32": return
        for name in ("XInput1_4.dll", "XInput1_3.dll", "XInput9_1_0.dll"):
            try:
                self.dll = C.WinDLL(name)
                self.fn = self.dll.XInputGetState
                self.fn.argtypes = [W.DWORD, C.POINTER(State)]; self.fn.restype = W.DWORD
                break
            except OSError: continue

    def state(self, slot):
        data = State()
        if self.fn is None or self.fn(slot, C.byref(data)) != 0: return None
        return {"source": "xinput_state", "values": {"slot": slot, "packet": data.packet,
                **{name: getattr(data.pad, name) for name, _ in Gamepad._fields_}}}

    def slots(self):
        return [slot for slot in range(4) if self.state(slot) is not None]


class XInputReader:
    def __init__(self, adapter, slot): self.adapter, self.slot = adapter, slot
    def read(self, timeout_ms=100):
        result = self.adapter.state(self.slot)
        if result is None: raise OSError("Selected input slot disconnected")
        return result
    def close(self): pass
