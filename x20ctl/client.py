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
import shutil
import sys
from dataclasses import dataclass, field

from bleak import BleakClient, BleakScanner
from bleak.exc import BleakError

from . import protocol as p
from . import validation

CONFIG_SERVICE = "d7f010e0-660d-46e9-96c3-19c4148bdab5"
CONFIG_WRITE = "d7f010e1-660d-46e9-96c3-19c4148bdab5"
CONFIG_NOTIFY = "d7f010e2-660d-46e9-96c3-19c4148bdab5"

# Never touch. Firmware lives here.
OTA_SERVICE = "2de516f0-50f5-45d5-a1b2-c3565de543ae"

ADVERTISED_NAME = "Xpert2"
# The prefixes the official app's AgreementConfig admits: standard gamepad,
# two-in-one and Switch-mode devices. Anything else in 98:B6:E_ is not one.
VENDOR_MAC_PREFIXES = ("98:B6:E9", "98:B6:EC", "98:B6:ED")

DEFAULT_TIMEOUT = 4.0

# The configuration peripheral advertises only now and then, so one short scan
# or one connect attempt is not enough to say the pad is absent.
SCAN_ROUND = 5.0
CONNECT_ATTEMPTS = 3
CONNECT_TIMEOUT = 12.0

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


class NotValidated(ControllerError):
    """Nothing proves this request is right for this controller.

    Raised before anything is sent. The pad identified itself, but its identity
    is not in the evidence table (x20ctl.validation), or the feature has never
    been proven on it.
    """


@dataclass(frozen=True)
class Found:
    """A controller seen while scanning."""

    address: str
    name: str = ""
    rssi: int | None = None

    @property
    def label(self) -> str:
        return self.name or "Controller"


# Below this a pad is heard only now and then and a connection often fails.
WEAK_SIGNAL = -85


def signal_note(rssi: int | None) -> str | None:
    """What to do about a weak signal, or None when the signal is fine or unknown."""
    if rssi is None or rssi > WEAK_SIGNAL:
        return None
    return (f"Signal is very weak ({rssi} dBm). Bring the pad within a metre or two of the PC "
            "and away from USB 3 ports and metal; settings commands often fail to connect at this range.")


# Set by the command line to show progress while a slow step runs; None stays silent.
progress = None


def _say(message: str) -> None:
    if progress is not None:
        progress(message)


def is_controller(name: str, address: str, service_uuids=()) -> bool:
    """Whether an advertisement belongs to a pad we can configure.

    Matches on the advertised name, the vendor MAC range or the configuration
    service it advertises, never on USB VID/PID, which the pad clones from
    Microsoft.
    """
    return (ADVERTISED_NAME.lower() in (name or "").lower()
            or (address or "").upper().startswith(VENDOR_MAC_PREFIXES)
            or CONFIG_SERVICE in {str(u).lower() for u in service_uuids or ()})


def parse_known_devices(listing: str) -> list[Found]:
    """Controllers in `bluetoothctl devices` output (lines like `Device AA:BB:.. Name`)."""
    found: list[Found] = []
    for line in listing.splitlines():
        parts = line.strip().split(None, 2)
        if len(parts) >= 2 and parts[0] == "Device":
            name = parts[2] if len(parts) > 2 else ""
            if is_controller(name, parts[1]):
                found.append(Found(address=parts[1].upper(), name=name))
    return found


async def known_controllers() -> list[Found]:
    """Pads the system's Bluetooth stack already knows, found or not by a scan.

    BlueZ keeps paired and recently seen devices, and a pad that is paired or
    connected may not advertise at all, so a scan alone can miss it. Linux only;
    returns nothing anywhere else or if bluetoothctl is unavailable.
    """
    if not sys.platform.startswith("linux") or not shutil.which("bluetoothctl"):
        return []
    try:
        process = await asyncio.create_subprocess_exec(
            "bluetoothctl", "devices", stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.DEVNULL)
        out, _ = await asyncio.wait_for(process.communicate(), 5)
    except (OSError, asyncio.TimeoutError):
        return []
    return parse_known_devices(out.decode("utf-8", "replace"))


def parse_connected(info: str) -> bool:
    """Whether `bluetoothctl info ADDRESS` output says BlueZ holds a connection to the device."""
    return any(line.strip().lower() == "connected: yes" for line in info.splitlines())


async def bluez_connected(address: str) -> bool:
    """Whether BlueZ (Blueman, bluetoothctl, the desktop applet) already holds a connection to
    this address. The pad takes one connection at a time, so settings cannot connect while it does.
    Linux only; False anywhere else or if bluetoothctl is unavailable."""
    if not sys.platform.startswith("linux") or not shutil.which("bluetoothctl"):
        return False
    try:
        process = await asyncio.create_subprocess_exec(
            "bluetoothctl", "info", address, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
        out, _ = await asyncio.wait_for(process.communicate(), 5)
    except (OSError, asyncio.TimeoutError):
        return False
    return parse_connected(out.decode("utf-8", "replace"))


async def find_controllers(timeout: float = 8.0, *, first: bool = False,
                           everything: list | None = None) -> list[Found]:
    """Every controller advertising right now, in the order first seen.

    Four pads can share a desk, so discovery returns all of them rather than
    whichever answered first (`first=True` returns as soon as one is seen). The
    scan runs in short rounds, restarting between them, because the pad
    advertises only now and then. If nothing is heard, pads the system already
    knows are returned instead. `everything`, when given, collects every
    advertisement heard as (address, name, rssi) for diagnosis.
    """
    found: dict[str, str] = {}
    strongest: dict[str, int] = {}
    anything = 0
    heard = asyncio.Event()

    def seen(device, adv) -> None:
        nonlocal anything
        anything += 1
        name = adv.local_name or device.name or ""
        rssi = getattr(adv, "rssi", None)
        if everything is not None:
            everything.append((device.address, name, rssi))
        if is_controller(name, device.address, getattr(adv, "service_uuids", ())):
            found.setdefault(device.address, name)
            if rssi is not None and rssi > strongest.get(device.address, -999):
                strongest[device.address] = rssi
            heard.set()

    remaining = timeout
    warmed = False
    while remaining > 0 and not (first and found):
        window = min(SCAN_ROUND, remaining)
        scanner = BleakScanner(detection_callback=seen)
        await scanner.start()
        try:
            if first:
                try:
                    await asyncio.wait_for(heard.wait(), window)
                except asyncio.TimeoutError:
                    pass
            else:
                await asyncio.sleep(window)
        finally:
            await scanner.stop()
        remaining -= window
        # hearing no device of any kind for a whole scan is unusual, so give it one more window
        # before giving up (cheap, and unconfirmed as a cure: a pad that is off or asleep does not
        # advertise, and a pad another program holds connected is not heard advertising either)
        if remaining <= 0 and not anything and not warmed:
            warmed = True; remaining = SCAN_ROUND
    result = [Found(address=address, name=name, rssi=strongest.get(address)) for address, name in found.items()]
    if not result:
        result = await known_controllers()
    return result


async def find_controller(timeout: float = 8.0) -> str | None:
    """The address of the first controller found, or None."""
    controllers = await find_controllers(timeout, first=True)
    return controllers[0].address if controllers else None


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

    @property
    def shutdown(self) -> int | None:
        """Idle shutdown in minutes, or None for never.

        It lives in the motor record, which this snapshot already reads, so
        exposing it here costs no extra traffic. Raises nothing: a pad whose
        motor record is too short simply has no field, and that reads as never
        rather than as an error, because the caller is a settings page.
        """
        data = self.raw.get("motor")
        if not data:
            return None
        try:
            return p.parse_shutdown(data)
        except ValueError:
            return None


class X20:
    """A connected controller.

    Only reads and settings writes. This class has no path to the bootloader:
    it speaks BLE GATT to the configuration service and nothing else.
    """

    def __init__(self, address: str, *, timeout: float = DEFAULT_TIMEOUT,
                 unvalidated_reads: bool = False):
        """`unvalidated_reads` lets an unrecognised controller answer read
        queries as well as identification, for research by its owner. It never
        allows a write."""
        self.address = address
        self.timeout = timeout
        self.unvalidated_reads = unvalidated_reads
        self._assessment: validation.Assessment | None = None
        self._info: p.DeviceInfo | None = None
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
        # whatever answers at this address is identified afresh
        self._info = self._assessment = self._caps = None
        last: Exception | None = None
        for attempt in range(CONNECT_ATTEMPTS):
            target = self.address
            _say(f"connecting, attempt {attempt + 1} of {CONNECT_ATTEMPTS}")
            if attempt:
                # a retry looks the pad up again first, so BlueZ has fresh state
                await asyncio.sleep(1.0)
                try:
                    target = await BleakScanner.find_device_by_address(
                        self.address, timeout=6.0) or self.address
                except (BleakError, OSError):
                    pass
            self._client = BleakClient(target, timeout=CONNECT_TIMEOUT)
            try:
                await self._client.connect()
                break
            except (BleakError, asyncio.TimeoutError, OSError) as error:
                last = error
                try:
                    await self._client.disconnect()
                except Exception:
                    pass
        else:
            self._client = None
            if await bluez_connected(self.address):
                raise ControllerError(
                    f"Bluetooth already holds a connection to {self.address} (from Blueman, bluetoothctl "
                    f"or the desktop's Bluetooth settings), and the pad takes one at a time. Disconnect it "
                    f"there, or run `bluetoothctl disconnect {self.address}`, then try again.") from last
            raise last
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

    async def assessment(self) -> validation.Assessment:
        """What the evidence table says about this controller (identifies it once)."""
        if self._assessment is None:
            self._assessment = validation.assess(await self.device_info())
        return self._assessment

    async def _authorize(self, packet: bytes) -> None:
        """Refuse, before sending, anything not proven for this controller.

        Every request passes through here, whichever method built it, so a new
        write path cannot skip the check. Identification is always allowed.
        """
        plain = p.unscramble(packet)
        kind, feature = validation.request_feature(plain[0], plain[4:-1])
        if kind == "identify":
            return
        verdict = await self.assessment()
        if kind == "read":
            if verdict.may_read() or self.unvalidated_reads:
                return
            raise NotValidated(verdict.reason)
        if feature is None or not verdict.may_write(feature):
            what = feature.replace("_", " ") if feature else f"command 0x{plain[0]:02X}"
            raise NotValidated(f"Changing {what} is not proven on this controller. {verdict.reason}")

    async def _send(self, packet: bytes, serial: int, *, expect_reply: bool = True):
        if self._client is None:
            raise ControllerError("not connected")
        await self._authorize(packet)
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
        """Query and strip the length prefix, following chunks if needed.

        The continuation query must keep the *shape* of the original payload.
        Most records take a single index byte, but HOST_MENU takes
        [position, kind], where position is the chunk index. Sending a bare
        index byte to HOST_MENU is a malformed request that answers with
        silence, so a chunked menu record used to truncate without error:
        kind 6 declares 16 bytes and only 14 arrive in the first packet.
        """
        first = await self.query(opcode, payload)
        if first is None or not first.crc_valid:
            return None
        body = p.unwrap(first.payload)
        # Records longer than one packet continue at the next index.
        index = 0
        while not body.complete and index < 8:
            index += 1
            if len(payload) >= 2:
                # [position, kind]: advance position, keep the sub-query.
                nxt = bytes([index]) + payload[1:]
            else:
                nxt = bytes([index])
            more = await self.query(opcode, nxt)
            if more is None or not more.crc_valid:
                break
            body = p.Body(declared=body.declared, data=body.data + more.payload)
        return body

    # -- reads -----------------------------------------------------------

    async def device_info(self) -> p.DeviceInfo:
        if self._info is None:
            pkt = await self.query(p.Op.READ_VID_PID_VERSION)
            if pkt is None:
                raise ControllerError("no reply to device info")
            self._info = p.parse_identity_reply(pkt.payload)
        return self._info

    async def host_guid(self) -> p.HostGuid | None:
        """The updater hardware id and firmware version, or None if the pad is silent.

        A read, like every query here. The record is 18 bytes and only 14 fit
        in the first packet, so this relies on read_body's continuation; that
        continuation has not been observed to work for this record on a pad.
        """
        body = await self.read_body(p.Op.HOST_GUID)
        if body is None or not body.complete:
            return None
        return p.parse_host_guid(body)

    async def capabilities(self, *, refresh: bool = False) -> p.Capabilities:
        if self._caps is None or refresh:
            body = await self.read_body(p.Op.HOST_MENU, bytes([0, p.MenuKind.MENU]))
            if body is None:
                raise ControllerError("no reply to the capability query")
            self._caps = p.parse_capabilities(body)
        return self._caps

    async def macro_limits(self) -> dict[str, int] | None:
        """Each macro slot's limit as the pad reports it, or None if it doesn't.

        Only pads whose descriptor sets D8 bit 1 are asked (kind 6), as the
        official app does. The numbers are the app's editor limits in steps; they
        are not used to size writes here, because their relation to the entry
        cap of this library (macro entries, press and release counted apart) is
        unmeasured.
        """
        caps = await self.capabilities()
        if not caps.new_macro_engine:
            return None
        body = await self.read_body(p.Op.HOST_MENU, bytes([0, p.MenuKind.MACRO_LIMITS]))
        if body is None:
            return None
        try:
            return caps.slot_limits(p.decode_macro_limits(body.data))
        except ValueError:
            return None

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
                    # body.declared is a byte count; the key count is the first
                    # byte of the record itself. Prepending declared made every
                    # real reply fail the count check and fall back below.
                    keys = p.decode_key_list(body.data)
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
        # checked first: the reply-less NORMAL_MODE below must not be the one refused
        await self._authorize(p.build(p.Op.SET_MODE, p.POWER_QUERY_PAYLOAD, serial=0))
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

    async def calibrate(self) -> None:
        """Calibrate the motion sensor against level.

        Lay the pad flat first: the app calls this "Sensor Calibration" and
        asks for a flat surface, which is a level reference for the gyro.

        It does not touch the sticks, tested and confirmed. Nothing is sent
        back; the app watches its indicator light rather than waiting for a
        reply, and so do we.
        """
        packet = p.build_calibrate(serial=self._next_save_counter())
        serial = p.unscramble(packet)[2]
        await self._send(packet, serial, expect_reply=False)

    async def factory_reset(self, *, second_generation: bool = True) -> None:
        """Clear every stored setting: macros, curves, remaps, the lot.

        Firmware is untouched. `second_generation` selects the protocol
        generation the way the phone app's `is_new_2_ver` flag does.

        It defaults to the newer one because the X20 ignores the older one
        **silently**: no error, no reply, nothing cleared. A reset that appears
        to do nothing is the wrong generation, not a broken pad.
        """
        packet = p.build_recover(second_generation=second_generation,
                                 serial=self._next_save_counter())
        serial = p.unscramble(packet)[2]
        await self._send(packet, serial, expect_reply=False)

    async def shutdown_timeout(self) -> int | None:
        """Minutes of idle before the pad powers off, or None for never."""
        body = await self.read_body(p.Op.HOST_MOTOR, bytes([0]))
        if body is None:
            raise ControllerError("could not read the motor record")
        return p.parse_shutdown(body.data)

    async def set_shutdown_timeout(self, minutes: int | None) -> int | None:
        """Set the idle shutdown timeout, then read back what the pad holds.

        The value lives in the motor record rather than in a command of its
        own, so this reads that record and rewrites it with the motor levels
        untouched.
        """
        payload_bytes = p.encode_shutdown(minutes)

        body = await self.read_body(p.Op.HOST_MOTOR, bytes([0]))
        if body is None:
            raise ControllerError("could not read the motor record")
        data = bytearray(body.data)
        if len(data) < p.SHUTDOWN_OFFSET + p.SHUTDOWN_WIDTH:
            raise NotSupported(
                "this pad's motor record has no shutdown field")
        data[p.SHUTDOWN_OFFSET:p.SHUTDOWN_OFFSET + p.SHUTDOWN_WIDTH] = payload_bytes
        payload = bytes([body.declared]) + bytes(data)

        serial = p.save_button_serial(slot=0, counter=self._next_save_counter())
        packet = p.build(
            p.Op.WRITE_MOTOR, payload, serial=serial,
            length_field=p.host_length(len(payload), self._next_host_counter()),
        )
        await self._send(packet, serial)
        return await self.shutdown_timeout()

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
        if not 1 <= slot <= 6:
            raise ValueError("slot must be 1-6")
        caps = await self.capabilities()
        if not caps.macros:
            raise NotSupported("this pad does not expose macros")

        await self.macro_layout()
        steps = p.parse_sequence(keys, hold_ms, gap_ms)
        await self.write_macro_steps(slot, steps, loop_ms=loop_ms)

    async def write_macro_steps(self, slot: int, steps, *,
                                loop_ms: int = 0) -> None:
        """Write a macro from steps rather than from the text syntax.

        The editor builds steps directly, so routing it through a string and
        parsing that back would be a lossy detour. `set_macro` is now this plus
        a parser.
        """
        if not 1 <= slot <= 6:
            raise ValueError("slot must be 1-6")
        caps = await self.capabilities()
        if not caps.macros:
            raise NotSupported("this pad does not expose macros")

        payload = p.build_macro_payload(steps, loop_interval_ms=loop_ms)
        for packet in p.build_macro_writes(
            payload, slot=slot - 1, start_counter=self._next_save_counter()
        ):
            serial = p.unscramble(packet)[2]
            await self._send(packet, serial)
            await asyncio.sleep(0.25)

    async def changekey_sources(self) -> list[int]:
        """The remappable source list, in the pad's own order."""
        body = await self.read_body(
            p.Op.HOST_MENU, bytes([0, p.MenuKind.CHANGEKEY_SUPPORT]))
        if body is None:
            return list(p.CHANGEKEY_DEFAULT_SOURCES)
        try:
            return p.decode_key_list(body.data)
        except ValueError:
            return list(p.CHANGEKEY_DEFAULT_SOURCES)

    async def remappings(self, sources: list[int] | None = None) -> dict[int, int]:
        """Current button remappings as {source: target}.

        Unchanged sources are omitted, so an empty result means the pad has no
        remaps configured. Pass `sources` to reuse a list already read.
        """
        if sources is None:
            sources = await self.changekey_sources()
        body = await self.read_body(p.Op.HOST_CHANGEKEY, bytes([0]))
        if body is None:
            raise ControllerError("No remapping reply received")
        if body.declared == 0:
            return {}
        return p.parse_changekey(sources, body.data)

    async def set_remapping(self, changes: dict[int, int]) -> dict[int, int]:
        """Apply button remappings, then read back what the pad reports.

        `changes` maps a source key to its new target; sources not mentioned
        keep their current value. The pad stores the whole record, so every
        source is written, unchanged ones as 0. What comes back is what the pad
        actually holds, which is the only way to tell a write that landed from
        one that was ignored.
        """
        caps = await self.capabilities()
        if not caps.changekey:
            raise NotSupported("this pad does not expose button remapping")

        sources = await self.changekey_sources()
        targets = [changes.get(src, src) for src in sources]
        payload = p.build_changekey_payload(sources, targets)

        for chunk_index, offset in enumerate(
                range(0, len(payload), p.CHANGEKEY_CHUNK_SIZE)):
            chunk = payload[offset:offset + p.CHANGEKEY_CHUNK_SIZE]
            serial = p.save_button_serial(
                slot=chunk_index, counter=self._next_save_counter())
            packet = p.build(
                p.Op.WRITE_CHANGEKEY,
                chunk,
                serial=serial,
                length_field=p.host_length(len(chunk), self._next_host_counter()),
            )
            await self._send(packet, serial)
            await asyncio.sleep(0.25)

        return await self.remappings(sources)

    async def read_macro(self, slot: int, *, strict: bool = False) -> p.MacroProgram | None:
        """Read one macro slot back off the pad.

        The pad ignores READ_MACRO (0xA7) but answers HOST_MACRO (0xB5) with
        the real record when queried as `[position, slot]`. Returns None for an
        empty slot, or when the pad does not answer.
        """
        if not 1 <= slot <= 6:
            raise ValueError("slot must be 1-6")
        index = slot - 1

        first = await self.query(p.Op.HOST_MACRO, bytes([0, index]))
        if first is None or not first.crc_valid or not first.payload:
            if strict:
                raise ControllerError(f"No valid reply from macro slot M{slot}")
            return None
        declared = first.payload[0]
        if declared == 0:
            return None
        data = bytearray(first.payload[1:])

        position = 1
        while len(data) < declared and position < 16:
            more = await self.query(p.Op.HOST_MACRO, bytes([position, index]))
            if more is None or not more.crc_valid:
                break
            data.extend(more.payload)
            position += 1

        if strict and (len(data) < declared or declared < 2 or (declared - 2) % p.MACRO_STEP_SIZE):
            raise ControllerError(f"Incomplete macro record in M{slot}")
        return p.parse_macro_payload(bytes([declared]) + bytes(data))

    async def macros(self) -> dict[int, p.MacroProgram]:
        """Read all macro slots (up to 6), keyed by slot number."""
        out: dict[int, p.MacroProgram] = {}
        caps = await self.capabilities()
        count = len(caps.macro_slots) if caps.macro_slots else 4
        for slot in range(1, count + 1):
            program = await self.read_macro(slot)
            if program is not None:
                out[slot] = program
        return out

    async def clear_macro(self, slot: int) -> None:
        """Empty a macro slot.

        Sends a bare [0], which is what the app emits for an empty step list.
        An empty header is a different thing and doesn't clear.
        """
        if not 1 <= slot <= 6:
            raise ValueError("slot must be 1-6")
        packet = p.build_macro_write(
            p.MACRO_CLEAR, slot=slot - 1, chunk=0, counter=self._next_save_counter()
        )
        serial = p.unscramble(packet)[2]
        await self._send(packet, serial)
