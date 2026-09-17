# x20ctl - Native Python Application & Windows Standalone Executable

This directory contains the 100% native Python desktop application and PyInstaller build toolchain for the EasySMX X20 Pro / KeyLinker gamepad controller.

---

## Directory Structure

```
python/
├── app.py              # Main desktop application (GUI & CLI modes)
├── keylinker.py        # KeyLinker BLE protocol framing, CRC-8, & packet dispatcher
├── build_exe.py        # PyInstaller build runner for x20ctl.exe
├── x20ctl.spec         # PyInstaller standalone specification
├── requirements.txt    # Python package dependencies (bleak, pygame, pyinstaller)
├── setup.py            # Standard setuptools installation script
├── build.bat           # 1-Click Windows batch script to compile dist\x20ctl.exe
├── run.bat             # 1-Click Windows batch script to run app directly
└── README.md           # This documentation
```

---

## Quick Start

### 1. Install Dependencies
```bash
cd python
pip install -r requirements.txt
```

### 2. Run Python Application
```bash
# Launch GUI mode:
python app.py

# Or launch CLI mode:
python app.py --cli

# Or list connected gamepads:
python app.py --list
```

---

## Building Standalone Windows Executable (`x20ctl.exe`)

### Option A: One-Click Windows Batch
Double-click `build.bat` in Windows Explorer, or run:
```cmd
build.bat
```

### Option B: PyInstaller Command Line
```bash
python build_exe.py
# Or directly:
pyinstaller x20ctl.spec
```

The resulting standalone executable will be generated at:
```
dist/x20ctl.exe
```
This `.exe` requires no Python installation and runs standalone on Windows 10/11 (64-bit).

---

## KeyLinker Protocol Engine (`keylinker.py`)

- Header: `0xAA 0x55`
- Length byte, command opcode, payload data, and CRC-8 (polynomial `0x07`)
- Supports:
  - Button remapping (`OP_KEY_MAPPING = 0x01`)
  - Hermite Bézier curve calibration for sticks and Hall-effect triggers (`OP_CURVE_CONFIG = 0x02`)
  - 4-Paddle macro playback (`OP_MACRO_CONFIG = 0x03`)
  - Dual-motor haptic rumble test (`OP_RUMBLE_TEST = 0x04`)
  - Sleep timer & power management (`OP_POWER_CONFIG = 0x05`)
  - Profile switching (`OP_PROFILE_SELECT = 0x06`)
