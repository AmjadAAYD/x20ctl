"""Build a standalone x20ctl.exe.

    python tools/build_exe.py

Builds the checked-in React UI and native runtime in one artifact.
The result lands in dist/x20ctl.exe and needs no Python or Node installed.
"""

from __future__ import annotations

import os
import subprocess
import sys
import time
import shutil
import argparse
from pathlib import Path

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


def run(argv: list[str], label: str) -> None:
    print(f"\n=== {label}")
    result = subprocess.run(argv, cwd=ROOT)
    if result.returncode != 0:
        raise SystemExit(f"{label} failed with code {result.returncode}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundled-runtime", action="store_true", help="Include Microsoft's fixed runtime for offline, dependency-free startup")
    args = parser.parse_args()
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
    if args.bundled_runtime:
        from runtime import prepare
        os.environ["X20CTL_BUNDLE_RUNTIME"] = str(prepare(Path(ROOT)))

    started = time.time()
    run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean",
         "x20ctl.spec"], "building the executable")

    exe = os.path.join(ROOT, "dist", "x20ctl.exe")
    if not os.path.exists(exe):
        raise SystemExit("build reported success but dist/x20ctl.exe is missing")

    size = os.path.getsize(exe) / (1024 * 1024)
    shutil.copy2(Path(ROOT) / "artifacts" / "THIRD_PARTY_LICENSES.txt", Path(ROOT) / "dist" / "THIRD_PARTY_LICENSES.txt")
    print(f"\n=== done in {time.time() - started:.0f}s")
    print(f"    {exe}")
    print(f"    {size:.1f} MB, runs without Python installed")


if __name__ == "__main__":
    main()
