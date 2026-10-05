"""Publish verified scanner assets once; never replace an existing scanner/app release."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))
from controller_scan_kit import VERSION


def publish():
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("GITHUB_REF") != "refs/heads/main":
        raise RuntimeError("Official publication requires GitHub Actions on main")
    if not re.fullmatch(r"\d+\.\d+\.\d+", VERSION): raise ValueError("Invalid scanner version")
    repo = os.environ["GITHUB_REPOSITORY"]
    sha = os.environ["GITHUB_SHA"]
    release = ROOT / "dist/release"
    info = json.loads((release / "BUILD_INFO.json").read_text())
    if info["sourceCommit"] != sha or info["sourceRepository"] != repo or info["kitVersion"] != VERSION:
        raise ValueError("Release does not belong to this build")
    assets = []
    for line in (release / "SHA256SUMS.txt").read_text().splitlines():
        match = re.fullmatch(r"([a-f0-9]{64})  ([A-Za-z0-9_.-]+)", line)
        if not match: raise ValueError("Invalid checksum entry")
        file = release / match[2]
        if hashlib.sha256(file.read_bytes()).hexdigest() != match[1]: raise ValueError("Release checksum mismatch")
        assets.append(file)
    assets.append(release / "SHA256SUMS.txt")
    tag = "scanner-v" + VERSION
    existing = subprocess.run(["gh", "release", "view", tag, "--repo", repo], capture_output=True, text=True)
    if existing.returncode == 0:
        print("Scanner release already exists; it was not replaced.")
        return
    if "not found" not in existing.stderr.lower():
        raise RuntimeError("Cannot determine release existence: " + existing.stderr)
    notes = ROOT / "dist/RELEASE_NOTES.md"
    notes.write_text(f"""# Controller Scan Kit {VERSION}

Standalone volunteer scanner for Windows 10/11 x64, separate from the X20ctl desktop app.
Download **ControllerScanKit-{VERSION}-win-x64.zip**, extract it and run START_HERE.bat.

The complete source, data collection table, privacy policy and source-running alternative:
https://github.com/{repo}/tree/{sha}/tools/controller-scan-kit

No automatic upload, telemetry, firmware/configuration writes, driver install or elevation
is implemented by this scanner. Input/BLE stages are optional. Raw protocol capture is
separate and opt-in; raw HID/trace bytes can contain identifiers and are not anonymized.
Cancellation leaves only the local folder; ZIP creation requires review confirmation.

Added hardware-free --capabilities, --privacy and --dry-run, privacy/archive/consent guards,
hash-locked dependencies, and public build/release verification.

Built on a GitHub-hosted Windows runner from commit `{sha}`.
Workflow: {info['workflowRun']}
Source/unit/policy tests, CodeQL, packaged self-check and Microsoft Defender gate publication.
BUILD_INFO.json records actual build versions/results. SHA256SUMS.txt covers all assets;
SBOM.cdx.json inventories locked Python runtime/build distributions and CPython, not every native DLL.
Verify checksums and GitHub build provenance using VERIFY_RELEASE.md in the ZIP/source.

The EXE is **unsigned**. Build provenance is separate from Windows code signing.
No bit-for-bit reproducibility, clean-machine acceptance or physical-controller testing is
claimed. Antivirus/attestation/test results do not prove all possible behavior is harmless.
Do not disable protection to run a file you do not trust. Results remain local until you
manually choose to share a reviewed ZIP privately. Never post real captures in public issues.

This scanner release is not marked as the latest desktop app release.
""", encoding="utf-8")
    subprocess.run(["gh", "release", "create", tag, *(str(a) for a in assets), "--repo", repo,
                    "--target", sha, "--title", f"Controller Scan Kit {VERSION}",
                    "--notes-file", str(notes), "--latest=false"], check=True)


if __name__ == "__main__": publish()
