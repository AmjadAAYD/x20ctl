import asyncio

from x20ctl import client


def test_weak_signal_gets_advice_and_good_signal_does_not():
    assert client.signal_note(-88) is not None and "-88 dBm" in client.signal_note(-88)
    assert client.signal_note(-85) is not None
    assert client.signal_note(-60) is None
    assert client.signal_note(None) is None


def test_scan_remembers_the_strongest_signal(monkeypatch):
    class Device:
        address = "98:B6:ED:23:60:61"; name = "Xpert2"

    class Adv:
        def __init__(self, rssi): self.local_name = "Xpert2"; self.rssi = rssi; self.service_uuids = ()

    class Scanner:
        def __init__(self, detection_callback): self.cb = detection_callback
        async def start(self):
            for rssi in (-91, -84, -88): self.cb(Device(), Adv(rssi))
        async def stop(self): pass

    monkeypatch.setattr(client, "BleakScanner", Scanner)
    monkeypatch.setattr(client, "SCAN_ROUND", 0.01)
    result = asyncio.run(client.find_controllers(timeout=0.02))
    assert [f.rssi for f in result] == [-84]


def test_a_scan_that_hears_nothing_gets_one_more_window(monkeypatch):
    starts = []

    class Scanner:
        def __init__(self, detection_callback): self.cb = detection_callback
        async def start(self): starts.append(1)
        async def stop(self): pass

    async def none():
        return []

    monkeypatch.setattr(client, "BleakScanner", Scanner)
    monkeypatch.setattr(client, "SCAN_ROUND", 0.01)
    monkeypatch.setattr(client, "known_controllers", none)
    asyncio.run(client.find_controllers(timeout=0.01))
    assert len(starts) == 2


def test_a_connection_timeout_is_explained_not_a_traceback(capsys):
    from x20ctl import cli, diagnose

    assert diagnose.diagnose(TimeoutError()) is diagnose.OUT_OF_RANGE

    async def times_out(_args):
        raise TimeoutError

    class Args:
        fn = staticmethod(times_out)

    class Parser:
        def parse_args(self, _argv): return Args()

    original = cli.build_parser
    cli.build_parser = lambda: Parser()
    try:
        assert cli.main(["sleep", "never"]) == 1
    finally:
        cli.build_parser = original
    out = capsys.readouterr().out
    assert "not responding" in out and "Traceback" not in out


def test_bluez_connection_is_read_from_bluetoothctl_info():
    assert client.parse_connected("Device 98:B6:ED:23:60:61 (public)\n\tName: Xpert2\n\tPaired: yes\n\tConnected: yes\n")
    assert not client.parse_connected("Device 98:B6:ED:23:60:61 (public)\n\tName: Xpert2\n\tConnected: no\n")
    assert not client.parse_connected("")


def test_scan_listens_for_the_seconds_asked(monkeypatch):
    from x20ctl import cli

    seen = {}

    async def fake(timeout=8.0, *, first=False, everything=None):
        seen["timeout"] = timeout; return []

    monkeypatch.setattr(cli, "find_controllers", fake)
    args = cli.build_parser().parse_args(["scan", "--all", "--seconds", "60"])
    asyncio.run(cli.cmd_scan(args))
    assert seen["timeout"] == 60
    asyncio.run(cli.cmd_scan(cli.build_parser().parse_args(["scan", "--seconds", "9999"])))
    assert seen["timeout"] == 120
