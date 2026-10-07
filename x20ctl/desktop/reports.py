"""Local report validation, immutable retry payloads, and consented upload."""

from __future__ import annotations

import hashlib
import json
import os
import re
import stat
import uuid
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from urllib import error, request

from x20ctl import __version__
from ._scanner_identity import SCANNER_SHA256, SCANNER_VERSION
from .scanner_integrity import packaged_scanner_path, run_scanner, verify_scanner
from .platform_support import data_directory

ENDPOINT = "https://x20-admin.vercel.app/api/controller-report"
ALLOWED = frozenset({"device.json", "usb-descriptors.txt", "hid-report-descriptor.bin",
                     "hid-report-descriptor-parsed.txt", "input-mapping.json",
                     "input-captures.json", "system.json", "scanner-version.txt", "README.txt"})
MAX_ZIP = 2 * 1024 * 1024
MAX_EXPANDED = 8 * 1024 * 1024
MAX_REQUEST = int(2.5 * 1024 * 1024)
SCAN_ID = re.compile(r"^[0-9a-f]{32}$")


def validate_output(directory: Path) -> list[Path]:
    if (directory.is_symlink() or not directory.is_dir() or
        getattr(directory.lstat(), "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)):
        raise ValueError("Report output directory is invalid")
    entries = list(directory.iterdir())
    if not entries or len(entries) > 9 or not any(p.name == "device.json" for p in entries):
        raise ValueError("Report is missing device data or has too many files")
    total = 0
    for path in entries:
        info = path.lstat()
        if (path.name not in ALLOWED or path.name in {".", ".."} or
            not stat.S_ISREG(info.st_mode) or
            getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)):
            raise ValueError("Report contains an unexpected file or link")
        total += info.st_size
        if total > MAX_EXPANDED:
            raise ValueError("Report exceeds the expanded size limit")
        with path.open("rb") as stream:
            if stream.read(4).startswith((b"PK\x03\x04", b"PK\x05\x06", b"PK\x07\x08")):
                raise ValueError("Nested archive is forbidden")
    try:
        device = json.loads((directory / "device.json").read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError) as exc:
        raise ValueError("Invalid device report") from exc
    if not isinstance(device, dict) or not isinstance(device.get("controllerName"), str):
        raise ValueError("Invalid device report")
    return sorted(entries, key=lambda p: p.name)


def make_zip(directory: Path, destination: Path) -> tuple[list[str], int, str]:
    files = validate_output(directory)
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6,
                         allowZip64=False) as archive:
        for path in files:
            data = path.read_bytes()
            if len(data) > MAX_EXPANDED:
                raise ValueError("Report file exceeds size limit")
            info = zipfile.ZipInfo(path.name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100600 << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED)
    data = destination.read_bytes()
    if len(data) > MAX_ZIP:
        destination.unlink()
        raise ValueError("ZIP exceeds receiver size limit")
    return [p.name for p in files], len(data), hashlib.sha256(data).hexdigest()


def upload_report(ident: str, metadata: dict, data: bytes, endpoint: str = ENDPOINT, *, max_request=MAX_REQUEST) -> dict:
    boundary = "x20ctl-" + uuid.uuid4().hex
    fields = {"clientSubmissionId": ident, "metadata": json.dumps(metadata, separators=(",", ":"))}
    body = bytearray()
    for key, value in fields.items():
        body.extend(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{key}\"\r\n\r\n{value}\r\n".encode())
    body.extend(f"--{boundary}\r\nContent-Disposition: form-data; name=\"report\"; filename=\"report.zip\"\r\nContent-Type: application/zip\r\n\r\n".encode())
    body.extend(data)
    body.extend(f"\r\n--{boundary}--\r\n".encode())
    if len(body) > max_request:
        raise ValueError("Report request exceeds receiver limit")
    outgoing = request.Request(endpoint, bytes(body), {"Content-Type": f"multipart/form-data; boundary={boundary}"}, method="POST")
    try:
        with request.urlopen(outgoing, timeout=20) as response:
            if response.status not in (200, 201):
                raise ValueError(f"Receiver returned HTTP {response.status}")
            result = json.loads(response.read(4096))
    except error.HTTPError as exc:
        raise ValueError(f"Receiver returned HTTP {exc.code}") from exc
    if not isinstance(result, dict) or not result.get("success") or not re.fullmatch(r"CR-[0-9A-F]{24}", str(result.get("submissionId", ""))):
        raise ValueError("Receiver returned an invalid submission ID")
    return {"submissionId": result["submissionId"]}


class ReportWorkflow:
    def __init__(self, root: Path | None = None, scanner_path: Path | None = None,
                 approved_hash: str | None = SCANNER_SHA256, runner=run_scanner, uploader=upload_report):
        self.root = Path(root or data_directory() / "Reports")
        self.scanner_path = scanner_path or packaged_scanner_path()
        self.approved_hash = approved_hash
        self.runner = runner
        self.uploader = uploader

    def prepare(self, address: str, name: str) -> dict:
        if not isinstance(name, str) or not name.strip() or len(name) > 120:
            raise ValueError("Selected controller name is invalid")
        scan_id = uuid.uuid4().hex
        folder = self.root / scan_id
        output = folder / "output"
        output.mkdir(parents=True, exist_ok=False)
        try:
            digest = self.runner(self.scanner_path, address, name, output, self.approved_hash)
            validate_output(output)
            device = json.loads((output / "device.json").read_text(encoding="utf-8"))
            if device.get("controllerName") != name:
                raise ValueError("Scanner output does not match selected controller")
            (output / "system.json").write_text(json.dumps({
                "reportSchemaVersion": 1, "x20ctlVersion": __version__,
                "scannerVersion": SCANNER_VERSION, "scannerSha256": digest,
            }, indent=2), encoding="utf-8")
            files, size, zip_hash = make_zip(output, folder / "report.zip")
            ident = str(uuid.uuid4())
            manifest = {
                "scanId": scan_id, "clientSubmissionId": ident,
                "metadata": {"controllerName": name, "appVersion": __version__,
                             "scannerVersion": SCANNER_VERSION,
                             "clientTimestamp": datetime.now(timezone.utc).isoformat(),
                             "connectionType": "Bluetooth LE"},
                "scannerSha256": digest, "zipSha256": zip_hash,
                "files": files, "size": size, "vid": device.get("vid"), "pid": device.get("pid"),
                "details": {key: value for key, value in device.items() if key != "address"},
                "sent": False,
            }
            (folder / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
            return self._preview(manifest)
        except Exception:
            # Preserve unsuccessful output for local troubleshooting, but it is never uploadable.
            raise

    @staticmethod
    def _preview(manifest: dict) -> dict:
        return ({key: manifest[key] for key in ("scanId", "files", "size", "vid", "pid", "details", "sent")}
                | {"controllerName": manifest["metadata"]["controllerName"],
                   "appVersion": manifest["metadata"]["appVersion"],
                   "scannerVersion": manifest["metadata"]["scannerVersion"],
                   "clientSubmissionId": manifest["clientSubmissionId"]})

    def pending(self) -> list[dict]:
        if not self.root.exists():
            return []
        found = []
        for folder in sorted(self.root.iterdir(), reverse=True):
            if not folder.is_dir() or not SCAN_ID.fullmatch(folder.name):
                continue
            try:
                manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
                if manifest.get("scanId") == folder.name and not manifest.get("sent"):
                    found.append(self._preview(manifest))
            except (OSError, ValueError, KeyError, TypeError):
                continue
        return found[:20]

    def export_bytes(self, scan_id: str) -> bytes:
        """Export only an existing verified report, never arbitrary filesystem data."""
        if not isinstance(scan_id, str) or not SCAN_ID.fullmatch(scan_id):
            raise ValueError("Invalid report identifier")
        folder = self.root / scan_id
        manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
        if manifest.get("scanId") != scan_id:
            raise ValueError("Report is unavailable")
        data = (folder / "report.zip").read_bytes()
        if len(data) > MAX_ZIP or hashlib.sha256(data).hexdigest() != manifest.get("zipSha256"):
            raise ValueError("Local report ZIP changed; export refused")
        return data

    def send(self, scan_id: str, consent: bool) -> dict:
        if consent is not True:
            raise ValueError("Explicit report upload consent is required")
        if not isinstance(scan_id, str) or not SCAN_ID.fullmatch(scan_id):
            raise ValueError("Invalid report identifier")
        folder = self.root / scan_id
        manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
        if manifest.get("scanId") != scan_id or manifest.get("sent"):
            raise ValueError("Report is unavailable for upload")
        actual = verify_scanner(self.scanner_path, self.approved_hash)
        if actual != manifest.get("scannerSha256"):
            raise ValueError("Report scanner identity no longer matches")
        data = (folder / "report.zip").read_bytes()
        if len(data) > MAX_ZIP or hashlib.sha256(data).hexdigest() != manifest.get("zipSha256"):
            raise ValueError("Local report ZIP changed; upload refused")
        result = self.uploader(manifest["clientSubmissionId"], manifest["metadata"], data)
        manifest["sent"] = True
        manifest["submissionId"] = result["submissionId"]
        temporary = folder / "manifest.tmp"
        temporary.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        temporary.replace(folder / "manifest.json")
        return result
