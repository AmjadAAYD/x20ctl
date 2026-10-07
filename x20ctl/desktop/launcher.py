"""Native desktop host for bundled UI, with navigation protection and cleanup."""

from __future__ import annotations

import argparse
import ctypes
import logging
import os
import sys
import tempfile
import threading
from pathlib import Path
from .platform_support import InstanceLock, data_directory, notify, protect_navigation
from .window_geometry import PREFERRED_SIZE, prepare_launch_bounds


def main():
    if sys.argv[1:] == ["--research-worker"]:
        from .scan_worker import serve
        return serve()
    parser = argparse.ArgumentParser(description="x20ctl desktop application")
    parser.add_argument(
        "--smoke-test",
        metavar="DIRECTORY",
        help="Run read-only UI acceptance and capture real window screenshots",
    )
    parser.add_argument("--verify-scanner", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("--linux-review", metavar="DIRECTORY", help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.verify_scanner:
        from .scanner_integrity import packaged_scanner_path, verify_scanner
        try:
            verify_scanner(packaged_scanner_path())
            return 0
        except ValueError:
            return 2
    log_dir = data_directory()
    log_dir.mkdir(parents=True, exist_ok=True)
    logging.basicConfig(filename=log_dir / "desktop.log", level=logging.INFO)
    try:
        return _run(args)
    except Exception as exc:
        logging.exception("Desktop startup failed")
        runtime_help = "Install Microsoft Edge WebView2 Runtime if it is missing." if sys.platform == "win32" else "See LINUX-README.txt for Linux runtime requirements."
        notify(f"x20ctl could not start.\n\n{exc}\n\n{runtime_help}\nDiagnostic log: {log_dir / 'desktop.log'}", error=True)
        return 1


def _run(args):
    import webbrowser
    from .runtime import ensure_runtime

    def prompt_runtime():
        answer = ctypes.windll.user32.MessageBoxW(
            None,
            "x20ctl needs Microsoft's WebView2 Runtime.\n\n"
            "Yes: open the official Microsoft download page.\n"
            "No: retry after installing it.\n"
            "Cancel: close x20ctl.",
            "WebView2 Runtime required", 0x33,
        )
        return {6: "download", 7: "retry"}.get(answer, "cancel")

    if sys.platform == "win32" and not ensure_runtime(prompt_runtime, webbrowser.open):
        return 1
    if args.smoke_test and sys.platform != "win32":
        raise RuntimeError("--smoke-test uses Windows capture APIs; use the isolated Linux review tool instead")
    if args.linux_review and (sys.platform != "linux" or os.environ.get("X20CTL_ISOLATED_REVIEW") != "1"):
        raise RuntimeError("Linux review is only enabled in the isolated build environment")
    import webview
    from .service import DesktopApi, DeviceService

    root = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[2]))
    index = root / "dist-ui" / "index.html"
    if not index.exists():
        raise RuntimeError("Desktop assets are missing. Run npm run build first.")
    from .runtime import configure
    renderer = configure(webview)
    logging.info("Desktop renderer: %s", renderer)
    # Review windows already use isolated temporary storage. Let independent
    # reviews coexist without closing an open desktop design preview.
    lock = InstanceLock(isolated=bool(args.smoke_test or args.linux_review))
    if not lock.acquire():
        notify("x20ctl is already running. Open its window or system tray icon.")
        return 0
    temporary = (
        tempfile.TemporaryDirectory(prefix="x20ctl-review-") if args.smoke_test or args.linux_review else None
    )
    service = DeviceService(directory=temporary.name) if temporary else DeviceService()
    if args.linux_review:
        def forbidden(*_args, **_kwargs):
            raise RuntimeError("Hardware access is forbidden in the isolated Linux review")
        service._factory = service._scanner = forbidden
        service._reader.poll = lambda: None
        (Path(temporary.name) / "preferences.json").write_text('{"updatesEnabled":false}', encoding="utf-8")
    api = DesktopApi(service)
    initial_size = PREFERRED_SIZE if sys.platform == "win32" else (1400, 940)
    window = webview.create_window(
        "x20ctl",
        str(index),
        js_api=api,
        width=initial_size[0],
        height=initial_size[1],
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

    window.events.before_show += lambda: protect_navigation(window)
    window.events.before_show += lambda: prepare_launch_bounds(window)

    def show(_icon=None, _item=None):
        window.show()
        window.restore()

    def quit_app(_icon=None, _item=None):
        quitting.set()
        window.destroy()

    def setup():
        nonlocal tray
        if sys.platform == "linux":
            # Linux desktops differ in tray support (notably GNOME/Wayland).
            # Keep close-to-exit until a visible native tray can be guaranteed.
            if args.linux_review:
                from .linux_review import exercise
                exercise(window, args.linux_review, result)
            return
        try:
            import pystray
            from PIL import Image

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
        webview.start(setup, gui="edgechromium" if renderer == "system" else renderer, debug=False, private_mode=True)
    finally:
        if tray:
            tray.stop()
        api._close()
        if temporary:
            temporary.cleanup()
        lock.close()
    return result["code"]


if __name__ == "__main__":
    sys.exit(main())
