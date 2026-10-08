# Phase 1 partial input support (2026-10-08)

Approved scope: local implementation only; no release, screen use or physical controller access.

1. Reuse explicit disconnect/reconnect and per-player source binding for D10, X15, X05 and X10. All accept correlated XInput sources. Only X15 accepts its narrowly observed HID layout; arbitrary HID layouts remain raw scanner evidence.
2. Recognize D10 receiver 2345:E062 from the published owner descriptor, and mark X15 receiver 1A34:F517 experimental. Receiver recognition is separate from input-slot correlation and wireless-controller link verification. Generic 045E:028E identifies no EasySMX model.
3. Shared model metadata provides Known/Missing evidence and readable standard controls. All unknown writes remain blocked. Preserve X20/Pro and other previews.
4. Extend existing scanner with repeated button/rear correlation, trigger sweeps and owner-selected long/short modes; optional reversible owner-programmed macro playback; bounded opt-in XInput rumble; normal/alternate/firmware identities. Parsed HID caps are not original descriptors. Import external traces for unavailable raw descriptors/vendor outputs.
5. Document USBPcap + Wireshark for PC USB/HID writes, phone HCI snoop logs for Android configuration, Windows Bluetooth tracing limitations and optional BLE sniffers. No driver install, firmware flash or command replay.
6. Verify failing then passing focused tests, frontend typecheck/build and isolated fixture flows. Physical observations remain outstanding.
