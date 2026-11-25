using System.Text.Json;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Tests;

public class PcModeAdvancedTests
{
    [Fact]
    public void LegacyProfileJsonRemainsValidAndNeutral()
    {
        const string legacyJson = """{"Remap":{},"Curves":{},"Turbo":{},"Macros":{},"Sticks":{},"Combos":[],"Keys":{},"Custom":{}}""";
        var profile = JsonSerializer.Deserialize<PcModeProfile>(legacyJson)!;
        profile.Validate();
        Assert.True(profile.IsNeutral);
        Assert.Equal(SocdMode.Off, profile.DPadSocd);
        Assert.Equal(0, profile.OutputRateHz);
    }

    [Fact]
    public void DPadSocdSupportsLastInputAndNeutral()
    {
        var lastProfile = new PcModeProfile { DPadSocd = SocdMode.LastInput };
        var procLast = new PadProcessor(lastProfile);

        var p1 = procLast.Process(new PadState(PadButtons.Left, 0, 0, 0, 0, 0, 0), 10);
        Assert.Equal(PadButtons.Left, p1.Buttons);

        // Press Right while holding Left -> Last Input Priority outputs Right only
        var p2 = procLast.Process(new PadState(PadButtons.Left | PadButtons.Right, 0, 0, 0, 0, 0, 0), 20);
        Assert.Equal(PadButtons.Right, p2.Buttons);

        var neutralProfile = new PcModeProfile { DPadSocd = SocdMode.Neutral };
        var procNeutral = new PadProcessor(neutralProfile);
        var pn = procNeutral.Process(new PadState(PadButtons.Left | PadButtons.Right, 0, 0, 0, 0, 0, 0), 10);
        Assert.Equal(0, pn.Buttons);
    }

    [Fact]
    public void RapidTriggerActuatesAndResetsOnTravelDelta()
    {
        var profile = new PcModeProfile
        {
            Triggers =
            {
                ["RT"] = new TriggerTuning { RapidTrigger = true, ActuationPct = 10, ResetDeltaPct = 5 }
            }
        };
        var proc = new PadProcessor(profile);

        // Pull from 0 to 12% (~31) -> actuates to 255
        var a1 = proc.Process(new PadState(0, 0, 35, 0, 0, 0, 0), 10);
        Assert.Equal(255, a1.RightTrigger);

        // Keep pulling to 200
        var a2 = proc.Process(new PadState(0, 0, 200, 0, 0, 0, 0), 20);
        Assert.Equal(255, a2.RightTrigger);

        // Lift by > 5% (e.g. down to 180) -> drops back to 0 even though physical value is still high!
        var a3 = proc.Process(new PadState(0, 0, 180, 0, 0, 0, 0), 30);
        Assert.Equal(0, a3.RightTrigger);

        // Pull again from 180 to 215 (+13%) -> re-actuates immediately without returning to zero!
        var a4 = proc.Process(new PadState(0, 0, 215, 0, 0, 0, 0), 40);
        Assert.Equal(255, a4.RightTrigger);
    }

    [Fact]
    public void DualStageTriggerAssertsSecondaryActionAndReleasesCleanly()
    {
        var profile = new PcModeProfile
        {
            Triggers =
            {
                ["LT"] = new TriggerTuning { DualStage = true, HalfPullPct = 40, FullPullPct = 90, FullPullOutput = "K:160" }
            }
        };
        var proc = new PadProcessor(profile);

        proc.Process(new PadState(0, 150, 0, 0, 0, 0, 0), 10); // ~58% -> not full pull yet
        Assert.Empty(proc.TakeActions());

        proc.Process(new PadState(0, 240, 0, 0, 0, 0, 0), 20); // ~94% -> triggers secondary key
        var down = proc.TakeActions();
        Assert.Single(down);
        Assert.Equal(("K:160", true), down[0]);

        var rel = proc.ReleaseAll();
        Assert.Single(rel);
        Assert.Equal(("K:160", false), rel[0]);
    }

    [Fact]
    public void HoldToAimSniperReducesRightStickSpeedWhileHeld()
    {
        var profile = new PcModeProfile
        {
            Sniper = new SniperModifier { Enabled = true, ModifierButton = "LT", ScalePct = 50 }
        };
        var proc = new PadProcessor(profile);

        var normal = proc.Process(new PadState(0, 0, 0, 0, 0, 20000, 0), 10);
        var sniper = proc.Process(new PadState(0, 200, 0, 0, 0, 20000, 0), 20);
        Assert.Equal(20000, normal.RightX);
        Assert.InRange(sniper.RightX, 9950, 10050);
    }

    [Fact]
    public void AutoCenterDriftCancelsSmallBiasAndClampsAtSixPercent()
    {
        var profile = new PcModeProfile
        {
            Sticks = { ["LeftStick"] = new StickTuning { AutoCenterDrift = true } }
        };
        var proc = new PadProcessor(profile);

        PadState outState = default;
        for (int i = 0; i < 120; i++)
            outState = proc.Process(new PadState(0, 0, 0, 1600, -1000, 0, 0), i * 10); // ~4.9% / -3.0% bias

        Assert.True(Math.Abs(outState.LeftX) < 350);
        Assert.True(Math.Abs(outState.LeftY) < 250);
    }

    [Fact]
    public void ConditionalMacroLoopsAndDeterministicJitterWork()
    {
        var profile = new PcModeProfile
        {
            Macros =
            {
                ["LB"] = [new SetupMacro(Guid.NewGuid(), ["A"], StickHeading.Neutral, StickHeading.Neutral, 40, 20)]
            },
            MacroOptions =
            {
                ["LB"] = new MacroOptions { Mode = MacroPlayMode.RepeatN, RepeatCount = 2, HumanJitterMs = 5 }
            }
        };
        // Inject deterministic +5ms jitter
        var proc = new PadProcessor(profile, (_, max) => max);

        // Start macro on press
        var s0 = proc.Process(new PadState(PadButtons.LB, 0, 0, 0, 0, 0, 0), 0);
        Assert.True((s0.Buttons & PadButtons.A) != 0);

        // Hold+jitter = 45ms, pause+jitter = 25ms -> total 70ms per cycle, 2 cycles = 140ms
        var sMidPause = proc.Process(new PadState(0, 0, 0, 0, 0, 0, 0), 50);
        Assert.Equal(0, sMidPause.Buttons & PadButtons.A);

        var sSecondCycle = proc.Process(new PadState(0, 0, 0, 0, 0, 0, 0), 75);
        Assert.True((sSecondCycle.Buttons & PadButtons.A) != 0);

        var sDone = proc.Process(new PadState(0, 0, 0, 0, 0, 0, 0), 150);
        Assert.Equal(0, sDone.Buttons & PadButtons.A);
        Assert.False(proc.PlayingMacro);
    }

    [Fact]
    public void MergeCoPilotCombinesButtonsTriggersAndActiveStick()
    {
        var p1 = new PadState(PadButtons.A, 40, 0, 500, 0, 0, 0);
        var p2 = new PadState(PadButtons.B, 10, 200, 0, 0, 25000, -10000);
        var merged = PadProcessor.MergeCoPilot(p1, p2);

        Assert.Equal(PadButtons.A | PadButtons.B, merged.Buttons);
        Assert.Equal(40, merged.LeftTrigger);
        Assert.Equal(200, merged.RightTrigger);
        Assert.Equal(500, merged.LeftX);
        Assert.Equal(25000, merged.RightX);
    }
}
