using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    public Action<string>? PulseNavigate { get; set; }

    private void BuildPulseReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var contentCanvas = new Canvas { Width = 1600, Height = 900 };
        var board = PulseToolFrame.WrapWithOrbit(
            "Tools",
            "Before you scan",
            "This is a full controller scan; you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports. Here is what it does and what to expect.",
            "Back to Tools",
            () => close(),
            PulseNavigate,
            contentCanvas,
            [("A", "Select"), ("B", "Back")]
        );
        warning.Background = new SolidColorBrush(PulseParts.VoidColor);
        warning.Child = board;

        static void Detach(FrameworkElement e)
        {
            if (e.Parent is Panel p) p.Children.Remove(e);
            else if (e.Parent is ContentControl cc) cc.Content = null;
            else if (e.Parent is Decorator dec) dec.Child = null;
        }
        Detach(systemRow);
        Detach(choices);
        Detach(linux);

        // 1. System choices at top (x=272, y=166) per t_pulse.py:932
        systemRow.Margin = new(0);
        if (systemRow is StackPanel systemStack)
        {
            systemStack.Orientation = Orientation.Horizontal;
            if (systemStack.Children.OfType<TextBlock>().FirstOrDefault() is { } caption)
            {
                caption.Text = "WHICH SYSTEM IS THE CONTROLLER CONNECTED TO?";
                caption.Foreground = PulseInk.B(PulseParts.CyanBright);
                caption.FontFamily = PulseParts.MonoFont;
                caption.FontSize = 12;
                caption.VerticalAlignment = VerticalAlignment.Center;
                caption.Margin = new(0, 0, 16, 0);
            }
        }
        Canvas.SetLeft(systemRow, 272);
        Canvas.SetTop(systemRow, 166);
        contentCanvas.Children.Add(systemRow);

        // 2. Five pulse steps on harmonic wave (x=272, y=290, w=1280) per t_pulse.py:933
        double waveW = 1280;
        double[] xs = [80, 360, 640, 920, 1200];
        string[] steps = ["Start the check", "Find your slot", "Confirm it's yours", "Test every control", "Save the report"];

        var waveCanvas = new Canvas { Width = waveW, Height = 100, IsHitTestVisible = false };

        // Continuous sine wave with peaks at xs
        var waveGeo = new StreamGeometry();
        using (var ctx = waveGeo.Open())
        {
            ctx.BeginFigure(new Point(0, 50), false, false);
            for (int p = 0; p < xs.Length; p++)
            {
                double px = xs[p];
                double amp = p == 0 ? 18 : 8;
                ctx.LineTo(new Point(px - 40, 50), true, false);
                ctx.BezierTo(new Point(px - 20, 50 - amp), new Point(px + 20, 50 - amp), new Point(px + 40, 50), true, false);
            }
            ctx.LineTo(new Point(waveW, 50), true, false);
        }
        waveGeo.Freeze();

        var wavePath = new Path
        {
            Data = waveGeo,
            Stroke = new LinearGradientBrush(PulseInk.C("#5cb3ff"), PulseInk.C("#a8ccff"), 0),
            StrokeThickness = 1.8,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.7 }
        };
        waveCanvas.Children.Add(wavePath);

        // Baseline across wave
        var baseLine = new Line { X1 = 0, Y1 = 50, X2 = waveW, Y2 = 50, Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.12)), StrokeThickness = 1 };
        waveCanvas.Children.Add(baseLine);

        // Resonance mark around Step 1
        var res1 = PulseParts.ResonanceMark(34);
        Canvas.SetLeft(res1, xs[0] - 34);
        Canvas.SetTop(res1, 50 - 18 - 34);
        waveCanvas.Children.Add(res1);

        for (int i = 0; i < steps.Length; i++)
        {
            double sx = xs[i];
            bool on = i == 0;
            double orbD = on ? 44 : 34;

            var orb = new Border
            {
                Width = orbD,
                Height = orbD,
                CornerRadius = new(orbD / 2),
                Background = on ? PulseParts.PearlWhiteBrush : PulseInk.B(PulseInk.A(PulseInk.C("#080c18"), 0.85)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, on ? 0.95 : 0.25)),
                BorderThickness = new(on ? 1.5 : 1),
                Effect = on ? new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.85 } : null,
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = PulseParts.BodyFont,
                    FontSize = on ? 15 : 12,
                    FontWeight = on ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = on ? PulseInk.B(PulseParts.PearlInk) : PulseInk.B(PulseInk.C("#dfe5ee")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(orb, sx - orbD / 2);
            Canvas.SetTop(orb, 50 - (on ? 18 : 8) - orbD / 2);
            waveCanvas.Children.Add(orb);

            var label = new TextBlock
            {
                Text = steps[i],
                FontFamily = PulseParts.BodyFont,
                FontSize = on ? 15 : 13,
                FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = on ? Brushes.White : PulseInk.B(PulseInk.C("#aab4c4")),
                TextAlignment = TextAlignment.Center,
                Width = 200
            };
            Canvas.SetLeft(label, sx - 100);
            Canvas.SetTop(label, 50 + 26);
            waveCanvas.Children.Add(label);
        }

        Canvas.SetLeft(waveCanvas, 272);
        Canvas.SetTop(waveCanvas, 290);
        contentCanvas.Children.Add(waveCanvas);

        // 3. Rising Pearl Card along bottom (x=262, y=400, w=1300, h=390) per t_pulse.py:944
        var pearlGrid = new Grid { Width = 1220 };
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left column: Active step details per t_pulse.py:940
        var leftCol = new StackPanel();
        leftCol.Children.Add(PulseParts.Kicker("1", onPearl: true));
        leftCol.Children.Add(PulseParts.Disp("Reads, never writes", 27, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(0, 6, 0, 8)));
        leftCol.Children.Add(PulseParts.Body("The scan only listens to what the controller already tells Windows. No settings, lighting, vibration or firmware are touched.", 15.5, PulseParts.PearlMid, wrap: true));
        leftCol.Children.Add(PulseParts.Chip("Read-only · nothing is changed or uploaded", "on", onPearl: true).Also(c => c.Margin = new(0, 14, 0, 0)));

        if (choices.Parent is Panel pChoices) pChoices.Children.Remove(choices);
        choices.Margin = new(0, 18, 0, 0);
        if (choices is Panel pActions)
        {
            foreach (var b in pActions.Children.OfType<Button>())
            {
                b.Height = 44;
                b.Margin = new(0, 0, 10, 0);
            }
        }
        leftCol.Children.Add(choices);
        Grid.SetColumn(leftCol, 0);
        pearlGrid.Children.Add(leftCol);

        // Right column: remaining card list per t_pulse.py:942
        var remainingItems = new[]
        {
            "Buttons, sticks and triggers",
            "Battery",
            "Stick resolution and report rate",
            "Lighting (RGB)",
            "Macro paddles (M buttons)",
            "Home, M and turbo",
            "Every step tells you what to do"
        };

        var rightCol = new StackPanel { Margin = new(0, -6, 0, 0) };
        for (int i = 0; i < remainingItems.Length; i++)
        {
            var rowBorder = new Border
            {
                Height = 38,
                CornerRadius = new(10),
                Padding = new(12, 0, 12, 0),
                Margin = new(0, 0, 0, 3),
                Background = PulseInk.B(PulseInk.A(Colors.White, 0.45)),
                BorderBrush = PulseInk.B(PulseInk.A(PulseParts.PearlMid, 0.15)),
                BorderThickness = new(1)
            };

            var rowDock = new DockPanel { LastChildFill = true };
            var num = PulseParts.Mono((i + 2).ToString(), 13, PulseParts.CyanBright, FontWeights.Bold).Also(t => { t.Width = 22; DockPanel.SetDock(t, Dock.Left); });
            var chev = PulseParts.Mono("›", 15, PulseParts.PearlSoft).Also(t => { DockPanel.SetDock(t, Dock.Right); });
            var title = PulseParts.Body(remainingItems[i], 14.5, PulseParts.PearlInk, FontWeights.Normal).Also(t => t.VerticalAlignment = VerticalAlignment.Center);

            rowDock.Children.Add(num);
            rowDock.Children.Add(chev);
            rowDock.Children.Add(title);
            rowBorder.Child = rowDock;

            rightCol.Children.Add(rowBorder);
        }
        Grid.SetColumn(rightCol, 2);
        pearlGrid.Children.Add(rightCol);

        // Bottom report disclaimer inside pearl card per t_pulse.py:945
        var reportNote = PulseParts.Body("Your report: the full ZIP is always saved to Documents\\X20CTLInputReports. \"Scan and send\" transmits only hardware IDs and controller results to the team.", 12.5, PulseParts.PearlSoft, wrap: true);
        reportNote.Margin = new(0, 16, 0, 0);

        var pearlStack = new StackPanel { Children = { pearlGrid, reportNote } };
        var pearl = PulseParts.RisingPearlCard(pearlStack, new Thickness(30, 24, 30, 24), topRadius: 30, bottomRadius: 24);
        pearl.Width = 1300;
        pearl.Height = 390;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 400);
        contentCanvas.Children.Add(pearl);
    }

    private void BuildPulseReadFlow(Grid originalPage)
    {
        originalPage.Background = new SolidColorBrush(PulseParts.VoidColor);
    }
}
