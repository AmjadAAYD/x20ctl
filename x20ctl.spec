# -*- mode: python ; coding: utf-8 -*-
import os

runtime = os.environ.get('X20CTL_BUNDLE_RUNTIME')
extra_data = [(runtime, 'webview2-runtime')] if runtime else []
if os.path.exists('artifacts/THIRD_PARTY_LICENSES.txt'):
    extra_data += [('artifacts/THIRD_PARTY_LICENSES.txt', '.')]


a = Analysis(
    ['app.py'],
    pathex=[],
    binaries=[],
    datas=[('assets/x20ctl.ico', '.'), ('assets/x20ctl.png', '.'), ('dist-ui', 'dist-ui'), ('LICENSE', '.'), ('THIRD_PARTY.md', '.')] + extra_data,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter', 'PySide6', 'PyQt5', 'PyQt6', 'qtpy', 'matplotlib', 'numpy', 'pandas', 'scipy', 'IPython', 'pytest'],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='x20ctl',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=['assets/x20ctl.ico'],
    version='assets/version_info.txt',
)
