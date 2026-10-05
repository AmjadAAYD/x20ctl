"""Volunteer console wizard and offline maintainer validator."""
import argparse
import ctypes
import json
from pathlib import Path
import platform
import sys
import tempfile

from . import VERSION
from .backend import Backend, worker
from .evidence import Report, candidates, related, public_device, clean_text, validate_archive, MAX_FILES
from .input_tests import ACTIONS


def choose(prompt, rows, describe, ask=input, say=print):
    if not rows: say("No candidate appeared. This step can be skipped."); return None
    say(prompt)
    for index, row in enumerate(rows, 1): say(f"  {index}. {describe(row)}")
    while True:
        answer = ask("Number, or ENTER to skip: ").strip()
        if not answer: return None
        if answer.isdigit() and 1 <= int(answer) <= len(rows): return rows[int(answer) - 1]
        say("Choose one of the listed numbers.")


def yes(prompt, ask=input):
    return ask(prompt + " [y/N]: ").strip().casefold() in {"y", "yes"}


def wizard(output, backend=None, ask=input, say=print, beginner=False, guided=False, app_only=False):
    if guided: beginner = False
    friendly = beginner or guided
    backend = backend or Backend()
    say(f"X20ctl Controller Scan Kit {VERSION}")
    if friendly:
        say("This reads controller information and saves a file for Amjad.")
        say("It does not change controller settings or send anything automatically.")
        if app_only: say("This separate advanced mode guides YOUR actions in an existing settings app.")
        say("Close any game and Steam first. Keep your usual controller mode.")
        say("You can stop with CTRL+C. No software or driver installation is needed.")
    else:
        say("Local standard-device and gameplay-input collection. No configuration/firmware writes or uploads.")
        say("Close games/remappers; use normal documented controller modes. CTRL+C stops collection.")
        say("Serials, full device paths and BLE addresses are omitted. Review device text, notes and raw payloads before sharing.")
    if not yes("Ready to start? Type y for yes" if friendly else "Start voluntary local collection?", ask): return None
    model = choose("1. What model is written on your controller or its box?" if friendly else
                   "What model is printed on your controller? (a claim, not automatic detection)",
                   ["X20", "X20 Pro", "Other model (X05, X05 Pro, X10, D10, etc.)"], str, ask, say)
    if model is None: return None
    if model.startswith("Other"): model = clean_text(ask("Exact printed model: ").strip()) or "Unknown"
    report = Report(output, model)
    report.write_json("system.json", {"os": platform.system(), "version": platform.version(),
                                      "architecture": platform.machine(), "collectorVersion": VERSION})
    say("Your result will be saved in the results folder beside the launcher." if friendly else f"Session folder: {report.directory.resolve()}")
    review_devices = []
    stopped = False
    if app_only:
        return finish_beginner(report, review_devices, stopped, ask, say, full=True, include_app_capture=True)
    try:
        if guided:
            say("CONTROLLER INFORMATION - press ENTER whenever you do not know an answer.")
            say("Use information you already know. Do not install an app or update firmware for this.")
            revision = clean_text(ask("Hardware revision on the box, if shown (not the serial number): ").strip()) or "unknown"
            app = clean_text(ask("Controller settings app you use, if any: ").strip()) or "unknown"
            app_version = clean_text(ask("That app's version, if known: ").strip()) if app != "unknown" else "unknown"
            firmware = clean_text(ask("Controller firmware version, if already known: ").strip()) or "unknown"
            settings = clean_text(ask("Settings you have actually changed in that app (lights, remapping, etc.): ").strip()) if app != "unknown" else "unknown"
            report.write_json("intake.json", {"evidenceType": "owner_reported", "hardwareRevision": revision,
                "appName": app, "appVersion": app_version or "unknown", "firmwareVersion": firmware,
                "appSettingsObserved": settings or "unknown"})
            say("Close any controller settings app during the tests. Keep your usual controller mode.")
        while len(report.manifest["sessions"]) < 8:
            if len([p for p in report.directory.rglob("*") if p.is_file()]) > MAX_FILES - 45:
                say("This report is near its file limit. Finish it, then start a new report for more sessions."); break
            labels = {"direct_usb": "A cable from the controller to the computer",
                      "receiver": "The small wireless USB receiver plugged into the computer",
                      "bluetooth": "Bluetooth, with no cable or USB receiver"}
            transport = choose("2. How do you connect the controller for this round?" if friendly else
                "Select one connection for this session", ["direct_usb", "receiver", "bluetooth"],
                (lambda value: labels[value]) if friendly else str, ask, say)
            if transport is None: break
            session_id = f"session{len(report.manifest['sessions']) + 1:02d}"
            session = {"id": session_id, "transport": transport,
                       "mode": "unknown" if beginner else clean_text(ask(
                           "Mode name if you know it (PC, Switch, etc.); ENTER if unsure: " if guided else
                           "Current documented mode (XInput/DInput/Switch/unknown): ").strip()) or "unknown",
                       "status": "started", "tests": []}
            report.manifest["sessions"].append(session)
            prefix = f"device/{session_id}"
            if friendly:
                say("3. Temporarily disconnect THIS controller:")
                say({"direct_usb": "Unplug the controller's cable from the computer.",
                     "receiver": "Unplug the small controller receiver from the computer.",
                     "bluetooth": "Turn the controller off; leave the computer's Bluetooth on."}[transport])
                say("Leave your keyboard, mouse and other devices connected.")
            else:
                say("Disconnect the target data connection; unplug its receiver for a receiver session.")
                say("For Bluetooth, turn the target off. Do not disconnect keyboards or other essential devices.")
            ask("Press ENTER when that is done: " if friendly else "Press ENTER for the disconnected baseline: ")
            try: before = backend.inventory()
            except Exception:
                session.update(status="failed", reason="Baseline inventory unavailable")
                report.write_json(prefix + "/session.json", session)
                say("Couldn't check the computer with the controller disconnected. Saving a note for Amjad." if friendly else "Baseline collection failed. The failed session is retained.")
                if beginner or not yes("Try a different session?", ask): break
                continue
            if friendly:
                say("4. Reconnect the same controller:")
                say({"direct_usb": "Plug the controller cable back in. Remove its wireless receiver if one is plugged in.",
                     "receiver": "Plug the receiver back in and turn on the controller. Leave its cable unplugged.",
                     "bluetooth": "Turn the controller on and wait until it connects as usual."}[transport])
            else: say("Connect only the target normally. Direct USB: remove receiver. Receiver: remove pad's USB data cable.")
            ask("Press ENTER once it is connected: " if friendly else "Press ENTER for the connected inventory: ")
            try: after = backend.inventory()
            except Exception:
                session.update(status="failed", reason="Connected inventory unavailable")
                report.write_json(prefix + "/session.json", session)
                say("Couldn't read the device list after reconnecting. Saving a note for Amjad." if friendly else "Connected inventory failed. Partial evidence is retained.")
                if beginner or not yes("Try a different session?", ask): break
                continue
            changed = candidates(before["devices"], after["devices"])
            # Show one row per parent. Child interfaces remain available after selection.
            groups = {}
            for row in changed: groups.setdefault(row.get("_parent") or row["_key"], row)
            options = list(groups.values())
            if friendly and len(options) == 1:
                say("One new device appeared: " + clean_text(options[0].get("product") or options[0].get("description", "Unnamed device")))
                selected = options[0] if yes("Was this the only device you just connected?", ask) else None
            else:
                if friendly: say("If you do not recognize a device, press ENTER to skip. Do not guess.")
                selected = choose("Which newly connected device belongs to your controller?", options,
                    (lambda r: clean_text(r.get("product") or r.get("description", "Unnamed device"))) if friendly else
                    (lambda r: f"{clean_text(r.get('product') or r.get('description', 'Device'))} "
                     f"VID {r.get('vid', 0):04X} PID {r.get('pid', 0):04X} ({r.get('kind')})"), ask, say)
            if selected:
                session["selection"] = "disconnected/connected difference plus volunteer confirmation"
                rows = related(after["devices"], selected)
                try: rows = backend.details(rows)
                except Exception:
                    session["hid_metadata_status"] = "unavailable"
                    say("Couldn't read all the controller details. You can still finish." if friendly else "Selected HID metadata timed out or was unavailable; PnP evidence is retained.")
                public_rows = [public_device(r) for r in rows]
                review_devices.extend(public_rows)
                report.write_json(prefix + "/windows-device.json", {"devices": public_rows})
                session["device_file"] = prefix + "/windows-device.json"
                if transport != "bluetooth":
                    try:
                        usb = backend.usb(selected)
                        report.write_json(prefix + "/usb-descriptors.json", usb["metadata"])
                        for name, value in usb["binaries"].items(): report.write_bytes(prefix + "/" + name, bytes.fromhex(value))
                        session["usb_status"] = usb["metadata"]["status"]
                    except Exception:
                        session["usb_status"] = "unavailable"
                        session["usb_reason"] = "Standard USB hub query failed or timed out; no driver changes attempted"
                session["hid_report_descriptor"] = {"status": "unavailable", "reason":
                    "Raw HID report descriptor not obtained by this native adapter; parsed caps are not original descriptor bytes"}
                if not beginner and yes("Test the buttons, sticks and triggers now?" if guided else
                                        "Run guided gameplay input tests for this selected device?", ask):
                    record_inputs(report, session, rows, selected, before, after, backend, ask, say, guided)
                elif not beginner: session["input_status"] = "skipped"
            else:
                session["selection"] = "not_correlated"
                session["input_status"] = "skipped"
            if beginner:
                session["input_status"] = "not_requested_first_run"
                session["ble_status"] = "not_requested_first_run"
            elif transport == "bluetooth" or yes("Check for your controller's Bluetooth information too? You may skip this" if guided else
                                               "Add optional BLE discovery for this session?", ask):
                collect_ble(report, session, prefix, backend, ask, say, guided)
            else: session["ble_status"] = "skipped"
            session["status"] = "collected_with_limits" if selected or session.get("ble_status") == "observed" else "no_target_correlated"
            report.write_json(prefix + "/session.json", session)
            if not beginner:
                report.write_json(f"input/{session_id}/mapping.json", {"tests": session["tests"],
                    "home": "Not reported by documented XInputGetState; no undocumented API used",
                    "timing": "Host samples only; not a measurement of controller polling rate/latency"})
            if beginner or not yes("Try this same controller with another connection or a mode you know?" if guided else
                                   "Add another connection/mode session?", ask): break
    except (KeyboardInterrupt, EOFError):
        stopped = True
        say("Collection stopped. Partial files stay local.")
        if report.manifest["sessions"] and report.manifest["sessions"][-1]["status"] == "started":
            report.manifest["sessions"][-1]["status"] = "cancelled"
    report.manifest["collectionStoppedEarly"] = stopped
    if friendly:
        return finish_beginner(report, review_devices, stopped, ask, say, full=guided)
    if stopped:
        say("No ZIP created after cancellation. Review the local folder before sharing anything.")
        return report.directory
    say("Preview: claimed model " + model)
    for session in report.manifest["sessions"]:
        say(f"  {session['id']}: {session['transport']} / {session['mode']} / {session['status']}")
    for file in sorted(report.directory.rglob("*")):
        if file.is_file(): say(f"  {file.relative_to(report.directory)} ({file.stat().st_size} bytes)")
    say("Serials and paths are excluded. Device descriptions, notes and raw input still need your review.")
    if not stopped:
        try:
            notes = ask("Optional notes (no names/serials/addresses), ENTER for none: ")
            if notes.strip(): report.write_text("docs/volunteer-notes.txt", clean_text(notes))
            if not yes("Create a local ZIP? You may inspect/remove optional files in the folder first", ask):
                say("No ZIP created. Local folder retained."); return report.directory
            if not yes("Have you reviewed the files for private information?", ask):
                say("No ZIP created. Review the local folder before sharing.")
                return report.directory
            report.manifest["privacy"]["userReviewed"] = True
        except (KeyboardInterrupt, EOFError):
            say("No ZIP created. Local folder retained."); return report.directory
    archive = report.package()
    say(f"Created: {archive.resolve()}")
    say("Nothing was uploaded. Send the reviewed ZIP privately to Amjad in your existing conversation.")
    if stopped: say("This is an unreviewed partial report; inspect it before sharing.")
    return archive


def record_inputs(report, session, rows, selected, before, after, backend, ask, say, guided):
    sources = []
    new_slots = sorted(set(after["xinput_slots"]) - set(before["xinput_slots"]))
    if len(new_slots) == 1:
        sources.append({"source": "xinput_state", "slot": new_slots[0], "label":
            f"XInput slot {new_slots[0]} appeared with target; confirm no other controller changed"})
    elif new_slots: say("Several controllers appeared at once. Normal Windows button recording is skipped to avoid guessing.")
    for index, row in enumerate(rows):
        if row.get("kind") == "hid" and row.get("usage_page") == 1 and row.get("usage") in (4, 5, 8) and row.get("input_len", 0):
            sources.append({"source": "hid_input", "selected": row, "label":
                f"Gameplay HID collection {index + 1}, input length {row['input_len']} bytes"})
    if guided:
        choices = [r for r in sources if r["source"] == "xinput_state"]
        raw = [r for r in sources if r["source"] == "hid_input"]
        if choices: say("Normal Windows button/stick readings will be recorded for your confirmed controller.")
        if raw:
            say("Optional detailed input records the controller's own input bytes and repeats the button checks.")
            say("Those bytes may include device-specific information. No keyboard/mouse input is recorded.")
            if yes("Include that detailed input in your private result?", ask): choices.extend(raw)
    else:
        choice = choose("Choose the recording source (API states and HID bytes differ)", sources, lambda r: r["label"], ask, say)
        if choice and yes("Confirm this input source belongs to the isolated controller?", ask):
            if choice["source"] == "hid_input" and not yes("Raw input bytes can contain device-specific data. Include them in this private report?", ask): choice = None
        else: choice = None
        choices = [choice] if choice else []
    if not choices:
        session["input_status"] = "skipped_or_unavailable"
        say("No confirmed input recording was selected. Other controller information will still be saved.")
        return
    session["input_source"] = choices[0]["source"]
    session["input_sources"] = [{"source": r["source"], "capture_index": i + 1} for i, r in enumerate(choices)]
    actions = list(ACTIONS)
    extra = ask("Names of extra buttons on your pad (M1, M2, paddles, etc.), separated by commas; ENTER for none: " if guided else
                "Extra physical controls to record, comma-separated; ENTER for none: ").strip()
    for index, label in enumerate(extra.split(",")[:8]):
        if label.strip(): actions.append((f"extra{index + 1}", "Press and release " + clean_text(label)))
    for source_index, choice in enumerate(choices, 1):
        if choice["source"] == "hid_input":
            report.manifest["privacy"].update(rawHidInputIncluded=True,
                rawHidInputMayContainIdentifiers=True, rawHidInputAutomaticallyAnonymized=False,
                serialsCollected=None, addressesExported=None)
        if guided:
            say(f"BUTTON CHECK ROUND {source_index} OF {len(choices)} - " +
                ("normal Windows readings" if choice["source"] == "xinput_state" else "detailed controller input"))
            say("ENTER starts one check. s skips a missing button; q stops this round; CTRL+C stops everything.")
        for index, (action, prompt) in enumerate(actions):
            if len([p for p in report.directory.rglob("*") if p.is_file()]) >= MAX_FILES - 16:
                session["input_status"] = "file_limit_reached"
                say("This result is nearly full. Finish it and run the launcher again for further tests."); return
            answer = ask(f"{prompt}. ENTER records 5 seconds; s skips; q ends this round: ").strip().casefold()
            if answer == "q": break
            if answer == "s":
                session["tests"].append({"action": action, "status": "skipped", "source": choice["source"], "capture_index": source_index}); continue
            say("Starting recorder. Repeat the requested action slowly until the result appears.")
            directory = f"input/{session['id']}/capture{source_index:02d}" if guided else f"input/{session['id']}"
            filename = f"{directory}/{index:02d}-{action}.jsonl"
            try:
                result = backend.capture(report, choice.get("selected", selected), choice["source"], choice.get("slot"), filename, action, 5.0)
            except Exception: result = {"action": action, "status": "failed", "reason": "Input worker failed or timed out"}
            result.update(physical_instruction=prompt, capture_index=source_index, source=choice["source"])
            session["tests"].append(result)
            descriptions = {"change_observed": "Input changed; saved for Amjad to check", "no_change_observed": "No input change during this check",
                            "no_samples": "No input was received", "failed": "This check failed; partial information is kept",
                            "cancelled": "This check was stopped", "limit_reached": "Recording limit reached"}
            say("Result: " + (descriptions.get(result["status"], result["status"]) if guided else result["status"]))
            if result["status"] in {"failed", "cancelled", "limit_reached"}: break


def finish_beginner(report, devices, stopped, ask, say, full=False, include_app_capture=False):
    if include_app_capture and not stopped:
        from .app_capture import collect_app_capture
        try:
            collect_app_capture(report, ask, say)
        except (KeyboardInterrupt, EOFError):
            report.manifest["appCapture"] = {"status": "cancelled"}
            report.manifest["collectionStoppedEarly"] = True
            say("App capture stopped. Partial results stay local.")
        stopped = bool(report.manifest.get("collectionStoppedEarly"))
    if full and not stopped:
        try:
            notes = clean_text(ask("Anything else you have seen working (lights, vibration, remapping, macros)? ENTER to skip: ").strip())
            if notes: report.write_json("owner-observations.json", {"evidenceType": "owner_reported", "notes": notes})
        except (KeyboardInterrupt, EOFError):
            report.manifest["collectionStoppedEarly"] = stopped = True
    lines = ["BEFORE YOU SEND THIS RESULT TO AMJAD", "", "Controller model you entered: " + report.manifest["claimedModel"]]
    for session in report.manifest["sessions"]:
        connection = {"direct_usb": "USB cable", "receiver": "Wireless USB receiver", "bluetooth": "Bluetooth"}[session["transport"]]
        lines.append("Connection: " + connection)
        lines.append("Device identified: " + ("yes, with your confirmation" if session.get("device_file") else "not completed; tell Amjad where you stopped"))
        if session.get("status") == "failed": lines.append("A collection step failed. This result still records what happened.")
        if full:
            recordings = sum(1 for r in session["tests"] if r.get("file"))
            lines.append(f"Button/stick recording files: {recordings}; Amjad will check their contents.")
            lines.append("Optional Bluetooth check: " + {"observed": "details saved", "skipped": "skipped",
                "not_observed_under_tested_conditions": "no target confirmed during this check", "failed": "could not finish"}.get(session.get("ble_status"), "not completed"))
    for device in devices:
        for key, label in (("description", "Windows device name"), ("product", "Product name"), ("manufacturer", "Manufacturer")):
            if device.get(key): lines.append(label + ": " + str(device[key]))
    if full:
        capture = report.manifest.get("appCapture", {})
        lines.append("Optional settings-app capture: " + capture.get("status", "not completed").replace("_", " "))
        if capture.get("traceFile"):
            lines += ["RAW APP TRACE INCLUDED: it is not anonymized and may include unique IDs or unrelated traffic.",
                      "The kit checked its format only. Review its actual contents before sending."]
        if capture.get("restored") is False:
            lines.append("The original setting was NOT confirmed restored. Restore it in your normal app.")
    lines += ["", "The report also contains device codes and basic USB information for Amjad.",
              "You do not need to understand those codes. The tool does not request serial numbers.",
              "Windows' device name can differ from the model printed on your controller.",
              "Optional button and Bluetooth stages are recorded separately; skipped/failed checks are kept." if full else
              "No button recording or Bluetooth research was requested in this first run.",
              "Optional detailed input can contain device-specific data; ask Amjad if you are unsure about sharing it." if full else "",
              "Nothing has been sent automatically.", "",
              "Check the model and names above. If you see personal information or are unsure,",
              "do not send the result; tell Amjad what concerns you."]
    review = "\n".join(lines) + "\n"
    report.write_text("BEFORE_YOU_SEND.txt", review)
    say("\n5. Read this short summary before saving the ZIP:\n" + review)
    if stopped:
        say("No ZIP created after cancellation. Files stay on your computer.")
        return report.directory
    if not stopped:
        try:
            if not yes("Does this look okay to share with Amjad? Type y to save the ZIP", ask):
                say("No ZIP created. Files stay on your computer. Tell Amjad what concerns you.")
                return report.directory
            report.manifest["privacy"]["userReviewed"] = True
        except (KeyboardInterrupt, EOFError):
            say("No ZIP created. Files stay on your computer."); return report.directory
    archive = report.package()
    say("\nDONE. The result ZIP is: " + archive.name)
    say("Open the results folder beside the launcher and send that ZIP to Amjad privately.")
    if stopped: say("You stopped early. This is a partial result; ask Amjad before sharing it.")
    return archive


def collect_ble(report, session, prefix, backend, ask, say, guided=False):
    say("OPTIONAL BLUETOOTH CHECK: only your confirmed controller's details are saved." if guided else
        "BLE discovery is optional. Only selected target details are exported; raw advert payloads are omitted.")
    if guided: say("Turn off the controller. Leave the computer's Bluetooth on. Skip if Bluetooth is unavailable.")
    if not yes("Controller off and ready for a 10-second check?" if guided else "Turn target off and collect a 10-second BLE baseline?", ask):
        session["ble_status"] = "skipped"; return
    try:
        off = backend.ble_scan()
        if guided: say("Turn the controller back on. Use its normal documented mode; do not guess button combinations.")
        ask("Press ENTER when the controller is awake: " if guided else "Wake the target in its documented configuration mode. ENTER to scan again: ")
        on = backend.ble_scan()
        fresh = candidates(off, on)
        if guided: say("Choose only a name you recognize as your controller. Press ENTER if unsure.")
        selected = choose("Which new Bluetooth device is your controller?" if guided else
                          "Select a new candidate you can identify; a name alone is not model detection", fresh,
                          lambda r: r["name"], ask, say)
        if selected is None:
            session["ble_status"] = "not_observed_under_tested_conditions"; return
        if not yes("Is this your controller? Allow a connection to read its basic information?" if guided else
                   "Confirm this is your controller before connecting for standard service discovery?", ask):
            session["ble_status"] = "skipped"; return
        advert = {k: v for k, v in selected.items() if not k.startswith("_")}
        report.write_json(prefix + "/ble-advertisement.json", advert)
        result = backend.ble_inspect(selected)
        report.write_json(prefix + "/ble-gatt.json", result)
        session["ble_status"] = result["status"]
    except (KeyboardInterrupt, EOFError): raise
    except Exception:
        session["ble_status"] = "failed"
        session["ble_reason"] = "BLE unavailable, failed or timed out; no automatic retry"
        say("The Bluetooth check could not finish. Your other results are still saved." if guided else "BLE stage unavailable. Other evidence remains valid.")


def self_check():
    # No Native(), inventory, HID handles, XInput calls or BLE discovery here.
    from .usb import parse_configuration, Connection
    if sys.platform == "win32":
        assert ctypes.sizeof(Connection) == 36
    raw = bytes.fromhex("09021900010100803209040000010300000007058103400001")
    assert parse_configuration(raw)["interfaces"][0]["endpoints"][0]["direction"] == "IN"
    with tempfile.TemporaryDirectory(prefix="controller-kit-selfcheck-") as folder:
        report = Report(folder, "SYNTHETIC SELF-CHECK; NO HARDWARE")
        report.write_json("device/check.json", {"status": "not_collected", "synthetic": True})
        manifest = validate_archive(report.package())
        assert manifest["modelDetected"] is False
    if sys.platform == "win32":
        import bleak
        assert bleak.BleakClient and bleak.BleakScanner
        from bleak.backends.winrt.client import BleakClientWinRT
        from bleak.backends.winrt.scanner import BleakScannerWinRT
        assert BleakClientWinRT and BleakScannerWinRT
    return {"status": "passed", "version": VERSION, "hardware_access": False,
            "windows_package_imports_checked": sys.platform == "win32"}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Local read-only Controller Scan Kit; no firmware/config writes/uploads")
    parser.add_argument("--output", type=Path, default=Path.cwd() / "results")
    workflow = parser.add_mutually_exclusive_group()
    workflow.add_argument("--validate", type=Path, help="Validate a received evidence ZIP offline")
    workflow.add_argument("--self-check", action="store_true", help="Check package with synthetic data, no hardware")
    workflow.add_argument("--capabilities", action="store_true", help="Print collection policy without accessing hardware")
    workflow.add_argument("--privacy", action="store_true", help="Explain report contents without accessing hardware")
    workflow.add_argument("--dry-run", action="store_true", help="Explain the workflow without accessing hardware")
    workflow.add_argument("--advanced", action="store_true", help="Maintainer input/BLE and multiple-session workflow")
    workflow.add_argument("--guided", action="store_true", help="Full collection with plain instructions, intake, input and optional BLE")
    workflow.add_argument("--basic", action="store_true", help="Optional short device-discovery-only run")
    workflow.add_argument("--app-capture", action="store_true", help="Guided settings-app experiment only; no repeated device/button scans")
    workflow.add_argument("--worker", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    try:
        if args.capabilities or args.privacy or args.dry_run:
            from .policy import explain
            explain("capabilities" if args.capabilities else "privacy" if args.privacy else "dry-run")
            return 0
        if args.worker:
            request = sys.stdin.read(1024 * 1024 + 1)
            if len(request) > 1024 * 1024: raise ValueError("Worker request too large")
            print(json.dumps(worker(json.loads(request)))); return 0
        if args.validate:
            result = validate_archive(args.validate)
            print(json.dumps({"status": "valid", "claimedModel": clean_text(result["claimedModel"]),
                              "sessions": len(result["sessions"]), "files": len(result["files"])})); return 0
        if args.self_check: print(json.dumps(self_check())); return 0
        if sys.platform != "win32": parser.error("Live collection requires Windows 10/11")
        if args.app_capture: wizard(args.output, guided=True, app_only=True)
        else: wizard(args.output, beginner=args.basic, guided=not (args.basic or args.advanced))
        return 0
    except (KeyboardInterrupt, EOFError):
        print("Stopped. Any collected files remain local."); return 130
    except Exception as exc:
        if args.worker: print(json.dumps({"worker_error": type(exc).__name__}))
        else: print(f"Could not complete this step ({type(exc).__name__}). Files stay local; no upload.", file=sys.stderr)
        return 1


if __name__ == "__main__": raise SystemExit(main())
