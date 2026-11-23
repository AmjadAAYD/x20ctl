"""Keep README.md, README.plain.md, and release SVG assets in sync with the latest GitHub release.

Usage:
    python tools/sync_readme_release.py v0.025
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(Path(__file__).resolve().parent) not in sys.path:
    sys.path.insert(0, str(Path(__file__).resolve().parent))

from version_name import name as release_name  # noqa: E402

REPO_RELEASES = "https://github.com/AmjadAAYD/x20ctl/releases"
VER_RE = r"V\d+\.\d+(?:\.\d+)?"
TAG_RE = r"v\d+\.\d+(?:\.\d+)?"

SVG_RELEASE_FILES = (
    "btn-download.svg",
    "hero-motion.svg",
)


def _sub_req(pattern: str, repl: str, text: str, label: str, minimum: int = 1) -> str:
    updated, count = re.subn(pattern, repl, text)
    if count < minimum:
        raise ValueError(f"Expected {label}; files left unchanged")
    return updated


def _sync_svg_text(text: str, filename: str, ver: str, tag: str) -> str:
    updated = re.sub(rf"\b{VER_RE}\b", ver, text)
    updated = re.sub(rf"\bTAG\s+{TAG_RE}\b", f"TAG {tag}", updated)
    if updated == text and not re.search(rf"\b{re.escape(ver)}\b", text):
        raise ValueError(f"Expected version token in {filename}; files left unchanged")
    return updated


def sync(root: Path, tag: str) -> bool:
    if not re.fullmatch(TAG_RE, tag):
        raise ValueError(f"Unsupported release tag: {tag!r}")

    ver = release_name(tag)
    readme = root / "README.md"
    plain = root / "README.plain.md"
    assets_dir = root / "assets" / "readme"

    required_svgs = [assets_dir / rel for rel in SVG_RELEASE_FILES]
    if not readme.is_file() or any(not p.is_file() for p in required_svgs):
        raise ValueError("Missing README.md or release SVG assets; files left unchanged")

    original_readme = readme.read_text(encoding="utf-8")
    updated_readme = original_readme

    updated_readme = _sub_req(
        rf'(<a\s+href="{re.escape(REPO_RELEASES)}/)tag/{TAG_RE}("><img\s+[^>]*src="assets/readme/btn-download\.svg)(\?[^"]*)?("[^>]*alt="Download ){VER_RE}(")',
        rf"\g<1>tag/{tag}\g<2>?v=release-{tag}\g<4>{ver}\g<5>",
        updated_readme,
        "README btn-download release button",
    )
    updated_readme = re.sub(
        rf"{re.escape(REPO_RELEASES)}/tag/{TAG_RE}",
        f"{REPO_RELEASES}/tag/{tag}",
        updated_readme,
    )
    updated_readme = re.sub(
        r"(assets/readme/(?:bg/)?[a-z0-9_-]+\.svg)\?v=release-[^\s\"']+",
        rf"\1?v=release-{tag}",
        updated_readme,
    )
    updated_readme = _sub_req(
        rf'(alt="X20CTL ){VER_RE}(:|")',
        rf"\g<1>{ver}\g<2>",
        updated_readme,
        "README hero version alt",
    )
    updated_readme = _sub_req(
        rf"(## Download \(){VER_RE}(\))",
        rf"\g<1>{ver}\g<2>",
        updated_readme,
        "README download heading",
    )
    updated_readme = _sub_req(
        rf"(\*\*Release ){VER_RE}(\*\*)",
        rf"\g<1>{ver}\g<2>",
        updated_readme,
        "README Release link label",
    )
    updated_readme = _sub_req(
        rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/SHA256SUMS\.txt",
        f"{REPO_RELEASES}/download/{tag}/SHA256SUMS.txt",
        updated_readme,
        "README SHA256SUMS download URL",
    )
    updated_readme = _sub_req(
        rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/X20CTL-{VER_RE}(-[^\"\s)<`\]]+)",
        rf"{REPO_RELEASES}/download/{tag}/X20CTL-{ver}\1",
        updated_readme,
        "README asset download URLs",
        minimum=7,
    )
    updated_readme = _sub_req(
        rf"\[`X20CTL-{VER_RE}(-[^`\]]+)`\]",
        rf"[`X20CTL-{ver}\1`]",
        updated_readme,
        "README asset link labels",
        minimum=7,
    )

    pending: list[tuple[Path, str, str]] = [(readme, original_readme, updated_readme)]

    if plain.is_file():
        original_plain = plain.read_text(encoding="utf-8")
        updated_plain = _sub_req(
            rf"{re.escape(REPO_RELEASES)}/tag/{TAG_RE}",
            f"{REPO_RELEASES}/tag/{tag}",
            original_plain,
            "README.plain.md tag URL",
            minimum=2,
        )
        updated_plain = _sub_req(
            rf"(\*\*|\[Download |Release |\(){VER_RE}",
            rf"\g<1>{ver}",
            updated_plain,
            "README.plain.md version headings",
            minimum=4,
        )
        updated_plain = _sub_req(
            rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/SHA256SUMS\.txt",
            f"{REPO_RELEASES}/download/{tag}/SHA256SUMS.txt",
            updated_plain,
            "README.plain.md SHA256SUMS link",
        )
        updated_plain = _sub_req(
            rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/X20CTL-{VER_RE}(-[^\"\s)<]+)",
            rf"{REPO_RELEASES}/download/{tag}/X20CTL-{ver}\1",
            updated_plain,
            "README.plain.md asset download URLs",
            minimum=7,
        )
        updated_plain = _sub_req(
            rf"(?<![/\w])X20CTL-{VER_RE}(-[A-Za-z0-9_.-]+\.(?:exe|AppImage|flatpak|tar\.gz|zip))",
            rf"X20CTL-{ver}\1",
            updated_plain,
            "README.plain.md filename labels",
            minimum=7,
        )
        pending.append((plain, original_plain, updated_plain))

    for svg_path in required_svgs:
        orig_svg = svg_path.read_text(encoding="utf-8")
        new_svg = _sync_svg_text(orig_svg, svg_path.name, ver, tag)
        pending.append((svg_path, orig_svg, new_svg))

    changed = any(new != old for _, old, new in pending)
    if changed:
        for path, old, new in pending:
            if new != old:
                path.write_text(new, encoding="utf-8")
    return changed


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(f"Usage: {Path(sys.argv[0]).name} v<version>")
    changed = sync(ROOT, sys.argv[1])
    print("Updated README release links and artwork" if changed else "README release links and artwork already current")


if __name__ == "__main__":
    main()
