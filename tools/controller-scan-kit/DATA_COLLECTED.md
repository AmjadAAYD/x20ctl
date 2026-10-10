# Data collected by the standalone kit

| Data | Collected? | Reason / limits |
|---|---|---|
| Claimed controller model and optional revision | Yes | Volunteer-provided; no automatic model/support verdict |
| Windows OS/version/architecture, collector version | Yes | Interpret platform-dependent results |
| Before/after Windows USB/HID inventory | Transiently | Identify newly connected device; unrelated paths/IDs are not exported |
| Selected VID/PID, device/product/manufacturer name | Yes | Identify selected interfaces |
| Selected class, driver service, hardware/compatible IDs, HID caps | Yes | Understand available interfaces and report lengths |
| Standard USB device/configuration descriptors | When available | Interfaces/endpoints, descriptor bytes; serial string index can be present but no serial-string request |
| XInput buttons, sticks, triggers, packet/slot values | Optional | Correlated slot; host samples, not controller latency/polling-rate measurement |
| Raw HID input bytes | Explicit opt-in | Only gameplay collections, never keyboard/mouse; bytes may contain identifiers |
| Nearby BLE devices | Optional, transiently | Off/on discovery for correlation; only selected target exported |
| Selected BLE name, UUIDs, RSSI, payload-presence flags | Optional | Raw manufacturer/service advertisement bytes are omitted |
| Selected GATT service/characteristic/descriptor UUIDs and properties | Optional | Service inventory; descriptor values and vendor characteristics are not read |
| Standard battery/model/revision/manufacturer GATT values | Optional | Allowlisted reads; serial characteristic `2A25` excluded |
| Serial-number API/string request | No | Not necessary for research |
| Full Windows device/instance paths and Bluetooth addresses | Not in standard exports | Private internal identifiers used for correlation; text filtering is not exhaustive anonymization |
| Windows username, account credentials, browser history | No intentional collection | Not requested or searched; user text/raw payloads can still contain private information |
| Owner notes, app/firmware/version observations | Optional | Owner-provided text needs review |
| Settings-app experiment timeline, phone/app version, changes/restoration | Separate opt-in mode | Owner performs actions externally; claims/timestamps do not verify protocol commands |
| Raw USBPcap/Android HCI trace | Separate opt-in, selected local import | Not anonymized; may include identifiers, pairing material or unrelated traffic |
| Full Android bug-report contents | Not exported | Reads archive directory and one bounded Bluetooth log; does not extract the full report |
| Internet traffic capture | No | PCAP import accepts USBPcap link type only; no network interfaces recorded by kit |
| Automatic upload or telemetry | No | All output remains local |
| Controller writes, firmware access, driver installation | No | Not implemented by scanner |

Never assume an unknown controller's raw input or imported trace is anonymous. When raw
HID/trace data is included, the manifest explicitly marks possible identifiers and does
not claim serials/addresses are absent. Device names and owner text may contain information
the filters do not recognize. Review the actual files before choosing to share anything.
