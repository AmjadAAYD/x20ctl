"""App-owned research workflow: scoped collection, review, immutable export, consented upload."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import platform
import queue
import re
import stat
import threading
import uuid
import zipfile
from urllib import request as http

from x20ctl import __version__
from x20ctl.scanning import APP_VERSION
from x20ctl.controllers.compatibility import identify_receiver
from x20ctl.scanning.backend import Backend
from x20ctl.scanning.evidence import (
    clean_text,
    public_device,
    candidates,
    related,
    utc_now,
    validate_input,
)
from x20ctl.scanning.input_tests import ACTIONS
from .platform_support import data_directory
from .reports import ENDPOINT, upload_report

MAX_FILE = 4 * 1024 * 1024
MAX_TOTAL = 32 * 1024 * 1024
MAX_ZIP = 8 * 1024 * 1024
COVERAGE = [
    "triggers",
    "vibration",
    "provenance",
    "identity",
    "usb",
    "hid_descriptor",
    "hid_caps",
    "xinput",
    "raw_input",
    "neutral",
    "buttons",
    "sticks",
    "combined",
    "rear_outputs",
    "turbo",
    "sensors",
    "other_features",
    "battery",
    "ble",
    "configuration",
    "trace",
    "power",
    "reliability",
    "integrity",
]


def evidence_name(name):
    if (
        not isinstance(name, str)
        or len(name) > 200
        or not re.fullmatch(r"[A-Za-z0-9_./-]+", name)
        or any(part in {"", ".", ".."} for part in name.split("/"))
        or Path(name).suffix.lower()
        not in {
            ".json",
            ".jsonl",
            ".txt",
            ".bin",
            ".pcap",
            ".pcapng",
            ".btsnoop",
            ".png",
            ".jpg",
        }
    ):
        raise ValueError("Invalid evidence filename")
    if "/" in name and name.split("/")[0] not in {
        "device",
        "input",
        "usb",
        "ble",
        "attachments",
        "experiments",
    }:
        raise ValueError("Invalid evidence category")
    return name


def validate_research_archive(path):
    if path.stat().st_size > MAX_ZIP:
        raise ValueError("Report ZIP exceeds 8 MiB; remove optional attachments")
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        if not 1 <= len(entries) <= 256:
            raise ValueError("Report file count exceeded")
        files = {}
        total = 0
        for entry in entries:
            name = evidence_name(entry.filename)
            if (
                name in files
                or entry.flag_bits & 1
                or stat.S_ISLNK(entry.external_attr >> 16)
            ):
                raise ValueError("Unsupported or duplicate archive entry")
            total += entry.file_size
            if entry.file_size > MAX_FILE or total > MAX_TOTAL:
                raise ValueError("Report size exceeded")
            data = archive.read(entry)
            if data.startswith((b"MZ", b"PK\x03\x04", b"PK\x05\x06")):
                raise ValueError("Executable or nested archive forbidden")
            if name.endswith(".json"):
                json.loads(data)
            if name.endswith(".jsonl"):
                for line in data.splitlines():
                    validate_input(json.loads(line))
            files[name] = data
        manifest = json.loads(files["manifest.json"])
        if (
            manifest.get("schemaVersion") != 2
            or manifest.get("consent") is not True
            or manifest.get("reviewed") is not True
        ):
            raise ValueError("Report review/consent missing")
        listed = manifest["files"]
        if len(listed) != len({entry["path"] for entry in listed}) or {
            entry["path"] for entry in listed
        } != set(files) - {"manifest.json"}:
            raise ValueError("Report manifest coverage mismatch")
        for entry in listed:
            data = files[entry["path"]]
            if (
                len(data) != entry["size"]
                or hashlib.sha256(data).hexdigest() != entry["sha256"]
            ):
                raise ValueError("Report checksum mismatch")
        return manifest


def export_archive(output, archive, metadata):
    if metadata.get("consent") is not True or metadata.get("reviewed") is not True:
        raise ValueError("Report consent and review required")
    if output.is_symlink() or not output.is_dir():
        raise ValueError("Invalid report directory")
    files = []
    total = 0
    for path in sorted(output.rglob("*")):
        info = path.lstat()
        if path.is_symlink() or getattr(info, "st_file_attributes", 0) & getattr(
            stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0
        ):
            raise ValueError("Report links are forbidden")
        if path.is_dir():
            continue
        name = evidence_name(path.relative_to(output).as_posix())
        if name == "manifest.json":
            raise ValueError("Report already finalized")
        data = path.read_bytes()
        total += len(data)
        if len(data) > MAX_FILE or total > MAX_TOTAL or len(files) >= 255:
            raise ValueError("Report exceeds size limits")
        if data.startswith((b"MZ", b"PK\x03\x04", b"PK\x05\x06")):
            raise ValueError("Executable/nested archive forbidden")
        files.append(
            {
                "path": name,
                "size": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
        )
    manifest = {"schemaVersion": 2, **metadata, "files": files}
    temporary = archive.with_suffix(".tmp")
    with zipfile.ZipFile(temporary, "x", zipfile.ZIP_DEFLATED) as bundle:
        for entry in files:
            bundle.write(output / entry["path"], entry["path"])
        bundle.writestr("manifest.json", json.dumps(manifest))
    try:
        validate_research_archive(temporary)
    except Exception:
        temporary.unlink(missing_ok=True)
        raise
    temporary.replace(archive)
    return archive, hashlib.sha256(archive.read_bytes()).hexdigest()


def submit_research(ident, metadata, data):
    # A preflight sends no controller data and prevents posting new-schema captures
    # to a receiver that only understands the old compatibility-report contract.
    try:
        with http.urlopen(ENDPOINT, timeout=10) as response:
            capability = json.loads(response.read(4096))
        if 2 not in capability.get("reportSchemaVersions", []):
            raise ValueError("Research report schema not supported")
    except Exception as error:
        raise ValueError(
            "X20CTLADMIN needs its research-receiver update. ZIP saved; use manual sharing or retry later."
        ) from error
    return upload_report(ident, metadata, data, max_request=MAX_ZIP + 512 * 1024)


class Cancelled(Exception):
    pass


class ResearchScanner:
    def __init__(self, root=None, backend=None, uploader=submit_research):
        self.root = Path(root or data_directory() / "ResearchReports")
        self.backend = backend or Backend()
        self.uploader = uploader
        self.lock = threading.RLock()
        self.stop = threading.Event()
        self.responses = queue.Queue()
        self.view = {
            "state": "idle",
            "prompt": None,
            "receipt": None,
            "coverage": {},
            "files": [],
            "notes": [],
            "input": None,
        }
        self.folder = None
        self.report_id = None
        self.thread = None

    def status(self):
        with self.lock:
            return json.loads(json.dumps(self.view))

    def busy(self):
        return self.view["state"] in {"collecting", "waiting", "submitting"}

    def _update(self, **items):
        with self.lock:
            self.view.update(items)

    def _mark(self, stage, status, reason=""):
        with self.lock:
            previous = self.view["coverage"].get(stage, {})
            if previous.get("status") in {
                "observed",
                "observed_with_limits",
            } and status in {"skipped", "unavailable", "failed"}:
                status = "observed_with_limits"
                reason = "Some evidence collected; " + reason
            elif previous.get("status") in {
                "observed_with_limits", "unavailable", "failed",
            } and status == "observed":
                status = "observed_with_limits"
                reason = previous.get("reason", reason)
            self.view["coverage"][stage] = {
                "status": status,
                "reason": clean_text(reason),
            }

    def _write(self, name, value, binary=False):
        path = self.output / evidence_name(name)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.resolve().is_relative_to(self.output.resolve()) or any(
            p.is_symlink() for p in [path, *path.parents] if p != self.folder.parent
        ):
            raise ValueError("Evidence path escapes report")
        data = value if binary else json.dumps(value, indent=2).encode("utf-8")
        if len(data) > MAX_FILE:
            raise ValueError("Evidence exceeds 4 MiB")
        path.write_bytes(data)

    def _ask(self, text, kind="continue", choices=None):
        if self.stop.is_set():
            raise Cancelled()
        prompt = {
            "id": uuid.uuid4().hex,
            "text": text,
            "kind": kind,
            "choices": choices or [],
        }
        self._update(state="waiting", prompt=prompt, input=None)
        while not self.stop.is_set():
            try:
                answer = self.responses.get(timeout=0.2)
            except queue.Empty:
                continue
            if answer is None:
                raise Cancelled()
            self._update(state="collecting", prompt=None)
            return answer
        raise Cancelled()

    def answer(self, payload):
        with self.lock:
            prompt = self.view.get("prompt")
            if (
                not prompt
                or payload.get("promptId") != prompt["id"]
                or self.view["state"] != "waiting"
            ):
                raise ValueError("This prompt is no longer active")
            if prompt["kind"] == "file":
                raise ValueError("Use the native attachment picker")
            answer = payload.get("answer", "")
            if not isinstance(answer, str) or len(answer) > 2000:
                raise ValueError("Invalid scan answer")
            if prompt["kind"] == "choice" and answer not in {
                str(i) for i in range(len(prompt["choices"]))
            } | {"skip"}:
                raise ValueError("Select a displayed option")
            if prompt["kind"] == "yes" and answer not in {"yes", "skip"}:
                raise ValueError("Invalid scope answer")
            self.view["state"] = "collecting"
            self.responses.put(clean_text(answer))
        return {"accepted": True}

    def start(self, payload):
        with self.lock:
            if payload.get("consent") is not True:
                raise ValueError("Upfront scan/submission consent is required")
            if self.thread and self.thread.is_alive() or self.view["state"] == "review":
                raise ValueError("Finish or cancel the current scan first")
            model = payload.get("model")
            if not isinstance(model, str) or not 1 <= len(model.strip()) <= 80:
                raise ValueError("Printed controller model is required")
            self.stop = threading.Event()
            self.responses = queue.Queue()
            self.report_id = uuid.uuid4().hex
            self.ident = str(uuid.uuid4())
            self.folder = self.root / self.report_id
            self.output = self.folder / "output"
            self.output.mkdir(parents=True, exist_ok=False)
            self.options = {
                "model": clean_text(model),
                "mode": clean_text(payload.get("mode", "unknown")),
                "transport": clean_text(payload.get("transport", "unknown")),
                "rawInput": payload.get("rawInput") is True,
                "autoSubmit": payload.get("autoSubmit") is True,
                **{key: clean_text(payload.get(key) or "unknown") for key in ("firmware", "hardwareRevision", "receiverFirmware", "appName", "appVersion")},
            }
            self.attachment_scopes = {}
            self.view = {
                "state": "collecting",
                "prompt": None,
                "receipt": None,
                "reportId": self.report_id,
                "model": self.options["model"],
                "coverage": {
                    key: {"status": "not_collected", "reason": "Not reached yet"}
                    for key in COVERAGE
                },
                "files": [],
                "notes": [],
                "input": None,
                "autoSubmit": self.options["autoSubmit"],
                "error": "",
            }
            self._write(
                "intake.json",
                {
                    **self.options,
                    "modelDetected": False,
                    "evidenceType": "owner_reported",
                    "firmware": self.options["firmware"],
                    "hardwareRevision": self.options["hardwareRevision"],
                },
            )
            self._write(
                "system.json",
                {
                    "appVersion": __version__,
                    "scannerVersion": APP_VERSION,
                    "os": platform.system(),
                    "osVersion": platform.version(),
                    "architecture": platform.machine(),
                },
            )
            self._write(
                "consent.json",
                {
                    "consent": True,
                    "timestamp": utc_now(),
                    "destination": ENDPOINT
                    if self.options["autoSubmit"]
                    else "local_only",
                    "rawInput": self.options["rawInput"],
                    "reviewed": False,
                    "scopeVersion": 1,
                },
            )
            self._mark(
                "provenance",
                "observed",
                "Model/firmware/mode claims remain owner reported",
            )
            self.backend.progress = self._progress
            self.thread = threading.Thread(target=self._collect, daemon=True)
            self.thread.start()
            return self.status()

    def _progress(self, data):
        self._update(input=data.get("input"), samples=data.get("samples", 0))

    def _step(self, stage, function):
        if self.stop.is_set():
            raise Cancelled()
        try:
            result = function()
            if isinstance(result, dict) and "action" in result and result.get("status") in {
                "failed", "cancelled", "no_samples", "limit_reached",
                "no_change_observed", "unrelated_change_observed", "inconclusive_source_changed",
            } and not (result["action"] == "neutral" and result["status"] == "no_change_observed"):
                self._mark(stage, "unavailable", result.get("interpretation") or result["status"])
            else:
                self._mark(stage, "observed")
            return result
        except Cancelled:
            raise
        except Exception as error:
            self._mark(stage, "unavailable", str(error))
            return None

    def _collect(self):
        try:
            self._ask(
                "Disconnect ONLY this controller: unplug its cable/receiver, or switch it off for Bluetooth. Then continue."
            )
            before = self._step("identity", self.backend.inventory)
            self._ask(
                "Reconnect the same controller in its normal mode. Wait until connected, then continue."
            )
            after = self._step("identity", self.backend.inventory)
            selected = None
            chosen_source = None
            rows = []
            tests = []
            if before and after:
                changed = candidates(before["devices"], after["devices"])
                groups = {}
                for row in changed:
                    groups.setdefault(row.get("_parent") or row["_key"], row)
                options = list(groups.values())
                if options:
                    chosen = self._ask(
                        "Select the newly connected device that belongs to your controller. Do not guess.",
                        "choice",
                        [
                            clean_text(
                                r.get("product")
                                or r.get("description")
                                or "Controller interface"
                            )
                            for r in options
                        ],
                    )
                    if chosen != "skip":
                        selected = options[int(chosen)]
                        rows = (
                            self._step(
                                "hid_caps",
                                lambda: self.backend.details(
                                    related(after["devices"], selected)
                                ),
                            )
                            or []
                        )
                        self._write("device/hid-caps.json", {"devices": [public_device(r) for r in rows if r.get("kind") == "hid"], "originalDescriptorBytes": False})
                        self._write(
                            "device.json",
                            {
                                "modelDetected": False,
                                "receiverEvidence": [identify_receiver(r) for r in rows],
                                "devices": [public_device(r) for r in rows],
                            },
                        )
                        self._mark(
                            "identity",
                            "observed",
                            "Disconnect/reconnect and explicit user selection",
                        )
                        if selected.get("kind") == "usb":
                            usb = self._step("usb", lambda: self.backend.usb(selected))
                            if usb:
                                self._write("usb/metadata.json", usb["metadata"])
                                for name, value in usb["binaries"].items():
                                    self._write(
                                        "usb/" + name, bytes.fromhex(value), True
                                    )
                                self._mark(
                                    "usb", usb["metadata"].get("status", "unavailable")
                                )
                if not selected:
                    self._mark(
                        "identity",
                        "unavailable",
                        "No selected correlated interface; source/observations may still be recorded",
                    )
                self._mark(
                    "hid_descriptor",
                    "unavailable",
                    "Windows parsed caps are not original descriptor bytes. Import a selected USB enumeration trace if available.",
                )
                sources = [
                    {
                        "source": "xinput_state",
                        "slot": slot,
                        "label": f"XInput slot {slot + 1}",
                    }
                    for slot in sorted(
                        set(after["xinput_slots"]) - set(before["xinput_slots"])
                    )
                ]
                if self.options["rawInput"]:
                    sources += [
                        {
                            "source": "hid_input",
                            "selected": r,
                            "label": "Gameplay HID collection (raw input opted in)",
                        }
                        for r in rows
                        if r.get("kind") == "hid"
                        and r.get("usage_page") == 1
                        and r.get("usage") in (4, 5, 8)
                        and r.get("input_len")
                    ]
                else:
                    self._mark(
                        "raw_input", "skipped", "Raw input scope was not authorized"
                    )
                if sources:
                    choice = self._ask(
                        "Choose this controller's gameplay source for the guided tests.",
                        "choice",
                        [s["label"] for s in sources],
                    )
                    if choice != "skip":
                        source = sources[int(choice)]
                        chosen_source = source
                        self._mark(
                            "xinput"
                            if source["source"] == "xinput_state"
                            else "raw_input",
                            "observed",
                            "Explicit source selection",
                        )
                        tests = self._inputs(source, selected)
                if not tests:
                    for key in (
                        "neutral",
                        "buttons",
                        "sticks",
                        "combined",
                        "rear_outputs",
                        "turbo",
                        "sensors",
                    ):
                        self._mark(
                            key, "skipped", "No input source selected or tests skipped"
                        )
            from x20ctl.scanning.guided import input_sessions, write_mapping, identity_sessions, vendor_session
            tests += vendor_session(self, rows)
            if chosen_source:
                tests += input_sessions(self, chosen_source, selected, rows)
            write_mapping(self, tests)
            identity_sessions(self)
            self._write(
                "session.json", {**self.options, "tests": tests, "modelDetected": False}
            )
            self._ble()
            self._observations()
            self._experiment()
            self._attachments()
            if self.stop.is_set():
                raise Cancelled()
            for stage, outcome in list(self.view["coverage"].items()):
                if outcome["status"] == "not_collected":
                    self._mark(
                        stage,
                        "unavailable",
                        "No confirmed data for this stage on the selected interface/mode",
                    )
            self._mark(
                "reliability",
                "observed",
                "Stage failures and source loss retained; host timing is not latency/polling rate",
            )
            self._write("coverage.json", self.view["coverage"])
            self._update(
                state="review",
                prompt=None,
                input=None,
                files=self._files(),
                message="Review the files and outcomes. Finish scan creates the ZIP and submits it if authorized.",
            )
        except Cancelled:
            self._update(
                state="cancelled",
                prompt=None,
                input=None,
                message="Scan cancelled. Partial evidence remains local; nothing submitted.",
            )
            self._write("coverage.json", self.view["coverage"])
        except Exception as error:
            self._update(
                state="failed",
                prompt=None,
                input=None,
                error=clean_text(str(error)),
                message="Partial files kept locally; nothing submitted.",
            )
        finally:
            close = getattr(self.backend, "close", None)
            if close:
                close()

    def _inputs(self, source, selected):
        tests = []
        from .x15_input import known_profile

        self.hid_layout_known = known_profile(source.get("selected", {}))
        if (
            self._ask(
                "Record the buttons, sticks and triggers? Follow each prompt; choose Skip for controls you cannot test.",
                "yes",
            )
            != "yes"
        ):
            return tests
        stages = [
            (
                action,
                instruction,
                5.0,
                "neutral"
                if action == "neutral"
                else "sticks"
                if "stick" in action
                else "buttons",
            )
            for action, instruction in ACTIONS
            if action not in {"LT", "RT", "left_stick", "right_stick"}
        ]
        for side in ("left", "right"):
            for direction in ("up", "right", "down", "left", "circle"):
                stages.append(
                    (
                        f"{side}_stick_{direction}",
                        f"Move the {side.upper()} stick {direction}, hold briefly, then return to centre.",
                        5.0,
                        "sticks",
                    )
                )
        for trigger in ("LT", "RT"):
            for phase, seconds in [
                ("released", 2),
                ("quarter", 3),
                ("half", 3),
                ("three_quarters", 3),
                ("full", 3),
                ("release", 3),
                ("smooth_sweep_up", 5),
                ("smooth_sweep_down", 5),
            ]:
                stages.append(
                    (
                        f"{trigger}_{phase}",
                        f"{trigger}: move SLOWLY to {phase.replace('_', ' ')} and hold. Keep the OTHER trigger released.",
                        float(seconds),
                        "triggers",
                    )
                )
        stages += [
            (
                "combined",
                "Hold both triggers, then press A while moving the left stick. Release everything.",
                7.0,
                "combined",
            )
        ]
        for index, (action, instruction, duration, category) in enumerate(stages):
            answer = self._ask(
                instruction
                + f" Recording lasts {int(duration)} seconds. Continue when ready, or Skip."
            )
            if answer == "skip":
                tests.append({"action": action, "status": "skipped"})
                continue
            filename = f"input/session01/{index:02d}-{action}.jsonl"
            self._update(
                message=f"Recording: {instruction}",
                action=action,
                duration=duration,
                samples=0,
            )
            result = self._step(
                category,
                lambda: self.backend.capture(
                    self,
                    source.get("selected", selected),
                    source["source"],
                    source.get("slot"),
                    filename,
                    action,
                    duration,
                ),
            )
            if result:
                tests.append(result)
                if result.get("status") in {"failed", "no_samples", "limit_reached"}:
                    self._mark(category, "unavailable", result["status"])
        for action, description, category in [
            (
                "home",
                "HOME/Guide button, if present; documented XInput may not expose it",
                "buttons",
            ),
            (
                "capture",
                "Capture/C or another non-RGB extra button, if present",
                "buttons",
            ),
            (
                "rear_left",
                "rear-left programmable button; its ordinary OUTPUT is recorded, not an independent M key",
                "rear_outputs",
            ),
            (
                "rear_right",
                "rear-right programmable button; its ordinary OUTPUT is recorded, not an independent M key",
                "rear_outputs",
            ),
            (
                "turbo",
                "normal gamepad button with turbo already enabled using controls you know",
                "turbo",
            ),
            (
                "tilt",
                "controller tilt/rotation, if this mode provides motion input",
                "sensors",
            ),
        ]:
            if self._ask(f"Optional: record {description}?", "yes") != "yes":
                self._mark(category, "skipped", "Owner skipped this optional test")
                continue
            self._ask(
                "Perform the described action during the next seven seconds; no setting commands are sent."
            )
            result = self._step(
                category,
                lambda: self.backend.capture(
                    self,
                    source.get("selected", selected),
                    source["source"],
                    source.get("slot"),
                    f"input/session01/{action}.jsonl",
                    action,
                    7.0,
                ),
            )
            if result:
                tests.append(result)
        self._write(
            "input/mapping.json",
            {
                "tests": tests,
                "scope": "Tentative action correlations, not configuration commands or independent paddle IDs",
            },
        )
        self._trigger_summary()
        return tests

    @property
    def directory(self):
        # Adapter expected by the shared capture backend; always app-owned storage.
        return self.output

    def _trigger_summary(self):
        groups = {"LT": set(), "RT": set()}
        others = {"LT": set(), "RT": set()}
        baseline = []
        for path in self.output.glob("input/session01/*.jsonl"):
            neutral = path.stem.endswith("-neutral")
            if not neutral and not re.match(r"\d+-[LR]T_", path.stem):
                continue
            side = "LT" if "-LT_" in path.stem else "RT"
            for line in path.read_text().splitlines():
                row = json.loads(line)
                values = None
                if row["source"] == "xinput_state":
                    values = (row["values"]["lt"], row["values"]["rt"])
                elif self.options["model"].upper() == "X15" and self.hid_layout_known:
                    from .x15_input import decode_hid

                    try:
                        parsed = decode_hid(bytes.fromhex(row["report_hex"]))
                        values = (
                            round(parsed["leftTrigger"] * 255),
                            round(parsed["rightTrigger"] * 255),
                        )
                    except ValueError:
                        pass
                if values is not None:
                    if neutral:
                        baseline.append(values == (0, 0))
                    else:
                        groups[side].add(values[0 if side == "LT" else 1])
                        others[side].add(values[1 if side == "LT" else 0])
        released = bool(baseline) and all(baseline)
        self._write(
            "input/trigger-summary.json",
            {
                side: {
                    "values": sorted(values),
                    "baseline_released": released,
                    "status": "inconclusive_baseline"
                    if not released
                    else "inconclusive_other_trigger"
                    if any(others[side])
                    else "intermediate_values_observed"
                    if any(0 < v < 255 for v in values)
                    else "endpoints_only_observed"
                    if values == {0, 255}
                    else "inconclusive",
                    "physical_sensor_type": "unverified",
                }
                for side, values in groups.items()
            },
        )

    def _ble(self):
        if (
            self._ask(
                "Optional: discover this controller's Bluetooth LE services and standard battery/device information?",
                "yes",
            )
            != "yes"
        ):
            self._mark("ble", "skipped", "Owner skipped Bluetooth LE")
            self._mark(
                "battery",
                "unavailable",
                "No battery value exposed on the collected input path",
            )
            return
        self._ask(
            "Switch ONLY this controller off, leaving PC Bluetooth on. Continue for the off inventory."
        )
        off = self._step("ble", self.backend.ble_scan) or []
        self._ask(
            "Switch this controller on in a normal mode you know; continue for the on inventory. No automatic pairing."
        )
        on = self._step("ble", self.backend.ble_scan) or []
        before = {r["_key"] for r in off}
        options = [r for r in on if r["_key"] not in before]
        from x20ctl.scanning.model_evidence import ble_hints
        for row in options:
            row["researchHint"] = ble_hints(self.options["model"], row)
        if not options:
            self._mark(
                "ble",
                "unavailable",
                "No correlated new BLE peripheral; do not guess a nearby device",
            )
            return
        selected = self._ask(
            "Choose your newly appeared BLE peripheral, or Skip if unsure.",
            "choice",
            [clean_text(r["name"]) + (" · discovery candidate" if r["researchHint"]["families"] else "") for r in options],
        )
        if selected == "skip":
            self._mark("ble", "skipped", "No target confirmed")
            return
        item = options[int(selected)]
        self._write(
            "ble/advertisement.json",
            {k: v for k, v in item.items() if not k.startswith("_")},
        )
        data = self._step("ble", lambda: self.backend.ble_inspect(item))
        if data:
            self._write("ble/gatt.json", data)
            self._write("ble/research-hints.json", ble_hints(self.options["model"], item, data))
            self._mark(
                "battery",
                "observed"
                if "battery_level" in data.get("standard_values", {})
                else "unavailable",
                "Allowlisted standard values only",
            )

    def _observations(self):
        self._mark(
            "configuration",
            "unavailable",
            "No generic configuration API; use optional owner-led non-RGB experiment",
        )
        self._mark(
            "other_features",
            "owner_reported",
            "Owner observations, not measured support",
        )
        observations = {}
        for category, text in [
            (
                "vibration",
                "Vibration check: using a game or an already-working app, describe whether the left and right grips vibrate, and any missing or uneven feedback. Enter not tested if you cannot check. The scanner sends no motor commands.",
            ),
            (
                "power",
                "Optional sleep/wake, charging and reconnect observations; ENTER if unknown.",
            ),
            (
                "other_features",
                "Optional display, touchpad, audio, trigger-stop or other NON-RGB feature observations; ENTER if unknown.",
            ),
        ]:
            answer = self._ask(
                text + " Do not include personal details or serials.", "text"
            )
            observations[category] = {
                "evidenceType": "owner_reported",
                "notes": answer or "unknown",
            }
            self._mark(
                category,
                "owner_reported" if answer else "unavailable",
                "Owner observation only" if answer else "No observation supplied",
            )
        self._write("observations.json", observations)

    def _experiment(self):
        if self.options["model"].strip().lower() in {"x15", "x10"}:
            from x20ctl.scanning.protocol_session import collect
            return collect(self)
        if (
            self._ask(
                "Optional: record one non-RGB setting change in an app you ALREADY use successfully with this controller? No firmware, reset or calibration.",
                "yes",
            )
            != "yes"
        ):
            self._mark("configuration", "skipped", "No existing-app experiment")
            return
        setting = self._ask(
            "Name one reversible NON-RGB setting, such as a mapping. No lighting, firmware, reset or calibration.",
            "text",
        )
        if not setting or re.search(
            r"rgb|light|colour|color|firmware|reset|calibrat|flash|upgrade",
            setting,
            re.I,
        ):
            self._mark(
                "configuration",
                "skipped",
                "Excluded or empty setting; keep settings unchanged",
            )
            return
        app = self._ask("App name/version and phone/PC platform, if known.", "text")
        original = self._ask("Original setting value, so you can restore it.", "text")
        changed = self._ask("Different test value.", "text")
        if not original or not changed or original == changed:
            self._mark("configuration", "skipped", "No known reversible change")
            return
        events = []
        for name, instruction in [
            (
                "baseline",
                "Start your already-available capture tool if using one, connect the working settings app and leave settings unchanged for five seconds.",
            ),
            (
                "change",
                f"In YOUR app, change only {setting} from {original} to {changed}. Wait five seconds, then continue.",
            ),
            (
                "restore",
                f"Restore {setting} to {original} in YOUR app, then continue. The scanner sends no commands.",
            ),
        ]:
            answer = self._ask(
                instruction + " Skip/cancel if you cannot do this safely."
            )
            events.append(
                {"event": name, "confirmed": answer != "skip", "timestamp": utc_now()}
            )
            self._write(
                "experiments/timeline.json",
                {
                    "app": app,
                    "setting": setting,
                    "original": original,
                    "test": changed,
                    "events": events,
                    "commandsVerified": False,
                },
            )
            if answer == "skip":
                self._mark(
                    "configuration",
                    "unavailable",
                    "Experiment incomplete; restoration must be checked by owner",
                )
                return
        self._mark(
            "configuration",
            "owner_reported",
            "Owner confirmed change/restore; protocol still unverified",
        )

    def _attachments(self):
        for category, question in [
            (
                "trace",
                "Optional: attach ONE existing USBPcap, Windows BTVS HCI PCAP/PCAPNG or Android Bluetooth log. Raw traces may contain IDs, pairing data or unrelated traffic. No logging driver is installed.",
            ),
        ]:
            if self._ask(question + " Include it in this report?", "yes") != "yes":
                self._mark(category, "skipped", "Attachment not authorized")
                continue
            answer = self._ask(
                "Choose the local file using the picker. Maximum 4 MiB for this report; larger originals stay separate.",
                "file",
                [category],
            )
            self._mark(
                category,
                "observed" if answer == "attached" else "skipped",
                "Owner selected and included a local file"
                if answer == "attached"
                else "No file included",
            )

    def attach_file(self, prompt_id, path):
        prompt = self.view.get("prompt")
        if not prompt or prompt["id"] != prompt_id or prompt["kind"] != "file":
            raise ValueError("Attachment prompt is no longer active")
        if path is None:
            self._update(state="collecting")
            self.responses.put("skip")
            return {"attached": False}
        path = Path(path)
        if path.is_symlink() or path.stat().st_size > MAX_FILE:
            raise ValueError(
                "Attachment must be a regular file under 4 MiB; keep larger originals separate"
            )
        data = path.read_bytes()
        category = prompt["choices"][0]
        if category == "trace":
            from x20ctl.scanning.app_capture import trace_info

            info = trace_info(
                data, "android" if data.startswith(b"btsnoop\0") else "windows"
            )
            extension = info["extension"]
            self._write(
                "attachments/trace-info.json",
                {**info, "identifiersPossible": True, "protocolMeaning": "unverified"},
            )
        else:
            raise ValueError(
                "Photo collection is not supported; only protocol traces can be attached"
            )
        self._write("attachments/trace-association.json", {
            **{key: self.options.get(key, "unknown") for key in ("model", "firmware", "hardwareRevision", "receiverFirmware", "appName", "appVersion", "mode", "transport")},
            "evidenceType": "owner_reported_context", "traceFormat": info["format"],
            "traceTransport": info["transport"], "sha256": hashlib.sha256(data).hexdigest(),
            "actionTimeline": [str(p.relative_to(self.output)).replace("\\", "/") for p in sorted((self.output / "experiments").glob("*timeline*.json"))],
            "commandProtocolVerified": False, "targetIdentityVerified": False,
        })
        self._write(f"attachments/{category}.{extension}", data, True)
        from x20ctl.scanning.capability_discovery import analyze_trace

        capabilities = analyze_trace(data)
        self._write("attachments/capability-profile.json", capabilities)
        self._update(detectedCapabilities=capabilities)
        self.attachment_scopes[category] = True
        self._update(state="collecting")
        self.responses.put("attached")
        return {"attached": True}

    def _files(self):
        return [
            {"path": p.relative_to(self.output).as_posix(), "size": p.stat().st_size}
            for p in sorted(self.output.rglob("*"))
            if p.is_file()
        ]

    def remove_attachment(self, name):
        if (
            self.view["state"] != "review"
            or not isinstance(name, str)
            or not name.startswith("attachments/")
        ):
            raise ValueError("Only optional attachments can be removed during review")
        path = self.output / evidence_name(name)
        if not path.resolve().is_relative_to(self.output.resolve()):
            raise ValueError("Invalid attachment")
        path.unlink()
        if name.startswith("attachments/trace."):
            # Derived claims belong to this optional trace; remove them with it.
            derived = self.output / "attachments/capability-profile.json"
            derived.unlink(missing_ok=True)
            self._update(detectedCapabilities=None)
        elif name == "attachments/capability-profile.json":
            self._update(detectedCapabilities=None)
        self._update(files=self._files())
        return self.status()

    def finish(self, payload):
        if self.view["state"] != "review" or payload.get("reviewed") is not True:
            raise ValueError("Complete the scan and confirm report review first")
        self._mark("integrity", "observed", "Files validated, hashed and archived")
        self._write("coverage.json", self.view["coverage"])
        self._write(
            "consent.json",
            {
                "consent": True,
                "autoSubmit": self.options["autoSubmit"],
                "rawInput": self.options["rawInput"],
                "reviewed": True,
                "timestamp": utc_now(),
                "destination": ENDPOINT,
            },
        )
        summary = {
            "model": self.options["model"],
            "coverage": self.view["coverage"],
            "limits": "Gameplay inputs and owner claims do not establish configuration support; host timing is not controller latency.",
        }
        self._write("SUMMARY.txt", json.dumps(summary, indent=2).encode(), True)
        self.archive, self.digest = export_archive(
            self.output,
            self.folder / "report.zip",
            {
                "consent": True,
                "reviewed": True,
                "clientSubmissionId": self.ident,
                "modelDetected": False,
                "rawInput": self.options["rawInput"],
                "autoSubmit": self.options["autoSubmit"],
                "rawTrace": self.attachment_scopes.get("trace", False),
                "media": False,
            },
        )
        self.metadata = {
            "controllerName": self.options["model"],
            "appVersion": __version__,
            "scannerVersion": "1.1.0-app",
            "connectionType": self.options["transport"],
            "clientTimestamp": utc_now(),
            "reportSchemaVersion": 2,
        }
        self._update(
            state="submitting" if self.options["autoSubmit"] else "saved",
            size=self.archive.stat().st_size,
            sha256=self.digest,
            files=self._files(),
            prompt=None,
        )
        self._persist()
        if self.options["autoSubmit"]:
            self.thread = threading.Thread(target=self._submit, daemon=True)
            self.thread.start()
        return self.status()

    def _persist(self):
        temporary = self.folder / "record.tmp"
        temporary.write_text(
            json.dumps(
                {
                    "id": self.report_id,
                    "clientSubmissionId": self.ident,
                    "metadata": self.metadata,
                    "sha256": self.digest,
                    "consent": True,
                    "reviewed": True,
                    "state": self.view["state"],
                    "receipt": self.view["receipt"],
                }
            )
        )
        temporary.replace(self.folder / "record.json")

    def _submit(self):
        try:
            result = self.uploader(
                self.ident, self.metadata, self.export_bytes(self.report_id)
            )
            if not re.fullmatch(
                r"CR-[0-9A-F]{24}", str(result.get("submissionId", ""))
            ):
                raise ValueError("Receiver returned no valid durable receipt")
            self._update(state="submitted", receipt=result["submissionId"], error="")
        except Exception as error:
            self._update(
                state="upload_failed",
                error=clean_text(str(error)),
                message="ZIP saved locally. Retry or share manually.",
            )
        self._persist()

    def retry(self):
        if self.view["state"] != "upload_failed":
            raise ValueError("No failed submission to retry")
        self._update(state="submitting", error="")
        self.thread = threading.Thread(target=self._submit, daemon=True)
        self.thread.start()
        return self.status()

    def export_bytes(self, ident):
        if not isinstance(ident, str) or not re.fullmatch(r"[0-9a-f]{32}", ident):
            raise ValueError("Invalid research report ID")
        folder = self.root / ident
        if folder.is_symlink() or not folder.resolve().is_relative_to(
            self.root.resolve()
        ):
            raise ValueError("Invalid report location")
        record = json.loads((folder / "record.json").read_text())
        path = folder / "report.zip"
        if path.is_symlink() or path.stat().st_size > MAX_ZIP:
            raise ValueError("Invalid report ZIP")
        data = path.read_bytes()
        if (
            hashlib.sha256(data).hexdigest() != record["sha256"]
            or record.get("consent") is not True
            or record.get("reviewed") is not True
        ):
            raise ValueError("Reviewed report changed; export/submission refused")
        validate_research_archive(path)
        return data

    def history(self):
        rows = []
        if self.root.exists():
            for path in sorted(self.root.glob("*/record.json"), reverse=True):
                if path.parent.is_symlink():
                    continue
                try:
                    record = json.loads(path.read_text())
                    if re.fullmatch(r"[0-9a-f]{32}", record["id"]):
                        rows.append(
                            {
                                "id": record["id"],
                                "model": record["metadata"]["controllerName"],
                                "state": record["state"],
                                "receipt": record["receipt"],
                            }
                        )
                except (ValueError, KeyError, OSError):
                    continue
        return rows[:100]

    def cancel(self):
        if self.view["state"] == "submitting":
            raise ValueError(
                "Submission has started; a transmitted request cannot be undone"
            )
        self.stop.set()
        self.responses.put(None)
        close = getattr(self.backend, "close", None)
        if close:
            try:
                close()
            except OSError as error:
                self._update(error=clean_text(str(error)))
        self._update(
            state="cancelled",
            prompt=None,
            input=None,
            message="Cancelled. Partial evidence kept locally; no upload.",
        )
        return self.status()

    def close(self):
        if self.busy() and self.view["state"] != "submitting":
            self.cancel()
