import importlib.util
from pathlib import Path


def load_sync():
    spec = importlib.util.spec_from_file_location("sync_readme_release", Path(__file__).parents[1] / "tools/sync_readme_release.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.sync


def test_download_tracks_latest_without_changing_other_readme_content(tmp_path):
    nav = tmp_path / "assets/readme/bg/nav1.svg"
    nav.parent.mkdir(parents=True)
    nav.write_text('<svg aria-label="Download v0.017"><text>Download v0.017</text><path d="M0,0"/></svg>', encoding="utf-8")
    readme = tmp_path / "README.md"
    readme.write_text('<p><a href="https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.017"><img src="assets/readme/bg/nav1.svg?v=26" alt="Download v0.017"></a><a href="CHANGELOG.md">Changelog</a></p>', encoding="utf-8")
    sync = load_sync()
    assert sync(tmp_path, "v0.018")
    assert 'href="https://github.com/AmjadAAYD/x20ctl/releases/latest"' in readme.read_text()
    assert 'alt="Download v0.018"' in readme.read_text()
    assert 'nav1.svg?v=release-v0.018' in readme.read_text()
    assert '<a href="CHANGELOG.md">Changelog</a>' in readme.read_text()
    assert nav.read_text() == '<svg aria-label="Download v0.018"><text>Download v0.018</text><path d="M0,0"/></svg>'
    assert not sync(tmp_path, "v0.018")
    assert sync(tmp_path, "v0.018.1")
    assert 'Download v0.018.1' in nav.read_text()


def test_invalid_tag_does_not_modify_files(tmp_path):
    import pytest
    with pytest.raises(ValueError, match="release tag"):
        load_sync()(tmp_path, 'v0.018"><script>')


def test_unexpected_artwork_leaves_readme_unchanged(tmp_path):
    import pytest
    nav = tmp_path / "assets/readme/bg/nav1.svg"
    nav.parent.mkdir(parents=True)
    nav.write_text('<svg aria-label="Download v0.017"><path/></svg>', encoding="utf-8")
    readme = tmp_path / "README.md"
    original = '<a href="old"><img src="assets/readme/bg/nav1.svg" alt="Download v0.017"></a>'
    readme.write_text(original, encoding="utf-8")
    with pytest.raises(ValueError, match="files left unchanged"):
        load_sync()(tmp_path, "v0.018")
    assert readme.read_text() == original
