"""Conservative analysis of action-labelled input, never vendor command inference."""

from x20ctl.desktop.x15_input import decode_hid, decode_xinput


def control_evidence(action, samples, model, hid_layout_known=False):
    observed = set()
    variants = set()
    xinput = False
    for sample in samples:
        try:
            if sample.get("source") == "xinput_state":
                xinput = True
                parsed = decode_xinput(sample["values"])
            else:
                variants.add(sample.get("report_hex", ""))
                if model != "x15" or not hid_layout_known:
                    continue
                parsed = decode_hid(bytes.fromhex(sample["report_hex"]))
            observed.update(parsed["buttons"])
            observed.update(
                name
                for name, key in (("LT", "leftTrigger"), ("RT", "rightTrigger"))
                if parsed[key] > 0
            )
        except (ValueError, KeyError, TypeError):
            continue
    return {
        "action": action,
        "observed_controls": sorted(observed),
        "raw_report_variants": len(variants),
        "independent_rear_input": "not_exposed_by_xinput" if xinput else "unknown",
        "configuration_protocol_verified": False,
        "scope": "Observed outputs; a rear action may be an alias or firmware macro",
    }


def trigger_evidence(samples, model, hid_layout_known=False):
    axes = {"LT": set(), "RT": set()}
    digital = {"LT": set(), "RT": set()}
    for sample in samples:
        try:
            if sample.get("source") == "xinput_state":
                values = sample["values"]
                for key, field in (("LT", "lt"), ("RT", "rt")):
                    axes[key].add(values[field])
            elif model == "x15" and hid_layout_known:
                state = decode_hid(bytes.fromhex(sample["report_hex"]))
                for key, field in (("LT", "leftTrigger"), ("RT", "rightTrigger")):
                    axes[key].add(round(state[field] * 255))
                    digital[key].add(key in state["buttons"])
        except (KeyError, TypeError, ValueError):
            continue
    result = {}
    for key in axes:
        values = sorted(axes[key])
        intermediate = [v for v in values if 0 < v < 255]
        representation = (
            "axis_and_digital_bit_observed"
            if len(digital[key]) > 1
            else "intermediate_axis_values_observed"
            if intermediate
            else "endpoint_only_axis_observed"
            if len(values) > 1
            else "insufficient_movement"
            if values
            else "unknown"
        )
        result[key] = {
            "representation": representation,
            "axis_values": values,
            "intermediate_values": intermediate,
            "interpretation": "Reported representation only; hardware mechanism and vendor mode commands remain unknown",
        }
    return result
