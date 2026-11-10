using System.Text.Json;
using X20Ctl.Zone;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;

public class AgentPadTests
{
    // the default records read off the owner's X20 Pro on 1.44 (ControllerRecordsTests)
    const string Sticks = "0E03035555AAAA0003035555AAAA00", Triggers = "0E06105555AAAA0006105555AAAA00";
    static ControllerSnapshot Pad(byte[]? remap = null) => new(remap ?? [0], Convert.FromHexString(Sticks), Convert.FromHexString(Triggers), [.. Enumerable.Repeat(new byte[] { 1, 0, 0, 0x88, 0, 0x20, 8, 0 }, 6)]);
    static object Act(string action, string target, object value) => new { action, target, value, reason = "Requested" };

    /// <summary>The real path: the validator, then the PC mode applier, then the pad plan from what was applied.</summary>
    static (AgentVerdict Verdict, AgentPadPlan Plan) Run(ControllerSnapshot pad, params object[] actions)
    {
        var json = JsonSerializer.Serialize(new { speech = "Done.", actions, recap = "r", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question = "" });
        var verdict = ProposalValidator.Check(json, "x20_pro");
        Assert.True(verdict.Valid, verdict.StructureError);
        var outcome = AgentApplier.Apply(new PcModeProfile(), verdict);
        Assert.NotNull(outcome.Next);
        return (verdict, AgentPad.Plan(pad, verdict.Applicable, outcome.Next!));
    }

    [Fact]
    public void ADeadZoneChangesOnlyThatStickOnThePad()
    {
        var pad = Pad();
        var (_, plan) = Run(pad, Act("stick_deadzone", "LeftStick", 8));
        Assert.Equal(new AgentChange("Left stick dead zone", "3%", "8%"), Assert.Single(plan.Changes));
        Assert.Equal(8, ControllerRecords.ParseChannel(plan.Next.Sticks, 0, false).Inner);
        Assert.Equal(ControllerRecords.ParseChannel(pad.Sticks, 1, false), ControllerRecords.ParseChannel(plan.Next.Sticks, 1, false));
        Assert.Same(pad.Triggers, plan.Next.Triggers); Assert.Same(pad.Remap, plan.Next.Remap);
        Assert.Empty(plan.PcOnly);
    }

    [Fact]
    public void TriggersUseTheTriggerScale()
    {
        var (_, plan) = Run(Pad(), Act("trigger_full_at", "RightTrigger", 40));
        Assert.Equal(new AgentChange("Right trigger full at", "92%", "40%"), Assert.Single(plan.Changes));
        Assert.Equal(40, ControllerRecords.ParseChannel(plan.Next.Triggers, 1, true).Outer);
    }

    [Fact]
    public void CurvePointsReachThePadAsTheyWereApplied()
    {
        var (_, plan) = Run(Pad(), Act("stick_curve_points", "RightStick", "35,20,75,62"));
        var right = ControllerRecords.ParseChannel(plan.Next.Sticks, 1, false);
        Assert.Equal((CurvePoints.ToByte(35), CurvePoints.ToByte(20)), right.Point1);
        Assert.Equal((CurvePoints.ToByte(75), CurvePoints.ToByte(62)), right.Point2);
        Assert.Equal("Right stick curve", Assert.Single(plan.Changes).What);
    }

    [Fact]
    public void ARemapKeepsThePadsOtherRemaps()
    {
        var pad = Pad(ControllerRecords.BuildRemap(new Dictionary<string, string> { ["X"] = "Y" }));
        var (_, plan) = Run(pad, Act("remap", "A", "B"));
        var map = ControllerRecords.ParseRemap(plan.Next.Remap);
        Assert.Equal("B", map["A"]); Assert.Equal("Y", map["X"]);
        Assert.Equal(new AgentChange("A sends", "A", "B"), Assert.Single(plan.Changes));
    }

    [Fact]
    public void WhatThePadCantHoldIsNamedAndNotWritten()
    {
        var pad = Pad();
        var (_, plan) = Run(pad, Act("stick_anti_deadzone", "LeftStick", 10), Act("turbo", "X", 12), Act("stick_deadzone", "RightStick", 5.5));
        Assert.False(plan.Writes);
        Assert.Equal(3, plan.PcOnly.Count);
        Assert.Contains(plan.PcOnly, line => line.Contains("whole percents"));
        Assert.Equal(Sticks, Convert.ToHexString(plan.Next.Sticks));
    }

    [Fact]
    public void OnlyTheX20ProIsOffered()
    {
        var (verdict, _) = Run(Pad(), Act("stick_deadzone", "LeftStick", 8));
        Assert.True(AgentPad.Offers("x20_pro", verdict.Applicable));
        Assert.False(AgentPad.Offers("x20", verdict.Applicable));
        var (turbo, _) = Run(Pad(), Act("turbo", "X", 12));
        Assert.False(AgentPad.Offers("x20_pro", turbo.Applicable));
    }

    [Fact]
    public void RestorePutsBackOnlyWhatTheAgentWrote()
    {
        var before = Pad();
        var (_, plan) = Run(before, Act("stick_deadzone", "LeftStick", 8));
        // the pad's remaps changed elsewhere after the agent's write: going back leaves them alone
        var held = plan.Next with { Remap = ControllerRecords.BuildRemap(new Dictionary<string, string> { ["B"] = "A" }) };
        var back = AgentPad.Restore(held, before, plan.Next);
        Assert.Equal(Sticks, Convert.ToHexString(back.Sticks));
        Assert.Same(held.Remap, back.Remap);
        Assert.Empty(AgentPad.Diff(before with { Remap = held.Remap }, back));
    }

    [Fact]
    public void RestoreRejectsLaterEditsToTheSameRecord()
    {
        var before = Pad();
        var (_, plan) = Run(before, Act("stick_deadzone", "LeftStick", 8));
        var (_, later) = Run(plan.Next, Act("stick_deadzone", "RightStick", 6));
        Assert.Throws<InvalidOperationException>(() => AgentPad.Restore(later.Next, before, plan.Next));
    }
}
