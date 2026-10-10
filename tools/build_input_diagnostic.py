"""Compile and verify the standalone scanner without accessing hardware or the screen."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools/input_diagnostic"
COMPILER = Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(output):
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    executable = output / "X20CTL-Input-Scan.exe"
    sources = sorted(SOURCE.glob("*.cs"))
    command = [str(COMPILER), "/nologo", "/optimize+", "/platform:x64", "/target:winexe",
               *["/r:" + name for name in ["System.Web.Extensions.dll", "System.Windows.Forms.dll",
                 "System.Drawing.dll", "System.IO.Compression.dll", "System.IO.Compression.FileSystem.dll"]],
               "/out:" + str(executable), *map(str, sources)]
    subprocess.run(command, check=True, capture_output=True, text=True, timeout=30)
    result = subprocess.run([str(executable), "--self-test"],
                            capture_output=True, text=True, timeout=30)
    if result.returncode:
        raise RuntimeError(result.stderr or result.stdout or "Executable self-test failed")
    checks = json.loads(result.stdout)
    if checks["status"] != "passed" or checks["hardware_access"] is not False:
        raise RuntimeError("Offline executable checks failed")
    subprocess.run([str(executable), "--preview", str(output / "live-preview.png")],
                   check=True, capture_output=True, text=True, timeout=30)
    shutil.copyfile(SOURCE / "README.txt", output / "README.txt")
    report = {"version": "2.0.0-local", "executable": str(executable), "sha256": digest(executable),
              "bytes": executable.stat().st_size, "compiler_sha256": digest(COMPILER),
              "source_hashes": {p.name: digest(p) for p in sources}, "self_test": checks,
              "hardware_acceptance": "not performed", "screen_access": False,
              "preview": "offscreen rendering with synthetic input; no shown window",
              "publication": "local only"}
    (output / "build.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=ROOT / "artifacts/input-diagnostic-20261008")
    build(parser.parse_args().out)
