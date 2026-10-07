"""Research/input integration boundaries, using fixtures and no hardware/network."""

import hashlib
import importlib
import json
from pathlib import Path
import time

import pytest


def module():
    return importlib.import_module("x20ctl.desktop.research_scan")


def test_x15_decoder_is_input_only_and_independent():
    decode = importlib.import_module("x20ctl.desktop.x15_input").decode_hid
    neutral = bytes.fromhex("0000000f808080800000")
    assert decode(neutral)["buttons"] == []
    frame = bytearray(neutral)
    frame[1] = 1
    frame[3] = 1
    frame[8] = 128
    state = decode(frame)
    assert set(state["buttons"]) == {"A", "DPAD_UP", "DPAD_RIGHT"}
    assert state["leftTrigger"] == 128 / 255 and state["rightTrigger"] == 0
    assert not any(name.startswith("M") for name in state["buttons"])
    with pytest.raises(ValueError):
        decode(b"wrong")
    with pytest.raises(ValueError):
        decode(bytes([1]) + neutral[1:])


class Backend:
    def inventory(self):
        return {"devices": [], "xinput_slots": []}


def wait(scanner, predicate):
    deadline = time.monotonic() + 4
    while time.monotonic() < deadline:
        state = scanner.status()
        if predicate(state):
            return state
        time.sleep(0.01)
    raise AssertionError(scanner.status())


def test_declining_consent_does_not_start_collection(tmp_path):
    scanner = module().ResearchScanner(
        tmp_path, backend=Backend(), uploader=lambda *_: pytest.fail("upload")
    )
    with pytest.raises(ValueError, match="consent"):
        scanner.start({"model": "X05", "consent": False})
    assert scanner.status()["state"] == "idle"
    assert list(tmp_path.iterdir()) == []


def test_cancel_preserves_local_evidence_without_upload(tmp_path):
    scanner = module().ResearchScanner(
        tmp_path, backend=Backend(), uploader=lambda *_: pytest.fail("upload")
    )
    scanner.start({"model": "X05", "consent": True})
    wait(scanner, lambda s: s.get("prompt") is not None)
    scanner.cancel()
    state = wait(scanner, lambda s: s["state"] == "cancelled")
    assert state["receipt"] is None
    assert any(tmp_path.rglob("intake.json"))


def test_report_review_required_before_upload(tmp_path):
    scanner = module().ResearchScanner(
        tmp_path, backend=Backend(), uploader=lambda *_: pytest.fail("upload")
    )
    with pytest.raises(ValueError, match="review"):
        scanner.finish({"reviewed": True})


def test_upload_ack_and_archive_validation(tmp_path):
    m = module()
    output = tmp_path / "output"
    output.mkdir()
    (output / "device.json").write_text('{"modelDetected":false}')
    archive, digest = m.export_archive(
        output, tmp_path / "report.zip", {"consent": True, "reviewed": True}
    )
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == digest
    assert m.validate_research_archive(archive)["schemaVersion"] == 2
    (output / "payload.exe").write_bytes(b"MZ")
    with pytest.raises(ValueError):
        m.export_archive(
            output, tmp_path / "bad.zip", {"consent": True, "reviewed": True}
        )


def test_stale_answer_cannot_advance_another_prompt(tmp_path):
    scanner = module().ResearchScanner(tmp_path, backend=Backend())
    scanner.start({"model": "X15", "consent": True})
    wait(scanner, lambda s: s.get("prompt") is not None)
    with pytest.raises(ValueError, match="prompt"):
        scanner.answer({"promptId": "wrong", "answer": "continue"})
    scanner.cancel()


def test_published_collection_modules_have_no_output_calls():
    root = Path(__file__).resolve().parents[1] / "x20ctl/scanning"
    source = "\n".join(p.read_text() for p in root.glob("*.py"))
    for api in ("XInputSetState", "HidD_SetFeature", "HidD_SetOutputReport"):
        assert api not in source
    assert (
        json.loads((root / "PROVENANCE.json").read_text())["source_tag"]
        == "scanner-v1.1.0"
    )


class CaptureBackend(Backend):
    def __init__(self):
        self.round = 0
        self.row = {"_key": "fixture", "_path": "fixture-not-a-real-device", "_parent": "fixture",
                    "kind": "hid", "vid": 0x79, "pid": 0x181C, "usage_page": 1, "usage": 5,
                    "input_len": 10, "output_len": 5, "feature_len": 0, "product": "Fixture gamepad"}

    def inventory(self):
        self.round += 1
        return {"devices": [] if self.round == 1 else [self.row], "xinput_slots": []}

    def details(self, rows):
        return rows

    def capture(self, report, _selected, source, _slot, filename, action, duration):
        from x20ctl.scanning.input_tests import summarize
        frames = [bytearray.fromhex("0000000f808080800000") for _ in range(3)]
        if action.startswith("LT_") and action not in {"LT_released", "LT_release"}:
            frames[1][8] = 255 if action == "LT_full" else 128
        if action.startswith("RT_") and action not in {"RT_released", "RT_release"}:
            frames[1][9] = 255 if action == "RT_full" else 64
        if action == "A":
            frames[1][1] = 1
        rows = [{"source": source, "action": action, "report_hex": frame.hex(), "report_length": 10,
                 "timestamp": "2026-10-07T12:00:00Z", "elapsed_ms": i * 20} for i, frame in enumerate(frames)]
        path = report.directory / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(map(json.dumps, rows)) + "\n")
        return {**summarize(action, rows), "file": filename, "duration_ms": duration * 1000}


def drive_to_review(scanner):
    deadline = time.monotonic() + 8
    while time.monotonic() < deadline:
        state = scanner.status()
        if state["state"] == "review":
            return state
        if state["state"] in {"failed", "cancelled"}:
            raise AssertionError(state)
        if state.get("prompt"):
            prompt = state["prompt"]
            answer = "0" if prompt["kind"] == "choice" else "yes" if prompt["kind"] == "yes" and prompt["text"].startswith("Record the buttons") else "skip" if prompt["kind"] == "yes" else "" if prompt["kind"] == "text" else "continue"
            scanner.answer({"promptId": prompt["id"], "answer": answer})
        time.sleep(.005)
    raise AssertionError(scanner.status())


def test_full_fixture_scan_requires_review_then_sends_exact_archive_once(tmp_path):
    uploads = []

    def upload(ident, metadata, data):
        uploads.append((ident, metadata, hashlib.sha256(data).hexdigest()))
        return {"submissionId": "CR-" + "A" * 24}

    scanner = module().ResearchScanner(tmp_path, backend=CaptureBackend(), uploader=upload)
    scanner.start({"model": "X15", "consent": True, "rawInput": True, "autoSubmit": True})
    state = drive_to_review(scanner)
    assert uploads == []
    assert len(state["coverage"]) == 24
    values = json.loads((scanner.output / "input/trigger-summary.json").read_text())
    assert values["LT"]["status"] == "intermediate_values_observed"
    with pytest.raises(ValueError):
        scanner.finish({"reviewed": False})
    scanner.finish({"reviewed": True})
    state = wait(scanner, lambda s: s["state"] == "submitted")
    assert len(uploads) == 1 and uploads[0][2] == state["sha256"]
    assert state["receipt"] == "CR-" + "A" * 24
    assert module().validate_research_archive(scanner.archive)["rawInput"] is True
    scanner.archive.write_bytes(b"changed")
    with pytest.raises(ValueError):
        scanner.export_bytes(state["reportId"])


def test_failure_keeps_zip_and_retry_reuses_identity(tmp_path):
    attempts = []

    def upload(ident, _metadata, _data):
        attempts.append(ident)
        if len(attempts) == 1:
            raise ValueError("Receiver unavailable")
        return {"submissionId": "CR-" + "B" * 24}

    scanner = module().ResearchScanner(tmp_path, backend=CaptureBackend(), uploader=upload)
    scanner.start({"model": "X15", "consent": True, "rawInput": True, "autoSubmit": True})
    drive_to_review(scanner)
    scanner.finish({"reviewed": True})
    wait(scanner, lambda s: s["state"] == "upload_failed")
    assert scanner.archive.exists()
    scanner.retry()
    wait(scanner, lambda s: s["state"] == "submitted")
    assert len(attempts) == 2 and attempts[0] == attempts[1]


def test_local_only_scope_does_not_upload(tmp_path):
    scanner = module().ResearchScanner(tmp_path, backend=CaptureBackend(), uploader=lambda *_: pytest.fail("upload"))
    scanner.start({"model": "X15", "consent": True, "rawInput": True, "autoSubmit": False})
    drive_to_review(scanner)
    assert scanner.finish({"reviewed": True})["state"] == "saved"


def test_internal_worker_capabilities_without_device_access():
    from x20ctl.desktop.scan_worker import WorkerSession
    worker = WorkerSession()
    try:
        result = worker.call({"operation": "capabilities"})
        assert result["hardware_access"] is False and result["same_executable"] is True
    finally:
        worker.close()


def test_binding_is_explicit_per_player_and_disconnect_clears_input():
    from x20ctl.desktop.x15_input import InputBindings

    class Worker:
        def __init__(self):
            self.closed = False
            self.fail = False

        def call(self, payload, **_options):
            if self.fail:
                raise OSError("fixture disconnect")
            if payload["operation"] == "input_read":
                return {"buttons": ["A"], "slot": 0}
            return {"attached": True}

        def close(self):
            self.closed = True

    bindings = InputBindings(CaptureBackend(), worker_factory=Worker)
    bindings.discover("before")
    found = bindings.discover("after")
    token = found[0]["token"]
    bindings.attach(2, token)
    assert bindings.poll(2)["input"]["buttons"] == ["A"]
    assert bindings.poll(1) == {"connected": False, "input": None}
    with pytest.raises(ValueError, match="another player"):
        bindings.attach(3, token)
    reader = bindings.bindings[2]["worker"]
    reader.fail = True
    assert bindings.poll(2) == {"connected": False, "input": None}
    assert reader.closed


def test_service_declining_scan_does_not_disconnect_existing_controller(tmp_path):
    import asyncio
    from x20ctl.desktop.service import DeviceService
    service = DeviceService(directory=tmp_path)
    calls = []

    async def disconnect(_payload):
        calls.append("disconnect")

    service.disconnect = disconnect
    with pytest.raises(ValueError, match="consent"):
        asyncio.run(service.dispatch("research_start", {"model": "X15", "consent": False}))
    assert calls == []
