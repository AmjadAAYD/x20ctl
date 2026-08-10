"""High-level client for a KeyLinker controller over BLE.

Wraps the request/response plumbing so callers work in terms of settings rather
than packets. Replies are matched to requests by the serial byte, which the pad
echoes.

    async with X20(address) as pad:
        print(await pad.device_info())
        await pad.set_vibration(60)
        await pad.set_macro(1, "A+B", hold_ms=100)

Discovery, if the address isn't known:

    address = await find_controller()
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass, field

from bleak import BleakClient, BleakScanner

from . import protocol as p

CONFIG_SERVICE = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

# Never touch. Firmware lives here.
OTA_SERVICE = "2de516f0-50f5-45d5-a1b2-c3565de543ae"

ADVERTISED_NAME = "Xpert2"
VENDOR_MAC_PREFIX = "98:B6:E"

DEFAULT_TIMEOUT = 4.0

# The two records that hold response curves, each two channels of seven bytes.
# Same shape, different opcodes and a different scale for the deadzones.
CURVE_KINDS = {
    "sticks": (p.Op.HOST_STICK, p.Op.WRITE_STICK, p.STICK_MAX_PROGRESS),
    "triggers": (p.Op.HOST_TRIGGER, p.Op.WRITE_TRIGGER, p.TRIGGER_MAX_PROGRESS),
}


def _curve_kind(kind: str) -> tuple[int, int, int]:
    if kind not in CURVE_KINDS:
        raise ValueError(f"kind must be one of {', '.join(CURVE_KINDS)}, got {kind!r}")
    return CURVE_KINDS[kind]


class ControllerError(RuntimeError):
    pass


class NotSupported(ControllerError):
    """The pad's capability descriptor says it doesn't expose this setting."""


async def find_controller(timeout: float = 8.0) -> str | None:
    """Scan for a controller's configuration peripheral and return its address.

    Matches on the advertised name or the vendor MAC range, never on USB
    VID/PID, which the pad clones from Microsoft.
    """
    found: dict[str, str] = {}

    def seen(device, adv) -> None:
        name = adv.local_name or device.name or ""
        if ADVERTISED_NAME.lower() in name.lower() or \
                device.address.upper().startswith(VENDOR_MAC_PREFIX):
            found.setdefault(device.address, name)

    scanner = BleakScanner(detection_callback=seen)
    await scanner.start()
    try:
        await asyncio.sleep(timeout)
    finally:
        await scanner.stop()
    return next(iter(found), None)


@dataclass
class Snapshot:
    """Everything readable from the pad in one pass."""

    device: p.DeviceInfo
    capabilities: p.Capabilities
    name: str
    vibration: tuple[int, int]
    sticks: list[bytes]
    triggers: list[bytes]
    battery: p.Battery | None = None
    raw: dict[str, bytes] = field(default_factory=dict)


class X20:
    """A connected controller.

    Only reads and settings writes. This class has no path to the bootloader:
    it speaks BLE GATT to the configuration service and nothing else.
    """

    def __init__(self, address: str, *, timeout: float = DEFAULT_TIMEOUT):
        self.address = address
        self.timeout = timeout
        self._client: BleakClient | None = None
        self._serial = p.SerialCounter()
        self._host_counter = 8       # matches CodeHelper.countHost
        self._save_counter = 0       # matches CodeHelper.countSaveButtons
        self._waiters: dict[int, asyncio.Future] = {}
        self._caps: p.Capabilities | None = None
        self._layout: p.MaskLayout | None = None

    # -- connection ------------------------------------------------------

    async def __aenter__(self) -> "X20":
        await self.connect()
        return self

    async def __aexit__(self, *exc) -> None:
        await self.disconnect()

    async def connect(self) -> None:
        self._client = BleakClient(self.address)
        await self._client.connect()
        services = {s.uuid.lower() for s in self._client.services}
        if CONFIG_SERVICE not in services:
            await self._client.disconnect()
            raise ControllerError(
                f"{self.address} does not expose the configuration service. "
                "Is this the gamepad interface rather than the Xpert2 peripheral?"
            )
        await self._client.start_notify(CONFIG_NOTIFY, self._on_notify)

    async def disconnect(self) -> None:
        if self._client and self._client.is_connected:
            try:
                await self._client.stop_notify(CONFIG_NOTIFY)
            except Exception:
                pass
            await self._client.disconnect()
        self._client = None

    # -- transport -------------------------------------------------------

    def _on_notify(self, _sender, data: bytearray) -> None:
        try:
            pkt = p.parse(bytes(data))
        except Exception:
            return
        waiter = self._waiters.pop(pkt.serial, None)
        if waiter and not waiter.done():
            waiter.set_result(pkt)

    def _next_host_counter(self) -> int:
        if self._host_counter == 0:
            self._host_counter = 8
        self._host_counter -= 1
        return self._host_counter

    def _next_save_counter(self) -> int:
        self._save_counter += 1
        return self._save_counter

    async def _send(self, packet: bytes, serial: int, *, expect_reply: bool = True):
        if self._client is None:
            raise ControllerError("not connected")
        loop = asyncio.get_running_loop()
        future: asyncio.Future = loop.create_future()
        if expect_reply:
            self._waiters[serial] = future
        await self._client.write_gatt_char(CONFIG_WRITE, packet, response=True)
        if not expect_reply:
            return None
        try:
            return await asyncio.wait_for(future, self.timeout)
        except asyncio.TimeoutError:
            self._waiters.pop(serial, None)
            return None

    async def query(self, opcode: p.Op, payload: bytes = b"") -> p.Packet | None:
        serial = self._serial.next()
        return await self._send(p.build(opcode, payload, serial=serial), serial)

    async def read_body(self, opcode: p.Op, payload: bytes = b"") -> p.Body | None:
        """Query and strip the length prefix, following chunks if needed."""
        first = await self.query(opcode, payload)
        if first is None or not first.crc_valid:
            return None
        body = p.unwrap(first.payload)
        # Records longer than one packet continue at the next index.
        index = 0
        while not body.complete and index < 8:
            index += 1
            more = await self.query(opcode, bytes([index]))
            if more is None or not more.crc_valid:
                break
            body = p.Body(declared=body.declared, data=body.data + more.payload)
        return body

    # -- reads -----------------------------------------------------------

    async def device_info(self) -> p.DeviceInfo:
        pkt = await self.query(p.Op.READ_VID_PID_VERSION)
        if pkt is None:
            raise ControllerError("no reply to device info")
        return p.parse_device_info(pkt.payload)

    async def capabilities(self, *, refresh: bool = False) -> p.Capabilities:
        if self._caps is None or refresh:
            body = await self.read_body(p.Op.HOST_MENU, bytes([0, p.MenuKind.MENU]))
            if body is None:
                raise ControllerError("no reply to the capability query")
            self._caps = p.parse_capabilities(body)
        return self._caps

    async def macro_layout(self, *, refresh: bool = False) -> p.MaskLayout:
        """The mask layout this particular controller uses.

        Bit positions are decided by the pad's own macro key list, so they are
        a property of the device instead of of the model. Reading it means a
        controller that reports a different list still gets correct macros.
        Falls back to the X20 layout if the pad won't say.
        """
        if self._layout is None or refresh:
            body = await self.read_body(
                p.Op.HOST_MENU, bytes([0, p.MenuKind.MACRO_SUPPORT]))
            if body is None:
                self._layout = p.DEFAULT_LAYOUT
            else:
                try:
                    keys = p.decode_key_list(bytes([body.declared]) + body.data)
                    self._layout = p.layout_from_key_list(keys)
                except ValueError:
                    self._layout = p.DEFAULT_LAYOUT
        return self._layout

    async def name(self) -> str:
        body = await self.read_body(p.Op.READ_NAME, bytes([0]))
        return body.as_text() if body else ""

    async def vibration(self) -> tuple[int, int]:
        """Motor 1 and 2 strength as percentages."""
        body = await self.read_body(p.Op.HOST_MOTOR, bytes([0]))
        if body is None:
            raise ControllerError("no reply to the vibration query")
        return (round(body.data[0] * 100 / 255), round(body.data[1] * 100 / 255))

    async def curves(self, kind: str) -> list[p.Curve]:
        """Read a stick or trigger record, left channel first.

        `kind` is "sticks" or "triggers".
        """
        read_op, _write_op, scale = _curve_kind(kind)
        body = await self.read_body(read_op, bytes([0]))
        if body is None:
            raise ControllerError(f"no reply to the {kind} query")
        return p.parse_curves(body, scale)

    async def battery(self) -> p.Battery | None:
        """Read charge state.

        The query puts the pad into a live-input streaming mode, so this always
        sends NORMAL_MODE afterwards to stop it. Returns None if the pad does
        not answer, which some models may not.
        """
        pkt = await self.query(p.Op.SET_MODE, p.POWER_QUERY_PAYLOAD)
        try:
            if pkt is None or not pkt.crc_valid:
                return None
            return p.parse_battery(p.unwrap(pkt.payload))
        except ValueError:
            return None
        finally:
            # Leave the pad as we found it whether or not the read worked.
            serial = self._serial.next()
            await self._send(
                p.build(p.Op.NORMAL_MODE, p.NORMAL_MODE_PAYLOAD, serial=serial),
                serial, expect_reply=False,
            )

    async def snapshot(self) -> Snapshot:
        caps = await self.capabilities()
        sticks = await self.read_body(p.Op.HOST_STICK, bytes([0]))
        triggers = await self.read_body(p.Op.HOST_TRIGGER, bytes([0]))
        raw = {}
        for label, op in (("stick", p.Op.HOST_STICK), ("trigger", p.Op.HOST_TRIGGER),
                          ("motor", p.Op.HOST_MOTOR), ("turbo", p.Op.HOST_TURBO),
                          ("changekey", p.Op.HOST_CHANGEKEY)):
            body = await self.read_body(op, bytes([0]))
            if body:
                raw[label] = body.data
        return Snapshot(
            device=await self.device_info(),
            capabilities=caps,
            name=await self.name(),
            vibration=await self.vibration(),
            sticks=sticks.groups(7) if sticks else [],
            triggers=triggers.groups(7) if triggers else [],
            battery=await self.battery(),
            raw=raw,
        )

    # -- writes ----------------------------------------------------------

    async def set_vibration(self, percent: int) -> tuple[int, int]:
        """Set both motors. Reads first so undecoded trailing bytes are preserved."""
        if not 0 <= percent <= 100:
            raise ValueError("percent must be 0-100")
        caps = await self.capabilities()
        if not caps.motors:
            raise NotSupported("this pad does not expose vibration settings")

        body = await self.read_body(p.Op.HOST_MOTOR, bytes([0]))
        if body is None:
            raise ControllerError("could not read current vibration")

        value = round(percent * 255 / 100)
        data = bytearray(body.data)
        data[0] = data[1] = value
        payload = bytes([body.declared]) + bytes(data)

        serial = p.save_button_serial(slot=0, counter=self._next_save_counter())
        packet = p.build(
            p.Op.WRITE_MOTOR, payload, serial=serial,
            length_field=p.host_length(len(payload), self._next_host_counter()),
        )
        await self._send(packet, serial)
        return await self.vibration()

    async def set_curves(self, kind: str, channels: list[p.Curve]) -> list[p.Curve]:
        """Write a whole stick or trigger record, then read it back.

        The record carries every channel at once, so a caller changing one side
        passes the other side through unchanged. Returns what the pad reports
        afterwards rather than what was sent, which is the only way to tell a
        write that took from one that was ignored.
        """
        _read_op, write_op, _scale = _curve_kind(kind)
        caps = await self.capabilities()
        if not (caps.sticks if kind == "sticks" else caps.triggers):
            raise NotSupported(f"this pad does not expose {kind} settings")

        current = await self.curves(kind)
        if len(channels) != len(current):
            raise ValueError(
                f"this pad reports {len(current)} {kind} channels and the record "
                f"is written whole, so all {len(current)} have to be supplied")
        for channel in channels:
            channel.validate()

        payload = p.build_curves(channels)
        # Two channels come to exactly 20 bytes on the wire, which is the whole
        # cap. A pad reporting a third would not fit, and no chunked form of
        # this write has been observed, so say so rather than failing inside the
        # packet builder.
        if len(payload) + p.FRAME_OVERHEAD > p.MAX_PACKET:
            raise ControllerError(
                f"this pad's {kind} record is {len(payload)} bytes, which does "
                f"not fit the {p.MAX_PACKET}-byte packet cap. Writing a record "
                "that large would need chunking, and nothing has been observed "
                "doing it.")

        serial = p.save_button_serial(slot=0, counter=self._next_save_counter())
        packet = p.build(
            write_op, payload, serial=serial,
            length_field=p.host_length(len(payload), self._next_host_counter()),
        )
        await self._send(packet, serial)
        return await self.curves(kind)

    async def set_macro(
        self,
        slot: int,
        keys: str,
        *,
        hold_ms: int = 100,
        gap_ms: int = 60,
        loop_ms: int = 0,
    ) -> None:
        """Write a macro to slot 1-4.

        `keys` uses ',' to separate steps in time and '+' to join keys within a
        step: "A,B" is A then B, "A+B" is both at once.
        """
        if not 1 <= slot <= 4:
            raise ValueError("slot must be 1-4")
        caps = await self.capabilities()
        if not caps.macros:
            raise NotSupported("this pad does not expose macros")

        layout = await self.macro_layout()
        steps = p.parse_sequence(keys, hold_ms, gap_ms)

        payload = p.build_macro_payload(steps, loop_interval_ms=loop_ms)
        for packet in p.build_macro_writes(
            payload, slot=slot - 1, start_counter=self._next_save_counter()
        ):
            serial = p.unscramble(packet)[2]
            await self._send(packet, serial)
            await asyncio.sleep(0.25)

    async def clear_macro(self, slot: int) -> None:
        """Empty a macro slot.

        Sends a bare [0], which is what the app emits for an empty step list.
        An empty header is a different thing and doesn't clear.
        """
        if not 1 <= slot <= 4:
            raise ValueError("slot must be 1-4")
        packet = p.build_macro_write(
            p.MACRO_CLEAR, slot=slot - 1, chunk=0, counter=self._next_save_counter()
        )
        serial = p.unscramble(packet)[2]
        await self._send(packet, serial)
