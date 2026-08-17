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
xor-linearity of a genuine CRC instead of an arbitrary substitution box.

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

**"TOOBLE" means turbo.** It's a transliteration that appears throughout.

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
`data` isn't yet known and must come from capture.

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
firmware path isn't even reachable in this mode.

### Device info payload

Decoded per `ZXBTHelper.parsingVidAndPidAndVersion`:

| Bytes | Field | This device |
|---|---|---|
| 0-1 | vendor id, hex string | `0710` |
| 2-3 | product id, hex string | `1320` |
| 4-5 | version, `major.minor` in hex | `9.01` |
| n/a | device_id, top 4 bits of pid | `1` |
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
undocumented. They are preserved verbatim over guessed at.

Crucially, this record **didn't change** when the pad's lighting was switched
from off to brightness 1 and set to green, and no `00 ff 00` appears anywhere in
it. So this is a stored palette, not the active lighting state. The live state is
somewhere else and is still unlocated.

This is a useful negative result: it rules out the obvious reading of
`HOST_LIGHTING` and means writes to it wouldn't have done what we expected.

A third confirmation was later taken with the LEDs fully off at brightness 0:
the record read back byte-identical again. Off, brightness 1, and green all
produce the same 24 bytes, so the palette is inert on this model.

#### The lighting write does fit, and it lands. The LEDs still ignore it.

An earlier version of this document claimed the record could not be written at
all: 24 bytes plus a length prefix plus a five-byte frame is 30, over the
20-byte `MAX_PACKET`. **That was wrong, and it was wrong because nobody read
`writeLightingData` to the end.**

`ZXBTHelper.writeLightingData` walks the payload in **15-byte chunks** with an
incrementing index, and `CodeHelper.writeHostLightingData(data, i)` puts that
index in the serial via `getSaveButtonSerial(toBinary(i, 4))` — byte for byte
the same shape as `writeHostMacroData`. So the 25-byte payload goes out as
15 + 10, producing packets of 20 and 15. Both fit.

Verified on hardware 2026-08-16 against a second X20 (`98:B6:ED:55:D9:22`):

```
2 packet(s): sizes [20, 15]
  sent 20B serial 0x81 -> reply op 0x19
  sent 15B serial 0x0a -> reply op 0x19
RESULT: the pad stored exactly what was written.
```

Both packets acknowledged, and the record read back byte-identical to what was
sent — `00ff00` in all four entries. **The LEDs stayed red.** The pad was later
observed still displaying red with a green record stored, which is the converse
of the earlier observation and completes the proof in both directions.

`build_write(Op.WRITE_LIGHTING, piece, slot=chunk, counter=counter)` reproduces
the app exactly, so this is a demonstrated capability, not a theory. It is
deliberately **not** exposed as a library API, because it does nothing a user
would want: writes to this record are stored and never read by the LED code.

**Why `MAX_PACKET` is 20** is also worth stating correctly. It is not the ATT
MTU. `SERIAL_KEY` has exactly 40 entries and `scramble` offsets into it by 0 or
20, so a packet over 20 bytes indexes off the end of the table — and the
firmware runs the same table in reverse. Negotiating a larger BLE MTU cannot
help; the cap is in the obfuscation, not the transport. The MTU coincidence is
just that.

#### Page mode is not the gate either

`CodeHelper.setHostMode(i)` sends `SET_MODE` with `05 DF AB 00 <a> <b>`, and
`ZXBTHelper` re-asserts it on a repeating handler while a settings page is open.
Mode 8 (`0A 03`) is the host settings menu. Writing lighting while **holding**
mode 8, and again while holding mode 4, changed nothing. See 4i for the table.

**The conclusion, measured rather than inferred:** `HOST_LIGHTING` /
`WRITE_LIGHTING` is a stored palette the firmware never reads on this model.
`caps.lighting = 0x00`, and the app gates its own lighting page on that byte
(`HostMenuBean.hasLighting`), so **KeyLinker cannot change an X20's colour
either**. Colour is reachable only through the on-pad combinations in
`00-findings.md` section 1.

## 4c. HOST_MENU is multiplexed

`0xB0` isn't a single query. Its payload is **two bytes, `[position, kind]`**,
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
| 6 | **macro step data** | `10` + `2a 00` four times, then eight zero bytes |
| 7 | device can-change list | no reply |
| 0, 8-15 | — | no reply |

Kinds 6 and 7 are named by the app itself, in `host/util/SimplifyBleUtils`:

```java
public static void readMacroStepData(int i)      { getHostChangeKeySupport(i, 6); }
public static void readDeviceCanChangeList(int i) { getHostChangeKeySupport(i, 7); }
```

**Kind 6 is macro step data**, and its full record is sixteen bytes — eight
16-bit slots, of which the first four hold **42** and the rest are zero:

```
2a 00 2a 00 2a 00 2a 00 00 00 00 00 00 00 00 00
```

Eight slots with four populated matches `caps.macros = 0x0f` exactly, so this is
one value per present macro slot, and the pad's own answer for **maximum macro
steps is 42**. Note that `MAX_MACRO_ENTRIES` in `protocol.py` says 50 and the
chunk-packing limit derives 47; the firmware's own number is lower than both and
should be treated as authoritative until tested otherwise.

Kind 4 is a 5-byte block repeated twice, matching the two-channel pattern
already seen in the stick and trigger records.

**Reading kind 6 requires the two-byte continuation.** It declares 16 bytes and
only 14 arrive in the first packet. `client.read_body` used to follow
continuations with a bare index byte, which is malformed for `HOST_MENU` and
answers with silence, so the record silently truncated. Continuations must be
`[position, kind]`, keeping the sub-query. Fixed 2026-08-16.

#### The "16-bit pairs" lead was wrong

An earlier note here proposed that recurring pairs across kinds 2, 3, 4 and 5 —
`01 88`, `0d 84`, `0b 82`, `5d 82`, `85 68` — were 16-bit key identifiers. They
are not. They are literal-plus-run-marker pairs in the ordinary `decode_key_list`
RLE, and `85 68` is not a pair at all: `0x85` is a run of five and `0x68` is a
separate literal, 104.

The reason this went unnoticed is a **caller bug**, not a protocol subtlety.
`decode_key_list` expects the record itself, whose first byte is the *key* count.
The macro-layout call site was passing `bytes([body.declared]) + body.data`, and
`body.declared` is the *byte* count. On a real X20 that is 11 against 18, so the
count check raised every time and the caller silently fell back to
`DEFAULT_LAYOUT`. The protocol layer was always correct and its unit tests always
passed, because they feed it the record directly. Only the live path was broken.

Passing `body.data` decodes cleanly, verified against hardware:

| kind | count | decoded |
|---|---|---|
| 2, gamepad all keys | `0x17` = 23 | `1-8, 11-19, 93, 94, 95, 96, 97, 104` |
| 3, changekey support | `0x10` = 16 | `1-8, 13-16, 11, 12, 93, 94` |
| 5, macro support | `0x12` = 18 | `18, 19, 13-16, 1-8, 11, 12, 93, 94` |

Kind 5 reproduces `X20_MACRO_KEYS` exactly, so that constant is now confirmed as
device-reported rather than merely a plausible default.

#### Codes above 19, and where `C` and `T` live

Kind 2 is the pad's own full key universe, and it carries six codes above 19:
**93, 94, 95, 96, 97, 104**.

These are not unknowns. `Key` was recovered from the app's drawable names, which
carry the code and the name together, and `captures/dex_strings_all.txt` holds
the complete table. Every one of the six resolves:

| code | drawable | meaning |
|---|---|---|
| 93 `0x5d` | *(none)* | no drawable exists; a genuine editor pseudo-key |
| 94 `0x5e` | *(none)* | likewise |
| **95 `0x5f`** | `ic_big_code_0x5f_cap` | **`C`, the capture button** |
| **96 `0x60`** | `ic_big_code_0x60_tooble` | **`T`, turbo** |
| 97 `0x61` | `ic_big_code_0x61_ml` | M-left |
| 104 `0x68` | `ic_big_code_0x68_m2` | M2 rear button |

`tooble` is the app's transliteration of turbo throughout, and `T` + `A` is the
turbo combination in the manual, so 96 is `T` on two independent grounds.

The surrounding range is populated too — `0x62 mr`, `0x63 modle`, `0x64 fn`,
`0x65 sl`, `0x66 sr`, `0x67 m1`, `0x69 m3`, `0x6a m4`, `0x6b m5`, `0x6c m6` —
which is the chip vendor's full button vocabulary rather than this pad's. An X20
reports only the six above.

**This closes the macro question with a reason.** 95 and 96 appear in kind 2 but
in neither kind 3 (changekey) nor kind 5 (macro), and kind 4 (turbo) excludes
them too, once that list is decoded correctly. The pad itself excludes `C` and
`T` from being macro keys or remap **sources**, so no encoder change could ever
have made them usable there.

**They are reachable as remap targets.** Absence from the source lists says
nothing about targets: the changekey payload is one target byte per source, and
the format never requires a target to be a source. Writing `A -> T` (96) is
accepted, stored, and read back, and the pad acts on it. Held down with the
remap live, A produces no XInput report at all; with the remap cleared and the
same button held, A reports normally. `A -> C` (95) is accepted and stored on
the same path. So `C` and `T` cannot be configured, but they can be **placed**,
which is the more useful direction anyway.

**93 and 94 are Select and Start.** They were called pseudo-keys here on the
grounds that they have no drawable in `ic_big_code_*`, which is true and turned
out not to mean what it looked like. Codes 9 and 10 own the select/start icons
as legacy vocabulary; this firmware reports 93 and 94 instead, and rejects 9 and
10 everywhere. Measured rather than argued: a one-step macro setting bit 22 made
XInput report `BACK`, and bit 23 made it report `START`. That also explains why
the pad's macro list looked like it was missing two physical buttons — it never
was. Found by [chriss80](https://github.com/chriss80/x20ctl).

An earlier version of this section offered "six unnamed codes against six
physical extras" as a suggestive fit. The arithmetic was closer than it deserved
to be: 93 and 94 are physical after all, and the extras are `C`, `T`, ML and M2.

Opcode `0xB5` also has both a one-byte and a two-byte form in the app
(`getHostToobleData(i)` on `0xB4` versus `getHostToobleData(i, i2)` on `0xB5`),
so its earlier silence has the same explanation.

**Sensor calibration is `SET_MODE` with payload `03 DF AB 10`.** One command,
no parameters, sent by the app's sensor test button and nothing else. The app
labels it "Sensor Calibration" and asks for the pad to be laid flat, which is a
level reference for the gyro; that dialog is instruction to the user, not
something transmitted. Note how close it sits to `05 DF AB 00 02 0E` on the
same opcode, the enter firmware update command; payload is the only thing
telling them apart.

### The full SET_MODE page table

`SET_MODE` (`0x1D`) carries `[argc, 0xDF, subcmd, args...]`. The six-byte
`05 DF AB 00 <a> <b>` form is a **page selector**, and the app re-asserts it on
a repeating handler for as long as a settings screen is open
(`ZXBTHelper` run loop, guarded by `isSet`). From `CodeHelper.setHostMode(i)`:

| i | payload tail | meaning |
|---|---|---|
| 1 | `00 00` | |
| 2 | `06 05` | button test (`ButtonTestActivity` sets `hostPositionType = 2`) |
| 3 | `06 02` | |
| 4 | `0A 02` | normal |
| 5 | `02 06` | |
| 6 | `02 0E` | **ENTER FIRMWARE UPDATE — never send** |
| 7 | `02 0C` | |
| 8 | `0A 03` | host settings menu (`HostActivity.onDeviceMenu`) |

`setMode(i)` overlaps: 0 gives the bare `02 DF AB`, 4 gives `0A 02`, 6 gives
`02 0E`, 7 gives `02 0C`.

Two corrections to earlier notes. `0A 03` is the **host settings menu**, not an
"EQ page mode". And the enter-firmware-update command is not a special case — it
is simply pair 6 of this table, which is exactly what makes it dangerous: it is
one byte away from ordinary page switching.

### The 0xDF values 0x0A-0x0F are a clock, not features

`03 DF AB 0A` through `0F` all answer `09 df aa <value> 90 <b0 b1 b2> 00 5e`,
echoing the value. The three varying bytes are a little-endian 24-bit counter.
Sending one value at 2 s, 8 s and 2 s intervals gives 4.95, 4.97 and 5.11 ms per
unit, so it is a **5 ms uptime counter** — the same timebase as the idle
shutdown record. The pad parses these values and returns generic status; there
is no feature behind any of them. Do not re-sweep this range.

**It does not calibrate the sticks**, despite the obvious reading of the word.
Tested directly: hold the left stick about a fifth of the way over, calibrate,
release, and measure the resting position with the deadzone stripped out. If
the held position had become the new centre, the released stick would read
around 6500 of 32767. It read 768, against 605 before, and the right stick
moved comparably without having been touched at all. Those are settling
deltas, not a new centre.

The effect on the sensor itself is **unverified**, and `READ_3D` (`0x9A`) turns
out not to be the oracle either.

It answers two payloads: `00` returns an eight-byte record, and `C0` returns
`01 04`, which looks like a status. `C1` is silent. The app only ever sends
`C0`, from `b1.U(192)`.

The eight-byte record is invariant. It read `94239b004b838489` while flat,
while held tilted, after calibrating tilted, and after calibrating flat and
confirming with the `+` button on the pad. Something that never changes is
neither live sensor output nor a calibration result, so it is probably an
identifier or a fixed constant.

An earlier version of this section said `READ_3D` answered nothing. That was
wrong and the mistake is worth recording: **the first query on a fresh
connection can time out**, and a single silent reply is not evidence that an
opcode is unsupported. Retry on an established link before concluding
anything.

**Factory reset is `RECOVER`, and the trailing byte is a protocol generation,
not a mode.** `03 DF A9 01` for older pads, `03 DF A9 02` for newer, chosen in
the app from its `is_new_2_ver` flag. An X20 on 9.01 wants `02`, and **ignores
`01` in silence**: no error, no reply, nothing cleared. A reset that seems to do
nothing is the wrong generation rather than a failure. Verified by setting the
idle timeout to never, resetting, and watching it return to the ten-minute
default while macros and remappings emptied.

**The idle shutdown timeout has no command of its own.** It rides in the motor
record, four bytes after the motor levels, as a 32-bit little-endian count of
the same 5 ms ticks macros use. All ones means never power off. The record is
`[declared, L1, L2, R1, R2, t0, t1, t2, t3]` with `declared` = 8, which matches
`HostActivity.Q9` in the phone app assembling exactly that array. A live X20
read back `4c4c0000 c0d40100`: motors at 0x4C of 255, or 30%, and 0x0001D4C0 =
120000 ticks = 600 seconds = ten minutes, which is what the app displayed at the
time.

This is why hunting for a shutdown opcode found nothing across three separate
attempts, including a full BLE capture of the app changing the setting. There
was never a packet to find. It also means anything that writes the motor record
without preserving bytes 4 to 7 silently rewrites the user's sleep setting, and
zero there is not "no timeout" but "sleep immediately".

**Macros can be read back, through `HOST_MACRO` and not through `READ_MACRO`.**
`READ_MACRO` (`0xA7`) answers with zeros or nothing at all, which read like a
pad that simply does not support reading. `HOST_MACRO` (`0xB5`) queried as
`[position, slot]` returns the stored record: the length-prefixed header
followed by five bytes per step, exactly the layout `build_macro_payload`
writes. Verified by round trip on an X20 running 9.01, writing
`A:100/60,B+X:200/50,LT:150/0` to M4 and reading back the same string, chords
and per-step timings intact. A cleared slot answers with a declared length of
zero, so empty is distinguishable from absent. Found by
[chriss80](https://github.com/chriss80/x20ctl).

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
| 7 | EQ | audio equaliser — see below |
| 8 | NFC | bits for pro/left/right pad and NFC 1-4 |
| 9 | sensor | gyro |

The offset mapping is confirmed against the app: `HostActivity` tests
`iArr[8] > 0` to `setHasEq(true)`, and `iArr[8]` is this table's offset 7 once
the length prefix is accounted for.

**EQ really is an audio equaliser**, not a second lighting surface. `HostActivity`
draws it as a bank of `VerticalSeekBar`s and sets `hostPositionType = 8` to enter
the settings-menu mode first. The X20 reports `eq = 0x02`, so the official app
*does* offer this page for this pad — making it the one enabled capability
`x20ctl` does not implement.

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
wrong bit order wouldn't produce four correct answers at once.

### The headline result

**The X20 doesn't expose lighting, turbo, or gyro to this protocol.** Offsets
6, 3 and 9 are all zero.

This isn't a limitation of our client. It's what the pad reports about itself,
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

That is a genuinely useful configuration tool, and it's more than the vendor
offers on the desktop, which is nothing. It isn't RGB control.

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

This confirms the two encodings that couldn't be checked any other way:

- **Length field** `0xE6`: top 3 bits carry the rotating `countHost` value (7),
  low 5 bits carry `len(payload) + 5` (6). Accepted.
- **Serial** `0x81`: `getSaveButtonSerial` packs parity, a 4-bit slot index and a
  3-bit counter into one byte. The acknowledgement **echoed 0x81 exactly**, so the
  packing is right instead of merely self-consistent.

### The two write counters are independent

`getHostLength` uses `countHost`, initialised to 8 and decremented before first
use, so the first write carries 7. `getSaveButtonSerial` uses `countSaveButtons`,
initialised to 0 and incremented before first use, so the first write carries 1.
They are separate fields and must not share a counter.

### `df aa` is an acknowledgement, not device state

The ack payload is `02 df aa`, byte-identical to what `TWO_IN_ONE_STATE` returned
in the earlier sweep. So `df aa` is very likely a generic acknowledgement, and the
earlier table entry treating it as two-in-one device state is probably wrong.
Corrected here over left standing.

### Stick and trigger writes are accepted too

`WRITE_CHANGEKEY` proved the framing. It could not prove that a *settings* write
changes anything, because the value it wrote was the value already there. Stick
and trigger writes, run later against the same pad, settle that.

Three steps in one session, left channel only. Sticks first:

```
baseline    08 08 55 55 aa aa 00  08 08 55 55 aa aa 00

1. no-op    write the record back byte for byte
   ack      19 08 81 .. 02 df aa      RESPONSE, crc ok
   after    08 08 55 55 aa aa 00  08 08 55 55 aa aa 00   unchanged

2. change   left inner deadzone 8 -> 10
   sent     0a 08 55 55 aa aa 00  08 08 55 55 aa aa 00
   after    0a 08 55 55 aa aa 00  08 08 55 55 aa aa 00   accepted

3. restore  baseline written back
   after    08 08 55 55 aa aa 00  08 08 55 55 aa aa 00   as found
```

Then the same procedure against `WRITE_TRIGGER`, on a record that starts out
curved rather than linear:

```
baseline    04 22 52 85 e5 eb 00  04 22 52 85 e5 eb 00

1. no-op    ack 19 08 81 .. 02 df aa, record unchanged
2. change   left inner deadzone 4 -> 6
   after    06 22 52 85 e5 eb 00  04 22 52 85 e5 eb 00   accepted
3. restore  04 22 52 85 e5 eb 00  04 22 52 85 e5 eb 00   as found
```

What this establishes:

- **The pad takes stick and trigger writes**, and the changed byte reads back
  changed in both records. The no-op step alone could never have shown this: an
  ignored write and an accepted one leave identical records behind. Only a value
  that differs distinguishes them.
- **The two records behave identically**, which the shared layout suggested but
  did not establish. The trigger record starts out curved and on a 200-unit
  scale where the stick record is linear on a 100-unit one, and both took the
  write the same way.
- **The record is written whole.** Byte 0 is a length prefix of 14, then two
  seven-byte channels. Sending one channel is not an option; the untouched side
  goes along unchanged, and did, verifiably.
- **A capability read preceded the write** in this run, as it does in the app,
  so this says nothing about whether a handshake is *required*. That question
  stays open.
- **The record is exactly at the packet cap.** 14 bytes of data plus the length
  prefix plus the five-byte frame is 20, which is the whole MTU-derived limit.
  A pad reporting three channels could not be written in one packet, and no
  chunked form of this write has been observed.

`tools/verify_curve_write.py` is this procedure, kept so it can be repeated on
another pad rather than taken on trust.

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
slots read back empty, so the protocol side is a real but unused feature. It's
not simply a second view of the same assignment.

The practical consequence is that **read-back can't verify a rear-button change
made on the pad**, and any plan relying on that's unsound.

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
| Wired USB, DInput | `0079:181C`, 1 collection | **none**, `feature=0` |
| Wired USB, Switch | `057E:2009`, 1 collection | **none**, `feature=0` |
| Bluetooth Classic | `045E:02FD`, BR/EDR HID | **none** |
| BLE, `Xpert2` peripheral | n/a | **yes**, service `d7f010e0` |
| 2.4 GHz dongle | presents the **pad's own** identity per mode | **none** |

All rows enumerated with `tools/hid_scan.py`, which opens devices at zero access
and only reads descriptors.

### DInput and Switch both answered: no vendor collection

Two open questions from `00-findings.md` are settled, and both negative.

**DInput** presents one collection: usage page `0x0001`, usage `0x05` Game Pad,
`input=10 output=5 feature=0`. Generic Desktop, nowhere near the `0xFF00+` vendor
range, and **no feature reports at all**, so there is nothing to probe. The
manufacturer string reads `ZhiXu` — ShenZhen ZhiXu, the KeyLinker publisher, which
corroborates the chip-vendor thesis straight from the USB descriptor.

The 5-byte output report is almost certainly rumble. It is too small to matter for
lighting regardless: four zones of RGB need twelve bytes.

**Switch mode** presents as a full Pro Controller clone: `057E:2009`, usage
`0x04`, `input=64 output=64 feature=0`. Again no feature reports, but the 64-byte
output report is the real Nintendo Switch Pro HID protocol, which is publicly
reverse-engineered and genuinely bidirectional.

Its LED subcommands are `0x30` set player lights and `0x38` set HOME light —
on/off/flash and single-LED brightness respectively. Neither carries colour, so
even if the X20 maps them onto its RGB hardware the ceiling is brightness, not
per-zone colour. **Untested, and the one remaining write channel worth trying.**

Note that the same command space contains `0x11` SPI flash write and `0x12` SPI
sector erase. Those are brick vectors sitting beside the LED commands, so this is
a spare-hardware experiment, not a main-controller one.

### The dongle carries no config channel — retracted, mistaken identity

**Everything below about `1D57:FA60` describes a different device.** Retracted
2026-08-16. `VID_1D57` is Xenta, and the collections attributed to the X20's
receiver are a keyboard, a mouse, a consumer-control page and `col04` — the
signature of a generic wireless **keyboard/mouse** receiver that happens to live
permanently in this machine. It was present during the original `hid_scan` and
was read as if it were the pad's dongle.

**What the X20's own receiver actually does:** it is transparent, presenting the
pad's identity per mode, exactly as the cable does. Verified by mode-switching a
pad connected over the dongle with no cable attached:

| Pad mode, over the dongle | Enumerates as |
|---|---|
| XInput | `045E:028E`, one collection, `feature=0` |
| DInput | `0079:181C`, `ZhiXu` / `EasySMX X20`, `feature=0` |

No feature-report collection appears in any mode, so the dongle rows match the
wired rows: **no config channel**. There is no dongle-side firmware surface for
this project, and the receiver has no mass-storage bootloader either — `L3` while
connecting is a pad gesture, confirmed by the pad's bootloader volume still
appearing with the dongle physically removed.

The original text is kept below so the error is legible rather than silently
deleted. Do not act on it.

The 2.4 GHz receiver is the one transport that does carry a config-shaped channel,
and `--vendor` misses it because the vendor used a low usage page rather than
`0xFF00+`:

```
\\?\hid#vid_1d57&pid_fa60&mi_02&col04
    usage        : page 0x000B usage 0x00
    report bytes : input=0 output=0 feature=64
```

No input and no output reports, only a 64-byte feature report — a collection that
exists purely to carry feature traffic.

A read-only `HidD_GetFeature` sweep answers on ids `0x04` through `0x22` and
`0xA0`, and the payloads are **8051 code**: `90` `MOV DPTR`, `12` `LCALL`, `02`
`LJMP`, `e0` `MOVX A,@DPTR`. Consecutive ids return consecutive bytes, so the
reads advance an internal pointer into code memory.

Two consequences. First, this channel is stateful and firmware-adjacent, so a
blind sweep of all 256 ids is not the harmless read it appears to be — an id that
latches bootloader state cannot be ruled out. Second, and more practically, this
is the **dongle's** firmware, not the pad's. A spare controller does not insure
against damaging it, and the dongle is what play depends on.

Note that link speed is irrelevant to configuration. The dongle's 1000 Hz polling
matters for input latency during play; a configuration write is a single packet
of under 20 bytes. The likely workflow is to configure over BLE and play over the
dongle, since both links are live simultaneously.

**Correction, 2026-08-16: none of this belongs to the X20.** Re-read read-only on
the documented ids: `0x04` returns the UTF-16 product string `2.4G Wireless
Device`, `0x05`-`0x22` return 8051 code, `0xA0` an unidentified blob. There is no
request/response interface — it only streams that device's own code memory, so it
is a firmware read-back surface rather than a config one. And it is an unrelated
wireless keyboard/mouse receiver, as the retraction above explains. Reading it
further would be poking a third party's peripheral for no benefit.

## 4j. The USB bootloader, and why the firmware cannot be dumped

Holding `L3` while connecting USB really does enter upgrade mode: the pad leaves
XInput and BLE and enumerates as a removable disk. Entering and leaving it
**without writing is harmless** — nothing is flashed and unplugging restores a
working controller.

The volume has no filesystem and reports `Size = 0`. A raw `\\.\D:` read fails
with `PermissionError` even elevated, because there are no sectors, not because
of privilege. `INQUIRY` over `IOCTL_SCSI_PASS_THROUGH_DIRECT` answers:

```
vendor "Gamepad "  product "Updater         "  rev "1.00"   (RMB set)
```

Every other standard read answers sense key `0x2` ASC `0x3A` (MEDIUM NOT
PRESENT); `READ BUFFER` answers `0x5` ASC `0x24`, so it is unimplemented.

### The vendor command set

`DeviceUsb.dll` exports `deviceUsb_Request`, which disassembles to a plain
`IOCTL_SCSI_PASS_THROUGH_DIRECT` wrapper hardcoding no opcode and no direction:

```c
int deviceUsb_Request(HANDLE h, BYTE dataIn, void *cdb, BYTE cdbLen,
                      void *dataBuf, DWORD dataLen);
```

The updater calls it from exactly four sites:

| VA | dataIn | data | meaning |
|---|---|---|---|
| `0x00402644` | 1 read | 20 B | vendor query, opcode `0xF1` |
| `0x00402872` | 0 write | 512 B | firmware chunk loop |
| `0x00402897` | 1 read | 20 B | status after each chunk |
| `0x004028de` | 1 read | 20 B | final status |

**Every read is 20 bytes. There is no bulk read.** The firmware cannot be dumped,
so no backup can be taken, so any write is unrecoverable. That is the reason
flashing stays out of scope — not caution, but the vendor's own command table.

`0xF1` is safe and useful: CDB `f1 00 00 00 00 00 00 00 00 00`, `dataIn = 1`, 20
bytes. It returns **the same 18-byte GUID as `HOST_GUID` (0x93) over BLE**, plus
two zero pad bytes, verified on one unit across both transports. That makes it a
cross-transport identity oracle — useful for proving which physical pad a
bootloader volume belongs to.

**Trap when disassembling the updater:** the `GetProcAddress` block at
`0x004021fa` stores each result one call late, so the naive mapping is off by
one. The real pointers are `0x5f9870` OpenByDrv, `0x5f986c` Close, `0x5f9868`
GetInfo, `0x5f9864` **Request**. Reading it wrong leads to a two-argument
identity check at `0x00402d52` that looks like the command path and is not.

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
`MacroStep.released()` exists instead of callers writing `mask=0`.

### Practical notes

- **Read-back can't verify a macro.** `READ_MACRO` returns zeros before, during
  and after. Only observed behaviour confirms a write.
- **Only one macro runs at a time.** Triggering another interrupts the first,
  which is the recovery route for a runaway loop.
- **50 ms is a very short press.** Some games poll input and may miss it; 80 to
  120 ms is closer to a deliberate human press.
- **Clearing a slot** sends a bare `[0]`, not an empty header.

## 4i. Do settings survive a power cycle?

Open question, and an important one for any profile system.

Vibration was written to 0% and confirmed by feel. The pad was then powered off
overnight. On the next session it read back **50%** (`0x80`), which is neither the
written value nor the factory default of `0x4c` (30%) observed at the very first
read.

Three candidate explanations, untested:

1. Settings are volatile and revert to a power-on default of `0x80`.
2. Something else reset the pad between sessions.
3. There is a separate commit or save step, and writes without it are transient.

If (1) or (3), a profile would need reapplying every time the pad powers on, and
the software should either detect that or offer to reapply on connect. Worth
resolving before building anything that assumes persistence.

Suggested test: write a distinctive value such as 90%, read it back, power cycle
the pad, and read again.

## 4j. What the macro format cannot express

The sections above cover what the format stores. This one covers what it can't,
which is less obvious from the encoding.

These were measured by running macros against a real game and reading its own
instrumentation, not by reading bytes. Rocket League was convenient because
there is a community training plugin for it that reports the angle of a dodge in
degrees and the gap between two stick positions in milliseconds, so a macro's
actual effect could be measured instead of guessed at.

### The limits

**Stick angle.** A stick is stored as one of eight compass headings. There is no
ninth. About forty logged attempts, covering every combination of steering, air
roll, timing and mirroring I could build, gave a dodge angle of either exactly 0
degrees or exactly 45. Nothing in between showed up once. Anything that needs 30
degrees can't be done, and no amount of timing work gets around it.

**No magnitude.** Every direction goes out at full deflection. Measured: a
diagonal reads `x = 1.0000, y = 1.0000`, so the axes are driven into the corner
rather than clamped to a circle. The format can't say "half right". A deadzone
setting in the receiving program can't be tuned to land between the two answers
a macro can give, because one of them is already at the maximum.

**Steps snap to 5 ms.** Finer than a 60 Hz frame, and never the limiting factor.

### Flicking a direction on and off gives partial magnitude, sometimes

Alternating a direction on and off every 5 ms produces a partial input if the
receiving program averages over time. Tested by steering a car: a 50% duty cycle
turned about half as far as holding the direction, and 25% turned less again, in
that order. So a controller with no analog output can still produce proportional
behaviour, paid for in time.

It doesn't work where the program reads the stick once at a single moment. The
same flick pattern across a dodge, which samples the stick at the button press,
gave 4 degrees instead of the 22 the duty cycle implied. It just caught whichever
phase happened to be live at that instant.

So: this works for anything integrated over time, and not at all for anything
sampled at a point. Worth knowing for any device driven this way.

### What the format does well

Listed because I expected the opposite:

- **Held buttons survive across steps.** Every step ends with an all-release
  entry of zero duration, which looks like it should break holds. It doesn't,
  because those releases never reach the host. Confirmed twice, once with the
  input tester and once in game by an unbroken boost trail across five step
  boundaries.
- **Chords, sequences and per-step timing** are exact and repeatable to the
  millisecond. Two runs of the same macro gave identical instrument readings.
- **25 steps** is plenty for anything written by hand.

The weakness is spatial rather than temporal. It can say when precisely, and
what only roughly.

## 5. What is still unknown

- Byte layout **inside** the payloads. The opcodes are certain; the field meanings
  aren't. Colour ordering, brightness scale, effect enumeration and gyro flags
  all need capture to confirm. Deadzones and curve control points are decoded,
  and writes to both the stick and trigger records are confirmed accepted on
  hardware.
- **How the firmware joins the two curve control points.** The points themselves
  are certain, and both are stored on a 0-255 axis independent of the deadzone
  scale. Nothing observed so far says whether the response between them is a
  bezier, a spline, or piecewise linear. Anything drawing that curve is guessing
  at the shape between two known values.
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
