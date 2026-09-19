"""Windows application hosting only bundled UI, with native tray and cleanup."""

from __future__ import annotations

import argparse
import ctypes
import logging
import os
import sys
import tempfile
import threading
from pathlib import Path
from urllib.parse import urlsplit


def main():
    parser = argparse.ArgumentParser(description="x20ctl desktop application")
    parser.add_argument("--bundled-runtime", action="store_true", help="Test the included runtime without using the system installation")
    parser.add_argument(
        "--smoke-test",
        metavar="DIRECTORY",
        help="Run read-only UI acceptance and capture real window screenshots",
    )
    args = parser.parse_args()
    log_dir = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "x20ctl"
    log_dir.mkdir(parents=True, exist_ok=True)
    logging.basicConfig(filename=log_dir / "desktop.log", level=logging.INFO)
    try:
        return _run(args)
    except Exception as exc:
        logging.exception("Desktop startup failed")
        ctypes.windll.user32.MessageBoxW(
            None,
            f"x20ctl could not start.\n\n{exc}\n\nInstall Microsoft Edge WebView2 Runtime if it is missing.\nDiagnostic log: {log_dir / 'desktop.log'}",
            "x20ctl startup error",
            0x10,
        )
        return 1


def _run(args):
    import webview
    from .service import DesktopApi, DeviceService

    root = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[2]))
    index = root / "dist-ui" / "index.html"
    if not index.exists():
        raise RuntimeError("Desktop assets are missing. Run npm run build first.")
    from .runtime import configure
    renderer = configure(webview, root, args.bundled_runtime)
    logging.info("Desktop renderer: %s", renderer)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateMutexW.restype = ctypes.c_void_p
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    mutex = kernel.CreateMutexW(
        None, False, "Local\\x20ctl.Desktop" + (".Smoke" if args.smoke_test else "")
    )
    if not mutex:
        raise ctypes.WinError(ctypes.get_last_error())
    if ctypes.get_last_error() == 183:
        kernel.CloseHandle(mutex)
        ctypes.windll.user32.MessageBoxW(
            None,
            "x20ctl is already running. Open it from the system tray.",
            "x20ctl",
            0x40,
        )
        return 0
    temporary = (
        tempfile.TemporaryDirectory(prefix="x20ctl-smoke-") if args.smoke_test else None
    )
    api = DesktopApi(DeviceService(directory=temporary.name) if temporary else None)
    window = webview.create_window(
        "x20ctl",
        str(index),
        js_api=api,
        width=1400,
        height=940,
        min_size=(1060, 760),
        background_color="#101215",
    )
    api._window = window
    webview.settings["ALLOW_DOWNLOADS"] = False
    webview.settings["OPEN_EXTERNAL_LINKS_IN_BROWSER"] = False
    webview.settings["ALLOW_FILE_URLS"] = False
    tray = None
    quitting = threading.Event()
    result = {"code": 0}

    def protect_navigation():
        browser = window.native.browser.webview
        expected = urlsplit(window.real_url)

        def guard(_sender, event):
            target = urlsplit(str(event.Uri))
            if (target.scheme, target.netloc, target.path) != (
                expected.scheme,
                expected.netloc,
                expected.path,
            ):
                event.Cancel = True

        browser.NavigationStarting += guard

    window.events.before_show += protect_navigation

    def setup():
        nonlocal tray
        try:
            import pystray
            from PIL import Image

            def show(_icon=None, _item=None):
                window.show()
                window.restore()

            def quit_app(_icon=None, _item=None):
                quitting.set()
                window.destroy()

            icon_path = root / "assets" / "x20ctl.ico"
            if not icon_path.exists():
                icon_path = root / "x20ctl.ico"
            tray = pystray.Icon(
                "x20ctl",
                Image.open(icon_path),
                "x20ctl · controller studio",
                menu=pystray.Menu(
                    pystray.MenuItem("Open x20ctl", show, default=True),
                    pystray.MenuItem("Quit", quit_app),
                ),
            )
            tray.run_detached()

            def closing():
                if not quitting.is_set() and tray.visible:
                    window.hide()
                    return False
                return True

            window.events.closing += closing
        except Exception:
            logging.exception("Tray unavailable; window close will exit")
        if args.smoke_test:
            from .smoke import exercise
            exercise(window, args.smoke_test, result, {"tray": tray, "show": show, "quit": quit_app})

    try:
        webview.start(setup, gui="edgechromium", debug=False, private_mode=True)
    finally:
        if tray:
            tray.stop()
        api._close()
        if temporary:
            temporary.cleanup()
        kernel.CloseHandle(mutex)
    return result["code"]


if __name__ == "__main__":
    sys.exit(main())
