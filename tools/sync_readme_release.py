"""Keep README.md and assets/readme/hero.svg in sync with the latest GitHub release.

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
VER_TOKEN = r"V\d+\.\d+(?:\.\d+)?"


def _sub_required(pattern: str, repl: str, text: str, label: str, minimum: int = 1) -> tuple[str, int]:
    updated, count = re.subn(pattern, repl, text)
    if count < minimum:
        raise ValueError(f"Expected at least {minimum} {label} marker(s); files left unchanged")
    return updated, count


def sync(root: Path, tag: str) -> bool:
    if not re.fullmatch(r"v\d+\.\d+(?:\.\d+)?", tag):
        raise ValueError(f"Unsupported release tag: {tag!r}")

    ver = release_name(tag)
    readme = root / "README.md"
    hero = root / "assets" / "readme" / "hero.svg"
    if not readme.is_file() or not hero.is_file():
        raise ValueError("Missing README.md or assets/readme/hero.svg; files left unchanged")

    original_readme = readme.read_text(encoding="utf-8")
    original_hero = hero.read_text(encoding="utf-8")

    # 1. Sync hero.svg (aria-label + visible release pill)
    updated_hero, _ = _sub_required(
        rf'aria-label="X20CTL {VER_TOKEN}([^"]*)"',
        rf'aria-label="X20CTL {ver}\1"',
        original_hero,
        "hero aria-label",
    )
    updated_hero, _ = _sub_required(
        rf'(<text\s+[^>]*id="release-badge"[^>]*>RELEASE · ){VER_TOKEN}(</text>)',
        rf'\g<1>{ver}\2',
        updated_hero,
        "hero release-badge",
    )

    # 2. Sync README.md hero artwork reference & cache-buster
    updated_readme, _ = _sub_required(
        r'assets/readme/hero\.svg(?:\?[^"\s]*)?',
        f"assets/readme/hero.svg?v=release-{tag}",
        original_readme,
        "README hero image",
    )
    updated_readme, _ = _sub_required(
        rf'alt="X20CTL {VER_TOKEN}([^"]*)"',
        rf'alt="X20CTL {ver}\1"',
        updated_readme,
        "README hero alt",
    )

    # 3. Sync release page tag links, CTA labels, SHA256SUMS, and Windows/Linux assets
    updated_readme, _ = _sub_required(
        rf"{re.escape(REPO_RELEASES)}/tag/v[^\"\s)#]+",
        f"{REPO_RELEASES}/tag/{tag}",
        updated_readme,
        "release tag URL",
    )
    updated_readme, _ = _sub_required(
        rf"(Download |Release ){VER_TOKEN}",
        rf"\g<1>{ver}",
        updated_readme,
        "release version heading/link text",
    )
    updated_readme, _ = _sub_required(
        rf"{re.escape(REPO_RELEASES)}/download/v[^/\"\s)]+/SHA256SUMS\.txt",
        f"{REPO_RELEASES}/download/{tag}/SHA256SUMS.txt",
        updated_readme,
        "SHA256SUMS link",
    )
    updated_readme, _ = _sub_required(
        rf"{re.escape(REPO_RELEASES)}/download/v[^/\"\s)]+/X20CTL-{VER_TOKEN}(-[^/\"\s)<]+)",
        rf"{REPO_RELEASES}/download/{tag}/X20CTL-{ver}\1",
        updated_readme,
        "release download URL",
        minimum=7,
    )
    updated_readme, _ = _sub_required(
        rf"(?<![/\w])X20CTL-{VER_TOKEN}(-[A-Za-z0-9_.-]+\.(?:exe|AppImage|flatpak|tar\.gz|zip))",
        rf"X20CTL-{ver}\1",
        updated_readme,
        "release filename label",
        minimum=7,
    )

    changed = updated_readme != original_readme or updated_hero != original_hero
    if changed:
        readme.write_text(updated_readme, encoding="utf-8")
        hero.write_text(updated_hero, encoding="utf-8")
    return changed


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python tools/sync_readme_release.py TAG")
    changed = sync(ROOT, sys.argv[1])
    print("Updated README and hero artwork" if changed else "README and hero artwork already current")
