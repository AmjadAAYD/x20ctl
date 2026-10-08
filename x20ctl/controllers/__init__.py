"""Published hardware profiles, independent of transport implementations."""
from dataclasses import dataclass
import json
from pathlib import Path


@dataclass(frozen=True)
class ControllerProfile:
    id: str
    name: str
    macro_slots: tuple[str, ...]
    backend: str | None
    visible: bool
    hardware: dict
    visual: dict
    input_backend: str | None = None
    availability: str = "preview"
    known: tuple[str, ...] = ()
    missing: tuple[str, ...] = ()


REGISTRY = {
    item["id"]: ControllerProfile(
        item["id"], item["name"], tuple(item["macroSlots"]),
        item["backend"], item["visible"], item["hardware"], item["visual"], item.get("inputBackend"), item.get("availability", "preview"), tuple(item.get("known", [])), tuple(item.get("missing", [])),
    )
    for item in json.loads(Path(__file__).with_name("catalog.json").read_text(encoding="utf-8"))
}


def get_profile(controller_id: str) -> ControllerProfile:
    try:
        return REGISTRY[controller_id]
    except (KeyError, TypeError):
        raise ValueError("Unknown controller model") from None
