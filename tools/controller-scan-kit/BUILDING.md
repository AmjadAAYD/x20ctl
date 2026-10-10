# Build and verify from source

Build on Windows x64 with Python **3.12 x64**. No controller is required.
From this folder:

```powershell
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --require-hashes --only-binary=:all: -r requirements-build.lock
$env:Path = "$(Resolve-Path .\.venv\Scripts);$env:Path"
.\scripts\build.ps1
```

The script runs unit tests and the source self-check, builds with PyInstaller, executes
hardware-free packaged checks, checks the unsigned status, requires a successful Microsoft
Defender custom scan, records scan definitions/result, and packages files/checksums.
Missing Defender or a failed check blocks packaging. Local build metadata says `local`;
local builds do not gain GitHub provenance just by running this script.

To check parsers/policy without Windows dependencies (Linux or Windows):

```text
python -m unittest discover -s tests -v
python src/controller_scan.py --dry-run
```

On Windows, install the runtime lock first: the self-check verifies WinRT/Bleak imports.
On non-Windows, those import and Win32 ABI checks are explicitly skipped; the self-check
output records that limit. No live hardware collection is supported outside Windows.

Official releases use `.github/workflows/controller-scan-kit.yml`. On a change to the kit
on `main`, tests and CodeQL gate the Windows build. The version in `src/controller_scan_kit/__init__.py`
produces a separate `scanner-vVERSION` release, targeting that exact build commit.
The workflow does not replace assets of an existing release. Scanner releases are marked
`--latest=false`, preserving the desktop app's latest-release/update behavior.

For updates, change the kit version and `pyproject.toml`, document the changes, and refresh
hash locks from the pinned distributions. Build tools and runtime packages are installed
with `--require-hashes --only-binary=:all:`. PyPI wheel hashes identify published bytes;
they do not prove a dependency itself is safe. Action references are full commit SHAs.

CodeQL/Dependabot are configured in repository files. Secret scanning is a repository
setting and is not claimed enabled by these files. No signing certificate exists in the
build; Windows code signing and byte-identical rebuilds are not claimed.
