"""Build a small local Windows kit; no app build or hardware access."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools/trigger_check"
COMPILER = Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")


def build(output):
    if sys.platform != "win32" or not COMPILER.exists():
        raise RuntimeError("Build requires the Windows .NET Framework compiler")
    output = output.resolve()
    kit = output / "X15-Trigger-Check"
    archive = output / "X15-Trigger-Check.zip"
    if kit.exists() or archive.exists():
        raise FileExistsError("Choose a fresh output directory; previous kits are retained")
    kit.mkdir(parents=True)
    technical = kit / "technical"
    technical.mkdir()
    for name in ("START_HERE.bat", "README.txt"):
        shutil.copyfile(SOURCE / name, kit / name)
    shutil.copyfile(SOURCE / "TriggerCheck.cs", technical / "TriggerCheck.cs")
    shutil.copyfile(ROOT / "LICENSE", kit / "LICENSE.txt")
    executable = kit / "TriggerCheck.exe"
    command = [str(COMPILER), "/nologo", "/optimize+", "/platform:x64",
               "/r:System.Web.Extensions.dll", "/r:System.IO.Compression.FileSystem.dll",
               "/r:System.IO.Compression.dll", "/out:" + str(executable), str(SOURCE / "TriggerCheck.cs")]
    subprocess.run(command, check=True, capture_output=True, text=True, timeout=30)
    check = subprocess.run([str(executable), "--self-test"], check=True, capture_output=True,
                           text=True, timeout=15, creationflags=subprocess.CREATE_NO_WINDOW)
    self_test = json.loads(check.stdout)
    assert self_test == {"status": "passed", "hardware_access": False, "version": "1.0.0"}
    declined = subprocess.run([str(executable)], input="n\n", check=True, capture_output=True,
                             text=True, timeout=15, creationflags=subprocess.CREATE_NO_WINDOW)
    assert "Type y to start" in declined.stdout and not (kit / "results").exists()
    source_digest = hashlib.sha256((SOURCE / "TriggerCheck.cs").read_bytes()).hexdigest()
    metadata = {"version": "1.0.0", "platform": "Windows x64", "runtime": ".NET Framework 4.5+",
                "source_sha256": source_digest, "compiler_sha256": hashlib.sha256(COMPILER.read_bytes()).hexdigest(),
                "packaged_self_test": self_test, "declined_run_no_results": True,
                "hardware_acceptance": "not performed", "release": "local unsigned research utility"}
    (technical / "BUILD_INFO.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    files = [p for p in sorted(kit.rglob("*")) if p.is_file()]
    manifest = {p.relative_to(kit).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    (technical / "CONTENTS.sha256").write_text("\n".join(f"{digest}  {name}" for name, digest in manifest.items()) + "\n", encoding="utf-8")
    with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(kit.rglob("*")):
            if path.is_file():
                bundle.write(path, kit.name + "/" + path.relative_to(kit).as_posix())
    with zipfile.ZipFile(archive) as bundle:
        for path in kit.rglob("*"):
            if path.is_file():
                assert bundle.read(kit.name + "/" + path.relative_to(kit).as_posix()) == path.read_bytes()
        assert len(bundle.infolist()) == 7
    result = {"archive": str(archive), "bytes": archive.stat().st_size,
              "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "packaged_self_test": self_test}
    (output / "verification.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    return archive


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    build(parser.parse_args().output)
