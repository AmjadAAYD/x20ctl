"""Regressions in how the app starts. No hardware, no Qt window.

These exist because a startup failure under pythonw.exe is silent: the windowed
launcher has no console, so an exception during import takes the app down with
no message at all. Anything that runs at import time needs a test.
"""

from __future__ import annotations

import importlib
import io
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))


def _fresh_import(name: str):
    for module in [m for m in sys.modules if m.startswith("x20ctl")]:
        del sys.modules[module]
    return importlib.import_module(name)


def test_ui_imports_when_stdout_is_none():
    """pythonw.exe gives sys.stdout as None rather than a stream.

    ui.py asked stdout whether it was a terminal at import time, which raised
    AttributeError and killed the GUI before it drew anything.
    """
    original = sys.stdout
    sys.stdout = None
    try:
        ui = _fresh_import("x20ctl.ui")
        assert ui._ENABLED is False, "ANSI must be off when there is no stdout"
        assert ui.RESET == "", "escape codes must be empty when disabled"
    finally:
        sys.stdout = original


def test_cli_imports_when_stdout_is_none():
    original = sys.stdout
    sys.stdout = None
    try:
        _fresh_import("x20ctl.cli")
    finally:
        sys.stdout = original


def test_gui_does_not_import_the_cli():
    """The GUI must not drag terminal-oriented code in.

    It previously imported load_address from cli, which pulled in ui, which is
    what broke the windowed launcher.
    """
    try:
        import PySide6  # noqa: F401
    except ImportError:
        return
    _fresh_import("x20ctl.gui.window")
    assert "x20ctl.cli" not in sys.modules, "the GUI should not import the CLI"
    assert "x20ctl.ui" not in sys.modules, "the GUI should not import terminal UI"


def test_save_files_live_with_the_user_not_with_the_executable():
    """Replacing x20ctl.exe must not take somebody's save files with it.

    A one-file PyInstaller build unpacks to a temporary directory that is
    deleted on exit, so anything derived from sys.executable, __file__ or the
    working directory would look like it worked and then vanish. The store is
    anchored to APPDATA instead, which survives a new download and an upgrade.
    """
    profiles = _fresh_import("x20ctl.profiles")
    config = _fresh_import("x20ctl.config")

    appdata = os.environ.get("APPDATA") or os.path.expanduser("~")
    for path in (profiles.DEFAULT_DIR, config.CONFIG_PATH):
        assert os.path.commonpath([appdata, path]) == os.path.normpath(appdata), \
            f"{path} is not under the user's own directory"

    # The three roots that move when the executable is replaced.
    for moving in (os.path.dirname(os.path.abspath(sys.executable)),
                   os.path.dirname(os.path.abspath(profiles.__file__)),
                   os.getcwd()):
        assert not profiles.DEFAULT_DIR.startswith(moving), \
            f"save files must not sit under {moving}"

    # sys._MEIPASS only exists in a frozen build, and is the temp unpack dir.
    assert not hasattr(sys, "_MEIPASS") or not \
        profiles.DEFAULT_DIR.startswith(sys._MEIPASS)


def test_config_survives_an_unwritable_location():
    """Remembering the address is a convenience and must never be fatal."""
    config = _fresh_import("x20ctl.config")
    original = config.CONFIG_PATH
    config.CONFIG_PATH = os.path.join("Z:\\", "definitely", "not", "there.json")
    try:
        config.save_address("11:22:33:44:55:66")     # must not raise
        assert config.load_address() is None
    finally:
        config.CONFIG_PATH = original


def test_config_ignores_a_corrupt_file(tmp_path=None):
    import tempfile

    config = _fresh_import("x20ctl.config")
    original = config.CONFIG_PATH
    with tempfile.TemporaryDirectory() as tmp:
        path = os.path.join(tmp, "config.json")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write("{ not json")
        config.CONFIG_PATH = path
        try:
            assert config.load_address() is None
        finally:
            config.CONFIG_PATH = original


def test_config_round_trips():
    import tempfile

    config = _fresh_import("x20ctl.config")
    original = config.CONFIG_PATH
    with tempfile.TemporaryDirectory() as tmp:
        config.CONFIG_PATH = os.path.join(tmp, "config.json")
        try:
            config.save_address("98:B6:ED:E3:15:C4")
            assert config.load_address() == "98:B6:ED:E3:15:C4"
        finally:
            config.CONFIG_PATH = original


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  PASS  {name}")
            except AssertionError as exc:
                failures += 1
                print(f"  FAIL  {name}: {exc}")
    print(f"\n{'all passed' if not failures else f'{failures} failed'}")
    sys.exit(1 if failures else 0)
