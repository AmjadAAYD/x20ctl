"""Build a fresh local volunteer distribution without modifying the app build."""
import argparse
from datetime import datetime, timezone
import hashlib
from importlib import metadata
import json
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from controller_scan_kit import VERSION


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def build(output):
    if sys.platform != "win32" or struct.calcsize("P") != 8 or sys.version_info < (3, 12):
        raise RuntimeError("Build with Windows x64 Python 3.12+ and the pinned existing environment")
    # No downloads/installers. Imported versions are recorded, not inferred from lock text.
    for package in ("bleak", "pyinstaller"): metadata.version(package)
    name = "X20ctl-Controller-Kit"
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    kit = output / name
    if kit.exists() or (kit.parent / (kit.name + ".zip")).exists():
        raise FileExistsError("Existing distribution is retained; choose another --output directory")
    work = output / ("build-" + uuid.uuid4().hex)
    work.mkdir()
    argv = [sys.executable, "-m", "PyInstaller", "--onefile", "--console", "--noupx", "--noconfirm",
            "--name", "ControllerScanKit", "--distpath", str(work / "dist"),
            "--workpath", str(work / "work"), "--specpath", str(work), "--paths", str(ROOT / "tools")]
    for module in ("tkinter", "PySide6", "PyQt5", "PyQt6", "webview", "requests", "httpx", "aiohttp", "pytest"):
        argv += ["--exclude-module", module]
    argv.append(str(ROOT / "tools" / "controller_scan.py"))
    with (work / "build.log").open("w", encoding="utf-8") as log:
        subprocess.run(argv, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=300)
    executable = work / "dist" / "ControllerScanKit.exe"
    check = subprocess.run([str(executable), "--self-check"], text=True, capture_output=True,
                           encoding="utf-8", timeout=45, check=True)
    check_data = json.loads(check.stdout)
    if check_data != {"status": "passed", "version": VERSION, "hardware_access": False}:
        raise RuntimeError("Packaged self-check did not confirm completion")
    kit.mkdir()
    shutil.copy2(executable, kit / "ControllerScanKit.exe")
    guides = ROOT / "docs" / "controller-scan-kit"
    shutil.copy2(guides / "complete/README.txt", kit / "README.txt")
    shutil.copy2(guides / "complete/START_HERE.bat", kit / "START_HERE.bat")
    technical = kit / "technical"
    technical.mkdir()
    source_dir = technical / "source"
    source_dir.mkdir()
    shutil.copy2(ROOT / "tools/controller_scan.py", source_dir / "controller_scan.py")
    shutil.copytree(ROOT / "tools/controller_scan_kit", source_dir / "controller_scan_kit",
                    ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    distributions = [d for d in metadata.distributions() if d.metadata["Name"].lower() in
                     {"bleak", "typing_extensions", "typing-extensions"} or d.metadata["Name"].lower().startswith("winrt")]
    versions = {d.metadata["Name"]: d.version for d in distributions}
    (source_dir / "requirements.in").write_text("\n".join(f"{k}=={v}" for k, v in sorted(versions.items())) + "\n", encoding="utf-8")
    licenses = technical / "licenses"
    licenses.mkdir()
    shutil.copy2(ROOT / "LICENSE", licenses / "X20CTL_LICENSE")
    shutil.copy2(Path(sys.base_prefix) / "LICENSE.txt", licenses / "PYTHON_LICENSE")
    shutil.copy2(guides / "PYWINRT_LICENSE.txt", licenses / "PYWINRT_LICENSE")
    for package, destination in (("bleak", "BLEAK_LICENSE"), ("pyinstaller", "PYINSTALLER_COPYING"),
                                  ("typing_extensions", "TYPING_EXTENSIONS_LICENSE")):
        dist = metadata.distribution(package)
        paths = [dist.locate_file(f) for f in dist.files or [] if
                 Path(f).name.lower() in {"license", "license.txt", "copying.txt"}]
        if not paths: raise RuntimeError(f"Required license missing: {package}")
        shutil.copy2(paths[0], licenses / destination)
    (technical / "LEGAL_NOTICES").write_text((guides / "THIRD_PARTY_NOTICES.txt").read_text(encoding="utf-8").replace(".txt", ""), encoding="utf-8")
    data = technical / "research-data"
    data.mkdir()
    for name in ("SESSION-TIMELINE.csv", "EVIDENCE-MANIFEST.example.json"):
        shutil.copy2(guides / "templates" / name, data / name)
    write_json(data / "PROVENANCE.json", {"purpose": "Retained research templates, not collected controller evidence",
        "manifestExample": "Earlier research design sketch; actual report validator uses generated manifest schema 1"})
    source_hashes = {p.relative_to(kit).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in sorted(source_dir.rglob("*")) if p.is_file()}
    base = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True, capture_output=True, check=True).stdout.strip()
    write_json(technical / "BUILD_INFO.json", {"kitVersion": VERSION, "builtAtUtc": datetime.now(timezone.utc).isoformat(),
        "platform": "Windows x64", "python": platform.python_version(), "dependencies": versions,
        "pyinstaller": metadata.version("pyinstaller"), "baseCommit": base,
        "sourceAuthority": "Source hashes below identify supplied working files; base commit is context only",
        "sourceSha256": source_hashes, "packagedSelfCheck": check_data, "signed": False,
        "hardwareAcceptance": "not exercised by build; requires physical controller pilot"})
    write_json(technical / "VERIFICATION.json", {"packagedSelfCheck": check_data,
        "checks": ["bundled imports", "Windows structure layout", "synthetic report/validator round-trip", "distribution ZIP bytes"],
        "physicalControllerTests": "not performed", "cleanMachineWithoutPython": "not tested",
        "release": "local unsigned research build"})
    hashes = [(p, hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(kit.rglob("*")) if p.is_file()]
    (technical / "CONTENTS.sha256").write_text("\n".join(f"{digest}  {p.relative_to(kit).as_posix()}" for p, digest in hashes) + "\n", encoding="utf-8")
    archive = kit.parent / (kit.name + ".zip")
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, allowZip64=False) as z:
        for p in sorted(kit.rglob("*")):
            if p.is_file(): z.write(p, kit.name + "/" + p.relative_to(kit).as_posix())
    with zipfile.ZipFile(archive) as z:
        for p in kit.rglob("*"):
            if p.is_file() and z.read(kit.name + "/" + p.relative_to(kit).as_posix()) != p.read_bytes():
                raise RuntimeError("Kit ZIP differs from build files")
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    archive.with_name(archive.name + ".sha256").write_text(f"{digest}  {archive.name}\n", encoding="utf-8")
    print(json.dumps({"archive": str(archive), "sha256": digest, "bytes": archive.stat().st_size,
                      "selfCheck": check_data}, indent=2))
    return archive


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/controller-scan-kit" / datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S"))
    args = parser.parse_args()
    build(args.output)
