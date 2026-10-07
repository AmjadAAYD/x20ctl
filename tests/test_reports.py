import hashlib
import asyncio
import json
import os
import subprocess
import zipfile
from pathlib import Path

import pytest

from x20ctl.desktop import reports, scanner_integrity as integrity
from x20ctl.desktop.service import DeviceService
from x20ctl.desktop import report_scanner


def approved(tmp_path, monkeypatch, content=b"official scanner"):
    scanner = tmp_path / integrity.SCANNER_FILENAME
    scanner.write_bytes(content)
    monkeypatch.setattr(integrity, "packaged_scanner_path", lambda: scanner)
    return scanner, hashlib.sha256(content).hexdigest()


def test_integrity_exact_file_and_mutation_matrix(tmp_path, monkeypatch):
    original = b"official scanner"
    scanner, digest = approved(tmp_path, monkeypatch, original)
    assert integrity.verify_scanner(scanner, digest) == digest
    for changed in (b"Official scanner", b"official scanneR", original + b"!", original[:-1], b"replacement"):
        scanner.write_bytes(changed)
        with pytest.raises(integrity.ScannerIntegrityError):
            integrity.verify_scanner(scanner, digest)
    scanner.write_bytes(original)
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.verify_scanner(scanner, hashlib.sha256(b"new version").hexdigest())
    assert integrity.verify_scanner(scanner, digest) == digest
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.verify_scanner(tmp_path / "wrong.exe", digest)
    scanner.unlink()
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.verify_scanner(scanner, digest)


def test_unreadable_file_and_link_rejected(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    scanner.unlink()
    try:
        scanner.symlink_to(tmp_path / "target")
    except OSError:
        pass  # Windows without Developer Mode cannot create symlinks.
    else:
        with pytest.raises(integrity.ScannerIntegrityError):
            integrity.verify_scanner(scanner, digest)
        scanner.unlink()
    scanner.mkdir()
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.verify_scanner(scanner, digest)


def test_unreadable_scanner_fails_closed(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    original = Path.open
    def denied(path, *args, **kwargs):
        if path == scanner:
            raise PermissionError("denied")
        return original(path, *args, **kwargs)
    monkeypatch.setattr(Path, "open", denied)
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.verify_scanner(scanner, digest)


def test_process_never_runs_when_identity_fails_and_never_uses_shell(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    calls = []
    def fake_run(argv, **kwargs):
        calls.append((argv, kwargs))
        return subprocess.CompletedProcess(argv, 0, b'{"schemaVersion":1,"status":"complete","scannerVersion":"1.0.0"}', b"")
    monkeypatch.setattr(integrity.subprocess, "run", fake_run)
    assert integrity.run_scanner(scanner, "AA:BB:CC:DD:EE:FF", "Controller ; rm", tmp_path, digest) == digest
    assert calls[0][0][0] == str(scanner)
    assert calls[0][0][4] == "Controller ; rm"
    assert calls[0][1]["shell"] is False
    scanner.write_bytes(b"tampered")
    with pytest.raises(integrity.ScannerIntegrityError):
        integrity.run_scanner(scanner, "AA:BB:CC:DD:EE:FF", "Controller", tmp_path, digest)
    assert len(calls) == 1


def test_process_failure_hides_local_paths(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    def timeout(argv, **kwargs):
        raise subprocess.TimeoutExpired(argv, 45)
    monkeypatch.setattr(integrity.subprocess, "run", timeout)
    with pytest.raises(ValueError, match="Scanner timed out") as error:
        integrity.run_scanner(scanner, "AA:BB:CC:DD:EE:FF", "Synthetic", tmp_path, digest)
    assert str(tmp_path) not in str(error.value)


@pytest.mark.parametrize("code,output", [(0, b"not json"), (0, b'{"schemaVersion":2,"status":"complete","scannerVersion":"1.0.0"}'), (10, b'{"schemaVersion":1,"status":"complete","scannerVersion":"1.0.0"}')])
def test_helper_protocol_rejects_bad_status(tmp_path, monkeypatch, code, output):
    scanner, digest = approved(tmp_path, monkeypatch)
    monkeypatch.setattr(integrity.subprocess, "run", lambda argv, **kw: subprocess.CompletedProcess(argv, code, output, b""))
    with pytest.raises(ValueError):
        integrity.run_scanner(scanner, "AA:BB:CC:DD:EE:FF", "Controller", tmp_path, digest)


def valid_output(folder: Path, name="Synthetic X20"):
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "device.json").write_text(json.dumps({"controllerName": name, "vid": None, "pid": None}), encoding="utf-8")


def test_report_validation_rejects_files_links_archives_and_oversize(tmp_path):
    output = tmp_path / "output"
    valid_output(output)
    assert [p.name for p in reports.validate_output(output)] == ["device.json"]
    extra = output / "extra.txt"
    extra.write_text("no")
    with pytest.raises(ValueError): reports.validate_output(output)
    extra.unlink()
    extra = output / "evil.exe"
    extra.write_bytes(b"MZ")
    with pytest.raises(ValueError): reports.validate_output(output)
    extra.unlink()
    extra = output / "README.txt"
    extra.write_bytes(b"PK\x03\x04nested")
    with pytest.raises(ValueError): reports.validate_output(output)
    extra.unlink()
    try:
        extra.symlink_to(output / "device.json")
    except OSError:
        pass
    else:
        with pytest.raises(ValueError): reports.validate_output(output)
        extra.unlink()
    extra.write_bytes(b"x" * (reports.MAX_EXPANDED + 1))
    with pytest.raises(ValueError): reports.validate_output(output)
    extra.unlink()
    (output / "subdir").mkdir()
    with pytest.raises(ValueError): reports.validate_output(output)


def test_zip_contains_only_validated_root_files(tmp_path):
    output = tmp_path / "output"
    valid_output(output)
    (output / "system.json").write_text('{"reportSchemaVersion":1}', encoding="utf-8")
    archive = tmp_path / "report.zip"
    names, size, digest = reports.make_zip(output, archive)
    assert set(names) == {"device.json", "system.json"}
    assert size == archive.stat().st_size and digest == hashlib.sha256(archive.read_bytes()).hexdigest()
    with zipfile.ZipFile(archive) as zipped:
        assert zipped.namelist() == names
        assert zipped.testzip() is None
        assert all(not name.startswith("/") and "/" not in name for name in names)


def test_oversized_zip_is_rejected_before_upload(tmp_path):
    output = tmp_path / "output"
    valid_output(output)
    (output / "hid-report-descriptor.bin").write_bytes(os.urandom(reports.MAX_ZIP + 1024))
    with pytest.raises(ValueError, match="ZIP exceeds"):
        reports.make_zip(output, tmp_path / "report.zip")


def test_local_report_consent_retry_and_exact_payload(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    def fake_runner(path, address, name, output, expected):
        assert path == scanner and address == "AA:BB:CC:DD:EE:FF" and expected == digest
        valid_output(output, name)
        return digest
    calls = []
    def fake_upload(ident, metadata, data):
        calls.append((ident, metadata.copy(), data))
        if len(calls) == 1:
            raise ConnectionError("offline")
        return {"submissionId": "CR-" + "A" * 24}
    workflow = reports.ReportWorkflow(tmp_path / "reports", scanner, digest, fake_runner, fake_upload)
    preview = workflow.prepare("AA:BB:CC:DD:EE:FF", "Synthetic X20")
    assert preview["scannerVersion"] == "1.0.0"
    assert "system.json" in preview["files"]
    assert len(workflow.pending()) == 1
    with pytest.raises(ValueError, match="consent"):
        workflow.send(preview["scanId"], False)
    assert not calls
    with pytest.raises(ConnectionError):
        workflow.send(preview["scanId"], True)
    assert len(workflow.pending()) == 1
    result = workflow.send(preview["scanId"], True)
    assert result["submissionId"].startswith("CR-")
    assert calls[0] == calls[1]
    assert len(workflow.pending()) == 0
    with pytest.raises(ValueError): workflow.send(preview["scanId"], True)


def test_integrity_failure_prevents_prepare_and_upload(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    uploads = []
    def runner(path, address, name, output, expected):
        integrity.verify_scanner(path, expected)
        valid_output(output, name)
        return digest
    workflow = reports.ReportWorkflow(tmp_path / "reports", scanner, digest, runner,
                                       lambda *args: uploads.append(args))
    scanner.write_bytes(b"tampered")
    with pytest.raises(integrity.ScannerIntegrityError): workflow.prepare("AA:BB:CC:DD:EE:FF", "Synthetic")
    assert uploads == [] and workflow.pending() == []


def test_integrity_failure_after_preview_still_blocks_upload(tmp_path, monkeypatch):
    scanner, digest = approved(tmp_path, monkeypatch)
    uploads = []
    def runner(path, address, name, output, expected):
        integrity.verify_scanner(path, expected)
        valid_output(output, name)
        return digest
    workflow = reports.ReportWorkflow(tmp_path / "reports", scanner, digest, runner,
                                       lambda *args: uploads.append(args))
    preview = workflow.prepare("AA:BB:CC:DD:EE:FF", "Synthetic")
    scanner.write_bytes(b"tampered")
    with pytest.raises(integrity.ScannerIntegrityError):
        workflow.send(preview["scanId"], True)
    assert uploads == [] and len(workflow.pending()) == 1


def test_upload_uses_receiver_multipart_contract(monkeypatch):
    class Response:
        status = 201
        def __enter__(self): return self
        def __exit__(self, *args): pass
        def read(self, size): return b'{"success":true,"submissionId":"CR-AAAAAAAAAAAAAAAAAAAAAAAA"}'
    def urlopen(outgoing, timeout):
        assert timeout == 20 and outgoing.full_url == reports.ENDPOINT
        body = outgoing.data
        assert b'name="clientSubmissionId"' in body
        assert b'name="metadata"' in body
        assert b'name="report"; filename="report.zip"' in body
        assert b'"controllerName":"Synthetic"' in body
        assert b"PK\x03\x04synthetic" in body
        return Response()
    monkeypatch.setattr(reports.request, "urlopen", urlopen)
    assert reports.upload_report("123e4567-e89b-42d3-a456-426614174000",
                                 {"controllerName": "Synthetic", "appVersion": "3.1.0", "scannerVersion": "1.0.0"},
                                 b"PK\x03\x04synthetic")["submissionId"].startswith("CR-")


def test_service_scans_only_currently_selected_peripheral(tmp_path):
    class FakeReports:
        def __init__(self): self.calls = []
        def prepare(self, address, name):
            self.calls.append((address, name))
            return {"scanId": "test"}
        def pending(self): return []
    workflow = FakeReports()
    service = DeviceService(directory=tmp_path, report_workflow=workflow)
    asyncio.run(service.dispatch("select_model", {"model": "x20"}))
    service._support_found = {"AA:BB:CC:DD:EE:FF": {"name": "Selected", "address": "AA:BB:CC:DD:EE:FF"}}
    with pytest.raises(ValueError):
        asyncio.run(service.dispatch("report_prepare", {"address": "11:22:33:44:55:66"}))
    assert workflow.calls == []
    assert asyncio.run(service.dispatch("report_prepare", {"address": "AA:BB:CC:DD:EE:FF"})) == {"scanId": "test"}
    assert workflow.calls == [("AA:BB:CC:DD:EE:FF", "Selected")]


def test_scanner_reads_only_standard_gatt_from_selected_address(monkeypatch):
    class Characteristic:
        def __init__(self, uuid): self.uuid, self.properties = uuid, ["read"]
    class Service:
        def __init__(self, uuid, chars): self.uuid, self.characteristics = uuid, chars
    class Client:
        def __init__(self, address, timeout):
            assert address == "AA:BB:CC:DD:EE:FF"
            self.services = [
                Service(report_scanner.INFO_SERVICE, [Characteristic(next(iter(report_scanner.STANDARD))), Characteristic("00002a25-0000-1000-8000-00805f9b34fb")]),
                Service("vendor", [Characteristic("vendor-char")]),
            ]
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def read_gatt_char(self, char):
            assert char.uuid in report_scanner.STANDARD
            return b"Synthetic model"
    monkeypatch.setattr(report_scanner, "BleakClient", Client)
    data = asyncio.run(report_scanner.collect("AA:BB:CC:DD:EE:FF", "Selected"))
    assert data["controllerName"] == "Selected"
    assert data["vid"] is None and data["pid"] is None
    assert data["standardGatt"] == {"modelNumber": "Synthetic model"}
