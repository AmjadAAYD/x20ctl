import ctypes
import importlib
import struct

import pytest


@pytest.fixture(params=["x20ctl.scanning", "tools.controller_scan_kit"])
def collector(request):
    return request.param


def samples(**changes):
    baseline = dict(slot=0, packet=1, buttons=0, lt=0, rt=0,
                    lx=0, ly=0, rx=0, ry=0)
    return [{"source": "xinput_state", "values": baseline},
            {"source": "xinput_state", "values": {**baseline, **changes}}]


@pytest.mark.parametrize("action", ["A", "LT", "RT_full"])
def test_stick_drift_does_not_confirm_requested_control(collector, action):
    summarize = importlib.import_module(collector + ".input_tests").summarize
    result = summarize(action, samples(lx=256))
    assert result["status"] == "unrelated_change_observed"
    assert result["requested_control_observed"] is False
    assert result["changed_fields"] == ["lx"]


def test_wrong_button_does_not_confirm_a(collector):
    summarize = importlib.import_module(collector + ".input_tests").summarize
    result = summarize("A", samples(buttons=0x2000))
    assert result["status"] == "unrelated_change_observed"
    assert result["changed_button_bits"] == ["2000"]


@pytest.mark.parametrize("action,changes", [("A", {"buttons": 0x1000}),
    ("LT_smooth_sweep_up", {"lt": 100}), ("right_stick_circle", {"rx": 20000})])
def test_requested_control_changes_are_retained(collector, action, changes):
    summarize = importlib.import_module(collector + ".input_tests").summarize
    result = summarize(action, samples(**changes))
    assert result["status"] == "change_observed"
    assert result["requested_control_observed"] is True


def test_xinput_slot_switch_cannot_be_used_as_control_evidence(collector):
    summarize = importlib.import_module(collector + ".input_tests").summarize
    result = summarize("A", samples(slot=1, buttons=0x1000))
    assert result["status"] == "inconclusive_source_changed"


def test_mixed_apis_cannot_be_used_as_control_evidence(collector):
    summarize = importlib.import_module(collector + ".input_tests").summarize
    result = summarize("A", [samples()[0], {"source": "hid_input", "report_hex": "0001"}])
    assert result["status"] == "inconclusive_source_changed"


def test_xinput_reader_decodes_native_bytes_without_hardware(collector):
    windows = importlib.import_module(collector + ".windows")
    assert ctypes.sizeof(windows.Gamepad) == 12
    assert ctypes.sizeof(windows.State) == 16
    assert windows.State.pad.offset == 4
    reader = object.__new__(windows.XInput)

    def fake_get_state(slot, pointer):
        raw = struct.pack("<IHBBhhhh", 123, 0x1000, 71, 212,
                          -32768, 32767, -1234, 2345)
        ctypes.memmove(pointer, raw, len(raw))
        return 0

    reader.fn = fake_get_state
    assert reader.state(0)["values"] == dict(slot=0, packet=123, buttons=0x1000,
        lt=71, rt=212, lx=-32768, ly=32767, rx=-1234, ry=2345)


@pytest.mark.parametrize("status", ["no_samples", "failed", "cancelled",
    "no_change_observed", "unrelated_change_observed", "inconclusive_source_changed"])
def test_inconclusive_capture_does_not_receive_observed_coverage(tmp_path, status):
    from x20ctl.desktop.research_scan import ResearchScanner
    scanner = ResearchScanner(tmp_path)
    scanner._step("buttons", lambda: {"action": "A", "status": status})
    assert scanner.status()["coverage"]["buttons"]["status"] == "unavailable"


def test_later_success_does_not_hide_an_earlier_failed_action(tmp_path):
    from x20ctl.desktop.research_scan import ResearchScanner
    scanner = ResearchScanner(tmp_path)
    scanner._step("buttons", lambda: {"action": "A", "status": "no_change_observed"})
    scanner._step("buttons", lambda: {"action": "B", "status": "change_observed"})
    coverage = scanner.status()["coverage"]["buttons"]
    assert coverage["status"] == "observed_with_limits"
    assert "no_change_observed" in coverage["reason"]


def test_quiet_neutral_capture_is_a_valid_baseline(tmp_path):
    from x20ctl.desktop.research_scan import ResearchScanner
    scanner = ResearchScanner(tmp_path)
    scanner._step("neutral", lambda: {"action": "neutral", "status": "no_change_observed"})
    assert scanner.status()["coverage"]["neutral"]["status"] == "observed"
