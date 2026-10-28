using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    public Action<string>? PulseNavigate { get; set; }

    private void BuildPulseWriteWarning(Panel cards, Button go, Button leave)
    {
        var contentCanvas = new Canvas { Width = 1600, Height = 900 };
        var board = PulseToolFrame.WrapWithOrbit(
            "Tools",
            "Tools / Write Scan",
            "Safe Bluetooth Log Inspection",
            "Back to Tools",
            () => close(),
            PulseNavigate,
            contentCanvas,
            [("A", "Start"), ("B", "Back")]
        );
        warning.Background = new SolidColorBrush(PulseParts.VoidColor);
        warning.Child = board;

        // 1. Left Stage: 4 Numbered Expectation Cards (x=262, y=140, w=760)
        var leftStage = new StackPanel { Width = 760 };
        leftStage.Children.Add(PulseParts.Kicker("PREREQUISITES & SAFETY"));
        leftStage.Children.Add(PulseParts.Disp("What to Expect", 24, PulseParts.TextPrimary, FontWeights.Bold).Also(t => t.Margin = new(0, 4, 0, 16)));

        string[] titles = ["This can take 20 to 40 minutes", "You need a mobile phone", "X20CTL sends nothing to hardware", "Read scan vs Write scan"];
        string[] bodies =
        [
            "Plan on 20 to 40 minutes the first time: turning on developer Bluetooth logging, running a small test, and transferring the log file to this PC.",
            "Android or iPhone with KeyLinker installed and controller paired. Both platforms can capture standard HCI Bluetooth packets.",
            "X20CTL never communicates directly with the controller during this scan. You make one change in the official app, and we read the phone log.",
            "The Read scan inspects what your PC sees. The Write scan teaches X20CTL how to configure new features safely."
        ];

        for (int i = 0; i < 4; i++)
        {
            var numOrb = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new(16),
                Background = i == 0 ? PulseInk.B(PulseParts.CyanBright) : PulseInk.B(PulseInk.A(Colors.White, 0.08)),
                BorderBrush = PulseInk.B(i == 0 ? Colors.White : PulseInk.A(Colors.White, 0.2)),
                BorderThickness = new(1),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new(0, 2, 16, 0),
                Child = PulseParts.Disp((i + 1).ToString(), 13, i == 0 ? PulseParts.PearlInk : PulseParts.TextPrimary, FontWeights.Bold).Also(t =>
                {
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                })
            };

            var textStack = new StackPanel();
            textStack.Children.Add(PulseParts.Body(titles[i], 15, PulseParts.TextPrimary, FontWeights.SemiBold));
            textStack.Children.Add(PulseParts.Body(bodies[i], 12.5, PulseParts.TextSoft, wrap: true).Also(t => t.Margin = new(0, 2, 0, 0)));

            var rowDock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(numOrb, Dock.Left);
            rowDock.Children.Add(numOrb);
            rowDock.Children.Add(textStack);

            var rowBorder = PulseParts.GlassRow(rowDock, compact: true);
            leftStage.Children.Add(rowBorder);
        }

        Canvas.SetLeft(leftStage, 262);
        Canvas.SetTop(leftStage, 140);
        contentCanvas.Children.Add(leftStage);

        // 2. Right Stage: Giant Chronograph Dial (cx=1320, cy=360, r=150)
        double cx = 1320, cy = 360, r = 150;
        var clockCanvas = new Canvas { Width = r * 2 + 100, Height = r * 2 + 100, IsHitTestVisible = false };
        double o = r + 50;

        // Aura & Resonance
        clockCanvas.Children.Add(PulseKit.Aura(o, o, 190, 190, 0.25));
        clockCanvas.Children.Add(PulseKit.Resonance(o, o, r + 24, pairs: 2, span: 26, op: 0.8));

        // Outer minute ticks (60 marks)
        for (int m = 0; m < 60; m++)
        {
            double ang = m * 6 - 90;
            double rad = ang * Math.PI / 180.0;
            bool major = m % 5 == 0;
            double len = major ? 12 : 6;
            double r0 = r + 14;
            double r1 = r0 - len;

            clockCanvas.Children.Add(new Line
            {
                X1 = o + r1 * Math.Cos(rad),
                Y1 = o + r1 * Math.Sin(rad),
                X2 = o + r0 * Math.Cos(rad),
                Y2 = o + r0 * Math.Sin(rad),
                Stroke = PulseInk.B(PulseInk.A(Colors.White, major ? 0.45 : 0.18)),
                StrokeThickness = major ? 1.4 : 0.9
            });
        }

        // Highlight arc for 20-40 min range (120 deg to 240 deg - 90 = 30 deg to 150 deg)
        var arc = PulseParts.CreateArcPath(o, o, r, 30, 150, PulseParts.CyanBright, 12);
        clockCanvas.Children.Add(arc);

        // Center readout: "20–40 min"
        var centerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        centerStack.Children.Add(PulseParts.Disp("20–40", 48, PulseParts.TextPrimary, FontWeights.Light).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        centerStack.Children.Add(PulseParts.Kicker("MINUTES", overrideColor: PulseParts.CyanBright).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        Canvas.SetLeft(centerStack, o - 90);
        Canvas.SetTop(centerStack, o - 44);
        clockCanvas.Children.Add(centerStack);

        Canvas.SetLeft(clockCanvas, cx - o);
        Canvas.SetTop(clockCanvas, cy - o);
        contentCanvas.Children.Add(clockCanvas);

        // 3. Bottom Rising Pearl Card for Actions & Privacy (x=262, y=670, w=1280, h=155)
        var bottomStack = new DockPanel { LastChildFill = true };

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(24, 0, 0, 0) };
        if (go.Parent is Panel pGo) pGo.Children.Remove(go);
        if (leave.Parent is Panel pLeave) pLeave.Children.Remove(leave);

        var startBtn = PulseParts.PillButton("I Have the Time · Start", primary: true, onPearl: true, onClick: () => go.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
        var backBtn = PulseParts.PillButton("Back to Tools", primary: false, onPearl: true, onClick: () => leave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Also(b => b.Margin = new(12, 0, 0, 0));
        actions.Children.Add(startBtn);
        actions.Children.Add(backBtn);
        DockPanel.SetDock(actions, Dock.Right);
        bottomStack.Children.Add(actions);

        var noteStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        noteStack.Children.Add(PulseParts.Kicker("LOCAL LOG ANALYSIS ONLY", onPearl: true));
        noteStack.Children.Add(PulseParts.Body("Nothing leaves your computer until you review the exact findings and press Send Online at the end of the scan. Results are saved only to Documents\\X20CTLWriteScans.", 13, PulseParts.PearlMid, wrap: true));
        bottomStack.Children.Add(noteStack);

        var pearl = PulseParts.RisingPearlCard(bottomStack, new Thickness(36, 22, 36, 22));
        pearl.Width = 1280;
        pearl.Height = 150;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 670);
        contentCanvas.Children.Add(pearl);
    }

    private void BuildPulseWriteFlow(Grid originalPage)
    {
        originalPage.Background = new SolidColorBrush(PulseParts.VoidColor);
    }

    private void BuildPulseWriteSteps()
    {
        // Stylize step chips in write scan
        strip.Children.Clear();
        for (int i = 0; i < StepNames.Length; i++)
        {
            bool done = i < step, current = i == step;
            var chip = PulseParts.Chip($"{i + 1}. {StepNames[i]}", current ? "on" : done ? "ok" : "default");
            chip.Margin = new(0, 0, 8, 0);
            strip.Children.Add(chip);
        }
    }

    private FrameworkElement PulseWritePlan() => PlanPage();
    private FrameworkElement PulseWriteTest(FrameworkElement testPage) => testPage;
    private FrameworkElement PulseWriteResults(FrameworkElement resultsPage) => resultsPage;
}
