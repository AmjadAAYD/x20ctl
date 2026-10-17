using System.Globalization;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>
/// What one stick did while nobody touched it: where it rested on average, and the furthest it wandered from the centre.
/// The furthest point, not the average, decides the dead zone: drift only stops reaching games when every resting reading
/// is inside it. Percent of full travel.
/// </summary>
public sealed record StickDrift(string Stick, double RestX, double RestY, double Peak, int Samples)
{
    /// <summary>The smallest whole dead zone that hides every reading, with a percent to spare (0 when the stick is clean).</summary>
    public int Suggested => Peak < 1 ? 0 : (int)Math.Min(StickTuning.MaxInner, Math.Ceiling(Peak) + 1);
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"{AgentApplier.Name(Stick)}: rests at {RestX:+0.0;-0.0;0.0}%, {RestY:+0.0;-0.0;0.0}%, and wandered up to {Peak:0.0}% from the centre ({Samples} readings). ")
        + (Suggested == 0 ? "No drift worth a dead zone." : $"Smallest dead zone that hides it: {Suggested}%.");
}

/// <summary>A measurement, or why there isn't one (no controller, or a stick was touched).</summary>
public sealed record DriftReport(IReadOnlyList<StickDrift> Sticks, string? Problem)
{
    public string Describe() => Problem ?? string.Join(" ", Sticks.Select(s => s.Describe()));
    public StickDrift? For(string stick) => Problem == null ? Sticks.FirstOrDefault(s => s.Stick == stick) : null;
}

/// <summary>Reads the sticks at rest for a few seconds. The app's implementation reads the real controller.</summary>
public interface IDriftMeter
{
    Task<DriftReport> Measure(IReadOnlyList<string> sticks, CancellationToken cancel);
}

/// <summary>Collects one stick's readings (raw XInput values) into a <see cref="StickDrift"/>. Pure, tested directly.</summary>
public sealed class DriftSampler(string stick)
{
    /// <summary>A reading this far from the centre means a thumb was on the stick, not drift.</summary>
    public const double TouchedAt = 25;
    double sumX, sumY, peak; int count; bool touched;

    public void Add(short x, short y)
    {
        double nx = x / 32767.0 * 100, ny = y / 32767.0 * 100, m = Math.Sqrt(nx * nx + ny * ny);
        sumX += nx; sumY += ny; count++; peak = Math.Max(peak, m);
        if (m > TouchedAt) touched = true;
    }

    public bool Touched => touched;
    public int Count => count;
    public StickDrift Result() => new(stick, count == 0 ? 0 : Math.Round(sumX / count, 1), count == 0 ? 0 : Math.Round(sumY / count, 1), Math.Round(peak, 1), count);
}
