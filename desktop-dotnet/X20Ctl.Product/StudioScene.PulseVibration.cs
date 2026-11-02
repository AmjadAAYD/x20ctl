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
    // 7-vibration: Vibration Control (matches t_pulse.py:631 vibration() and vibration.png)
    // Left (cx=600, cy=400): Floating controller with grip ripples emitting from both sides
    // Right (x=1000): OVERALL STRENGTH, huge 132px readout, vibration wave, preset chips
    // Bottom (x=262, y=640, w=1300): Rising pearl card with motor sliders & region preview
    // ========================================================================
    private void BuildPulseVibration(Canvas host)
    {
        Vibration ??= new VibrationWorkbench(vibrationDraft, this, RegisterFocus, animate, Model.Id);
        int strength = vibrationDraft.Strength;
        string region = Vibration.PreviewRegion ?? "All motors";

        // 1. Frame Title & Subtitle (x=270, y=30)
        var titleBlock = PulseParts.Disp("Vibration", 44, Colors.White, FontWeights.Light);
        titleBlock.Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.45 };
        Canvas.SetLeft(titleBlock, 270);
        Canvas.SetTop(titleBlock, 30);
        host.Children.Add(titleBlock);

        var subStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        subStack.Children.Add(PulseParts.Chip("Modified", "on"));
        subStack.Children.Add(PulseParts.Body("Rumble motors & trigger haptics", 14, PulseInk.C("#aab4c4")).Also(t => t.Margin = new(14, 0, 0, 0)));
        Canvas.SetLeft(subStack, 272);
        Canvas.SetTop(subStack, 98);
        host.Children.Add(subStack);

        // 2. Middle-Left: Floating Controller Stage with Grip Ripples (cx=600, cy=400, w=540)
        double cx = 600, cy = 400, cw = 540, ch = 340;

        // Grip ripple rings around left and right grips
        double leftGripX = cx - cw * 0.28;
        double rightGripX = cx + cw * 0.28;
        double gripY = cy + ch * 0.18;

        for (int rStep = 1; rStep <= 5; rStep++)
        {
            double r = 46 + rStep * 26;
            double op = 0.35 - rStep * 0.06;

            var lRing = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), op)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection([4, 6])
            };
            Canvas.SetLeft(lRing, leftGripX - r);
            Canvas.SetTop(lRing, gripY - r);
            host.Children.Add(lRing);

            var rRing = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), op)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection([4, 6])
            };
            Canvas.SetLeft(rRing, rightGripX - r);
            Canvas.SetTop(rRing, gripY - r);
            host.Children.Add(rRing);
        }

        // Controller artwork
        var finish = PadColours.For(Player, Model.Id);
        var ctlPedestal = PulseParts.ControllerPedestal(Model.Id, false, cw, ch, showRipples: false, finish: finish);
        Canvas.SetLeft(ctlPedestal, cx - cw / 2);
        Canvas.SetTop(ctlPedestal, cy - ch / 2);
        host.Children.Add(ctlPedestal);

        // 3. Right Side: OVERALL STRENGTH & Huge 132px Readout (x=1000, y=170)
        var rightStack = new StackPanel { Width = 540 };
        Canvas.SetLeft(rightStack, 1000);
        Canvas.SetTop(rightStack, 170);

        rightStack.Children.Add(PulseParts.Kicker("OVERALL STRENGTH"));

        var readoutText = new TextBlock
        {
            Text = $"{strength}%",
            FontFamily = PulseParts.DispFont,
            FontSize = 132,
            FontWeight = FontWeights.ExtraLight,
            Foreground = Brushes.White,
            LineHeight = 132,
            Margin = new(0, 4, 0, 0),
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.65 }
        };
        rightStack.Children.Add(readoutText);

        // Vibration Sine Wave Ribbon (w=540, h=60) at (x=1000, y=420)
        var waveCanvas = new Canvas { Width = 540, Height = 60, Margin = new(0, 16, 0, 16) };
        var wGeo = new StreamGeometry();
        using (var wCtx = wGeo.Open())
        {
            wCtx.BeginFigure(new Point(0, 30), false, false);
            for (int x = 0; x <= 540; x += 10)
            {
                double envelope = Math.Exp(-Math.Pow((x - 270) / 140.0, 2));
                double y = 30 - Math.Sin(x * 0.09) * (30 * (strength / 100.0) * envelope + 4);
                wCtx.LineTo(new Point(x, y), true, false);
            }
        }
        wGeo.Freeze();
        var waveStroke = new Path
        {
            Data = wGeo,
            Stroke = new LinearGradientBrush(PulseInk.C("#5cb3ff"), PulseInk.C("#a8ccff"), 0),
            StrokeThickness = 2.4,
            Effect = new DropShadowEffect { Color = PulseParts.CyanColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
        };
        waveCanvas.Children.Add(waveStroke);
        rightStack.Children.Add(waveCanvas);

        // Preset chips: Off, Soft 30%, Medium 60%, Strong 80%, Max 100%
        var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 14) };
        foreach (var (pName, pVal) in new[] { ("Off", 0), ("Soft 30%", 30), ("Medium 60%", 60), ("Strong 80%", 80), ("Max 100%", 100) })
        {
            bool isPresetActive = Math.Abs(strength - pVal) < 5;
            int vCopy = pVal;
            presetRow.Children.Add(PulseParts.ChipButton(pName, isPresetActive, () =>
            {
                Vibration.Set(vCopy);
                PulseLater();
            }).Also(b => b.Margin = new(0, 0, 8, 0)));
        }
        rightStack.Children.Add(presetRow);

        rightStack.Children.Add(PulseParts.Body("Real-time preview pulses as motors rumble", 13.5, PulseInk.C("#7f8aa0")));
        host.Children.Add(rightStack);

        // 4. Rising Pearl Card at Bottom (x=262, y=640, w=1300)
        var pearlGrid = new Grid { Width = 1220 };
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(600) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        pearlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left of pearl: Motor Sliders
        var motorStack = new StackPanel();

        // Left motor
        var lSlider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = 0,
            Maximum = 100,
            Value = strength,
            Width = 240,
            VerticalAlignment = VerticalAlignment.Center
        };
        lSlider.ValueChanged += (_, e) => { Vibration.Set((int)Math.Round(e.NewValue)); PulseLater(); };
        var lCtrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        lCtrl.Children.Add(lSlider);
        lCtrl.Children.Add(PulseParts.Mono($"{strength}%", 14, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(14, 0, 0, 0)));

        motorStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Left grip motor", 15, PulseParts.PearlInk, FontWeights.SemiBold),
            lCtrl,
            active: true,
            compact: true));

        // Right motor
        var rSlider = new Slider
        {
            Style = DS.Style("DS.Slider"),
            Minimum = 0,
            Maximum = 100,
            Value = strength,
            Width = 240,
            VerticalAlignment = VerticalAlignment.Center
        };
        rSlider.ValueChanged += (_, e) => { Vibration.Set((int)Math.Round(e.NewValue)); PulseLater(); };
        var rCtrl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rCtrl.Children.Add(rSlider);
        rCtrl.Children.Add(PulseParts.Mono($"{strength}%", 14, PulseParts.PearlMid).Also(t => t.Margin = new(14, 0, 0, 0)));

        motorStack.Children.Add(PulseParts.GlassRow(
            PulseParts.Disp("Right grip motor", 15, PulseParts.PearlInk, FontWeights.SemiBold),
            rCtrl,
            active: false,
            compact: true).Also(r => r.Margin = new(0, 8, 0, 0)));

        Grid.SetColumn(motorStack, 0);
        pearlGrid.Children.Add(motorStack);

        // Right of pearl: Region Selection & Note
        var regionStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        regionStack.Children.Add(PulseParts.Kicker("MOTOR REGIONS", onPearl: true));

        var regChips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 12) };
        foreach (string rName in new[] { "All motors", "Left grip", "Right grip" })
        {
            bool regActive = region == rName;
            string rCopy = rName;
            regChips.Children.Add(PulseParts.ChipButton(rName, regActive, () =>
            {
                Vibration.SelectRegion(rCopy);
                PulseLater();
            }, onPearl: true).Also(b => b.Margin = new(0, 0, 8, 0)));
        }
        regionStack.Children.Add(regChips);

        regionStack.Children.Add(PulseParts.Body("Region selection preview runs haptics only · Backend has one linked master strength.", 13, PulseParts.PearlSoft));
        Grid.SetColumn(regionStack, 2);
        pearlGrid.Children.Add(regionStack);

        var pearlCard = PulseParts.RisingPearlCard(pearlGrid, padding: new Thickness(32, 24, 32, 24), topRadius: 28, bottomRadius: 24);
        pearlCard.Width = 1300;
        Canvas.SetLeft(pearlCard, 262);
        Canvas.SetTop(pearlCard, 640);
        host.Children.Add(pearlCard);
    }
}
