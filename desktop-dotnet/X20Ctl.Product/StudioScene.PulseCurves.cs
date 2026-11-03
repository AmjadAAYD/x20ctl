using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    // ========================================================================
    // 5-curves: Response Curves (matches t_pulse.py:476 curves() and curves.png)
    // sx=560, sy=452: Concentric rings around pearl stick cap
    // Right side (x=920): Heading, 4 glass slider rows, rising pearl curve card
    // ========================================================================
    private void BuildPulseCurves(Canvas host)
    {
        var ch = Curves.Channel;
        var currentCurve = Curves.Draft[ch];
        double innerVal = Curves.Inner.Value;
        double outerVal = Curves.Outer.Value;
        double antiDeadVal = 0.0;
        double maxOutputVal = 100.0;

        string chTitle = ch switch
        {
            CurveChannel.LeftStick => "Left stick",
            CurveChannel.RightStick => "Right stick",
            CurveChannel.LeftTrigger => "Left trigger · LT",
            _ => "Right trigger · RT"
        };

        // 1. Frame Title (x=270, y=30)
        var titleBlock = PulseParts.Disp("Curves", 44, Colors.White, FontWeights.Light);
        titleBlock.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(titleBlock, 270);
        Canvas.SetTop(titleBlock, 30);
        host.Children.Add(titleBlock);

        // 2. Local draft tag + subtitle (x=272, y=98)
        var draftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        draftStack.Children.Add(PulseParts.Chip("Local draft", "on"));
        draftStack.Children.Add(PulseParts.Body("Not written to controller memory.", 14, PulseInk.C("#aab4c4")).Also(t => t.Margin = new(14, 0, 0, 0)));
        Canvas.SetLeft(draftStack, 272);
        Canvas.SetTop(draftStack, 98);
        host.Children.Add(draftStack);

        // 3. Tabs (x=272, y=148): Left stick, Right stick, Left trigger · LT, Right trigger · RT
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        var tabItems = new (CurveChannel Chan, string Label)[]
        {
            (CurveChannel.LeftStick, "Left stick"),
            (CurveChannel.RightStick, "Right stick"),
            (CurveChannel.LeftTrigger, "Left trigger · LT"),
            (CurveChannel.RightTrigger, "Right trigger · RT")
        };
        foreach (var (chan, label) in tabItems)
        {
            var cCopy = chan;
            bool active = ch == chan;
            tabsStack.Children.Add(PulseParts.TabButton(label, active, () =>
            {
                Curves.SelectChannel(cCopy);
                PulseLater();
            }));
        }
        Canvas.SetLeft(tabsStack, 272);
        Canvas.SetTop(tabsStack, 148);
        host.Children.Add(tabsStack);

        // 4. Concentric Rings & Pearl Stick Cap (Center: sx=560, sy=452)
        double sx = 560, sy = 452;
        var ringsCanvas = new Canvas { Width = 560, Height = 560, ClipToBounds = false };
        Canvas.SetLeft(ringsCanvas, sx - 280);
        Canvas.SetTop(ringsCanvas, sy - 280);
        double rcx = 280, rcy = 280;

        // Concentric fields: (Label, PctValue, Radius)
        var fields = new (string Name, double Value, double Radius)[]
        {
            ("Inner dead zone", innerVal, 108),
            ("Anti-dead zone", antiDeadVal, 140),
            ("Full output at", outerVal, 172),
            ("Max output", maxOutputVal, 204)
        };

        for (int i = 0; i < fields.Length; i++)
        {
            var (name, val, r) = fields[i];
            bool isFocused = i == 0;

            // Track circle
            var track = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), isFocused ? 0.25 : 0.14)),
                StrokeThickness = isFocused ? 9 : 6
            };
            Canvas.SetLeft(track, rcx - r);
            Canvas.SetTop(track, rcy - r);
            ringsCanvas.Children.Add(track);

            // Active arc sweep (sweep starts at 135 deg per t_pulse.py)
            double sweep = Math.Clamp(val / 100.0 * 270.0, 4.0, 270.0);
            var arcPath = PulseParts.CreateArcPath(rcx, rcy, r, 135, 135 + sweep, isFocused ? Colors.White : PulseInk.C("#c8d7f0"), isFocused ? 9 : 6);
            if (isFocused)
            {
                arcPath.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 };
            }
            ringsCanvas.Children.Add(arcPath);

            // Callout line from angle 135 deg: (x0, y0) -> (rcx - 200, ly) -> (rcx - 214, ly)
            double rad135 = 135.0 * Math.PI / 180.0;
            double x0 = rcx + r * Math.Cos(rad135);
            double y0 = rcy + r * Math.Sin(rad135);
            double ly = rcy + 118 + i * 30;

            var calloutGeo = new StreamGeometry();
            using (var ctx = calloutGeo.Open())
            {
                ctx.BeginFigure(new Point(x0, y0), false, false);
                ctx.LineTo(new Point(rcx - 200, ly), true, false);
                ctx.LineTo(new Point(rcx - 214, ly), true, false);
            }
            calloutGeo.Freeze();

            var calloutPath = new Path
            {
                Data = calloutGeo,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), isFocused ? 0.60 : 0.25)),
                StrokeThickness = 1.2
            };
            ringsCanvas.Children.Add(calloutPath);

            // Text callout (right-aligned to rcx - 222)
            var calloutText = new TextBlock
            {
                Text = $"{name} · {val:0}%",
                FontFamily = PulseParts.BodyFont,
                FontSize = 13,
                FontWeight = isFocused ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isFocused ? Brushes.White : PulseInk.B(PulseInk.C("#8a94a6")),
                TextAlignment = TextAlignment.Right,
                Width = 180
            };
            Canvas.SetLeft(calloutText, rcx - 222 - 180);
            Canvas.SetTop(calloutText, ly - 8);
            ringsCanvas.Children.Add(calloutText);
        }

        // Resonance mark around stick cap
        var resMark = PulseParts.ResonanceMark();
        Canvas.SetLeft(resMark, rcx - 7);
        Canvas.SetTop(resMark, rcy - 7);
        ringsCanvas.Children.Add(resMark);

        // Stick cap: 132x132 pearl circle
        var capBorder = new Border
        {
            Width = 132,
            Height = 132,
            CornerRadius = new(66),
            Background = new LinearGradientBrush(Colors.White, PulseInk.C("#dde5f1"), 135),
            BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.9)),
            BorderThickness = new(1.5),
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 30, ShadowDepth = 10, Opacity = 0.45 }
        };

        // Inner concentric grooves on cap
        var capCanvas = new Canvas { Width = 132, Height = 132 };
        for (double cr = 24; cr <= 54; cr += 10)
        {
            var groove = new Ellipse
            {
                Width = cr * 2,
                Height = cr * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.35)),
                StrokeThickness = 1.0
            };
            Canvas.SetLeft(groove, 66 - cr);
            Canvas.SetTop(groove, 66 - cr);
            capCanvas.Children.Add(groove);
        }
        capBorder.Child = capCanvas;
        Canvas.SetLeft(capBorder, rcx - 66);
        Canvas.SetTop(capBorder, rcy - 66);
        ringsCanvas.Children.Add(capBorder);

        // Label under cap: "Left stick"
        var capLabel = PulseParts.Mono(chTitle.ToUpperInvariant(), 12, PulseParts.CyanBright, FontWeights.Bold);
        capLabel.TextAlignment = TextAlignment.Center;
        capLabel.Width = 180;
        Canvas.SetLeft(capLabel, rcx - 90);
        Canvas.SetTop(capLabel, rcy + 228);
        ringsCanvas.Children.Add(capLabel);

        host.Children.Add(ringsCanvas);

        // 5. Right Side: Heading (x=930, y=214)
        var rightHead = PulseParts.Disp(chTitle, 26, Colors.White, FontWeights.Light);
        Canvas.SetLeft(rightHead, 930);
        Canvas.SetTop(rightHead, 214);
        host.Children.Add(rightHead);

        // 6. Right Side: 4 Slider Rows (x=920, y=268, w=630)
        var sliderStack = new StackPanel { Width = 630 };
        Canvas.SetLeft(sliderStack, 920);
        Canvas.SetTop(sliderStack, 268);

        // Row 1: Inner dead zone
        var r1Slider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = Curves.Inner.Minimum,
            Maximum = Curves.Inner.Maximum,
            Value = innerVal,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center
        };
        r1Slider.ValueChanged += (_, e) => { Curves.Inner.Value = Math.Round(e.NewValue); PulseLater(); };
        var r1Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        r1Ctrl.Children.Add(r1Slider);
        r1Ctrl.Children.Add(PulseParts.Mono($"{innerVal:0}%", 14, Colors.White, FontWeights.SemiBold).Also(t => t.Margin = new(14, 0, 0, 0)));
        sliderStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Inner dead zone", 15, Colors.White, FontWeights.SemiBold),
            r1Ctrl,
            active: true,
            compact: true));

        // Row 2: Anti-dead zone
        var r2Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var r2Slider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = 0,
            Maximum = 30,
            Value = antiDeadVal,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center
        };
        r2Ctrl.Children.Add(r2Slider);
        r2Ctrl.Children.Add(PulseParts.Mono($"{antiDeadVal:0}%", 14, PulseInk.C("#8a94a6")).Also(t => t.Margin = new(14, 0, 0, 0)));
        sliderStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Anti-dead zone", 15, PulseInk.C("#dfe5ee")),
            r2Ctrl,
            active: false,
            compact: true));

        // Row 3: Full output at
        var r3Slider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = Curves.Outer.Minimum,
            Maximum = Curves.Outer.Maximum,
            Value = outerVal,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center
        };
        r3Slider.ValueChanged += (_, e) => { Curves.Outer.Value = Math.Round(e.NewValue); PulseLater(); };
        var r3Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        r3Ctrl.Children.Add(r3Slider);
        r3Ctrl.Children.Add(PulseParts.Mono($"{outerVal:0}%", 14, PulseInk.C("#dfe5ee")).Also(t => t.Margin = new(14, 0, 0, 0)));
        sliderStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Full output at", 15, PulseInk.C("#dfe5ee")),
            r3Ctrl,
            active: false,
            compact: true));

        // Row 4: Max output
        var r4Ctrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var r4Slider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = 50,
            Maximum = 100,
            Value = maxOutputVal,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center
        };
        r4Ctrl.Children.Add(r4Slider);
        r4Ctrl.Children.Add(PulseParts.Mono($"{maxOutputVal:0}%", 14, PulseInk.C("#dfe5ee")).Also(t => t.Margin = new(14, 0, 0, 0)));
        sliderStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Max output", 15, PulseInk.C("#dfe5ee")),
            r4Ctrl,
            active: false,
            compact: true));

        host.Children.Add(sliderStack);

        // 7. Bottom-Right: Rising Pearl Card with Curve Plot (x=920, y=556, w=640)
        double plotW = 520, plotH = 150;
        var plotCanvas = new Canvas { Width = plotW, Height = plotH + 30, ClipToBounds = false };

        // Grid axes: V 150 H 520
        var axisGeo = new StreamGeometry();
        using (var ctx = axisGeo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), false, false);
            ctx.LineTo(new Point(0, plotH), true, false);
            ctx.LineTo(new Point(plotW, plotH), true, false);
        }
        axisGeo.Freeze();
        plotCanvas.Children.Add(new Path { Data = axisGeo, Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.25)), StrokeThickness = 1 });

        // Build curve bezier path
        var curveGeo = new StreamGeometry();
        var fillGeo = new StreamGeometry();
        int steps = 50;
        var curvePoints = new Point[steps + 1];
        for (int s = 0; s <= steps; s++)
        {
            double inPct = (double)s / steps;
            double outVal = Curves.Output(inPct);
            curvePoints[s] = new Point(inPct * plotW, (1.0 - outVal) * plotH);
        }

        using (var ctx = curveGeo.Open())
        {
            ctx.BeginFigure(curvePoints[0], false, false);
            for (int s = 1; s <= steps; s++) ctx.LineTo(curvePoints[s], true, false);
        }
        curveGeo.Freeze();

        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(new Point(0, plotH), true, true);
            for (int s = 0; s <= steps; s++) ctx.LineTo(curvePoints[s], true, false);
            ctx.LineTo(new Point(plotW, plotH), true, false);
        }
        fillGeo.Freeze();

        // Area fill
        plotCanvas.Children.Add(new Path { Data = fillGeo, Fill = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.08)) });

        // Curve stroke
        var strokePath = new Path
        {
            Data = curveGeo,
            Stroke = new LinearGradientBrush(PulseInk.C("#5cb3ff"), PulseInk.C("#a8ccff"), 0),
            StrokeThickness = 3,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 }
        };
        plotCanvas.Children.Add(strokePath);

        // Control point dot at in=0.55
        double sampleIn = 0.55;
        double sampleOut = Curves.Output(sampleIn);
        double px = sampleIn * plotW;
        double py = (1.0 - sampleOut) * plotH;

        var dot = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = Brushes.White,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(dot, px - 7);
        Canvas.SetTop(dot, py - 7);
        plotCanvas.Children.Add(dot);

        // Axis labels
        var inLabel = new TextBlock
        {
            Text = "Input %",
            FontFamily = PulseParts.BodyFont,
            FontSize = 12.5,
            Foreground = PulseInk.B(PulseInk.C("#8a94a6"))
        };
        Canvas.SetLeft(inLabel, 0);
        Canvas.SetTop(inLabel, plotH + 8);
        plotCanvas.Children.Add(inLabel);

        var outLabel = new TextBlock
        {
            Text = "Output % ↑",
            FontFamily = PulseParts.BodyFont,
            FontSize = 12.5,
            Foreground = PulseInk.B(PulseInk.C("#8a94a6")),
            TextAlignment = TextAlignment.Right,
            Width = 100
        };
        Canvas.SetLeft(outLabel, plotW - 100);
        Canvas.SetTop(outLabel, plotH + 8);
        plotCanvas.Children.Add(outLabel);

        // Action row: Keyboard test button + note
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 24, 0, 0) };
        actionRow.Children.Add(PulseParts.PillButton("Keyboard test", primary: false, onPearl: true, onClick: () => OpenStickTools()));
        actionRow.Children.Add(PulseParts.Body("Connect a controller or use Keyboard test", 14, PulseParts.PearlSoft).Also(t => t.Margin = new(18, 0, 0, 0)));

        var pearlContent = new StackPanel { Margin = new(10, 6, 10, 6) };
        pearlContent.Children.Add(plotCanvas);
        pearlContent.Children.Add(actionRow);

        var pearlCard = PulseParts.RisingPearlCard(pearlContent, padding: new Thickness(34, 30, 34, 30), topRadius: 26, bottomRadius: 26);
        pearlCard.Width = 640;
        Canvas.SetLeft(pearlCard, 920);
        Canvas.SetTop(pearlCard, 556);
        host.Children.Add(pearlCard);
    }
}
