"""Select the native, serviced browser engine for each supported platform."""
import sys

WEBVIEW2_DOWNLOAD = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"


def system_runtime_installed():
    if sys.platform != "win32":
        return False
    import winreg
    client = r"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
        for prefix in ("SOFTWARE\\", "SOFTWARE\\WOW6432Node\\"):
            try:
                with winreg.OpenKey(hive, prefix + client) as key:
                    version, _ = winreg.QueryValueEx(key, "pv")
                    if version and version != "0.0.0.0":
                        return True
            except OSError:
                pass
    return False


def ensure_runtime(prompt, open_download) -> bool:
    """Offer an official download and Retry; never install automatically."""
    while not system_runtime_installed():
        choice = prompt()
        if choice == "download":
            open_download(WEBVIEW2_DOWNLOAD)
        elif choice == "retry":
            continue
        else:
            return False
    return True


def configure(webview):
    if sys.platform.startswith("linux"):
        try:
            import gi
            gi.require_version("Gtk", "3.0")
            gi.require_version("WebKit2", "4.1")
            from gi.repository import Gtk, WebKit2  # noqa: F401
        except (ImportError, ValueError) as exc:
            raise RuntimeError("GTK 3 / WebKitGTK 4.1 is missing. See LINUX-README.txt.") from exc
        return "gtk"
    if sys.platform != "win32":
        raise RuntimeError("This desktop build supports Windows and Linux")
    if not system_runtime_installed():
        raise RuntimeError("Microsoft Edge WebView2 Runtime is missing")
    return "system"
