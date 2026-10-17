"""Scanning and connecting without a controller: the Bluetooth stack is replaced by fakes.

The pad advertises only now and then, so discovery must not give up after one quiet window and
a connect must not give up after one failure. These tests pin that behaviour; they say nothing
about real Bluetooth hardware.
"""
from __future__ import annotations

import asyncio
from types import SimpleNamespace

import pytest
from bleak.exc import BleakError

from x20ctl import client

PAD = "98:B6:ED:23:60:61"


def run(coro):
    return asyncio.run(coro)


def test_is_controller_by_name_prefix_or_config_service():
    assert client.is_controller("Xpert2-1234", "11:22:33:44:55:66")
    assert client.is_controller("", PAD)
    assert client.is_controller("", "11:22:33:44:55:66", [client.CONFIG_SERVICE.upper()])
    assert not client.is_controller("Keyboard", "11:22:33:44:55:66", ["0000180f-0000-1000-8000-00805f9b34fb"])


def test_parse_known_devices_keeps_only_pads():
    listing = (f"Device {PAD.lower()} Xpert2\n"
               "Device 11:22:33:44:55:66 Yunzii Keyboard\n"
               "Device 98:B6:E9:00:00:01\n"
               "garbage\n")
    found = client.parse_known_devices(listing)
    assert [f.address for f in found] == [PAD, "98:B6:E9:00:00:01"]
    assert found[0].name == "Xpert2"


class FakeScanner:
    """Delivers advertisements only from the nth start onwards."""

    starts = 0
    quiet_rounds = 0
    callback = None

    def __init__(self, detection_callback=None, **_):
        type(self).callback = detection_callback

    async def start(self):
        type(self).starts += 1
        if type(self).starts > type(self).quiet_rounds:
            device = SimpleNamespace(address=PAD, name="Xpert2")
            type(self).callback(device, SimpleNamespace(local_name="Xpert2", rssi=-60, service_uuids=[]))

    async def stop(self):
        pass

    @staticmethod
    async def find_device_by_address(address, timeout=10.0):
        return SimpleNamespace(address=address, name="Xpert2")


@pytest.fixture
def scanner(monkeypatch):
    FakeScanner.starts = 0
    FakeScanner.quiet_rounds = 0
    monkeypatch.setattr(client, "BleakScanner", FakeScanner)
    monkeypatch.setattr(client, "known_controllers", lambda: asyncio.sleep(0, result=[]))
    return FakeScanner


def test_scan_restarts_and_finds_a_pad_that_advertises_late(scanner, monkeypatch):
    monkeypatch.setattr(client, "SCAN_ROUND", 0.05)
    scanner.quiet_rounds = 2
    found = run(client.find_controllers(0.5, first=True))
    assert [f.address for f in found] == [PAD]
    assert scanner.starts == 3


def test_find_controller_returns_as_soon_as_one_is_seen(scanner, monkeypatch):
    monkeypatch.setattr(client, "SCAN_ROUND", 30)
    assert run(asyncio.wait_for(client.find_controller(60), 3)) == PAD


def test_scan_falls_back_to_pads_the_system_knows(scanner, monkeypatch):
    monkeypatch.setattr(client, "SCAN_ROUND", 0.05)
    scanner.quiet_rounds = 99
    monkeypatch.setattr(client, "known_controllers",
                        lambda: asyncio.sleep(0, result=[client.Found(PAD, "Xpert2")]))
    assert run(client.find_controller(0.2)) == PAD


def test_everything_collects_every_advertisement(scanner, monkeypatch):
    monkeypatch.setattr(client, "SCAN_ROUND", 0.05)
    heard: list = []
    run(client.find_controllers(0.05, everything=heard))
    assert heard and heard[0][0] == PAD


class FakeLink:
    """A BleakClient whose first `failures` connects fail."""

    failures = 0
    attempts = 0
    targets: list = []

    def __init__(self, target, **_):
        type(self).targets.append(target)
        self.is_connected = False
        self.services = [SimpleNamespace(uuid=client.CONFIG_SERVICE)]

    async def connect(self):
        type(self).attempts += 1
        if type(self).attempts <= type(self).failures:
            raise BleakError("Device not found")
        self.is_connected = True

    async def start_notify(self, *_):
        pass

    async def stop_notify(self, *_):
        pass

    async def disconnect(self):
        self.is_connected = False


@pytest.fixture
def link(monkeypatch, scanner):
    FakeLink.failures = 0
    FakeLink.attempts = 0
    FakeLink.targets = []
    monkeypatch.setattr(client, "BleakClient", FakeLink)

    async def no_wait(_):
        return None

    monkeypatch.setattr(client.asyncio, "sleep", no_wait)
    return FakeLink


def test_connect_retries_and_then_succeeds(link):
    link.failures = 2
    pad = client.X20(PAD)
    run(pad.connect())
    assert link.attempts == 3
    assert link.targets[0] == PAD
    assert getattr(link.targets[1], "address", None) == PAD   # a retry looks the device up first


def test_connect_gives_up_with_the_last_error(link):
    link.failures = 99
    pad = client.X20(PAD)
    with pytest.raises(BleakError, match="Device not found"):
        run(pad.connect())
    assert link.attempts == client.CONNECT_ATTEMPTS
