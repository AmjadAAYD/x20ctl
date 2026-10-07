"""Reject recognizable private research material in the public Git index.

This is an accidental-publication check, not content redaction or copy protection.
It reads filenames only; it never reads controllers, uploads data or changes Git.
"""
from __future__ import annotations

import subprocess
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
PRIVATE_DIRECTORIES = (
    "docs/research", "private-research", "research-inbox", "captures",
    "vendor", "jadx-out", "receiver",
)
PRIVATE_FILES = frozenset({
    "docs/submissions_setup.md", "controller-report.txt",
})
RAW_NAMES = frozenset({
    "usb-descriptors.txt", "hid-report-descriptor.bin",
    "hid-report-descriptor-parsed.txt", "input-captures.json",
})
PRIVATE_SUFFIXES = frozenset({".pcap", ".pcapng", ".zip", ".7z", ".apk", ".dex"})


def private_paths(paths: list[str]) -> list[str]:
    violations = []
    for original in paths:
        normalized = original.replace("\\", "/").casefold()
        path = PurePosixPath(normalized)
        private_directory = any(
            normalized == prefix or normalized.startswith(prefix + "/")
            for prefix in PRIVATE_DIRECTORIES
        )
        secret = path.name.startswith(".env") and path.name != ".env.example"
        if (private_directory or normalized in PRIVATE_FILES or
                path.name in RAW_NAMES or path.suffix in PRIVATE_SUFFIXES or secret):
            violations.append(original)
    return violations


def main() -> int:
    result = subprocess.run(
        ["git", "ls-files", "--cached", "-z"], cwd=ROOT,
        capture_output=True, check=True,
    )
    paths = result.stdout.decode("utf-8", errors="surrogateescape").split("\0")
    violations = private_paths([path for path in paths if path])
    if violations:
        print("Private research material is staged/tracked in the public repository:")
        for path in violations:
            print(f"  {path}")
        print("Preserve it in the private research workspace before publishing.")
        return 1
    print("Public research boundary passed: no recognized private files in Git's index.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
