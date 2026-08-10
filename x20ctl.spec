# PyInstaller build description.
#
#     python tools/build_exe.py
#
# Kept as a spec file rather than a pile of command line flags so the excludes
# and bundled data are reviewable, and so a build is reproducible.

import os

block_cipher = None

ROOT = os.path.abspath(os.getcwd())
ICON = os.path.join(ROOT, "assets", "x20ctl.ico")

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
)
