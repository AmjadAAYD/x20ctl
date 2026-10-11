"""One shape for "what a controller says about itself", and a comparison of two.

A fingerprint holds only what the pad reports: its 0x91 identity, its B0
descriptor (raw, so bits understood later can still be read) and the B0 lists.
It names no product, so a fingerprint can be collected from anything and
compared with anything without implying that the two are the same model.

Sources: a live connection (`collect`), a captured exchange (`from_discovery`)
and the recorded X20 below (`X20_REFERENCE`).
"""

from __future__ import annotations

from x20ctl import protocol as p

LISTS = ("changekeySources", "turboKeys", "macroKeys", "macroLimits", "selectList")
_LIST_KINDS = {
    "changekeySources": p.MenuKind.CHANGEKEY_SUPPORT,
    "turboKeys": p.MenuKind.TURBO_SUPPORT,
    "macroKeys": p.MenuKind.MACRO_SUPPORT,
    "macroLimits": p.MenuKind.MACRO_LIMITS,
    "selectList": p.MenuKind.CHANGEKEY_SELECT_LIST,
}


def identity_of(info: p.DeviceInfo) -> dict:
    return {
        "internalVendorId": info.vid, "internalProductId": info.pid,
        "version": info.version, "deviceId": info.device_id,
        "mode": info.mode, "sensor": info.sensor,
        "rawGeneration": info.raw_generation,
        "effectiveGeneration": info.effective_generation,
        "record": info.record,
    }


def descriptor_of(caps: p.Capabilities) -> dict:
    return {
        "raw": caps.raw,
        "unexplainedBits": [p.descriptor_bit_label(*bit) for bit in caps.unexplained_bits()],
        "macroLimitDiscovery": caps.new_macro_engine,
        "remapSelectList": caps.remap_select_list,
    }


def fingerprint(info: p.DeviceInfo | None, caps: p.Capabilities | None,
                lists: dict | None = None) -> dict:
    return {
        "identity": identity_of(info) if info else None,
        "descriptor": descriptor_of(caps) if caps else None,
        "lists": {name: (lists or {}).get(name) for name in LISTS},
    }


async def collect(pad) -> dict:
    """Read a fingerprint from a connected pad. Sends identification only:
    0x91 and B0 queries, which the official app sends to any controller."""
    info = await pad.device_info()
    caps = await pad.capabilities()
    lists: dict = {}
    for name, kind in _LIST_KINDS.items():
        # the app asks for these two only when the descriptor says to
        if name == "macroLimits" and not caps.new_macro_engine:
            continue
        if name == "selectList" and not caps.remap_select_list:
            continue
        try:
            body = await pad.read_body(p.Op.HOST_MENU, bytes([0, kind]))
        except Exception:
            body = None
        if body is None:
            continue
        lists[name] = _decode_list(name, body.data)
    return fingerprint(info, caps, lists)


def _decode_list(name: str, data: bytes):
    if name == "macroLimits":
        return data.hex()           # layout not decoded yet; kept whole
    try:
        return p.decode_key_list(data)
    except ValueError:
        return {"undecoded": data.hex()}


def from_discovery(profile: dict) -> dict:
    """Turn one capability_discovery profile (a captured exchange) into a fingerprint."""
    lists = {"changekeySources": profile.get("supportedSourceCodes"),
             "selectList": profile.get("supportedDestinationCodes")}
    for name in ("turboKeys", "macroKeys", "macroLimits"):
        lists[name] = profile.get(name)
    return {"identity": profile.get("identity"),
            "descriptor": profile.get("descriptor"),
            "lists": {name: lists.get(name) for name in LISTS}}


def _reference() -> dict:
    """The owner's X20 on firmware 1.23, from recorded replies (docs/01-protocol.md)."""
    info = p.parse_identity_reply(bytes.fromhex("0710132009012352"))
    caps = p.parse_capabilities(p.unwrap(bytes.fromhex("0a030303000f010002c000")))
    return fingerprint(info, caps, {
        "changekeySources": list(p.CHANGEKEY_DEFAULT_SOURCES),
        "turboKeys": p.decode_key_list(bytes.fromhex("0c01880d84")),
        "macroKeys": list(p.X20_MACRO_KEYS),
        "macroLimits": "2a002a002a002a000000000000000000",
    })


X20_REFERENCE = _reference()


def compare(a: dict, b: dict) -> list[dict]:
    """Every reported difference between two fingerprints, most telling first.

    Each item is {"area", "field", "a", "b", "meaning"}. Areas, in order:
    "protocol" (generation, mode, engine bits, lists the engines read),
    "identity", "features" (descriptor bits) and "unexplained" (bits nobody has
    traced, which are exactly the ones worth looking at). A difference only
    says what differs; whether the same code can drive both is for hardware to
    show.
    """
    out: list[dict] = []
    ia, ib = a.get("identity") or {}, b.get("identity") or {}
    for field, area, meaning in (
        ("effectiveGeneration", "protocol", "the app picks command variants by this"),
        ("rawGeneration", "protocol", "generation as sent"),
        ("mode", "protocol", "operating mode; the app routes on it"),
        ("internalVendorId", "identity", ""), ("internalProductId", "identity", ""),
        ("deviceId", "identity", "top nibble of the product id (the app's device_id)"),
        ("version", "identity", "firmware"), ("sensor", "identity", "sensor flag in the identity"),
    ):
        if ia.get(field) != ib.get(field):
            out.append(_diff(area, field, ia.get(field), ib.get(field), meaning))

    da, db = a.get("descriptor") or {}, b.get("descriptor") or {}
    ra = bytes.fromhex(da.get("raw") or "")
    rb = bytes.fromhex(db.get("raw") or "")
    if ra != rb:
        if len(ra) != len(rb):
            out.append(_diff("protocol", "descriptor length", len(ra), len(rb),
                             "a longer descriptor carries bytes nothing here decodes"))
        for offset in range(max(len(ra), len(rb))):
            va = ra[offset] if offset < len(ra) else None
            vb = rb[offset] if offset < len(rb) else None
            if va == vb:
                continue
            for bit in range(8):
                ba = None if va is None else bool(va >> bit & 1)
                bb = None if vb is None else bool(vb >> bit & 1)
                if ba == bb:
                    continue
                name = p.DESCRIPTOR_BIT_NAMES.get((offset, bit))
                area = ("protocol" if (offset, bit) in p.DESCRIPTOR_ENGINE_BITS
                        else "features" if name else "unexplained")
                out.append(_diff(area, p.descriptor_bit_label(offset, bit), ba, bb, name or "not traced"))

    la, lb = a.get("lists") or {}, b.get("lists") or {}
    for name in LISTS:
        xa, xb = la.get(name), lb.get(name)
        if xa == xb:
            continue
        if isinstance(xa, list) and isinstance(xb, list):
            only_a = [k for k in xa if k not in xb]
            only_b = [k for k in xb if k not in xa]
            detail = (f"only in A: {only_a or 'none'}; only in B: {only_b or 'none'}"
                      if only_a or only_b else "same keys, different order")
            out.append(_diff("protocol", name, xa, xb, detail))
        else:
            out.append(_diff("protocol", name, xa, xb, "missing on one side" if xa is None or xb is None else ""))

    rank = {"protocol": 0, "identity": 1, "features": 2, "unexplained": 3}
    return sorted(out, key=lambda d: rank[d["area"]])


def _diff(area, field, a, b, meaning):
    return {"area": area, "field": field, "a": a, "b": b, "meaning": meaning}


def render(differences: list[dict], a_name: str = "A", b_name: str = "B") -> str:
    if not differences:
        return "No differences in anything either controller reports."
    lines, area = [], None
    for d in differences:
        if d["area"] != area:
            area = d["area"]
            lines.append(f"\n{area.upper()}")
        note = f"  ({d['meaning']})" if d["meaning"] else ""
        lines.append(f"  {d['field']}: {a_name}={d['a']}  {b_name}={d['b']}{note}")
    return "\n".join(lines).lstrip("\n")
