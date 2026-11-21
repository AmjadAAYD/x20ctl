import importlib.util
from pathlib import Path
import shutil
import re
import pytest


def load_sync():
    spec = importlib.util.spec_from_file_location(
        "sync_readme_release", Path(__file__).parents[1] / "tools/sync_readme_release.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_sync_updates_obsidian_bg_slices_and_plain_twin(tmp_path):
    src_root = Path(__file__).parents[1]
    shutil.copy2(src_root / "README.md", tmp_path / "README.md")
    shutil.copy2(src_root / "README.plain.md", tmp_path / "README.plain.md")
    shutil.copytree(src_root / "assets/readme/bg", tmp_path / "assets/readme/bg")

    mod = load_sync()
    assert not mod.sync(tmp_path, "v0.025")

    assert mod.sync(tmp_path, "v0.026")
    md_text = (tmp_path / "README.md").read_text(encoding="utf-8")
    plain_text = (tmp_path / "README.plain.md").read_text(encoding="utf-8")

    assert "https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.026" in md_text
    assert "nav_1.svg?v=release-v0.026" in md_text
    assert 'alt="Download V0.026"' in md_text
    assert "https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/SHA256SUMS.txt" in md_text
    for asset in (
        "X20CTL-V0.026-windows-x64-setup.exe",
        "X20CTL-V0.026-windows-x64.exe",
        "X20CTL-V0.026-linux-x86_64.AppImage",
        "X20CTL-V0.026-linux-x86_64.flatpak",
        "X20CTL-V0.026-linux-x64.tar.gz",
        "X20CTL-V0.026-linux-musl-x64.tar.gz",
        "X20CTL-V0.026-linux-check.zip",
    ):
        url = f"https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/{asset}"
        assert url in md_text
        assert url in plain_text
        assert f"[`{asset}`]" in plain_text

    assert "v0.025" not in md_text and "V0.025" not in md_text
    assert "v0.025" not in plain_text and "V0.025" not in plain_text
    assert "V0.026" in (tmp_path / "assets/readme/bg/hero.svg").read_text(encoding="utf-8")
    assert "Download V0.026" in (tmp_path / "assets/readme/bg/nav_1.svg").read_text(encoding="utf-8")
    assert "DOWNLOAD — V0.026" in (tmp_path / "assets/readme/bg/download_head.svg").read_text(encoding="utf-8")
    assert "TAG v0.026" in (tmp_path / "assets/readme/bg/download_head.svg").read_text(encoding="utf-8")
    assert "V0.026" in (tmp_path / "assets/readme/bg/end.svg").read_text(encoding="utf-8")

    assert not mod.sync(tmp_path, "v0.026")
    assert mod.sync(tmp_path, "v0.026.1")
    assert "X20CTL-V0.026.1-linux-x86_64.flatpak" in (tmp_path / "assets/readme/bg/dl_lin1_2.svg").read_text(encoding="utf-8")


def test_invalid_tag_does_not_modify_files(tmp_path):
    with pytest.raises(ValueError, match="release tag"):
        load_sync().sync(tmp_path, 'v0.026"><script>')


def test_malformed_slices_leave_readme_unchanged(tmp_path):
    mod = load_sync()
    bg = tmp_path / "assets/readme/bg"
    bg.mkdir(parents=True)
    for name in mod.SVG_RELEASE_SLICES:
        (bg / name).write_text("<svg/>", encoding="utf-8")
    original = '<a href="old"><img src="assets/readme/bg/nav_1.svg" alt="Download V0.025"></a>'
    (tmp_path / "README.md").write_text(original, encoding="utf-8")

    with pytest.raises(ValueError, match="files left unchanged"):
        mod.sync(tmp_path, "v0.026")
    assert (tmp_path / "README.md").read_text(encoding="utf-8") == original


def test_repository_readme_is_currently_synced():
    root = Path(__file__).parents[1]
    text = (root / "README.md").read_text(encoding="utf-8")
    match = re.search(r"nav_1\.svg\?v=release-(v\d+\.\d+(?:\.\d+)?)", text)
    assert match, "Expected release cache-buster tag on README nav_1 image"
    assert not load_sync().sync(root, match.group(1))
