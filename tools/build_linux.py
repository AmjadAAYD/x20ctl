"""Build a local Linux bundle without modifying the host checkout/identity.

Windows: python tools/build_linux.py --docker --skip-ui-build
Linux:   python tools/build_linux.py --skip-ui-build
Use --skip-ui-build only after npm run lint and npm run build have passed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import shutil
import subprocess
import sys
import tarfile
import tempfile
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(argv, *, cwd=ROOT, **kwargs):
    subprocess.run([str(v) for v in argv], cwd=cwd, check=True, **kwargs)


def copy_source(source, destination):
    # Explicit whitelist: no .git, private research, device captures, credentials,
    # host virtualenv, node_modules or previous release artifacts enter the build.
    for name in ("x20ctl", "dist-ui", "tests", "assets"):
        if name == "assets":
            (destination / name).mkdir()
            for filename in ("x20ctl.ico", "x20ctl.png"):
                shutil.copy2(source / name / filename, destination / name / filename)
        else:
            shutil.copytree(source / name, destination / name, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    for name in ("app.py", "scanner_entry.py", "x20ctl-linux.spec", "x20ctl-scanner.spec", "LICENSE", "THIRD_PARTY.md"):
        shutil.copy2(source / name, destination / name)
    for name in ("tools/linux",):
        shutil.copytree(source / name, destination / name, ignore=shutil.ignore_patterns("__pycache__"))
    # Reuse the license collector with only the frontend's original notices.
    shutil.copy2(source / "tools/licenses.py", destination / "tools/licenses.py")
    for package in ("react", "react-dom", "scheduler", "lucide-react"):
        folder = destination / "node_modules" / package
        folder.mkdir(parents=True)
        for notice in (source / "node_modules" / package).glob("LICENSE*"):
            shutil.copy2(notice, folder / notice.name)


def make_launcher(archive: Path) -> Path:
    """Single executable download; extract privately, run the ELF, then clean up."""
    header = '''#!/bin/sh
# x20ctl native Linux desktop bundle. Requires GTK 3 / WebKitGTK 4.1.
set -eu
bundle_tmp=$(mktemp -d "${TMPDIR:-/tmp}/x20ctl.XXXXXX")
trap 'rm -rf -- "$bundle_tmp"' 0
trap 'exit 130' INT
trap 'exit 143' TERM
tail -n +PAYLOAD_LINE "$0" | tar -xz -C "$bundle_tmp"
"$bundle_tmp/x20ctl-linux-x86_64/x20ctl" "$@"
exit $?
'''
    header = header.replace("PAYLOAD_LINE", str(header.count("\n") + 1))
    launcher = archive.with_name("x20ctl-linux-x86_64.run")
    if launcher.exists():
        raise FileExistsError(launcher)
    with launcher.open("wb") as destination, archive.open("rb") as source:
        destination.write(header.encode("utf-8"))
        shutil.copyfileobj(source, destination)
    launcher.chmod(0o755)
    return launcher


def native_build(source, output):
    if sys.platform != "linux" or platform.machine() not in ("x86_64", "AMD64"):
        raise SystemExit("This Linux build requires an x86-64 Linux builder. Use --docker on Windows.")
    if (output / "x20ctl-linux-x86_64.tar.gz").exists():
        raise SystemExit("Output bundle already exists; choose a new --output directory.")
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="x20ctl-linux-build-") as temporary:
        work = Path(temporary)
        copy_source(source, work)
        manifest = {str(file.relative_to(work)): hashlib.sha256(file.read_bytes()).hexdigest()
                    for file in work.rglob("*") if file.is_file()}
        (output / "source-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        run([sys.executable, "-m", "pytest", "tests/test_linux_desktop.py", "tests/test_desktop.py",
             "tests/test_controller_registry.py", "tests/test_reports.py", "-q"], cwd=work)
        run([sys.executable, "tools/licenses.py"], cwd=work)
        from importlib.metadata import distributions
        license_file = work / "artifacts/THIRD_PARTY_LICENSES.txt"
        # Debian's system Python/GI notices are outside pip metadata.
        with license_file.open("a", encoding="utf-8") as notices:
            for package in ("python3.10", "python3-gi", "python3-evdev"):
                copyright_file = Path("/usr/share/doc") / package / "copyright"
                if copyright_file.is_file():
                    notices.write(f"\n\n{package}\n{copyright_file.read_text(errors='replace')}")
        dist = work / "dist"
        run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean", "--distpath", dist,
             "x20ctl-scanner.spec"], cwd=work)
        scanner = dist / "x20ctl-scanner"
        scanner_hash = hashlib.sha256(scanner.read_bytes()).hexdigest()
        identity = work / "x20ctl/desktop/_scanner_identity.py"
        identity.write_text('SCANNER_VERSION = "1.0.0"\nSCANNER_FILENAME = "x20ctl-scanner"\n'
                            f'SCANNER_SHA256 = "{scanner_hash}"\n', encoding="utf-8")
        run([sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean", "--distpath", dist,
             "x20ctl-linux.spec"], cwd=work)
        bundle = dist / "x20ctl-linux-x86_64"
        for name in ("Gtk-3.0.typelib", "WebKit2-4.1.typelib", "JavaScriptCore-4.1.typelib", "Soup-3.0.typelib"):
            if not (bundle / "_internal/gi_typelibs" / name).is_file():
                raise RuntimeError(f"Required browser introspection data missing from bundle: {name}")
        shutil.copy2(scanner, bundle / scanner.name)
        shutil.copy2(work / "tools/linux/LINUX-README.txt", bundle / "LINUX-README.txt")
        shutil.copy2(work / "tools/linux/launch.sh", bundle / "launch.sh")
        shutil.copy2(license_file, bundle / license_file.name)
        (bundle / "launch.sh").chmod(0o755)
        for file in (bundle / "x20ctl", bundle / scanner.name):
            assert file.read_bytes()[:4] == b"\x7fELF", f"Not a native Linux executable: {file}"
            file.chmod(0o755)
        run([bundle / "x20ctl", "--verify-scanner"], cwd=bundle)
        status = subprocess.run([bundle / scanner.name], capture_output=True, text=True)
        assert status.returncode == 20 and json.loads(status.stdout)["status"] == "invalid_arguments"
        # Real packaged app, GTK/WebKit native window, in an isolated display.
        review = output / "review"
        run(["dbus-run-session", "--", "xvfb-run", "-a", "-s", "-screen 0 1440x1000x24",
             bundle / "x20ctl", "--linux-review", review], cwd=bundle,
            env={**os.environ, "X20CTL_ISOLATED_REVIEW": "1", "WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS": "1",
                 "LIBGL_ALWAYS_SOFTWARE": "1"}, timeout=180)
        report = json.loads((review / "report.json").read_text())
        assert report["passed"], report
        archive = output / "x20ctl-linux-x86_64.tar.gz"
        with tarfile.open(archive, "w:gz", compresslevel=6) as tar:
            tar.add(bundle, arcname=bundle.name)
        launcher = make_launcher(archive)
        run([launcher, "--verify-scanner"], cwd=output)
        # Extract on Linux. Copying thousands of GTK theme files individually
        # onto a Windows bind mount is slow and can lose Unix permissions.
        checksums = {archive.name: hashlib.sha256(archive.read_bytes()).hexdigest(),
                     launcher.name: hashlib.sha256(launcher.read_bytes()).hexdigest(),
                     "x20ctl": hashlib.sha256((bundle / "x20ctl").read_bytes()).hexdigest(),
                     "x20ctl-scanner": scanner_hash}
        (output / "SHA256SUMS.txt").write_text("\n".join(f"{digest}  {name}" for name, digest in checksums.items()) + "\n")
        (output / "build-report.json").write_text(json.dumps({"platform": platform.platform(),
             "python": platform.python_version(), "architecture": platform.machine(),
             "reviewPassed": True, "archiveBytes": archive.stat().st_size, "checksums": checksums,
             "dependencies": {d.metadata["Name"]: d.version for d in distributions()}}, indent=2))
        print(f"Linux bundle: {archive}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--docker", action="store_true")
    parser.add_argument("--skip-ui-build", action="store_true")
    parser.add_argument("--source", type=Path, default=ROOT)
    parser.add_argument("--output", type=Path, default=ROOT / "dist" / f"local-linux-{datetime.now():%Y%m%d}")
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if not args.skip_ui_build:
        npm = shutil.which("npm.cmd" if os.name == "nt" else "npm")
        if not npm:
            raise SystemExit("Node/npm is required to compile the frontend, or use --skip-ui-build with a current build.")
        run([npm, "run", "lint"], cwd=source)
        run([npm, "run", "build"], cwd=source)
    if not (source / "dist-ui/index.html").is_file():
        raise SystemExit("Compiled frontend missing. Run npm run build first.")
    if args.docker:
        run(["docker", "build", "--pull=false", "-t", "x20ctl-linux-builder:local", source / "tools/linux"])
        output.mkdir(parents=True, exist_ok=True)
        run(["docker", "run", "--rm", "--network=none", "--shm-size=512m",
             "--mount", f"type=bind,source={source},target=/source,readonly",
             "--mount", f"type=bind,source={output},target=/output",
             "x20ctl-linux-builder:local", "python", "/source/tools/build_linux.py",
             "--skip-ui-build", "--source", "/source", "--output", "/output"])
    else:
        native_build(source, output)


if __name__ == "__main__":
    main()
