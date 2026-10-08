"""Bound hardware calls in disposable processes, keeping private IDs in memory."""

import asyncio
from pathlib import Path

from .evidence import safe_name


class Files:
    def __init__(self, directory):
        self.directory = Path(directory)

    def path(self, name):
        safe_name(name)
        path = self.directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.resolve().is_relative_to(self.directory.resolve()) or any(
            p.is_symlink() for p in [path, *path.parents] if p != self.directory.parent
        ):
            raise ValueError("Worker output must stay inside report")
        return path


def worker(request):
    if request["operation"] == "rumble_probe":
        from x20ctl.desktop.rumble import probe
        return probe(request.get("slot"), request.get("left"), request.get("right"),
                     request.get("duration"), request.get("confirmed"))
    if request["operation"] == "ble_scan":
        from .ble import scan

        return {
            "devices": [
                {k: v for k, v in row.items() if k != "_device"}
                for row in asyncio.run(scan())
            ]
        }
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
                try:
                    native.hid_info(row)
                except OSError:
                    row["status"] = "metadata_access_limited"
        return {"devices": rows}
    if operation == "usb":
        result, binaries = usb.inspect(Native(), request["selected"])
        return {
            "metadata": result,
            "binaries": {name: value.hex() for name, value in binaries.items()},
        }
    if operation == "capture":
        reader = (
            XInputReader(XInput(), request["slot"])
            if request["source"] == "xinput_state"
            else HidReader(Native(), request["selected"], request.get("vendorInput", False))
        )
        try:
            return record_action(
                Files(request["directory"]),
                request["filename"],
                request["action"],
                reader,
                request["duration"],
            )
        finally:
            reader.close()
    raise ValueError("Unknown worker operation")


class Backend:
    def __init__(self):
        self.session = None
        self.progress = None
        self.rumble_slot = None

    def run(self, request, timeout=25):
        from x20ctl.desktop.scan_worker import WorkerSession

        if self.session is None:
            self.session = WorkerSession()
        try:
            return self.session.call(
                request, timeout=timeout, on_progress=self.progress
            )
        except Exception:
            self.close()
            raise

    def close(self):
        slot = self.rumble_slot
        if self.session:
            self.session.close()
            self.session = None
        if slot is not None:
            from x20ctl.desktop.rumble import stop
            self.rumble_slot = None
            stop(slot)

    def inventory(self):
        return self.run({"operation": "inventory"})

    def details(self, rows):
        return self.run({"operation": "details", "rows": rows})["devices"]

    def usb(self, selected):
        return self.run({"operation": "usb", "selected": selected})

    def ble_scan(self):
        return self.run({"operation": "ble_scan"}, timeout=25)["devices"]

    def ble_inspect(self, selected):
        return self.run({"operation": "ble_inspect", "selected": selected}, timeout=45)

    def rumble(self, slot, left, right, confirmed):
        if confirmed is not True or type(slot) is not int or not 0 <= slot <= 3:
            raise ValueError("Approve a selected XInput slot before rumble")
        self.rumble_slot = slot
        try:
            return self.run({"operation": "rumble_probe", "slot": slot, "left": left,
                             "right": right, "duration": .35, "confirmed": confirmed}, timeout=5)
        finally:
            self.rumble_slot = None

    def capture(self, report, selected, source, slot, filename, action, duration, vendor_input=False):
        return self.run(
            {
                "operation": "capture",
                "directory": str(report.directory.resolve()),
                "selected": selected,
                "source": source,
                "slot": slot,
                "filename": filename,
                "action": action,
                "duration": duration,
                "vendorInput": vendor_input,
            },
            timeout=duration + 10,
        )
