using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public sealed class PadProcessorTests
{
    static PadState Pad(ushort buttons = 0, byte lt = 0, byte rt = 0, short lx = 0, short ly = 0, short rx = 0, short ry = 0) => new(buttons, lt, rt, lx, ly, rx, ry);
    static SetupCurve Curve(int inner = 0, int outer = 100, int x1 = 85, int y1 = 85, int x2 = 170, int y2 = 170) => new("custom", "default", inner, outer, x1, y1, x2, y2);

    [Fact]
    public void A_neutral_profile_passes_everything_through()
    {
        var p = new PadProcessor(new());
        var input = Pad(PadButtons.A | PadButtons.LB, 200, 10, 12000, -8000, -300, 32767);
        var output = p.Process(input, 0);
        Assert.Equal(input.Buttons, output.Buttons);
        Assert.Equal(input.LeftTrigger, output.LeftTrigger);
        Assert.Equal(input.RightTrigger, output.RightTrigger); // a light pull below the "pressed" threshold survives
        Assert.Equal((input.LeftX, input.LeftY, input.RightX, input.RightY), (output.LeftX, output.LeftY, output.RightX, output.RightY));
    }

    [Fact]
    public void A_remap_sends_the_button_somewhere_else()
    {
        var profile = new PcModeProfile { Remap = { ["A"] = "B" } };
        var output = new PadProcessor(profile).Process(Pad(PadButtons.A), 0);
        Assert.Equal(PadButtons.B, output.Buttons);
    }

    [Fact]
    public void Two_buttons_swapped_stay_swapped()
    {
        var profile = new PcModeProfile { Remap = { ["A"] = "B", ["B"] = "A" } };
        var p = new PadProcessor(profile);
        Assert.Equal(PadButtons.B, p.Process(Pad(PadButtons.A), 0).Buttons);
        Assert.Equal(PadButtons.A, p.Process(Pad(PadButtons.B), 1).Buttons);
        Assert.Equal(PadButtons.A | PadButtons.B, p.Process(Pad(PadButtons.A | PadButtons.B), 2).Buttons);
    }

    [Fact]
    public void A_button_can_become_a_trigger_and_a_trigger_a_button()
    {
        var profile = new PcModeProfile { Remap = { ["LB"] = "LT", ["RT"] = "X" } };
        var p = new PadProcessor(profile);
        Assert.Equal(255, p.Process(Pad(PadButtons.LB), 0).LeftTrigger);
        var output = p.Process(Pad(rt: 180), 1);
        Assert.Equal(PadButtons.X, output.Buttons);
        Assert.Equal(0, output.RightTrigger);
    }

    [Fact]
    public void An_analog_trigger_keeps_its_value_when_not_remapped()
    {
        var profile = new PcModeProfile { Remap = { ["A"] = "B" } };
        Assert.Equal(143, new PadProcessor(profile).Process(Pad(lt: 143), 0).LeftTrigger);
    }

    [Fact]
    public void Turbo_toggles_the_button_while_held_and_stops_when_released()
    {
        var p = new PadProcessor(new PcModeProfile { Turbo = { ["A"] = 10 } }); // 50 ms on, 50 ms off
        var seen = new List<bool>();
        for (long t = 0; t < 400; t += 10) seen.Add((p.Process(Pad(PadButtons.A), t).Buttons & PadButtons.A) != 0);
        Assert.Contains(true, seen); Assert.Contains(false, seen);
        int changes = seen.Zip(seen.Skip(1), (a, b) => a != b).Count(x => x);
        Assert.InRange(changes, 6, 9);
        Assert.Equal(0, p.Process(Pad(), 410).Buttons);
        // a fresh press starts "on" again
        Assert.NotEqual(0, p.Process(Pad(PadButtons.A), 500).Buttons & PadButtons.A);
    }

    [Fact]
    public void Turbo_leaves_other_buttons_alone()
    {
        var p = new PadProcessor(new PcModeProfile { Turbo = { ["A"] = 10 } });
        for (long t = 0; t < 200; t += 10) Assert.NotEqual(0, p.Process(Pad(PadButtons.A | PadButtons.X), t).Buttons & PadButtons.X);
    }

    [Fact]
    public void An_inner_deadzone_removes_small_stick_movement()
    {
        var profile = new PcModeProfile { Curves = { ["LeftStick"] = Curve(inner: 20) } };
        var p = new PadProcessor(profile);
        var small = p.Process(Pad(lx: 4000, ly: 3000), 0); // about 15%
        Assert.Equal((0, 0), (small.LeftX, small.LeftY));
        var big = p.Process(Pad(lx: 32767), 1);
        Assert.InRange(big.LeftX, 32000, 32767);
    }

    [Fact]
    public void An_outer_deadzone_reaches_full_travel_early()
    {
        var profile = new PcModeProfile { Curves = { ["LeftStick"] = Curve(outer: 80) } };
        var output = new PadProcessor(profile).Process(Pad(lx: (short)(32767 * 0.82)), 0);
        Assert.InRange(output.LeftX, 32600, 32767);
    }

    [Fact]
    public void A_curve_reshapes_the_response_and_keeps_the_direction()
    {
        var slow = Curve(x1: 35, y1: 35, x2: 219, y2: 145); // the "slow" preset
        var profile = new PcModeProfile { Curves = { ["RightStick"] = slow } };
        var output = new PadProcessor(profile).Process(Pad(rx: 16000, ry: 16000), 0);
        Assert.Equal(output.RightX, output.RightY);          // same direction
        Assert.NotEqual(16000, output.RightX);               // actually reshaped
        Assert.True(output.RightX > 0);
    }

    [Fact]
    public void A_default_curve_changes_nothing()
    {
        var profile = new PcModeProfile { Curves = { ["LeftStick"] = Curve() } };
        var output = new PadProcessor(profile).Process(Pad(lx: -12345, ly: 6789), 0);
        Assert.Equal((-12345, 6789), (output.LeftX, output.LeftY));
    }

    [Fact]
    public void Trigger_curves_apply_to_the_trigger_value()
    {
        var profile = new PcModeProfile { Curves = { ["LeftTrigger"] = Curve(inner: 40) } };
        var p = new PadProcessor(profile);
        Assert.Equal(0, p.Process(Pad(lt: 80), 0).LeftTrigger);
        Assert.Equal(255, p.Process(Pad(lt: 255), 1).LeftTrigger);
    }

    [Fact]
    public void A_macro_plays_once_per_press_and_keeps_its_trigger_from_the_game()
    {
        var steps = new[]
        {
            new SetupMacro(Guid.NewGuid(), ["A"], StickHeading.Neutral, StickHeading.Neutral, 50, 50),
            new SetupMacro(Guid.NewGuid(), ["B"], StickHeading.Up, StickHeading.Neutral, 50, 0),
        };
        var p = new PadProcessor(new PcModeProfile { Macros = { ["RB"] = steps } });
        var first = p.Process(Pad(PadButtons.RB), 0);
        Assert.Equal(PadButtons.A, first.Buttons);                       // RB itself is not passed on
        Assert.Equal(0, p.Process(Pad(PadButtons.RB), 60).Buttons);      // the pause
        var second = p.Process(Pad(PadButtons.RB), 120);                 // held: no restart
        Assert.Equal(PadButtons.B, second.Buttons);
        Assert.Equal(32767, second.LeftY);
        var done = p.Process(Pad(PadButtons.RB), 200);
        Assert.Equal(0, done.Buttons); Assert.False(p.PlayingMacro);
        // let go and press again: it plays again
        p.Process(Pad(), 300);
        Assert.Equal(PadButtons.A, p.Process(Pad(PadButtons.RB), 400).Buttons);
    }

    [Fact]
    public void Fine_dead_zones_work_in_tenths_of_a_percent()
    {
        var p = new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { Inner = 4.5 } } });
        Assert.Equal((0, 0), (p.Process(Pad(lx: (short)(32768 * .044)), 0).LeftX, 0)); // 4.4%: inside
        Assert.True(p.Process(Pad(lx: (short)(32768 * .047)), 1).LeftX > 0);            // 4.7%: outside
        Assert.InRange(PadProcessor.Response(null, new() { Inner = 4.5, Outer = 95.5 }, .5), .4999, .5001);
    }

    [Fact]
    public void A_square_gate_takes_a_full_diagonal_to_both_corners()
    {
        var p = new PadProcessor(new PcModeProfile { Sticks = { ["RightStick"] = new() { SquareGate = true } } });
        var o = p.Process(Pad(rx: 23170, ry: 23170), 0); // the circular edge at 45 degrees
        Assert.InRange(o.RightX, 32700, 32767); Assert.InRange(o.RightY, 32700, 32767);
        var half = p.Process(Pad(rx: 16384), 1);          // straight right is unchanged
        Assert.InRange(half.RightX, 16300, 16450); Assert.Equal(0, half.RightY);
    }

    [Fact]
    public void An_axis_ratio_scales_up_and_down_only()
    {
        var p = new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { AxisRatio = 50 } } });
        var o = p.Process(Pad(lx: 16000, ly: 16000), 0);
        Assert.InRange(o.LeftX, 15950, 16050); Assert.InRange(o.LeftY, 7950, 8050);
        Assert.Equal(32767, new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { AxisRatio = 150 } } }).Process(Pad(ly: 30000), 0).LeftY);
    }

    [Fact]
    public void Anti_deadzone_and_max_output_squeeze_the_output_and_the_dead_zone_stays_zero()
    {
        var t = new StickTuning { Inner = 5, AntiDeadzone = 10, MaxOutput = 90 };
        Assert.Equal(0, PadProcessor.Response(null, t, .04));            // inside the dead zone: nothing
        Assert.InRange(PadProcessor.Response(null, t, .051), .10, .102); // just past it: the floor
        Assert.InRange(PadProcessor.Response(null, t, 1), .8999, .9001); // full push: the ceiling
        var p = new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = t } });
        Assert.InRange(p.Process(Pad(lx: 32767), 0).LeftX, 29400, 29500);
    }

    [Fact]
    public void Raw_passes_the_stick_through_untouched()
    {
        var p = new PadProcessor(new PcModeProfile { Curves = { ["LeftStick"] = Curve(inner: 20, outer: 80) }, Sticks = { ["LeftStick"] = new() { Raw = true, Inner = 9 } } });
        var o = p.Process(Pad(lx: 1234, ly: -4321), 0);
        Assert.Equal((1234, -4321), (o.LeftX, o.LeftY));
        Assert.Equal(.03, PadProcessor.Response(null, new() { Raw = true }, .03));
    }

    [Fact]
    public void Stick_tuning_counts_as_a_setting_and_bad_values_are_refused()
    {
        Assert.False(new PcModeProfile { Sticks = { ["LeftStick"] = new() { Raw = true } } }.IsNeutral);
        Assert.False(new PcModeProfile { Sticks = { ["LeftStick"] = new() { AntiDeadzone = 4 } } }.IsNeutral);
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { AntiDeadzone = 50 } } }));
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { MaxOutput = 20 } } }));
        Assert.False(new PcModeProfile { Sticks = { ["LeftStick"] = new() { SquareGate = true } } }.IsNeutral);
        Assert.True(new PcModeProfile { Sticks = { ["LeftStick"] = new() } }.IsNeutral);
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Sticks = { ["LeftStick"] = new() { AxisRatio = 300 } } }));
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Sticks = { ["LT"] = new() { Inner = 2 } } }));
    }

    [Fact]
    public void A_profile_with_an_unknown_button_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Remap = { ["A"] = "NOPE" } }));
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Turbo = { ["A"] = 500 } }));
        Assert.Throws<InvalidDataException>(() => new PadProcessor(new PcModeProfile { Curves = { ["LeftStick"] = Curve(inner: 90, outer: 50) } }));
    }

    [Fact]
    public void The_store_round_trips_and_drops_neutral_profiles()
    {
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-pcmode-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new PcModeStore(path); store.Load();
            var profile = new PcModeProfile { Remap = { ["A"] = "B" }, Turbo = { ["X"] = 12 }, Curves = { ["LeftStick"] = Curve(inner: 10) }, Macros = { ["RB"] = [new SetupMacro(Guid.NewGuid(), ["A", "B"], StickHeading.Left, StickHeading.Neutral, 40, 20)] } };
            store.Save("x10", profile);
            var again = new PcModeStore(path); again.Load();
            var loaded = again.Get("x10");
            Assert.Equal("B", loaded.Remap["A"]); Assert.Equal(12, loaded.Turbo["X"]); Assert.Equal(10, loaded.Curves["LeftStick"].Inner);
            Assert.Equal(StickHeading.Left, loaded.Macros["RB"][0].Left);
            again.Save("x10", new PcModeProfile { Sticks = { ["LeftStick"] = new() { Inner = 3.7, SquareGate = true, AxisRatio = 80 } } });
            var tuned = new PcModeStore(path); tuned.Load();
            Assert.Equal((3.7, true, 80), (tuned.Get("x10").Sticks["LeftStick"].Inner, tuned.Get("x10").Sticks["LeftStick"].SquareGate, tuned.Get("x10").Sticks["LeftStick"].AxisRatio));
            Assert.True(again.Get("dune").IsNeutral);
            again.Save("x10", new());
            var cleared = new PcModeStore(path); cleared.Load();
            Assert.True(cleared.Get("x10").IsNeutral);
        }
        finally { foreach (var f in new[] { path, path + ".bak" }) if (File.Exists(f)) File.Delete(f); }
    }

    [Fact]
    public void An_unreadable_store_is_never_overwritten()
    {
        string path = Path.Combine(Path.GetTempPath(), "x20ctl-pcmode-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");
            var store = new PcModeStore(path); store.Load();
            Assert.False(store.Healthy);
            Assert.Throws<IOException>(() => store.Save("x10", new PcModeProfile { Remap = { ["A"] = "B" } }));
            Assert.Equal("{ not json", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
