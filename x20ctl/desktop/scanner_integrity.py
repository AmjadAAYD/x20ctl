"""Verify the release-pinned helper before every execution."""

from __future__ import annotations

import hashlib
import hmac
import os
import stat
import subprocess
import sys
from pathlib import Path

from ._scanner_identity import SCANNER_FILENAME, SCANNER_SHA256, SCANNER_VERSION


class ScannerIntegrityError(ValueError):
    def __init__(self, detail: str):
        self.detail = detail
        super().__init__(
            "Scanner integrity verification failed. X20ctl will not run or upload this report. "
            "Reinstall or update X20ctl from its official release."
        )


def packaged_scanner_path() -> Path:
    # A source checkout has no approved helper. Only the packaged host can run it.
    return Path(sys.executable).parent / SCANNER_FILENAME


def verify_scanner(path: Path, expected_hash: str | None = SCANNER_SHA256) -> str:
    path = Path(path)
    if path.name != SCANNER_FILENAME or path != packaged_scanner_path():
        raise ScannerIntegrityError("Scanner integrity verification failed: unexpected scanner path")
    if not expected_hash or len(expected_hash) != 64:
        raise ScannerIntegrityError("Scanner integrity verification failed: no approved release identity")
    try:
        info = path.lstat()
        if not stat.S_ISREG(info.st_mode) or getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0):
            raise ScannerIntegrityError("Scanner integrity verification failed: unexpected file type")
        digest = hashlib.sha256()
        with path.open("rb") as stream:
            if os.fstat(stream.fileno()).st_ino != info.st_ino:
                raise ScannerIntegrityError("Scanner integrity verification failed: scanner changed")
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
        after = path.lstat()
        if (after.st_ino, after.st_size, after.st_mtime_ns) != (info.st_ino, info.st_size, info.st_mtime_ns):
            raise ScannerIntegrityError("Scanner integrity verification failed: scanner changed")
    except OSError as exc:
        raise ScannerIntegrityError("Scanner integrity verification failed: scanner unavailable") from exc
    if not hmac.compare_digest(digest.hexdigest(), expected_hash.lower()):
        raise ScannerIntegrityError("Scanner integrity verification failed: modified or damaged scanner")
    return digest.hexdigest()


def run_scanner(path: Path, address: str, name: str, output: Path, expected_hash: str | None = SCANNER_SHA256):
    verified_hash = verify_scanner(path, expected_hash)
    # One executable path, argument array, no shell or command interpreter.
    try:
        completed = subprocess.run(
            [str(path), "--address", address, "--name", name, "--output", str(output)],
            shell=False, capture_output=True, timeout=45,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except subprocess.TimeoutExpired as exc:
        raise ValueError("Scanner timed out. The report was not prepared.") from exc
    except OSError as exc:
        raise ValueError("Scanner could not start. The report was not prepared.") from exc
    if len(completed.stdout) > 2048 or completed.stderr and len(completed.stderr) > 2048:
        raise ValueError("Scanner returned excessive output")
    import json
    try:
        status = json.loads(completed.stdout.decode("utf-8"))
    except (UnicodeError, ValueError) as exc:
        raise ValueError("Scanner returned malformed status") from exc
    expected = {0: "complete", 10: "controller_unavailable", 11: "capability_unavailable", 20: "invalid_arguments", 30: "internal_failure"}
    if not isinstance(status, dict) or set(status) != {"schemaVersion", "status", "scannerVersion"} or (
        status.get("schemaVersion") != 1 or status.get("scannerVersion") != SCANNER_VERSION
        or status.get("status") != expected.get(completed.returncode)
    ):
        raise ValueError("Scanner returned unexpected status")
    if completed.returncode:
        raise ValueError(f"Scanner: {status['status'].replace('_', ' ')}")
    return verified_hash
