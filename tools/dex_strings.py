"""Extract the string table from the dex files inside an APK.

Parses the dex string_ids table directly not scraping printable runs, so
the output is exactly the constants and identifiers the app was compiled with.
Useful as a first pass: it reveals BLE UUIDs, command names and obfuscation level
in seconds, before committing to a full decompile.

Usage:
    python dex_strings.py base.apk                     dump every string
    python dex_strings.py base.apk --grep uuid ble     only strings matching
    python dex_strings.py base.apk --classes           only class descriptors
    python dex_strings.py base.apk --out strings.txt   write to a file
"""

from __future__ import annotations

import argparse
import io
import re
import struct
import zipfile

DEX_MAGIC = b"dex\n"


def read_uleb128(buf: io.BytesIO) -> int:
    result = 0
    shift = 0
    while True:
        byte = buf.read(1)[0]
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result
        shift += 7


def dex_strings(data: bytes) -> list[str]:
    """Every entry of a dex file's string_ids table, in order."""
    if data[:4] != DEX_MAGIC:
        raise ValueError("not a dex file")

    # header: string_ids_size at 0x38, string_ids_off at 0x3C
    count, offset = struct.unpack_from("<II", data, 0x38)
    out: list[str] = []
    for i in range(count):
        (str_off,) = struct.unpack_from("<I", data, offset + i * 4)
        buf = io.BytesIO(data[str_off : str_off + 4096])
        read_uleb128(buf)  # utf16 length, not the byte length
        raw = buf.read()
        end = raw.find(b"\x00")
        chunk = raw if end < 0 else raw[:end]
        # dex uses MUTF-8; utf-8 with replacement is close enough for triage
        out.append(chunk.decode("utf-8", errors="replace"))
    return out


def collect(apk_path: str) -> list[str]:
    strings: list[str] = []
    with zipfile.ZipFile(apk_path) as z:
        for name in z.namelist():
            if re.fullmatch(r"classes\d*\.dex", name):
                strings.extend(dex_strings(z.read(name)))
    return strings


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk")
    parser.add_argument("--grep", nargs="*", help="case-insensitive substrings to keep")
    parser.add_argument("--classes", action="store_true", help="only Lcom/... descriptors")
    parser.add_argument("--min", type=int, default=1, help="minimum length")
    parser.add_argument("--out", help="write results to this file")
    args = parser.parse_args()

    strings = collect(args.apk)
    total = len(strings)

    seen: set[str] = set()
    kept: list[str] = []
    for s in strings:
        if len(s) < args.min or s in seen:
            continue
        if args.classes and not s.startswith("L"):
            continue
        if args.grep and not any(g.lower() in s.lower() for g in args.grep):
            continue
        seen.add(s)
        kept.append(s)

    body = "\n".join(kept)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as fh:
            fh.write(body)
        print(f"{total} strings total, {len(kept)} kept -> {args.out}")
    else:
        print(body)
        print(f"\n--- {total} strings total, {len(kept)} shown ---")


if __name__ == "__main__":
    main()
