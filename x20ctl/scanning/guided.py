"""Owner-led input experiments; outbound protocol traffic requires external capture."""

import json

from .analysis import control_evidence, trigger_evidence
from .evidence import public_device, candidates, related, utc_now, clean_text
from x20ctl.controllers.compatibility import identify_receiver


def capture(scanner, source, selected, action, instruction, category="buttons"):
    if (
        scanner._ask(instruction + " Press/hold/release during five seconds, or Skip.")
        == "skip"
    ):
        return {"action": action, "status": "skipped"}
    result = scanner._step(
        category,
        lambda: scanner.backend.capture(
            scanner,
            source.get("selected", selected),
            source["source"],
            source.get("slot"),
            f"input/phase1/{action}.jsonl",
            action,
            5.0,
        ),
    )
    return result or {"action": action, "status": "unavailable"}


def vendor_session(scanner, rows):
    tests = []
    from .windows import allowed_collection

    vendor_rows = [
        r
        for r in rows
        if r.get("kind") == "hid"
        and r.get("usage_page") == 0xFFA0
        and allowed_collection(r, True)
    ]
    if (
        scanner.options["rawInput"]
        and vendor_rows
        and scanner._ask(
            "Optional D10 receiver vendor INPUT capture? Log its matching Report-6 collection as uninterpreted bytes. No Report-7 output or feature command is sent.",
            "yes",
        )
        == "yes"
    ):
        for index, row in enumerate(vendor_rows):
            scanner._ask(
                "Operate only this controller normally during the next five seconds. No app setting change is required."
            )
            result = scanner._step(
                "raw_input",
                lambda: scanner.backend.capture(
                    scanner,
                    row,
                    "hid_input",
                    None,
                    f"input/phase1/vendor{index}.jsonl",
                    "vendor_input",
                    5.0,
                    vendor_input=True,
                ),
            )
            if result:
                tests.append(result)
    return tests


def input_sessions(scanner, source, selected, rows=()):
    tests = []
    from x20ctl.controllers import REGISTRY
    model = REGISTRY.get(scanner.options["model"].lower().replace(" ", "_"))
    slots = model.macro_slots if model else ()
    if scanner.options["model"].lower() == "x05" and scanner._ask("Earlier findings describe X05 M1/M2, but this profile's artwork has none. Does YOUR revision physically have M1/M2, and can you safely use its documented programming procedure?", "yes") == "yes":
        slots = ("M1", "M2")
        scanner._write("device/owner-rear-controls.json", {"slots": list(slots), "evidenceType": "owner_reported", "revisionVerified": False})
    if (
        scanner._ask(
            "Repeat button mapping twice, including each physical rear/M button? This records inputs, not configuration commands.",
            "yes",
        )
        == "yes"
    ):
        from .input_tests import ACTIONS
        from x20ctl.controllers import REGISTRY

        model = REGISTRY.get(scanner.options["model"].lower().replace(" ", "_"))
        controls = [
            (key, text)
            for key, text in ACTIONS
            if key not in {"neutral", "LT", "RT", "left_stick", "right_stick"}
        ]
        controls += [
            (slot, f"Press only physical {slot}; do not change its assignment")
            for slot in slots
        ]
        for repeat in (1, 2):
            tests.append(
                capture(
                    scanner,
                    source,
                    selected,
                    f"repeat{repeat}_neutral",
                    "Leave all controls released",
                    "neutral",
                )
            )
            for key, instruction in controls:
                tests.append(
                    capture(
                        scanner,
                        source,
                        selected,
                        f"repeat{repeat}_{key}",
                        instruction,
                        "rear_outputs" if key.startswith("M") else "buttons",
                    )
                )
    if (
        scanner._ask(
            "Does this controller have a PHYSICAL long/short trigger-mode switch you can safely operate? No software command will be sent.",
            "yes",
        )
        == "yes"
    ):
        for mode in ("long", "short"):
            if (
                scanner._ask(
                    f"Set the physical trigger switch to {mode} mode. Skip if unsupported or uncertain."
                )
                == "skip"
            ):
                continue
            for key, instruction in [
                ("neutral", "Leave LT/RT released"),
                (
                    "LT_sweep",
                    "Keep RT released; sweep LT smoothly from released to full and back",
                ),
                (
                    "RT_sweep",
                    "Keep LT released; sweep RT smoothly from released to full and back",
                ),
                ("LT_RT", "Pull LT and RT simultaneously, then release"),
            ]:
                tests.append(
                    capture(
                        scanner,
                        source,
                        selected,
                        f"{mode}_{key}",
                        instruction,
                        "triggers",
                    )
                )
        scanner._ask(
            "Restore your original physical trigger-mode switch positions before continuing."
        )
    if (
        slots
        and scanner._ask(
            "Optional macro playback test: can you program rear buttons using an ALREADY working physical procedure/app AND restore the original assignments? No programming commands or host protocol capture are provided here.",
            "yes",
        )
        == "yes"
    ):
        original = scanner._ask(
            "Record the original rear assignments and how you will restore them. Skip if unknown.",
            "text",
        )
        if original and original != "skip":
            events = []
            try:
                for slot in slots:
                    for pattern in ("baseline", "clear_before", "A", "clear", "B", "A_B", "clear_after"):
                        operation = "leave the original assignment unchanged on" if pattern == "baseline" else "clear" if pattern.startswith("clear") else "assign " + pattern.replace("_", " then ") + " to"
                        instruction = f"Using your known procedure, {operation} {slot}. Then press only {slot} twice to capture playback. Skip unsupported patterns."
                        result = capture(
                            scanner,
                            source,
                            selected,
                            f"{slot}_{pattern}",
                            instruction,
                            "rear_outputs",
                        )
                        tests.append(result)
                        events.append(
                            {
                                "slot": slot,
                                "pattern": pattern,
                                "timestamp": utc_now(),
                                "result": result,
                            }
                        )
                        scanner._write(
                            "experiments/macro-playback.json",
                            {
                                "original": original,
                                "events": events,
                                "programming": "owner_performed",
                                "outboundProtocolCaptured": False,
                                "restorationConfirmed": False,
                            },
                        )
            finally:
                # On cancellation, preserve the restoration instruction for the owner.
                scanner._write(
                    "experiments/RESTORE-MACROS.txt",
                    "Restore original assignments using your known procedure: "
                    + original,
                )
            restored = scanner._ask(
                "Restore ALL original rear assignments. Continue only when restored; Skip if restoration could not be confirmed."
            )
            scanner._write(
                "experiments/macro-playback.json",
                {
                    "original": original,
                    "events": events,
                    "programming": "owner_performed",
                    "outboundProtocolCaptured": False,
                    "restorationConfirmed": restored != "skip",
                },
            )
    if (
        source["source"] == "xinput_state"
        and scanner._ask(
            "Optional standard XInput vibration test on the SELECTED slot? Off/low/medium/high/maximum each last 0.35 seconds, then motors stop. This may drive grip or controller-linked motors; it cannot independently address trigger motors. Hold the controller securely. If a pulse does not stop, disconnect this controller. Skip if uncomfortable. No stored strength setting is written.",
            "yes",
        )
        == "yes"
    ):
        events = []
        for name, level in (
            ("off", 0),
            ("low", 0.25),
            ("medium", 0.5),
            ("high", 0.75),
            ("maximum", 1),
        ):
            if (
                scanner._ask(
                    f"Ready for {name} vibration? Continue to run one 0.35-second pulse, or Skip."
                )
                == "skip"
            ):
                continue
            result = scanner._step(
                "vibration",
                lambda: scanner.backend.rumble(source["slot"], level, level, True),
            )
            note = scanner._ask(
                "Describe physical feedback (none/left/right/both/linked triggers). API acceptance alone does not prove motor behavior.",
                "text",
            )
            events.append(
                {
                    "level": name,
                    "apiResult": result,
                    "ownerObservation": note,
                    "timestamp": utc_now(),
                }
            )
            scanner._write(
                "experiments/vibration.json", {"tests": events, "vendorCommands": False}
            )
            if result is None:
                break
        if (
            events
            and events[-1]["apiResult"]
            and scanner._ask(
                "Also test LEFT then RIGHT grip channels separately at low strength?",
                "yes",
            )
            == "yes"
        ):
            for side in ("left", "right"):
                if (
                    scanner._ask(f"Ready for low {side}-channel pulse, or Skip?")
                    == "skip"
                ):
                    continue
                result = scanner._step(
                    "vibration",
                    lambda: scanner.backend.rumble(
                        source["slot"],
                        0.25 if side == "left" else 0,
                        0.25 if side == "right" else 0,
                        True,
                    ),
                )
                note = scanner._ask(
                    "Describe which parts physically vibrated; linked trigger feedback is not independent trigger control.",
                    "text",
                )
                events.append(
                    {
                        "channel": side,
                        "apiResult": result,
                        "ownerObservation": note,
                        "timestamp": utc_now(),
                    }
                )
                scanner._write(
                    "experiments/vibration.json",
                    {"tests": events, "vendorCommands": False},
                )
                if result is None:
                    break
    return tests


def write_mapping(scanner, tests):
    actions = []
    mode_samples = {"long": [], "short": []}
    for result in tests:
        if not result.get("file"):
            continue
        rows = [
            json.loads(line)
            for line in (scanner.output / result["file"]).read_text().splitlines()
        ]
        for mode in mode_samples:
            if result["action"].startswith(mode + "_"):
                mode_samples[mode].extend(rows)
        actions.append(
            control_evidence(
                result["action"],
                rows,
                scanner.options["model"].lower(),
                getattr(scanner, "hid_layout_known", False),
            )
        )
    scanner._write(
        "input/trigger-modes.json",
        {
            mode: trigger_evidence(
                samples,
                scanner.options["model"].lower(),
                getattr(scanner, "hid_layout_known", False),
            )
            for mode, samples in mode_samples.items()
        },
    )
    scanner._write(
        "input/button-map.json",
        {
            "actions": actions,
            "protocolCapture": False,
            "independentRearInputs": "unknown",
            "scope": "Action/output correlations; repeated consistency does not prove independent rear inputs",
        },
    )
    scanner._write(
        "input/protocol-capture-status.json",
        {
            "inputCapture": True,
            "outboundUSBHIDCapture": False,
            "otherAppBLECapture": False,
            "externalTraceRequired": True,
            "unknownVendorCommandsTransmitted": False,
        },
    )


def identity_sessions(scanner):
    if (
        scanner._ask(
            "Optional: capture identities in other modes (XInput/DInput, wired/2.4G/Bluetooth or documented firmware mode)? Use only manufacturer-documented physical mode changes; no updater will run.",
            "yes",
        )
        != "yes"
    ):
        return
    records = []
    for index in range(5):
        choice = scanner._ask(
            "Choose the next mode to enumerate, or Skip to finish.",
            "choice",
            [
                "Wired XInput",
                "Wired DInput/native",
                "2.4G receiver",
                "Bluetooth",
                "Documented firmware mode",
            ],
        )
        if choice == "skip":
            break
        mode = ["wired_xinput", "wired_dinput", "receiver", "bluetooth", "firmware"][
            int(choice)
        ]
        if (
            mode == "firmware"
            and scanner._ask(
                "Have you confirmed the exact firmware-mode button sequence for YOUR model/revision in its manufacturer's manual? Enumeration only: do NOT run or click Update, flash, reset or calibrate.",
                "yes",
            )
            != "yes"
        ):
            continue
        scanner._ask(
            "Disconnect ONLY this controller/receiver. Continue when disconnected."
        )
        before = scanner._step("identity", scanner.backend.inventory)
        scanner._ask(
            f"Reconnect the controller in {mode.replace('_', ' ')} mode using its documented procedure. Do not start an updater. Continue when enumerated."
        )
        after = scanner._step("identity", scanner.backend.inventory)
        if not before or not after:
            continue
        options = candidates(before["devices"], after["devices"])
        groups = {}
        for row in options:
            groups.setdefault(row.get("_parent") or row["_key"], row)
        options = list(groups.values())
        if not options:
            records.append(
                {"mode": mode, "status": "no_correlated_device", "timestamp": utc_now()}
            )
            continue
        selected = scanner._ask(
            "Select the new device belonging to this controller; do not guess.",
            "choice",
            [
                clean_text(r.get("product") or r.get("description") or "Device")
                for r in options
            ],
        )
        if selected == "skip":
            continue
        target = options[int(selected)]
        rows = (
            scanner._step(
                "identity",
                lambda: scanner.backend.details(related(after["devices"], target)),
            )
            or []
        )
        records.append(
            {
                "mode": mode,
                "devices": [public_device(r) for r in rows],
                "receiverEvidence": [identify_receiver(r) for r in rows],
                "timestamp": utc_now(),
                "modeClaim": "owner_reported",
            }
        )
        scanner._write(
            "device/mode-identities.json",
            {"sessions": records, "flashAttempted": False},
        )
        if target.get("kind") == "usb":
            data = scanner._step("usb", lambda: scanner.backend.usb(target))
            if data:
                scanner._write(f"usb/mode{index}/metadata.json", data["metadata"])
                for name, value in data["binaries"].items():
                    scanner._write(
                        f"usb/mode{index}/" + name, bytes.fromhex(value), True
                    )
        if mode == "bluetooth":
            scanner._ble()
        scanner._ask(
            "Leave firmware mode if entered, and restore your normal working connection before continuing."
        )
    scanner._write(
        "device/mode-identities.json", {"sessions": records, "flashAttempted": False}
    )
