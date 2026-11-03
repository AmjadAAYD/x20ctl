using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? PulseNavigate { get; set; }
    private Canvas? pulseHost;
    private Action? pulseCloseAction;
    private Button? pulseResetButton;

    private void BuildPulseAdvanced(Action close, Button reset)
    {
        pulseCloseAction = close;
        pulseResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = new SolidColorBrush(PulseParts.VoidColor);

        pulseHost = new Canvas { Width = 1600, Height = 900 };
        Child = PulseToolFrame.WrapWithOrbit(
            "Curves",
            "Advanced stick tools",
            "PC Mode · 0.1% Fine Stick Calibration",
            "Back to Curves",
            () => close(),
            target => PulseNavigate?.Invoke(target),
            pulseHost,
            [("A", "Adjust"), ("B", "Done"), ("X", "Measure drift"), ("LB/RB", "Switch stick")]);

        RefreshPulseAdvanced();
    }

    private void RefreshPulseAdvanced()
    {
        if (pulseHost == null) return;
        pulseHost.Children.Clear();

        var r = rows[selectedStick];
        string stickLabel = selectedStick == CurveChannel.LeftStick ? "Left Stick" : "Right Stick";

        // Top tabs at (272, 138)
        var topTabs = new StackPanel { Orientation = Orientation.Horizontal };
        topTabs.Children.Add(PulseParts.ChipButton("Left stick", selectedStick == CurveChannel.LeftStick, () => SelectStick(CurveChannel.LeftStick)));
        topTabs.Children.Add(PulseParts.ChipButton("Right stick", selectedStick == CurveChannel.RightStick, () => SelectStick(CurveChannel.RightStick)));
        topTabs.Children.Add(PulseParts.ChipButton(linkSticks.IsChecked == true ? "Link L+R: On" : "Link L+R: Off", linkSticks.IsChecked == true, () =>
        {
            linkSticks.IsChecked = linkSticks.IsChecked != true;
            RefreshPulseAdvanced();
        }));
        topTabs.Children.Add(PulseParts.ChipButton(r.Raw.IsChecked == true ? "Bypass: On" : "Bypass: Off", r.Raw.IsChecked == true, () =>
        {
            r.Raw.IsChecked = r.Raw.IsChecked != true;
            RefreshPulseAdvanced();
        }));
        Canvas.SetLeft(topTabs, 272);
        Canvas.SetTop(topTabs, 138);
        pulseHost.Children.Add(topTabs);

        // Left stage: (262, 186, w=610)
        var leftStage = new StackPanel { Width = 610 };
        Canvas.SetLeft(leftStage, 262);
        Canvas.SetTop(leftStage, 186);

        leftStage.Children.Add(PulseParts.Kicker("INPUT RANGE").Also(k => k.Margin = new(4, 0, 0, 6)));
        leftStage.Children.Add(BuildSliderRow("Inner dead zone", "Resting movement ignored · 0.1% steps", r.Inner, $"{r.Inner.Value:0.0}%", active: true));
        leftStage.Children.Add(BuildSliderRow("Full output at", "Deflection where output reaches 100%", r.Outer, $"{r.Outer.Value:0.0}%"));

        leftStage.Children.Add(PulseParts.Kicker("OUTPUT RANGE").Also(k => k.Margin = new(4, 10, 0, 6)));
        leftStage.Children.Add(BuildSliderRow("Minimum output", "Counteracts heavy in-game dead zones", r.Anti, $"{r.Anti.Value:0.0}%"));
        leftStage.Children.Add(BuildSliderRow("Maximum output", "Caps the peak output sent to the game", r.Max, $"{r.Max.Value:0.0}%"));

        leftStage.Children.Add(PulseParts.Kicker("GATE & RATIO").Also(k => k.Margin = new(4, 10, 0, 6)));

        // Gate + Vertical Sensitivity row
        var gateRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        gateRight.Children.Add(PulseParts.ChipButton(r.Square.IsChecked == true ? "Square Gate" : "Circular Gate", r.Square.IsChecked == true, () =>
        {
            r.Square.IsChecked = r.Square.IsChecked != true;
            RefreshPulseAdvanced();
        }));
        var vSlider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = r.Ratio.Minimum,
            Maximum = r.Ratio.Maximum,
            Value = r.Ratio.Value,
            Width = 110,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(8, 0, 8, 0)
        };
        vSlider.ValueChanged += (_, e) => { r.Ratio.Value = e.NewValue; RefreshPulseAdvanced(); };
        gateRight.Children.Add(vSlider);
        gateRight.Children.Add(PulseParts.Mono($"V {r.Ratio.Value:0}%", 12.5, PulseParts.CyanBright, FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));

        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Gate shape & vertical ratio", 14, PulseParts.TextPrimary, FontWeights.SemiBold), PulseParts.Body("Circular/square boundary · vertical sensitivity", 11.5, PulseParts.TextSoft) } },
            gateRight,
            active: false,
            compact: true));

        // Action row: Measure drift & reset
        var actionRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actionRight.Children.Add(PulseParts.ChipButton("Measure drift (3s)", false, () => measure.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
        actionRight.Children.Add(PulseParts.ChipButton("Reset stick", false, () =>
        {
            pulseResetButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RefreshPulseAdvanced();
        }));
        leftStage.Children.Add(PulseParts.GlassRow(
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { PulseParts.Disp("Calibration actions", 14, PulseParts.TextPrimary, FontWeights.SemiBold), PulseParts.Body("Measure hands-off noise · reset to default", 11.5, PulseParts.TextSoft) } },
            actionRight,
            active: false,
            compact: true));

        pulseHost.Children.Add(leftStage);

        // Right side: Focused Setting as one big dial at (cx=1250, cy=356)
        double cx = 1250, cy = 356, rDial = 150;

        // Dial ring
        var dialCanvas = new Canvas { Width = 380, Height = 380 };
        Canvas.SetLeft(dialCanvas, cx - 190);
        Canvas.SetTop(dialCanvas, cy - 190);

        // Outer background ring
        var baseRing = new Ellipse
        {
            Width = rDial * 2,
            Height = rDial * 2,
            Stroke = PulseInk.B(PulseInk.A(PulseParts.PearlColor, 0.14)),
            StrokeThickness = 10,
            Fill = new RadialGradientBrush(PulseInk.A(PulseParts.CyanColor, 0.08), PulseInk.A(PulseParts.VoidColor, 0.0))
        };
        Canvas.SetLeft(baseRing, 190 - rDial);
        Canvas.SetTop(baseRing, 190 - rDial);
        dialCanvas.Children.Add(baseRing);

        // Active sweep ring arc
        double pct = Math.Clamp(r.Inner.Value / 25.0, 0.0, 1.0);
        var activeRing = new Ellipse
        {
            Width = rDial * 2,
            Height = rDial * 2,
            Stroke = PulseInk.B(PulseParts.CyanBright),
            StrokeThickness = 10,
            StrokeDashArray = new DoubleCollection([Math.Max(1.0, pct * 30), 40]),
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.85 }
        };
        Canvas.SetLeft(activeRing, 190 - rDial);
        Canvas.SetTop(activeRing, 190 - rDial);
        dialCanvas.Children.Add(activeRing);

        // Resonance mark
        var rmark = PulseParts.ResonanceMark(rDial + 24);
        Canvas.SetLeft(rmark, 190 - (rDial + 24));
        Canvas.SetTop(rmark, 190 - (rDial + 24));
        dialCanvas.Children.Add(rmark);

        pulseHost.Children.Add(dialCanvas);

        // Dial Center Text at (cx - 160, cy - 52, w=320)
        var dialCenter = new StackPanel { Width = 320, HorizontalAlignment = HorizontalAlignment.Center };
        dialCenter.Children.Add(PulseParts.Disp($"{r.Inner.Value:0.0}%", 72, PulseParts.TextPrimary, FontWeights.ExtraLight).Also(t => { t.TextAlignment = TextAlignment.Center; t.LineHeight = 72; }));
        dialCenter.Children.Add(PulseParts.Kicker("INNER DEAD ZONE").Also(t => { t.TextAlignment = TextAlignment.Center; t.Margin = new(0, 10, 0, 0); }));
        Canvas.SetLeft(dialCenter, cx - 160);
        Canvas.SetTop(dialCenter, cy - 52);
        pulseHost.Children.Add(dialCenter);

        // Preset chips under dial at (cx - 170, cy + 168)
        var presetsWrap = new WrapPanel { Width = 340, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (pName, pIn, pOut, pAnti, pSq) in new[]
        {
            ("Tight 3%", 3.0, 96.0, 2.0, false),
            ("Linear 5%", 5.0, 100.0, 0.0, false),
            ("Wide 10%", 10.0, 95.0, 0.0, false),
            ("Square 4%", 4.0, 94.0, 2.0, true)
        })
        {
            var (inV, outV, antiV, sqV) = (pIn, pOut, pAnti, pSq);
            presetsWrap.Children.Add(PulseParts.ChipButton(pName, Math.Abs(r.Inner.Value - inV) < 0.2 && r.Square.IsChecked == sqV, () =>
            {
                r.Inner.Value = inV;
                r.Outer.Value = outV;
                r.Anti.Value = antiV;
                r.Square.IsChecked = sqV;
                RefreshPulseAdvanced();
            }));
        }
        Canvas.SetLeft(presetsWrap, cx - 170);
        Canvas.SetTop(presetsWrap, cy + 168);
        pulseHost.Children.Add(presetsWrap);

        // Rising pearl card at (912, 664, w=650, h=160)
        var pearlContent = new StackPanel();
        pearlContent.Children.Add(PulseParts.Disp("Inner dead zone", 22, PulseParts.PearlInk, FontWeights.SemiBold));
        pearlContent.Children.Add(PulseParts.Body("Resting movement ignored · 0.1% steps to prevent unintended stick drift.", 14.5, PulseParts.PearlMid).Also(t => t.Margin = new(0, 6, 0, 16)));

        var pearlBtns = new StackPanel { Orientation = Orientation.Horizontal };
        pearlBtns.Children.Add(PulseParts.PillButton("Copy to other stick", primary: true, onPearl: true, onClick: () =>
        {
            var other = selectedStick == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick;
            CopyValues(r, rows[other]);
            RefreshPulseAdvanced();
        }).Also(b => b.Margin = new(0, 0, 12, 0)));
        pearlBtns.Children.Add(PulseParts.PillButton("Done · Back to Curves", primary: false, onPearl: true, onClick: () =>
        {
            pulseCloseAction?.Invoke();
            Done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        pearlContent.Children.Add(pearlBtns);

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, new Thickness(28, 24, 28, 24));
        pearlCard.Width = 650;
        pearlCard.Height = 168;
        Canvas.SetLeft(pearlCard, 912);
        Canvas.SetTop(pearlCard, 664);
        pulseHost.Children.Add(pearlCard);

        // Bottom note at (272, 852)
        var bottomNote = PulseParts.Body("PC Mode active · Changes apply instantly to virtual controller output.", 13.5, PulseParts.TextSoft);
        Canvas.SetLeft(bottomNote, 272);
        Canvas.SetTop(bottomNote, 852);
        pulseHost.Children.Add(bottomNote);
    }

    private void RefreshPulseAdvancedGraph(CurveChannel channel)
    {
        if (pulseHost != null && channel == selectedStick)
            RefreshPulseAdvanced();
    }

    private Border BuildSliderRow(string name, string desc, Slider source, string valueText, bool active = false)
    {
        var slider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = source.Minimum,
            Maximum = source.Maximum,
            Value = source.Value,
            Width = 190,
            TickFrequency = 0.1,
            IsSnapToTickEnabled = true,
            SmallChange = 0.1,
            LargeChange = 1.0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 10, 0)
        };
        slider.ValueChanged += (_, e) =>
        {
            source.Value = Math.Round(e.NewValue, 1);
            RefreshPulseAdvanced();
        };
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(slider);
        right.Children.Add(PulseParts.Mono(valueText, 14, PulseParts.CyanBright, FontWeights.Bold).Also(t => { t.Width = 56; t.TextAlignment = TextAlignment.Right; t.VerticalAlignment = VerticalAlignment.Center; }));

        return PulseParts.GlassRow(
            new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    PulseParts.Disp(name, 14, PulseParts.TextPrimary, FontWeights.SemiBold),
                    PulseParts.Body(desc, 11.5, PulseParts.TextSoft)
                }
            },
            right,
            active: active,
            compact: true);
    }
}
