"""Package an already-built desktop preview and check its internal worker."""
import hashlib
import json
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from x20ctl import __version__

def main():
    directory = ROOT / "dist" / __version__
    exe = directory / "x20ctl.exe"
    result = subprocess.run([str(exe), "--research-worker"],
        input=json.dumps({"operation": "capabilities", "requestId": "release-check"}) + "\n",
        capture_output=True, text=True, timeout=30,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if result.returncode or '"hardware_access": false' not in result.stdout:
        raise SystemExit("Packaged internal-worker verification failed")
    bundle = ROOT / "dist" / f"x20ctl-{__version__}-win-x64.zip"
    sources = {"x20ctl.exe": exe, "LICENSE": ROOT / "LICENSE",
        "THIRD_PARTY.md": ROOT / "THIRD_PARTY.md",
        "THIRD_PARTY_LICENSES.txt": ROOT / "artifacts" / "THIRD_PARTY_LICENSES.txt",
        "README.txt": ROOT / "RELEASES" / f"{__version__}.md"}
    with zipfile.ZipFile(bundle, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name, source in sources.items():
            archive.write(source, name)
    with zipfile.ZipFile(bundle) as archive:
        if archive.testzip() or archive.namelist() != list(sources):
            raise SystemExit("Archive verification failed")
        if archive.read("x20ctl.exe") != exe.read_bytes():
            raise SystemExit("Archive executable mismatch")
    sums = ROOT / "dist" / "SHA256SUMS-" / __version__ / "SHA256SUMS.txt"
    sums.parent.mkdir(parents=True, exist_ok=True)
    sums.write_text(hashlib.sha256(bundle.read_bytes()).hexdigest() + "  " + bundle.name + "\n")
    print(bundle)
    print(sums)

if __name__ == "__main__":
    main()
