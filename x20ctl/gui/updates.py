"""Ask GitHub whether there is a newer release.

Called two ways: from the Updates button in the header, and once at launch by
`gui.updatebar`. The launch check was added deliberately, replacing an earlier
on-demand-only rule, and it earns its keep by being silent: it says nothing at
all unless a newer version exists, and it never blocks the UI. A check that
reports "you are fine" every launch is the thing worth avoiding, not the check
itself.

This module stays free of Qt so version comparison can be tested without a
network or a display. The fetch is injected for the same reason.
"""

from __future__ import annotations

import json
import re
import urllib.request
from dataclasses import dataclass

RELEASES_API = "https://api.github.com/repos/AmjadAAYD/x20ctl/releases/latest"
RELEASES_PAGE = "https://github.com/AmjadAAYD/x20ctl/releases"
TIMEOUT = 8


@dataclass(frozen=True)
class Release:
    """What the newest release calls itself."""

    version: str
    url: str = RELEASES_PAGE
    notes: str = ""


def parse_version(text: str) -> tuple:
    """Turn '0.2.1' or 'v0.2.1-beta' into something comparable.

    Anything unparseable sorts lowest rather than raising, so a tag somebody
    typed by hand cannot break the check.
    """
    numbers = re.findall(r"\d+", text or "")
    return tuple(int(n) for n in numbers[:3]) or (0,)


def is_newer(candidate: str, current: str) -> bool:
    """Whether `candidate` is a later version than `current`."""
    return parse_version(candidate) > parse_version(current)


def fetch_latest() -> Release:
    """Ask GitHub for the newest release. Raises on any network trouble."""
    request = urllib.request.Request(
        RELEASES_API, headers={"Accept": "application/vnd.github+json",
                               "User-Agent": "x20ctl"})
    with urllib.request.urlopen(request, timeout=TIMEOUT) as response:
        payload = json.load(response)
    return Release(
        version=payload.get("tag_name") or payload.get("name") or "",
        url=payload.get("html_url") or RELEASES_PAGE,
        notes=(payload.get("body") or "").strip(),
    )


def check(current: str, fetch=fetch_latest) -> tuple[str, Release | None]:
    """Compare the running version against the newest release.

    Returns a sentence to show and the release if one is newer. Network
    failures come back as a sentence too: not being able to reach GitHub is
    not an error worth interrupting anybody over.
    """
    try:
        release = fetch()
    except Exception as exc:                    # noqa: BLE001 - shown to user
        reason = str(exc).strip() or exc.__class__.__name__
        return f"Could not reach GitHub: {reason}", None

    if not release.version:
        return "GitHub did not name a version.", None
    if is_newer(release.version, current):
        return f"Version {release.version} is available.", release
    return f"You are on the newest version ({current}).", None
