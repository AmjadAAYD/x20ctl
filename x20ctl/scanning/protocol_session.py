"""Owner-led official-app differential capture. X20CTL sends no setting commands."""

from .evidence import utc_now


def collect(scanner):
    model = scanner.options["model"].upper()
    app = "KeyLinker" if model == "X15" else "EasySMX Android app through QMacro"
    if (
        scanner._ask(
            f"Optional {model} protocol capture using {app} that ALREADY works with your unit? Enable Android HCI snoop before the app session. X20CTL only records your timeline and imports your selected log; it sends no mapping commands.",
            "yes",
        )
        != "yes"
    ):
        scanner._mark("configuration", "skipped", "No owner-led app capture")
        return
    original = scanner._ask(
        "Read and save the original mapping/profile in YOUR working app. Record the exact original assignment and how you will restore it. Skip if unknown.",
        "text",
    )
    if not original or original.strip().lower() in {"skip", "unknown", "not tested"}:
        scanner._mark("configuration", "skipped", "No known restoration baseline")
        return
    scanner._write(
        "experiments/RESTORE-MAPPING.txt",
        "Restore the original mapping/profile using your working app: " + original,
    )
    if scanner.options.get("appName") == "unknown":
        scanner.options["appName"] = app
    if scanner.options.get("appVersion") == "unknown":
        scanner.options["appVersion"] = (
            scanner._ask("Record the app version, or enter unknown.", "text")
            or "unknown"
        )
    events = []
    steps = [
        (
            "baseline",
            "With HCI logging running, connect the configuration app and read the unchanged baseline. Preserve all settings.",
        ),
        (
            "change_save",
            "In YOUR app, change just one reversible button mapping, such as A to B if different from its original target. Save it. Record no lighting or firmware changes.",
        ),
        (
            "readback",
            "Reopen/read that mapping in YOUR app. Confirm the saved value, or Skip if it cannot be verified.",
        ),
        (
            "phone_disconnect",
            "Disconnect the configuration phone/app. Test the changed physical button on the gameplay PC. Continue only if it performs the new action.",
        ),
        (
            "power_cycle",
            "Power-cycle the controller, reconnect gameplay and test the changed button again. Continue only if it persisted.",
        ),
        (
            "restore",
            "Reconnect YOUR configurator and restore the EXACT saved baseline. Save and read it back. Verify the original physical button action.",
        ),
        (
            "restore_power_cycle",
            "Power-cycle again and verify that the original behavior remains restored. Stop the HCI log; later import only the selected controller session.",
        ),
    ]
    for name, instruction in steps:
        answer = scanner._ask(instruction + " Skip if uncertain or unsupported.")
        events.append(
            {"event": name, "ownerConfirmed": answer != "skip", "timestamp": utc_now()}
        )
        scanner._write(
            "experiments/protocol-timeline.json",
            {
                "model": model,
                "appName": scanner.options.get("appName"),
                "appVersion": scanner.options.get("appVersion"),
                "original": original,
                "events": events,
                "evidenceType": "owner_reported",
                "configurationWritesByX20CTL": False,
                "commandProtocolVerified": False,
                "physicalAcceptanceComplete": False,
            },
        )
        if answer == "skip":
            scanner._ask(
                "If you changed any mapping, restore your saved baseline now using your known procedure. If restoration is uncertain, preserve the report and ask for help."
            )
            scanner._mark(
                "configuration",
                "unavailable",
                "Owner session incomplete; protocol remains unverified",
            )
            return
    scanner._mark(
        "configuration",
        "owner_reported",
        "Owner confirmed a capture timeline; imported bytes still need decoding and physical acceptance",
    )
