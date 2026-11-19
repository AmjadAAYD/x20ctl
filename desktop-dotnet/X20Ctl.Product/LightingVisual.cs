using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Hardware-native RGB visualizer wrapping the real X20 Pro <see cref="PhotoController"/> photo:
/// dual thumbstick ring LEDs at catalog stick centers plus body-clipped grip/shoulder rim glow.
/// </summary>
internal sealed class LightingVisual : Grid
{
    // X20 Pro stick centers in the standard 1536×1024 controller coordinate space.
    private static readonly Point LeftStickCenter = new(433.0, 310.0);
    private static readonly Point RightStickCenter = new(942.0, 486.0);
    private const double StickRingRadius = 78.0;

    private readonly PhotoController photo = new("x20_pro");
    private readonly Canvas edgeGlowLayer = new() { Width = 1536, Height = 1024, IsHitTestVisible = false };
    private readonly Canvas ringLayer = new() { Width = 1536, Height = 1024, IsHitTestVisible = false };

    private readonly List<RingLayer> stickRings = new();
    private readonly List<IslandGlow> edgeIslands = new();

    private readonly bool animate;
    private bool rendering;
    private bool streamRunning;
    private LightingFrame staticFrame = new(0xff, 0x00, 0x00, 0x50, LightingRecord.ModeSolid);
    private LightingEffectKind? activeKind;
    private string currentFinish;

    private sealed record RingLayer(Ellipse Halo, Ellipse Bloom, Ellipse Core);
    private sealed record IslandGlow(Path Halo, Path Bloom, Path Core);

    public LightingVisual(bool animate = true)
    {
        this.animate = animate;
        Width = 1536;
        Height = 1024;
        IsHitTestVisible = false;
        currentFinish = PadColours.Active("x20_pro");

        Children.Add(photo);

        if (ControllerOutlines.LoadSilhouette("x20_pro", currentFinish) is { } silhouette)
            edgeGlowLayer.Clip = silhouette;

        foreach (var island in ControllerOutlines.LoadIslands("x20_pro", currentFinish))
        {
            var halo = new Path { Data = island.Line, StrokeThickness = 42, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Opacity = 0 };
            var bloom = new Path { Data = island.Line, StrokeThickness = 18, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Opacity = 0 };
            var core = new Path { Data = island.Line, StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Opacity = 0 };
            edgeGlowLayer.Children.Add(halo);
            edgeGlowLayer.Children.Add(bloom);
            edgeGlowLayer.Children.Add(core);
            edgeIslands.Add(new IslandGlow(halo, bloom, core));
        }

        Children.Add(edgeGlowLayer);

        AddStickRing(LeftStickCenter);
        AddStickRing(RightStickCenter);
        Children.Add(ringLayer);

        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => StopRendering();
        IsVisibleChanged += (_, _) => UpdateSubscription();

        RenderCurrentState();
    }

    private void AddStickRing(Point center)
    {
        Ellipse MakeRing(double thickness)
        {
            var ellipse = new Ellipse
            {
                Width = StickRingRadius * 2,
                Height = StickRingRadius * 2,
                StrokeThickness = thickness,
                Fill = null,
                Opacity = 0,
            };
            Canvas.SetLeft(ellipse, center.X - StickRingRadius);
            Canvas.SetTop(ellipse, center.Y - StickRingRadius);
            ringLayer.Children.Add(ellipse);
            return ellipse;
        }

        stickRings.Add(new RingLayer(MakeRing(26), MakeRing(12), MakeRing(4.2)));
    }

    public void SetStaticFrame(byte r, byte g, byte b, byte brightness, byte nativeMode = LightingRecord.ModeSolid)
    {
        staticFrame = new(r, g, b, brightness, nativeMode);
        if (!streamRunning) RenderCurrentState();
    }

    public void SetEffectMode(bool running, LightingEffectKind? effectKind = null)
    {
        streamRunning = running;
        activeKind = effectKind;
        UpdateSubscription();
        RenderCurrentState();
    }

    private bool ShouldAnimate =>
        animate &&
        DS.Motion &&
        !ReviewSandbox.Active &&
        IsLoaded &&
        IsVisible &&
        (streamRunning || staticFrame.NativeMode is LightingRecord.ModeRainbow or LightingRecord.ModeBreathe or LightingRecord.ModeCycle);

    private void UpdateSubscription()
    {
        bool want = ShouldAnimate;
        if (want && !rendering)
        {
            CompositionTarget.Rendering += OnRenderTick;
            rendering = true;
        }
        else if (!want && rendering)
        {
            StopRendering();
        }
    }

    private void StopRendering()
    {
        if (!rendering) return;
        CompositionTarget.Rendering -= OnRenderTick;
        rendering = false;
    }

    private void OnRenderTick(object? sender, EventArgs e) => RenderCurrentState();

    private void RenderCurrentState()
    {
        if (streamRunning)
        {
            var frame = LightingEffectsService.EvaluatePreview();
            bool rainbowLike = activeKind == LightingEffectKind.SmoothHueWave ||
                (LightingEffectsService.Status.IsRunning && LightingEffectsService.CurrentSpec.Kind == LightingEffectKind.SmoothHueWave);
            if (!ShouldAnimate && rainbowLike && frame.Brightness > 0)
            {
                PaintSpectrum(frame.Brightness);
                return;
            }
            PaintSolid(frame.R, frame.G, frame.B, frame.Brightness);
            return;
        }

        if (staticFrame.NativeMode == LightingRecord.ModeOff || staticFrame.Brightness == 0)
        {
            PaintOff();
            return;
        }

        if (staticFrame.NativeMode == LightingRecord.ModeRainbow)
        {
            if (ShouldAnimate)
            {
                double secs = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                var leftRgb = ColourPalette.FromHsv((secs * 72) % 360, 1, 1);
                var rightRgb = ColourPalette.FromHsv(((secs * 72) + 120) % 360, 1, 1);
                PaintDualColor(
                    Color.FromRgb((byte)(leftRgb >> 16), (byte)(leftRgb >> 8), (byte)leftRgb),
                    Color.FromRgb((byte)(rightRgb >> 16), (byte)(rightRgb >> 8), (byte)rightRgb),
                    staticFrame.Brightness);
            }
            else
            {
                PaintSpectrum(staticFrame.Brightness);
            }
            return;
        }

        if (staticFrame.NativeMode is LightingRecord.ModeBreathe or LightingRecord.ModeCycle && ShouldAnimate)
        {
            double t = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            if (staticFrame.NativeMode == LightingRecord.ModeBreathe)
            {
                double wave = 0.25 + 0.75 * (0.5 - 0.5 * Math.Cos(t * 2.1));
                byte b = (byte)Math.Clamp((int)Math.Round(staticFrame.Brightness * wave), 1, 255);
                PaintSolid(staticFrame.R, staticFrame.G, staticFrame.B, b);
            }
            else
            {
                int cycle = ColourPalette.FromHsv((t * 48) % 360, 0.92, 1.0);
                PaintSolid((byte)(cycle >> 16), (byte)(cycle >> 8), (byte)cycle, staticFrame.Brightness);
            }
            return;
        }

        PaintSolid(staticFrame.R, staticFrame.G, staticFrame.B, staticFrame.Brightness);
    }

    private void PaintOff()
    {
        foreach (var r in stickRings) r.Halo.Opacity = r.Bloom.Opacity = r.Core.Opacity = 0;
        foreach (var e in edgeIslands) e.Halo.Opacity = e.Bloom.Opacity = e.Core.Opacity = 0;
    }

    private void PaintSolid(byte r, byte g, byte b, byte brightness)
    {
        var color = Color.FromRgb(r, g, b);
        PaintDualColor(color, color, brightness);
    }

    private void PaintDualColor(Color leftColor, Color rightColor, byte brightness)
    {
        if (brightness == 0) { PaintOff(); return; }
        double level = Math.Clamp(0.18 + 0.82 * (brightness / 255.0), 0.18, 1.0);

        for (int i = 0; i < stickRings.Count; i++)
        {
            var c = i == 0 ? leftColor : rightColor;
            var brightCore = Color.FromRgb(
                (byte)Math.Min(255, c.R + (255 - c.R) * 0.35),
                (byte)Math.Min(255, c.G + (255 - c.G) * 0.35),
                (byte)Math.Min(255, c.B + (255 - c.B) * 0.35));

            var haloBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(70 * level), c.R, c.G, c.B));
            var bloomBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(170 * level), c.R, c.G, c.B));
            var coreBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(245 * level), brightCore.R, brightCore.G, brightCore.B));
            haloBrush.Freeze(); bloomBrush.Freeze(); coreBrush.Freeze();

            var ring = stickRings[i];
            ring.Halo.Stroke = haloBrush; ring.Halo.Opacity = 1;
            ring.Bloom.Stroke = bloomBrush; ring.Bloom.Opacity = 1;
            ring.Core.Stroke = coreBrush; ring.Core.Opacity = 1;
        }

        var avg = Color.FromRgb(
            (byte)((leftColor.R + rightColor.R) / 2),
            (byte)((leftColor.G + rightColor.G) / 2),
            (byte)((leftColor.B + rightColor.B) / 2));

        var edgeHalo = new SolidColorBrush(Color.FromArgb((byte)Math.Round(34 * level), avg.R, avg.G, avg.B));
        var edgeBloom = new SolidColorBrush(Color.FromArgb((byte)Math.Round(82 * level), avg.R, avg.G, avg.B));
        var edgeCore = new SolidColorBrush(Color.FromArgb((byte)Math.Round(135 * level), avg.R, avg.G, avg.B));
        edgeHalo.Freeze(); edgeBloom.Freeze(); edgeCore.Freeze();

        foreach (var island in edgeIslands)
        {
            island.Halo.Stroke = edgeHalo; island.Halo.Opacity = 1;
            island.Bloom.Stroke = edgeBloom; island.Bloom.Opacity = 1;
            island.Core.Stroke = edgeCore; island.Core.Opacity = 1;
        }
    }

    private void PaintSpectrum(byte brightness)
    {
        if (brightness == 0) { PaintOff(); return; }
        double level = Math.Clamp(0.22 + 0.78 * (brightness / 255.0), 0.22, 1.0);

        Brush RingSpectrum(double alpha, double offset)
        {
            var g = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
            for (int i = 0; i <= 4; i++)
            {
                int rgb = ColourPalette.FromHsv((offset + i * 75) % 360, 0.95, 1.0);
                g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255 * level), (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb), i / 4.0));
            }
            g.Freeze();
            return g;
        }

        for (int i = 0; i < stickRings.Count; i++)
        {
            double offset = i == 0 ? 180 : 300;
            var ring = stickRings[i];
            ring.Halo.Stroke = RingSpectrum(0.28, offset); ring.Halo.Opacity = 1;
            ring.Bloom.Stroke = RingSpectrum(0.68, offset); ring.Bloom.Opacity = 1;
            ring.Core.Stroke = RingSpectrum(0.95, offset); ring.Core.Opacity = 1;
        }

        var edgeSpectrum = RingSpectrum(0.45, 210);
        var edgeHalo = RingSpectrum(0.14, 210);
        foreach (var island in edgeIslands)
        {
            island.Halo.Stroke = edgeHalo; island.Halo.Opacity = 1;
            island.Bloom.Stroke = edgeSpectrum; island.Bloom.Opacity = 0.65;
            island.Core.Stroke = edgeSpectrum; island.Core.Opacity = 0.85;
        }
    }
}
