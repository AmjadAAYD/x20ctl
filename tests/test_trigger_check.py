"""Offline checks for the tiny Windows trigger utility; no device enumeration."""
import json
from pathlib import Path
import subprocess
import sys
import hashlib
import zipfile

import pytest

ROOT = Path(__file__).resolve().parents[1]
COMPILER = Path("C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
pytestmark = pytest.mark.skipif(sys.platform != "win32" or not COMPILER.exists(), reason="Windows .NET Framework compiler")


@pytest.fixture(scope="module")
def executable(tmp_path_factory):
    source = ROOT / "tools/trigger_check/TriggerCheck.cs"
    assert source.is_file(), "The standalone trigger checker has not been implemented"
    output = tmp_path_factory.mktemp("trigger-build") / "TriggerCheck.exe"
    subprocess.run([str(COMPILER), "/nologo", "/optimize+", "/platform:x64",
                    "/r:System.Web.Extensions.dll", "/r:System.IO.Compression.FileSystem.dll",
                    "/r:System.IO.Compression.dll", "/out:" + str(output), str(source)], check=True, capture_output=True)
    return output


def run(executable, *args):
    return subprocess.run([str(executable), *map(str, args)], text=True, capture_output=True, timeout=20,
                          creationflags=subprocess.CREATE_NO_WINDOW)


def test_packaged_self_test_never_uses_hardware(executable):
    result = run(executable, "--self-test")
    assert result.returncode == 0, result.stderr + result.stdout
    assert json.loads(result.stdout)["hardware_access"] is False


@pytest.mark.parametrize("source", ["hid_input", "xinput_state"])
def test_gradual_left_and_binary_right_are_reported_separately(executable, tmp_path, source):
    rows = []
    for action, values in [("neutral", [(0, 0)] * 3), ("LT", [(0, 0), (32, 0), (64, 0), (128, 0), (255, 0), (0, 0)]),
                           ("RT", [(0, 0), (0, 255), (0, 0)])]:
        for left, right in values:
            row = {"source": source, "action": action, "elapsed_ms": len(rows) * 20}
            if source == "hid_input":
                payload = bytearray.fromhex("0000000f808080800000")
                payload[8], payload[9] = left, right
                row["report_hex"] = payload.hex()
            else:
                row["values"] = {"lt": left, "rt": right}
            rows.append(row)
    (tmp_path / "samples.jsonl").write_text("\n".join(json.dumps(row) for row in rows), encoding="utf-8")
    result = run(executable, "--analyze", tmp_path)
    assert result.returncode == 0, result.stderr + result.stdout
    report = json.loads(result.stdout)
    assert report["LT"]["status"] == "intermediate_values_observed"
    assert report["LT"]["intermediate_values"] == [32, 64, 128]
    assert report["RT"]["status"] == "endpoints_only_observed"


def test_missing_trigger_test_is_inconclusive(executable, tmp_path):
    row = {"source": "xinput_state", "action": "neutral", "values": {"lt": 0, "rt": 0}}
    (tmp_path / "samples.jsonl").write_text(json.dumps(row), encoding="utf-8")
    result = run(executable, "--analyze", tmp_path)
    assert result.returncode == 0
    assert json.loads(result.stdout)["LT"]["status"] == "inconclusive_no_samples"


@pytest.mark.parametrize("row", [
    {"source": "hid_input", "action": "LT", "report_hex": "0100000f808080804000"},
    {"source": "xinput_state", "action": "LT", "values": {"lt": 300, "rt": 0}},
])
def test_unknown_hid_layout_or_invalid_range_is_rejected(executable, tmp_path, row):
    (tmp_path / "samples.jsonl").write_text(json.dumps(row), encoding="utf-8")
    result = run(executable, "--analyze", tmp_path)
    assert result.returncode != 0


def test_other_trigger_movement_prevents_clean_verdict(executable, tmp_path):
    rows = [{"source":"xinput_state", "action":action, "values":{"lt":left, "rt":right}}
            for action,left,right in [("neutral",0,0),("LT",0,0),("LT",128,64),("LT",255,255)]]
    (tmp_path / "samples.jsonl").write_text("\n".join(map(json.dumps,rows)), encoding="utf-8")
    result = run(executable, "--analyze", tmp_path)
    assert json.loads(result.stdout)["LT"]["status"] == "inconclusive_other_trigger_moved"


def test_held_neutral_prevents_clean_verdict(executable, tmp_path):
    rows = [{"source":"xinput_state", "action":action, "values":{"lt":left, "rt":0}}
            for action,left in [("neutral",64),("LT",0),("LT",128),("LT",255)]]
    (tmp_path / "samples.jsonl").write_text("\n".join(map(json.dumps,rows)), encoding="utf-8")
    result = run(executable, "--analyze", tmp_path)
    assert json.loads(result.stdout)["LT"]["status"] == "inconclusive_baseline_not_released"


def test_mixed_input_paths_are_not_combined(executable, tmp_path):
    rows = [{"source":"xinput_state","action":"neutral","values":{"lt":0,"rt":0}},
            {"source":"hid_input","action":"LT","report_hex":"0000000f808080808000"}]
    (tmp_path / "samples.jsonl").write_text("\n".join(map(json.dumps,rows)), encoding="utf-8")
    assert run(executable, "--analyze", tmp_path).returncode != 0


def test_result_zip_contains_verdict_and_matching_hashes(executable, tmp_path):
    """Exercise the real result writer using synthetic input, without live APIs."""
    harness = tmp_path / "ArchiveFixture.cs"
    harness.write_text('''
using System;
using System.Collections.Generic;
using System.Reflection;
namespace TriggerCheck {
    class ArchiveFixture {
        public static int Main(string[] args) {
            var samples=Analysis.ReadFolder(args[0]);
            var metadata=new Dictionary<string,object>{{"complete",true},{"synthetic",true}};
            typeof(Program).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Static)
                .Invoke(null,new object[]{args[0],samples,metadata});
            return 0;
        }
    }
}
''', encoding="utf-8")
    runner = tmp_path / "ArchiveFixture.exe"
    subprocess.run([str(COMPILER), "/nologo", "/platform:x64", "/main:TriggerCheck.ArchiveFixture",
                    "/r:System.Web.Extensions.dll", "/r:System.IO.Compression.FileSystem.dll",
                    "/r:System.IO.Compression.dll", "/out:" + str(runner),
                    str(ROOT / "tools/trigger_check/TriggerCheck.cs"), str(harness)], check=True, capture_output=True)
    folder = tmp_path / "synthetic-result"
    folder.mkdir()
    rows = [{"source":"xinput_state", "action":action, "values":{"lt":left,"rt":right}}
            for action,left,right in [("neutral",0,0),("LT",0,0),("LT",64,0),("LT",255,0),
                                      ("RT",0,0),("RT",0,255)]]
    (folder / "samples.jsonl").write_text("\n".join(map(json.dumps, rows)), encoding="utf-8")
    result = run(runner, folder)
    assert result.returncode == 0, result.stdout + result.stderr
    with zipfile.ZipFile(str(folder) + ".zip") as bundle:
        summary = json.loads(bundle.read("summary.json"))
        assert summary["LT"]["status"] == "intermediate_values_observed"
        assert json.loads(bundle.read("metadata.json"))["synthetic"] is True
        manifest = json.loads(bundle.read("manifest.json"))
        assert {e["path"] for e in manifest["files"]} == set(bundle.namelist()) - {"manifest.json"}
        for entry in manifest["files"]:
            content = bundle.read(entry["path"])
            assert len(content) == entry["size"]
            assert hashlib.sha256(content).hexdigest() == entry["sha256"]
