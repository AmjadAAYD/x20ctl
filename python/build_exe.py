#!/usr/bin/env python3
"""
x20ctl - Automated Windows .EXE Build Script
Compiles the Python Application into a standalone x20ctl.exe binary via PyInstaller.
"""

import os
import sys
import subprocess
import shutil

APP_DIR = os.path.dirname(os.path.abspath(__file__))
DIST_DIR = os.path.join(APP_DIR, "dist")
BUILD_DIR = os.path.join(APP_DIR, "build")
SPEC_FILE = os.path.join(APP_DIR, "x20ctl.spec")
ICON_FILE = os.path.join(APP_DIR, "assets", "x20ctl.ico")


def check_and_install_dependencies():
    """Ensures PyInstaller and required modules are available."""
    print("[*] Checking Python environment dependencies...")
    try:
        import PyInstaller
        print(f"[✓] PyInstaller {PyInstaller.__version__} detected.")
    except ImportError:
        print("[!] PyInstaller not found. Installing via pip...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "pyinstaller"])


def build_exe():
    check_and_install_dependencies()

    print("\n" + "=" * 60)
    print("  BUILDING x20ctl.exe (Standalone Windows Executable)")
    print("=" * 60 + "\n")

    # If x20ctl.spec exists, build from spec
    if os.path.exists(SPEC_FILE):
        print(f"[*] Using PyInstaller specification: {SPEC_FILE}")
        cmd = [sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean", SPEC_FILE]
    else:
        print("[*] Generating PyInstaller single-file build...")
        cmd = [
            sys.executable, "-m", "PyInstaller",
            "--noconfirm",
            "--clean",
            "--onefile",
            "--windowed",
            "--name=x20ctl",
            f"--add-data=keylinker.py{os.pathsep}.",
        ]
        if os.path.exists(ICON_FILE):
            cmd.append(f"--icon={ICON_FILE}")
        cmd.append("app.py")

    print(f"Executing: {' '.join(cmd)}\n")
    ret = subprocess.call(cmd)

    if ret == 0:
        exe_path = os.path.join(DIST_DIR, "x20ctl.exe")
        print("\n" + "=" * 60)
        print(" [✓] BUILD COMPLETED SUCCESSFULLY!")
        print(f" [✓] Executable binary: {exe_path}")
        print("=" * 60)
    else:
        print(f"\n[!] Build failed with exit code: {ret}")

    return ret


if __name__ == "__main__":
    sys.exit(build_exe())
