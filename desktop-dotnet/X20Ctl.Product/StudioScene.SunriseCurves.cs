using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowCurves()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Curves");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("RESPONSE CURVES", "Hall-effect analog curve shaping, deadzones, and dynamic acceleration.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        var curChannel = Curves.Channel;

        // Channel selector pills
        var chanSp = new StackPanel { Orientation = Orientation.Horizontal };
        var channels = new (string Label, CurveChannel Ch)[]
        {
            ("LEFT STICK", CurveChannel.LeftStick),
            ("RIGHT STICK", CurveChannel.RightStick),
            ("LEFT TRIGGER", CurveChannel.LeftTrigger),
            ("RIGHT TRIGGER", CurveChannel.RightTrigger)
        };

        foreach (var ch in channels)
        {
            bool isCur = (ch.Ch == curChannel);
            var chanBtn = SunriseKit.SunriseChip(ch.Label, isCur, () =>
            {
                Curves.SelectChannel(ch.Ch);
                SunriseShowCurves();
            }, tag: $"theme:curve:chan:{ch.Label.ToLowerInvariant().Replace(' ', '_')}");
            chanSp.Children.Add(chanBtn);
        }
        Canvas.SetLeft(chanSp, 10);
        Canvas.SetTop(chanSp, 75);
        content.Children.Add(chanSp);

        // ==========================================
        // LEFT STICKER CARD: Presets & Deadzone Sliders
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var presetsHeader = new TextBlock
        {
            Text = "RESPONSE PRESETS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(presetsHeader);

        var presetsGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 0, 0, 16) };
        var presets = new (string Name, string Desc, string Key)[]
        {
            ("LINEAR (1:1)", "Direct 1:1 hardware translation", "linear"),
            ("AGGRESSIVE", "Fast initial response for rapid aim", "aggressive"),
            ("SMOOTH / EXPO", "Soft center for micro-adjustments", "smooth"),
            ("INSTANT SNAP", "Immediate threshold engagement", "instant")
        };

        foreach (var p in presets)
        {
            var pBorder = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                Tag = $"theme:preset:{p.Key}"
            };
            var pSp = new StackPanel();
            pSp.Children.Add(new TextBlock { Text = p.Name, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.InkBrush });
            pSp.Children.Add(new TextBlock { Text = p.Desc, FontFamily = SunriseKit.Body, FontSize = 10.5, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 2, 0, 0) });
            pBorder.Child = pSp;

            string key = p.Key;
            pBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Curves.SetPreset(key);
                SunriseShowCurves();
            };
            presetsGrid.Children.Add(pBorder);
        }
        leftSp.Children.Add(presetsGrid);

        // Deadzone adjustments
        var dzHeader = new TextBlock
        {
            Text = "DEADZONE THRESHOLDS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(dzHeader);

        var curCurve = Curves.Draft[curChannel];

        // Inner Deadzone Slider
        var innerRow = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var innerHeader = new Grid();
        innerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        innerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        innerHeader.Children.Add(new TextBlock { Text = "INNER DEAD ZONE", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = SunriseKit.InkBrush });
        var innerValTb = new TextBlock { Text = $"{curCurve.Inner}%", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = SunriseKit.OrangeBrush };
        Grid.SetColumn(innerValTb, 1);
        innerHeader.Children.Add(innerValTb);
        innerRow.Children.Add(innerHeader);

        var innerSlider = new Slider { Minimum = 0, Maximum = 40, Value = curCurve.Inner, Width = 540, HorizontalAlignment = HorizontalAlignment.Left, Tag = "theme:curve:Inner dead zone" };
        innerSlider.ValueChanged += (_, e) =>
        {
            int val = (int)e.NewValue;
            innerValTb.Text = $"{val}%";
            Curves.Draft.Set(curChannel, curCurve with { Inner = val });
            Curves.Refresh();
        };
        innerRow.Children.Add(innerSlider);
        leftSp.Children.Add(innerRow);

        // Outer Deadzone Slider
        var outerRow = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var outerHeader = new Grid();
        outerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        outerHeader.Children.Add(new TextBlock { Text = "OUTER DEAD ZONE", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = SunriseKit.InkBrush });
        var outerValTb = new TextBlock { Text = $"{curCurve.Outer}%", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = SunriseKit.OrangeBrush };
        Grid.SetColumn(outerValTb, 1);
        outerHeader.Children.Add(outerValTb);
        outerRow.Children.Add(outerHeader);

        var outerSlider = new Slider { Minimum = 60, Maximum = 100, Value = curCurve.Outer, Width = 540, HorizontalAlignment = HorizontalAlignment.Left, Tag = "theme:curve:Outer dead zone" };
        outerSlider.ValueChanged += (_, e) =>
        {
            int val = (int)e.NewValue;
            outerValTb.Text = $"{val}%";
            Curves.Draft.Set(curChannel, curCurve with { Outer = val });
            Curves.Refresh();
        };
        outerRow.Children.Add(outerSlider);
        leftSp.Children.Add(outerRow);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 130);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Curve Oscilloscope
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var scopeTitle = new TextBlock
        {
            Text = "RESPONSE CURVE OSCILLOSCOPE",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(scopeTitle);

        var scopeCanvas = new Canvas
        {
            Width = 560,
            Height = 340,
            Background = SunriseKit.CreamBrush,
            ClipToBounds = true
        };

        // Grid lines
        for (int i = 0; i <= 4; i++)
        {
            double pos = i * (340.0 / 4.0);
            var hLine = new Line { X1 = 0, Y1 = pos, X2 = 560, Y2 = pos, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
            var vLine = new Line { X1 = i * (560.0 / 4.0), Y1 = 0, X2 = i * (560.0 / 4.0), Y2 = 340, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
            scopeCanvas.Children.Add(hLine);
            scopeCanvas.Children.Add(vLine);
        }

        // Curve in Black
        var curveGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(curCurve.Inner * 4.0, 340) };
        double endX = (100.0 - curCurve.Outer) * 5.6;
        fig.Segments.Add(new BezierSegment(
            new Point(endX * 0.35, 340 * 0.7),
            new Point(endX * 0.65, 340 * 0.2),
            new Point(endX, 0),
            true));
        curveGeom.Figures.Add(fig);

        var curvePath = new Path
        {
            Data = curveGeom,
            Stroke = SunriseKit.InkBrush,
            StrokeThickness = 4.0
        };
        scopeCanvas.Children.Add(curvePath);

        // Handles in Orange
        var handle1 = new Ellipse { Width = 14, Height = 14, Fill = SunriseKit.OrangeBrush, Stroke = SunriseKit.InkBrush, StrokeThickness = 2 };
        Canvas.SetLeft(handle1, curCurve.Inner * 4.0 - 7);
        Canvas.SetTop(handle1, 340 - 7);
        scopeCanvas.Children.Add(handle1);

        var handle2 = new Ellipse { Width = 14, Height = 14, Fill = SunriseKit.OrangeBrush, Stroke = SunriseKit.InkBrush, StrokeThickness = 2 };
        Canvas.SetLeft(handle2, endX - 7);
        Canvas.SetTop(handle2, -7);
        scopeCanvas.Children.Add(handle2);

        rightSp.Children.Add(scopeCanvas);

        // Actions
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var resetBtn = SunriseKit.SunriseButton("Reset Curve", () =>
        {
            Curves.Reset();
            SunriseShowCurves();
        }, isPrimary: false, width: 140, height: 38);
        resetBtn.Margin = new Thickness(0, 0, 10, 0);

        actRow.Children.Add(resetBtn);
        if (ControllerWrites)
        {
            var applyBtn = SunriseKit.SunriseButton("APPLY TO CONTROLLER", () =>
            {
                _ = ApplyToController(text => { ChangeCount.Text = text; SunriseLater(); });
                SunriseShowCurves();
            }, isPrimary: true, width: 200, height: 38, tag: "theme:apply:curves");
            actRow.Children.Add(applyBtn);
        }
        else
        {
            var note = new TextBlock
            {
                Text = "Curves edit in local draft · Apply to controller is active on supported devices.",
                FontFamily = SunriseKit.Body,
                FontSize = 11.5,
                Foreground = SunriseKit.OliveBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            rightSp.Children.Add(note);
        }
        rightSp.Children.Add(actRow);

        var tvCard = SunriseKit.CreateTvScreenCard(rightSp, 610, 520, withAntennae: true);
        Canvas.SetLeft(tvCard, 630);
        Canvas.SetTop(tvCard, 80);
        content.Children.Add(tvCard);

        board.Children.Add(content);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}
