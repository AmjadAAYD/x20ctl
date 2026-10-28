using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Product.Diagnostics;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private DiagnosticsWorkbench? pulseDiagBench;
    private bool pulseDiagDetails;
    private TextBlock? pulseDiagRate;

    private void BuildPulseDiagnostics(Canvas host)
    {
        var w = Diagnostics;
        if (w == null) return;

        if (!ReferenceEquals(w, pulseDiagBench))
        {
            pulseDiagBench = w;
            w.Refreshed += () =>
            {
                if (CurrentPage == "Diagnostics" && ReferenceEquals(pulseDiagBench, Diagnostics))
                    PulseLater();
            };
        }

        foreach (var tb in new[] { w.SourceBlock, w.RateBlock, w.RawBlock, w.NumericBlock, w.DriftBlock })
            (tb.Parent as Panel)?.Children.Remove(tb);

        // 1. Frame Header per t_pulse.py:712
        var title = PulseParts.Disp("Diagnostics", 40, PulseParts.TextPrimary, FontWeights.ExtraLight);
        Canvas.SetLeft(title, 272);
        Canvas.SetTop(title, 30);
        host.Children.Add(title);

        var lead = PulseParts.Body("Inspect raw stick circles, trigger pull depth, and packet timings directly from OS API.", 15, PulseParts.TextSoft);
        lead.Width = 800;
        Canvas.SetLeft(lead, 272);
        Canvas.SetTop(lead, 80);
        host.Children.Add(lead);

        var status = PulseParts.Body(PulseStatusString(), 14, PulseParts.TextSoft);
        Canvas.SetRight(status, 44);
        Canvas.SetTop(status, 36);
        host.Children.Add(status);

        // 2. Toolbar Chips at (x=272, y=140, w=1280)
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        int shown = w.ChosenSlot >= 0 ? w.ChosenSlot : w.ActiveSlot >= 0 ? w.ActiveSlot : w.PreferredSlot;

        var playerBtn = PulseParts.ChipButton($"Player input: XInput {shown + 1} ▾", active: true, () =>
        {
            w.ChooseSlot(((w.ChosenSlot < 0 ? w.PreferredSlot : w.ChosenSlot) + 1) % 4);
            PulseLater();
        });
        bar.Children.Add(playerBtn);

        bar.Children.Add(PulseParts.ChipButton("Retry player input", active: false, () =>
        {
            w.ChooseSlot(-1);
            PulseLater();
        }));

        bar.Children.Add(PulseParts.ChipButton(w.RawValues ? "Raw: Integer On" : "Raw: Normal", active: w.RawValues, () =>
        {
            w.ToggleRaw();
            PulseLater();
        }));

        bar.Children.Add(PulseParts.ChipButton(w.Measuring ? "Deviation: Measuring…" : "Measure deviation", active: w.Measuring, () =>
        {
            w.Measure();
            PulseLater();
        }));

        bar.Children.Add(PulseParts.ChipButton("Clear data", active: false, () =>
        {
            w.ClearAll();
            PulseLater();
        }));

        bar.Children.Add(PulseParts.ChipButton("Copy report", active: false, () =>
        {
            w.CopyReport();
            PulseLater();
        }));

        bar.Children.Add(PulseParts.ChipButton(pulseDiagDetails ? "View: Scopes" : "View: Details", active: pulseDiagDetails, () =>
        {
            pulseDiagDetails = !pulseDiagDetails;
            PulseLater();
        }));

        Canvas.SetLeft(bar, 272);
        Canvas.SetTop(bar, 140);
        host.Children.Add(bar);

        if (!pulseDiagDetails)
        {
            // 3. Scopes for Left and Right Stick per t_pulse.py:717 (cx=470, cx=830, cy=420, r=118)
            PulseDiagScope(host, w, leftStick: true, cx: 470, cy: 420, r: 118);
            PulseDiagScope(host, w, leftStick: false, cx: 830, cy: 420, r: 118);

            // 4. Trigger Ring Dials for LT and RT per t_pulse.py:719 (cx=1160, cx=1400, cy=410, r=86)
            PulseDiagTrigger(host, "LT", cx: 1160, cy: 410, r: 86);
            PulseDiagTrigger(host, "RT", cx: 1400, cy: 410, r: 86);
        }
        else
        {
            // Details View
            var detailsCard = PulseParts.GlassCard(new StackPanel
            {
                Children =
                {
                    PulseParts.Kicker("DETAILED NUMERIC READOUT"),
                    w.NumericBlock.Also(tb => { tb.FontFamily = PulseParts.MonoFont; tb.FontSize = 14; tb.Foreground = PulseInk.B(PulseParts.TextPrimary); tb.Margin = new(0, 10, 0, 10); }),
                    PulseParts.Kicker("RAW PACKET LOG"),
                    w.RawBlock.Also(tb => { tb.FontFamily = PulseParts.MonoFont; tb.FontSize = 12.5; tb.Foreground = PulseInk.B(PulseParts.TextSoft); tb.Margin = new(0, 10, 0, 0); })
                }
            }, new Thickness(28), radius: 24);
            detailsCard.Width = 1280;
            detailsCard.Height = 440;
            Canvas.SetLeft(detailsCard, 262);
            Canvas.SetTop(detailsCard, 190);
            host.Children.Add(detailsCard);
        }

        // 5. Rising Pearl card along the bottom (x=262, y=682, w=1300, h=145) per t_pulse.py:725
        var bottomStack = new DockPanel { LastChildFill = true };

        var ratePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 48, 0) };
        ratePanel.Children.Add(PulseParts.Kicker("OBSERVED INPUT RATE", onPearl: true));
        pulseDiagRate = PulseParts.Disp(w.RateBlock.Text is { Length: > 0 } rt ? rt : "1000 Hz", 26, PulseParts.PearlInk, FontWeights.Bold);
        ratePanel.Children.Add(pulseDiagRate);
        DockPanel.SetDock(ratePanel, Dock.Left);
        bottomStack.Children.Add(ratePanel);

        var sourcePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        w.SourceBlock.FontFamily = PulseParts.BodyFont;
        w.SourceBlock.FontSize = 15;
        w.SourceBlock.Foreground = PulseInk.B(PulseParts.PearlMid);
        w.SourceBlock.TextWrapping = TextWrapping.Wrap;
        w.SourceBlock.Margin = new(0);
        sourcePanel.Children.Add(w.SourceBlock);

        var note = PulseParts.Body("Read-only input capture · Safe live diagnostic · Esc to leave", 13, PulseParts.PearlSoft);
        note.Margin = new(0, 6, 0, 0);
        sourcePanel.Children.Add(note);
        bottomStack.Children.Add(sourcePanel);

        var pearl = PulseParts.RisingPearlCard(bottomStack, new Thickness(36, 22, 36, 22));
        pearl.Width = 1300;
        pearl.Height = 145;
        Canvas.SetLeft(pearl, 262);
        Canvas.SetTop(pearl, 682);
        host.Children.Add(pearl);
    }

    private static void PulseDiagScope(Canvas host, DiagnosticsWorkbench w, bool leftStick, double cx, double cy, double r)
    {
        var (reach, dot, rest, stats) = leftStick ? w.LeftView : w.RightView;
        foreach (UIElement e in new UIElement[] { reach, dot, rest, stats })
            (e as FrameworkElement)?.Parent.Also(p => (p as Panel)?.Children.Remove(e));

        var c = new Canvas { Width = r * 2 + 80, Height = r * 2 + 80, IsHitTestVisible = false };
        double o = r + 40;

        // Outer glow rim
        c.Children.Add(new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.45)),
            StrokeThickness = 1.4,
            Effect = PulseInk.Glow(PulseInk.Cyan, 12, 0.6)
        }.Also(e => { Canvas.SetLeft(e, o - r); Canvas.SetTop(e, o - r); }));

        // Concentric harmonic division rings
        foreach (var k in new[] { 0.33, 0.66 })
        {
            c.Children.Add(new Ellipse
            {
                Width = 2 * r * k,
                Height = 2 * r * k,
                Stroke = PulseInk.B(PulseInk.A(Colors.White, 0.14)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([3, 5])
            }.Also(e => { Canvas.SetLeft(e, o - r * k); Canvas.SetTop(e, o - r * k); }));
        }

        // Crosshairs
        c.Children.Add(new Line { X1 = o - r, Y1 = o, X2 = o + r, Y2 = o, Stroke = PulseInk.B(PulseInk.A(Colors.White, 0.18)), StrokeThickness = 1 });
        c.Children.Add(new Line { X1 = o, Y1 = o - r, X2 = o, Y2 = o + r, Stroke = PulseInk.B(PulseInk.A(Colors.White, 0.18)), StrokeThickness = 1 });

        // Resonance wings
        c.Children.Add(PulseParts.ResonanceMark(r + 16));

        // Live reach & deflection plot
        double plot = DiagnosticsWorkbench.PlotSize;
        double f = r / DiagnosticsWorkbench.UnitSize;
        var live = new Canvas
        {
            Width = plot,
            Height = plot,
            RenderTransform = new ScaleTransform(f, f, plot / 2, plot / 2),
            IsHitTestVisible = false
        };
        reach.Stroke = PulseInk.B(PulseParts.CyanBright);
        reach.StrokeThickness = 1.6;
        reach.Fill = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.22));
        rest.Fill = PulseInk.B(PulseInk.Warm);
        live.Children.Add(reach);
        live.Children.Add(rest);
        live.Children.Add(dot);
        Canvas.SetLeft(live, o - plot / 2);
        Canvas.SetTop(live, o - plot / 2);
        c.Children.Add(live);

        Canvas.SetLeft(c, cx - o);
        Canvas.SetTop(c, cy - o);
        host.Children.Add(c);

        // Scope Label
        var labelStack = new StackPanel { Width = 260, HorizontalAlignment = HorizontalAlignment.Center };
        labelStack.Children.Add(PulseParts.Disp(leftStick ? "Left stick" : "Right stick", 18, PulseParts.TextPrimary, FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        stats.FontFamily = PulseParts.BodyFont;
        stats.FontSize = 12.5;
        stats.Foreground = PulseInk.B(PulseParts.TextSoft);
        stats.TextAlignment = TextAlignment.Center;
        stats.TextWrapping = TextWrapping.Wrap;
        stats.Margin = new(0, 4, 0, 0);
        labelStack.Children.Add(stats);

        Canvas.SetLeft(labelStack, cx - 130);
        Canvas.SetTop(labelStack, cy + r + 24);
        host.Children.Add(labelStack);
    }

    private static void PulseDiagTrigger(Canvas host, string name, double cx, double cy, double r)
    {
        var ring = PulseParts.RingDial(0.0, name, "0%", size: r * 2, accent: PulseParts.CyanBright);
        Canvas.SetLeft(ring, cx - r);
        Canvas.SetTop(ring, cy - r);
        host.Children.Add(ring);

        var subLabel = PulseParts.Body("Analog Trigger", 12.5, PulseParts.TextSoft);
        subLabel.HorizontalAlignment = HorizontalAlignment.Center;
        Canvas.SetLeft(subLabel, cx - 60);
        Canvas.SetTop(subLabel, cy + r + 18);
        host.Children.Add(subLabel);
    }
}
