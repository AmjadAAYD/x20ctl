# PyInstaller build description.
#
#     python tools/build_exe.py
#
# Kept as a spec file rather than a pile of command line flags so the excludes
# and bundled data are reviewable, and so a build is reproducible.

import os
import sys

block_cipher = None

ROOT = os.path.abspath(os.getcwd())
ICON = os.path.join(ROOT, "assets", "x20ctl.ico")

sys.path.insert(0, ROOT)
from x20ctl import __version__ as VERSION

# Windows reads a file's version out of a resource compiled into the executable,
# so it is generated here from the package's own number. Typing it twice is how
# the properties dialog ends up disagreeing with the app.
NUMERIC = tuple((list(int(part) for part in VERSION.split(".")) + [0, 0, 0, 0])[:4])
VERSION_FILE = os.path.join(ROOT, "build", "version_info.txt")
os.makedirs(os.path.dirname(VERSION_FILE), exist_ok=True)
with open(VERSION_FILE, "w", encoding="utf-8") as handle:
    handle.write(f"""VSVersionInfo(
  ffi=FixedFileInfo(
    filevers={NUMERIC}, prodvers={NUMERIC},
    mask=0x3f, flags=0x0, OS=0x40004, fileType=0x1, subtype=0x0, date=(0, 0)),
  kids=[
    StringFileInfo([StringTable('040904B0', [
      StringStruct('CompanyName', 'x20ctl'),
      StringStruct('FileDescription',
                   'Open configuration for the EasySMX X20'),
      StringStruct('FileVersion', '{VERSION}'),
      StringStruct('InternalName', 'x20ctl'),
      StringStruct('LegalCopyright', 'MIT licence. Not affiliated with any manufacturer.'),
      StringStruct('OriginalFilename', 'x20ctl.exe'),
      StringStruct('ProductName', 'x20ctl'),
      StringStruct('ProductVersion', '{VERSION}'),
    ])]),
    VarFileInfo([VarStruct('Translation', [1033, 1200])]),
  ],
)
""")

a = Analysis(
    ["app.py"],
    pathex=[ROOT],
    binaries=[],
    # The icon travels with the build so the running app can set its window
    # icon, not only the executable's shell icon.
    datas=[(ICON, "assets")],
    hiddenimports=[
        # bleak reaches WinRT through modules PyInstaller cannot see statically
        "winrt.windows.devices.bluetooth",
        "winrt.windows.devices.bluetooth.advertisement",
        "winrt.windows.devices.bluetooth.genericattributeprofile",
        "winrt.windows.devices.enumeration",
        "winrt.windows.foundation",
        "winrt.windows.foundation.collections",
        "winrt.windows.storage.streams",
    ],
    hookspath=[],
    runtime_hooks=[],
    # Qt ships a great deal this app never touches. Dropping it roughly halves
    # the build without changing behaviour.
    excludes=[
        "PySide6.QtWebEngineCore", "PySide6.QtWebEngineWidgets",
        "PySide6.QtWebEngineQuick", "PySide6.QtQuick", "PySide6.QtQuick3D",
        "PySide6.QtQml", "PySide6.Qt3DCore", "PySide6.Qt3DRender",
        "PySide6.QtMultimedia", "PySide6.QtMultimediaWidgets",
        "PySide6.QtCharts", "PySide6.QtDataVisualization",
        "PySide6.QtOpenGL", "PySide6.QtOpenGLWidgets", "PySide6.QtPdf",
        "PySide6.QtPdfWidgets", "PySide6.QtSql", "PySide6.QtTest",
        "PySide6.QtBluetooth", "PySide6.QtNetworkAuth", "PySide6.QtPositioning",
        "PySide6.QtSerialPort", "PySide6.QtSensors", "PySide6.QtWebSockets",
        "PySide6.QtDesigner", "PySide6.QtHelp", "PySide6.QtUiTools",
        "tkinter", "unittest", "pydoc", "doctest", "pdb",
        "PIL",              # only the icon generator needs it
        "setuptools", "pip",
    ],
    noarchive=False,
    cipher=block_cipher,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name="x20ctl",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    runtime_tmpdir=None,
    # No console: this is a windowed app, and a stray terminal behind it looks
    # unfinished.
    console=False,
    disable_windowed_traceback=False,
    icon=ICON,
    version=VERSION_FILE,
)
