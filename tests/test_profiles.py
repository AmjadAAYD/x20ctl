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


class FakePad:
    """Records what a profile asks the controller to do."""

    def __init__(self, motors=3, macros=0x0F):
        self._caps = p.Capabilities(
            sticks=3, triggers=3, motors=motors, turbo=0, macros=macros,
            changekey=1, lighting=0, eq=0, nfc=0, sensor=0)
        self.calls = []

    async def capabilities(self):
        return self._caps

    async def set_vibration(self, percent):
        self.calls.append(("vibration", percent))

    async def set_macro(self, slot, keys, **kw):
        self.calls.append(("set", slot, keys))

    async def clear_macro(self, slot):
        self.calls.append(("clear", slot))


def _run(coro):
    import asyncio
    return asyncio.run(coro)


def test_applying_makes_the_pad_match_the_save_file():
    """The bug this guards: switching save files must switch the controller.

    A profile that defines only M1 has to clear M2-M4, otherwise the previous
    profile's macros stay live on the pad while the app shows the new file."""
    profile = Profile(name="two")
    profile.macros["M1"] = MacroSpec(keys="X")
    pad = FakePad()
    _run(profile.apply(pad))

    assert ("set", 1, "X") in pad.calls
    for slot in (2, 3, 4):
        assert ("clear", slot) in pad.calls, f"M{slot} was left holding stale data"


def test_clear_undefined_can_be_turned_off():
    profile = Profile(name="keep", clear_undefined=False)
    profile.macros["M1"] = MacroSpec(keys="X")
    pad = FakePad()
    _run(profile.apply(pad))
    assert not any(call[0] == "clear" for call in pad.calls)


def test_clear_undefined_defaults_on_for_old_files_without_the_field():
    """Files written before the field existed must still behave correctly."""
    profile = Profile.from_dict({"name": "old", "macros": {"M1": {"keys": "A"}}})
    assert profile.clear_undefined is True


def test_apply_skips_settings_the_pad_does_not_expose():
    profile = Profile(name="x", vibration=50)
    profile.macros["M1"] = MacroSpec(keys="A")
    pad = FakePad(motors=0, macros=0)
    report = _run(profile.apply(pad))
    assert not pad.calls
    assert any("vibration skipped" in line for line in report)
    assert any("macros skipped" in line for line in report)


def test_per_step_timing_is_parsed():
    """The hardware stores a duration per step, so the syntax exposes one."""
    steps = MacroSpec(keys="A:150/40, B:80").steps()
    # press, release, press, release
    assert len(steps) == 4
    assert steps[0].duration_ms == 150
    assert steps[1].duration_ms == 40
    assert steps[2].duration_ms == 80


def test_steps_without_timing_use_the_defaults():
    steps = MacroSpec(keys="A:150, B", hold_ms=100, gap_ms=60).steps()
    assert steps[0].duration_ms == 150      # stated
    assert steps[1].duration_ms == 60       # default gap
    assert steps[2].duration_ms == 100      # default hold


def test_timing_syntax_errors_are_explained():
    for bad, expect in (("A:abc", "timing"), ("A:33", "multiples of 5"),
                        ("A:0", "positive"), (":150", "no keys")):
        try:
            MacroSpec(keys=bad).validate()
        except ValueError as exc:
            assert expect in str(exc), f"{bad!r} gave {exc}"
        else:
            raise AssertionError(f"{bad!r} should have been rejected")


def test_recorded_macro_keeps_the_timing_it_captured():
    """Regression: the recorder used to average every step into one hold and
    one gap, which discarded the performance it had just captured."""
    from x20ctl import protocol as p
    from x20ctl.input import MacroRecorder, RecordedStep, XInputReader

    recorder = MacroRecorder(XInputReader())
    recorder.steps = [
        RecordedStep(keys=[p.Key.A], duration_ms=145),
        RecordedStep(keys=[], duration_ms=35),
        RecordedStep(keys=[p.Key.B], duration_ms=90),
    ]
    recorder._last = None
    recorder._recording = True
    spec = recorder.stop()

    assert "A:145/35" in spec.keys
    assert "B:90" in spec.keys
    steps = spec.steps()
    assert steps[0].duration_ms == 145
    assert steps[1].duration_ms == 35
    assert steps[2].duration_ms == 90


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
