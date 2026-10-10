"""Hardware-free explanations backed by the packaged capability declaration."""
import json
from pathlib import Path
import sys


def capabilities():
    root = Path(sys._MEIPASS) if getattr(sys, "frozen", False) else Path(__file__).resolve().parents[2]
    return json.loads((root / "CAPABILITIES.json").read_text(encoding="utf-8"))


def explain(mode, say=print):
    policy = capabilities()
    if mode == "capabilities":
        say(json.dumps(policy, indent=2))
    elif mode == "privacy":
        say("Reports stay local. No Internet upload or telemetry is implemented by this scanner.")
        say("Standard reports: claimed model, OS/version/architecture, selected device VID/PID,")
        say("names, hardware/compatible IDs, driver service, HID caps, standard USB descriptors.")
        say("Optional: gameplay input, selected BLE services and standard values, owner notes.")
        say("Serial-number APIs are not called; internal device paths and BLE addresses are omitted.")
        say("Notes/device text still require review. Raw HID bytes can contain identifiers.")
        say("Separate --app-capture mode can import a user-selected local USB/Bluetooth trace.")
        say("Raw traces are NOT anonymized and can include IDs, sensitive data or unrelated traffic.")
        say("No browser history or personal-document search. Only the explicit capture file is read.")
        say("An Android bug-report ZIP is inspected only to read one bounded Bluetooth log.")
        say("No hardware access has been performed by this command.")
    elif mode == "dry-run":
        say("DRY RUN: no hardware enumeration, connections, input reads or report creation.")
        say("A normal scan would:")
        say("1. Ask for consent and the model printed on your controller.")
        say("2. Compare disconnected/connected Windows device inventories.")
        say("3. Ask you to confirm the target; read selected metadata and standard USB descriptors.")
        say("4. Offer optional input recording and BLE discovery/standard GATT reads.")
        say("5. Save locally; ask you to review before creating a ZIP. Nothing is uploaded.")
        say("--basic skips input/BLE. --app-capture is a separate advanced opt-in workflow.")
    else:
        raise ValueError("Unknown explanation")
