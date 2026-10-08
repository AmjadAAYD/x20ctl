# Optional external protocol capture research

Investigated 2026-10-08. This is a later research mode, not a Phase 1 dependency. No capture driver, tool, firmware update or registry change was installed or run during implementation.

| Traffic origin | Practical route | Evidence | Limits |
| --- | --- | --- | --- |
| Official Windows software writing to a USB controller or receiver | USBPcap and Wireshark, selected root hub/device | Enumeration, control transfers, interrupt OUT/HID writes and IN replies at the USB host boundary | Driver installation/admin rights may be required; captures can include other devices; proprietary payloads still need analysis |
| An official application using this PC's Bluetooth stack | Microsoft Bluetooth Virtual Sniffer (BTVS, BTP package) and Wireshark | Host HCI/ACL traffic; inspect ATT/GATT writes and notifications if present | Default logs can omit large ACL/HID payloads. Pilot-test the exact Windows stack. Do not enable pairing-debug modes automatically |
| Android KeyLinker, QMacro or EasySMX app | Android Bluetooth HCI snoop log, opened on the Windows analysis machine | The phone's host-side packets during app actions | Vendor-specific extraction; potentially unrelated traffic and keys. A PC logger cannot see the separate phone connection |
| Other-host traffic unavailable through host logs | Optional Nordic nRF Sniffer for BLE and Wireshark | Over-air selected-link traffic | Extra hardware, connection-following limits and possible pairing/key requirements; encrypted traffic is not guaranteed readable |

Recommendation: start Phase 2 with a manual USBPcap/BTVS helper and Android-log import guide. Require a pilot containing actual app writes/replies before offering an integrated protocol recorder. Wireshark's current Bluetooth documentation also describes Windows etwdump/ETW with a stated Wireshark 4.7.3+ requirement; verify availability on the owner's installation first.

## Controlled experiment

1. Record the model/revision claim, actual mode/transport and already-working official app/version.
2. Isolate the selected device and start capture before reconnecting to include enumeration/descriptors.
3. Record an unchanged baseline.
4. Change exactly one reversible non-lighting setting in an app already known to work.
5. Record the change, restore the original value, record restoration and repeat.
6. For known macro procedures, compare M1=A, M1=A/B, clear M1 and corresponding M2 behavior. Preserve original assignments and capture playback separately.
7. Check that OUT writes and replies/notifications are actually present. Input-only changes are not a configuration protocol capture.
8. Keep immutable originals privately; share only selected-device/time-window evidence after inspecting serials, addresses, keys and unrelated traffic. Never replay unknown bytes or flash firmware.

The built-in scanner can import one selected PCAP/PCAPNG/BTSnoop attachment, limited to 4 MiB/file. Large originals stay separate; do not arbitrarily truncate binary captures. Input logging cannot intercept another process's HID writes or another host's BLE connection. Standard XInput motor tests address two channels; linked trigger feedback is an owner observation, not independent trigger control.

## Primary sources

- [USBPcap capture guide](https://desowin.org/usbpcap/tour.html): hub selection, extcap, reconnect-time descriptors.
- [Microsoft BTVS documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/testing-btp-tools-btvs): HCI traces, Wireshark mode, omitted payloads and debug controls.
- [Wireshark Bluetooth capture documentation](https://wiki.wireshark.org/CaptureSetup/Bluetooth): Windows-host ETW/etwdump and other-host limits.
- [Android Bluetooth verification/debugging](https://source.android.com/docs/core/connect/bluetooth/verifying_debugging): host HCI snoop collection.
- [Nordic bonded-connection sniffing guide](https://docs.nordicsemi.com/r/bundle/nrfutil/page/nrfutil-ble-sniffer/guides/common_sniffing_actions.html/sniffing-a-connection-between-bonded-devices): pairing/key prerequisites.

No live protocol capture, official-app behavior or encryption/decryption was verified on hardware in this task.
