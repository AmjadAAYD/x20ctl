"""Native desktop plumbing; no device access or global desktop changes."""
from __future__ import annotations

import ctypes
import os
import sys
from pathlib import Path
from urllib.parse import urlsplit


def data_directory() -> Path:
    if sys.platform.startswith("linux"):
        return Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "x20ctl"
    return Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "x20ctl"


def notify(message: str, *, error=False):
    if sys.platform == "win32":
        ctypes.windll.user32.MessageBoxW(None, message, "x20ctl", 0x10 if error else 0x40)
    else:
        # Also works when GTK itself is unavailable (stderr is captured by launch.sh).
        print(message, file=sys.stderr)


class InstanceLock:
    """Per-user lock released on normal exit or process death."""
    def __init__(self, *, isolated=False):
        self.isolated = isolated
        self.handle = None
        self.kernel = None

    def acquire(self) -> bool:
        if self.isolated:
            return True
        if sys.platform == "win32":
            self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
            self.kernel.CreateMutexW.restype = ctypes.c_void_p
            self.kernel.CloseHandle.argtypes = [ctypes.c_void_p]
            self.handle = self.kernel.CreateMutexW(None, False, "Local\\x20ctl.Desktop")
            if not self.handle:
                raise ctypes.WinError(ctypes.get_last_error())
            if ctypes.get_last_error() == 183:
                self.close()
                return False
        else:
            import fcntl
            directory = data_directory()
            directory.mkdir(parents=True, exist_ok=True)
            # Do not unlink: another process might still hold the original inode.
            fd = os.open(directory / "desktop.lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
            self.handle = os.fdopen(fd, "w")
            try:
                fcntl.flock(self.handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                self.close()
                return False
        return True

    def close(self):
        if self.handle is not None:
            if self.kernel:
                self.kernel.CloseHandle(self.handle)
            else:
                self.handle.close()
            self.handle = None


def bundled_navigation_allowed(expected: str, target: str) -> bool:
    """Allow only the bundled entry document, including its query/hash changes."""
    origin = urlsplit(expected)
    destination = urlsplit(target)
    return (origin.scheme, origin.netloc, origin.path) == (
        destination.scheme, destination.netloc, destination.path
    )


def protect_navigation(window):
    expected = window.real_url
    if sys.platform == "win32":
        def guard(_sender, event):
            if not bundled_navigation_allowed(expected, str(event.Uri)):
                event.Cancel = True
        window.native.browser.webview.NavigationStarting += guard
    else:
        from webview.platforms.gtk import BrowserView, webkit
        browser = BrowserView.instances[window.uid]
        # Replace the default handler so _blank cannot load a foreign document
        # before our allowlist runs. Response/download handling stays native.
        browser.webview.disconnect_by_func(browser.on_navigation)

        def guard(view, decision, decision_type):
            if isinstance(decision, webkit.NavigationPolicyDecision):
                uri = decision.get_navigation_action().get_request().get_uri()
                if not bundled_navigation_allowed(expected, uri):
                    decision.ignore()
                    return True
            return browser.on_navigation(view, decision, decision_type)

        browser.webview.connect("decide-policy", guard)
