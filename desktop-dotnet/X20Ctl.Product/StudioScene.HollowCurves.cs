using System.Windows.Controls.Primitives;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HollowShowCurves()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Curves");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var channelDefs = new (string Name, CurveChannel Ch)[]
        {
            ("LEFT STICK", CurveChannel.LeftStick),
            ("RIGHT STICK", CurveChannel.RightStick),
            ("LEFT TRIGGER", CurveChannel.LeftTrigger),
            ("RIGHT TRIGGER", CurveChannel.RightTrigger)
        };

        var curChannel = Curves.Channel;
        var curSetting = Curves.Draft[curChannel];

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        // Title Block
        var titleSp = HollowKit.HollowHeader("ANALOG RESPONSE CURVES", "Calibrate stick tension, dead zones, and trigger response.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // Channel Selection Tabs
        var channelPanel = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < channelDefs.Length; i++)
        {
            var def = channelDefs[i];
            bool isSel = (def.Ch == curChannel);

            var chTab = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                Background = isSel ? HollowKit.RaisedBrush : Brushes.Transparent,
                BorderBrush = isSel ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:stick:{def.Name.ToLowerInvariant().Replace(' ', '-')}"
            };
            var tabSp = new StackPanel { Orientation = Orientation.Horizontal };
            if (isSel)
            {
                var tabDiamond = new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                tabSp.Children.Add(tabDiamond);
            }
            tabSp.Children.Add(new TextBlock
            {
                Text = def.Name,
                FontFamily = HollowKit.Display,
                FontSize = 12,
                Foreground = isSel ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            chTab.Child = tabSp;

            chTab.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Curves.SelectChannel(def.Ch);
                HollowShowCurves();
            };
            channelPanel.Children.Add(chTab);
        }
        Canvas.SetLeft(channelPanel, 16);
        Canvas.SetTop(channelPanel, 70);
        content.Children.Add(channelPanel);

        // ==========================================
        // LEFT PANEL: Delicate Ivory Oscilloscope Chart
        // ==========================================
        var chartCanvas = new Canvas
        {
            Width = 600,
            Height = 480,
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            ClipToBounds = true
        };

        // Grid lines (5x5)
        for (int step = 1; step <= 4; step++)
        {
            double gx = step * (600.0 / 5.0);
            double gy = step * (480.0 / 5.0);

            var vLine = new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = 480, Stroke = new SolidColorBrush(Color.FromArgb(0x28, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } };
            var hLine = new Line { X1 = 0, Y1 = gy, X2 = 600, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(0x28, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } };
            chartCanvas.Children.Add(vLine);
            chartCanvas.Children.Add(hLine);
        }

        // Deadzone Shading
        double dzWidth = 600 * (curSetting.Inner / 100.0);
        if (dzWidth > 0)
        {
            var dzRect = new Rectangle { Width = dzWidth, Height = 480, Fill = new SolidColorBrush(Color.FromArgb(0x25, 0xb8, 0xcc, 0xe0)) };
            chartCanvas.Children.Add(dzRect);
            var dzLine = new Line { X1 = dzWidth, Y1 = 0, X2 = dzWidth, Y2 = 480, Stroke = HollowKit.IvoryBorderBrush, StrokeThickness = 1 };
            chartCanvas.Children.Add(dzLine);
        }

        // Diagonal baseline
        var baseLine = new Line { X1 = 0, Y1 = 480, X2 = 600, Y2 = 0, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x8d, 0x97, 0xa5)), StrokeThickness = 1 };
        chartCanvas.Children.Add(baseLine);

        // Dynamic Curve Path (Bezier interpolation)
        double pMidX = (600 + dzWidth) / 2.0;
        double pMidY = curSetting.Preset switch
        {
            "quick" => 360,
            "slow" => 160,
            "smooth" => 200,
            "fine" => 140,
            _ => 240
        };

        var pathGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(dzWidth, 480) };
        fig.Segments.Add(new BezierSegment(
            new Point(dzWidth + 80, 480 - (480 - pMidY) * 0.4),
            new Point(pMidX, pMidY),
            new Point(600, 20),
            true
        ));
        pathGeom.Figures.Add(fig);

        var curvePath = new Path
        {
            Data = pathGeom,
            Stroke = HollowKit.IvoryTextBrush,
            StrokeThickness = 2
        };
        chartCanvas.Children.Add(curvePath);

        // Control Handle Diamond
        var handleDiamond = new Path
        {
            Data = HollowKit.CreateDiamond(10),
            Fill = HollowKit.RaisedBrush,
            Stroke = HollowKit.IvoryTextBrush,
            StrokeThickness = 1
        };
        Canvas.SetLeft(handleDiamond, pMidX - 5);
        Canvas.SetTop(handleDiamond, pMidY - 5);
        chartCanvas.Children.Add(handleDiamond);

        var chartCard = HollowKit.FiligreeCard(chartCanvas, padding: 18, cornerSize: 14);
        chartCard.Width = 636;
        chartCard.Height = 580;
        Canvas.SetLeft(chartCard, 16);
        Canvas.SetTop(chartCard, 115);
        content.Children.Add(chartCard);

        // ==========================================
        // RIGHT PANEL: Response Presets & Parameters
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var presetsHeader = new TextBlock
        {
            Text = "RESPONSE PRESETS",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(presetsHeader);

        // Presets Grid
        var presetsGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 16) };
        var presets = new (string Name, string P)[]
        {
            ("Linear (Standard)", "default"),
            ("Aggressive (Snap)", "quick"),
            ("Smooth (Precision)", "smooth"),
            ("Fine (Focus)", "fine"),
            ("Exponential (Slow)", "slow")
        };

        foreach (var pr in presets)
        {
            bool isCurPreset = string.Equals(curSetting.Preset, pr.P, StringComparison.OrdinalIgnoreCase);
            var pBtn = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Background = isCurPreset ? HollowKit.RaisedBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0x09, 0x12, 0x1c)),
                BorderBrush = isCurPreset ? HollowKit.IvoryBorderBrush : new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = $"theme:preset:{pr.P}"
            };
            var pSp = new StackPanel { Orientation = Orientation.Horizontal };
            if (isCurPreset)
            {
                pSp.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            }
            pSp.Children.Add(new TextBlock { Text = pr.Name, FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = isCurPreset ? HollowKit.IvoryTextBrush : HollowKit.SoftTextBrush });
            pBtn.Child = pSp;

            string targetP = pr.P;
            pBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Curves.SetPreset(targetP);
                HollowShowCurves();
            };
            presetsGrid.Children.Add(pBtn);
        }
        rightSp.Children.Add(presetsGrid);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Calibration Sliders
        rightSp.Children.Add(new TextBlock { Text = $"INNER DEADZONE: {curSetting.Inner}%", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 10, 0, 4) });
        var innerSlider = new Slider { Minimum = 0, Maximum = 30, Value = curSetting.Inner, Width = 540, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12), Tag = "theme:stick:deadzone" };
        innerSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Inner = newVal });
            Curves.Refresh();
        };
        rightSp.Children.Add(innerSlider);

        rightSp.Children.Add(new TextBlock { Text = $"OUTER DEADZONE: {curSetting.Outer}%", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 4, 0, 4) });
        var outerSlider = new Slider { Minimum = 70, Maximum = 100, Value = curSetting.Outer, Width = 540, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16), Tag = "theme:stick:outer" };
        outerSlider.ValueChanged += (_, e) =>
        {
            int newVal = (int)Math.Round(e.NewValue);
            Curves.Draft.Set(curChannel, Curves.Draft[curChannel] with { Outer = newVal });
            Curves.Refresh();
        };
        rightSp.Children.Add(outerSlider);

        // Action Buttons Row: Reset channel, Apply to both sticks
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 14) };
        var resetBtn = HollowKit.HollowButton("Reset channel", () =>
        {
            Curves.Reset();
            HollowShowCurves();
        }, isPrimary: false, width: 140, height: 34, tag: "theme:curves:reset");
        resetBtn.Margin = new Thickness(0, 0, 10, 0);
        actRow.Children.Add(resetBtn);

        var copyBtn = HollowKit.HollowButton("Apply to both sticks", () =>
        {
            var curS = Curves.Draft[curChannel];
            Curves.Draft.Set(CurveChannel.LeftStick, curS);
            Curves.Draft.Set(CurveChannel.RightStick, curS);
            Curves.Refresh();
            HollowShowCurves();
        }, isPrimary: false, width: 180, height: 34, tag: "theme:curves:copy");
        actRow.Children.Add(copyBtn);
        rightSp.Children.Add(actRow);

        if (ControllerWrites)
        {
            var applyCtrlBtn = HollowKit.HollowPrimaryApplyButton("Apply to controller", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; HollowLater(); });
                HollowShowCurves();
            }, width: double.NaN, height: 42, tag: "theme:curves:apply");
            applyCtrlBtn.Margin = new Thickness(0, 4, 0, 0);
            rightSp.Children.Add(applyCtrlBtn);
        }

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 115);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Curves");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}
