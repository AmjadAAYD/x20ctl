import importlib.util
from pathlib import Path
import pytest


def load_sync():
    spec = importlib.util.spec_from_file_location(
        "sync_readme_release", Path(__file__).parents[1] / "tools/sync_readme_release.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.sync


def _seed_release_files(root: Path) -> tuple[Path, Path]:
    hero = root / "assets/readme/hero.svg"
    hero.parent.mkdir(parents=True)
    hero.write_text(
        '<svg aria-label="X20CTL V0.025 — Studio">'
        '<text id="release-badge" x="0" y="0">RELEASE · V0.025</text></svg>',
        encoding="utf-8",
    )
    assets = [
        "X20CTL-V0.025-windows-x64-setup.exe",
        "X20CTL-V0.025-windows-x64.exe",
        "X20CTL-V0.025-linux-x86_64.AppImage",
        "X20CTL-V0.025-linux-x86_64.flatpak",
        "X20CTL-V0.025-linux-x64.tar.gz",
        "X20CTL-V0.025-linux-musl-x64.tar.gz",
        "X20CTL-V0.025-linux-check.zip",
    ]
    links = "\n".join(
        f"[`{a}`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.025/{a})"
        for a in assets
    )
    readme = root / "README.md"
    readme.write_text(
        '<a href="https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.025">'
        '<img src="assets/readme/hero.svg?v=release-v0.025" alt="X20CTL V0.025 — Studio"></a>\n'
        "[Download V0.025](#download) · [Release V0.025](https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.025)\n"
        "[SHA256SUMS.txt](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.025/SHA256SUMS.txt)\n"
        f"{links}\n"
        "[CHANGELOG](CHANGELOG.md)\n",
        encoding="utf-8",
    )
    return readme, hero


def test_sync_updates_all_windows_linux_and_hero_markers(tmp_path):
    readme, hero = _seed_release_files(tmp_path)
    sync = load_sync()

    assert sync(tmp_path, "v0.026")
    text = readme.read_text(encoding="utf-8")
    svg = hero.read_text(encoding="utf-8")

    assert "assets/readme/hero.svg?v=release-v0.026" in text
    assert 'alt="X20CTL V0.026 — Studio"' in text
    assert "https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.026" in text
    assert "Download V0.026" in text
    assert "Release V0.026" in text
    assert "https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/SHA256SUMS.txt" in text
    for asset in (
        "X20CTL-V0.026-windows-x64-setup.exe",
        "X20CTL-V0.026-windows-x64.exe",
        "X20CTL-V0.026-linux-x86_64.AppImage",
        "X20CTL-V0.026-linux-x86_64.flatpak",
        "X20CTL-V0.026-linux-x64.tar.gz",
        "X20CTL-V0.026-linux-musl-x64.tar.gz",
        "X20CTL-V0.026-linux-check.zip",
    ):
        assert f"https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/{asset}" in text
        assert f"[`{asset}`]" in text
    assert "v0.025" not in text and "V0.025" not in text
    assert "[CHANGELOG](CHANGELOG.md)" in text

    assert 'aria-label="X20CTL V0.026 — Studio"' in svg
    assert '<text id="release-badge" x="0" y="0">RELEASE · V0.026</text>' in svg

    assert not sync(tmp_path, "v0.026")
    assert sync(tmp_path, "v0.026.1")
    assert "X20CTL-V0.026.1-linux-x86_64.flatpak" in readme.read_text(encoding="utf-8")
    assert "RELEASE · V0.026.1" in hero.read_text(encoding="utf-8")


def test_invalid_tag_does_not_modify_files(tmp_path):
    with pytest.raises(ValueError, match="release tag"):
        load_sync()(tmp_path, 'v0.026"><script>')


def test_unexpected_artwork_leaves_readme_unchanged(tmp_path):
    hero = tmp_path / "assets/readme/hero.svg"
    hero.parent.mkdir(parents=True)
    hero.write_text("<svg><path/></svg>", encoding="utf-8")
    readme = tmp_path / "README.md"
    original = '<img src="assets/readme/hero.svg" alt="X20CTL">'
    readme.write_text(original, encoding="utf-8")

    with pytest.raises(ValueError, match="files left unchanged"):
        load_sync()(tmp_path, "v0.026")
    assert readme.read_text(encoding="utf-8") == original


def test_repository_readme_and_hero_sync_cleanly_at_current_release(tmp_path):
    import re
    import shutil

    root = Path(__file__).parents[1]
    text = (root / "README.md").read_text(encoding="utf-8")
    match = re.search(r"assets/readme/hero\.svg\?v=release-(v\d+\.\d+(?:\.\d+)?)", text)
    assert match, "Expected release cache-buster tag on README hero image"
    sync = load_sync()
    assert not sync(root, match.group(1))

    scratch = tmp_path / "repo"
    (scratch / "assets/readme").mkdir(parents=True)
    shutil.copy2(root / "README.md", scratch / "README.md")
    shutil.copy2(root / "assets/readme/hero.svg", scratch / "assets/readme/hero.svg")
    assert sync(scratch, "v9.999")
    assert "RELEASE · V9.999" in (scratch / "assets/readme/hero.svg").read_text(encoding="utf-8")
    assert "X20CTL-V9.999-windows-x64-setup.exe" in (scratch / "README.md").read_text(encoding="utf-8")
    assert "X20CTL-V9.999-linux-x86_64.flatpak" in (scratch / "README.md").read_text(encoding="utf-8")
