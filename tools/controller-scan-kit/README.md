# X20ctl Controller Scan Kit

A standalone Windows 10/11 x64 console tool for volunteers helping research controller support.
It compares disconnected/connected devices, asks you to confirm your controller, and reads
standard metadata. Gameplay input and Bluetooth research are optional. Reports stay on
your computer; **the kit has no uploader, telemetry, firmware operations or settings writes**.
The model printed on your controller is your claim, not automatic support detection.

This folder contains the complete scanner source. It is separate from the desktop app and
its integrated `x20ctl-scanner.exe` compatibility-report helper. The desktop app's optional
update checks and consent-based uploads are not features of this standalone kit.

## Download and run

1. Open [GitHub Releases](https://github.com/AmjadAAYD/x20ctl/releases) and choose a
   **Controller Scan Kit** release with a `scanner-v…` tag.
2. Download `ControllerScanKit-1.1.0-win-x64.zip` and extract it.
3. [Verify its checksums and build provenance](VERIFY_RELEASE.md).
4. Run `START_HERE.bat`, choose the ordinary controller scan, and follow the prompts.
5. Read `BEFORE_YOU_SEND.txt` and inspect your local results before creating/sharing the ZIP.
   Send it privately in your existing conversation with Amjad if you choose.

No Python installation is needed for the portable executable. The EXE is unsigned;
build provenance is separate from Windows code signing. Do not disable antivirus or bypass
a warning to run a file you do not trust. A scan does not require administrator access.
No physical-controller compatibility is established by the release's automated tests.

## Run the source instead

Use Windows x64 and Python 3.12 x64. From this repository's root:

```powershell
cd tools/controller-scan-kit
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --require-hashes --only-binary=:all: -r requirements.lock
.\.venv\Scripts\python.exe src/controller_scan.py --dry-run
.\.venv\Scripts\python.exe src/controller_scan.py --guided
```

This installs Python dependencies in the virtual environment; the scanner itself installs
no software or drivers. Source execution makes the code inspectable but still depends on
Python, Windows, and third-party packages. [Build instructions](BUILDING.md).

## Choose the scope

| Command | Behavior |
|---|---|
| `--basic` | Device discovery/standard USB metadata only; no gameplay input or BLE stage |
| `--guided` | Ordinary scan with plain instructions and optional input/BLE stages |
| `--advanced` | Detailed device/input/BLE workflow for maintainers |
| `--app-capture` | Separate advanced experiment; owner uses an existing settings app and optionally imports a local trace |
| `--capabilities` | Print the machine-readable collection policy; no hardware access |
| `--privacy` | Explain all result categories; no hardware access |
| `--dry-run` | Explain the planned workflow; no hardware access or result creation |
| `--self-check` | Synthetic parser/archive/package checks; no hardware access; creates and removes temporary test data |
| `--validate scan.zip` | Validate a received archive offline without extracting it |

`--output PATH` chooses the local results directory. Cancelling or declining review leaves
the local folder without creating a ZIP. CTRL+C stops collection; no upload follows.

**Raw capture is optional and separate.** USBPcap or Android HCI captures can contain
serials, addresses, pairing information or unrelated traffic. They are not anonymized.
The kit imports only the file you choose (or one bounded Bluetooth log from your chosen
Android bug report); it does not install capture drivers or record Internet traffic.
External tools can require elevation or phone settings changes. In this mode the owner,
not the scanner, changes and restores one ordinary setting using an existing app.

## Inspect and verify

- [Exactly what is collected](DATA_COLLECTED.md) and [privacy lifecycle](PRIVACY.md)
- [Security policy](SECURITY.md), [threat model and limits](THREAT_MODEL.md)
- [Source](src/controller_scan_kit), [tests](tests), [build](scripts/build.ps1)
- [Public CI/release workflow](../../.github/workflows/controller-scan-kit.yml)
- [Verification](VERIFY_RELEASE.md), [changes](CHANGELOG.md), [contributing](CONTRIBUTING.md)

Official scanner releases are built from a visible commit on a GitHub-hosted Windows runner.
The workflow gates publication on tests, CodeQL, a packaged self-check, Microsoft Defender,
checksums and a GitHub build-provenance attestation. See the actual workflow/run results;
documentation is not proof that a check ran. No bit-for-bit reproducibility is claimed.

The AST policy guards apply only to this kit's `src/`. They detect regressions, not every
possible malicious program. Python/PyInstaller and BLE libraries can contain generic
networking/file-writing internals; finding those strings in an EXE is not a source audit.

Independent, unofficial interoperability project. See [LICENSE](LICENSE),
[LEGAL_NOTICES](LEGAL_NOTICES) and [third-party licenses](licenses).
