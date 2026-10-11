"""The Linux PC mode engine (tools/linux_controller_check/x20ctl-pcmode.py), against the same cases as the Windows
engine's PadProcessorTests.cs, so the two can't drift apart. Pure logic: runs on any OS, no controller needed."""
import importlib.util, json, struct, sys
from pathlib import Path

import pytest

_PATH = Path(__file__).resolve().parents[1] / "tools" / "linux_controller_check" / "x20ctl-pcmode.py"
_spec = importlib.util.spec_from_file_location("x20ctl_pcmode", _PATH)
pm = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(pm)


def pad(buttons=0, lt=0, rt=0, lx=0, ly=0, rx=0, ry=0): return (buttons, lt, rt, lx, ly, rx, ry)
def curve(inner=0, outer=100, x1=85, y1=85, x2=170, y2=170): return {"Preset": "custom", "BasePreset": "default", "Inner": inner, "Outer": outer, "X1": x1, "Y1": y1, "X2": x2, "Y2": y2}
def profile(**kw): p = pm.new_profile(); p.update(kw); return p


def test_neutral_passes_everything_through():
    s = pad(pm.A | pm.LB, 200, 10, 12000, -8000, -300, 32767)
    assert pm.PadProcessor(pm.new_profile()).process(s, 0) == s


def test_remap_and_swap():
    assert pm.PadProcessor(profile(Remap={"A": "B"})).process(pad(pm.A), 0)[0] == pm.B
    p = pm.PadProcessor(profile(Remap={"A": "B", "B": "A"}))
    assert p.process(pad(pm.A), 0)[0] == pm.B
    assert p.process(pad(pm.B), 1)[0] == pm.A
    assert p.process(pad(pm.A | pm.B), 2)[0] == pm.A | pm.B


def test_button_to_trigger_and_trigger_to_button():
    p = pm.PadProcessor(profile(Remap={"LB": "LT", "RT": "X"}))
    assert p.process(pad(pm.LB), 0)[1] == 255
    out = p.process(pad(rt=180), 1)
    assert out[0] == pm.X and out[2] == 0


def test_analog_trigger_keeps_its_value():
    assert pm.PadProcessor(profile(Remap={"A": "B"})).process(pad(lt=143), 0)[1] == 143


def test_turbo_toggles_and_restarts_on():
    p = pm.PadProcessor(profile(Turbo={"A": 10}))
    seen = [bool(p.process(pad(pm.A), t)[0] & pm.A) for t in range(0, 400, 10)]
    assert True in seen and False in seen
    assert 6 <= sum(a != b for a, b in zip(seen, seen[1:])) <= 9
    assert p.process(pad(), 410)[0] == 0
    assert p.process(pad(pm.A), 500)[0] & pm.A


def test_turbo_leaves_other_buttons_alone():
    p = pm.PadProcessor(profile(Turbo={"A": 10}))
    assert all(p.process(pad(pm.A | pm.X), t)[0] & pm.X for t in range(0, 200, 10))


def test_inner_and_outer_deadzones():
    p = pm.PadProcessor(profile(Curves={"LeftStick": curve(inner=20)}))
    assert p.process(pad(lx=4000, ly=3000), 0)[3:5] == (0, 0)
    assert 32000 <= p.process(pad(lx=32767), 1)[3] <= 32767
    out = pm.PadProcessor(profile(Curves={"LeftStick": curve(outer=80)})).process(pad(lx=int(32767 * 0.82)), 0)
    assert 32600 <= out[3] <= 32767


def test_curve_reshapes_and_keeps_direction():
    out = pm.PadProcessor(profile(Curves={"RightStick": curve(x1=35, y1=35, x2=219, y2=145)})).process(pad(rx=16000, ry=16000), 0)
    assert out[5] == out[6] and out[5] != 16000 and out[5] > 0


def test_default_curve_changes_nothing():
    assert pm.PadProcessor(profile(Curves={"LeftStick": curve()})).process(pad(lx=-12345, ly=6789), 0)[3:5] == (-12345, 6789)


def test_trigger_curve():
    p = pm.PadProcessor(profile(Curves={"LeftTrigger": curve(inner=40)}))
    assert p.process(pad(lt=80), 0)[1] == 0
    assert p.process(pad(lt=255), 1)[1] == 255


def test_macro_plays_once_per_press():
    steps = [{"Id": "1", "Buttons": ["A"], "Left": 0, "Right": 0, "HoldMs": 50, "PauseMs": 50},
             {"Id": "2", "Buttons": ["B"], "Left": 1, "Right": 0, "HoldMs": 50, "PauseMs": 0}]
    p = pm.PadProcessor(profile(Macros={"RB": steps}))
    assert p.process(pad(pm.RB), 0)[0] == pm.A
    assert p.process(pad(pm.RB), 60)[0] == 0
    second = p.process(pad(pm.RB), 120)
    assert second[0] == pm.B and second[4] == 32767
    assert p.process(pad(pm.RB), 200)[0] == 0 and p.playing is None
    p.process(pad(), 300)
    assert p.process(pad(pm.RB), 400)[0] == pm.A


def test_bad_profiles_are_refused():
    for bad in (profile(Remap={"A": "NOPE"}), profile(Turbo={"A": 500}), profile(Curves={"LeftStick": curve(inner=90, outer=50)})):
        with pytest.raises(ValueError): pm.PadProcessor(bad)


def test_settings_file_matches_the_windows_format(tmp_path):
    path = tmp_path / "pc-mode.json"
    p = profile(Remap={"A": "B"}, Turbo={"X": 12}, Curves={"LeftStick": pm.curve("slow", 10, 95)}, Macros={"RB": pm.parse_macro("A+B 40 20; X 30 0")})
    pm.save_profile(p, path)
    data = json.loads(path.read_text())
    assert data["version"] == 1 and set(data["profiles"]["default"]) == {"Remap", "Curves", "Turbo", "Macros", "Sticks"}
    assert data["profiles"]["default"]["Curves"]["LeftStick"]["X2"] == 219
    loaded = pm.load_profile(path)
    assert loaded["Macros"]["RB"][0]["Buttons"] == ["A", "B"] and loaded["Macros"]["RB"][1]["HoldMs"] == 30
    pm.save_profile(pm.new_profile(), path)
    assert "default" not in json.loads(path.read_text())["profiles"]


def test_a_windows_written_file_loads():
    # what X20Ctl.Zone/PcMode/PcModeStore.cs writes (System.Text.Json, default casing, enums as numbers)
    text = '{"version":1,"profiles":{"default":{"Remap":{"A":"B"},"Curves":{"LeftStick":{"Preset":"quick","BasePreset":"quick","Inner":5,"Outer":100,"X1":35,"Y1":35,"X2":136,"Y2":204}},"Turbo":{"X":10},"Macros":{"RB":[{"Id":"3f2b","Buttons":["A"],"Left":3,"Right":0,"HoldMs":40,"PauseMs":20}]}}}}'
    import tempfile, os
    with tempfile.TemporaryDirectory() as d:
        path = Path(d) / "pc-mode.json"; path.write_text(text)
        p = pm.load_profile(path)
    out = pm.PadProcessor(p).process(pad(pm.RB), 0)
    assert out[0] == pm.A and out[3] == 32767  # the macro's step pushes the left stick right


def test_macro_text_errors_are_plain():
    with pytest.raises(ValueError, match="Couldn't read"): pm.parse_macro("Q 50 30")


def test_uinput_and_evdev_layouts():
    assert pm.USER_DEV.size == 1116                      # sizeof(struct uinput_user_dev)
    assert pm.EVENT.size in (16, 24)                     # struct input_event on 32 / 64-bit
    assert pm.UI_SET_EVBIT == 0x40045564 and pm.UI_SET_KEYBIT == 0x40045565 and pm.UI_SET_ABSBIT == 0x40045567
    assert pm.UI_DEV_CREATE == 0x5501 and pm.UI_DEV_DESTROY == 0x5502
    assert pm.EVIOCGRAB == 0x40044590 and pm.EVIOCGID == 0x80084502 and pm.EVIOCGABS(0) == 0x80184540
    name, bus, vid, pid, ver, ff, *rest = pm.USER_DEV.unpack(pm.user_dev_bytes())
    assert name.rstrip(b"\0") == pm.VIRTUAL_NAME.encode() and (vid, pid) == (0x045E, 0x028E)
    absmax, absmin = rest[:64], rest[64:128]
    assert (absmin[pm.ABS_X], absmax[pm.ABS_X], absmax[pm.ABS_Z], absmin[pm.ABS_HAT0Y]) == (-32768, 32767, 255, -1)


def test_axis_normalisation():
    assert pm.norm_stick(0, 0, 255, False) == -32768 and pm.norm_stick(255, 0, 255, False) == 32767
    assert pm.norm_stick(-32768, -32768, 32767, True) == 32767   # Linux up (min) becomes X20CTL up (+)
    assert pm.norm_trigger(1023, 0, 1023) == 255 and pm.norm_trigger(0, 0, 1023) == 0


def test_fine_dead_zones_in_tenths():
    p = pm.PadProcessor(profile(Sticks={"LeftStick": {"Inner": 4.5, "Outer": 100, "SquareGate": False, "AxisRatio": 100}}))
    assert p.process(pad(lx=int(32768 * .044)), 0)[3] == 0
    assert p.process(pad(lx=int(32768 * .047)), 1)[3] > 0
    assert abs(pm.response(None, {"Inner": 4.5, "Outer": 95.5}, .5) - .5) < 1e-4


def test_square_gate_and_axis_ratio():
    sq = pm.PadProcessor(profile(Sticks={"RightStick": {"SquareGate": True}}))
    out = sq.process(pad(rx=23170, ry=23170), 0)
    assert 32700 <= out[5] <= 32767 and 32700 <= out[6] <= 32767
    half = sq.process(pad(rx=16384), 1)
    assert 16300 <= half[5] <= 16450 and half[6] == 0
    ratio = pm.PadProcessor(profile(Sticks={"LeftStick": {"AxisRatio": 50}})).process(pad(lx=16000, ly=16000), 0)
    assert 15950 <= ratio[3] <= 16050 and 7950 <= ratio[4] <= 8050


def test_anti_deadzone_max_output_and_raw():
    s = dict(pm.new_tuning(), Inner=5.0, AntiDeadzone=10.0, MaxOutput=90.0)
    assert pm.response(None, s, 0.04) == 0
    assert 0.10 <= pm.response(None, s, 0.051) <= 0.102
    assert abs(pm.response(None, s, 1.0) - 0.9) < 1e-4
    raw = pm.PadProcessor(profile(Sticks={"LeftStick": {"Raw": True, "Inner": 9.0}}))
    out = raw.process(pad(lx=1234, ly=-4321), 0)
    assert (out[3], out[4]) == (1234, -4321)
    assert not pm.is_neutral(profile(Sticks={"LeftStick": {"Raw": True}}))
    for bad in ({"LeftStick": {"AntiDeadzone": 50}}, {"LeftStick": {"MaxOutput": 20}}):
        with pytest.raises(ValueError): pm.PadProcessor(profile(Sticks=bad))


def test_stick_tuning_is_a_setting_and_bad_values_are_refused():
    assert not pm.is_neutral(profile(Sticks={"LeftStick": {"SquareGate": True}}))
    assert pm.is_neutral(profile(Sticks={"LeftStick": pm.new_tuning()}))
    for bad in ({"LeftStick": {"AxisRatio": 300}}, {"LT": {"Inner": 2}}):
        with pytest.raises(ValueError): pm.PadProcessor(profile(Sticks=bad))


def test_a_windows_file_with_stick_tuning_loads(tmp_path):
    # PcModeStore's JSON for StickTuning (IsDefault is [JsonIgnore])
    text = '{"version":1,"profiles":{"default":{"Remap":{},"Curves":{},"Turbo":{},"Macros":{},"Sticks":{"LeftStick":{"Inner":3.7,"Outer":100,"SquareGate":true,"AxisRatio":80}}}}}'
    path = tmp_path / "pc-mode.json"; path.write_text(text)
    p = pm.load_profile(path)
    assert p["Sticks"]["LeftStick"]["Inner"] == 3.7 and not pm.is_neutral(p)
    old = tmp_path / "old.json"; old.write_text('{"version":1,"profiles":{"default":{"Remap":{"A":"B"},"Curves":{},"Turbo":{},"Macros":{}}}}')
    assert pm.load_profile(old)["Sticks"] == {}


# --- sharing, the View + Menu switch and the controller mouse (Windows: MouseAndShareTests.cs) ---

def test_share_code_and_file_round_trip_without_the_name():
    p = profile(Remap={"A": "B"}, Turbo={"X": 12}, Name="My game")
    code = pm.to_code(p)
    assert code.startswith("X20PC1.") and "=" not in code and "+" not in code and "/" not in code
    back = pm.from_code(code)
    assert back["Remap"] == {"A": "B"} and back["Turbo"] == {"X": 12} and "Name" not in back
    text = pm.to_file_text(p)
    assert json.loads(text)["x20ctlPcMode"] == 1 and "My game" not in text
    assert pm.from_file_text(text)["Remap"] == {"A": "B"}


WINDOWS_CODE = "X20PC1.q1YKSs1NLFCyqlZyVLJSclKq1VFyLi0qSy0GCgHZIaVFSfkQpm9iclE-VDi4JDM5G8r2S8xNVbLKK83J0VHyLPZLLS0pSsxRskpLzClOrQUA"


def test_a_code_made_on_windows_imports():
    # made by PcModeShare.ToCode on Windows for { Remap = { A = B } }
    assert pm.from_code(WINDOWS_CODE)["Remap"] == {"A": "B"}


@pytest.mark.parametrize("bad", ["hello", "X20PC1.", "X20PC1.!!!!", "X20PC1." + "A" * 70000], ids=["not-a-code", "empty", "garbage", "too-long"])
def test_hostile_codes_are_refused(bad):
    with pytest.raises(ValueError): pm.from_code(bad)


def test_hostile_files_are_refused():
    import zlib, base64
    for text in ["[]", "{}", '{"x20ctlPcMode":2,"profile":{}}', '{"x20ctlPcMode":1,"profile":{"Remap":{"A":"NOPE"}}}', '{"x20ctlPcMode":1,"profile":{"Turbo":{"A":999}}}']:
        with pytest.raises(ValueError): pm.from_file_text(text)
    bomb = zlib.compressobj(9, zlib.DEFLATED, -15); packed = bomb.compress(b" " * 10_000_000) + bomb.flush()
    with pytest.raises(ValueError): pm.from_code("X20PC1." + base64.urlsafe_b64encode(packed).decode().rstrip("="))


def test_hold_combo_fires_once_per_hold():
    c = pm.HoldCombo(pm.BACK | pm.START, 1000)
    assert not c.update(pm.BACK | pm.START, 0) and not c.update(pm.BACK | pm.START, 999)
    assert c.update(pm.BACK | pm.START, 1000) and not c.update(pm.BACK | pm.START, 3000)
    assert not c.update(pm.BACK, 3100) and not c.update(pm.BACK | pm.START, 3200) and c.update(pm.BACK | pm.START, 4200)


def mouse(**kw): m = pm.new_mouse(); m.update(kw); return m


def test_mouse_moves_at_the_set_speed_and_y_is_screen_down():
    mp = pm.MouseMapper(mouse(Deadzone=0, Curve=1, Speed=1000))
    dx, dy, *_ = mp.step(pad(lx=32767), 0.1); assert 99 <= dx <= 100 and dy == 0
    dx, dy, *_ = mp.step(pad(ly=32767), 0.1); assert dx == 0 and dy <= -99


def test_mouse_dead_zone_curve_and_fractions_carry():
    assert pm.MouseMapper(mouse()).step(pad(lx=3000), 0.1)[:2] == (0, 0)
    mp = pm.MouseMapper(mouse(Deadzone=0, Curve=1, Speed=200))
    total = sum(mp.step(pad(lx=3277), 0.01)[0] for _ in range(100))  # 10% push for a second: 20 px, a fifth of a pixel a tick
    assert 19 <= total <= 20
    assert pm.mouse_push(0.5, 0, 2) == pytest.approx(0.25)


def test_mouse_buttons_edges_precision_and_release():
    mp = pm.MouseMapper(mouse(Deadzone=0, Curve=1, Speed=1000))
    assert mp.step(pad(pm.A), 0.01)[4] == [("LeftClick", True)]
    assert mp.step(pad(pm.A), 0.01)[4] == []
    slow = mp.step(pad(pm.A, lt=255, lx=32767), 0.1)
    assert slow[4] == [] and 29 <= slow[0] <= 30
    assert mp.release() == [("LeftClick", False)]


def test_mouse_scrolls_with_the_other_stick_and_can_swap():
    mp = pm.MouseMapper(mouse(Deadzone=0, ScrollSpeed=10))
    assert 119 <= mp.step(pad(ry=32767), 0.1)[2] <= 120  # a notch
    right = pm.MouseMapper(mouse(PointerStick="Right", Deadzone=0, Curve=1, Speed=1000))
    dx, _, wheel, *_ = right.step(pad(rx=32767, ly=-32767), 0.1)
    assert dx >= 99 and wheel < 0


def test_mouse_settings_load_like_windows_and_bad_ones_fall_back(tmp_path):
    f = tmp_path / "mouse.json"
    f.write_text(json.dumps({"PointerStick": "Right", "Speed": 900, "Buttons": {"A": "Enter"}}))
    m = pm.load_mouse(f)
    assert m["PointerStick"] == "Right" and m["Speed"] == 900 and m["Buttons"]["A"] == "Enter" and m["Buttons"]["B"] == "RightClick"
    f.write_text(json.dumps({"Speed": 99999}))
    assert pm.load_mouse(f) == pm.new_mouse()
    f.write_text("not json")
    assert pm.load_mouse(f) == pm.new_mouse()
