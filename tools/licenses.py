"""Collect original license notices from the isolated release build environment."""
from __future__ import annotations
from importlib.metadata import distributions
from pathlib import Path
import sys


def collect(root: Path):
    sections = [(root / "THIRD_PARTY.md").read_text(encoding="utf-8")]
    microsoft_terms = root / "licenses" / "MICROSOFT-WEBVIEW2.txt"
    if microsoft_terms.exists():
        sections.append(
            "Microsoft Edge WebView2 fixed-version runtime terms\n"
            + microsoft_terms.read_text(encoding="utf-8")
        )
    for distribution in sorted(distributions(), key=lambda d: d.metadata["Name"].lower()):
        notices = []
        for entry in distribution.files or []:
            if any(word in entry.name.lower() for word in ("license", "copying", "notice")):
                path = distribution.locate_file(entry)
                if path.is_file() and path.suffix.lower() not in (".py", ".pyc", ".exe", ".dll"):
                    notices.append(f"{entry}\n{path.read_text(encoding='utf-8', errors='replace')}")
        if not notices and distribution.metadata.get("License"):
            notices.append(distribution.metadata["License"])
        sections.append(f"\n{'=' * 72}\n{distribution.metadata['Name']} {distribution.version}\n"+"\n\n".join(notices))
    python_license = Path(sys.base_prefix) / "LICENSE.txt"
    if python_license.exists():
        sections.append("Python runtime\n" + python_license.read_text(encoding="utf-8"))
    for package in ("react", "react-dom", "scheduler", "lucide-react"):
        folder = root / "node_modules" / package
        for path in folder.glob("LICENSE*"):
            sections.append(f"{package}\n"+path.read_text(encoding="utf-8"))
    output = root / "artifacts" / "THIRD_PARTY_LICENSES.txt"
    output.parent.mkdir(exist_ok=True)
    output.write_text("\n\n".join(sections), encoding="utf-8")
    return output


if __name__ == "__main__":
    print(collect(Path(__file__).resolve().parents[1]))
