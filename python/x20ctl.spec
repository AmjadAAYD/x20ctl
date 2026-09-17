# -*- mode: python ; coding: utf-8 -*-
# PyInstaller Spec for x20ctl Windows Standalone Executable (.exe)

import os
import sys

block_cipher = None
project_root = os.path.abspath(SPECPATH)

datas = [
    (os.path.join(project_root, 'keylinker.py'), '.'),
    (os.path.join(project_root, 'assets'), 'assets'),
]

# Optional documentation and findings if present
for doc_file in ['README.md', 'LICENSE', 'IF-YOUR-CONTROLLER-ISNT-WORKING.md']:
    doc_path = os.path.join(project_root, doc_file)
    if os.path.exists(doc_path):
        datas.append((doc_path, '.'))

a = Analysis(
    ['app.py'],
    pathex=[project_root],
    binaries=[],
    datas=datas,
    hiddenimports=[
        'keylinker',
        'bleak',
        'pygame',
        'tkinter',
        'tkinter.ttk',
        'tkinter.messagebox',
        'tkinter.filedialog',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['IPython', 'notebook', 'scipy', 'numpy', 'pandas', 'matplotlib'],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='x20ctl',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,  # Windowed GUI application (no black command prompt)
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=os.path.join(project_root, 'assets', 'x20ctl.ico') if os.path.exists(os.path.join(project_root, 'assets', 'x20ctl.ico')) else None,
)
