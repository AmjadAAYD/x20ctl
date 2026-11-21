"""Keep README.md, README.plain.md, and assets/readme/hero.svg in sync with the latest GitHub release.

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


def _sub_req(pattern: str, repl: str, text: str, label: str, minimum: int = 1) -> str:
    updated, count = re.subn(pattern, repl, text)
    if count < minimum:
        raise ValueError(f"Expected {label}; files left unchanged")
    return updated


def _sync_hero_svg(text: str, ver: str) -> str:
    updated = re.sub(rf"\b{VER_RE}\b", ver, text)
    if (
        updated == text
        and not re.search(rf'id="hero-version">{re.escape(ver)}<', text)
    ):
        raise ValueError("Expected version token in assets/readme/hero.svg; files left unchanged")
    return updated


def _sync_markdown_downloads(text: str, label_prefix: str, tag: str, ver: str, min_headings: int = 2) -> str:
    updated = _sub_req(
        rf"{re.escape(REPO_RELEASES)}/tag/{TAG_RE}",
        f"{REPO_RELEASES}/tag/{tag}",
        text,
        f"{label_prefix} release tag URL",
        minimum=2,
    )
    updated = _sub_req(
        rf"(\*\*|Download |Release |\(){VER_RE}",
        rf"\g<1>{ver}",
        updated,
        f"{label_prefix} version headings",
        minimum=min_headings,
    )
    updated = _sub_req(
        rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/SHA256SUMS\.txt",
        f"{REPO_RELEASES}/download/{tag}/SHA256SUMS.txt",
        updated,
        f"{label_prefix} SHA256SUMS URL",
    )
    updated = _sub_req(
        rf"{re.escape(REPO_RELEASES)}/download/{TAG_RE}/X20CTL-{VER_RE}(-[^\"\s)<]+)",
        rf"{REPO_RELEASES}/download/{tag}/X20CTL-{ver}\1",
        updated,
        f"{label_prefix} asset download URLs",
        minimum=7,
    )
    updated = _sub_req(
        rf"(?<![/\w])X20CTL-{VER_RE}(-[A-Za-z0-9_.-]+\.(?:exe|AppImage|flatpak|tar\.gz|zip))",
        rf"X20CTL-{ver}\1",
        updated,
        f"{label_prefix} asset filename labels",
        minimum=7,
    )
    return updated


def sync(root: Path, tag: str) -> bool:
    if not re.fullmatch(TAG_RE, tag):
        raise ValueError(f"Unsupported release tag: {tag!r}")

    ver = release_name(tag)
    readme = root / "README.md"
    plain = root / "README.plain.md"
    hero_svg = root / "assets" / "readme" / "hero.svg"

    if not readme.is_file() or not hero_svg.is_file():
        raise ValueError("Missing README.md or assets/readme/hero.svg; files left unchanged")

    original_readme = readme.read_text(encoding="utf-8")
    updated_readme = _sync_markdown_downloads(original_readme, "README.md", tag, ver, min_headings=2)
    updated_readme = _sub_req(
        r"(assets/readme/(?:hero|thanks)\.svg)\?v=release-[^\s\"']+",
        rf"\1?v=release-{tag}",
        updated_readme,
        "README artwork cache-busters",
        minimum=2,
    )
    updated_readme = _sub_req(
        rf'(alt="X20CTL ){VER_RE}',
        rf"\g<1>{ver}",
        updated_readme,
        "README hero alt version",
    )

    pending: list[tuple[Path, str, str]] = [(readme, original_readme, updated_readme)]

    if plain.is_file():
        original_plain = plain.read_text(encoding="utf-8")
        updated_plain = _sync_markdown_downloads(original_plain, "README.plain.md", tag, ver, min_headings=4)
        pending.append((plain, original_plain, updated_plain))

    original_hero = hero_svg.read_text(encoding="utf-8")
    updated_hero = _sync_hero_svg(original_hero, ver)
    pending.append((hero_svg, original_hero, updated_hero))

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
