using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Agent;

/// <summary>A positive horizontal-axis sample (vertical input is zero). All numbers are percentages.
/// Input uses the gameplay input range of 32768; output uses the positive full-scale value of 32767.
/// Percentages retain output quantization, without display rounding.</summary>
public sealed record AgentResponseSample(string Target, double Input, double BeforePercent, double AfterPercent,
    string? ContextualNote = null);

/// <summary>Pure saved-profile comparison; samples describe axis output, not in-game movement speed.
/// Uses the gameplay Stick path and its shared Response logic, including raw/neutral bypass and quantization.</summary>
public static class AgentResponsePreview
{
    public static IReadOnlyList<AgentResponseSample> Samples(PcModeProfile before, PcModeProfile after)
    {
        var samples = new List<AgentResponseSample>();
        foreach (string target in new[] { "LeftStick", "RightStick" })
        {
            var (bc, bt) = Settings(before, target);
            var (ac, at) = Settings(after, target);
            if (CurveKey(bc) == CurveKey(ac) && TuningKey(bt) == TuningKey(at)) continue;
            string? note = (bt?.AxisRatio ?? 100) != (at?.AxisRatio ?? 100)
                || (bt?.SquareGate ?? false) != (at?.SquareGate ?? false)
                ? "Vertical ratio and square gate affect vertical/diagonal response, not horizontal strength in these samples. Raw mode bypasses shaping."
                : null;
            foreach (int input in new[] { 25, 50, 75 })
            {
                short x = (short)(input * 32768 / 100);
                samples.Add(new(target, input, Percent(x, bc, bt), Percent(x, ac, at), note));
            }
        }
        return samples;
    }

    /// <summary>The whole response of one stick before and after, input 0..100% in equal steps, for drawing as a curve.</summary>
    public static IReadOnlyList<(double Input, double Before, double After)> Curve(PcModeProfile before, PcModeProfile after, string target, int steps = 24)
    {
        var (bc, bt) = Settings(before, target);
        var (ac, at) = Settings(after, target);
        var points = new List<(double, double, double)>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double input = 100.0 * i / steps;
            short x = (short)Math.Min(32767, Math.Round(input * 32768 / 100));
            points.Add((input, Percent(x, bc, bt), Percent(x, ac, at)));
        }
        return points;
    }

    /// <summary>Where a real stick sits on the curves above: its push (how far from the centre, in any direction, as a
    /// percent) and what the game gets for that push before and after, by the same path as <see cref="Curve"/>.</summary>
    public static (double Input, double Before, double After) Live(PcModeProfile before, PcModeProfile after, string target, short x, short y)
    {
        double push = Math.Min(32767, Math.Sqrt((double)x * x + (double)y * y));
        var (bc, bt) = Settings(before, target);
        var (ac, at) = Settings(after, target);
        short along = (short)Math.Round(push);
        return (push * 100 / 32768, Percent(along, bc, bt), Percent(along, ac, at));
    }

    /// <summary>The sticks whose response differs between the two profiles.</summary>
    public static IReadOnlyList<string> ChangedSticks(PcModeProfile before, PcModeProfile after)
        => new[] { "LeftStick", "RightStick" }.Where(target =>
        {
            var (bc, bt) = Settings(before, target);
            var (ac, at) = Settings(after, target);
            return CurveKey(bc) != CurveKey(ac) || TuningKey(bt) != TuningKey(at);
        }).ToArray();

    static double Percent(short x, SetupCurve? curve, StickTuning? tuning)
        => PadProcessor.Stick(x, 0, curve, tuning).Item1 / 32767.0 * 100;

    // Match Process: explicit defaults are treated as absent, preserving curve dead-zone fallback.
    static (SetupCurve?, StickTuning?) Settings(PcModeProfile profile, string target)
    {
        var curve = profile.Curves.GetValueOrDefault(target);
        var tuning = profile.Sticks.GetValueOrDefault(target);
        return (curve is null || PcModeProfile.IsDefault(curve) ? null : curve,
            tuning is null || tuning.IsDefault ? null : tuning);
    }

    // Preset labels do not affect gameplay; compare saved numerical settings by value.
    static (int, int, int, int, int, int) CurveKey(SetupCurve? c)
        => (c?.Inner ?? 0, c?.Outer ?? 100, c?.X1 ?? 85, c?.Y1 ?? 85, c?.X2 ?? 170, c?.Y2 ?? 170);
    static (double, double, double, double, int, bool, bool) TuningKey(StickTuning? t)
        => (t?.Inner ?? 0, t?.Outer ?? 100, t?.AntiDeadzone ?? 0, t?.MaxOutput ?? 100,
            t?.AxisRatio ?? 100, t?.SquareGate ?? false, t?.Raw ?? false);
}
