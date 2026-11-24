using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// GPU-light, controller-free PS5 motion stage: concentric acoustic pulse rings,
/// dual harmonic waveform ribbons, and 4-channel precision motor equalizer columns.
/// Preserves <see cref="IsRunning"/>, <see cref="Phase"/>, <see cref="ReducedMotion"/>,
/// and <see cref="Levels"/> contracts while keeping zero idle CPU load.
/// </summary>
public sealed class VibrationVisual : Grid
{
    private readonly bool animations;
    private readonly MotionStageCanvas canvas;
    private readonly bool hasTriggers;

    private int strength;
    private TimeSpan started, last;
    private bool rendering;
    private bool hasLiveMotors;
    private double liveLg, liveRg, liveLt, liveRt;
    private double audioRms;
    private bool vocalActive;
    private bool ecoMode;
    private string region = "All";

    public int Strength => strength;
    public bool IsRunning { get; private set; }
    public bool ReducedMotion => !animations;
    public VibrationVisualProfile Profile => VibrationVisualProfile.For(strength);
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(VibrationVisual), new FrameworkPropertyMetadata(0d));
    public double Phase { get => (double)GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }

    /// <summary>Preview motor levels 0..1 (heavy left, light right), for external listeners.</summary>
    public event Action<double, double>? Levels;

    public VibrationVisual(Geometry outline, bool animate, string model = "x20")
    {
        animations = animate;
        hasTriggers = ModelProfiles.For(model).Motors.Contains("triggers");
        IsHitTestVisible = false;
        MinWidth = 320;
        MinHeight = 230;

        canvas = new MotionStageCanvas(hasTriggers);
        Children.Add(canvas);

        Loaded += (_, _) => UpdateClock();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => UpdateClock();
    }

    public void SetRegion(string targetRegion)
    {
        region = string.IsNullOrWhiteSpace(targetRegion) ? "All" : targetRegion;
        canvas.UpdateState(strength, 0, false, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
    }

    public void SetStrength(int percent)
    {
        if (percent == strength) return;
        strength = percent;
        UpdateClock();
    }

    public void SetTelemetry(double rms, bool vocal, bool eco)
    {
        double clamped = Math.Clamp(rms, 0.0, 1.0);
        if (Math.Abs(clamped - audioRms) < 0.005 && vocal == vocalActive && eco == ecoMode)
            return;
        audioRms = clamped;
        vocalActive = vocal;
        ecoMode = eco;
        if (!IsRunning)
            canvas.UpdateState(strength, 0, false, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
    }

    public void SetLiveMotors(double leftGrip, double rightGrip, double leftTrigger, double rightTrigger)
    {
        liveLg = Math.Clamp(leftGrip, 0.0, 1.0);
        liveRg = Math.Clamp(rightGrip, 0.0, 1.0);
        liveLt = Math.Clamp(leftTrigger, 0.0, 1.0);
        liveRt = Math.Clamp(rightTrigger, 0.0, 1.0);
        hasLiveMotors = liveLg > 0.005 || liveRg > 0.005 || liveLt > 0.005 || liveRt > 0.005;

        if (hasLiveMotors && !IsRunning && IsVisible)
        {
            if (animations)
            {
                IsRunning = true;
                started = TimeSpan.Zero;
                rendering = true;
                CompositionTarget.Rendering += Tick;
            }
            else
            {
                canvas.UpdateState(strength, 0, false, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
            }
        }
        else if (!hasLiveMotors && strength == 0)
        {
            Stop();
        }
        else if (!animations || !IsRunning)
        {
            canvas.UpdateState(strength, 0, false, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
        }
    }

    public void ClearLiveMotors()
    {
        if (!hasLiveMotors) return;
        hasLiveMotors = false;
        liveLg = liveRg = liveLt = liveRt = 0;
        UpdateClock();
    }

    private void Stop()
    {
        if (rendering)
        {
            CompositionTarget.Rendering -= Tick;
            rendering = false;
        }
        IsRunning = false;
        Phase = 0;
        canvas.UpdateState(strength, 0, false, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
        Levels?.Invoke(0, 0);
    }

    private void UpdateClock()
    {
        Stop();
        if ((strength == 0 && !hasLiveMotors) || !IsVisible) return;
        if (!animations)
        {
            double s = hasLiveMotors ? Math.Max(liveLg, liveRg) : strength / 100.0;
            Levels?.Invoke(hasLiveMotors ? liveLg : s, hasLiveMotors ? liveRg : s);
            return;
        }
        IsRunning = true;
        started = TimeSpan.Zero;
        rendering = true;
        CompositionTarget.Rendering += Tick;
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (last != TimeSpan.Zero && (now - last).TotalSeconds < 0.032) return;
        last = now;
        if (started == TimeSpan.Zero) started = now;
        double t = (now - started).TotalSeconds, s = strength / 100.0;
        Phase = t % 1;
        canvas.UpdateState(strength, t, true, region, hasLiveMotors, liveLg, liveRg, liveLt, liveRt, audioRms, vocalActive, ecoMode);
        Levels?.Invoke(hasLiveMotors ? liveLg : s, hasLiveMotors ? liveRg : s);
    }

    private sealed class MotionStageCanvas(bool hasTriggers) : FrameworkElement
    {
        static readonly Typeface MonoTypeface = new(new FontFamily("Consolas, Cascadia Mono, Courier New"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        static readonly Typeface LabelTypeface = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        int strength;
        double time;
        bool animate;
        string region = "All";
        bool live;
        double lg, rg, lt, rt;
        double rms;
        bool vocal;
        bool eco;
        readonly double[] peakHold = new double[4];

        public void UpdateState(int str, double t, bool anim, string reg, bool isLive, double lGrip, double rGrip, double lTrig, double rTrig, double audioRms, bool isVocal, bool isEco)
        {
            strength = str;
            time = t;
            animate = anim;
            region = reg;
            live = isLive;
            lg = lGrip;
            rg = rGrip;
            lt = lTrig;
            rt = rTrig;
            rms = audioRms;
            vocal = isVocal;
            eco = isEco;

            double[] cur = [lGrip, rGrip, lTrig, rTrig];
            for (int i = 0; i < 4; i++)
            {
                if (cur[i] >= peakHold[i]) peakHold[i] = cur[i];
                else peakHold[i] = Math.Max(cur[i], peakHold[i] - 0.018);
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 24 || h < 24) return;

            var theme = ThemeCatalog.Current;
            Color accent = theme.Accent;
            Color accentHi = theme.AccentHi;
            Color ecoCol = Color.FromRgb(0x22, 0xC5, 0x5E);
            Color coreColor = eco ? ThemeCatalog.Mix(accent, ecoCol, 0.55) : vocal ? accentHi : accent;

            double sNorm = strength / 100.0;
            double mLg = live ? lg : (region == "Right grip" ? 0 : sNorm);
            double mRg = live ? rg : (region == "Left grip" ? 0 : sNorm);
            double mLt = live ? lt : 0;
            double mRt = live ? rt : 0;
            double energy = Math.Clamp(Math.Max(Math.Max(mLg, mRg), Math.Max(mLt, mRt) * 0.9), 0, 1);

            double splitY = h * 0.64;
            double cx = w * 0.5;
            double cy = splitY * 0.52;
            double baseR = Math.Min(w * 0.24, splitY * 0.36);

            // Subtle radial acoustic bloom behind rings
            double bloomAlpha = 18 + 64 * Math.Max(energy, rms * 0.7);
            var aura = new RadialGradientBrush(
                Color.FromArgb((byte)Math.Clamp(bloomAlpha, 0, 120), coreColor.R, coreColor.G, coreColor.B),
                Colors.Transparent);
            aura.Freeze();
            dc.DrawEllipse(aura, null, new Point(cx, cy), baseR * 2.1, baseR * 1.45);

            // Concentric acoustic pulse rings
            for (int i = 0; i < 4; i++)
            {
                double wavePhase = animate ? ((time * (0.45 + 0.55 * energy) + i * 0.25) % 1.0) : (0.18 + i * 0.22);
                double r = baseR * (0.42 + wavePhase * (0.95 + 0.38 * energy));
                double fade = (1.0 - wavePhase);
                byte ringA = (byte)Math.Clamp((28 + 130 * energy) * fade * fade, 10, 210);
                Color rc = i % 2 == 0 ? coreColor : accentHi;
                var pen = MakePen(Color.FromArgb(ringA, rc.R, rc.G, rc.B), 1.2 + 1.4 * energy * fade);
                dc.DrawEllipse(null, pen, new Point(cx, cy), r, r * 0.74);
            }

            // Dual harmonic waveform ribbon across stage middle
            double waveW = w - 36;
            double x0 = 18;
            double amp = 6 + 34 * Math.Max(energy, rms * 0.65);
            const int segs = 44;
            var upperPts = new Point[segs + 1];
            var lowerPts = new Point[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                double u = i / (double)segs;
                double env = Math.Sin(u * Math.PI);
                env *= env;
                double p1 = animate ? time * (4.2 + energy * 4.5) : 0.8;
                double p2 = animate ? time * (7.6 + (vocal ? 4.0 : 1.5)) : 1.7;
                double yOff = (Math.Sin(u * 9.5 - p1) * 0.62 + Math.Sin(u * 21.0 + p2) * (vocal ? 0.45 : 0.24)) * amp * env;
                upperPts[i] = new Point(x0 + u * waveW, cy + yOff);
                lowerPts[i] = new Point(x0 + u * waveW, cy - yOff * 0.55);
            }

            var bassPen = MakePen(Color.FromArgb((byte)(110 + 135 * Math.Max(energy, rms)), coreColor.R, coreColor.G, coreColor.B), 2.1);
            var shimmerPen = MakePen(Color.FromArgb((byte)(65 + 115 * Math.Max(mLt, mRt)), accentHi.R, accentHi.G, accentHi.B), 1.35);
            DrawPolyline(dc, bassPen, upperPts);
            DrawPolyline(dc, shimmerPen, lowerPts);

            // Center state readout badge (clean sharp geometry, zero round pill)
            string modeBadge = eco
                ? "ECO BATTERY SAVER · 25 HZ"
                : vocal && live
                    ? "VOCAL GATE ACTIVE · TRIGGER DRIVE"
                    : live
                        ? "LIVE HAPTICS STREAM"
                        : strength > 0
                            ? $"MOTOR TEST PREVIEW · {strength}%"
                            : "STANDBY · READY";

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var badgeText = new FormattedText(
                modeBadge,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                MonoTypeface,
                10.5,
                MakeBrush(Color.FromArgb(235, theme.Text.R, theme.Text.G, theme.Text.B)),
                dpi);
            double padX = 12, padY = 4;
            double bw = badgeText.Width + padX * 2, bh = badgeText.Height + padY * 2;
            var badgeRect = new Rect(cx - bw * 0.5, splitY - bh - 8, bw, bh);
            dc.DrawRoundedRectangle(
                MakeBrush(Color.FromArgb(190, theme.Night.R, theme.Night.G, theme.Night.B)),
                MakePen(Color.FromArgb(140, coreColor.R, coreColor.G, coreColor.B), 1),
                badgeRect, 3, 3);
            dc.DrawText(badgeText, new Point(badgeRect.X + padX, badgeRect.Y + padY));

            // Separator hairline
            dc.DrawLine(MakePen(Color.FromArgb(95, theme.Rule.R, theme.Rule.G, theme.Rule.B), 1),
                new Point(14, splitY + 2), new Point(w - 14, splitY + 2));

            // 4-Channel (or 2-Channel) Vertical Motor Equalizer Columns
            int cols = hasTriggers ? 4 : 2;
            string[] names = ["L GRIP", "R GRIP", "L TRIG", "R TRIG"];
            double[] vals = [mLg, mRg, mLt, mRt];
            Color[] colsColors =
            [
                ThemeCatalog.Tone(Color.FromRgb(0xFF, 0x3B, 0x7F), t => t.Accent),
                ThemeCatalog.Tone(Color.FromRgb(0x38, 0xBD, 0xF8), t => t.AccentHi),
                ThemeCatalog.Tone(Color.FromRgb(0xFB, 0x92, 0x3C), t => ThemeCatalog.Mix(t.Accent, t.Text, 0.25)),
                ThemeCatalog.Tone(Color.FromRgb(0xA7, 0x8B, 0xFA), t => ThemeCatalog.Mix(t.AccentHi, t.Text, 0.25)),
            ];

            double colTop = splitY + 12;
            double colBot = h - 30;
            double colH = Math.Max(32, colBot - colTop);
            double gap = 12;
            double areaW = w - 28;
            double colW = (areaW - gap * (cols - 1)) / cols;

            for (int i = 0; i < cols; i++)
            {
                double x = 14 + i * (colW + gap);
                double val = live ? vals[i] : vals[i];
                double pk = live ? Math.Max(peakHold[i], val) : val;

                var trackRect = new Rect(x, colTop, colW, colH);
                dc.DrawRoundedRectangle(
                    MakeBrush(Color.FromArgb(115, theme.Void.R, theme.Void.G, theme.Void.B)),
                    MakePen(Color.FromArgb(80, theme.Rule.R, theme.Rule.G, theme.Rule.B), 1),
                    trackRect, 4, 4);

                // Subtle 25/50/75% ticks
                for (int t = 1; t <= 3; t++)
                {
                    double ty = colBot - colH * (t * 0.25);
                    dc.DrawLine(MakePen(Color.FromArgb(45, theme.Muted.R, theme.Muted.G, theme.Muted.B), 0.7),
                        new Point(x + 4, ty), new Point(x + colW - 4, ty));
                }

                if (val > 0.01)
                {
                    double fillH = Math.Max(3, colH * val);
                    var fillRect = new Rect(x + 2, colBot - fillH, Math.Max(4, colW - 4), fillH);
                    Color mc = colsColors[i];
                    var grad = new LinearGradientBrush(
                        Color.FromArgb(245, mc.R, mc.G, mc.B),
                        Color.FromArgb(120, mc.R, mc.G, mc.B),
                        90);
                    grad.Freeze();
                    dc.DrawRoundedRectangle(grad, null, fillRect, 3, 3);
                }

                if (pk > 0.02)
                {
                    double py = colBot - colH * pk;
                    dc.DrawRectangle(MakeBrush(colsColors[i]), null, new Rect(x + 2, py - 1.5, Math.Max(4, colW - 4), 2.5));
                }

                var labelFt = new FormattedText(
                    names[i],
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    LabelTypeface,
                    10,
                    MakeBrush(Color.FromArgb(190, theme.Soft.R, theme.Soft.G, theme.Soft.B)),
                    dpi);
                dc.DrawText(labelFt, new Point(x + (colW - labelFt.Width) * 0.5, colBot + 4));

                var pctFt = new FormattedText(
                    $"{Math.Round(val * 100):0}%",
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    MonoTypeface,
                    10.5,
                    MakeBrush(Color.FromArgb(240, theme.Text.R, theme.Text.G, theme.Text.B)),
                    dpi);
                dc.DrawText(pctFt, new Point(x + (colW - pctFt.Width) * 0.5, colBot + 16));
            }
        }

        static Brush MakeBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        static Pen MakePen(Color c, double w) { var p = new Pen(MakeBrush(c), w) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; p.Freeze(); return p; }
        static void DrawPolyline(DrawingContext dc, Pen pen, Point[] pts)
        {
            if (pts.Length < 2) return;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                ctx.PolyLineTo(pts[1..], true, true);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }
    }
}
