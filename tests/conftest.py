"""Session-wide test setup.

On the Windows CI runner, Python crashes while it shuts down after the whole suite has passed: an access violation
in finalisation with PySide6 (Qt) and winrt loaded, which turns a green run into exit code 1 (seen from 10 Oct 2026;
it does not happen on a desktop). The GUI tests leave the Tester's background scan thread inside a native HID
enumeration, and tearing the interpreter down under it faults. Nothing is left to run by then, so once pytest has its
result and has written its report, the process leaves with that exact result and skips the finalisation that crashes
(the fault handler is switched off first, or it would print that thread's stack on the way out).
"""
import faulthandler
import os
import sys

_status = None


def pytest_sessionfinish(session, exitstatus):
    global _status
    _status = int(exitstatus)


def pytest_unconfigure(config):
    if _status is None or "PySide6" not in sys.modules:
        return
    sys.stdout.flush(); sys.stderr.flush()
    faulthandler.disable()
    os._exit(_status)
