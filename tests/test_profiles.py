"""Offline tests for the profile store. No hardware required."""

from __future__ import annotations

import json
import os
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from x20ctl import protocol as p
from x20ctl.profiles import MacroSpec, Profile, ProfileStore, SLOTS, slugify


def test_macro_spec_validates_against_the_real_builder():
    """A saved spec must be buildable, so apply cannot fail on something the
    store already accepted."""
    MacroSpec(keys="A+B").validate()
    MacroSpec(keys="A,B,X").validate()
    MacroSpec(keys="LT+RB,Y", hold_ms=120, gap_ms=80).validate()


def test_macro_spec_rejects_unknown_keys():
    try:
        MacroSpec(keys="NOPE").validate()
    except ValueError as exc:
        assert "unknown key" in str(exc)
        return
    raise AssertionError("expected unknown key to be rejected")


def test_macro_spec_rejects_buttons_the_pad_cannot_macro():
    try:
        MacroSpec(keys="START").validate()
    except ValueError as exc:
        assert "not macro-capable" in str(exc)
        return
    raise AssertionError("Start is not in the pad's macro list")


def test_macro_spec_rejects_durations_off_the_5ms_grid():
    try:
        MacroSpec(keys="A", hold_ms=33).validate()
    except ValueError:
        return
    raise AssertionError("durations must be multiples of 5 ms")


def test_macro_spec_rejects_empty_keys():
    try:
        MacroSpec(keys="").validate()
    except ValueError:
        return
    raise AssertionError("an empty macro must be rejected")


def test_profile_round_trips_through_json():
    profile = Profile(name="Save file 1", vibration=60)
    profile.macros["M1"] = MacroSpec(keys="A+B", hold_ms=100)
    profile.macros["M3"] = MacroSpec(keys="X,Y", hold_ms=120, gap_ms=80)

    restored = Profile.from_dict(json.loads(json.dumps(profile.to_dict())))
    assert restored.name == "Save file 1"
    assert restored.vibration == 60
    assert restored.macros["M1"] == profile.macros["M1"]
    assert restored.macros["M3"].hold_ms == 120
    assert restored.macros["M2"] is None


def test_profile_rejects_unknown_slot():
    try:
        Profile.from_dict({"name": "x", "macros": {"M9": {"keys": "A"}}})
    except ValueError as exc:
        assert "unknown slot" in str(exc)
        return
    raise AssertionError("slots outside M1-M4 must be rejected")


def test_profile_rejects_out_of_range_vibration():
    profile = Profile(name="x", vibration=150)
    try:
        profile.validate()
    except ValueError:
        return
    raise AssertionError("vibration above 100 must be rejected")


def test_profile_validation_names_the_offending_slot():
    profile = Profile(name="x")
    profile.macros["M2"] = MacroSpec(keys="HOME")
    try:
        profile.validate()
    except ValueError as exc:
        assert "M2" in str(exc)
        return
    raise AssertionError("the failing slot should be identified")


def test_store_saves_lists_loads_and_deletes():
    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        assert store.list() == []

        profile = Profile(name="Save file 1", vibration=30)
        profile.macros["M1"] = MacroSpec(keys="A+B")
        path = store.save(profile)
        assert os.path.exists(path)
        assert profile.updated, "saving should stamp a timestamp"

        listed = store.list()
        assert len(listed) == 1 and listed[0].name == "Save file 1"

        loaded = store.load("Save file 1")
        assert loaded.macros["M1"].keys == "A+B"

        store.delete("Save file 1")
        assert store.list() == []


def test_store_skips_malformed_files_rather_than_failing():
    with tempfile.TemporaryDirectory() as tmp:
        store = ProfileStore(tmp)
        store.save(Profile(name="good"))
        with open(os.path.join(tmp, "broken.json"), "w", encoding="utf-8") as fh:
            fh.write("{ not json")
        names = [x.name for x in store.list()]
        assert names == ["good"], "one bad file must not break the listing"


def test_slugify_keeps_filenames_sane():
    assert slugify("Save file 1") == "save-file-1"
    assert slugify("  ???  ") == "profile"


def test_describe_is_readable():
    assert "A + B" in MacroSpec(keys="A+B").describe()
    assert "then" in MacroSpec(keys="A,B").describe()
    assert "loops" in MacroSpec(keys="A", loop_ms=200).describe()


if __name__ == "__main__":
    failures = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print(f"  PASS  {name}")
            except AssertionError as exc:
                failures += 1
                print(f"  FAIL  {name}: {exc}")
    print(f"\n{'all passed' if not failures else f'{failures} failed'}")
    sys.exit(1 if failures else 0)
