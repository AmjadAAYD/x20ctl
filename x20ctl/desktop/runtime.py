"""Prefer the serviced system runtime; fall back to our app-private runtime."""
import sys
import subprocess
import winreg
from pathlib import Path


def system_runtime_installed():
    client = r"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    for hive, prefix in ((winreg.HKEY_LOCAL_MACHINE, "SOFTWARE\\WOW6432Node\\"),
                         (winreg.HKEY_CURRENT_USER, "SOFTWARE\\")):
        try:
            with winreg.OpenKey(hive, prefix + client) as key:
                version, _ = winreg.QueryValueEx(key, "pv")
                if version and version != "0.0.0.0":
                    return True
        except OSError:
            pass
    return False


def configure(webview, root: Path, force=False):
    bundled = root / "webview2-runtime"
    if not force and system_runtime_installed():
        return "system"
    if not (bundled / "msedgewebview2.exe").exists():
        raise RuntimeError("WebView2 Runtime is missing. Use the full x20ctl.exe download or install Microsoft WebView2 Runtime.")
    # Microsoft requires these read/execute ACLs for unpackaged Win10 apps.
    # Only the app-owned extracted runtime directory is affected.
    if sys.getwindowsversion().build < 22000:
        subprocess.run(["icacls.exe", str(bundled), "/grant", "*S-1-15-2-2:(OI)(CI)(RX)",
                        "*S-1-15-2-1:(OI)(CI)(RX)"], check=True,
                       stdout=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    webview.settings["WEBVIEW2_RUNTIME_PATH"] = str(bundled)
    return "bundled"
