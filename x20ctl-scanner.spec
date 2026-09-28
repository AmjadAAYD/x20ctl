# -*- mode: python ; coding: utf-8 -*-
a = Analysis(['scanner_entry.py'], pathex=[], binaries=[], datas=[], hiddenimports=[],
             hookspath=[], hooksconfig={}, runtime_hooks=[],
             excludes=['tkinter', 'PySide6', 'PyQt5', 'PyQt6', 'pywebview', 'webview',
                       'requests', 'urllib3', 'httpx', 'aiohttp', 'pytest'],
             noarchive=False, optimize=0)
pyz = PYZ(a.pure)
exe = EXE(pyz, a.scripts, a.binaries, a.datas, [], name='x20ctl-scanner',
          debug=False, bootloader_ignore_signals=False, strip=False, upx=False,
          console=True, disable_windowed_traceback=False)
