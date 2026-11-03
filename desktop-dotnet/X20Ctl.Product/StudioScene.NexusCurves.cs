using System;
using System.Linq;
using System.Windows.Data;
using X20Ctl.Zone;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool nexusCurvesWired;
    private Action? nexusCurveRefresh;
    private void NexusShowCurves()
    {
        nexusPageHost!.Children.Clear();
        if (!nexusCurvesWired) { nexusCurvesWired = true; Curves.Changed += () => nexusCurveRefresh?.Invoke(); Curves.ChannelSelected += _ => NexusLater(); }

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Tabs
        var title = new TextBlock
        {
            Text = "Curves",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 140);
        nexusPageHost.Children.Add(title);

        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        string[] tabs = { "Left stick", "Right stick", "Left trigger · LT", "Right trigger · RT" };
        for (int i = 0; i < tabs.Length; i++)
        {
            string t = tabs[i];
            var channel = (CurveChannel)i;
            bool on = channel == Curves.Channel;
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = t,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(ThemeAction(tabBtn, "theme:curve:channel:" + channel, () => Curves.SelectChannel(channel)));
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 200);
        nexusPageHost.Children.Add(tabsStack);

        // 4. Left Graph Panel at (68, 264, w=860, h=580)
        var graphPanel = new Border
        {
            Width = 860, Height = 580,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        Canvas.SetLeft(graphPanel, 68);
        Canvas.SetTop(graphPanel, 264);
        nexusPageHost.Children.Add(graphPanel);

        // SVG Canvas: gw=760, gh=500 at (118, 300)
        double gw = 760, gh = 500;
        var graphCanvas = new Canvas { Width = gw, Height = gh, ClipToBounds = true };

        void RefreshGraph()
        {
            graphCanvas.Children.Clear();
        // Grid lines every 50px
        var gridBrush = NexusKit.B(NexusKit.A(Colors.White, 0.06));
        for (double gx = 0; gx <= gw; gx += 50)
        {
            var vl = new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = gh, Stroke = gridBrush, StrokeThickness = 1 };
            graphCanvas.Children.Add(vl);
        }
        for (double gy = 0; gy <= gh; gy += 50)
        {
            var hl = new Line { X1 = 0, Y1 = gy, X2 = gw, Y2 = gy, Stroke = gridBrush, StrokeThickness = 1 };
            graphCanvas.Children.Add(hl);
        }

        // Curve Path: dz=0.04, full=0.96, gamma=1.6
        Point PointOnCurve(double t)
        {
            double y = Math.Clamp(Curves.Output(t), 0, 1);
            return new Point(t * gw, gh - y * gh);
        }

        var curveGeom = new StreamGeometry();
        using (var ctx = curveGeom.Open())
        {
            Point start = PointOnCurve(0);
            ctx.BeginFigure(start, false, false);
            for (int s = 1; s <= 60; s++)
            {
                ctx.LineTo(PointOnCurve(s / 60.0), true, true);
            }
        }
        curveGeom.Freeze();

        var curvePath = new System.Windows.Shapes.Path
        {
            Data = curveGeom,
            Stroke = NexusKit.GreenBrush,
            StrokeThickness = 4
        };
        graphCanvas.Children.Add(curvePath);

        // Control Points at t = 0.2, 0.45, 0.7
        foreach (double t in new[] { 0.2, 0.45, 0.7 })
        {
            var pt = PointOnCurve(t);
            var dot = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = NexusKit.HouseBrush,
                Stroke = NexusKit.WhiteBrush,
                StrokeThickness = 3
            };
            Canvas.SetLeft(dot, pt.X - 9);
            Canvas.SetTop(dot, pt.Y - 9);
            graphCanvas.Children.Add(dot);
        }

        // Active point at t = 0.55
        var apt = PointOnCurve(0.55);
        var activeDot = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = NexusKit.GreenBrush
        };
        Canvas.SetLeft(activeDot, apt.X - 7);
        Canvas.SetTop(activeDot, apt.Y - 7);
        graphCanvas.Children.Add(activeDot);

        }
        nexusCurveRefresh = RefreshGraph; RefreshGraph();

        Canvas.SetLeft(graphCanvas, 118);
        Canvas.SetTop(graphCanvas, 300);
        nexusPageHost.Children.Add(graphCanvas);

        var axesLabel = new TextBlock
        {
            Text = "Input %   ·   Output % ↑",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(axesLabel, 118);
        Canvas.SetTop(axesLabel, 808);
        nexusPageHost.Children.Add(axesLabel);

        // 5. Right Faders Panel at (952, 264, w=580, h=580)
        var faderPanel = new Border
        {
            Width = 580, Height = 580,
            Padding = new Thickness(30, 28, 30, 28),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var fStack = new StackPanel();

        var fields = RebelCurveRows;
        for (int i = 0; i < fields.Length; i++)
        {
            var f = (Name: fields[i].label, Source: fields[i].slider);
            bool isFocused = (i == 0);

            var fieldBox = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 24),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent
            };
            if (isFocused)
            {
                fieldBox.BorderBrush = NexusKit.GreenBrush;
                fieldBox.BorderThickness = new Thickness(3);
            }

            var itemStack = new StackPanel();
            var textRow = new Grid();
            textRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            textRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = f.Name,
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush
            };
            Grid.SetColumn(nameText, 0);
            textRow.Children.Add(nameText);

            var valText = new TextBlock
            {

                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush
            };
            valText.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = f.Source, StringFormat = "{0:0}%" });
            Grid.SetColumn(valText, 1);
            textRow.Children.Add(valText);
            itemStack.Children.Add(textRow);

            // Slider
            var slider = ThemeSlider(f.Source, "theme:curve:" + f.Name, 480, Curves.Draft.CanEdit);
            slider.Margin = new Thickness(0, 16, 0, 0);
            itemStack.Children.Add(slider);

            fieldBox.Child = itemStack;
            fStack.Children.Add(fieldBox);
        }

        var connectNote = new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 24, 0, 0)
        };
        fStack.Children.Add(connectNote);
        var actions = new WrapPanel { Margin = new Thickness(0,12,0,0) };
        foreach(var (label,tag,action) in new (string,string,Action)[] { ("Keyboard test","keyboard",StartTry), ("Reset","reset",()=>Curves.Reset()), ("Preset","preset",()=>Curves.SetPreset(RebelPresets[(Array.IndexOf(RebelPresets,Curves.Draft[Curves.Channel].Preset)+1)%RebelPresets.Length])) })
        {
            var face = new Border { Padding=new Thickness(12,8,12,8), Margin=new Thickness(0,0,8,0), Background=NexusKit.TileBrush, Child=new TextBlock { Text=label, Foreground=NexusKit.WhiteBrush } };
            actions.Children.Add(ThemeAction(face,"theme:curve:"+tag,action,tag=="keyboard"||Curves.Draft.CanEdit));
        }
        fStack.Children.Add(actions);

        faderPanel.Child = fStack;
        Canvas.SetLeft(faderPanel, 952);
        Canvas.SetTop(faderPanel, 264);
        nexusPageHost.Children.Add(faderPanel);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Adjust"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }

    private void NexusShowAdvanced() => OpenStickTools();
}
