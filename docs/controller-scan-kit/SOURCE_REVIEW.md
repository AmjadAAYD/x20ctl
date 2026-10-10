# Inspecting the kit

The distribution includes the exact collector source under `source/`, its license, runtime notices and source hashes in `BUILD_INFO.json`. A source review and matching hashes establish which files were supplied. They do not independently prove the bundled EXE was built from them; rebuilding is the stronger route for someone who needs that assurance. This build is unsigned.

The source entry is `source/controller_scan.py`; the implementation is `source/controller_scan_kit/`. There are no download/install/upload functions, no vendor-report probing, no firmware/configuration setters and no injected manufacturer binaries. USB queries are allowlisted standard hub operations. HID gameplay handles have read access only. BLE reads match a service-and-characteristic UUID pair; serial-number and vendor fields are excluded. Device-selection identifiers stay transient in the worker protocol.

Review `backend.py` for process limits, `windows.py` for native handles/input restrictions, `usb.py` for standard descriptor requests, `ble.py` for the exact read allowlist, `evidence.py` for exports/hash validation, and `cli.py` for prompts and consent. The console's description of read-only research allows standard metadata requests and BLE connection/service discovery; it is not a guarantee that a connection never affects radio power or state.

## Optional Python route

The packaged EXE needs no Python installation. If a contributor deliberately prefers source, use Windows 10/11 x64 and an already prepared Python 3.12+ environment with the versions in `source/requirements.txt`. Install dependencies explicitly only if desired; no script installs them for you. Run from the extracted kit root:

```powershell
python source/controller_scan.py --self-check
python source/controller_scan.py --output results
python source/controller_scan.py --advanced --output results
python source/controller_scan.py --validate results/scan-EXAMPLE.zip
```

`--self-check` uses only synthetic fixtures, imports and structure-layout checks. It never opens a controller, enumerates devices, starts Bluetooth discovery or performs XInput calls.

The default run is the short beginner discovery. `--advanced` enables the earlier input/BLE and multiple-session workflow; reserve it for a separately explained follow-up.

## Maintainer build route in the x20ctl repository

Use the existing pinned build environment. The build does not change the app, publish a release or install dependencies:

```powershell
.\.venv\Scripts\python.exe -m pytest tests/test_controller_scan_kit.py -q
.\.venv\Scripts\python.exe tools/controller_scan.py --self-check
.\.venv\Scripts\python.exe tools/build_controller_scan_kit.py
```

It creates a fresh distribution under ignored `artifacts/controller-scan-kit/`, runs the packaged no-hardware self-check and packages the source/guides/licenses. Source hashes and dependency versions are recorded independently of the repository's dirty base commit. Check `VERIFICATION.md` in the built kit for what was actually tested. Live device testing remains a separate acceptance step.

The kit contains Bleak/PyWinRT for BLE and a PyInstaller bootloader/Python runtime. Their source/license references are in `licenses/` and `THIRD_PARTY_NOTICES.txt`. No official EasySMX application, firmware, capture driver, APK or controller owner's evidence is bundled.
