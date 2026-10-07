"""Selected-device exports, immutable evidence and bounded archive validation."""
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path, PurePosixPath
import re
import stat
import uuid
import zipfile

from . import VERSION

MAX_FILE = 4 * 1024 * 1024
MAX_TOTAL = 32 * 1024 * 1024
MAX_ZIP = 16 * 1024 * 1024
MAX_FILES = 128


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def clean_text(value):
    value = str(value)[:1000]
    value = re.sub(r"(?:\\\\|//)[^\n\r\"<>]*", "[network/device path omitted]", value)
    value = re.sub(r"(?i)\b(?:USB|HID|BTHENUM|BTHLEDEVICE)\\[^\s\"<>]*\\[^\s\"<>]*",
                   "[device instance omitted]", value)
    value = re.sub(r"(?i)[a-z]:[\\/][^\n\r\"<>]*", "[local path omitted]", value)
    value = re.sub(r"(?i)(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}", "[address omitted]", value)
    value = re.sub(r"[\x00-\x08\x0b-\x1f]", "", value)
    return value


def candidates(before, after):
    known = {r["_key"] for r in before}
    return [r for r in after if r["_key"] not in known]


def related(rows, selected):
    parent = selected.get("_parent")
    return [r for r in rows if parent and r.get("_parent") == parent] if parent else [selected]


def public_device(row):
    out = {}
    for key in ("kind", "manufacturer", "product", "description", "class", "driver_service",
                "usage_page", "usage", "input_len", "output_len", "feature_len", "version",
                "hardware_ids", "compatible_ids", "status"):
        if key in row:
            value = row[key]
            out[key] = [clean_text(v) for v in value] if isinstance(value, list) else (
                clean_text(value) if isinstance(value, str) else value)
    for key in ("vid", "pid"):
        if isinstance(row.get(key), int): out[key] = f"{row[key]:04X}"
    return out


def safe_name(name):
    if (not isinstance(name, str) or len(name) > 180 or
            not re.fullmatch(r"[A-Za-z0-9_./-]+", name) or
            any(part in {"", ".", ".."} for part in name.split("/")) or
            PurePosixPath(name).is_absolute() or
            PurePosixPath(name).suffix.lower() not in {".json", ".jsonl", ".txt", ".bin", ".pcap", ".pcapng", ".btsnoop"}):
        raise ValueError("Invalid evidence filename")
    return name


def validate_input(record):
    if not isinstance(record, dict) or not all(k in record for k in ("timestamp", "action", "source", "elapsed_ms")):
        raise ValueError("Invalid input record")
    if record["source"] not in {"xinput_state", "hid_input"}: raise ValueError("Unknown input source")
    if not isinstance(record["timestamp"], str) or not record["timestamp"].endswith("Z"):
        raise ValueError("Invalid input timestamp")
    datetime.fromisoformat(record["timestamp"].replace("Z", "+00:00"))
    if not isinstance(record["action"], str) or not 1 <= len(record["action"]) <= 100:
        raise ValueError("Invalid input action")
    elapsed = record["elapsed_ms"]
    if isinstance(elapsed, bool) or not isinstance(elapsed, (float, int)) or not math.isfinite(elapsed) or elapsed < 0:
        raise ValueError("Invalid input elapsed time")
    if record["source"] == "hid_input":
        text = record.get("report_hex")
        if not isinstance(text, str) or not re.fullmatch(r"(?:[0-9a-fA-F]{2}){1,4096}", text):
            raise ValueError("Invalid raw input hex")
        if "report_length" in record and record["report_length"] != len(text) // 2:
            raise ValueError("Invalid input report length")
    else:
        values = record.get("values")
        bounds = {"slot": (0, 3), "packet": (0, 0xFFFFFFFF), "buttons": (0, 65535),
                  "lt": (0, 255), "rt": (0, 255), **{k: (-32768, 32767) for k in ("lx", "ly", "rx", "ry")}}
        if not isinstance(values, dict) or "buttons" not in values: raise ValueError("Invalid input states")
        for key, value in values.items():
            if key not in bounds or type(value) is not int or not bounds[key][0] <= value <= bounds[key][1]:
                raise ValueError("Invalid input state value")


class Report:
    def __init__(self, output, claimed_model):
        self.ident = uuid.uuid4().hex
        self.directory = Path(output) / ("scan-" + self.ident)
        self.directory.mkdir(parents=True, exist_ok=False)
        self.manifest = {"schemaVersion": 1, "submissionId": self.ident,
                         "collectorVersion": VERSION, "createdAtUtc": utc_now(),
                         "claimedModel": clean_text(claimed_model), "modelDetected": False,
                         "sessions": [], "files": [], "privacy": {
                             "serialsCollected": False, "addressesExported": False,
                             "automaticUpload": False, "userReviewed": False}}
        self.write_text("RESULTS_README.txt", "Local standard-device and gameplay-input research.\n"
                        "Claimed model is not detection or a support verdict.\n"
                        "Missing/failed/skipped fields are explicit. No firmware/configuration writes.\n"
                        "Raw HID payloads are included only with explicit session permission.\n"
                        "Optional app experiments are performed by the owner in their normal settings app.\n"
                        "Opt-in app traces are not anonymized and may include unique identifiers.\n"
                        "No automatic upload. Review privately before sharing.\n")

    def path(self, name):
        safe_name(name)
        path = self.directory / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if any(p.is_symlink() for p in [path, *path.parents] if p != self.directory.parent):
            raise ValueError("Evidence path cannot contain a link")
        if not path.resolve().is_relative_to(self.directory.resolve()):
            raise ValueError("Evidence path escapes report")
        return path

    def write_bytes(self, name, data):
        if len(data) > MAX_FILE: raise ValueError("Evidence file exceeds size limit")
        with self.path(name).open("xb") as stream: stream.write(data)

    def write_text(self, name, text):
        self.write_bytes(name, text.encode("utf-8"))

    def write_json(self, name, data):
        self.write_text(name, json.dumps(data, ensure_ascii=False, indent=2) + "\n")

    def package(self):
        files = sorted(p for p in self.directory.rglob("*") if p.is_file())
        if len(files) >= MAX_FILES: raise ValueError("Too many evidence files")
        entries = []
        total = 0
        for file in files:
            name = file.relative_to(self.directory).as_posix()
            safe_name(name)
            self.path(name)
            data = file.read_bytes()
            if len(data) > MAX_FILE: raise ValueError("Evidence file exceeds size limit")
            total += len(data)
            entries.append({"path": name, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()})
        if total > MAX_TOTAL: raise ValueError("Report exceeds size limit")
        self.manifest["files"] = entries
        self.write_json("manifest.json", self.manifest)
        archive = self.directory.with_suffix(".zip")
        with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, allowZip64=False) as z:
            for file in [*files, self.directory / "manifest.json"]:
                z.write(file, file.relative_to(self.directory).as_posix())
        if archive.stat().st_size > MAX_ZIP: raise ValueError("ZIP exceeds size limit; do not send it")
        validate_archive(archive)
        return archive


def validate_archive(path):
    path = Path(path)
    if path.stat().st_size > MAX_ZIP: raise ValueError("ZIP size exceeds limit")
    with zipfile.ZipFile(path) as z:
        info = z.infolist()
        if not 1 <= len(info) <= MAX_FILES: raise ValueError("Invalid entry count")
        names = set()
        total = 0
        data = {}
        for item in info:
            name = safe_name(item.filename)
            if name.casefold() in names: raise ValueError("Duplicate ZIP filename")
            names.add(name.casefold())
            mode = item.external_attr >> 16
            if stat.S_ISLNK(mode) or item.flag_bits & 1 or item.extract_version >= 45:
                raise ValueError("Unsupported ZIP entry")
            if item.file_size > MAX_FILE: raise ValueError("Expanded file size exceeds limit")
            total += item.file_size
            if total > MAX_TOTAL: raise ValueError("Expanded archive exceeds limit")
            with z.open(item) as stream: content = stream.read(MAX_FILE + 1)
            if len(content) != item.file_size: raise ValueError("Invalid file size")
            if content.startswith((b"PK\x03\x04", b"PK\x05\x06", b"MZ")):
                raise ValueError("Nested archive/executable forbidden")
            data[name] = content
        try:
            manifest = json.loads(data["manifest.json"])
            if not isinstance(manifest, dict) or manifest.get("schemaVersion") != 1:
                raise ValueError("Unsupported manifest schema")
            if not isinstance(manifest.get("claimedModel"), str): raise ValueError("Invalid model claim")
            if manifest.get("modelDetected") is not False: raise ValueError("Model claim cannot be automatic detection")
            uuid.UUID(hex=manifest["submissionId"])
            if not isinstance(manifest["sessions"], list): raise ValueError("Invalid sessions")
            listed = manifest["files"]
            if not isinstance(listed, list): raise ValueError("Invalid file manifest")
            seen = set()
            traces = []
            raw_hid = False
            for entry in listed:
                name = safe_name(entry["path"])
                if name in seen or name == "manifest.json": raise ValueError("Invalid listed file")
                seen.add(name)
                content = data[name]
                if entry["size"] != len(content): raise ValueError("Manifest size mismatch")
                if entry["sha256"] != hashlib.sha256(content).hexdigest(): raise ValueError("Manifest hash mismatch")
                if name.endswith(".json"):
                    json.loads(content)
                elif name.endswith(".jsonl"):
                    for line in content.splitlines():
                        record = json.loads(line)
                        validate_input(record)
                        raw_hid = raw_hid or record["source"] == "hid_input"
                elif name.endswith((".pcap", ".pcapng", ".btsnoop")):
                    from .app_capture import trace_info
                    trace_info(content, "android" if name.endswith(".btsnoop") else "windows")
                    traces.append(name)
            if seen != set(data) - {"manifest.json"}: raise ValueError("Files are not fully listed in manifest")
            if raw_hid:
                privacy = manifest.get("privacy", {})
                if (privacy.get("rawHidInputIncluded") is not True
                        or privacy.get("rawHidInputMayContainIdentifiers") is not True
                        or privacy.get("rawHidInputAutomaticallyAnonymized") is not False
                        or privacy.get("serialsCollected", False) is not None
                        or privacy.get("addressesExported", False) is not None):
                    raise ValueError("Raw HID input requires explicit identifier privacy flags")
            if traces:
                capture = manifest.get("appCapture", {})
                privacy = manifest.get("privacy", {})
                if (traces != [capture.get("traceFile")] or capture.get("status") != "trace_attached_for_review"
                        or privacy.get("rawAppTraceIncluded") is not True
                        or privacy.get("rawAppTraceMayContainIdentifiers") is not True
                        or privacy.get("rawAppTraceAutomaticallyAnonymized") is not False
                        or privacy.get("serialsCollected", False) is not None
                        or privacy.get("addressesExported", False) is not None):
                    raise ValueError("App trace requires explicit provenance and identifier privacy flags")
        except (KeyError, TypeError, json.JSONDecodeError, UnicodeError) as exc:
            raise ValueError("Invalid evidence manifest or data") from exc
    return manifest
