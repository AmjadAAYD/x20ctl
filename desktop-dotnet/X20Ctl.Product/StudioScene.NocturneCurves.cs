using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowCurves()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Curves");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        var curChannel = Curves.Channel;
        var curSetting = Curves.Draft[curChannel];

        // Title Block
        var title = NocturneKit.NocturneTitle("RESPONSE", "CURVES", "Hardware hall sensor mapping with sub-millisecond dynamic contour interpolation.");
        Canvas.SetLeft(title, 300);
        Canvas.SetTop(title, 70);
        board.Children.Add(title);

        // Channel Selector Ribbon (Skewed Chips)
        var channelPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var channelDefs = new (string Name, CurveChannel Ch)[]
        {
            ("Left Stick", CurveChannel.LeftStick),
            ("Right Stick", CurveChannel.RightStick),
            ("Left Trigger", CurveChannel.LeftTrigger),
            ("Right Trigger", CurveChannel.RightTrigger)
        };

        foreach (var def in channelDefs)
        {
            bool isSel = (def.Ch == curChannel);
            var chTab = NocturneKit.NocturneChip(def.Name, isSel, () =>
            {
                Curves.SelectChannel(def.Ch);
                NocturneShowCurves();
            }, tag: $"theme:stick:{def.Name.ToLowerInvariant().Replace(' ', '-')}");
            channelPanel.Children.Add(chTab);
        }
        Canvas.SetLeft(channelPanel, 300);
        Canvas.SetTop(channelPanel, 205);
        board.Children.Add(channelPanel);

        // ==========================================
        // LEFT STAGE: Oscilloscope on Slanted White Card
        // ==========================================
        var chartCanvas = new Canvas
        {
            Width = 560,
            Height = 440,
            Background = new SolidColorBrush(Color.FromArgb(0x10, 0x01, 0x00, 0x42)),
            ClipToBounds = true
        };

        // Grid lines
        for (int step = 1; step <= 4; step++)
        {
            double gx = step * (560.0 / 5.0);
            double gy = step * (440.0 / 5.0);

            var vLine = new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = 440, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 4 } };
            var hLine = new Line { X1 = 0, Y1 = gy, X2 = 560, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 4 } };
            chartCanvas.Children.Add(vLine);
            chartCanvas.Children.Add(hLine);
        }

        // Deadzone Shading
        double dzWidth = 560 * (curSetting.Inner / 100.0);
        if (dzWidth > 0)
        {
            var dzRect = new Rectangle { Width = dzWidth, Height = 440, Fill = new SolidColorBrush(Color.FromArgb(0x25, 0xd1, 0x00, 0x2f)) };
            chartCanvas.Children.Add(dzRect);
            var dzLine = new Line { X1 = dzWidth, Y1 = 0, X2 = dzWidth, Y2 = 440, Stroke = NocturneKit.DangerBrush, StrokeThickness = 1.5 };
            chartCanvas.Children.Add(dzLine);
        }

        // Diagonal baseline
        var baseLine = new Line { X1 = 0, Y1 = 440, X2 = 560, Y2 = 0, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x01, 0x00, 0x42)), StrokeThickness = 1 };
        chartCanvas.Children.Add(baseLine);

        // Response curve: cobalt line & square handles
        double pMidX = (560 + dzWidth) / 2.0;
        double pMidY = curSetting.Preset switch
        {
            "quick" => 330,
            "slow" => 140,
            "smooth" => 180,
            "fine" => 120,
            _ => 220
        };

        var pathGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(dzWidth, 440) };
        fig.Segments.Add(new BezierSegment(
            new Point(dzWidth + 70, 440 - (440 - pMidY) * 0.4),
            new Point(pMidX, pMidY),
            new Point(560, 20),
            true
        ));
        pathGeom.Figures.Add(fig);

        var curvePath = new Path
        {
            Data = pathGeom,
            Stroke = NocturneKit.CobaltBrush,
            StrokeThickness = 3.5
        };
        chartCanvas.Children.Add(curvePath);

        // Square handles on curve
        var handleSquare = new Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = NocturneKit.ActiveBlueBrush,
            Stroke = NocturneKit.WhiteBrush,
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(handleSquare, pMidX - 5);
        Canvas.SetTop(handleSquare, pMidY - 5);
        chartCanvas.Children.Add(handleSquare);

        var leftCardStack = new StackPanel();
        leftCardStack.Children.Add(new TextBlock
        {
            Text = $"{curChannel.ToString().ToUpperInvariant()} RESPONSE",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
        leftCardStack.Children.Add(chartCanvas);

        var leftWhiteCard = NocturneKit.NocturneWhiteCard(leftCardStack, width: 610, height: 570);
        Canvas.SetLeft(leftWhiteCard, 280);
        Canvas.SetTop(leftWhiteCard, 260);
        board.Children.Add(leftWhiteCard);

        // ==========================================
        // RIGHT STAGE: Tuning Deck on Slanted White Card
        // ==========================================
        var rightStack = new StackPanel { Width = 510 };

        var rTitle = new TextBlock
        {
            Text = "RESPONSE PRESETS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightStack.Children.Add(rTitle);

        // Presets Grid
        var presetsGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 16) };
        var presets = new (string Name, string P)[]
        {
            ("Linear (1:1)", "default"),
            ("Quick (Snap)", "quick"),
            ("Smooth (Soft)", "smooth"),
            ("Fine (Precision)", "fine"),
            ("Slow (Exponential)", "slow")
        };

        foreach (var pr in presets)
        {
            bool isCurPreset = string.Equals(curSetting.Preset, pr.P, StringComparison.OrdinalIgnoreCase);
            var pBtn = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(3),
                Background = isCurPreset ? NocturneKit.ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0x18, 0x01, 0x00, 0x42)),
                BorderBrush = isCurPreset ? NocturneKit.CobaltBrush : new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:preset:{pr.P}"
            };
            pBtn.Child = new TextBlock
            {
                Text = pr.Name.ToUpperInvariant(),
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = isCurPreset ? NocturneKit.WhiteBrush : NocturneKit.InkBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            string targetP = pr.P;
            pBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Curves.SetPreset(targetP);
                NocturneShowCurves();
            };
            presetsGrid.Children.Add(pBtn);
        }
        rightStack.Children.Add(presetsGrid);

        // Thin navy rule
        rightStack.Children.Add(new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x01, 0x00, 0x42)), Margin = new Thickness(0, 0, 0, 14) });

        // Inner Deadzone Slider
        rightStack.Children.Add(new TextBlock { Text = $"INNER DEADZONE: {curSetting.Inner}%", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush, Margin = new Thickness(0, 4, 0, 4) });
        var innerSlider = new Slider { Minimum = 0, Maximum = 30, Value = curSetting.Inner, Width = 480, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12), Tag = "theme:stick:deadzone" };
        innerSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Inner = newVal });
            Curves.Refresh();
        };
        rightStack.Children.Add(innerSlider);

        // Outer Deadzone Slider
        rightStack.Children.Add(new TextBlock { Text = $"OUTER DEADZONE: {curSetting.Outer}%", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = NocturneKit.InkBrush, Margin = new Thickness(0, 4, 0, 4) });
        var outerSlider = new Slider { Minimum = 70, Maximum = 100, Value = curSetting.Outer, Width = 480, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16), Tag = "theme:stick:outer" };
        outerSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Outer = newVal });
            Curves.Refresh();
        };
        rightStack.Children.Add(outerSlider);

        // Action Buttons Row: Reset channel, Apply to both sticks
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 14) };
        var resetBtn = NocturneKit.NocturneButton("Reset Channel", () =>
        {
            Curves.Reset();
            NocturneShowCurves();
        }, isPrimary: false, width: 160, height: 34, tag: "theme:curves:reset");
        resetBtn.Margin = new Thickness(0, 0, 10, 0);
        actRow.Children.Add(resetBtn);

        var copyBtn = NocturneKit.NocturneButton("Apply Both Sticks", () =>
        {
            var curS = Curves.Draft[curChannel];
            Curves.Draft.Set(CurveChannel.LeftStick, curS);
            Curves.Draft.Set(CurveChannel.RightStick, curS);
            Curves.Refresh();
            NocturneShowCurves();
        }, isPrimary: false, width: 180, height: 34, tag: "theme:curves:copy");
        actRow.Children.Add(copyBtn);
        rightStack.Children.Add(actRow);

        if (ControllerWrites)
        {
            var applyBtn = NocturneKit.NocturneButton("APPLY TO CONTROLLER", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; NocturneLater(); });
                NocturneShowCurves();
            }, isPrimary: true, width: null, height: 42, tag: "theme:curves:apply");
            applyBtn.Margin = new Thickness(0, 4, 0, 0);
            rightStack.Children.Add(applyBtn);
        }

        var rightWhiteCard = NocturneKit.NocturneWhiteCard(rightStack, width: 570, height: 570);
        Canvas.SetLeft(rightWhiteCard, 970);
        Canvas.SetTop(rightWhiteCard, 260);
        board.Children.Add(rightWhiteCard);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}
