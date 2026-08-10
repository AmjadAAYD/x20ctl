# The KeyLinker protocol

Recovered by decompiling `com.pulsenet.inputset` version 3.x with jadx. The app is
**not obfuscated**: class, method and constant names are intact.

Primary source: `com/pulsenet/inputset/util/CodeHelper.java` (packet construction),
`com/pulsenet/inputset/config/AgreementConfig.java` (transport UUIDs),
`com/pulsenet/inputset/util/BlueToothHelper.java` (GATT plumbing).

Everything below is read from source. **Nothing has been sent to hardware yet**, so
treat field meanings within payloads as unverified until confirmed by capture.

---

## 1. Transport

Bluetooth LE GATT.

| Role | UUID |
|---|---|
| Configuration service | `d7f010e0-660d-46e9-96c3-19c4148bdab5` |
| Write characteristic | `d7f010e1-660d-46e9-96c3-19c4148bdab5` |
| Read / notify characteristic | `d7f010e2-660d-46e9-96c3-19c4148bdab5` |
| CCCD (standard) | `00002902-0000-1000-8000-00805f9b34fb` |
| Battery service (standard) | `0000180f-...` / level `00002a19-...` |

**OTA service, never touch:** `2de516f0-50f5-45d5-a1b2-c3565de543ae`, with
`2de516f1..f4` as its characteristics. This is the firmware path.

There is a second, unrelated pair used elsewhere in the app,
`00010203-0405-0607-0809-0a0b0c0d1912` / `...2b12`. That is a stock Android BLE
sample UUID and appears to belong to a different device family. The `d7f010e0`
family is the one guarded by `AgreementConfig`.

### Device identification

MAC address prefixes, from `AgreementConfig`:

| Prefix | Meaning |
|---|---|
| `98:B6:E9:` | standard gamepad |
| `98:B6:ED:` | Switch-mode device |
| `98:B6:EC:` | two-in-one device |

This is a far better fingerprint than VID/PID, which is a cloned `045E:028E`.

---

## 2. Packet format

All packets, both directions:

```
byte 0        opcode
byte 1        length field
byte 2        serial
byte 3        random nonce
bytes 4..n-2  payload
byte n-1      CRC-8 over bytes 0..n-2
```

The whole packet is then passed through `encrypt()` before being written to the
characteristic, and incoming packets through `decode()`.

### Length field

Three variants, all producing byte 1:

- `getLength(payload)`: `len(payload) + 5`, or `5` when the payload is empty.
- `getHostLength(payload)`: top 3 bits are a rotating counter `countHost`, which
  cycles 7,6,5,...,0 and reloads at 8; bottom 5 bits are `len(payload) + 5`.
- `getHostMacroLength(n, payload)`: top 3 bits are the caller-supplied `n`,
  bottom 5 bits `len(payload) + 5`.

So on `host` commands byte 1 is **not** a plain length. Mask with `0x1F` to read
the length and `>> 5` for the counter.

### Serial

- `getSerial()`: increments a counter; even values return `(n % 128) + 128`, odd
  values return `n % 128`. The high bit therefore alternates each packet.
- `getSaveButtonSerial(bits)`: `parity_bit ++ bits ++ counter[0:3]` assembled as a
  binary string and parsed as an integer. Used by every `writeHost*Data` call,
  where `bits` is a 4-bit profile/slot index.

### CRC

```java
int crc = 0;
for (int b : bytes) crc = CRC_ARRAY[crc ^ b];
```

A 256-entry table-driven CRC-8 in the standard `crc = table[crc ^ byte]` form.

The shipped table is **reflected (LSB-first) CRC-8, polynomial `0xEB`, init `0x00`,
no final xor**, equivalent to normal-form polynomial `0xD7`. Confirmed two ways:
generating the table from that polynomial reproduces all 256 entries exactly, and
the table satisfies `T[a ^ b] == T[a] ^ T[b]` for every pair, which is the defining
xor-linearity of a genuine CRC rather than an arbitrary substitution box.

`x20ctl/protocol.py` generates the table and asserts equality against the literal
one from the app, so the claim is checked on every test run.

### Obfuscation layer

`encrypt()`, applied after the CRC is appended:

```
out[0] = in[0] ^ ((in[2] + in[3]) - 154)
out[1] = in[1] ^ ((in[2] + in[3]) + 155)
out[2] = (in[2] - 173) ^ in[3]
out[3] = (in[3] + 191) ^ 219
out[i] = in[i] ^ ((in[2] + in[3]) - SEND_DATA_ENCRYPT[i-4])   for 4 <= i < len-1
out[len-1] = in[len-1]                                        CRC byte passes through
then, for all i:
out[i] ^= SERIAL_ENCRYPT[offset + i]   where offset = (out[3] & 2) * 10
```

`decode()` is the exact inverse, deriving the same offset as
`((in[3] ^ SERIAL_ENCRYPT[3]) & 2) != 0 ? 20 : 0`.

Two fixed keystreams:

```
SEND_DATA_ENCRYPT = [51, 99, 157, 121, 242, 219, 162, 26, 170, 33, 139, 232, 116, 211, 88]
SERIAL_ENCRYPT    = [161, 82, 213, 163, 245, 137, 246, 143, 240, 157, 72, 147, 234, 52,
                     49, 186, 195, 77, 198, 235, 73, 96, 216, 163, 218, 42, 83, 141,
                     244, 97, 24, 191, 174, 215, 111, 81, 228, 160, 217, 146]
```

This is scrambling, not cryptography. There is no key exchange and no
authentication, so packets can be constructed offline.

> Note: `SEND_DATA_ENCRYPT` has 15 entries, which caps the useful payload at 15
> bytes past the header. This matches `ComposeByte.compse20Bytes`, which chunks
> button data into 15-byte groups.

---

## 3. Opcodes

From `CodeHelper` constants. Names are the app's own.

### Query, device to host reads

| Dec | Hex | Constant | Meaning |
|---|---|---|---|
| 144 | `0x90` | `CODE_READ_NAME` | device name |
| 145 | `0x91` | `CODE_READ_VID_PID_VERSION` | VID, PID, firmware version |
| 147 | `0x93` | `CODE_HOST_GUID` | unclear, possibly gyro |
| 151 | `0x97` | `CODE_READ_BUTTON_MODE` | button mode |
| 152 | `0x98` | `CODE_LOAD_BUTTON` | load button config |
| 154 | `0x9A` | `CODE_READ_3D` | 3D / motion data |
| 164 | `0xA4` | `READ_PRESS_GUN` | recoil macro |
| 165 | `0xA5` | `READ_TOOBLE` | **turbo** settings |
| 166 | `0xA6` | `READ_SLIDE_SCREEN` | slide screen |
| 167 | `0xA7` | `READ_MACRO` | macros |
| 168 | `0xA8` | `CODE_TWO_IN_ONE_STATE` | two-in-one state |
| 176 | `0xB0` | `CODE_HOST_MENU` | menu / capability query |
| 177 | `0xB1` | `CODE_HOST_ROCK` | **stick settings** |
| 178 | `0xB2` | `CODE_HOST_TRIGGER` | **trigger settings** |
| 179 | `0xB3` | `CODE_HOST_MOTOR` | **vibration** |
| 180 | `0xB4` | `CODE_HOST_TOOBLE` | **turbo** |
| 181 | `0xB5` | `CODE_HOST_MACRO` | macros |
| 182 | `0xB6` | `CODE_HOST_CHANGEKEY_NEW` | **button remapping** |
| 183 | `0xB7` | `CODE_HOST_LIGHTING` | **RGB lighting** |

### Write

| Dec | Hex | Constant | Meaning |
|---|---|---|---|
| 49 | `0x31` | `CODE_HOST_WRITE_ROCK` | write stick settings |
| 50 | `0x32` | `CODE_HOST_WRITE_TRIGGER` | write trigger settings |
| 51 | `0x33` | `CODE_HOST_WRITE_MOTOR` | write vibration |
| 52 | `0x34` | `CODE_HOST_WRITE_TOOBLE` | write turbo |
| 53 | `0x35` | `CODE_HOST_WRITE_MACRO` | write macro |
| 54 | `0x36` | `CODE_HOST_WRITE_CHANGEKEY` | write remapping |
| 55 | `0x37` | `CODE_HOST_WHITE_LIGHTING` | **write RGB lighting** |

`CODE_HOST_WHITE_LIGHTING` is a misspelling of WRITE in the original source, not a
white-LED command.

### Control

| Dec | Hex | Constant |
|---|---|---|
| 22 | `0x16` | `CODE_RECOVER` |
| 23 | `0x17` | `CODE_WRITE_BUTTON_MODE` |
| 24 | `0x18` | `CODE_SAVE_BUTTON` |
| 25 | `0x19` | `CODE_RESPONSE` |
| 26 | `0x1A` | `CODE_WITER_3D` |
| 27 | `0x1B` | `CODE_TEST_SET_MODE` |
| 28 | `0x1C` | `CODE_TEST_NORMAL_MODE` |
| 29 | `0x1D` | `CODE_SET_MODE` |
| 30 | `0x1E` | `CODE_NORMAL_MODE` |
| 31 | `0x1F` | `CODE_LOCATION` |
| 32 | `0x20` | `CODE_FLOAT` |
| 36 | `0x24` | `CODE_PRESS_GUN_MODE` |
| 37 | `0x25` | `CODE_TOOBLE_MODE` |
| 38 | `0x26` | `CODE_SLIDE_SCREEN_MODE` |
| 39 | `0x27` | `CODE_WITER_MACRO` |
| 41 | `0x29` | `CODE_CAST_CALIBRATION_XY` |

**"TOOBLE" means turbo.** It is a transliteration that appears throughout.

---

## 4. Command shapes

### Query, e.g. lighting

```java
getHostLighting(int i)
  -> [183, getLength([i]), getSerial(), getRandom(), i, CRC]
```

Every `getHost*` query has this identical shape with only the opcode changing.
The single payload byte `i` is a slot or profile index.

### Write, e.g. lighting

```java
writeHostLightingData(int[] data, int slot)
  -> [55, getHostLength(data), getSaveButtonSerial(toBinary(slot,4)),
      getRandom(), ...data, CRC]
```

Every `writeHost*Data` is identical apart from the opcode. Payload layout inside
`data` is not yet known and must come from capture.

### Factory reset

```java
getRecover()     -> [22, getLength([3,223,169,1]), serial, random, 3, 223, 169, 1, CRC]
getRecoverHost() -> [22, ..., 3, 223, 169, 2, CRC]
```

Payload `0x03 0xDF 0xA9` is a magic guard, with a trailing `1` for the device and
`2` for the host. Useful as a known-good restore, and a reminder that the guard
exists precisely because this command is destructive to settings.

---

## 4a. Confirmed against hardware

First live exchange with an EasySMX X20, BLE `98:B6:ED:E3:15:C4`.

Request, `READ_VID_PID_VERSION`:

```
plain   91 05 01 43 be
wire    9a 88 c2 7a 4b        written to d7f010e1
```

Reply, after 2.2 s on the notify characteristic:

```
wire    26 8c b6 8e f7 4c 7e 10 bf c1 fd df 62
plain   19 0d 01 37 07 10 13 20 09 01 23 52 88
```

| Field | Value | Note |
|---|---|---|
| opcode | `0x19` | `CODE_RESPONSE`, generic for all replies |
| length | `0x0D` | 13 = 8 payload + 5 |
| serial | `0x01` | **echoes the request serial** |
| nonce | `0x37` | fresh, not echoed |
| CRC | valid | |

**Replies are correlated to requests by the serial byte**, which makes a
request/response client straightforward: send, then match the reply on serial.

### GATT layout as observed

```
d7f010e0-660d-46e9-96c3-19c4148bdab5     configuration service
    d7f010e1  [write]
    d7f010e2  [notify]  + CCCD 00002902
0000ff12-0000-1000-8000-00805f9b34fb     secondary service, purpose unknown
    0000ff13  [write]
    0000ff14  [notify]  + CCCD
    0000ff15  [read, write-without-response]
```

The OTA service `2de516f0-...` was **not present** on this peripheral, so the
firmware path is not even reachable in this mode.

### Device info payload

Decoded per `ZXBTHelper.parsingVidAndPidAndVersion`:

| Bytes | Field | This device |
|---|---|---|
| 0-1 | vendor id, hex string | `0710` |
| 2-3 | product id, hex string | `1320` |
| 4-5 | version, `major.minor` in hex | `9.01` |
| — | device_id, top 4 bits of pid | `1` |
| 6 | bitfield: model (low 4), sensor (bit 4), family (bits 5-7) | **only parsed when the payload is exactly 7 bytes** |

These are the vendor's own identifiers and are unrelated to the cloned USB
`045E:*` descriptors. With an 8-byte payload the app sets `is_new_2_ver = 0` and
skips the bitfield entirely, so bytes 6 and 7 (`23 52` here) remain unexplained.

## 4b. Response payload structure

**Byte 0 of every response payload is a length prefix.** Proven by `READ_NAME`,
which returns `06` followed by exactly six ASCII bytes, `Xpert2`. If the declared
length exceeds what arrived, the record is chunked and the remainder is fetched by
re-querying with the next index.

The index byte on `getHost*` queries is therefore a **chunk number**, not a zone.
`HOST_LIGHTING` answers on index 0 and 1 and is silent from 2 upward, because its
record is 25 bytes and a single BLE packet carries at most 20.

### Live readings from an X20, firmware 9.01

| Opcode | Raw payload | Structure |
|---|---|---|
| `READ_NAME` | `06 58 70 65 72 74 32` | length 6, ASCII `Xpert2` |
| `HOST_STICK` | `0e` + `08 08 55 55 aa aa 00` ×2 | two 7-byte channels, left then right |
| `HOST_TRIGGER` | `0e` + `04 22 52 85 e5 eb 00` ×2 | two 7-byte channels |
| `HOST_MOTOR` | `08` + `4c 4c 00 00 c0 d4 01 00` | `4c 4c` = two motor strengths (76) |
| `HOST_TURBO` | `0d` + `08 00 …  58 02 58 02` | `0x0258` = 600, twice; interval in ms |
| `HOST_CHANGEKEY` | `00` | length 0, no remaps configured |
| `HOST_GUID` | `12` + 14 bytes | declares 18, chunked; a device GUID, **not gyro** |
| `READ_BUTTON_MODE` | `03 01 00 00` | |
| `TWO_IN_ONE_STATE` | `02 df aa` | |
| `HOST_MENU`, `HOST_MACRO`, `READ_TURBO` | no reply | not supported on this model, or need a handshake |

The stick and trigger records are two identical halves at factory defaults, which
is what you would expect from symmetric per-side settings. In the stick record
`0x55` and `0xAA` are precisely one third and two thirds of 255, consistent with
response-curve control points.

### The lighting record

Chunks 0 and 1 concatenate to 25 bytes. `0x18` is 24, which is
**four six-byte entries**, not eight RGB triplets. The entry layout comes from
`ZXBTHelper.writeLightingData`, which appends `r, g, b, data4, data5, light` per
entry and prefixes the run with `entry_count * 6`:

```
18 | ff 00 00 00 00 ff | 00 00 ff 10 10 ff | 00 00 ff 40 00 40 | 80 80 80 a0 80 ff
```

| # | r | g | b | data4 | data5 | light |
|---|---|---|---|---|---|---|
| 1 | `ff` | `00` | `00` | `00` | `00` | `ff` |
| 2 | `00` | `00` | `ff` | `10` | `10` | `ff` |
| 3 | `00` | `00` | `ff` | `40` | `00` | `40` |
| 4 | `80` | `80` | `80` | `a0` | `80` | `ff` |

`data4` and `data5` are the app's own field names and their meaning is genuinely
undocumented. They are preserved verbatim rather than guessed at.

Crucially, this record **did not change** when the pad's lighting was switched
from off to brightness 1 and set to green, and no `00 ff 00` appears anywhere in
it. So this is a stored palette, not the active lighting state. The live state is
somewhere else and is still unlocated.

This is a useful negative result: it rules out the obvious reading of
`HOST_LIGHTING` and means writes to it would not have done what we expected.

## 4c. HOST_MENU is multiplexed

`0xB0` is not a single query. Its payload is **two bytes, `[position, kind]`**,
where `position` is the chunk index and `kind` selects a sub-query. This was
missed initially, and sending a single byte produced silence that looked like an
unsupported opcode. It was a malformed request.

Kinds, from the app's call sites (`getHostChangeKeySupport(pos, 3)`,
`getHostToobleSupport(pos, 4)`, `getHostMacroSupport(pos, 5)`, and so on):

| kind | sub-query | reply from an X20 at `[0, kind]` |
|---|---|---|
| 1 | menu | `0a` + `03 03 03 00 0f 01 00 02 c0 00` |
| 2 | gamepad all keys | `08` + `17 01 88 0b 89 5d 85 68` |
| 3 | changekey support | `09` + `10 01 88 0d 84 0b 82 5d 82` |
| 4 | turbo support | `0a` + `0c 01 88 0d 84` repeated twice |
| 5 | macro support | `0b` + `12 12 13 0d 84 01 88 0b 82 5d 82` |
| 6 | changekey variant | `10` + `2a 00` four times, then six zero bytes |
| 7 | changekey variant | no reply |

Two observations, offered as leads rather than conclusions. Kind 4 is a 5-byte
block repeated twice, matching the two-channel pattern already seen in the stick
and trigger records. Kind 6 repeats `2a 00` exactly four times, which is
suggestive on a pad with four rear buttons, though nothing yet confirms that.

Recurring 16-bit-looking pairs appear across kinds 2, 3, 4 and 5: `01 88`,
`0d 84`, `0b 82`, `5d 82`, `85 68`. These are plausibly key identifiers, since
they cluster in the changekey and macro sub-queries.

Opcode `0xB5` also has both a one-byte and a two-byte form in the app
(`getHostToobleData(i)` on `0xB4` versus `getHostToobleData(i, i2)` on `0xB5`),
so its earlier silence has the same explanation.

## 4d. The capability descriptor, and what the X20 actually exposes

`HOST_MENU` kind 1 returns the record that drives the entire app UI.
`HostActivity.initMenu` switches each settings page on or off from a single byte,
so this record is authoritative about what a given pad will accept.

Byte offsets below are into the payload **after** the length prefix, so offset 0
here is the app's `iArr[1]`.

| Offset | Capability | Bit meaning |
|---|---|---|
| 0 | sticks | bit 1 left, bit 0 right |
| 1 | triggers | bit 1 left, bit 0 right |
| 2 | motors | bits 0-3, motors 1 to 4 |
| 3 | turbo | non-zero enables the page |
| 4 | macros | bits 0-7: M1-M6, ML, MR |
| 5 | button remapping | non-zero enables the page |
| 6 | lighting | one bit per lighting zone |
| 7 | EQ | audio equaliser |
| 8 | NFC | bits for pro/left/right pad and NFC 1-4 |
| 9 | sensor | gyro |

### Reading from an EasySMX X20, firmware 9.01

Record: `0a | 03 03 03 00 0f 01 00 02 c0 00`

```
configurable: sticks, triggers, vibration, macros, button remapping, eq, nfc
  sticks     L=True R=True
  triggers   L=True R=True
  motors     2
  macros     M1, M2, M3, M4
  lighting   0 zone(s)
```

The decode cross-checks against the physical hardware on four independent
counts: two hall sticks, two triggers, two rumble motors, four rear buttons. A
wrong bit order would not produce four correct answers at once.

### The headline result

**The X20 does not expose lighting, turbo, or gyro to this protocol.** Offsets
6, 3 and 9 are all zero.

This is not a limitation of our client. It is what the pad reports about itself,
and the official app honours it by never showing those pages for this model.
Those three features exist in the hardware and are driven entirely by on-pad
button combinations, which is exactly how the manual documents them:
`C + L3` for brightness, `T + A` for turbo, `C + BACK + L3/R3` for gyro mapping.

It also retroactively explains the `HOST_LIGHTING` result. That opcode answers,
and returns a well-formed four-entry palette, but the record is inert on this
model. Nothing we could have written to it would have changed the LEDs. The
earlier negative result was correct, and this is why.

### What is buildable on an X20

- Button remapping, which the pad reports as supported and currently has unset
- Macros on M1 through M4
- Stick response curves, per side
- Trigger response curves, per side
- Vibration strength, two motors

That is a genuinely useful configuration tool, and it is more than the vendor
offers on the desktop, which is nothing. It is not RGB control.

## 4e. Writes are accepted

The first write to the hardware was deliberately chosen to be a no-op: the
changekey write the official app emits when the user changed nothing.

```java
CodeHelper.writeHostChangeKeyData(new int[]{0}, 0)
```

A count of zero means no remaps, and the pad already held zero remaps.

```
plain     36 e6 81 5a 00 ae
wire      3e f0 56 61 72 84
ack       19 08 81 .. 02 df aa      RESPONSE, serial echoed, crc ok

before    HOST_CHANGEKEY -> 00
after     HOST_CHANGEKEY -> 00
```

This confirms the two encodings that could not be checked any other way:

- **Length field** `0xE6`: top 3 bits carry the rotating `countHost` value (7),
  low 5 bits carry `len(payload) + 5` (6). Accepted.
- **Serial** `0x81`: `getSaveButtonSerial` packs parity, a 4-bit slot index and a
  3-bit counter into one byte. The acknowledgement **echoed 0x81 exactly**, so the
  packing is right rather than merely self-consistent.

### The two write counters are independent

`getHostLength` uses `countHost`, initialised to 8 and decremented before first
use, so the first write carries 7. `getSaveButtonSerial` uses `countSaveButtons`,
initialised to 0 and incremented before first use, so the first write carries 1.
They are separate fields and must not share a counter.

### `df aa` is an acknowledgement, not device state

The ack payload is `02 df aa`, byte-identical to what `TWO_IN_ONE_STATE` returned
in the earlier sweep. So `df aa` is very likely a generic acknowledgement, and the
earlier table entry treating it as two-in-one device state is probably wrong.
Corrected here rather than left standing.

## 4f. On-pad assignments are invisible to the protocol

An experiment worth recording, because it invalidates an obvious assumption.

A rear button was assigned to `A` using the pad's own programming combo
(`hold C + M4`, press `A`, `C + M4` again). An independent controller test page
confirmed the button then reports as `A`.

Every readable protocol value was then re-queried: `HOST_STICK`, `HOST_TRIGGER`,
`HOST_MOTOR`, `HOST_TURBO`, `HOST_CHANGEKEY`, `READ_BUTTON_MODE`,
`TWO_IN_ONE_STATE`, `HOST_LIGHTING`, all six `HOST_MENU` kinds, and `READ_MACRO`
in one- and two-byte forms across four slots.

**Not a single byte changed.**

So the pad keeps two separate stores:

| Store | Set by | Visible to protocol |
|---|---|---|
| On-pad button assignment | pad combos | **no** |
| Protocol macro slots M1-M4 | KeyLinker | yes, and currently empty |

The capability record reports macros as supported (`0f`, M1-M4) and the macro
slots read back empty, so the protocol side is a real but unused feature. It is
not simply a second view of the same assignment.

The practical consequence is that **read-back cannot verify a rear-button change
made on the pad**, and any plan relying on that is unsound.

### Where the protocol's unique value actually lies

This shifts the priority. Rear-button assignment already has an on-pad route, so
software adds convenience there at best. Two settings have **no on-pad route at
all**, are capability-supported, and return real non-default-looking data:

| Setting | Current value | Note |
|---|---|---|
| Stick curves | `08 08 55 55 aa aa 00` per side | `0x55`/`0xAA` at ⅓ and ⅔ of range |
| Trigger curves | `04 22 52 85 e5 eb 00` per side | ascending control points |
| Vibration | `4c 4c` = 76, 76 | two motors |

These are the settings a desktop tool could offer that nothing else can.

## 4g. Transports tested so far

| Transport | Gamepad interface | Config channel |
|---|---|---|
| Wired USB, XInput | `045E:028E`, 2 interfaces | **none** |
| Bluetooth Classic | `045E:02FD`, BR/EDR HID | **none** |
| BLE, `Xpert2` peripheral | n/a | **yes**, service `d7f010e0` |
| 2.4 GHz dongle | not yet tested | not yet tested |

The dongle remains untested. Expectation is that it presents as another plain
HID gamepad with no vendor collection, matching wired and Bluetooth Classic, but
it is a distinct USB device with its own VID/PID and is worth enumerating.

Note that link speed is irrelevant to configuration. The dongle's 1000 Hz polling
matters for input latency during play; a configuration write is a single packet
of under 20 bytes. The likely workflow is to configure over BLE and play over the
dongle, since both links are live simultaneously.

## 4h. Macros, confirmed working

Verified on hardware: a single press of M1 produces exactly one A, with no
repetition and no stick movement.

### Payload

```
header   [ (steps*5)+2 , interval_lo , packed ]
step     [ mask_lo, mask_mid, mask_hi, dur_lo, dur_hi ]   x N
```

Working example, one A tap on M1:

```
0c 00 00 | 88 10 00 0a 00 | 88 00 00 0a 00
   ^loop     ^A, 50ms         ^released, 50ms
```

### The loop field

The header's 12-bit value is a **loop interval**, not a total duration. The app
names its parameter list `pos_loopTime_trigger` and binds the field to a checkbox
as `loop_checkbox.setChecked(loopTime > 0)`, with the slider labelled "loop
interval time".

| Value | Behaviour |
|---|---|
| `0` | loop disabled, fires once |
| `> 0` | repeats forever with this interval |

There is no "run for N seconds" setting. A bounded burst must be expressed as
explicit repeated steps.

### The analog trap

Each of the two analog entries occupies four bits at the bottom of the mask, and
an untouched analog input encodes as `0b1000`, not `0b0000`. The top bit means
"no input"; the low three bits are a direction minus one.

A mask of zero therefore commands **direction 0 on both sticks**. Observed on
hardware: both sticks swept continuously until the macro was interrupted. Every
mask must start from `0x88`, including release steps, which is why
`MacroStep.released()` exists rather than callers writing `mask=0`.

### Practical notes

- **Read-back cannot verify a macro.** `READ_MACRO` returns zeros before, during
  and after. Only observed behaviour confirms a write.
- **Only one macro runs at a time.** Triggering another interrupts the first,
  which is the recovery route for a runaway loop.
- **50 ms is a very short press.** Some games poll input and may miss it; 80 to
  120 ms is closer to a deliberate human press.
- **Clearing a slot** sends a bare `[0]`, not an empty header.

## 5. What is still unknown

- Byte layout **inside** the payloads. The opcodes are certain; the field meanings
  are not. Colour ordering, brightness scale, effect enumeration, gyro flags and
  deadzone encoding all need capture to confirm.
- Whether `CODE_HOST_GUID` (147) is gyro or a device identifier.
- Whether the pad requires a `CODE_HOST_MENU` capability handshake before it will
  accept writes.
- Whether settings are committed to flash immediately or need an explicit save.
- Whether the same protocol is reachable over USB HID in DInput or Switch mode, or
  BLE only.

## 6. Method for resolving the unknowns

For each setting, in order:

1. Send the **query** opcode and record the reply. Queries are read-only and safe.
2. Change that one setting in the official app, query again, diff the two replies.
3. Only then construct a write, and only for the byte that changed.

This keeps every write traceable to an observed difference, per rule 4 of the
safety contract.
