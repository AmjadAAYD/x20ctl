"""Internal same-executable worker. No WebView, tray, uploader or configuration driver."""

import json
from pathlib import Path
import queue
import subprocess
import sys
import threading
import time
import uuid


class WorkerSession:
    def __init__(self):
        command = (
            [sys.executable, "--research-worker"]
            if getattr(sys, "frozen", False)
            else [sys.executable, "-m", "x20ctl.desktop.scan_worker"]
        )
        self.process = subprocess.Popen(
            command,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            bufsize=1,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        self.replies = queue.Queue()
        self.lock = threading.Lock()
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        for line in self.process.stdout:
            if len(line) > 4 * 1024 * 1024:
                self.replies.put(None)
                break
            try:
                self.replies.put(json.loads(line))
            except ValueError:
                self.replies.put(None)
        self.replies.put(None)

    def call(self, request, timeout=25, on_progress=None):
        with self.lock:
            ident = uuid.uuid4().hex
            request = {**request, "requestId": ident}
            try:
                self.process.stdin.write(json.dumps(request) + "\n")
                self.process.stdin.flush()
                deadline = time.monotonic() + timeout
                while True:
                    reply = self.replies.get(
                        timeout=max(0.01, deadline - time.monotonic())
                    )
                    if not reply or reply.get("requestId") != ident:
                        raise OSError("Controller worker stopped")
                    if reply.get("event") == "progress":
                        if on_progress:
                            on_progress(reply["data"])
                        continue
                    break
                if reply.get("error"):
                    raise OSError(reply["error"])
                return reply["data"]
            except (OSError, queue.Empty) as error:
                self.close()
                raise OSError("Controller worker unavailable or timed out") from error

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=3)


def serve():
    # Only worker mode rebinds redirected handles in a windowed frozen build.
    if sys.platform == "win32" and (sys.stdin is None or sys.stdout is None):
        import ctypes
        import io
        import os
        import msvcrt

        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.GetStdHandle.argtypes = [ctypes.c_ulong]
        kernel.GetStdHandle.restype = ctypes.c_void_p
        if sys.stdin is None:
            fd = msvcrt.open_osfhandle(
                kernel.GetStdHandle(-10 & 0xFFFFFFFF), os.O_RDONLY
            )
            sys.stdin = io.open(fd, "r", encoding="utf-8")
        if sys.stdout is None:
            fd = msvcrt.open_osfhandle(
                kernel.GetStdHandle(-11 & 0xFFFFFFFF), os.O_WRONLY
            )
            sys.stdout = io.open(fd, "w", encoding="utf-8", buffering=1)
    reader = None
    source = None
    try:
        while True:
            line = sys.stdin.readline(1024 * 1024 + 1)
            if not line:
                break
            ident = None
            try:
                if len(line) > 1024 * 1024:
                    raise ValueError("Request limit exceeded")
                request = json.loads(line)
                ident = request.get("requestId")
                operation = request.get("operation")
                if operation == "capabilities":
                    from x20ctl.scanning import APP_VERSION
                    data = {
                        "version": APP_VERSION,
                        "phase": 1,
                        "standard_xinput_rumble": "opt_in_bounded",
                        "outbound_protocol_capture": False,
                        "hardware_access": False,
                        "rgb": False,
                        "same_executable": True,
                    }
                elif operation == "input_attach":
                    from x20ctl.scanning.windows import (
                        Native,
                        HidReader,
                        XInput,
                        XInputReader,
                    )
                    from .x15_input import known_profile

                    if reader:
                        reader.close()
                    source = request["source"]
                    if source == "hid_input" and known_profile(request["selected"]):
                        reader = HidReader(Native(), request["selected"])
                    elif (
                        source == "xinput_state"
                        and type(request.get("slot")) is int
                        and 0 <= request["slot"] <= 3
                    ):
                        reader = XInputReader(XInput(), request["slot"])
                    else:
                        raise ValueError("Unknown input profile")
                    data = {"attached": True}
                elif operation == "input_read":
                    from .x15_input import decode_hid, decode_xinput

                    if not reader:
                        raise ValueError("No selected input")
                    sample = reader.read(timeout_ms=100)
                    data = (
                        None
                        if sample is None
                        else (
                            decode_hid(bytes.fromhex(sample["report_hex"]))
                            if source == "hid_input"
                            else decode_xinput(sample["values"])
                        )
                    )
                elif operation == "capture":
                    from .platform_support import data_directory

                    destination = Path(request["directory"]).resolve()
                    if not destination.is_relative_to(
                        (data_directory() / "ResearchReports").resolve()
                    ):
                        raise ValueError(
                            "Capture directory is outside app report storage"
                        )
                    from x20ctl.scanning.backend import Files
                    from x20ctl.scanning.windows import (
                        Native,
                        HidReader,
                        XInput,
                        XInputReader,
                    )
                    from x20ctl.scanning.input_tests import record_action
                    from .x15_input import decode_hid, decode_xinput, known_profile

                    capture_reader = (
                        XInputReader(XInput(), request["slot"])
                        if request["source"] == "xinput_state"
                        else HidReader(Native(), request["selected"], request.get("vendorInput", False))
                    )

                    def progress(sample, count):
                        live = None
                        try:
                            live = (
                                decode_hid(bytes.fromhex(sample["report_hex"]))
                                if sample["source"] == "hid_input" and known_profile(request.get("selected", {}))
                                else decode_xinput(sample["values"]) if sample["source"] == "xinput_state" else None
                            )
                        except (ValueError, KeyError):
                            pass
                        print(
                            json.dumps(
                                {
                                    "requestId": ident,
                                    "event": "progress",
                                    "data": {"samples": count, "input": live},
                                }
                            ),
                            flush=True,
                        )

                    try:
                        data = record_action(
                            Files(destination),
                            request["filename"],
                            request["action"],
                            capture_reader,
                            request["duration"],
                            on_sample=progress,
                        )
                    finally:
                        capture_reader.close()
                elif operation in {
                    "inventory",
                    "details",
                    "usb",
                    "ble_scan",
                    "ble_inspect",
                    "rumble_probe",
                }:
                    from x20ctl.scanning.backend import worker

                    data = worker(request)
                else:
                    raise ValueError("Unknown worker operation")
                reply = {"requestId": ident, "data": data}
            except Exception as error:
                if reader:
                    reader.close()
                    reader = None
                reply = {
                    "requestId": ident,
                    "error": type(error).__name__ + ": controller step failed",
                }
            print(json.dumps(reply), flush=True)
    finally:
        if reader:
            reader.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(serve())
