"""Guided official-app experiments and bounded local trace import.

Recording is performed by Android or an existing Wireshark/USBPcap installation.
The kit never installs a driver, changes phone settings, or sends vendor commands.
"""
import struct
from pathlib import Path
import zipfile

from .evidence import MAX_FILE, MAX_FILES, MAX_TOTAL, clean_text, utc_now

SOURCES = {
    "android": "https://source.android.com/docs/core/connect/bluetooth/verifying_debugging",
    "windows": "https://desowin.org/usbpcap/tour.html",
    "export": "https://www.wireshark.org/docs/wsug_html_chunked/ChIOExportSection.html",
}


def trace_info(data, route):
    """Validate complete records, not filenames, and reject unrelated link types."""
    if not 16 < len(data) <= MAX_FILE:
        raise ValueError("Use a short capture smaller than 4 MB, with at least one packet.")
    count = 0
    if route == "android":
        if data[:8] != b"btsnoop\0" or struct.unpack_from(">II", data, 8) not in {(1, 1001), (1, 1002)}:
            raise ValueError("This is not a supported Android Bluetooth snoop file.")
        offset = 16
        while offset < len(data):
            if len(data) - offset < 24:
                raise ValueError("Bluetooth capture has an incomplete record.")
            original, included = struct.unpack_from(">II", data, offset)
            if not 0 < included <= original or included > len(data) - offset - 24:
                raise ValueError("Bluetooth capture has an incomplete packet.")
            offset += 24 + included
            count += 1
        kind, extension = "android_btsnoop", "btsnoop"
    elif data[:4] in {b"\xd4\xc3\xb2\xa1", b"\xa1\xb2\xc3\xd4", b"\x4d\x3c\xb2\xa1", b"\xa1\xb2\x3c\x4d"}:
        endian = "<" if data[:4] in {b"\xd4\xc3\xb2\xa1", b"\x4d\x3c\xb2\xa1"} else ">"
        if len(data) < 24 or struct.unpack_from(endian + "HH", data, 4) != (2, 4):
            raise ValueError("Unsupported PCAP header.")
        link = struct.unpack_from(endian + "I", data, 20)[0]
        if link not in {249, 187, 201}:
            raise ValueError("Use USBPcap or Bluetooth HCI traffic, not a network capture.")
        offset = 24
        while offset < len(data):
            if len(data) - offset < 16:
                raise ValueError("USB capture has an incomplete record.")
            included, original = struct.unpack_from(endian + "II", data, offset + 8)
            if not 0 < included <= original or included > len(data) - offset - 16:
                raise ValueError("USB capture has an incomplete packet.")
            offset += 16 + included
            count += 1
        kind, extension = ("usbpcap" if link == 249 else "hci_pcap"), "pcap"
    elif data[:4] == b"\x0a\x0d\x0d\x0a":
        offset, endian, interfaces = 0, None, []
        observed_links = set()
        while offset < len(data):
            if len(data) - offset < 12:
                raise ValueError("PCAPNG has an incomplete block.")
            if data[offset:offset + 4] == b"\x0a\x0d\x0d\x0a":
                order = data[offset + 8:offset + 12]
                endian = {b"\x4d\x3c\x2b\x1a": "<", b"\x1a\x2b\x3c\x4d": ">"}.get(order)
                interfaces = []
            if endian is None:
                raise ValueError("PCAPNG byte order is invalid.")
            block, size = struct.unpack_from(endian + "II", data, offset)
            if size < 12 or size % 4 or size > len(data) - offset or struct.unpack_from(endian + "I", data, offset + size - 4)[0] != size:
                raise ValueError("PCAPNG has an invalid or incomplete block.")
            if block == 0x0A0D0D0A and size < 28:
                raise ValueError("PCAPNG section is incomplete.")
            if block == 1:
                if size < 20:
                    raise ValueError("Incomplete PCAPNG interface")
                link = struct.unpack_from(endian + "H", data, offset + 8)[0]
                if link not in {249, 187, 201}:
                    raise ValueError("Use USBPcap or Bluetooth HCI interfaces, not network interfaces.")
                interfaces.append(link)
            elif block == 6:
                if size < 32:
                    raise ValueError("PCAPNG packet is incomplete.")
                interface, _, _, included, original = struct.unpack_from(endian + "IIIII", data, offset + 8)
                if interface >= len(interfaces) or not 0 < included <= original or ((included + 3) & ~3) > size - 32:
                    raise ValueError("PCAPNG packet is invalid.")
                observed_links.add(interfaces[interface])
                count += 1
            elif block in {2, 3}:
                raise ValueError("Save as current Wireshark PCAPNG or USBPcap PCAP.")
            offset += size
        kind = "usbpcap_ng" if observed_links == {249} else "hci_pcapng" if 249 not in observed_links else "mixed_usb_hci_pcapng"
        extension = "pcapng"
    else:
        raise ValueError("Choose the capture file, not a screenshot, executable or full bug report.")
    if not count:
        raise ValueError("The capture contains no packets.")
    transport = "usb" if kind in {"usbpcap", "usbpcap_ng"} else "mixed_usb_bluetooth" if kind == "mixed_usb_hci_pcapng" else "bluetooth_hci"
    return {"format": kind, "extension": extension, "transport": transport, "packets": count, "bytes": len(data),
            "contentValidation": "Container and packet boundaries only; commands and target identity not verified"}


def read_trace(path, route):
    """Read one bounded trace. A bug report ZIP is never copied or extracted."""
    path = Path(path)
    if not path.is_file() or path.is_symlink():
        raise ValueError("Choose an existing regular capture file on this computer.")
    with path.open("rb") as stream:
        signature = stream.read(4)
    if signature == b"PK\x03\x04" and route == "android":
        if path.stat().st_size > 256 * 1024 * 1024:
            raise ValueError("Bug report is over 256 MB. Ask Amjad to help extract the Bluetooth log locally.")
        with zipfile.ZipFile(path) as archive:
            if len(archive.infolist()) > 4096:
                raise ValueError("Bug report contains too many entries.")
            matches = [i for i in archive.infolist() if Path(i.filename.replace("\\", "/")).name.lower()
                       in {"btsnoop_hci.log", "btsnoop_hci.cfa", "btsnoop.log"} and not i.is_dir()]
            if len(matches) != 1:
                raise ValueError("No single full Bluetooth log found. Keep the bug report private; ask Amjad for help.")
            item = matches[0]
            if item.flag_bits & 1 or item.file_size > MAX_FILE:
                raise ValueError("Bluetooth log is encrypted or over 4 MB. Use a shorter recording.")
            with archive.open(item) as stream:
                data = stream.read(MAX_FILE + 1)
    else:
        if path.stat().st_size > MAX_FILE:
            raise ValueError("Capture is over 4 MB. Export a shorter controller-only recording.")
        with path.open("rb") as stream:
            data = stream.read(MAX_FILE + 1)
    return data, trace_info(data, route)


def collect_app_capture(report, ask=input, say=print):
    """Optional step; timestamp owner actions and include only an opt-in trace."""
    from .cli import choose, yes
    if not yes("Optional: record one setting change in a controller settings app you already use?", ask):
        report.manifest["appCapture"] = {"status": "skipped"}
        return
    say("Use an app that already connects and changes ordinary settings on THIS controller.")
    say("We will test one reversible setting, then restore it. No firmware, reset or calibration.")
    route = choose("Where does that working controller app run?", ["android", "windows"],
                   lambda value: {"android": "Android phone with Bluetooth", "windows": "Windows PC with USB cable or receiver"}[value], ask, say)
    if route is None:
        report.manifest["appCapture"] = {"status": "skipped"}
        return
    experiment = {"status": "started", "route": route, "evidenceType": "owner_timed_experiment",
                  "events": [], "sources": SOURCES, "commandsVerified": False}
    report.manifest["appCapture"] = experiment

    def note(label, prompt):
        experiment["events"].append({"event": label + "_prompt", "timestampUtc": utc_now()})
        ask(prompt + " Press ENTER when done, or CTRL+C to stop: ")
        experiment["events"].append({"event": label + "_confirmed", "timestampUtc": utc_now()})

    try:
        experiment["appName"] = clean_text(ask("Name of the app you already use: ").strip()) or "unknown"
        experiment["appVersion"] = clean_text(ask("App version, if known; ENTER if unsure: ").strip()) or "unknown"
        experiment["setting"] = clean_text(ask("ONE setting to test (e.g. vibration level or M1 mapping): ").strip())
        if not experiment["setting"]:
            experiment["status"] = "skipped_no_setting"
            return
        experiment["originalValue"] = clean_text(ask("Its current value, so you can restore it: ").strip())
        experiment["testValue"] = clean_text(ask("The different value you want to test: ").strip())
        if not experiment["originalValue"] or not experiment["testValue"] or experiment["originalValue"] == experiment["testValue"]:
            experiment["status"] = "skipped_no_reversible_change"
            say("Keep the setting unchanged. We need a known original and a different test value.")
            return
        if not yes("Is this an ordinary reversible setting, with no firmware/update/reset/calibration involved?", ask):
            experiment["status"] = "skipped_unconfirmed_setting"
            return
        say("Packet captures can include unique IDs and unrelated traffic. Nothing is uploaded.")
        if route == "android":
            experiment["phoneModel"] = clean_text(ask("Phone model and Android version, if known (no serial/IMEI): ").strip()) or "unknown"
            say("On the phone, disconnect other Bluetooth devices. Do not use Bluetooth calls or audio.")
            say("If Developer options is hidden: Settings > About phone > Build number; tap it seven times.")
            say("Phone menus vary. If that option is missing or blocked, skip and ask Amjad.")
            if not yes("Can you find Developer options > Bluetooth HCI snoop log?", ask):
                experiment["status"] = "unavailable_phone_logging"
                return
            say("Turn Bluetooth HCI snoop logging ON; choose Full if that choice exists.")
            say("Turn Bluetooth OFF then ON. Do not enable USB debugging or root the phone.")
            experiment["loggingMode"] = clean_text(ask("Logging choice shown (Full, Filtered, toggle only, etc.): ").strip()) or "unknown"
            note("recording_ready", "Complete those phone steps; keep the controller settings app closed")
        else:
            say("Windows recording needs Wireshark and its USBPcap capture driver.")
            say("If USBPcap is missing, skip this stage; do not install or replace drivers during this run.")
            say("Open Wireshark > Capture > Options. Find a USBPcap entry (not Wi-Fi/Ethernet).")
            if not yes("Do you see USBPcap and agree to use this optional capture tool?", ask):
                experiment["status"] = "unavailable_usbpcap"
                return
            say("Open the USBPcap entry's settings. Its attached-device list shows USB devices.")
            say("Find THIS controller or receiver by unplugging/replugging only it and refreshing the list.")
            say("Select only that device address for capture. Leave capture-all and capture-new-devices OFF.")
            if not yes("Can you identify and select only this controller/receiver? Do not guess", ask):
                experiment["status"] = "unavailable_target_filter"
                return
            say("Keep it connected after selection; its device address can change on reconnect.")
            experiment["usbpcapVersion"] = clean_text(ask("Wireshark/USBPcap versions, if shown; ENTER if unsure: ").strip()) or "unknown"
            note("recording_ready", "Start the selected USBPcap capture. Approve its Windows permission prompt only if you chose this tool")
        note("app_connected", "Open your controller settings app and connect normally; do not change anything yet")
        note("idle_before", "Leave the controller and app untouched for about five seconds")
        say("Test: " + experiment["setting"] + " from " + experiment["originalValue"] + " to " + experiment["testValue"])
        note("change", "Change ONLY that setting. Click Apply/Save if the app normally requires it")
        experiment["changeObserved"] = clean_text(ask("What actually changed on the controller? ENTER if unsure: ").strip()) or "unknown"
        note("idle_after", "Wait about five seconds without other changes")
        note("restore", "Restore the original value: " + experiment["originalValue"] + ". Apply/Save if needed")
        experiment["restored"] = yes("Did the setting return to its original value?", ask)
        if not experiment["restored"]:
            say("Stop further experiments. Restore the setting using your normal app before continuing.")
        if route == "android":
            note("recording_stopped", "Close the app and turn Bluetooth HCI snoop logging OFF")
            say("Use Developer options > Take bug report, if your phone provides it.")
            say("Save/share that bug-report ZIP to this computer privately, or copy an exported Bluetooth snoop log.")
            say("The kit reads ONLY one Bluetooth log from the ZIP; the full bug report stays outside your result.")
            say("Some phones omit the full log. If so, keep the report private and ask Amjad; do not root the phone.")
        else:
            note("recording_stopped", "Click the red Stop button in Wireshark, then File > Save As to save the short capture")
            say("If the recording includes other USB devices, do not attach it. Ask Amjad to help create a controller-only export.")
        experiment["timingNote"] = "UTC times from this PC bracket owner-confirmed actions; phone clock and packet times may differ"
        entered = ask("Drag the saved capture (or Android bug-report ZIP) into this window, then ENTER; blank skips attachment: ").strip().strip('"')
        if not entered:
            experiment["status"] = "timeline_only_no_trace"
            return
        try:
            data, info = read_trace(entered, route)
        except (OSError, ValueError, zipfile.BadZipFile, RuntimeError):
            experiment["status"] = "timeline_only_import_failed"
            say("That file could not be used: missing, invalid, too large, or no single full Bluetooth log.")
            say("Keep the file private; the action timeline is still saved. Ask Amjad for help.")
            return
        say(f"Found {info['packets']} packets. This checks the file format, not which device or setting commands it contains.")
        say("This optional raw trace may contain serials, Bluetooth addresses or unrelated traffic, including older phone data.")
        say("It is NOT automatically anonymized. Review it with Amjad first if you are unsure.")
        if not yes("Include this raw trace in the local ZIP for private review with Amjad?", ask):
            experiment["status"] = "timeline_only_trace_declined"
            return
        files = [p for p in report.directory.rglob("*") if p.is_file()]
        if len(files) >= MAX_FILES - 4 or sum(p.stat().st_size for p in files) + len(data) > MAX_TOTAL - MAX_FILE:
            experiment["status"] = "timeline_only_report_full"
            say("This result is nearly full. The timeline is saved; run the app-only option for a separate capture.")
            return
        filename = "app-capture/setting-change." + info["extension"]
        report.write_bytes(filename, data)
        experiment.update(status="trace_attached_for_review", traceFile=filename, trace=info)
        report.manifest["privacy"].update(serialsCollected=None, addressesExported=None,
            rawAppTraceIncluded=True, rawAppTraceMayContainIdentifiers=True,
            rawAppTraceAutomaticallyAnonymized=False)
        say("Capture attached locally. Its contents still need review before sharing.")
    except (KeyboardInterrupt, EOFError):
        experiment["status"] = "cancelled"
        report.manifest["collectionStoppedEarly"] = True
        say("App capture stopped. Restore the original setting if changed; stop Wireshark/phone logging if still running.")
    finally:
        report.write_json("app-capture/experiment.json", experiment)
