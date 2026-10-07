"""Inspect the local executable archive without starting the app or any device API."""
import hashlib
import json
from pathlib import Path

from PyInstaller.archive.readers import CArchiveReader

root = Path(__file__).resolve().parents[1]
exe = root / "dist/local-macro-library-x15-20261002/x20ctl.exe"
archive = CArchiveReader(str(exe))
checked = []
for source in sorted((root / "dist-ui").rglob("*")):
    if source.is_file():
        entry = str(source.relative_to(root)).replace("/", "\\")
        assert archive.extract(entry) == source.read_bytes(), entry
        checked.append(entry)
catalog = root / "x20ctl/controllers/catalog.json"
assert archive.extract("x20ctl\\controllers\\catalog.json") == catalog.read_bytes()
pyz = archive.open_embedded_archive("PYZ.pyz")
assert "x20ctl.desktop.macro_library" in pyz.toc
report = {
    "passed": True, "exe": str(exe), "bytes": exe.stat().st_size,
    "sha256": hashlib.sha256(exe.read_bytes()).hexdigest(),
    "frontendFilesMatched": len(checked), "catalogMatched": True,
    "macroLibraryModulePresent": True, "nativeGuiLaunched": False, "hardwareAccess": False,
}
(root / "artifacts/macro-library-x15/local-desktop-build.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report))
