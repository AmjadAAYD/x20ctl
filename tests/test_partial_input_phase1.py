import asyncio
import pytest


@pytest.mark.parametrize(
    "row,model,status",
    [
        ({"vid": 0x2345, "pid": 0xE062}, "d10", "verified_receiver"),
        ({"vid": 0x1A34, "pid": 0xF517}, "x15", "experimental_receiver"),
        ({"vid": 0x045E, "pid": 0x028E}, None, "ambiguous_compatibility"),
        ({"vid": 0x0079, "pid": 0x181C}, None, "unknown"),
    ],
)
def test_receiver_identification_never_promotes_generic_identity(row, model, status):
    from x20ctl.controllers.compatibility import identify_receiver

    result = identify_receiver(row)
    assert result["model"] == model and result["status"] == status
    assert result["controller_link_verified"] is False


@pytest.mark.parametrize("model", ["d10", "x15", "x05", "x10"])
def test_profiles_enable_input_without_configuration(model):
    from x20ctl.controllers import get_profile

    p = get_profile(model)
    assert p.availability == "input_experimental"
    assert p.backend is None and p.input_backend
    assert p.known and p.missing


@pytest.mark.parametrize("model", ["d10", "x15", "x05", "x10"])
def test_service_allows_selected_input_player_but_still_blocks_writes(
    tmp_path, monkeypatch, model
):
    import x20ctl.desktop.service as m

    monkeypatch.setattr(m.sys, "platform", "win32")
    service = m.DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("select_model", {"model": model, "player": 2}))

    class Bindings:
        def poll(self, player):
            return {"connected": False, "input": None, "player": player}

    service._input_bindings = Bindings()
    assert asyncio.run(service.dispatch("gameplay_input", {"player": 2}))["player"] == 2
    with pytest.raises(ValueError):
        asyncio.run(service.dispatch("gameplay_input", {"player": 1}))
    for op in ["apply", "reset", "connect"]:
        with pytest.raises(ValueError):
            asyncio.run(service.dispatch(op, {}))


class Output:
    def __init__(self):
        self.commands = []

    def state(self, slot):
        return {"connected": True}

    def set_state(self, slot, left, right):
        self.commands.append((slot, left, right))
        return 0


@pytest.mark.parametrize(
    "parameters",
    [
        {"slot": 4, "left": 0.5, "right": 0.5, "duration": 0.35, "confirmed": True},
        {"slot": 0, "left": 2, "right": 0, "duration": 0.35, "confirmed": True},
        {"slot": 0, "left": 1, "right": 0, "duration": 3, "confirmed": True},
        {"slot": 0, "left": 1, "right": 0, "duration": 0.35, "confirmed": False},
    ],
)
def test_rumble_rejects_unbounded_or_unapproved_requests(parameters):
    from x20ctl.desktop.rumble import probe

    adapter = Output()
    with pytest.raises(ValueError):
        probe(**parameters, adapter=adapter, sleep=lambda _: None)
    assert adapter.commands == []


def test_rumble_resets_even_when_probe_interrupted():
    from x20ctl.desktop.rumble import probe

    adapter = Output()

    def fail(_):
        raise RuntimeError("interrupted")

    with pytest.raises(RuntimeError):
        probe(0, 1, 0.5, 0.35, True, adapter=adapter, sleep=fail)
    assert adapter.commands == [(0, 65535, 32768), (0, 0, 0)]


def test_rear_analysis_does_not_invent_independent_keys():
    from x20ctl.scanning.analysis import control_evidence

    frames = [
        {
            "source": "xinput_state",
            "values": {
                "slot": 0,
                "buttons": 0x1000,
                "lt": 0,
                "rt": 0,
                "lx": 0,
                "ly": 0,
                "rx": 0,
                "ry": 0,
                "packet": 1,
            },
        }
    ]
    result = control_evidence("M1_A", frames, "x15")
    assert result["observed_controls"] == ["A"]
    assert result["independent_rear_input"] == "not_exposed_by_xinput"
    assert result["configuration_protocol_verified"] is False


def test_unknown_hid_changes_are_not_a_mapping():
    from x20ctl.scanning.analysis import control_evidence

    result = control_evidence(
        "M2", [{"source": "hid_input", "report_hex": "06010203"}], "d10"
    )
    assert result["observed_controls"] == []
    assert result["independent_rear_input"] == "unknown"
    assert result["raw_report_variants"] == 1


def test_phase1_scan_keeps_input_and_protocol_capture_separate(tmp_path):
    from x20ctl.desktop.research_scan import ResearchScanner
    from tests.test_research_scanner import CaptureBackend, drive_to_review

    scanner = ResearchScanner(tmp_path, backend=CaptureBackend())
    scanner.start({"model": "X15", "consent": True, "rawInput": True})
    drive_to_review(scanner)
    import json

    mapping = json.loads((scanner.output / "input/button-map.json").read_text())
    assert mapping["protocolCapture"] is False
    assert mapping["independentRearInputs"] == "unknown"
    assert all(
        row["configuration_protocol_verified"] is False for row in mapping["actions"]
    )
    assert (scanner.output / "input/protocol-capture-status.json").exists()
    scanner.cancel()


def test_d10_vendor_collection_requires_exact_layout_and_opt_in():
    from x20ctl.scanning.windows import allowed_collection

    row = {
        "vid": 0x2345,
        "pid": 0xE062,
        "usage_page": 0xFFA0,
        "usage": 1,
        "input_len": 64,
        "output_len": 32,
    }
    assert not allowed_collection(row)
    assert allowed_collection(row, vendor_input=True)
    assert not allowed_collection({**row, "vid": 0x79}, vendor_input=True)
    assert not allowed_collection(
        {**row, "usage_page": 1, "usage": 6}, vendor_input=True
    )


def test_changing_model_invalidates_player_discovery(tmp_path):
    from x20ctl.desktop.service import DeviceService

    service = DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("select_model", {"model": "x15", "player": 2}))
    service._input_bindings.source_owner = 2
    service._input_bindings.sources = {"old": {}}
    asyncio.run(service.dispatch("select_model", {"model": "d10", "player": 2}))
    assert service._input_bindings.source_owner is None
    assert service._input_bindings.sources == {}


def test_trigger_evidence_distinguishes_reported_axis_from_digital_bit():
    from x20ctl.scanning.analysis import trigger_evidence

    samples = []
    for value in (0, 128, 255, 0):
        frame = bytearray.fromhex("0000000f808080800000")
        frame[8] = value
        if value:
            frame[2] = 1
        samples.append({"source": "hid_input", "report_hex": frame.hex()})
    result = trigger_evidence(samples, "x15", True)
    assert result["LT"]["representation"] == "axis_and_digital_bit_observed"
    assert result["LT"]["intermediate_values"] == [128]
    assert trigger_evidence(samples, "d10", False)["LT"]["representation"] == "unknown"


def test_backend_close_stops_only_active_rumble_slot(monkeypatch):
    from x20ctl.scanning.backend import Backend
    import x20ctl.desktop.rumble as m

    stopped = []
    monkeypatch.setattr(m, "stop", lambda slot: stopped.append(slot), raising=False)
    backend = Backend()
    backend.rumble_slot = 2

    class Session:
        def close(self):
            stopped.append("worker-closed")

    backend.session = Session()
    backend.close()
    assert stopped == ["worker-closed", 2]


class XInputFixture:
    def __init__(self):
        self.turn = 0
        self.pulses = []
        self.row = {
            "_key": "offline-fixture",
            "_parent": "fixture-parent",
            "kind": "hid",
            "vid": 0x2345,
            "pid": 0xE062,
            "usage_page": 1,
            "usage": 5,
            "input_len": 20,
        }

    def inventory(self):
        self.turn += 1
        return {
            "devices": [self.row] if self.turn % 2 == 0 else [],
            "xinput_slots": [0] if self.turn % 2 == 0 else [],
        }

    def details(self, rows):
        return rows

    def capture(
        self,
        report,
        selected,
        source,
        slot,
        filename,
        action,
        duration,
        vendor_input=False,
    ):
        import json
        from x20ctl.scanning.input_tests import summarize

        rows = []
        values = (0, 255, 0) if action.startswith("short_") else (0, 128, 255, 0)
        for i, value in enumerate(values):
            buttons = (0x3000 if action.endswith("A_B") else 0x1000) if value else 0
            state = {
                "slot": slot,
                "packet": i,
                "buttons": buttons,
                "lt": value,
                "rt": 0,
                "lx": 0,
                "ly": 0,
                "rx": 0,
                "ry": 0,
            }
            rows.append(
                {
                    "source": "xinput_state",
                    "action": action,
                    "timestamp": "2026-10-08T12:00:00Z",
                    "elapsed_ms": i * 20,
                    "values": state,
                }
            )
        path = report.directory / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(map(json.dumps, rows)) + "\n")
        return {**summarize(action, rows), "file": filename}

    def rumble(self, slot, left, right, confirmed):
        assert slot == 0 and confirmed is True
        self.pulses.append((left, right))
        return {
            "apiAccepted": True,
            "physicalFeedbackVerified": False,
            "left": left,
            "right": right,
        }


def test_optional_trigger_rear_macro_rumble_and_mode_sessions_use_only_fixture(
    tmp_path,
):
    import json
    import time
    from x20ctl.desktop.research_scan import ResearchScanner

    backend = XInputFixture()
    scanner = ResearchScanner(tmp_path, backend=backend)
    scanner.start({"model": "D10", "consent": True, "rawInput": True})
    next_mode = 0
    deadline = time.monotonic() + 12
    while time.monotonic() < deadline:
        state = scanner.status()
        if state["state"] == "review":
            break
        assert state["state"] not in {"failed", "cancelled"}, state
        if state.get("prompt"):
            prompt = state["prompt"]
            text = prompt["text"]
            if prompt["kind"] == "yes":
                answer = (
                    "skip"
                    if (
                        "Bluetooth LE" in text
                        or "BLE" in text
                        or "attach" in text
                        or "one non-RGB setting" in text
                    )
                    else "yes"
                )
            elif prompt["kind"] == "choice":
                if text.startswith("Choose the next mode"):
                    answer = str((0, 4)[next_mode]) if next_mode < 2 else "skip"
                    next_mode += 1
                else:
                    answer = "0"
            elif prompt["kind"] == "text":
                answer = (
                    "known original assignments; restore with the same owner procedure"
                )
            else:
                answer = "continue"
            scanner.answer({"promptId": prompt["id"], "answer": answer})
        time.sleep(0.002)
    assert scanner.status()["state"] == "review", scanner.status()
    mapping = json.loads((scanner.output / "input/button-map.json").read_text())
    assert any(r["action"] == "repeat2_M2" for r in mapping["actions"])
    assert any(
        r["action"] == "M1_A_B" and r["observed_controls"] == ["A", "B", "LT"]
        for r in mapping["actions"]
    )
    modes = json.loads((scanner.output / "input/trigger-modes.json").read_text())
    assert modes["long"]["LT"]["intermediate_values"] == [128]
    assert modes["short"]["LT"]["representation"] == "endpoint_only_axis_observed"
    macros = json.loads(
        (scanner.output / "experiments/macro-playback.json").read_text()
    )
    assert (
        macros["restorationConfirmed"] and macros["outboundProtocolCaptured"] is False
    )
    assert backend.pulses == [
        (0, 0),
        (0.25, 0.25),
        (0.5, 0.5),
        (0.75, 0.75),
        (1, 1),
        (0.25, 0),
        (0, 0.25),
    ]
    identities = json.loads(
        (scanner.output / "device/mode-identities.json").read_text()
    )
    assert [r["mode"] for r in identities["sessions"]] == ["wired_xinput", "firmware"]
    assert identities["flashAttempted"] is False
    scanner.cancel()


def test_printed_unknown_model_still_gets_generic_scan(tmp_path):
    from x20ctl.desktop.research_scan import ResearchScanner
    from tests.test_research_scanner import CaptureBackend, drive_to_review

    scanner = ResearchScanner(tmp_path, backend=CaptureBackend())
    scanner.start({"model": "Other controller", "consent": True, "rawInput": True})
    assert drive_to_review(scanner)["state"] == "review"
    scanner.cancel()
