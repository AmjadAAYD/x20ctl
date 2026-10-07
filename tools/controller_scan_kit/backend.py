"""Bound hardware calls in disposable processes, keeping private IDs in memory."""
import asyncio
import json
from pathlib import Path
import subprocess
import sys

from .evidence import safe_name


class Files:
    def __init__(self, directory): self.directory = Path(directory)
    def path(self, name):
        safe_name(name)
        path = self.directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if (not path.resolve().is_relative_to(self.directory.resolve()) or
                any(p.is_symlink() for p in [path, *path.parents] if p != self.directory.parent)):
            raise ValueError("Worker output must stay inside report")
        return path


def worker(request):
    if request["operation"] == "ble_scan":
        from .ble import scan
        return {"devices": [{k: v for k, v in row.items() if k != "_device"} for row in asyncio.run(scan())]}
    if request["operation"] == "ble_inspect":
        from .ble import inspect
        return asyncio.run(inspect(request["selected"]))
    from .windows import Native, XInput, XInputReader, HidReader
    from . import usb
    from .input_tests import record_action
    operation = request["operation"]
    if operation == "inventory":
        return {"devices": Native().inventory(), "xinput_slots": XInput().slots()}
    if operation == "details":
        native = Native()
        rows = request["rows"]
        for row in rows:
            if row.get("kind") == "hid":
                try: native.hid_info(row)
                except OSError: row["status"] = "metadata_access_limited"
        return {"devices": rows}
    if operation == "usb":
        result, binaries = usb.inspect(Native(), request["selected"])
        return {"metadata": result, "binaries": {name: value.hex() for name, value in binaries.items()}}
    if operation == "capture":
        reader = (XInputReader(XInput(), request["slot"]) if request["source"] == "xinput_state"
                  else HidReader(Native(), request["selected"]))
        try:
            return record_action(Files(request["directory"]), request["filename"], request["action"],
                                 reader, request["duration"])
        finally: reader.close()
    raise ValueError("Unknown worker operation")


class Backend:
    def run(self, request, timeout=25):
        entry = Path(__file__).resolve().parents[1] / "controller_scan.py"
        argv = [sys.executable, "--worker"] if getattr(sys, "frozen", False) else [sys.executable, str(entry), "--worker"]
        creationflags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
        result = subprocess.run(argv, input=json.dumps(request), capture_output=True, text=True,
                                encoding="utf-8", timeout=timeout, creationflags=creationflags)
        if result.returncode or len(result.stdout) > 4 * 1024 * 1024:
            raise OSError("Collection worker unavailable")
        payload = json.loads(result.stdout)
        if payload.get("worker_error"): raise OSError("Collection worker failed")
        return payload

    def inventory(self): return self.run({"operation": "inventory"})
    def details(self, rows): return self.run({"operation": "details", "rows": rows})["devices"]
    def usb(self, selected): return self.run({"operation": "usb", "selected": selected})
    def ble_scan(self): return self.run({"operation": "ble_scan"}, timeout=25)["devices"]
    def ble_inspect(self, selected): return self.run({"operation": "ble_inspect", "selected": selected}, timeout=45)
    def capture(self, report, selected, source, slot, filename, action, duration):
        return self.run({"operation": "capture", "directory": str(report.directory.resolve()),
                         "selected": selected, "source": source, "slot": slot,
                         "filename": filename, "action": action, "duration": duration}, timeout=duration + 10)
