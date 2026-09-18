"""Enumerate HID collections and probe for a vendor configuration channel.

No third-party dependencies: talks to setupapi.dll and hid.dll through ctypes so
it runs on a bare Python install.

Usage:
    python hid_scan.py                  list every HID collection
    python hid_scan.py --vendor         list only vendor-defined collections
    python hid_scan.py --vid 045E       filter by vendor id (hex)
    python hid_scan.py --probe <path>   read-only sweep of feature report ids
    python hid_scan.py --watch <path>   dump input reports as they arrive

The --probe sweep is read-only. It issues HidD_GetFeature for each report id and
reports which ones the device answers. Devices that hide a configuration channel
almost always answer on at least one id.
"""

from __future__ import annotations

import argparse
import ctypes
import sys
import time
from ctypes import wintypes

if sys.platform != "win32":
    raise SystemExit("this tool is Windows-only; use hidraw on Linux")

setupapi = ctypes.WinDLL("setupapi")
hid = ctypes.WinDLL("hid")
kernel32 = ctypes.WinDLL("kernel32")

DIGCF_PRESENT = 0x02
DIGCF_DEVICEINTERFACE = 0x10
GENERIC_READ = 0x80000000
GENERIC_WRITE = 0x40000000
FILE_SHARE_READ = 0x01
FILE_SHARE_WRITE = 0x02
OPEN_EXISTING = 3
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
HIDP_STATUS_SUCCESS = 0x00110000


class GUID(ctypes.Structure):
    _fields_ = [
        ("Data1", wintypes.DWORD),
        ("Data2", wintypes.WORD),
        ("Data3", wintypes.WORD),
        ("Data4", ctypes.c_ubyte * 8),
    ]


class SP_DEVICE_INTERFACE_DATA(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("InterfaceClassGuid", GUID),
        ("Flags", wintypes.DWORD),
        ("Reserved", ctypes.POINTER(ctypes.c_ulong)),
    ]


class HIDD_ATTRIBUTES(ctypes.Structure):
    _fields_ = [
        ("Size", ctypes.c_ulong),
        ("VendorID", ctypes.c_ushort),
        ("ProductID", ctypes.c_ushort),
        ("VersionNumber", ctypes.c_ushort),
    ]


class HIDP_CAPS(ctypes.Structure):
    _fields_ = [
        ("Usage", ctypes.c_ushort),
        ("UsagePage", ctypes.c_ushort),
        ("InputReportByteLength", ctypes.c_ushort),
        ("OutputReportByteLength", ctypes.c_ushort),
        ("FeatureReportByteLength", ctypes.c_ushort),
        ("Reserved", ctypes.c_ushort * 17),
        ("NumberLinkCollectionNodes", ctypes.c_ushort),
        ("NumberInputButtonCaps", ctypes.c_ushort),
        ("NumberInputValueCaps", ctypes.c_ushort),
        ("NumberInputDataIndices", ctypes.c_ushort),
        ("NumberOutputButtonCaps", ctypes.c_ushort),
        ("NumberOutputValueCaps", ctypes.c_ushort),
        ("NumberOutputDataIndices", ctypes.c_ushort),
        ("NumberFeatureButtonCaps", ctypes.c_ushort),
        ("NumberFeatureValueCaps", ctypes.c_ushort),
        ("NumberFeatureDataIndices", ctypes.c_ushort),
    ]


# Explicit signatures throughout. Without them ctypes truncates 64-bit HANDLEs
# to int and the setupapi calls fail with an overflow.
setupapi.SetupDiGetClassDevsW.restype = wintypes.HANDLE
setupapi.SetupDiGetClassDevsW.argtypes = [
    ctypes.POINTER(GUID), wintypes.LPCWSTR, wintypes.HWND, wintypes.DWORD,
]
setupapi.SetupDiEnumDeviceInterfaces.restype = wintypes.BOOL
setupapi.SetupDiEnumDeviceInterfaces.argtypes = [
    wintypes.HANDLE, ctypes.c_void_p, ctypes.POINTER(GUID), wintypes.DWORD,
    ctypes.POINTER(SP_DEVICE_INTERFACE_DATA),
]
setupapi.SetupDiGetDeviceInterfaceDetailW.restype = wintypes.BOOL
setupapi.SetupDiGetDeviceInterfaceDetailW.argtypes = [
    wintypes.HANDLE, ctypes.POINTER(SP_DEVICE_INTERFACE_DATA), ctypes.c_void_p,
    wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p,
]
setupapi.SetupDiDestroyDeviceInfoList.restype = wintypes.BOOL
setupapi.SetupDiDestroyDeviceInfoList.argtypes = [wintypes.HANDLE]

kernel32.CreateFileW.restype = wintypes.HANDLE
kernel32.CreateFileW.argtypes = [
    wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p,
    wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE,
]
kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
kernel32.ReadFile.restype = wintypes.BOOL
kernel32.ReadFile.argtypes = [
    wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
    ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p,
]

hid.HidD_GetAttributes.argtypes = [wintypes.HANDLE, ctypes.POINTER(HIDD_ATTRIBUTES)]
hid.HidD_GetPreparsedData.argtypes = [wintypes.HANDLE, ctypes.POINTER(ctypes.c_void_p)]
hid.HidD_FreePreparsedData.argtypes = [ctypes.c_void_p]
hid.HidP_GetCaps.argtypes = [ctypes.c_void_p, ctypes.POINTER(HIDP_CAPS)]
hid.HidD_GetFeature.restype = wintypes.BOOL
hid.HidD_GetFeature.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.ULONG]
for _fn in (hid.HidD_GetProductString, hid.HidD_GetManufacturerString, hid.HidD_GetSerialNumberString):
    _fn.restype = wintypes.BOOL
    _fn.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.ULONG]


def hid_guid() -> GUID:
    g = GUID()
    hid.HidD_GetHidGuid(ctypes.byref(g))
    return g


def interface_paths() -> list[str]:
    """Every present HID interface path on the system."""
    guid = hid_guid()
    dev_set = setupapi.SetupDiGetClassDevsW(
        ctypes.byref(guid), None, None, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE
    )
    paths: list[str] = []
    iface = SP_DEVICE_INTERFACE_DATA()
    iface.cbSize = ctypes.sizeof(SP_DEVICE_INTERFACE_DATA)
    index = 0
    while setupapi.SetupDiEnumDeviceInterfaces(
        dev_set, None, ctypes.byref(guid), index, ctypes.byref(iface)
    ):
        needed = wintypes.DWORD(0)
        setupapi.SetupDiGetDeviceInterfaceDetailW(
            dev_set, ctypes.byref(iface), None, 0, ctypes.byref(needed), None
        )
        buf = ctypes.create_string_buffer(needed.value)
        # cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA_W: 8 on x64, 6 on x86
        ctypes.memmove(buf, ctypes.byref(wintypes.DWORD(8 if ctypes.sizeof(ctypes.c_void_p) == 8 else 6)), 4)
        if setupapi.SetupDiGetDeviceInterfaceDetailW(
            dev_set, ctypes.byref(iface), buf, needed, ctypes.byref(needed), None
        ):
            paths.append(ctypes.wstring_at(ctypes.addressof(buf) + 4))
        index += 1
    setupapi.SetupDiDestroyDeviceInfoList(dev_set)
    return paths


def open_device(path: str, *, rw: bool = False) -> int | None:
    """Open a HID path. Zero access still permits descriptor queries, which
    matters because a gamepad may already be held exclusively by a game."""
    access = (GENERIC_READ | GENERIC_WRITE) if rw else 0
    handle = kernel32.CreateFileW(
        path, access, FILE_SHARE_READ | FILE_SHARE_WRITE, None, OPEN_EXISTING, 0, None
    )
    return None if handle == INVALID_HANDLE_VALUE else handle


def _string(handle: int, fn) -> str:
    buf = ctypes.create_unicode_buffer(256)
    return buf.value if fn(handle, buf, 512) else ""


def describe(path: str) -> dict | None:
    handle = open_device(path)
    if handle is None:
        return None
    try:
        attrs = HIDD_ATTRIBUTES()
        attrs.Size = ctypes.sizeof(HIDD_ATTRIBUTES)
        hid.HidD_GetAttributes(handle, ctypes.byref(attrs))

        info = {
            "path": path,
            "vid": attrs.VendorID,
            "pid": attrs.ProductID,
            "version": attrs.VersionNumber,
            "manufacturer": _string(handle, hid.HidD_GetManufacturerString),
            "product": _string(handle, hid.HidD_GetProductString),
            "serial": _string(handle, hid.HidD_GetSerialNumberString),
        }

        preparsed = ctypes.c_void_p()
        if hid.HidD_GetPreparsedData(handle, ctypes.byref(preparsed)):
            caps = HIDP_CAPS()
            if hid.HidP_GetCaps(preparsed, ctypes.byref(caps)) == HIDP_STATUS_SUCCESS:
                info.update(
                    usage_page=caps.UsagePage,
                    usage=caps.Usage,
                    input_len=caps.InputReportByteLength,
                    output_len=caps.OutputReportByteLength,
                    feature_len=caps.FeatureReportByteLength,
                )
            hid.HidD_FreePreparsedData(preparsed)
        return info
    finally:
        kernel32.CloseHandle(handle)


def is_vendor(info: dict) -> bool:
    return info.get("usage_page", 0) >= 0xFF00


def render(info: dict) -> str:
    flag = "   <<< VENDOR-DEFINED" if is_vendor(info) else ""
    lines = [
        info["path"],
        f"    VID_{info['vid']:04X}  PID_{info['pid']:04X}  rev {info['version']:04X}",
        f"    manufacturer : {info['manufacturer']}",
        f"    product      : {info['product']}",
    ]
    if info["serial"]:
        lines.append(f"    serial       : {info['serial']}")
    if "usage_page" in info:
        lines.append(
            f"    usage        : page 0x{info['usage_page']:04X} usage 0x{info['usage']:02X}{flag}"
        )
        lines.append(
            f"    report bytes : input={info['input_len']} "
            f"output={info['output_len']} feature={info['feature_len']}"
        )
    return "\n".join(lines)


def probe_features(path: str) -> None:
    """Read-only sweep of feature report ids. Never writes to the device."""
    info = describe(path)
    if info is None:
        raise SystemExit(f"cannot open {path}")
    length = info.get("feature_len", 0)
    if length == 0:
        print("this collection declares no feature reports; nothing to probe")
        return

    handle = open_device(path, rw=True) or open_device(path)
    if handle is None:
        raise SystemExit("cannot open device for probing")
    print(f"probing feature report ids, buffer length {length}\n")
    try:
        for report_id in range(0x100):
            buf = ctypes.create_string_buffer(length)
            buf[0] = report_id
            if hid.HidD_GetFeature(handle, buf, length):
                payload = bytes(buf.raw)
                if any(payload[1:]):
                    print(f"  id 0x{report_id:02X} -> {payload.hex(' ')}")
    finally:
        kernel32.CloseHandle(handle)


def watch(path: str) -> None:
    """Print input reports as they arrive. Useful for mapping buttons to bits."""
    info = describe(path)
    if info is None:
        raise SystemExit(f"cannot open {path}")
    length = info.get("input_len", 0)
    if length == 0:
        raise SystemExit("collection has no input reports")

    handle = open_device(path, rw=True)
    if handle is None:
        raise SystemExit("cannot open for reading; a game may hold it exclusively")
    print(f"reading {length}-byte input reports, ctrl-c to stop\n")
    previous = None
    try:
        while True:
            buf = ctypes.create_string_buffer(length)
            read = wintypes.DWORD(0)
            if kernel32.ReadFile(handle, buf, length, ctypes.byref(read), None):
                payload = bytes(buf.raw[: read.value])
                if payload != previous:
                    print(f"{time.strftime('%H:%M:%S')}  {payload.hex(' ')}")
                    previous = payload
    except KeyboardInterrupt:
        pass
    finally:
        kernel32.CloseHandle(handle)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vendor", action="store_true", help="only vendor-defined collections")
    parser.add_argument("--vid", help="filter by vendor id, hex, e.g. 045E")
    parser.add_argument("--probe", metavar="PATH", help="read-only feature report sweep")
    parser.add_argument("--watch", metavar="PATH", help="dump input reports live")
    args = parser.parse_args()

    if args.probe:
        return probe_features(args.probe)
    if args.watch:
        return watch(args.watch)

    wanted = int(args.vid, 16) if args.vid else None
    shown = 0
    for path in interface_paths():
        info = describe(path)
        if info is None:
            continue
        if wanted is not None and info["vid"] != wanted:
            continue
        if args.vendor and not is_vendor(info):
            continue
        print(render(info))
        print()
        shown += 1
    print(f"{shown} collection(s)")


if __name__ == "__main__":
    main()
