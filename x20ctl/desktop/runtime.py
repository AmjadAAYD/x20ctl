"""Use the serviced Microsoft WebView2 Runtime shared by Windows apps."""
import winreg

WEBVIEW2_DOWNLOAD = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"


def system_runtime_installed():
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
    if not system_runtime_installed():
        raise RuntimeError("Microsoft Edge WebView2 Runtime is missing")
    return "system"
