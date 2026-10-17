using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The "Controller not working?" card's picture (owner request 13 Oct 2026: a computer instead of a controller): a
/// monitor and keyboard where a scan runs. Inputs are read down the left, a live stick dot and level bars move on the
/// right, a line of light sweeps the screen and a progress bar fills and starts again. All of it is a handful of
/// transforms on cached shapes at 24 frames a second, so it costs next to nothing while the page is open.
/// </summary>
public sealed class PcScanArt : Canvas
{
    private static readonly Color Ice = Kit.Ice, Blue = Kit.Blue, Green = Kit.Green;
    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }
    private readonly Random random = new(13102026);

    public PcScanArt()
    {
        Width = 520; Height = 410; IsHitTestVisible = false; SnapsToDevicePixels = false;
        var glow = new Ellipse { Width = 480, Height = 300, Fill = new RadialGradientBrush(Color.FromArgb(70, Blue.R, Blue.G, Blue.B), Color.FromArgb(0, Blue.R, Blue.G, Blue.B)) };
        SetLeft(glow, 20); SetTop(glow, 0); Children.Add(glow);

        // stand and base
        var neck = new Polygon { Points = [new(238, 270), new(282, 270), new(296, 326), new(224, 326)], Fill = new LinearGradientBrush(Color.FromRgb(46, 58, 92), Color.FromRgb(22, 30, 54), 90) };
        var foot = new Border { Width = 170, Height = 14, CornerRadius = new(7), Background = new LinearGradientBrush(Color.FromRgb(70, 86, 128), Color.FromRgb(30, 40, 70), 90) };
        SetLeft(foot, 175); SetTop(foot, 322); Children.Add(neck); Children.Add(foot);

        // monitor
        var body = new Border
        {
            Width = 400, Height = 262, CornerRadius = new(20), BorderThickness = new(1.4),
            Background = new LinearGradientBrush(Color.FromRgb(52, 66, 108), Color.FromRgb(18, 26, 50), 90),
            BorderBrush = new LinearGradientBrush(Color.FromArgb(190, 170, 205, 255), Color.FromArgb(50, 170, 205, 255), 90),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Blue, BlurRadius = 30, ShadowDepth = 0, Opacity = .35 },
        };
        SetLeft(body, 60); SetTop(body, 20); Children.Add(body);
        var screen = new Border { Width = 368, Height = 226, CornerRadius = new(11), ClipToBounds = true, Background = new LinearGradientBrush(Color.FromRgb(10, 18, 40), Color.FromRgb(5, 9, 22), 90), Child = Screen() };
        SetLeft(screen, 76); SetTop(screen, 36); Children.Add(screen);
        // glass reflection
        var glass = new Polygon { Points = [new(80, 40), new(250, 40), new(170, 258), new(80, 258)], Fill = new LinearGradientBrush(Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0), IsHitTestVisible = false };
        Children.Add(glass);

        // keyboard
        var keys = new Border { Width = 330, Height = 50, CornerRadius = new(12), BorderThickness = new(1), BorderBrush = Brush(70, 150, 190, 255),
            Background = new LinearGradientBrush(Color.FromRgb(34, 46, 80), Color.FromRgb(16, 24, 46), 90) };
        SetLeft(keys, 95); SetTop(keys, 352); Children.Add(keys);
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 14; col++)
            {
                double width = row == 2 && col is >= 3 and <= 9 ? 0 : 15;
                if (width == 0) { if (col == 3) AddKey(95 + 14 + 3 * 21, 352 + 8 + row * 13, 7 * 21 - 6, row, col); continue; }
                AddKey(95 + 14 + col * 21, 352 + 8 + row * 13, 15, row, col);
            }
    }

    private void AddKey(double x, double y, double width, int row, int col)
    {
        var key = new Rectangle { Width = width, Height = 8, RadiusX = 2.5, RadiusY = 2.5, Fill = Brush(70, 150, 190, 255) };
        SetLeft(key, x); SetTop(key, y); Children.Add(key);
        // a few keys light now and then, each on its own beat
        if (DS.Motion && random.NextDouble() < .22)
        {
            var light = new ColorAnimation(Color.FromArgb(70, 150, 190, 255), Color.FromArgb(230, Ice.R, Ice.G, Ice.B), TimeSpan.FromMilliseconds(220 + random.Next(300)))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(random.Next(2400)) };
            var brush = new SolidColorBrush(Color.FromArgb(70, 150, 190, 255)); key.Fill = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, Ambient.Rate(light, 24));
        }
    }

    /// <summary>What the monitor shows: a tiny Read scan.</summary>
    private Canvas Screen()
    {
        var s = new Canvas { Width = 368, Height = 226 };
        // title strip
        var strip = new Rectangle { Width = 368, Height = 28, Fill = Brush(16, 255, 255, 255) }; s.Children.Add(strip);
        for (int i = 0; i < 3; i++)
        {
            var dot = new Ellipse { Width = 7, Height = 7, Fill = Brush(i == 0 ? (byte)200 : (byte)90, 150, 190, 255) }; SetLeft(dot, 12 + i * 12); SetTop(dot, 10.5); s.Children.Add(dot);
        }
        var title = new TextBlock { Text = "READ SCAN", FontFamily = DS.Display, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brush(190, 190, 215, 255) }; SetLeft(title, 56); SetTop(title, 7); s.Children.Add(title);
        var live = new StackPanel { Orientation = Orientation.Horizontal, Children = {
            new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Green), Margin = new(0, 4, 5, 0) },
            new TextBlock { Text = "LIVE", FontFamily = DS.Display, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Green) } } };
        SetLeft(live, 316); SetTop(live, 7); s.Children.Add(live);
        if (DS.Motion) ((Ellipse)live.Children[0]).BeginAnimation(OpacityProperty, Ambient.Rate(new DoubleAnimation(1, .25, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }, 24));

        // the controls being read: a dot and a bar each, the bars breathing at their own pace
        string[] names = ["A", "B", "X", "Y", "LB", "RB"];
        for (int i = 0; i < names.Length; i++)
        {
            double y = 46 + i * 21;
            var tag = new Border { Width = 26, Height = 15, CornerRadius = new(7.5), Background = Brush(40, 125, 182, 255), Child = new TextBlock { Text = names[i], FontFamily = DS.Display, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brush(230, 220, 235, 255), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            SetLeft(tag, 14); SetTop(tag, y); s.Children.Add(tag);
            var track = new Rectangle { Width = 150, Height = 7, RadiusX = 3.5, RadiusY = 3.5, Fill = Brush(24, 255, 255, 255) }; SetLeft(track, 48); SetTop(track, y + 4); s.Children.Add(track);
            var fill = new Rectangle { Width = 150, Height = 7, RadiusX = 3.5, RadiusY = 3.5, Fill = new LinearGradientBrush(Color.FromArgb(230, Blue.R, Blue.G, Blue.B), Color.FromArgb(230, Ice.R, Ice.G, Ice.B), 0), RenderTransformOrigin = new(0, .5) };
            var scale = new ScaleTransform(.15 + random.NextDouble() * .7, 1); fill.RenderTransform = scale; SetLeft(fill, 48); SetTop(fill, y + 4); s.Children.Add(fill);
            if (DS.Motion)
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, Ambient.Rate(new DoubleAnimation(.1 + random.NextDouble() * .25, .6 + random.NextDouble() * .4, TimeSpan.FromMilliseconds(900 + random.Next(1100)))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, BeginTime = TimeSpan.FromMilliseconds(random.Next(900)) }, 24));
        }

        // a stick, live: ring, crosshair and a dot that wanders
        var pad = new Ellipse { Width = 84, Height = 84, Stroke = Brush(130, 150, 190, 255), StrokeThickness = 1.5, Fill = Brush(18, 125, 182, 255) }; SetLeft(pad, 238); SetTop(pad, 46); s.Children.Add(pad);
        var h = new Rectangle { Width = 84, Height = 1, Fill = Brush(50, 150, 190, 255) }; SetLeft(h, 238); SetTop(h, 88); s.Children.Add(h);
        var v = new Rectangle { Width = 1, Height = 84, Fill = Brush(50, 150, 190, 255) }; SetLeft(v, 280); SetTop(v, 46); s.Children.Add(v);
        var puck = new Ellipse { Width = 13, Height = 13, Fill = new RadialGradientBrush(Colors.White, Ice), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Ice, BlurRadius = 10, ShadowDepth = 0, Opacity = 1 } };
        var move = new TranslateTransform(); puck.RenderTransform = move; SetLeft(puck, 273.5); SetTop(puck, 81.5); s.Children.Add(puck);
        if (DS.Motion)
        {
            move.BeginAnimation(TranslateTransform.XProperty, Ambient.Rate(new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(3.2), RepeatBehavior = RepeatBehavior.Forever, KeyFrames = {
                new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)), new EasingDoubleKeyFrame(26, KeyTime.FromPercent(.25)), new EasingDoubleKeyFrame(-4, KeyTime.FromPercent(.5)), new EasingDoubleKeyFrame(-26, KeyTime.FromPercent(.75)), new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)) } }, 24));
            move.BeginAnimation(TranslateTransform.YProperty, Ambient.Rate(new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(3.2), RepeatBehavior = RepeatBehavior.Forever, KeyFrames = {
                new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)), new EasingDoubleKeyFrame(-22, KeyTime.FromPercent(.25)), new EasingDoubleKeyFrame(24, KeyTime.FromPercent(.5)), new EasingDoubleKeyFrame(8, KeyTime.FromPercent(.75)), new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)) } }, 24));
        }
        // two triggers as upright level bars, one each side of the stick
        for (int i = 0; i < 2; i++)
        {
            var trackBar = new Rectangle { Width = 12, Height = 84, RadiusX = 5, RadiusY = 5, Fill = Brush(24, 255, 255, 255) }; SetLeft(trackBar, i == 0 ? 216 : 332); SetTop(trackBar, 46); s.Children.Add(trackBar);
            var level = new Rectangle { Width = 12, Height = 84, RadiusX = 5, RadiusY = 5, Fill = new LinearGradientBrush(Color.FromArgb(235, Ice.R, Ice.G, Ice.B), Color.FromArgb(235, Blue.R, Blue.G, Blue.B), 90), RenderTransformOrigin = new(.5, 1) };
            var pull = new ScaleTransform(1, .2); level.RenderTransform = pull; SetLeft(level, i == 0 ? 216 : 332); SetTop(level, 46); s.Children.Add(level);
            if (DS.Motion)
                pull.BeginAnimation(ScaleTransform.ScaleYProperty, Ambient.Rate(new DoubleAnimation(.08, .95, TimeSpan.FromMilliseconds(1300 + i * 500)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, BeginTime = TimeSpan.FromMilliseconds(i * 400) }, 24));
        }

        // a short log and the progress of the scan
        string[] log = ["> reading XInput slot 1", "> raw input · 8 usages", "> report rate ok"];
        for (int i = 0; i < log.Length; i++)
        {
            var line = new TextBlock { Text = log[i], FontFamily = new FontFamily("Consolas"), FontSize = 10, Foreground = Brush((byte)(150 - i * 28), 150, 190, 255) };
            SetLeft(line, 14); SetTop(line, 172 + i * 12); s.Children.Add(line);
        }
        var track2 = new Rectangle { Width = 340, Height = 7, RadiusX = 3.5, RadiusY = 3.5, Fill = Brush(26, 255, 255, 255) }; SetLeft(track2, 14); SetTop(track2, 212); s.Children.Add(track2);
        var bar = new Rectangle { Width = 340, Height = 7, RadiusX = 3.5, RadiusY = 3.5, RenderTransformOrigin = new(0, .5), Fill = new LinearGradientBrush(Color.FromRgb(Blue.R, Blue.G, Blue.B), Color.FromRgb(Green.R, Green.G, Green.B), 0) };
        var fillScale = new ScaleTransform(.62, 1); bar.RenderTransform = fillScale; SetLeft(bar, 14); SetTop(bar, 212); s.Children.Add(bar);
        if (DS.Motion) fillScale.BeginAnimation(ScaleTransform.ScaleXProperty, Ambient.Rate(new DoubleAnimation(0, 1, TimeSpan.FromSeconds(3.6)) { RepeatBehavior = RepeatBehavior.Forever }, 24));

        // the sweep
        var sweep = new Rectangle { Width = 368, Height = 60, IsHitTestVisible = false, Fill = new LinearGradientBrush(new GradientStopCollection {
            new(Color.FromArgb(0, 125, 182, 255), 0), new(Color.FromArgb(110, 125, 182, 255), .85), new(Color.FromArgb(200, 190, 225, 255), .97), new(Color.FromArgb(0, 125, 182, 255), 1) }, 90) };
        var sweepMove = new TranslateTransform(0, -70); sweep.RenderTransform = sweepMove; s.Children.Add(sweep);
        if (DS.Motion) sweepMove.BeginAnimation(TranslateTransform.YProperty, Ambient.Rate(new DoubleAnimation(-70, 240, TimeSpan.FromSeconds(2.8)) { RepeatBehavior = RepeatBehavior.Forever }, 24));
        return s;
    }
}
