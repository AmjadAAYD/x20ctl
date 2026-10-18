"""Renames the GitHub releases published before the new version names (docs/versions.md). One-off, run by hand.

  python tools/rename_releases.py BACKUP.json                 dry run: prints each release's title and first note line, before and after
  python tools/rename_releases.py BACKUP.json --apply v0.2.1  renames that one release
  python tools/rename_releases.py BACKUP.json --apply all     renames every one

BACKUP.json is `gh api "repos/AmjadAAYD/x20ctl/releases?per_page=100"` saved before anything changed. Only the title and the
notes change; tags, assets, download counts, dates and the pre-release flag stay. The notes keep every word and gain one first
line with the new name. Before each edit the release is read again and must still match the backup, so nothing written since is
overwritten; a release that already has its new title is skipped, so running it twice is harmless.
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from version_name import legacy  # noqa: E402

REPO = "AmjadAAYD/x20ctl"
GUIDE = "https://github.com/AmjadAAYD/x20ctl/blob/main/docs/versions.md"
# what some old titles said besides the version, kept after the new name
SUFFIX = {"v4.1.0-preview.4": " (Controller Check test)", "v4.1.0-preview.3-exe": " (single exe)", "v4.1.0-preview.1-exe": " (single exe)"}


def plan(release: dict, names: dict) -> tuple[str, str] | None:
    tag = release["tag_name"]
    if tag not in names:
        return None
    name = names[tag]
    title = f"X20CTL {name}{SUFFIX.get(tag, '')}"
    body = release["body"] or ""
    first = f"**{name}** (tagged `{tag}` under the old numbering, see [version names]({GUIDE}))."
    return title, first + "\n\n" + body if body else first


def current(tag: str) -> dict:
    out = subprocess.run(["gh", "api", f"repos/{REPO}/releases/tags/{tag}"], capture_output=True, text=True, encoding="utf-8", check=True)
    return json.loads(out.stdout)


def main() -> None:
    backup = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    apply = sys.argv[3] if len(sys.argv) > 3 and sys.argv[2] == "--apply" else None
    names = legacy()
    for release in backup:
        tag = release["tag_name"]
        p = plan(release, names)
        if p is None:
            print(f"{tag}: not an app release, left alone ({release['name']})")
            continue
        title, body = p
        print(f"{tag}\n  title  {release['name']!r}\n     -> {title!r}\n  notes  + {body.splitlines()[0]}")
        if apply not in ("all", tag):
            continue
        live = current(tag)
        if live["name"] == title:
            print("  already renamed, skipped")
            continue
        if live["name"] != release["name"] or (live["body"] or "") != (release["body"] or ""):
            sys.exit(f"  {tag} changed since the backup; stopping without editing it")
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", newline="", suffix=".md", delete=False) as notes:
            notes.write(body)
        subprocess.run(["gh", "release", "edit", tag, "--repo", REPO, "--title", title, "--notes-file", notes.name], check=True)
        Path(notes.name).unlink()
        after = current(tag)
        ok = after["name"] == title and after["body"] == body and len(after["assets"]) == len(live["assets"]) and after["prerelease"] == live["prerelease"]
        print("  renamed, checked" if ok else "  RENAMED BUT THE READ-BACK DIFFERS")
        if not ok:
            sys.exit(1)


if __name__ == "__main__":
    main()
