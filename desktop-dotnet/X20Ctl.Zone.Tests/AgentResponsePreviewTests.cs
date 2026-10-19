using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Tests;

public sealed class AgentResponsePreviewTests
{
    static SetupCurve Curve(int inner = 0, int outer = 100) => new("custom", "default", inner, outer, 85, 35, 170, 120);

    static void AssertGameplay(PcModeProfile before, PcModeProfile after, string target)
    {
        var samples = AgentResponsePreview.Samples(before, after);
        Assert.Equal(new double[] { 25, 50, 75 }, samples.Select(s => s.Input));
        foreach (var sample in samples)
        {
            Assert.Equal(target, sample.Target);
            short x = (short)(sample.Input / 100 * 32768);
            var input = new PadState(0, 0, 0, target == "LeftStick" ? x : (short)0, 0,
                target == "RightStick" ? x : (short)0, 0);
            double Output(PcModeProfile p)
            {
                var output = new PadProcessor(p).Process(input, 0);
                return (target == "LeftStick" ? output.LeftX : output.RightX) / 32767.0 * 100;
            }
            Assert.Equal(Output(before), sample.BeforePercent);
            Assert.Equal(Output(after), sample.AfterPercent);
        }
    }

    [Theory]
    [InlineData("LeftStick")]
    [InlineData("RightStick")]
    public void Curves_only_use_shared_response_and_curve_deadzone_fallback(string target)
    {
        var before = new PcModeProfile();
        var after = new PcModeProfile { Curves = { [target] = Curve(30, 80) }, Sticks = { [target] = new() } };
        AssertGameplay(before, after, target);
        var samples = AgentResponsePreview.Samples(before, after);
        Assert.Equal(0, samples[0].AfterPercent);
        foreach (var sample in samples)
        {
            double response = PadProcessor.Response(after.Curves[target], null, sample.Input / 100);
            Assert.Equal(Math.Round(response * 32767) / 32767 * 100, sample.AfterPercent);
        }
    }

    [Fact]
    public void Tuning_only_covers_fine_deadzones_anti_deadzone_and_max_output()
    {
        var after = new PcModeProfile { Sticks = { ["RightStick"] = new() { Inner = 25.1, Outer = 70.5, AntiDeadzone = 12.3, MaxOutput = 81.2 } } };
        AssertGameplay(new(), after, "RightStick");
        var samples = AgentResponsePreview.Samples(new(), after);
        Assert.Equal(0, samples[0].AfterPercent);
        Assert.Equal(Math.Round(.812 * 32767) / 32767 * 100, samples[2].AfterPercent);
    }

    [Fact]
    public void Nondefault_tuning_overrides_curve_deadzones_and_raw_bypasses_everything()
    {
        var before = new PcModeProfile { Curves = { ["LeftStick"] = Curve(30, 80) },
            Sticks = { ["LeftStick"] = new() { Inner = 4.5, Outer = 95.5, AntiDeadzone = 10, MaxOutput = 90 } } };
        var after = new PcModeProfile { Curves = { ["LeftStick"] = Curve(30, 80) },
            Sticks = { ["LeftStick"] = new() { Raw = true, Inner = 40, MaxOutput = 60, AxisRatio = 150, SquareGate = true } } };
        AssertGameplay(before, after, "LeftStick");
        foreach (var sample in AgentResponsePreview.Samples(before, after))
        {
            Assert.True(sample.BeforePercent > 0);
            Assert.Equal(sample.Input * 32768 / 32767, sample.AfterPercent, 10);
        }
    }

    [Fact]
    public void Reset_restores_neutral_passthrough_including_quantization()
    {
        var before = new PcModeProfile { Curves = { ["LeftStick"] = Curve() }, Sticks = { ["LeftStick"] = new() { MaxOutput = 75 } } };
        AssertGameplay(before, new(), "LeftStick");
        Assert.Equal(8192 / 32767.0 * 100, AgentResponsePreview.Samples(before, new())[0].AfterPercent);
    }

    [Fact]
    public void Context_changes_are_reported_without_claiming_horizontal_strength_changes()
    {
        var before = new PcModeProfile { Sticks = { ["LeftStick"] = new() { Inner = 5 } } };
        var after = new PcModeProfile { Sticks = { ["LeftStick"] = new() { Inner = 5, AxisRatio = 150, SquareGate = true } } };
        AssertGameplay(before, after, "LeftStick");
        Assert.All(AgentResponsePreview.Samples(before, after), s =>
        {
            Assert.Equal(s.BeforePercent, s.AfterPercent);
            Assert.Contains("not horizontal strength", s.ContextualNote!);
        });
    }

    [Fact]
    public void Unchanged_sticks_defaults_and_button_or_trigger_only_changes_are_omitted()
    {
        var before = new PcModeProfile { Curves = { ["LeftStick"] = Curve() }, Sticks = { ["LeftStick"] = new() { Inner = 5 } } };
        var after = new PcModeProfile { Curves = { ["LeftStick"] = Curve(), ["RightStick"] = new("default", "default", 0, 100, 85, 85, 170, 170), ["LeftTrigger"] = Curve() },
            Sticks = { ["LeftStick"] = new() { Inner = 5 }, ["RightStick"] = new() }, Remap = { ["A"] = "B" }, Turbo = { ["X"] = 10 } };
        Assert.Empty(AgentResponsePreview.Samples(before, after));
    }

    [Fact]
    public void Both_changed_sticks_have_three_samples_in_target_order()
    {
        var after = new PcModeProfile { Curves = { ["LeftStick"] = Curve(), ["RightStick"] = Curve() } };
        Assert.Equal(new[] { "LeftStick", "LeftStick", "LeftStick", "RightStick", "RightStick", "RightStick" },
            AgentResponsePreview.Samples(new(), after).Select(s => s.Target));
    }

    [Fact]
    public void Curve_matches_the_samples_and_spans_the_whole_travel()
    {
        var before = new PcModeProfile();
        var after = new PcModeProfile { Curves = { ["RightStick"] = Curve(10, 90) } };
        var curve = AgentResponsePreview.Curve(before, after, "RightStick", 4);
        Assert.Equal(new double[] { 0, 25, 50, 75, 100 }, curve.Select(p => p.Input));
        var samples = AgentResponsePreview.Samples(before, after);
        foreach (var s in samples)
        {
            var point = curve.Single(p => p.Input == s.Input);
            Assert.Equal(s.BeforePercent, point.Before);
            Assert.Equal(s.AfterPercent, point.After);
        }
        Assert.Equal(0, curve[0].After);
        Assert.Equal(new[] { "RightStick" }, AgentResponsePreview.ChangedSticks(before, after));
        Assert.Empty(AgentResponsePreview.ChangedSticks(before, before));
    }
}
