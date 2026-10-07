"""Launch placement fixtures: never read the host screen or open a window."""

import sys
from types import SimpleNamespace

import pytest

from x20ctl.desktop import launcher, window_geometry


def bounds_for(area):
    return window_geometry.launch_bounds(*area)


@pytest.mark.parametrize("area,expected", [
    ((0, 0, 2560, 1680), (488, 266, 1583, 1147)),
    ((0, 0, 1920, 1040), (168, 16, 1583, 1008)),
    ((56, 30, 1864, 1010), (196, 46, 1583, 978)),
    ((-1920, -200, 1920, 1160), (-1752, -194, 1583, 1147)),
    ((0, 0, 1583, 1147), (0, 0, 1583, 1147)),
    ((0, 0, 900, 1440), (16, 146, 868, 1147)),
])
def test_preferred_size_is_centered_and_clamped_to_work_area(area, expected):
    bounds = bounds_for(area)
    assert (bounds.x, bounds.y, bounds.width, bounds.height) == expected
    x, y, width, height = area
    assert x <= bounds.x and y <= bounds.y
    assert bounds.x + bounds.width <= x + width
    assert bounds.y + bounds.height <= y + height
    assert abs((bounds.x - x) * 2 + bounds.width - width) <= 1
    assert abs((bounds.y - y) * 2 + bounds.height - height) <= 1


def test_small_display_minimum_size_cannot_force_window_under_taskbar():
    bounds = bounds_for((0, 0, 1024, 700))
    assert (bounds.width, bounds.height) == (992, 668)
    assert bounds.min_size == (992, 668)


@pytest.mark.parametrize("width,height", [(0, 900), (1400, 0), (-1, 900)])
def test_invalid_work_area_is_rejected(width, height):
    with pytest.raises(ValueError, match="positive"):
        bounds_for((0, 0, width, height))


@pytest.mark.parametrize("scale", [1, 1.25, 1.5, 2])
def test_native_bounds_are_applied_after_scaling_before_first_frame(monkeypatch, scale):
    prepare = getattr(launcher, "prepare_launch_bounds", None)
    assert callable(prepare), "Native startup must apply bounds before the first frame"

    class Event:
        def __init__(self):
            self.handlers = []

        def __iadd__(self, handler):
            self.handlers.append(handler)
            return self

    form = SimpleNamespace(Load=Event(), Bounds=None, MinimumSize=(1060, 760), StartPosition=None)
    order = []

    def autoscale():
        order.append("autoscale")
        form.MinimumSize = (round(1060 * scale), round(760 * scale))
        form.Bounds = (0, 0, round(1583 * scale), round(1147 * scale))

    def monitor(control):
        assert control is form
        order.append("work_area")
        return SimpleNamespace(WorkingArea=SimpleNamespace(X=0, Y=0, Width=1920, Height=1160))

    form.PerformAutoScale = autoscale
    monkeypatch.setattr(launcher.sys, "platform", "win32")
    monkeypatch.setitem(sys.modules, "System.Drawing", SimpleNamespace(Size=lambda *args: args, Rectangle=lambda *args: args))
    monkeypatch.setitem(sys.modules, "System.Windows.Forms", SimpleNamespace(
        Screen=SimpleNamespace(FromControl=monitor), FormStartPosition=SimpleNamespace(Manual="manual")))
    prepare(SimpleNamespace(native=form))
    assert form.StartPosition == "manual"
    assert order == []  # Installation neither shows a window nor queries the screen.
    assert len(form.Load.handlers) == 1
    form.Load.handlers[0](form, None)
    assert order == ["autoscale", "work_area"]
    assert form.Bounds == (168, 6, 1583, 1147)
    assert form.MinimumSize == (1060, 760)


def test_linux_host_is_not_changed(monkeypatch):
    prepare = getattr(launcher, "prepare_launch_bounds", None)
    assert callable(prepare)
    monkeypatch.setattr(launcher.sys, "platform", "linux")
    prepare(SimpleNamespace())  # No Windows imports or native form access.


@pytest.mark.parametrize("platform,expected_size", [("win32", (1583, 1147)), ("linux", (1400, 940))])
def test_real_launcher_registers_placement_before_show_without_devices(monkeypatch, tmp_path, platform, expected_size):
    index = tmp_path / "dist-ui" / "index.html"
    index.parent.mkdir()
    index.write_text("<html></html>", encoding="utf-8")
    calls, created = [], []

    class Event:
        def __init__(self):
            self.handlers = []

        def __iadd__(self, handler):
            self.handlers.append(handler)
            return self

    window = SimpleNamespace(events=SimpleNamespace(before_show=Event()))

    def create_window(_title, _index, **options):
        created.append(options)
        return window

    def start(_setup, **_options):
        for handler in window.events.before_show.handlers:
            handler()
        calls.append("show")

    monkeypatch.setattr(launcher.sys, "platform", platform)
    monkeypatch.setattr(launcher.sys, "_MEIPASS", str(tmp_path), raising=False)
    monkeypatch.setitem(sys.modules, "webview", SimpleNamespace(create_window=create_window, settings={}, start=start))
    monkeypatch.setitem(sys.modules, "x20ctl.desktop.runtime", SimpleNamespace(
        ensure_runtime=lambda *_args: True, configure=lambda _webview: "system" if platform == "win32" else "gtk"))
    monkeypatch.setitem(sys.modules, "x20ctl.desktop.service", SimpleNamespace(
        DeviceService=lambda: SimpleNamespace(),
        DesktopApi=lambda _service: SimpleNamespace(_close=lambda: calls.append("close"))))
    monkeypatch.setattr(launcher, "InstanceLock", lambda **_options: SimpleNamespace(
        acquire=lambda: True, close=lambda: calls.append("unlock")))
    monkeypatch.setattr(launcher, "protect_navigation", lambda view: calls.append("guard") if view is window else pytest.fail())
    monkeypatch.setattr(launcher, "prepare_launch_bounds", lambda view: calls.append("bounds") if view is window else pytest.fail())
    assert launcher._run(SimpleNamespace(smoke_test=None, linux_review=None)) == 0
    assert (created[0]["width"], created[0]["height"]) == expected_size
    assert calls == ["guard", "bounds", "show", "close", "unlock"]
