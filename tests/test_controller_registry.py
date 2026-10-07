import asyncio
from types import SimpleNamespace

import pytest

from x20ctl.controllers import REGISTRY
from x20ctl.profiles import Profile, ProfileStore, MacroSpec
from x20ctl.desktop.service import DeviceService, DesktopApi
from x20ctl.desktop.settings import import_profile


PREVIEW_MODELS = tuple(key for key, value in REGISTRY.items() if value.backend is None)


def test_registry_published_counts_and_preview_models():
    assert {key: len(value.macro_slots) for key, value in REGISTRY.items()} == {
        "x20": 4, "x20_pro": 6, "x05": 0, "x05_pro": 2, "x10": 2, "d10": 2, "x15": 2,
    }
    assert REGISTRY["x20"].backend == "x20_keylinker"
    assert set(PREVIEW_MODELS) == {"x20_pro", "x05", "x05_pro", "x10", "d10", "x15"}
    assert all(REGISTRY[model].visible for model in REGISTRY)
    assert len(REGISTRY["x20_pro"].visual["motors"]) == 4
    assert REGISTRY["x05"].hardware["motion"] is False
    assert REGISTRY["x05_pro"].hardware["motion"] is None
    assert REGISTRY["x10"].hardware["rgb"] is None


def test_legacy_file_is_read_as_x20_without_rewriting(tmp_path):
    path = tmp_path / "legacy.json"
    original = '{"name":"Legacy", "macros":{"M1":{"keys":"A"}}}'
    path.write_text(original)
    profile = ProfileStore(str(tmp_path)).load("legacy")
    assert profile.controller_id == "x20"
    assert path.read_text() == original


@pytest.mark.parametrize("model", PREVIEW_MODELS)
def test_preview_macros_survive_store_roundtrip_and_cannot_apply(tmp_path, model):
    profile = Profile("Preview", controller_id=model)
    slots = REGISTRY[model].macro_slots
    if slots:
        profile.macros[slots[-1]] = MacroSpec("A,B")
    store = ProfileStore(str(tmp_path))
    store.save(profile)
    restored = store.load("Preview")
    assert restored.controller_id == model
    assert set(restored.macros) == set(slots)
    if slots:
        assert restored.macros[slots[-1]].keys == "A,B"
    with pytest.raises(ValueError, match="unverified"):
        asyncio.run(restored.apply(object()))
    with pytest.raises(ValueError, match="another controller"):
        import_profile(restored.to_dict())


@pytest.mark.parametrize("model", tuple(REGISTRY))
def test_profile_rejects_macro_slots_outside_selected_model(model):
    invalid_slot = f"M{len(REGISTRY[model].macro_slots) + 1}"
    with pytest.raises(ValueError, match="unknown slot"):
        Profile.from_dict({"name": "wrong", "controller_id": model, "macros": {invalid_slot: None}})


def test_unlisted_model_cannot_load_or_be_selected(tmp_path, monkeypatch):
    with pytest.raises(ValueError, match="Unknown controller model"):
        Profile.from_dict({"name": "wrong", "controller_id": "unlisted_model", "macros": {}})
    service = DeviceService(directory=tmp_path)

    async def forbidden_disconnect(_payload):
        raise AssertionError("An invalid selection must not change the session")

    monkeypatch.setattr(service, "disconnect", forbidden_disconnect)
    with pytest.raises(ValueError, match="Unknown controller model"):
        asyncio.run(service.dispatch("select_model", {"model": "unlisted_model"}))
    assert service._active_model == "x20"
    assert service._active_player == 1


@pytest.mark.parametrize("model", PREVIEW_MODELS)
def test_preview_model_can_be_selected_without_device_access(tmp_path, model):
    service = DeviceService(directory=tmp_path)
    assert asyncio.run(service.dispatch("select_model", {"model": model, "player": 3})) == {"model": model}
    assert service._active_model == model
    assert service._active_player == 3
    assert not list(tmp_path.iterdir())


@pytest.mark.parametrize("model", PREVIEW_MODELS)
@pytest.mark.parametrize("operation,payload", [
    ("scan", {}),
    ("connect", {"address": "scanned"}),
    ("read", {}),
    ("apply", {"category": "vibration", "value": 50}),
    ("reset", {"confirm": True}),
    ("input", {}),
    ("record_start", {}),
    ("record_stop", {}),
    ("pro_scan", {}),
    ("pro_inspect", {"address": "scanned"}),
    ("pro_hid", {}),
    ("controller_scan", {}),
    ("report_prepare", {"address": "scanned"}),
    ("report_pending", {}),
    ("report_send", {"scanId": "existing", "consent": True}),
    ("bootstrap", {}),
    ("save_profiles", {"profiles": []}),
    ("import_profile", {"profile": {"name": "Legacy"}}),
])
def test_preview_blocks_native_operations_before_device_or_store_access(tmp_path, monkeypatch, model, operation, payload):
    from x20ctl.desktop import pro_discovery

    async def forbidden(*args, **kwargs):
        raise AssertionError("Device or profile access must not happen")

    def forbidden_sync(*args, **kwargs):
        raise AssertionError("Device or profile access must not happen")

    monkeypatch.setattr(pro_discovery, "scan", forbidden)
    monkeypatch.setattr(pro_discovery, "inspect", forbidden)
    monkeypatch.setattr(pro_discovery, "hid_inventory", forbidden_sync)
    reports = SimpleNamespace(prepare=forbidden_sync, pending=forbidden_sync, send=forbidden_sync)
    service = DeviceService(directory=tmp_path, scanner=forbidden, client_factory=forbidden_sync, report_workflow=reports)
    asyncio.run(service.dispatch("select_model", {"model": model}))
    monkeypatch.setattr(service, "require_pad", forbidden_sync)
    monkeypatch.setattr(service._reader, "poll", forbidden_sync)
    for action in ("bootstrap", "save_profiles", "import_profile"):
        monkeypatch.setattr(service, action, forbidden)
    service._found["scanned"] = object()
    service._pro_found["scanned"] = {"address": "scanned", "name": "Preview"}
    service._support_found["scanned"] = {"address": "scanned", "name": "Preview"}
    with pytest.raises(ValueError, match="unavailable"):
        asyncio.run(service.dispatch(operation, payload))
    assert not list(tmp_path.iterdir())


@pytest.mark.parametrize("model", PREVIEW_MODELS)
@pytest.mark.parametrize("operation", ["open_profile", "export_profile", "report_export"])
def test_preview_blocks_native_file_dialogs(tmp_path, monkeypatch, model, operation):
    service = DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("select_model", {"model": model}))
    api = DesktopApi(service)

    def forbidden(*args, **kwargs):
        raise AssertionError("A preview must not open a file dialog")

    monkeypatch.setattr(api, "_profile_dialog", forbidden)
    monkeypatch.setattr(api, "_report_dialog", forbidden)
    try:
        result = api.request(operation, {})
        assert result["ok"] is False
        assert result["error"]["code"] == "ValueError"
        assert "unavailable" in result["error"]["message"]
    finally:
        api._close()
    assert not list(tmp_path.iterdir())


def test_changing_player_disconnects_even_when_model_is_unchanged(tmp_path, monkeypatch):
    service = DeviceService(directory=tmp_path)
    disconnected = []

    async def disconnect(_payload):
        disconnected.append(service._active_player)

    monkeypatch.setattr(service, "disconnect", disconnect)
    service._found["old"] = object()
    asyncio.run(service.dispatch("select_model", {"model": "x20", "player": 2}))
    assert disconnected == [1]
    assert service._active_player == 2
    assert service._found == {}
    asyncio.run(service.dispatch("select_model", {"model": "x20", "player": 2}))
    assert disconnected == [1]
    asyncio.run(service.dispatch("select_model", {"model": "x20_pro", "player": 2}))
    assert disconnected == [1, 2]
    with pytest.raises(ValueError, match="unavailable"):
        asyncio.run(service.dispatch("apply", {}))


@pytest.mark.parametrize("model", tuple(REGISTRY))
@pytest.mark.parametrize("player", [0, 5, True, False, "2", None, 1.0])
def test_invalid_player_cannot_change_native_session(tmp_path, player, model):
    service = DeviceService(directory=tmp_path)
    with pytest.raises(ValueError, match="player between"):
        asyncio.run(service.dispatch("select_model", {"model": model, "player": player}))
    assert service._active_model == "x20"
    assert service._active_player == 1
