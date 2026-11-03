using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? NexusNavigate { get; set; }
    private Canvas? nexusHost;
    private Action? nexusCloseAction;
    private Button? nexusResetButton;
    private string selectedNexusRow = "inner";
    private Canvas? nexusGraphCanvas;
    private TextBlock? nexusDetailValueTb;
    private readonly Dictionary<string, TextBlock> nexusSliderReadouts = new();

    private void BuildNexusAdvanced(Action close, Button reset)
    {
        nexusCloseAction = close;
        nexusResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = NexusKit.HouseBrush;

        nexusHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Child = NexusToolFrame.WrapFrame(
            "Advanced stick tools",
            "Dead zones, tension trim and response smoothing for hardware-level precision.",
            nexusHost,
            new[] { ("A", "Adjust"), ("B", "Back"), ("X", "Reset curve"), ("Y", "Next preset") },
            onBack: close);

        RefreshNexusAdvanced();
    }

    private void RefreshNexusAdvanced()
    {
        if (nexusHost == null) return;
        nexusHost.Children.Clear();
        nexusSliderReadouts.Clear();

        var r = rows[selectedStick];

        // Tabs: Left stick, Right stick, Link L+R, Bypass at (68, 230)
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        var leftTab = new Border
        {
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(4),
            Background = selectedStick == CurveChannel.LeftStick ? NexusKit.WhiteBrush : NexusKit.TileBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Left stick",
                FontFamily = NexusKit.Body,
                FontSize = 16,
                FontWeight = selectedStick == CurveChannel.LeftStick ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = selectedStick == CurveChannel.LeftStick ? NexusKit.B("#111111") : NexusKit.WhiteBrush
            }
        };
        leftTab.MouseLeftButtonDown += (_, _) => SelectStick(CurveChannel.LeftStick);
        tabsStack.Children.Add(leftTab);

        var rightTab = new Border
        {
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(4),
            Background = selectedStick == CurveChannel.RightStick ? NexusKit.WhiteBrush : NexusKit.TileBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Right stick",
                FontFamily = NexusKit.Body,
                FontSize = 16,
                FontWeight = selectedStick == CurveChannel.RightStick ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = selectedStick == CurveChannel.RightStick ? NexusKit.B("#111111") : NexusKit.WhiteBrush
            }
        };
        rightTab.MouseLeftButtonDown += (_, _) => SelectStick(CurveChannel.RightStick);
        tabsStack.Children.Add(rightTab);

        var linkTab = new Border
        {
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(4),
            Background = linkSticks.IsChecked == true ? NexusKit.GreenBrush : NexusKit.TileBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = linkSticks.IsChecked == true ? "Link L+R: On" : "Link L+R: Off",
                FontFamily = NexusKit.Body,
                FontSize = 16,
                FontWeight = FontWeights.Normal,
                Foreground = linkSticks.IsChecked == true ? NexusKit.B("#111111") : NexusKit.WhiteBrush
            }
        };
        linkTab.MouseLeftButtonDown += (_, _) => { linkSticks.IsChecked = linkSticks.IsChecked != true; Changed(); RefreshNexusAdvanced(); };
        tabsStack.Children.Add(linkTab);

        var rawTab = new Border
        {
            Padding = new Thickness(18, 8, 18, 8),
            Margin = new Thickness(0, 0, 10, 0),
            CornerRadius = new CornerRadius(4),
            Background = r.Raw.IsChecked == true ? NexusKit.GreenBrush : NexusKit.TileBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = new TextBlock
            {
                Text = r.Raw.IsChecked == true ? "Bypass: On" : "Bypass: Off",
                FontFamily = NexusKit.Body,
                FontSize = 16,
                FontWeight = FontWeights.Normal,
                Foreground = r.Raw.IsChecked == true ? NexusKit.B("#111111") : NexusKit.WhiteBrush
            }
        };
        rawTab.MouseLeftButtonDown += (_, _) => { r.Raw.IsChecked = r.Raw.IsChecked != true; Changed(); RefreshNexusAdvanced(); };
        tabsStack.Children.Add(rawTab);

        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 230);
        nexusHost.Children.Add(tabsStack);

        // Left Rows Column at (68, 280, w = 820)
        var rowPanel = new StackPanel { Width = 820 };

        rowPanel.Children.Add(new TextBlock
        {
            Text = "Input range",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 6, 0, 10)
        });

        // Row 1: Inner dead zone
        var r1Right = BuildNexusSliderControl("inner", r.Inner, 0, StickTuning.MaxInner);
        var r1 = NexusKit.RowButton("Inner dead zone", "Resting movement ignored · 0.1% steps", r1Right, focused: selectedNexusRow == "inner", onClick: () => { selectedNexusRow = "inner"; RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r1);

        // Row 2: Full output at
        var r2Right = BuildNexusSliderControl("outer", r.Outer, StickTuning.MinOuter, 100);
        var r2 = NexusKit.RowButton("Full output at", "Deflection where output reaches 100%", r2Right, focused: selectedNexusRow == "outer", onClick: () => { selectedNexusRow = "outer"; RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r2);

        rowPanel.Children.Add(new TextBlock
        {
            Text = "Output range",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 12, 0, 10)
        });

        // Row 3: Minimum output
        var r3Right = BuildNexusSliderControl("anti", r.Anti, 0, StickTuning.MaxAnti);
        var r3 = NexusKit.RowButton("Minimum output", "Anti-deadzone output floor", r3Right, focused: selectedNexusRow == "anti", onClick: () => { selectedNexusRow = "anti"; RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r3);

        // Row 4: Maximum output
        var r4Right = BuildNexusSliderControl("max", r.Max, StickTuning.MinOuter, 100);
        var r4 = NexusKit.RowButton("Maximum output", "Output saturation ceiling clamp", r4Right, focused: selectedNexusRow == "max", onClick: () => { selectedNexusRow = "max"; RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r4);

        // Row 5: Gate shape
        var r5Right = NexusKit.Toggle(r.Square.IsChecked == true, () => { r.Square.IsChecked = r.Square.IsChecked != true; Changed(); RefreshNexusAdvanced(); });
        var r5 = NexusKit.RowButton("Gate circularity", "Circular or square gate geometry", r5Right, focused: selectedNexusRow == "gate", onClick: () => { selectedNexusRow = "gate"; r.Square.IsChecked = r.Square.IsChecked != true; Changed(); RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r5);

        // Row 6: Vertical sensitivity
        var r6Right = BuildNexusSliderControl("ratio", r.Ratio, StickTuning.MinRatio, StickTuning.MaxRatio, isInteger: true);
        var r6 = NexusKit.RowButton("Vertical sensitivity", "Scales Y-axis response relative to X", r6Right, focused: selectedNexusRow == "ratio", onClick: () => { selectedNexusRow = "ratio"; RefreshNexusAdvanced(); });
        rowPanel.Children.Add(r6);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 280);
        nexusHost.Children.Add(rowPanel);

        // Right Details Side at (920, 280, w = 612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual curve box
        var curveBox = new Border
        {
            Width = 612,
            Height = 200,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18)
        };
        nexusGraphCanvas = new Canvas { Width = 572, Height = 170, Margin = new Thickness(20, 15, 20, 15) };
        DrawNexusCurve();
        curveBox.Child = nexusGraphCanvas;
        sideStack.Children.Add(curveBox);

        // Details card
        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();

        string currentTitle = selectedNexusRow switch
        {
            "gate" => "Gate circularity",
            "outer" => "Full output at",
            "anti" => "Minimum output",
            "max" => "Maximum output",
            "ratio" => "Vertical sensitivity",
            _ => "Inner dead zone"
        };

        string currentVal = selectedNexusRow switch
        {
            "gate" => r.Square.IsChecked == true ? "Square Gate" : "Circular Gate",
            "outer" => $"{r.Outer.Value:0.0}%",
            "anti" => $"{r.Anti.Value:0.0}%",
            "max" => $"{r.Max.Value:0.0}%",
            "ratio" => $"{r.Ratio.Value:0}%",
            _ => $"{r.Inner.Value:0.0}%"
        };

        string currentDesc = selectedNexusRow switch
        {
            "gate" => "Allows diagonal input to reach full output on both axes simultaneously.",
            "outer" => "Outer threshold where deflection reaches 100% output. Lower values reach top speed faster.",
            "anti" => "Minimum output floor applied immediately once outside the inner deadzone to counteract game dead zones.",
            "max" => "Caps the maximum output sent to the game for precision aim speed control.",
            "ratio" => "Adjusts vertical pitch sensitivity relative to horizontal yaw aiming.",
            _ => "Minimum stick deflection before output begins. Keep low for reactive aim, raise if experiencing stick drift."
        };

        cardStack.Children.Add(new TextBlock
        {
            Text = currentTitle,
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });

        nexusDetailValueTb = new TextBlock
        {
            Text = currentVal,
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        };
        cardStack.Children.Add(nexusDetailValueTb);

        // Preset Chips
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
        foreach (var (ch, presetAction) in new (string, Action)[]
        {
            ("Linear", () => { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; }),
            ("DZ 5%", () => { r.Inner.Value = 5; }),
            ("Full 80%", () => { r.Outer.Value = 80; }),
            ("Anti 8%", () => { r.Anti.Value = 8; })
        })
        {
            var chip = new Border
            {
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(4),
                Background = NexusKit.TileBrush,
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock
                {
                    Text = ch,
                    FontFamily = NexusKit.Body,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = NexusKit.WhiteBrush
                }
            };
            chip.MouseLeftButtonDown += (_, _) => { presetAction(); Changed(); RefreshNexusAdvanced(); };
            chips.Children.Add(chip);
        }
        cardStack.Children.Add(chips);

        cardStack.Children.Add(new TextBlock
        {
            Text = currentDesc,
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var actionButtonsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var doneBtn = NexusKit.TheatricalButton("Done", primary: true, onClick: () => nexusCloseAction?.Invoke(), height: 44, fontSize: 15);
        actionButtonsRow.Children.Add(doneBtn);

        var resetBtn = NexusKit.TheatricalButton("Reset", primary: false, onClick: () =>
        {
            r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100;
            r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100;
            Changed(); RefreshNexusAdvanced();
        }, height: 44, fontSize: 15);
        resetBtn.Margin = new Thickness(10, 0, 0, 0);
        actionButtonsRow.Children.Add(resetBtn);

        var driftBtn = NexusKit.TheatricalButton("Measure drift", primary: false, onClick: async () => await MeasureDrift(), height: 44, fontSize: 15);
        driftBtn.Margin = new Thickness(10, 0, 0, 0);
        actionButtonsRow.Children.Add(driftBtn);

        cardStack.Children.Add(actionButtonsRow);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 280);
        nexusHost.Children.Add(sideStack);
    }

    private FrameworkElement BuildNexusSliderControl(string rowKey, Slider source, double min, double max, bool isInteger = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = source.Value,
            Width = 190,
            TickFrequency = isInteger ? 5 : 0.1,
            IsSnapToTickEnabled = true,
            SmallChange = isInteger ? 5 : 0.1,
            LargeChange = isInteger ? 10 : 1.0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Tag = "theme:stick:" + rowKey
        };
        var valBlock = new TextBlock
        {
            Text = isInteger ? $"{source.Value:0}%" : $"{source.Value:0.0}%",
            FontFamily = NexusKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.GreenBrush,
            Width = 56,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        nexusSliderReadouts[rowKey] = valBlock;

        slider.ValueChanged += (_, e) =>
        {
            source.Value = isInteger ? Math.Round(e.NewValue) : Math.Round(e.NewValue, 1);
            string valText = isInteger ? $"{source.Value:0}%" : $"{source.Value:0.0}%";
            valBlock.Text = valText;
            if (selectedNexusRow == rowKey && nexusDetailValueTb != null)
            {
                nexusDetailValueTb.Text = valText;
            }
            DrawNexusCurve();
            Changed();
        };
        panel.Children.Add(slider);
        panel.Children.Add(valBlock);
        return panel;
    }

    private void DrawNexusCurve()
    {
        if (nexusGraphCanvas == null) return;
        nexusGraphCanvas.Children.Clear();

        var profile = Build();
        var curve = profile.Curves.GetValueOrDefault(selectedStick.ToString());
        var tuning = profile.Sticks.GetValueOrDefault(selectedStick.ToString());

        // Grid lines
        for (int i = 0; i <= 4; i++)
        {
            double y = i * 42.5;
            nexusGraphCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = 572, Y2 = y, Stroke = NexusKit.B("#25282e"), StrokeThickness = 1 });
        }

        // Response polyline
        var points = new PointCollection();
        for (int i = 0; i <= 100; i++)
        {
            double inVal = i / 100.0;
            double outVal = PadProcessor.Response(curve, tuning, inVal);
            double px = i * 5.72;
            double py = 170 - (outVal * 170);
            points.Add(new Point(px, Math.Clamp(py, 0, 170)));
        }
        var curveLine = new Polyline { Stroke = NexusKit.GreenBrush, StrokeThickness = 3, Points = points };
        nexusGraphCanvas.Children.Add(curveLine);

        // Dot at current position
        double stickX = selectedStick == CurveChannel.LeftStick ? lx : rx;
        double stickY = selectedStick == CurveChannel.LeftStick ? ly : ry;
        double defl = Math.Clamp(Math.Sqrt(stickX * stickX + stickY * stickY), 0, 1);
        double curOut = PadProcessor.Response(curve, tuning, defl);

        var cDot = new Ellipse { Width = 12, Height = 12, Fill = NexusKit.WhiteBrush };
        Canvas.SetLeft(cDot, defl * 572 - 6);
        Canvas.SetTop(cDot, (1.0 - curOut) * 170 - 6);
        nexusGraphCanvas.Children.Add(cDot);
    }

    private void RefreshNexusAdvancedGraph(CurveChannel channel)
    {
        if (nexusHost != null && channel == selectedStick)
            DrawNexusCurve();
    }
}
