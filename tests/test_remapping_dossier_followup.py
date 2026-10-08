import asyncio
import struct
from types import SimpleNamespace
import pytest


def test_x15_family_discovery_is_a_hint_not_remap_authorization():
    from x20ctl.scanning.model_evidence import ble_hints

    gatt = {
        "services": [
            {
                "uuid": "d7f010e0-660d-46e9-96c3-19c4148bdab5",
                "characteristics": [
                    {
                        "uuid": "d7f010e1-660d-46e9-96c3-19c4148bdab5",
                        "handle": 101,
                        "properties": ["write"],
                    },
                    {
                        "uuid": "d7f010e2-660d-46e9-96c3-19c4148bdab5",
                        "handle": 103,
                        "properties": ["notify"],
                    },
                ],
            },
            {"uuid": "0000ff12-0000-1000-8000-00805f9b34fb", "characteristics": []},
        ]
    }
    result = ble_hints("X15", {"name": "EasySMX X15"}, gatt)
    assert result["families"] == ["keylinker_family", "x15_ff12_owner_observed"]
    assert (
        result["configurationWritesAuthorized"] is False
        and result["modelIdentified"] is False
    )


def test_qmacro_name_discovery_does_not_enable_a_backend():
    from x20ctl.scanning.model_evidence import ble_hints

    result = ble_hints("X10", {"name": "QMacro"}, {"services": []})
    assert result["families"] == ["qmacro_name_candidate"]
    assert result["configurationWritesAuthorized"] is False


def test_gatt_handles_are_read_from_discovery_and_vendor_values_are_not_read():
    from x20ctl.scanning.ble import inspect_client

    char = SimpleNamespace(
        uuid="d7f010e1-660d-46e9-96c3-19c4148bdab5",
        handle=123,
        properties=["write"],
        descriptors=[],
    )

    class Client:
        services = [
            SimpleNamespace(
                uuid="d7f010e0-660d-46e9-96c3-19c4148bdab5", characteristics=[char]
            )
        ]

        async def read_gatt_char(self, _):
            pytest.fail("vendor read")

        async def write_gatt_char(self, *_):
            pytest.fail("vendor write")

    result = asyncio.run(inspect_client(Client()))
    assert result["services"][0]["characteristics"][0]["handle"] == 123


@pytest.mark.parametrize("link", [187, 201])
def test_windows_hci_trace_container_can_be_imported_without_claiming_protocol(link):
    from x20ctl.scanning.app_capture import trace_info

    payload = b"\x04\x0e\x00"
    data = (
        struct.pack("<IHHIIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, link)
        + struct.pack("<IIII", 0, 0, len(payload), len(payload))
        + payload
    )
    result = trace_info(data, "windows")
    assert result["format"] == "hci_pcap" and result["transport"] == "bluetooth_hci"
    assert result["packets"] == 1


def test_network_trace_is_rejected():
    from x20ctl.scanning.app_capture import trace_info

    data = (
        struct.pack("<IHHIIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, 1)
        + struct.pack("<IIII", 0, 0, 1, 1)
        + b"\x00"
    )
    with pytest.raises(ValueError):
        trace_info(data, "windows")


@pytest.mark.parametrize("model", ["d10", "x15", "x05", "x10"])
def test_matching_discovery_clues_never_allow_remap_writes(tmp_path, model):
    from x20ctl.desktop.service import DeviceService

    class Pad:
        _client = SimpleNamespace(is_connected=True)

        async def capabilities(self):
            pytest.fail("unverified configuration accessed")

        async def set_remapping(self, _):
            pytest.fail("unverified remap write")

    service = DeviceService(directory=tmp_path)
    asyncio.run(service.dispatch("select_model", {"model": model, "player": 1}))
    service.pad = Pad()
    with pytest.raises(ValueError):
        asyncio.run(
            service.dispatch("apply", {"category": "remaps", "value": {"A": "B"}})
        )


def test_trace_association_retains_model_app_context_and_is_not_decoded(tmp_path):
    from x20ctl.desktop.research_scan import ResearchScanner
    from tests.test_research_scanner import Backend
    import json

    scanner = ResearchScanner(tmp_path, backend=Backend())
    scanner.start(
        {
            "model": "X15",
            "consent": True,
            "firmware": "owner-fw",
            "appName": "KeyLinker",
            "appVersion": "owner-app",
            "mode": "wired XInput",
            "transport": "direct_usb",
        }
    )
    scanner.cancel()
    scanner.thread.join(3)
    scanner.view.update(
        state="waiting", prompt={"id": "trace", "kind": "file", "choices": ["trace"]}
    )
    payload = b"\x04\x0e\x00"
    data = (
        struct.pack("<IHHIIII", 0xA1B2C3D4, 2, 4, 0, 0, 65535, 201)
        + struct.pack("<IIII", 0, 0, len(payload), len(payload))
        + payload
    )
    path = tmp_path / "hci.pcap"
    path.write_bytes(data)
    scanner.attach_file("trace", path)
    context = json.loads(
        (scanner.output / "attachments/trace-association.json").read_text()
    )
    assert context["model"] == "X15" and context["firmware"] == "owner-fw"
    assert context["appName"] == "KeyLinker" and context["appVersion"] == "owner-app"
    assert context["commandProtocolVerified"] is False


@pytest.mark.parametrize("model", ["X15", "X10"])
def test_owner_app_timeline_never_claims_decoded_or_physical_acceptance(model):
    from x20ctl.scanning.protocol_session import collect

    class Collector:
        options = {"model": model, "appName": "working app", "appVersion": "test"}
        files = {}

        def _ask(self, text, kind="continue"):
            return (
                "yes"
                if kind == "yes"
                else "saved original A mapping/profile"
                if kind == "text"
                else "continue"
            )

        def _write(self, name, data):
            self.files[name] = data

        def _mark(self, *args):
            pass

    collector = Collector()
    collect(collector)
    record = collector.files["experiments/protocol-timeline.json"]
    assert [e["event"] for e in record["events"]] == [
        "baseline",
        "change_save",
        "readback",
        "phone_disconnect",
        "power_cycle",
        "restore",
        "restore_power_cycle",
    ]
    assert record["configurationWritesByX20CTL"] is False
    assert (
        record["commandProtocolVerified"] is False
        and record["physicalAcceptanceComplete"] is False
    )


def test_hci_pcapng_import_and_truncation_rejection():
    from x20ctl.scanning.app_capture import trace_info

    def block(kind, body):
        size = len(body) + 12
        return struct.pack("<II", kind, size) + body + struct.pack("<I", size)

    section = block(0x0A0D0D0A, struct.pack("<IHHq", 0x1A2B3C4D, 1, 0, -1))
    interface = block(1, struct.pack("<HHI", 201, 0, 65535))
    packet = block(6, struct.pack("<IIIII", 0, 0, 0, 3, 3) + b"\x04\x0e\x00\x00")
    data = section + interface + packet
    assert trace_info(data, "windows")["format"] == "hci_pcapng"
    with pytest.raises(ValueError):
        trace_info(data[:-1], "windows")


def test_vendor_read_properties_do_not_authorize_reads_or_writes():
    from x20ctl.scanning.ble import inspect_client

    char = SimpleNamespace(
        uuid="0000ff15-0000-1000-8000-00805f9b34fb",
        handle=88,
        properties=["read", "write-without-response"],
        descriptors=[],
    )

    class Client:
        services = [
            SimpleNamespace(
                uuid="0000ff12-0000-1000-8000-00805f9b34fb", characteristics=[char]
            )
        ]

        async def read_gatt_char(self, _):
            pytest.fail("unknown vendor read")

        async def write_gatt_char(self, *_):
            pytest.fail("unknown vendor write")

    asyncio.run(inspect_client(Client()))


@pytest.mark.parametrize(
    "name", ["EasySMX X15", "QMacro", "EasySMX X10", "EasySMX D10", "EasySMX X05"]
)
def test_foreign_configuration_names_cannot_enter_x20_writer(tmp_path, name):
    from x20ctl.client import Found
    from x20ctl.desktop.service import DeviceService

    address = "98:B6:E0:00:00:01"

    async def scanner(**_):
        return [Found(address, name)]

    def factory(_):
        pytest.fail("X20 driver instantiated for a different model")

    service = DeviceService(directory=tmp_path, scanner=scanner, client_factory=factory)
    assert asyncio.run(service.dispatch("scan", {})) == []
    service._found[address] = Found(address, name)
    with pytest.raises(ValueError):
        asyncio.run(service.dispatch("connect", {"address": address}))
