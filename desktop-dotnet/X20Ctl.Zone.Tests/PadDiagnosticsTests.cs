using X20Ctl.Zone.Diagnostics;
namespace X20Ctl.Zone.Tests;

public sealed class PadDiagnosticsTests
{
    static void Circle(StickStats s, double radius, int points = 720)
    {
        for (int i = 0; i < points; i++)
        {
            double a = i * 2 * Math.PI / points;
            s.Add((short)Math.Round(Math.Cos(a) * radius * 32767), (short)Math.Round(Math.Sin(a) * radius * 32767));
        }
    }

    [Fact]
    public void A_round_stick_reaching_its_edge_scores_near_zero()
    {
        var s = new StickStats(); Circle(s, 1);
        Assert.Equal(StickStats.Sectors, s.Covered);
        Assert.InRange(s.CircularityError!.Value, 0, 0.5);
    }

    [Fact]
    public void A_stick_that_falls_short_scores_its_shortfall()
    {
        var s = new StickStats(); Circle(s, .9);
        Assert.InRange(s.CircularityError!.Value, 9.5, 10.5);
    }

    [Fact]
    public void Circularity_waits_until_most_directions_are_pushed()
    {
        var s = new StickStats();
        for (int i = 0; i < 50; i++) s.Add(32767, 0);
        Assert.Null(s.CircularityError);
    }

    [Fact]
    public void The_resting_point_shows_drift()
    {
        var s = new StickStats();
        for (int i = 0; i < 400; i++) s.Add(1310, -655); // 4% right, 2% down
        var rest = s.Rest!.Value;
        Assert.InRange(rest.X, 3.9, 4.1); Assert.InRange(rest.Y, -2.1, -1.9);
    }

    [Fact]
    public void The_smallest_step_gives_the_resolution()
    {
        var eight = new StickStats();
        for (int v = 0; v < 20000; v += 256) eight.Add((short)v, 0); // an 8-bit stick: XInput steps of 256
        Assert.Equal(8, eight.ResolutionBits);
        var twelve = new StickStats();
        for (int v = 0; v < 20000; v += 16) twelve.Add((short)v, 0);
        Assert.Equal(12, twelve.ResolutionBits);
        Assert.Null(new StickStats().ResolutionBits);
    }

    [Fact]
    public void Trigger_travel_and_steps()
    {
        var t = new TriggerStats();
        foreach (byte v in new byte[] { 0, 4, 8, 120, 255, 0 }) t.Add(v);
        Assert.True(t.FullTravel); Assert.Equal(6, t.ResolutionBits); // steps of 4
        var weak = new TriggerStats(); weak.Add(0); weak.Add(200);
        Assert.False(weak.FullTravel);
    }

    [Fact]
    public void A_press_too_short_for_a_finger_counts_as_a_bounce()
    {
        var b = new ButtonStats(); ushort lb = 0x0100; var masks = new[] { lb };
        b.Add(lb, 0, masks); b.Add(0, 120, masks);    // a real press
        b.Add(lb, 300, masks); b.Add(0, 305, masks);  // a 5 ms chatter
        Assert.Equal(2, b[lb].Presses); Assert.Equal(1, b[lb].Bounces); Assert.Equal(5, b[lb].ShortestMs);
    }

    [Fact]
    public void The_rate_meter_counts_one_second()
    {
        var r = new RateMeter();
        for (int i = 0; i < 2000; i++) r.Add(i * 0.5); // 2000 reports a second
        Assert.InRange(r.Current(999.5), 1990, 2001); Assert.InRange(r.Peak, 1990, 2001);
        Assert.Equal(0, r.Current(5000));
    }
}
