"""Single-event-loop owner for controller access and desktop settings operations."""

from __future__ import annotations

import asyncio
import json
import logging
import os
import threading
from dataclasses import asdict
from pathlib import Path

from x20ctl import __version__, protocol as p
from x20ctl.client import ControllerError, X20, find_controllers
from x20ctl.input import XInputReader, MacroRecorder
from .settings import (
    TARGETS,
    curve_to_ui,
    curve_from_ui,
    macro_from_ui,
    macro_to_ui,
    number,
    validate_profile,
    import_profile,
)

log = logging.getLogger(__name__)


class DeviceService:
    def __init__(self, client_factory=X20, scanner=find_controllers, directory=None):
        self.pad = None
        self._factory = client_factory
        self._scanner = scanner
        self._found = {}
        self._lock = asyncio.Lock()
        self._directory = Path(
            directory
            or Path(os.environ.get("APPDATA", str(Path.home()))) / "x20ctl" / "desktop"
        )
        self._reader = XInputReader()
        self._recorder = None
        self._record_task = None
        self._record_error = None

    def connected(self):
        return bool(self.pad and self.pad._client and self.pad._client.is_connected)

    def require_pad(self):
        if not self.connected():
            raise ControllerError(
                "Controller disconnected. Scan and connect before writing settings."
            )
        return self.pad

    async def dispatch(self, operation, payload):
        if operation == "input":
            return self.input_state()
        if operation == "check_updates":
            return await self.check_updates(payload)
        async with self._lock:
            actions = {
                "bootstrap": self.bootstrap,
                "scan": self.scan,
                "connect": self.connect,
                "disconnect": self.disconnect,
                "read": self.read,
                "apply": self.apply,
                "reset": self.reset,
                "save_profiles": self.save_profiles,
                "record_start": self.record_start,
                "record_stop": self.record_stop,
                "import_profile": self.import_profile,
                "set_updates": self.set_updates,
            }
            if operation not in actions:
                raise ValueError("Unknown desktop operation")
            return await actions[operation](payload)

    async def bootstrap(self, _payload):
        path = self._directory / "profiles.json"
        warning = None
        try:
            saved = (
                json.loads(path.read_text(encoding="utf-8")) if path.exists() else []
            )
            if not isinstance(saved, list) or len(saved) > 100:
                raise ValueError("Invalid saved profile collection")
            for profile in saved:
                validate_profile(profile)
        except (ValueError, KeyError, TypeError, OSError) as exc:
            saved = []
            warning = f"Saved setups could not be loaded: {exc}. Original file preserved; repair it before saving."
        self._storage_error = warning
        return {
            "version": __version__,
            "profiles": saved,
            "targets": TARGETS,
            "warning": warning,
            "updatesEnabled": self._updates_enabled(),
        }

    def _updates_enabled(self):
        path = self._directory / "preferences.json"
        try:
            return (
                json.loads(path.read_text(encoding="utf-8")).get("updatesEnabled", True)
                is True
            )
        except (OSError, ValueError, AttributeError):
            return True

    async def set_updates(self, payload):
        if not isinstance(payload.get("enabled"), bool):
            raise ValueError("Expected an update preference")
        self._directory.mkdir(parents=True, exist_ok=True)
        path = self._directory / "preferences.tmp"
        path.write_text(
            json.dumps({"updatesEnabled": payload["enabled"]}), encoding="utf-8"
        )
        path.replace(self._directory / "preferences.json")
        return payload["enabled"]

    async def check_updates(self, _payload):
        if not self._updates_enabled():
            return None
        from x20ctl.gui.updates import fetch_latest
        from packaging.version import Version

        try:
            release = await asyncio.to_thread(fetch_latest)
            if Version(release.version.lstrip("v")) > Version(__version__):
                return {"version": release.version}
        except Exception:
            log.info("Optional release check unavailable")
        return None

    async def import_profile(self, payload):
        return import_profile(payload["profile"])

    async def save_profiles(self, payload):
        if getattr(self, "_storage_error", None):
            raise ValueError(self._storage_error)
        profiles = payload["profiles"]
        if not isinstance(profiles, list) or len(profiles) > 100:
            raise ValueError("Expected at most 100 profiles")
        for profile in profiles:
            validate_profile(profile)
        self._directory.mkdir(parents=True, exist_ok=True)
        temporary = self._directory / "profiles.tmp"
        temporary.write_text(json.dumps(profiles, indent=2), encoding="utf-8")
        temporary.replace(self._directory / "profiles.json")
        return {"saved": len(profiles)}

    async def scan(self, _payload):
        self._found = {
            device.address: device for device in await self._scanner(timeout=5)
        }
        return [asdict(device) for device in self._found.values()]

    async def connect(self, payload):
        address = payload["address"]
        if address not in self._found:
            raise ValueError("Select a controller returned by Scan")
        await self.disconnect({})
        candidate = self._factory(address)
        try:
            await candidate.connect()
            await candidate.device_info()
            await candidate.capabilities()
            self.pad = candidate
            return await self.read({})
        except Exception:
            await candidate.disconnect()
            self.pad = None
            raise

    async def disconnect(self, _payload):
        if self._recorder:
            self._recorder.stop()
            self._recorder = None
        if self._record_task:
            self._record_task.cancel()
            await asyncio.gather(self._record_task, return_exceptions=True)
            self._record_task = None
        pad, self.pad = self.pad, None
        if pad:
            await pad.disconnect()
        return {"connected": False}

    async def read(self, _payload):
        pad = self.require_pad()
        device = await pad.device_info()
        caps = await pad.capabilities()
        result = {
            "connected": True,
            "device": asdict(device),
            "name": await pad.name(),
            "capabilities": asdict(caps),
            "values": {},
            "warnings": [],
            "battery": None,
        }
        values = result["values"]
        reads = []
        if caps.changekey:
            reads.append(("remaps", self._read_remaps))
        if caps.sticks:
            reads.append(("stickCurves", lambda: self._read_curves("sticks")))
        if caps.triggers:
            reads.append(("triggerCurves", lambda: self._read_curves("triggers")))
        if caps.motors:
            reads.extend(
                [
                    ("vibration", pad.vibration),
                    ("idleTimeoutMinutes", pad.shutdown_timeout),
                ]
            )
        if caps.macros:
            reads.append(("macros", self._read_macros))
        for key, reader in reads:
            try:
                values[key] = await reader()
            except Exception as exc:
                result["warnings"].append(f"{key}: {exc}")
        if "vibration" in values:
            result["motors"] = values["vibration"]
            values["vibration"] = values["vibration"][0]
        if "idleTimeoutMinutes" in values:
            values["idleTimeoutMinutes"] = values["idleTimeoutMinutes"] or 0
        if "macros" in values:
            values["macros"], values["macroLoops"] = values["macros"]
        try:
            battery = await pad.battery()
            result["battery"] = asdict(battery) if battery else None
        except Exception as exc:
            result["warnings"].append(f"Battery unavailable: {exc}")
        self.require_pad()
        return result

    async def _read_remaps(self):
        pad = self.require_pad()
        sources = await pad.changekey_sources()
        changes = await pad.remappings(sources)
        return {
            p.Key(k).name: p.Key(changes.get(k, k)).name
            for k in sources
            if p.Key(k).name in TARGETS
        }

    async def _read_curves(self, kind):
        curves = await self.require_pad().curves(kind)
        if len(curves) != 2:
            raise ControllerError("This editor requires two curve channels")
        return dict(zip(("left", "right"), map(curve_to_ui, curves)))

    async def _read_macros(self):
        pad = self.require_pad()
        layout = await pad.macro_layout()
        caps = await pad.capabilities()
        rows, loops = {}, {}
        for index in range(1, 5):
            program = (
                await pad.read_macro(index, strict=True)
                if f"M{index}" in caps.macro_slots
                else None
            )
            rows[f"M{index}"] = macro_to_ui(program, layout)
            loops[f"M{index}"] = program.loop_interval_ms if program else 0
        return rows, loops

    async def apply(self, payload):
        pad = self.require_pad()
        category, value = payload["category"], payload["value"]
        caps = await pad.capabilities()
        if category == "remaps":
            if not caps.changekey or not isinstance(value, dict):
                raise ValueError("Remapping is unavailable")
            sources = await pad.changekey_sources()
            if any(
                k not in TARGETS or v not in TARGETS or p.Key[k] not in sources
                for k, v in value.items()
            ):
                raise ValueError("Unsupported remapping")
            if any(value.get(k, k) != k for k in ("SELECT", "START")):
                raise ValueError("Select and Start cannot be remapped as sources")
            changes = {p.Key[k]: p.Key[v] for k, v in value.items()}
            actual = await pad.set_remapping(changes)
            if any(actual.get(k, k) != v for k, v in changes.items()):
                raise ControllerError(
                    "Controller did not retain the requested remapping"
                )
        elif category in ("stickCurves", "triggerCurves"):
            kind = "sticks" if category == "stickCurves" else "triggers"
            current = await pad.curves(kind)
            if len(current) != 2:
                raise ValueError("Unsupported curve channel count")
            requested = [
                curve_from_ui(value[side], old)
                for side, old in zip(("left", "right"), current)
            ]
            actual = await pad.set_curves(kind, requested)
            if actual != requested:
                raise ControllerError(
                    "Controller curve read-back differs from the requested values"
                )
        elif category == "vibration":
            requested = number(value, "Vibration", 0, 100, integer=True)
            actual = await pad.set_vibration(requested)
            if any(abs(v - requested) > 1 for v in actual):
                raise ControllerError("Controller vibration read-back differs")
        elif category == "idleTimeoutMinutes":
            requested = number(value, "Timeout", 0, 1092, integer=True) or None
            if await pad.set_shutdown_timeout(requested) != requested:
                raise ControllerError("Controller timeout read-back differs")
        elif category in ("M1", "M2", "M3", "M4"):
            index = int(category[1])
            if category not in caps.macro_slots:
                raise ValueError("This macro slot is unavailable")
            layout = await pad.macro_layout()
            loop_ms = payload.get("loopMs", 0)
            steps = macro_from_ui(value, layout, loop_ms)
            if steps:
                await pad.write_macro_steps(index, steps, loop_ms=loop_ms)
                actual = await pad.read_macro(index, strict=True)
                if (
                    actual is None
                    or actual.steps != steps
                    or actual.loop_interval_ms != loop_ms
                ):
                    raise ControllerError(
                        "Macro was sent but its read-back could not be verified"
                    )
            else:
                await pad.clear_macro(index)
                # None also means no response in the legacy client: never call it verified.
                return {
                    "status": "sent",
                    "message": f"Clear command sent to {category}; reread to inspect the slot.",
                }
        else:
            raise ValueError("Unknown settings category")
        return {"status": "verified", "message": "Controller read-back verified."}

    async def reset(self, payload):
        if payload.get("confirmation") != "RESET":
            raise ValueError("Reset requires explicit confirmation")
        await self.require_pad().factory_reset()
        await self.disconnect({})
        return {
            "status": "sent",
            "message": "Reset command sent. Reconnect to read the controller.",
        }

    def input_state(self):
        state = self._reader.poll()
        return {
            "connected": self.connected(),
            "input": None
            if state is None
            else {
                "slot": state.slot,
                "buttons": state.pressed_names,
                "leftStick": {
                    "x": state.left_stick[0] / 32768,
                    "y": -state.left_stick[1] / 32768,
                },
                "rightStick": {
                    "x": state.right_stick[0] / 32768,
                    "y": -state.right_stick[1] / 32768,
                },
                "leftTrigger": state.left_trigger / 255,
                "rightTrigger": state.right_trigger / 255,
            },
        }

    async def record_start(self, _payload):
        if self._reader.poll() is None:
            raise ControllerError("Connect an XInput gamepad before recording")
        if self._recorder:
            raise ValueError("Recording already active")
        self._recorder = MacroRecorder(self._reader)
        self._record_error = None
        self._recorder.start()
        self._record_task = asyncio.create_task(self._sample_recording())
        return {"recording": True}

    async def _sample_recording(self):
        while self._recorder:
            if self._reader.poll() is None:
                self._record_error = (
                    "Gamepad disconnected during recording. Recording discarded."
                )
                return
            if len(self._recorder.steps) >= p.MAX_MACRO_ENTRIES:
                self._record_error = "Recording exceeded the 47-entry hardware limit. Record a shorter sequence."
                return
            self._recorder.poll()
            await asyncio.sleep(0.005)

    async def record_stop(self, _payload):
        if not self._recorder:
            raise ValueError("No recording is active")
        recorder, self._recorder = self._recorder, None
        if self._record_task:
            self._record_task.cancel()
            await asyncio.gather(self._record_task, return_exceptions=True)
            self._record_task = None
        spec = recorder.stop()
        if self._record_error:
            raise ControllerError(self._record_error)
        if spec is None:
            return []
        rows = macro_to_ui(p.MacroProgram(spec.steps()))
        macro_from_ui(rows)
        return rows


class DesktopApi:
    """Only request is exposed by pywebview. Device operations share one asyncio loop."""

    def __init__(self, service=None):
        self._loop = asyncio.new_event_loop()
        self._service = service or DeviceService()
        self._window = None
        self._thread = threading.Thread(target=self._loop.run_forever, daemon=True)
        self._thread.start()

    def request(self, operation, payload=None):
        try:
            if not isinstance(operation, str) or (
                payload is not None and not isinstance(payload, dict)
            ):
                raise ValueError("Invalid desktop request")
            if len(json.dumps(payload or {})) > 2_000_000:
                raise ValueError("Request too large")
            if operation in ("open_profile", "export_profile"):
                return {
                    "ok": True,
                    "data": self._profile_dialog(operation, payload or {}),
                }
            if operation == "open_releases":
                import webbrowser

                webbrowser.open("https://github.com/AmjadAAYD/x20ctl/releases")
                return {"ok": True, "data": None}
            future = asyncio.run_coroutine_threadsafe(
                self._service.dispatch(operation, payload or {}), self._loop
            )
            return {"ok": True, "data": future.result()}
        except Exception as exc:
            log.warning("Desktop operation %s failed: %s", operation, exc)
            return {
                "ok": False,
                "error": {"code": type(exc).__name__, "message": str(exc)},
            }

    def _profile_dialog(self, operation, payload):
        import webview

        if self._window is None:
            raise ValueError("Desktop window is unavailable")
        if operation == "open_profile":
            selected = self._window.create_file_dialog(
                webview.FileDialog.OPEN, file_types=("JSON (*.json)",)
            )
            if not selected:
                return None
            path = Path(selected[0])
            if path.stat().st_size > 2_000_000:
                raise ValueError("Profile file is too large")
            return import_profile(json.loads(path.read_text(encoding="utf-8-sig")))
        profile = validate_profile(payload["profile"])
        selected = self._window.create_file_dialog(
            webview.FileDialog.SAVE,
            save_filename="x20ctl-setup.json",
            file_types=("JSON (*.json)",),
        )
        if not selected:
            return None
        path = Path(selected if isinstance(selected, str) else selected[0])
        path.write_text(json.dumps(profile, indent=2), encoding="utf-8")
        return {"saved": True}

    def _close(self):
        try:
            asyncio.run_coroutine_threadsafe(
                self._service.disconnect({}), self._loop
            ).result(timeout=8)
        except Exception:
            log.exception("Disconnect during shutdown")
        self._loop.call_soon_threadsafe(self._loop.stop)
        self._thread.join(timeout=2)
