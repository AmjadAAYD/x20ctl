"""Keep the README's download artwork in sync with GitHub's latest stable release.

Usage: python tools/sync_readme_release.py v0.018
The workflow obtains the tag from /releases/latest, not the triggering release.
"""
import re
import sys
from pathlib import Path


def sync(root: Path, tag: str) -> bool:
    if not re.fullmatch(r"v\d+\.\d+(?:\.\d+)?", tag):
        raise ValueError(f"Unsupported release tag: {tag!r}")
    readme = root / "README.md"
    nav = root / "assets/readme/bg/nav1.svg"
    original_readme = readme.read_text(encoding="utf-8")
    original_nav = nav.read_text(encoding="utf-8")
    label = f"Download {tag}"

    def button(match):
        image, count = re.subn(r'alt="Download [^"]+"', f'alt="{label}"', match[1])
        if count != 1:
            raise ValueError("Expected one download image label")
        image = re.sub(r'assets/readme/bg/nav1\.svg(?:\?[^"\s]*)?', f"assets/readme/bg/nav1.svg?v=release-{tag}", image)
        return f'<a href="https://github.com/AmjadAAYD/x20ctl/releases/latest">{image}</a>'

    updated_readme, links = re.subn(r'<a\s+href="[^"]+">(<img\s+[^>]*src="assets/readme/bg/nav1\.svg[^>]*>)</a>', button, original_readme)
    updated_nav, aria = re.subn(r'aria-label="Download [^"]+"', f'aria-label="{label}"', original_nav)
    updated_nav, text = re.subn(r'>Download [^<]+</text>', f'>{label}</text>', updated_nav)
    if (links, aria, text) != (1, 1, 1):
        raise ValueError("Expected one README download button and one SVG label; files left unchanged")
    changed = updated_readme != original_readme or updated_nav != original_nav
    if changed:
        readme.write_text(updated_readme, encoding="utf-8")
        nav.write_text(updated_nav, encoding="utf-8")
    return changed


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python tools/sync_readme_release.py TAG")
    changed = sync(Path(__file__).resolve().parents[1], sys.argv[1])
    print("Updated README download" if changed else "README download already current")
