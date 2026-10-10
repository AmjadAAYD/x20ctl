# Windows x64 console executable; asInvoker is PyInstaller's default (no uac_admin).
a = Analysis(['src/controller_scan.py'], pathex=['src'], binaries=[],
             datas=[('CAPABILITIES.json', '.')],
             hiddenimports=['bleak.backends.winrt.client', 'bleak.backends.winrt.scanner'],
             hookspath=[], hooksconfig={}, runtime_hooks=[],
             excludes=['tkinter', 'PySide6', 'PyQt5', 'PyQt6', 'webview',
                       'requests', 'httpx', 'aiohttp', 'pytest'],
             noarchive=False, optimize=0)
pyz = PYZ(a.pure)
exe = EXE(pyz, a.scripts, a.binaries, a.datas, [], name='ControllerScanKit',
          debug=False, bootloader_ignore_signals=False, strip=False, upx=False,
          console=True, disable_windowed_traceback=False, uac_admin=False)
