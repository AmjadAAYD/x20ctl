"""Package only known source/docs, with actual build metadata and an environment SBOM."""
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import platform
import re
import shutil
import sys
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))
from controller_scan_kit import VERSION


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_file(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def generate():
    dist = ROOT / "dist"
    exe = dist / "ControllerScanKit.exe"
    check = json.loads((dist / "SELF_CHECK.json").read_text(encoding="utf-8-sig"))
    defender = json.loads((dist / "DEFENDER.json").read_text(encoding="utf-8-sig"))
    if check.get("status") != "passed" or check.get("hardware_access") is not False:
        raise ValueError("No successful packaged self-check")
    if defender.get("status") != "passed" or defender.get("exitCode") != 0:
        raise ValueError("No successful Defender scan")
    if not exe.is_file() or exe.read_bytes()[:2] != b"MZ":
        raise ValueError("Missing Windows executable")
    output = dist / "release"
    output.mkdir(exist_ok=False)
    shutil.copy2(exe, output / exe.name)
    files = sorted(p for p in ROOT.rglob("*") if p.is_file() and
                   p.relative_to(ROOT).parts[0] in {"src", "scripts", "licenses", "tests"}
                   and "__pycache__" not in p.parts and p.suffix != ".pyc")
    files += sorted(p for p in ROOT.iterdir() if p.is_file())
    source_hashes = {p.relative_to(ROOT).as_posix(): digest(p) for p in files}
    built = datetime.now(timezone.utc).isoformat()
    repo = os.environ.get("GITHUB_REPOSITORY")
    sha = os.environ.get("GITHUB_SHA")
    run = os.environ.get("GITHUB_RUN_ID")
    info = {"kitVersion": VERSION, "builtAtUtc": built, "python": platform.python_version(),
            "platform": platform.platform(), "sourceCommit": sha, "sourceRepository": repo,
            "workflow": os.environ.get("GITHUB_WORKFLOW_REF"),
            "workflowRun": f"https://github.com/{repo}/actions/runs/{run}" if repo and run else None,
            "builder": "GitHub Actions" if os.environ.get("GITHUB_ACTIONS") == "true" else "local",
            "sourceSha256": source_hashes, "executableSha256": digest(exe), "signed": False,
            "packagedSelfCheck": check, "antivirusChecks": {"windowsDefender": defender},
            "physicalControllerTests": "not performed by this build",
            "cleanMachineWithoutPython": "not tested",
            "bitForBitReproducibility": "not established"}
    json_file(output / "BUILD_INFO.json", info)
    components = []
    for lock in (ROOT / "requirements.lock", ROOT / "requirements-build.lock"):
        for name, version in re.findall(r"(?m)^([A-Za-z0-9_.-]+)==([^\s]+)", lock.read_text()):
            actual = importlib.metadata.version(name)
            if actual != version:
                raise ValueError(f"Installed version differs from lock: {name}")
            normal = re.sub(r"[-_.]+", "-", name).lower()
            components.append({"type": "library", "name": name, "version": actual,
                               "bom-ref": f"pkg:pypi/{normal}@{actual}", "purl": f"pkg:pypi/{normal}@{actual}",
                               "properties": [{"name": "x20ctl:dependency-role",
                                               "value": "runtime" if lock.name == "requirements.lock" else "build"}]})
    components.append({"type": "application", "name": "CPython", "version": platform.python_version()})
    json_file(output / "SBOM.cdx.json", {"bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
        "serialNumber": "urn:uuid:" + str(uuid.uuid4()), "metadata": {"timestamp": built,
        "component": {"type": "application", "name": "ControllerScanKit", "version": VERSION},
        "properties": [{"name": "x20ctl:scope", "value": "locked Python runtime/build distributions; not a native DLL inventory"}]},
        "components": components})
    archive = output / f"ControllerScanKit-{VERSION}-win-x64.zip"
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED) as z:
        for p in [exe, output / "BUILD_INFO.json", output / "SBOM.cdx.json"]:
            z.write(p, "ControllerScanKit/" + p.name)
        for p in files:
            z.write(p, "ControllerScanKit/" + p.relative_to(ROOT).as_posix())
    (output / "SHA256SUMS.txt").write_text("".join(f"{digest(p)}  {p.name}\n" for p in sorted(output.iterdir())), encoding="ascii")
    print(f"Prepared scanner {VERSION}: {output}")


if __name__ == "__main__":
    generate()
