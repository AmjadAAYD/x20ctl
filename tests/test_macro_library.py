import asyncio
import json
from types import SimpleNamespace

import pytest

from x20ctl.controllers import REGISTRY
from x20ctl.desktop.macro_library import MacroLibrary, validate_macro
from x20ctl.desktop.service import DeviceService, DesktopApi


def macro(model="x20", gap=0, length=1):
    return {"format": "x20ctl-macro", "version": 1, "name": "Combo", "sourceModel": model,
            "loopMs": 0, "steps": [{"buttons": ["A", "RT"], "leftStick": 8, "rightStick": 0,
                                     "durationMs": 40, "intervalMs": gap} for _ in range(length)]}


def test_library_roundtrip_is_separate_from_profiles_and_rejects_bad_save(tmp_path):
    store = MacroLibrary(tmp_path)
    profiles = tmp_path / "profiles.json"
    profiles.write_text("keep existing setups")
    assert store.load() == []
    first = store.change("save", {"entry": macro()})
    second = store.change("save", {"entry": macro()})
    assert len(second) == 2 and second[0]["id"] != second[1]["id"]
    before = store.path.read_bytes()
    with pytest.raises(ValueError):
        store.change("save", {"entry": macro(gap=5, length=24)})
    assert store.path.read_bytes() == before
    assert profiles.read_text() == "keep existing setups"
    remaining = store.change("delete", {"id": first[0]["id"]})
    assert len(remaining) == 1
    assert store.load()[0]["id"] == remaining[0]["id"]


@pytest.mark.parametrize("patch", [
    {"format": "script"}, {"version": True}, {"version": 2}, {"name": ""},
    {"sourceModel": "unknown"}, {"sourceModel": "x05"}, {"loopMs": 1},
    {"loopMs": float("nan")}, {"loopMs": True}, {"mode": "toggle"},
])
def test_invalid_metadata_cannot_enter_library(patch):
    with pytest.raises(ValueError):
        validate_macro({**macro(), **patch})


@pytest.mark.parametrize("patch", [{"buttons": ["CAPTURE"]}, {"buttons": ["A", "A"]},
    {"buttons": [{}]}, {"durationMs": 0}, {"durationMs": 327680}, {"intervalMs": 7},
    {"leftStick": 9}, {"rightStick": True}, {"durationMs": float("inf")}])
def test_invalid_steps_cannot_enter_library(patch):
    value = macro()
    value["steps"][0].update(patch)
    with pytest.raises(ValueError):
        validate_macro(value)


def test_import_budget_and_independent_step_identifiers():
    clean = validate_macro(macro(length=47))
    assert len(clean["steps"]) == 47
    assert len({row["id"] for row in clean["steps"]}) == 47
    with pytest.raises(ValueError, match="47"):
        validate_macro(macro(gap=5, length=24))


def test_recording_input_represents_both_sticks_without_analog_precision_claim():
    from x20ctl import protocol as p
    from x20ctl.input import GamepadState
    state = GamepadState(slot=0, packet=1, buttons=frozenset([p.Key.A]),
        left_trigger=0, right_trigger=0, left_stick=(32767, 0), right_stick=(0, 32767))
    assert state.macro_inputs == [p.Key.A, p.StickInput(p.Key.LSTICK_ANALOG, p.Direction.RIGHT),
                                  p.StickInput(p.Key.RSTICK_ANALOG, p.Direction.UP)]


@pytest.mark.parametrize("model", [key for key, profile in REGISTRY.items() if profile.macro_slots])
def test_local_library_works_in_each_model_without_hardware_access(tmp_path, monkeypatch, model):
    service = DeviceService(directory=tmp_path)

    def forbidden(*args, **kwargs):
        raise AssertionError("No hardware operation is allowed")

    service._active_model = model
    monkeypatch.setattr(service._reader, "poll", forbidden)
    monkeypatch.setattr(service, "require_pad", forbidden)
    result = asyncio.run(service.dispatch("macro_library", {"action": "save", "entry": macro(model)}))
    assert result["entries"][0]["sourceModel"] == model
    assert result["path"].endswith("macro-library.json")
    assert len(asyncio.run(service.dispatch("macro_library", {}))["entries"]) == 1


def test_import_export_dialogs_on_preview_are_only_file_operations(tmp_path):
    import webview
    service = DeviceService(directory=tmp_path)
    service._active_model = "x20_pro"
    api = DesktopApi(service)
    source = tmp_path / "input.json"
    destination = tmp_path / "output.json"
    source.write_text(json.dumps(macro()))
    results = iter([(str(source),), str(destination)])
    api._window = SimpleNamespace(create_file_dialog=lambda *args, **kwargs: next(results))
    try:
        imported = api.request("macro_import")
        assert imported["ok"] and imported["data"]["sourceModel"] == "x20"
        assert api.request("macro_export", {"entry": imported["data"]})["data"] == {"saved": True}
        exported = json.loads(destination.read_text())
        assert exported["steps"][0]["buttons"] == ["A", "RT"]
        assert not (tmp_path / "macro-library.json").exists()
    finally:
        api._close()


def test_corrupt_library_cannot_be_overwritten_by_save(tmp_path):
    store = MacroLibrary(tmp_path)
    store.path.write_text("broken json")
    with pytest.raises(ValueError):
        store.change("save", {"entry": macro()})
    assert store.path.read_text() == "broken json"
