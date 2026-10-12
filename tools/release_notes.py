"""Print the CHANGELOG.md section for one version (release_notes.py 4.1.0-preview.5), for the release workflow."""
import re
import sys
from pathlib import Path

version = sys.argv[1].removeprefix("v")
text = Path(__file__).resolve().parents[1].joinpath("CHANGELOG.md").read_text(encoding="utf-8")
match = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(.*?)(?=^## \[|\Z)", text, re.S | re.M)
if not match:
    sys.exit(f"CHANGELOG.md has no section for {version}")
print(match.group(1).strip())
