import importlib.util
from pathlib import Path
import shutil
import subprocess
import sys

import pytest


script = Path(__file__).resolve().parents[1] / "tools/check_research_boundary.py"
spec = importlib.util.spec_from_file_location("research_boundary", script)
boundary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(boundary)


@pytest.mark.parametrize("path", [
    "docs/research/controller-contributor-kit/02-MAINTAINER-RESEARCH.md",
    "DOCS\\RESEARCH\\local-notes.md", "private-research", "research-inbox/tester.json",
    "captures/device/session.json", "receiver/.env.local", "vendor/app.apk",
    "sessions/experiment.pcapng", "reports/report.zip", "incoming/test.7z",
    "controller-report.txt", "docs/SUBMISSIONS_SETUP.md", "docs/.env.production",
    "outputs/input-captures.json", "outputs/hid-report-descriptor.bin",
])
def test_private_material_rejected_even_if_force_added(path):
    assert boundary.private_paths([path]) == [path]


def test_public_code_templates_and_documentation_remain_allowed():
    assert boundary.private_paths([
        "x20ctl/protocol.py", "x20ctl/controllers/catalog.json", "LICENSE",
        "docs/research-policy.md", "docs/controller-scan-kit/templates/INTAKE.md",
        "docs/controller-scan-kit/templates/EVIDENCE-MANIFEST.example.json",
        "tests/test_research_boundary.py", ".env.example",
        "src/assets/controllers/x20/controller.png",
    ]) == []


def test_directory_prefix_does_not_hide_unrelated_names():
    assert boundary.private_paths([
        "docs/research-policy.md", "captures-guide.md", "vendor-notes.md",
        "private-research-policy.md",
    ]) == []


def test_cli_rejects_force_added_research_without_changing_index(tmp_path):
    subprocess.run(["git", "init", str(tmp_path)], check=True, capture_output=True)
    (tmp_path / "tools").mkdir()
    shutil.copy2(script, tmp_path / "tools/check_research_boundary.py")
    (tmp_path / ".gitignore").write_text("research-inbox/\n", encoding="utf-8")
    evidence = tmp_path / "research-inbox/session.pcapng"
    evidence.parent.mkdir()
    evidence.write_bytes(b"synthetic test fixture")
    subprocess.run(["git", "add", ".gitignore"], cwd=tmp_path, check=True)
    command = [sys.executable, str(tmp_path / "tools/check_research_boundary.py")]
    assert subprocess.run(command, capture_output=True).returncode == 0
    subprocess.run(["git", "add", "-f", "research-inbox/session.pcapng"],
                   cwd=tmp_path, check=True)
    index = tmp_path / ".git/index"
    before = index.read_bytes()
    result = subprocess.run(command, capture_output=True, text=True)
    assert result.returncode == 1
    assert "research-inbox/session.pcapng" in result.stdout
    assert index.read_bytes() == before
