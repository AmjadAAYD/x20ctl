"""Print the CHANGELOG.md section for one release (release_notes.py v0.017, or an old tag such as v4.1.0-preview.7), for the release workflow.
Sections are headed by the version name people see (## [V0.017] - date), see tools/version_name.py."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from version_name import name  # noqa: E402

wanted = name(sys.argv[1])
text = Path(__file__).resolve().parents[1].joinpath("CHANGELOG.md").read_text(encoding="utf-8")
match = re.search(rf"^## \[{re.escape(wanted)}\][^\n]*\n(.*?)(?=^## |\Z)", text, re.S | re.M)
if not match:
    sys.exit(f"CHANGELOG.md has no section for {wanted}")
print(match.group(1).strip())
