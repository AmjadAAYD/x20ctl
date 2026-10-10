# After Amjad reviews the discovery report

The automated kit identifies interfaces and input. It cannot establish the configuration commands for RGB, vibration strength, trigger settings, deadzones, remapping or macros. Those need a capture of a known configuration app communicating with the same controller/mode, plus a physical result and restoration record.

Read [the controlled-capture research guide](research/05-CONTROLLED-CAPTURES.md) and [the feature validation guide](research/06-FEATURE-VALIDATION.md) for the detailed sequence. Use [the timeline template](research/templates/SESSION-TIMELINE.csv). Perform one reversible change per capture, with the baseline value, new value, exact click/time, result, then restoration to the baseline. Capture a no-change control and repeat the same experiment if useful. Do not combine RGB, remapping and firmware updates in one log.

## Original USB/HID descriptors

Windows' preparsed HID caps describe parsed collections. This kit marks original HID report descriptor bytes unavailable. Do not rename a capabilities JSON file to `report-descriptor.bin`.

If Amjad needs the original bytes, a guided USBPcap/Wireshark enumeration capture may provide a standard GET_DESCRIPTOR response. Start the capture before reconnecting the target. Confirm the physical hub and newly assigned USB address and retain the descriptor transfer's request, response and provenance. An HID report-descriptor request uses descriptor type `0x22`; configuration data's HID descriptor (`0x21`) states a length but is not itself the report descriptor. If the transfer is absent, report it as absent. Do not sweep feature IDs to obtain it.

USBPcap records the chosen USB root hub's traffic; it cannot record arbitrary Bluetooth or over-the-air receiver traffic. Other USB devices can appear in the same capture. A Wireshark display filter affects display, not the saved file. Amjad should first provide the exact hub/device-address and review/export procedure for that session. Keep the original private and send only the agreed reviewed subset. Preserve request/response correlation and descriptor provenance when removing other packets.

There is no `usb.device == VID/PID` display filter. VID/PID comes from descriptor data; device-address/bus/interface filters must follow that actual enumeration. The receiver's USB configuration channel, when present, is evidence about its PC-facing interface, not proof of the radio protocol. The kit never installs USBPcap, Zadig, a replacement driver or an updater.

## Manufacturer application

Record the application's exact name/version, official download source, host OS, connection and mode. Prove it connects and a normal setting changes the physical device before assigning a full capture. The app may use HID feature reports, output/interrupt reports, USB vendor control transfers or BLE; the target model's channel is unknown until observed. Do not replay X20 commands against a different model based on a product name or shared VID/PID.

Some Android configuration routes require a separate Bluetooth HCI snoop workflow. A Windows BLE UUID inventory is not an app-command capture. Android snoop files may contain unrelated Bluetooth traffic and personal information; they require their own isolation/review plan. Do not submit the whole phone's diagnostic archive by default.

## File delivery and provenance

PCAP/PCAPNG, original descriptor exports and photos are separate supplemental evidence. The automated report validator intentionally accepts only its own JSON/JSONL/TXT/BIN evidence format. Identify the related submission ID, session ID, app version, mode, file SHA-256, capture start/end time, baseline/action/restoration and whether the raw descriptor is original or reconstructed. Record omissions explicitly. Never execute received binaries or replay captured commands as part of archive inspection.

## Primary references

- [Microsoft: obtaining HID collection information](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/obtaining-collection-information) explains collection/preparsed information.
- [Microsoft USBView descriptor-query sample](https://github.com/microsoft/Windows-driver-samples/blob/main/usb/usbview/enum.c) shows standard USB descriptor requests.
- [USBPcap capture walkthrough](https://desowin.org/usbpcap/tour.html) describes root-hub capture and enumeration.
- [Wireshark display-filter reference for USB](https://www.wireshark.org/docs/dfref/u/usb.html) lists actual field names.
- The broader [source register](research/08-SOURCES-AND-LIMITS.md) documents the research limitations and vendor/manual leads.
