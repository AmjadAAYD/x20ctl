"""Behavioral boundaries of the native UI adapter; no controller writes in tests."""

import asyncio
from types import SimpleNamespace

import pytest

from x20ctl import protocol as p
from x20ctl.desktop.service import DeviceService, DesktopApi
from x20ctl.desktop.settings import (
    curve_from_ui,
    curve_to_ui,
    macro_from_ui,
    macro_to_ui,
)


def test_adjacent_recorded_changes_are_not_skipped():
    from x20ctl.input import MacroRecorder, RecordedStep, XInputReader

    recorder = MacroRecorder(XInputReader())
    recorder.steps = [
        RecordedStep([p.Key.A], 100),
        RecordedStep([p.Key.A, p.Key.B], 50),
        RecordedStep([p.Key.B], 75),
    ]
    spec = recorder.stop()
    assert spec is not None
    assert spec.steps() == [
        p.MacroStep(p.mask_for([p.Key.A]), 100),
        p.MacroStep(p.mask_for([p.Key.A, p.Key.B]), 50),
        p.MacroStep(p.mask_for([p.Key.B]), 75),
    ]


def test_legacy_import_preserves_partial_application(tmp_path):
    from x20ctl.desktop.settings import import_profile

    legacy = {
        "name": "Old setup",
        "macros": {"M2": {"keys": "A:100/50,B:75", "loop_ms": 200}},
        "vibration": None,
        "clear_undefined": False,
    }
    profile = import_profile(legacy)
    assert profile["categories"] == ["M2"]
    assert profile["macroLoops"]["M2"] == 200
    assert len(profile["macros"]["M2"]) == 2
    service = DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("save_profiles", {"profiles": [profile]}))
    assert asyncio.run(service.dispatch("bootstrap", {}))["profiles"] == [profile]


def test_invalid_profile_never_overwrites_saved_setups(tmp_path):
    service = DeviceService(directory=tmp_path)
    path = tmp_path / "profiles.json"
    path.write_text("broken original", encoding="utf-8")
    boot = asyncio.run(service.dispatch("bootstrap", {}))
    assert boot["warning"] and boot["profiles"] == []
    with pytest.raises(ValueError, match="preserved"):
        asyncio.run(service.dispatch("save_profiles", {"profiles": []}))
    assert path.read_text() == "broken original"


def test_saved_setup_and_macro_identifiers_must_be_unique(tmp_path):
    from x20ctl.desktop.settings import import_profile

    service = DeviceService(directory=tmp_path)
    profile = import_profile({"name": "Setup", "macros": {}, "vibration": None})
    duplicate = dict(profile)
    duplicate["name"] = "Duplicate"
    with pytest.raises(ValueError, match="identifiers must be unique"):
        asyncio.run(service.dispatch("save_profiles", {"profiles": [profile, duplicate]}))

    profile["macros"]["M1"] = [
        {"id": "same", "buttons": ["A"], "duration": 10},
        {"id": "same", "buttons": ["B"], "duration": 10},
    ]
    with pytest.raises(ValueError, match="macro steps need unique identifiers"):
        asyncio.run(service.dispatch("save_profiles", {"profiles": [profile]}))


def test_bridge_rejects_wrong_payload_types():
    api = DesktopApi()
    try:
        for payload in ([], "", False, 0):
            assert not api.request("scan", payload)["ok"]
        assert not api.request("scan", {"x": "a" * 2_000_001})["ok"]
    finally:
        api._close()


def test_device_operations_are_serialized():
    active = 0

    async def scan(**_kwargs):
        nonlocal active
        active += 1
        assert active == 1
        await asyncio.sleep(0.01)
        active -= 1
        return []

    service = DeviceService(scanner=scan)

    async def together():
        return await asyncio.gather(
            service.dispatch("scan", {}), service.dispatch("scan", {})
        )

    assert asyncio.run(together()) == [[], []]


def test_failed_handshake_disconnects_candidate():
    closed = []

    class Pad:
        def __init__(self, _address):
            pass

        async def connect(self):
            pass

        async def device_info(self):
            raise ValueError("handshake failed")

        async def disconnect(self):
            closed.append(True)

    service = DeviceService(client_factory=Pad)
    service._found["scanned"] = object()
    with pytest.raises(ValueError, match="handshake"):
        asyncio.run(service.dispatch("connect", {"address": "scanned"}))
    assert closed == [True] and not service.connected()


def test_missing_macro_reply_is_not_an_empty_slot():
    from x20ctl.client import X20, ControllerError

    pad = X20("test")

    async def absent(*_args):
        return None

    pad.query = absent
    with pytest.raises(ControllerError, match="No valid reply"):
        asyncio.run(pad.read_macro(1, strict=True))

    async def empty(*_args):
        return SimpleNamespace(crc_valid=True, payload=b"\x00")

    pad.query = empty
    assert asyncio.run(pad.read_macro(1, strict=True)) is None


def test_missing_remap_reply_is_not_a_default_layout():
    from x20ctl.client import X20, ControllerError

    pad = X20("test")

    async def absent(*_args):
        return None

    pad.read_body = absent
    with pytest.raises(ControllerError, match="No remapping reply"):
        asyncio.run(pad.remappings([p.Key.A]))


def test_disconnected_write_fails_closed():
    api = DesktopApi()
    try:
        response = api.request("apply", {"category": "vibration", "value": 50})
        assert not response["ok"]
        assert "disconnected" in response["error"]["message"]
        assert not api.request("shell", {"command": "anything"})["ok"]
    finally:
        api._close()


def test_curve_roundtrip_preserves_wire_values_and_flags():
    original = p.Curve(7, 15, (80, 95), (175, 190), flags=7)
    assert curve_from_ui(curve_to_ui(original), original) == original
    invalid = curve_to_ui(original)
    invalid["innerDeadzone"] = 99
    invalid["outerDeadzone"] = 1
    with pytest.raises(ValueError):
        curve_from_ui(invalid, original)


def test_macro_roundtrip_and_hardware_limit():
    rows = [
        {
            "buttons": ["A"],
            "leftStick": 2,
            "rightStick": 0,
            "durationMs": 100,
            "intervalMs": 60,
        }
    ]
    entries = macro_from_ui(rows)
    assert len(entries) == 2
    assert entries[-1] == p.MacroStep.released(60)
    assert macro_from_ui(macro_to_ui(p.MacroProgram(entries))) == entries
    with pytest.raises(ValueError, match="47"):
        macro_from_ui(rows * 24)
    with pytest.raises(ValueError, match="5 ms"):
        macro_from_ui([{**rows[0], "intervalMs": 16}])


def test_empty_hardware_macro_sentinel_can_be_saved(tmp_path):
    from x20ctl.desktop.settings import import_profile

    # Xpert2 firmware 9.01 returned a released, zero-duration wire entry
    # for unused M2-M4 slots. It must not become an invalid editable hold.
    profile = import_profile({"name": "Read from controller", "macros": {}})
    for slot in ("M2", "M3", "M4"):
        profile["macros"][slot] = macro_to_ui(p.MacroProgram([p.MacroStep.released(0)]))
    service = DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("save_profiles", {"profiles": [profile]}))
    saved = asyncio.run(service.dispatch("bootstrap", {}))["profiles"][0]
    assert saved["macros"] == {"M1": [], "M2": [], "M3": [], "M4": []}


def test_macro_translation_keeps_real_initial_delay():
    program = p.MacroProgram([p.MacroStep.released(0), p.MacroStep.released(50), p.MacroStep(p.mask_for([p.Key.A]), 100)])
    rows = macro_to_ui(program)
    assert len(rows) == 2
    assert rows[0]["buttons"] == []
    assert rows[0]["durationMs"] == 50
    assert rows[1]["buttons"] == ["A"]
    assert rows[1]["durationMs"] == 100


def test_readback_mismatch_never_reports_success():
    class Pad:
        _client = SimpleNamespace(is_connected=True)

        async def capabilities(self):
            return SimpleNamespace(motors=2)

        async def set_vibration(self, value):
            return (0, 0)

    service = DeviceService()
    service.pad = Pad()
    with pytest.raises(Exception, match="read-back differs"):
        asyncio.run(service.dispatch("apply", {"category": "vibration", "value": 50}))


def test_connect_rejects_unscanned_address():
    service = DeviceService()
    with pytest.raises(ValueError, match="Scan"):
        asyncio.run(service.dispatch("connect", {"address": "untrusted"}))
