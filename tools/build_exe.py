"""Build a standalone x20ctl.exe.

    python tools/build_exe.py

Regenerates the icon first so the executable can never ship an icon that
disagrees with the one the app draws, then runs PyInstaller against the spec
file. The result lands in dist/x20ctl.exe and needs no Python installed.
"""

from __future__ import annotations

import os
import subprocess
import sys
import time

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


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

    run([sys.executable, os.path.join("tools", "make_icon.py")],
        "generating the icon")

    started = time.time()
    run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean",
         "x20ctl.spec"], "building the executable")

    exe = os.path.join(ROOT, "dist", "x20ctl.exe")
    if not os.path.exists(exe):
        raise SystemExit("build reported success but dist/x20ctl.exe is missing")

    size = os.path.getsize(exe) / (1024 * 1024)
    print(f"\n=== done in {time.time() - started:.0f}s")
    print(f"    {exe}")
    print(f"    {size:.1f} MB, runs without Python installed")


if __name__ == "__main__":
    main()
