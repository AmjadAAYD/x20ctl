using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private TesterWorkbench? pulseTesterBench;

    private void BuildPulseTester(Canvas host)
    {
        var w = Tester;
        if (w == null) return;

        if (!ReferenceEquals(w, pulseTesterBench))
        {
            pulseTesterBench = w;
            w.Updated += () =>
            {
                if (CurrentPage == "Tester" && ReferenceEquals(pulseTesterBench, Tester))
                    PulseLater();
            };
        }

        // Frame Header per t_pulse.py:808
        var title = PulseParts.Disp("Tester", 44, PulseParts.TextPrimary, FontWeights.ExtraLight);
        Canvas.SetLeft(title, 272);
        Canvas.SetTop(title, 30);
        host.Children.Add(title);

        var lead = PulseParts.Body("Live stick and trigger verification · Real-time controller feedback", 15, PulseParts.TextSoft);
        Canvas.SetLeft(lead, 272);
        Canvas.SetTop(lead, 82);
        host.Children.Add(lead);

        var status = PulseParts.Body(PulseStatusString(), 14, PulseParts.TextSoft);
        Canvas.SetRight(status, 44);
        Canvas.SetTop(status, 36);
        host.Children.Add(status);

        // 1. Controller Stage: cx=560, cy=400, w=520 per t_pulse.py:807
        double cx = 560, cy = 400, wPad = 520;
        double hPad = wPad / 1.5;
        double x0 = cx - wPad / 2;
        double y0 = cy - hPad / 2;

        // Aura and ripples
        host.Children.Add(PulseKit.Aura(cx, cy + 20, 400, 250, 0.18));
        host.Children.Add(PulseKit.Ripples(cx, cy, n: 6, r0: 220, step: 48, op: 0.08));

        // Pedestal Controller
        var padView = PulseParts.ControllerPedestal(Model.Id, rear: w.RearShown, width: wPad, height: hPad, showRipples: false, finish: PadColours.For(Player, Model.Id));
        Canvas.SetLeft(padView, x0);
        Canvas.SetTop(padView, y0);
        host.Children.Add(padView);

        // Control rings overlay around key inputs per t_pulse.py:813
        var ringsCanvas = new Canvas { Width = wPad, Height = hPad, IsHitTestVisible = false };
        foreach (var (fx, fy, r) in new[]
        {
            (0.27, 0.42, 44.0),  // Left Stick
            (0.62, 0.63, 44.0),  // Right Stick
            (0.37, 0.66, 40.0),  // D-Pad
            (0.755, 0.39, 50.0), // ABXY
            (0.20, 0.06, 30.0),  // LB/LT
            (0.80, 0.06, 30.0)   // RB/RT
        })
        {
            var ring = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.35)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection([3, 6])
            };
            Canvas.SetLeft(ring, fx * wPad - r);
            Canvas.SetTop(ring, fy * hPad - r);
            ringsCanvas.Children.Add(ring);
        }
        Canvas.SetLeft(ringsCanvas, x0);
        Canvas.SetTop(ringsCanvas, y0);
        host.Children.Add(ringsCanvas);

        // Front / Rear toggle and Player Badge under controller at (cx - 150, cy + hPad / 2 + 16)
        var tabStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        tabStack.Children.Add(PulseParts.ChipButton("Front face", !w.RearShown, () => { w.ShowRear(false); PulseLater(); }));
        tabStack.Children.Add(PulseParts.ChipButton("Rear paddles", w.RearShown, () => { w.ShowRear(true); PulseLater(); }).Also(b => b.Margin = new(8, 0, 0, 0)));
        tabStack.Children.Add(PulseParts.Chip($"Player {Player + 1}", "on").Also(c => c.Margin = new(12, 0, 0, 0)));

        Canvas.SetLeft(tabStack, cx - 150);
        Canvas.SetTop(tabStack, cy + hPad / 2 + 16);
        host.Children.Add(tabStack);

        // 2. Bottom Gauges at (272, 742) per t_pulse.py:822 (Left Stick, Right Stick, LT, RT)
        var bottomGauges = new StackPanel { Orientation = Orientation.Horizontal };

        // Left Stick Mini Scope (52px)
        bottomGauges.Children.Add(BuildMiniScope("Left stick", w.LeftX ?? 0, w.LeftY ?? 0));
        // Right Stick Mini Scope (52px)
        bottomGauges.Children.Add(BuildMiniScope("Right stick", w.RightX ?? 0, w.RightY ?? 0).Also(g => g.Margin = new(44, 0, 0, 0)));
        // Left Trigger Dial (52px)
        bottomGauges.Children.Add(BuildMiniTriggerDial("LT", w.LeftTrigger ?? 0.0).Also(g => g.Margin = new(44, 0, 0, 0)));
        // Right Trigger Dial (52px)
        bottomGauges.Children.Add(BuildMiniTriggerDial("RT", w.RightTrigger ?? 0.0).Also(g => g.Margin = new(44, 0, 0, 0)));

        Canvas.SetLeft(bottomGauges, 272);
        Canvas.SetTop(bottomGauges, 742);
        host.Children.Add(bottomGauges);

        // 3. Right Stage: Rising Pearl Card at (1000, 180, w=560, h=640) per t_pulse.py:828
        var pearlStack = new StackPanel();
        pearlStack.Children.Add(PulseParts.Kicker("ACTIVE GAMEPLAY SOURCE", onPearl: true));
        pearlStack.Children.Add(PulseParts.Disp(w.SourceText is { Length: > 0 } src ? src : "XInput Controller 1", 27, PulseParts.PearlInk, FontWeights.SemiBold).Also(t => t.Margin = new(0, 6, 0, 12)));
        pearlStack.Children.Add(PulseParts.Body("Listening directly to live gamepad events through the active runtime API.", 15, PulseParts.PearlMid));
        pearlStack.Children.Add(PulseParts.Body("Move sticks, squeeze triggers, or press face buttons to inspect real-time precision and dead-zone limits.", 15, PulseParts.PearlMid).Also(t => t.Margin = new(0, 6, 0, 8)));

        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 20, 0, 16) };
        actRow.Children.Add(PulseParts.PillButton("Close tester", primary: true, onPearl: true, onClick: () => PulseSelectPage("Dashboard")));
        actRow.Children.Add(PulseParts.PillButton("Keyboard test", primary: false, onPearl: true, onClick: () => w.KeyboardMode.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))).Also(b => b.Margin = new(12, 0, 0, 0)));
        pearlStack.Children.Add(actRow);

        pearlStack.Children.Add(PulseParts.Mono($"PRESSED: {(string.IsNullOrEmpty(w.PressedText) ? "None" : w.PressedText)}", 13, PulseParts.PearlMid));

        var pearlCard = PulseParts.RisingPearlCard(pearlStack, new Thickness(32, 30, 32, 30));
        pearlCard.Width = 560;
        pearlCard.Height = 640;
        Canvas.SetLeft(pearlCard, 1000);
        Canvas.SetTop(pearlCard, 180);
        host.Children.Add(pearlCard);

        // Bottom note at (272, 852)
        var bottomNote = PulseParts.Body("Input tester · All buttons, triggers, and axes are reflected in real time.", 13.5, PulseParts.TextSoft);
        Canvas.SetLeft(bottomNote, 272);
        Canvas.SetTop(bottomNote, 852);
        host.Children.Add(bottomNote);
    }

    private static FrameworkElement BuildMiniScope(string label, double x, double y)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var scopeCanvas = new Canvas { Width = 52, Height = 52 };
        var bg = new Ellipse
        {
            Width = 48,
            Height = 48,
            Fill = PulseInk.B(PulseInk.A(Colors.White, 0.03)),
            Stroke = PulseInk.B(PulseInk.A(PulseParts.PearlColor, 0.40)),
            StrokeThickness = 1.2
        };
        Canvas.SetLeft(bg, 2);
        Canvas.SetTop(bg, 2);
        scopeCanvas.Children.Add(bg);

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.White,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(dot, 26 + Math.Clamp(x, -1.0, 1.0) * 18 - 4);
        Canvas.SetTop(dot, 26 - Math.Clamp(y, -1.0, 1.0) * 18 - 4);
        scopeCanvas.Children.Add(dot);

        row.Children.Add(scopeCanvas);

        var textCol = new StackPanel { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        textCol.Children.Add(PulseParts.Disp(label, 17, PulseParts.TextPrimary, FontWeights.Normal));
        textCol.Children.Add(PulseParts.Mono($"{x:+0.00;-0.00;0.00}, {y:+0.00;-0.00;0.00}", 12.5, PulseParts.TextSoft));
        row.Children.Add(textCol);

        return row;
    }

    private static FrameworkElement BuildMiniTriggerDial(string label, double value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var dialCanvas = new Canvas { Width = 52, Height = 52 };
        var bg = new Ellipse
        {
            Width = 48,
            Height = 48,
            Fill = PulseInk.B(PulseInk.A(Colors.White, 0.03)),
            Stroke = PulseInk.B(PulseInk.A(PulseParts.PearlColor, 0.30)),
            StrokeThickness = 1.2
        };
        Canvas.SetLeft(bg, 2);
        Canvas.SetTop(bg, 2);
        dialCanvas.Children.Add(bg);

        if (value > 0.02)
        {
            var arc = new Ellipse
            {
                Width = 48,
                Height = 48,
                Stroke = PulseInk.B(PulseParts.CyanBright),
                StrokeThickness = 3.2,
                StrokeDashArray = new DoubleCollection([value * 15, 20])
            };
            Canvas.SetLeft(arc, 2);
            Canvas.SetTop(arc, 2);
            dialCanvas.Children.Add(arc);
        }

        row.Children.Add(dialCanvas);

        var textCol = new StackPanel { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        textCol.Children.Add(PulseParts.Disp(label, 17, PulseParts.TextPrimary, FontWeights.Normal));
        textCol.Children.Add(PulseParts.Mono($"{value * 100:0.0}%", 12.5, PulseParts.CyanBright));
        row.Children.Add(textCol);

        return row;
    }
}
