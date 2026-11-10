using X20Ctl.Zone.Agent;
namespace X20Ctl.Zone.Tests;

/// <summary>The agent's own check-up: what it measures from the live sticks, triggers and buttons, and what it says.</summary>
public sealed class AgentCheckupTests
{
    [Fact]
    public void A_full_circle_covers_the_edge()
    {
        var sweep = new StickSweep();
        for (double a = 0; a < 2 * Math.PI; a += .05) sweep.Add(Math.Cos(a), Math.Sin(a));
        Assert.True(sweep.Done);
        Assert.InRange(sweep.AverageReach, .99, 1.01);
    }

    [Fact]
    public void Half_a_circle_is_not_done_and_the_centre_never_counts()
    {
        var sweep = new StickSweep();
        for (double a = 0; a < Math.PI; a += .05) sweep.Add(Math.Cos(a), Math.Sin(a));
        sweep.Add(.1, .1);
        Assert.False(sweep.Done);
        Assert.InRange(sweep.Coverage, .45, .56);
    }

    [Fact]
    public void A_short_direction_is_named()
    {
        var sweep = new StickSweep();
        for (double a = -Math.PI; a < Math.PI; a += .02)
        {
            double r = Math.Abs(a - Math.PI / 2) < .3 ? .75 : 1; // weak going up
            sweep.Add(r * Math.Cos(a), r * Math.Sin(a));
        }
        Assert.Equal("up", sweep.Weakest.Where);
    }

    [Fact]
    public void Resting_off_centre_is_drift_and_a_hand_is_not()
    {
        var rest = new RestReading();
        for (int i = 0; i < 100; i++) rest.Add(.06, .02);
        Assert.InRange(rest.Offset, .062, .064);
        Assert.False(rest.Touched);
        rest.Add(.6, 0);
        Assert.True(rest.Touched);
        Assert.Equal(10, CheckupResult.DeadZoneFor(.063));
    }

    [Fact]
    public void Findings_name_what_is_wrong()
    {
        var full = new StickSweep(); for (double a = 0; a < 2 * Math.PI; a += .05) full.Add(Math.Cos(a), Math.Sin(a));
        var result = new CheckupResult(.08, .01, full, full, 1, .7, ["A", "B"], ["L3"], 125, []);
        var bad = result.Findings().Where(f => !f.Good).Select(f => f.Line).ToList();
        Assert.Contains(bad, l => l.Contains("left stick rests 8%"));
        Assert.Contains(bad, l => l.StartsWith("RT only got to 70%"));
        Assert.Contains(bad, l => l.StartsWith("L3 never registered"));
        Assert.Equal(3, bad.Count);
    }

    [Fact]
    public void The_drift_fix_it_offers_is_understood_without_the_model()
    {
        var proposal = AgentDirect.Resolve($"set the left stick dead zone to {CheckupResult.DeadZoneFor(.08)}%");
        Assert.NotNull(proposal);
        Assert.Single(proposal!.Actions);
    }
}
