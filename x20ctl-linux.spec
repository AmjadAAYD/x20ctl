# Native Linux folder bundle. GTK/WebKit stay serviced by the distribution.
import sys

if not sys.platform.startswith('linux'):
    raise SystemExit('Build the Linux spec on Linux, or use tools/build_linux.py --docker')

a = Analysis(
    ['app.py'], pathex=[], binaries=[],
    datas=[('assets/x20ctl.ico', '.'), ('assets/x20ctl.png', '.'),
           ('dist-ui', 'dist-ui'), ('LICENSE', '.'), ('THIRD_PARTY.md', '.'),
           ('x20ctl/controllers/catalog.json', 'x20ctl/controllers')],
    hiddenimports=['webview.platforms.gtk', 'gi.repository.Gtk', 'gi.repository.Gdk',
                   'gi.repository.WebKit2', 'gi.repository.Soup', 'evdev',
                   'pystray._xorg', 'pystray._appindicator'],
    hookspath=['tools/linux/hooks'],
    hooksconfig={'gi': {'module-versions': {'Gtk': '3.0', 'Gdk': '3.0', 'WebKit2': '4.1', 'Soup': '3.0'}}},
    runtime_hooks=[],
    excludes=['tkinter', 'PySide6', 'PyQt5', 'PyQt6', 'qtpy', 'clr', 'pythonnet',
              'matplotlib', 'numpy', 'pandas', 'scipy', 'IPython', 'pytest'],
    noarchive=False, optimize=0,
)
# Keep Python and extension dependencies; GTK/WebKit and glibc come from Linux.
a.exclude_system_libraries(list_of_exceptions=['libpython*', 'libffi*'])
pyz = PYZ(a.pure)
exe = EXE(pyz, a.scripts, [], exclude_binaries=True, name='x20ctl',
          debug=False, bootloader_ignore_signals=False, strip=False, upx=False,
          console=True)
coll = COLLECT(exe, a.binaries, a.datas, strip=False, upx=False, name='x20ctl-linux-x86_64')
