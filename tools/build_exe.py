"""Build and verify the paired Windows release archive.

    python tools/build_exe.py

Builds the scanner first, embeds its SHA-256 in the desktop host, then
packages both executables in dist/x20ctl-<version>-win-x64.zip.
End users need neither Python nor Node installed.
"""

from __future__ import annotations

import os
import hashlib
import subprocess
import sys
import time
import shutil
import tempfile
import zipfile
from pathlib import Path

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, ROOT)
from x20ctl import __version__

RELEASE_BIN_DIR = Path(ROOT) / "dist" / __version__


def run(argv: list[str], label: str) -> None:
    print(f"\n=== {label}")
    result = subprocess.run(argv, cwd=ROOT)
    if result.returncode != 0:
        raise SystemExit(f"{label} failed with code {result.returncode}")


def main() -> None:
    try:
        import PyInstaller  # noqa: F401
    except ImportError:
        raise SystemExit("PyInstaller is not installed. pip install pyinstaller")

    npm = shutil.which("npm.cmd" if os.name == "nt" else "npm")
    if not npm:
        raise SystemExit("Node.js/npm is required on the build machine")
    run([npm, "ci", "--ignore-scripts"], "installing locked UI dependencies")
    run([npm, "run", "lint"], "checking the UI")
    run([npm, "run", "build"], "compiling local UI assets")
    from licenses import collect
    collect(Path(ROOT))

    started = time.time()
    RELEASE_BIN_DIR.mkdir(parents=True, exist_ok=True)
    run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean",
         "--distpath", str(RELEASE_BIN_DIR),
         "x20ctl-scanner.spec"], "building the scanner helper")
    scanner = RELEASE_BIN_DIR / "x20ctl-scanner.exe"
    if not scanner.is_file():
        raise SystemExit("scanner build reported success but helper is missing")
    approved_hash = hashlib.sha256(scanner.read_bytes()).hexdigest()
    identity = Path(ROOT) / "x20ctl" / "desktop" / "_scanner_identity.py"
    original_identity = identity.read_bytes()
    try:
        identity.write_text(
            '"""Build-generated release identity."""\n'
            'SCANNER_VERSION = "1.0.0"\n'
            'SCANNER_FILENAME = "x20ctl-scanner.exe"\n'
            f'SCANNER_SHA256 = "{approved_hash}"\n', encoding="utf-8"
        )
        run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean",
             "--distpath", str(RELEASE_BIN_DIR),
             "x20ctl.spec"], "building the executable with the approved scanner hash")
    finally:
        identity.write_bytes(original_identity)

    exe = RELEASE_BIN_DIR / "x20ctl.exe"
    if not os.path.exists(exe):
        raise SystemExit("build reported success but release x20ctl.exe is missing")

    size = os.path.getsize(exe) / (1024 * 1024)
    if size > 40:
        raise SystemExit(f"Desktop executable exceeds the 40 MiB size ceiling: {size:.1f} MiB")
    shutil.copy2(Path(ROOT) / "artifacts" / "THIRD_PARTY_LICENSES.txt", RELEASE_BIN_DIR / "THIRD_PARTY_LICENSES.txt")
    bundle = Path(ROOT) / "dist" / f"x20ctl-{__version__}-win-x64.zip"
    included = ("x20ctl.exe", "x20ctl-scanner.exe", "THIRD_PARTY_LICENSES.txt", "LICENSE")
    with zipfile.ZipFile(bundle, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in included:
            source = Path(ROOT) / name if name == "LICENSE" else RELEASE_BIN_DIR / name
            archive.write(source, arcname=name)
    with tempfile.TemporaryDirectory(prefix="x20ctl-release-") as temporary:
        with zipfile.ZipFile(bundle) as archive:
            if archive.namelist() != list(included) or archive.testzip() is not None:
                raise SystemExit("release archive is incomplete or corrupt")
            archive.extractall(temporary)
        extracted = Path(temporary)
        if hashlib.sha256((extracted / "x20ctl-scanner.exe").read_bytes()).hexdigest() != approved_hash:
            raise SystemExit("packaged scanner hash does not match the approved hash")
        run([str(extracted / "x20ctl.exe"), "--verify-scanner"],
            "verifying the packaged scanner against the embedded host hash")
    print(f"\n=== done in {time.time() - started:.0f}s")
    print(f"    {bundle}")
    print(f"    {exe}")
    print(f"    {scanner}  SHA-256 {approved_hash}")
    print(f"    desktop {size:.1f} MiB, runs without Python installed (WebView2 Runtime required)")


if __name__ == "__main__":
    main()
