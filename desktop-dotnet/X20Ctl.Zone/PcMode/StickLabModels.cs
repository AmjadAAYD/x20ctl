using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.PcMode;

public enum DeadzoneShape { Radial, Axial, Hybrid }

public readonly record struct CurvePoint(double X, double Y);

/// <summary>Full Stick Lab advanced settings for an analog stick (up to 10-point spline, shapes, tilt, snap, morph, smoothing).</summary>
public sealed class StickLabSettings
{
    public const int MaxCustomPoints = 10;

    public DeadzoneShape DeadzoneShape { get; set; } = DeadzoneShape.Radial;
    public double RadialAntiDeadzone { get; set; }
    public double AxialAntiDeadzone { get; set; }
    public double GateMorph { get; set; }
    public double RotationDegrees { get; set; }
    public double AngleSnapDegrees { get; set; }
    public bool AngleSnap8Way { get; set; }
    public bool InvertX { get; set; }
    public bool InvertY { get; set; }
    public double Smoothing { get; set; }
    public double DriftOffsetX { get; set; }
    public double DriftOffsetY { get; set; }
    public List<CurvePoint> CustomPoints { get; set; } = [];

    [JsonIgnore]
    public bool IsDefault =>
        DeadzoneShape == DeadzoneShape.Radial &&
        RadialAntiDeadzone == 0 &&
        AxialAntiDeadzone == 0 &&
        GateMorph == 0 &&
        RotationDegrees == 0 &&
        AngleSnapDegrees == 0 &&
        !AngleSnap8Way &&
        !InvertX &&
        !InvertY &&
        Smoothing == 0 &&
        DriftOffsetX == 0 &&
        DriftOffsetY == 0 &&
        (CustomPoints == null || CustomPoints.Count == 0);

    public StickLabSettings Clone() => new()
    {
        DeadzoneShape = DeadzoneShape,
        RadialAntiDeadzone = RadialAntiDeadzone,
        AxialAntiDeadzone = AxialAntiDeadzone,
        GateMorph = GateMorph,
        RotationDegrees = RotationDegrees,
        AngleSnapDegrees = AngleSnapDegrees,
        AngleSnap8Way = AngleSnap8Way,
        InvertX = InvertX,
        InvertY = InvertY,
        Smoothing = Smoothing,
        DriftOffsetX = DriftOffsetX,
        DriftOffsetY = DriftOffsetY,
        CustomPoints = CustomPoints?.ToList() ?? [],
    };
}

/// <summary>Left-stick outer-ring auto-sprint configuration (automatically clicks L3 near full push).</summary>
public sealed class AutoSprintSettings
{
    public bool Enabled { get; set; }
    public int OuterThresholdPct { get; set; } = 90;
    public string OutputButton { get; set; } = "L3";

    [JsonIgnore]
    public bool IsDefault => !Enabled;
}

/// <summary>Real-time mechanical stick snapback peak detector (measures opposite-side bounce on fast release).</summary>
public sealed class StickSnapbackDetector
{
    double prevX, prevY;
    double releasePeakMag;
    double releaseDirX, releaseDirY;
    bool trackingRelease;
    int releaseTicksRemaining;

    public double PeakBouncePct { get; private set; }
    public double LastBouncePct { get; private set; }
    public int BounceEvents { get; private set; }

    public void Observe(short rawX, short rawY)
    {
        double nx = rawX / 32768.0;
        double ny = rawY / 32768.0;
        double mag = Math.Sqrt(nx * nx + ny * ny);
        double prevMag = Math.Sqrt(prevX * prevX + prevY * prevY);

        if (prevMag >= 0.72 && mag < prevMag - 0.12)
        {
            trackingRelease = true;
            releasePeakMag = prevMag;
            releaseDirX = prevX / prevMag;
            releaseDirY = prevY / prevMag;
            releaseTicksRemaining = 24;
        }
        else if (trackingRelease)
        {
            releaseTicksRemaining--;
            // Opposite-direction component relative to prior deflection
            double opposite = -(nx * releaseDirX + ny * releaseDirY);
            if (opposite > 0.04)
            {
                double pct = Math.Clamp(opposite * 100.0, 0.0, 100.0);
                LastBouncePct = pct;
                if (pct > PeakBouncePct) PeakBouncePct = pct;
                if (pct >= 12.0 && releasePeakMag >= 0.72)
                {
                    BounceEvents++;
                    trackingRelease = false;
                }
            }
            if (releaseTicksRemaining <= 0)
                trackingRelease = false;
        }

        prevX = nx;
        prevY = ny;
    }

    public void Reset()
    {
        prevX = prevY = releasePeakMag = releaseDirX = releaseDirY = 0;
        trackingRelease = false;
        releaseTicksRemaining = 0;
        PeakBouncePct = 0;
        LastBouncePct = 0;
        BounceEvents = 0;
    }
}

public static class StickLabMath
{
    public static List<CurvePoint> NormalizePoints(IEnumerable<CurvePoint>? points)
    {
        if (points == null) return [];
        var list = points
            .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y))
            .Select(p => new CurvePoint(Math.Clamp(p.X, 0, 100), Math.Clamp(p.Y, 0, 100)))
            .OrderBy(p => p.X)
            .ToList();

        var dedup = new List<CurvePoint>(StickLabSettings.MaxCustomPoints);
        foreach (var pt in list)
        {
            if (dedup.Count > 0 && Math.Abs(dedup[^1].X - pt.X) < 0.25)
                dedup[^1] = pt;
            else if (dedup.Count < StickLabSettings.MaxCustomPoints)
                dedup.Add(pt);
        }
        return dedup;
    }

    /// <summary>Fritsch-Carlson monotonic cubic spline evaluation across up to 10 control points (0..1 -> 0..1).</summary>
    public static double EvaluateSpline(IReadOnlyList<CurvePoint>? customPoints, double xNorm)
    {
        double x = Math.Clamp(xNorm, 0.0, 1.0);
        if (customPoints == null || customPoints.Count == 0) return x;

        var raw = NormalizePoints(customPoints)
            .Select(p => (X: p.X / 100.0, Y: p.Y / 100.0))
            .ToList();

        if (raw.Count == 0) return x;
        if (raw[0].X > 0.002) raw.Insert(0, (0.0, 0.0));
        else raw[0] = (0.0, raw[0].Y);
        if (raw[^1].X < 0.998) raw.Add((1.0, 1.0));
        else raw[^1] = (1.0, raw[^1].Y);

        if (raw.Count < 2) return x;
        if (raw.Count == 2)
        {
            double span2 = Math.Max(1e-6, raw[1].X - raw[0].X);
            double t2 = Math.Clamp((x - raw[0].X) / span2, 0.0, 1.0);
            return Math.Clamp(raw[0].Y + (raw[1].Y - raw[0].Y) * t2, 0.0, 1.0);
        }

        int n = raw.Count;
        var d = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
            d[i] = (raw[i + 1].Y - raw[i].Y) / Math.Max(1e-6, raw[i + 1].X - raw[i].X);

        var m = new double[n];
        m[0] = d[0];
        m[n - 1] = d[n - 2];
        for (int i = 1; i < n - 1; i++)
            m[i] = d[i - 1] * d[i] <= 0 ? 0.0 : (d[i - 1] + d[i]) * 0.5;

        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(d[i]) < 1e-9)
            {
                m[i] = m[i + 1] = 0.0;
                continue;
            }
            double a = m[i] / d[i];
            double b = m[i + 1] / d[i];
            double h2 = a * a + b * b;
            if (h2 > 9.0)
            {
                double tau = 3.0 / Math.Sqrt(h2);
                m[i] = tau * a * d[i];
                m[i + 1] = tau * b * d[i];
            }
        }

        if (x <= raw[0].X) return Math.Clamp(raw[0].Y, 0.0, 1.0);
        if (x >= raw[^1].X) return Math.Clamp(raw[^1].Y, 0.0, 1.0);

        int seg = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (x >= raw[i].X && x <= raw[i + 1].X) { seg = i; break; }
        }

        double h = Math.Max(1e-6, raw[seg + 1].X - raw[seg].X);
        double t = (x - raw[seg].X) / h;
        double tSq = t * t, tCu = tSq * t;
        double y = (2 * tCu - 3 * tSq + 1) * raw[seg].Y
                 + (tCu - 2 * tSq + t) * h * m[seg]
                 + (-2 * tCu + 3 * tSq) * raw[seg + 1].Y
                 + (tCu - tSq) * h * m[seg + 1];
        return Math.Clamp(y, 0.0, 1.0);
    }

    /// <summary>Generates clean 6-point Smart Curve Maker shapes in 0..100% space.</summary>
    public static List<CurvePoint> GenerateSmartCurve(string presetKind, double strength = 1.8)
    {
        double p = Math.Clamp(strength, 1.1, 3.5);
        int[] xs = [0, 20, 40, 60, 80, 100];
        return presetKind.ToLowerInvariant() switch
        {
            "precision" or "slow" => xs.Select(x => new CurvePoint(x, Math.Round(Math.Pow(x / 100.0, p) * 100.0, 1))).ToList(),
            "aggressive" or "quick" => xs.Select(x => new CurvePoint(x, Math.Round(Math.Pow(x / 100.0, 1.0 / p) * 100.0, 1))).ToList(),
            "scurve" or "smooth" => xs.Select(x =>
            {
                double t = x / 100.0;
                double s = 3 * t * t - 2 * t * t * t;
                return new CurvePoint(x, Math.Round((t * 0.3 + s * 0.7) * 100.0, 1));
            }).ToList(),
            "dualzone" => [new(0, 0), new(25, 14), new(55, 32), new(75, 58), new(90, 84), new(100, 100)],
            "exponential" => xs.Select(x => new CurvePoint(x, Math.Round((Math.Exp(2.2 * (x / 100.0)) - 1) / (Math.Exp(2.2) - 1) * 100.0, 1))).ToList(),
            _ => xs.Select(x => new CurvePoint(x, x)).ToList(),
        };
    }

    public static (double X, double Y) Rotate(double x, double y, double degrees)
    {
        if (Math.Abs(degrees) < 0.05) return (x, y);
        double rad = degrees * Math.PI / 180.0;
        double c = Math.Cos(rad), s = Math.Sin(rad);
        return (x * c - y * s, x * s + y * c);
    }

    public static (double UX, double UY) AngleSnap(double ux, double uy, double snapDegrees, bool eightWay)
    {
        if (snapDegrees <= 0.1) return (ux, uy);
        double ang = Math.Atan2(uy, ux);
        double step = eightWay ? Math.PI / 4.0 : Math.PI / 2.0;
        double nearest = Math.Round(ang / step) * step;
        double diffDeg = Math.Abs(ang - nearest) * 180.0 / Math.PI;
        if (diffDeg <= snapDegrees)
            return (Math.Cos(nearest), Math.Sin(nearest));
        return (ux, uy);
    }

    public static (double X, double Y) ApplyDeadzone(
        double nx, double ny, DeadzoneShape shape, double innerNorm, double outerNorm,
        out double magNorm)
    {
        double span = Math.Max(0.05, outerNorm - innerNorm);
        if (shape == DeadzoneShape.Axial)
        {
            double ax = Math.Abs(nx) <= innerNorm ? 0 : Math.Sign(nx) * Math.Clamp((Math.Abs(nx) - innerNorm) / span, 0.0, 1.0);
            double ay = Math.Abs(ny) <= innerNorm ? 0 : Math.Sign(ny) * Math.Clamp((Math.Abs(ny) - innerNorm) / span, 0.0, 1.0);
            double rm = Math.Min(1.0, Math.Sqrt(ax * ax + ay * ay));
            magNorm = rm;
            if (rm <= 1e-6) return (0, 0);
            return (ax / rm, ay / rm);
        }

        double rawR = Math.Sqrt(nx * nx + ny * ny);
        if (rawR <= innerNorm || rawR <= 1e-6)
        {
            magNorm = 0;
            return (0, 0);
        }
        double scaledR = Math.Clamp((rawR - innerNorm) / span, 0.0, 1.0);
        double ux = nx / rawR, uy = ny / rawR;

        if (shape == DeadzoneShape.Hybrid)
        {
            // Light axial stabilization near the deadzone border that fades cleanly at full push
            double crossBand = innerNorm * 0.45 * (1.0 - scaledR * 0.65);
            if (Math.Abs(nx) < crossBand) ux = 0;
            if (Math.Abs(ny) < crossBand) uy = 0;
            double len = Math.Sqrt(ux * ux + uy * uy);
            if (len <= 1e-6) { magNorm = 0; return (0, 0); }
            ux /= len;
            uy /= len;
        }

        magNorm = scaledR;
        return (ux, uy);
    }

    public static (double X, double Y) MorphGate(double ux, double uy, double mag, double morphPct)
    {
        double t = Math.Clamp(morphPct / 100.0, 0.0, 1.0);
        double len = Math.Sqrt(ux * ux + uy * uy);
        if (len <= 1e-6 || t <= 0.0) return (ux * mag, uy * mag);
        double maxAxis = Math.Max(Math.Abs(ux), Math.Abs(uy));
        double sqX = maxAxis > 1e-6 ? ux / maxAxis : ux;
        double sqY = maxAxis > 1e-6 ? uy / maxAxis : uy;
        double bx = ux * (1.0 - t) + sqX * t;
        double by = uy * (1.0 - t) + sqY * t;
        return (bx * mag, by * mag);
    }
}

/// <summary>Compact checksummed curve/lab preset code format (`X20C1.&lt;base64url&gt;.&lt;crc8&gt;`) for sharing and paste.</summary>
public static class StickLabShareCode
{
    public const string Prefix = "X20C1.";

    public sealed record Payload(
        string Channel,
        SetupCurve Curve,
        StickTuning? Tuning,
        SocdMode DPadSocd = SocdMode.Off,
        SocdMode LeftStickSocd = SocdMode.Off);

    static readonly JsonSerializerOptions CompactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Encode(CurveChannel channel, SetupCurve curve, StickTuning? tuning = null, SocdMode dpadSocd = SocdMode.Off, SocdMode leftStickSocd = SocdMode.Off)
    {
        var payload = new Payload(channel.ToString(), curve, tuning, dpadSocd, leftStickSocd);
        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(payload, CompactJson);
        string b64 = Convert.ToBase64String(utf8).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        byte crc = Crc8(utf8);
        return $"{Prefix}{b64}.{crc:X2}";
    }

    public static bool TryDecode(string? code, out Payload? payload, out string error)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(code)) { error = "Share code is empty."; return false; }
        string trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal)) { error = "Share code must start with X20C1."; return false; }
        string body = trimmed[Prefix.Length..];
        int dot = body.LastIndexOf('.');
        if (dot <= 0 || dot + 3 != body.Length) { error = "Share code checksum is missing or malformed."; return false; }
        string b64Part = body[..dot].Replace('-', '+').Replace('_', '/');
        string crcHex = body[(dot + 1)..];
        if (!byte.TryParse(crcHex, System.Globalization.NumberStyles.HexNumber, null, out byte expectedCrc))
        { error = "Invalid checksum hex in share code."; return false; }

        try
        {
            while (b64Part.Length % 4 != 0) b64Part += '=';
            byte[] utf8 = Convert.FromBase64String(b64Part);
            if (utf8.Length > 16384) { error = "Share code payload exceeds maximum size."; return false; }
            if (Crc8(utf8) != expectedCrc) { error = "Share code checksum mismatch (corrupted or edited)."; return false; }
            var decoded = JsonSerializer.Deserialize<Payload>(utf8, CompactJson);
            if (decoded == null || !Enum.TryParse<CurveChannel>(decoded.Channel, out _))
            { error = "Share code contains an unknown channel."; return false; }
            payload = decoded;
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            error = "Could not parse share code: " + ex.Message;
            return false;
        }
    }

    static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0x5A;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        return crc;
    }
}
