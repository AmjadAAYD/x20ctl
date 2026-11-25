using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.Diagnostics;

namespace X20Ctl.Product.Overlays;

/// <summary>
/// Interactive 60 Hz Stick Aim Trainer window for feeling right-stick curves, sniper scaling, and dead zones.
/// Reads physical XInput slot directly (skipping PC mode virtual pads) or mouse preview if needed. Never opened in ReviewSandbox.
/// </summary>
internal sealed class AimTrainerWindow : Window
{
    const double ArenaWidth = 560, ArenaHeight = 290;

    readonly AimTrainerSession session = new();
    readonly Canvas arena = new()
    {
        Width = ArenaWidth,
        Height = ArenaHeight,
        ClipToBounds = true,
        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(230, 8, 13, 26))),
    };
    readonly Ellipse targetDot = new() { Stroke = Brushes.White, StrokeThickness = 1.6, Fill = DS.Brush("DS.Accent") };
    readonly Ellipse crosshair = new() { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2 };
    readonly TextBlock hudText = Kit.Text("Press Start (30s) · Aim with Right Stick · Fire with RT or A", 13.5, "DS.Text", FontWeights.SemiBold);
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    WindowsReader? reader;
    long lastTickMs;

    public AimTrainerWindow()
    {
        Title = "X20CTL · Stick Aim Trainer";
        Width = 620;
        Height = 440;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = DS.Brush("DS.Deep");
        Foreground = DS.Brush("DS.Text");
        FontFamily = DS.Body;

        arena.Children.Add(new Line { X1 = ArenaWidth / 2, X2 = ArenaWidth / 2, Y1 = 0, Y2 = ArenaHeight, Stroke = DS.Brush("DS.Hairline"), StrokeThickness = 1 });
        arena.Children.Add(new Line { X1 = 0, X2 = ArenaWidth, Y1 = ArenaHeight / 2, Y2 = ArenaHeight / 2, Stroke = DS.Brush("DS.Hairline"), StrokeThickness = 1 });
        arena.Children.Add(targetDot);
        arena.Children.Add(crosshair);

        var startBtn = new Button { Content = "Start 30s Drill", Style = DS.Style("DS.ConsolePrimary"), Padding = new(16, 6, 16, 6), Margin = new(0, 0, 10, 0) };
        startBtn.Click += (_, _) =>
        {
            lastTickMs = Environment.TickCount64;
            session.Start(30, lastTickMs);
            SyncArena();
        };

        var closeBtn = new Button { Content = "Close", Style = DS.Style("DS.ConsoleBtn"), Padding = new(14, 6, 14, 6) };
        closeBtn.Click += (_, _) => Close();

        var root = new StackPanel { Margin = new(24, 18, 24, 18) };
        root.Children.Add(Kit.Section("AIM TRAINER", 0));
        root.Children.Add(hudText.Also(t => t.Margin = new(0, 4, 0, 12)));
        root.Children.Add(new Border
        {
            CornerRadius = ThemeCatalog.Round(10),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Child = arena,
        });
        root.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0), Children = { startBtn, closeBtn } });
        Content = root;

        Loaded += (_, _) =>
        {
            try { reader = new WindowsReader("XInput1_4.dll"); } catch { }
            lastTickMs = Environment.TickCount64;
            session.Start(30, lastTickMs);
            SyncArena();
            timer.Tick += (_, _) => TickFrame();
            timer.Start();
        };
        Closed += (_, _) =>
        {
            timer.Stop();
            reader?.Dispose();
            reader = null;
        };
    }

    void TickFrame()
    {
        long now = Environment.TickCount64;
        double dt = Math.Clamp((now - lastTickMs) / 1000.0, 0.004, 0.1);
        lastTickMs = now;

        double sx = 0, sy = 0;
        bool fire = false;
        if (reader != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (PcModeService.IsVirtual(i)) continue;
                if (reader.Read(i, out var st))
                {
                    sx = Math.Abs(st.Pad.RX) > 2500 ? st.Pad.RX / 32768.0 : 0.0;
                    sy = Math.Abs(st.Pad.RY) > 2500 ? st.Pad.RY / 32768.0 : 0.0;
                    fire = st.Pad.RT > 80 || (st.Pad.Buttons & 0x1000) != 0; // RT or A
                    break;
                }
            }
        }

        if (session.Running)
            session.Step(sx, sy, fire, dt, now);

        SyncArena();
    }

    void SyncArena()
    {
        double rx = session.TargetRadius * (ArenaWidth / 2);
        double ry = session.TargetRadius * (ArenaHeight / 2);
        double diameter = Math.Max(14, Math.Min(rx, ry) * 2);
        targetDot.Width = diameter;
        targetDot.Height = diameter;
        Canvas.SetLeft(targetDot, (session.TargetX + 1.0) * 0.5 * ArenaWidth - diameter / 2);
        Canvas.SetTop(targetDot, (1.0 - session.TargetY) * 0.5 * ArenaHeight - diameter / 2);

        Canvas.SetLeft(crosshair, (session.CursorX + 1.0) * 0.5 * ArenaWidth - 7);
        Canvas.SetTop(crosshair, (1.0 - session.CursorY) * 0.5 * ArenaHeight - 7);

        hudText.Text = session.Running
            ? $"Time {session.TimeRemainingSec:0.0}s · Hits {session.Hits} · Accuracy {session.HitRatePct:0}% · Avg Reaction {session.AvgReactionMs:0} ms"
            : $"Finished · Score {session.Hits} hits ({session.HitRatePct:0}% accuracy) · Avg Reaction {session.AvgReactionMs:0} ms";
    }
}
