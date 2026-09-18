"""Fetch Microsoft's signed redistributable runtime for a self-contained build."""
from __future__ import annotations

import hashlib
import subprocess
import shutil
import urllib.request
from pathlib import Path

VERSION = "153.0.4234.32"
SHA256 = "2cb653a74426f0aa802c2396775c6bc674fd662d5396bd677f47bfa6e12eba9c"
URL = "https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/c3d95bc1-a0a7-4ca6-aaa1-fa0ac3dd1a37/Microsoft.WebView2.FixedVersionRuntime.153.0.4234.32.x64.cab"


def prepare(root: Path) -> Path:
    cache = root / "artifacts" / "runtime-download"
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / "WebView2-x64.cab"
    if not archive.exists():
        print(f"Downloading Microsoft WebView2 {VERSION} (x64)", flush=True)
        urllib.request.urlretrieve(URL, archive)
    with archive.open("rb") as handle:
        digest = hashlib.file_digest(handle, "sha256").hexdigest()
    if digest != SHA256:
        raise RuntimeError(f"WebView2 archive checksum mismatch: {digest}")
    extracted = cache / "extracted"
    runtime = extracted / f"Microsoft.WebView2.FixedVersionRuntime.{VERSION}.x64"
    if not (runtime / "msedgewebview2.exe").exists():
        extracted.mkdir(exist_ok=True)
        subprocess.run(["expand.exe", str(archive), "-F:*", str(extracted)], check=True, stdout=subprocess.DEVNULL)
    shell = shutil.which("pwsh") or "powershell.exe"
    subprocess.run([shell, "-NoProfile", "-File", str(root / "tools" / "verify_runtime.ps1"),
                    "-Path", str(runtime / "msedgewebview2.exe")], check=True)
    return runtime
