using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? TideNavigate { get; set; }
    private TideToolFrame? tideFrame;
    private Action? tideCloseAction;
    private Button? tideResetButton;
    private string selectedTideRow = "inner";
    private Canvas? tidePlotCanvas;

    private void BuildTideAdvanced(Action close, Button reset)
    {
        tideCloseAction = close;
        tideResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = TideKit.Sea0Brush;

        tideFrame = new TideToolFrame(
            "STICK TOOLS",
            "PC-side processing for accessible analog sticks",
            onBack: () => close(),
            hints: new[] { "\u24B7 Back", "\u24B6 Adjust", "\u24BD Reset curve", "\u24BE Next preset" },
            seed: 12);
        Child = tideFrame;

        RefreshTideAdvanced();
    }

    private void RefreshTideAdvanced()
    {
        if (tideFrame == null) return;
        // The rebuild replaces every row; put keyboard focus back on the row that had it.
        var focusTag = (Keyboard.FocusedElement as FrameworkElement)?.Tag as string;
        RefreshTideAdvancedCore();
        if (focusTag == null || !focusTag.StartsWith("theme:stick:")) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (FindTagged(tideFrame.ContentArea, focusTag) is { } el) { el.Focus(); Keyboard.Focus(el); }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private static FrameworkElement? FindTagged(DependencyObject root, string tag)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is FrameworkElement fe && Equals(fe.Tag, tag)) return fe;
            if (FindTagged(c, tag) is { } hit) return hit;
        }
        return null;
    }

    private void RefreshTideAdvancedCore()
    {
        tideFrame!.ContentArea.Children.Clear();

        var r = rows[selectedStick];

        // Left List: Tabs + Staircase rows
        var listStack = new StackPanel { Width = 640 };
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        tabsRow.Children.Add(TideKit.PersonaButton("LEFT STICK", primary: selectedStick == CurveChannel.LeftStick, onClick: () => SelectStick(CurveChannel.LeftStick)));
        var tab2 = TideKit.PersonaButton("RIGHT STICK", primary: selectedStick == CurveChannel.RightStick, onClick: () => SelectStick(CurveChannel.RightStick));
        tab2.Margin = new Thickness(14, 0, 0, 0);
        tabsRow.Children.Add(tab2);
        listStack.Children.Add(tabsRow);

        listStack.Children.Add(BuildTideRow(
            "link",
            "Link stick settings",
            value: null,
            ctl: "toggle",
            focused: selectedTideRow == "link",
            onClick: () => { selectedTideRow = "link"; linkSticks.IsChecked = linkSticks.IsChecked != true; Changed(); RefreshTideAdvanced(); }));

        listStack.Children.Add(BuildTideRow(
            "raw",
            "Bypass X20CTL processing",
            value: null,
            ctl: "toggle",
            focused: selectedTideRow == "raw",
            onClick: () => { selectedTideRow = "raw"; r.Raw.IsChecked = r.Raw.IsChecked != true; Changed(); RefreshTideAdvanced(); }));

        listStack.Children.Add(TideKit.SectionHeader("Input range"));

        listStack.Children.Add(BuildTideRow(
            "inner",
            "Inner dead zone",
            value: $"{r.Inner.Value:0.0}%",
            ctl: $"slider:{r.Inner.Value:0}",
            focused: selectedTideRow == "inner",
            onClick: () => { selectedTideRow = "inner"; RefreshTideAdvanced(); },
            slider: r.Inner,
            step: 0.5));

        listStack.Children.Add(BuildTideRow(
            "outer",
            "Full output at",
            value: $"{r.Outer.Value:0.0}%",
            ctl: $"slider:{r.Outer.Value:0}",
            focused: selectedTideRow == "outer",
            onClick: () => { selectedTideRow = "outer"; RefreshTideAdvanced(); },
            slider: r.Outer,
            step: 1.0));

        listStack.Children.Add(TideKit.SectionHeader("Output range"));

        listStack.Children.Add(BuildTideRow(
            "anti",
            "Minimum output",
            value: $"{r.Anti.Value:0.0}%",
            ctl: $"slider:{r.Anti.Value:0}",
            focused: selectedTideRow == "anti",
            onClick: () => { selectedTideRow = "anti"; RefreshTideAdvanced(); },
            slider: r.Anti,
            step: 0.5));

        listStack.Children.Add(BuildTideRow(
            "max",
            "Maximum output",
            value: $"{r.Max.Value:0.0}%",
            ctl: $"slider:{r.Max.Value:0}",
            focused: selectedTideRow == "max",
            onClick: () => { selectedTideRow = "max"; RefreshTideAdvanced(); },
            slider: r.Max,
            step: 1.0));

        listStack.Children.Add(BuildTideRow(
            "gate",
            "Gate: \u25CB Circular / \u25A1 Square",
            value: null,
            ctl: "toggle",
            focused: selectedTideRow == "gate",
            onClick: () => { selectedTideRow = "gate"; r.Square.IsChecked = r.Square.IsChecked != true; Changed(); RefreshTideAdvanced(); }));

        listStack.Children.Add(BuildTideRow(
            "ratio",
            "Vertical sensitivity",
            value: $"{r.Ratio.Value:0}%",
            ctl: $"slider:{(r.Ratio.Value - 50) / 1.5:0}",
            focused: selectedTideRow == "ratio",
            onClick: () => { selectedTideRow = "ratio"; RefreshTideAdvanced(); },
            slider: r.Ratio,
            step: 5.0));

        listStack.Children.Add(BuildTideRow(
            "reset-range",
            "Reset range to 1:1",
            value: null,
            ctl: "chev",
            focused: false,
            onClick: () => { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; Changed(); RefreshTideAdvanced(); }));

        listStack.Children.Add(BuildTideRow(
            "drift",
            "Measure drift",
            value: null,
            ctl: "chev",
            focused: false,
            onClick: async () => await MeasureDrift()));

        listStack.Children.Add(BuildTideRow(
            "reset-all",
            "Reset selected advanced settings",
            value: null,
            ctl: "chev",
            focused: false,
            onClick: () => { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100; Changed(); RefreshTideAdvanced(); }));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        tideFrame.SetLeftList(listHost, 176);

        // Right side: Visual Card + Detail Card
        tidePlotCanvas = new Canvas { Width = 556, Height = 335 };
        DrawTideCurve();
        var visualCard = TideKit.VisualCard(tidePlotCanvas, width: 580, rot: -2);

        // Detail Card
        string detailTitle = selectedTideRow switch
        {
            "gate" => "GATE GEOMETRY",
            "outer" => "FULL OUTPUT AT",
            "anti" => "MINIMUM OUTPUT",
            "max" => "MAXIMUM OUTPUT",
            "link" => "LINK STICKS",
            "raw" => "BYPASS ROUTING",
            "ratio" => "VERTICAL SENSITIVITY",
            _ => "INNER DEAD ZONE"
        };

        string detailVal = selectedTideRow switch
        {
            "gate" => r.Square.IsChecked == true ? "SQUARE GATE" : "CIRCULAR GATE",
            "outer" => $"{r.Outer.Value:0.0}%",
            "anti" => $"{r.Anti.Value:0.0}%",
            "max" => $"{r.Max.Value:0.0}%",
            "link" => linkSticks.IsChecked == true ? "LINKED" : "SEPARATE",
            "raw" => r.Raw.IsChecked == true ? "BYPASSED" : "FILTERED",
            "ratio" => $"{r.Ratio.Value:0}%",
            _ => $"{r.Inner.Value:0.0}%"
        };

        string detailDesc = selectedTideRow switch
        {
            "gate" => "Allows diagonals to reach full deflection on both axes simultaneously.",
            "outer" => "Outer threshold where deflection reaches 100% output to the game.",
            "anti" => "Output floor applied immediately once outside the dead zone.",
            "max" => "Caps the maximum output sent to the game for precision control.",
            "ratio" => "Scales vertical stick output relative to horizontal pitch.",
            _ => "Resting stick movements below this threshold are ignored to prevent unwanted drift."
        };

        var detailCard = TideKit.DetailCard(
            title: detailTitle,
            sub: detailVal,
            body: detailDesc,
            buttons: new[] { "DONE", "RESET", "MEASURE DRIFT" },
            width: 580,
            rot: 2);

        if (detailCard is Border b && b.Child is StackPanel sp)
        {
            var btnRow = sp.Children.OfType<StackPanel>().LastOrDefault();
            if (btnRow != null)
            {
                var btns = btnRow.Children.OfType<Button>().ToArray();
                if (btns.Length > 0) btns[0].Click += (_, _) => tideCloseAction?.Invoke();
                if (btns.Length > 1) btns[1].Click += (_, _) => { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100; Changed(); RefreshTideAdvanced(); };
                if (btns.Length > 2) btns[2].Click += async (_, _) => await MeasureDrift();
            }
        }

        var sideStack = TideKit.SideStack(visualCard, detailCard, width: 600);
        tideFrame.SetSideStack(sideStack, 182);
    }

    private void DrawTideCurve()
    {
        if (tidePlotCanvas == null) return;
        tidePlotCanvas.Children.Clear();

        for (int i = 0; i <= 556; i += 51)
        {
            tidePlotCanvas.Children.Add(new Line { X1 = i, Y1 = 10, X2 = i, Y2 = 305, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
        }
        for (int j = 10; j <= 305; j += 42)
        {
            tidePlotCanvas.Children.Add(new Line { X1 = 0, Y1 = j, X2 = 556, Y2 = j, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
        }

        var profile = Build();
        var curve = profile.Curves.GetValueOrDefault(selectedStick.ToString());
        var tuning = profile.Sticks.GetValueOrDefault(selectedStick.ToString());

        var points = new PointCollection();
        for (int i = 0; i <= 100; i++)
        {
            double inVal = i / 100.0;
            double outVal = PadProcessor.Response(curve, tuning, inVal);
            double px = i * 5.56;
            double py = 305 - (outVal * 295);
            points.Add(new Point(px, Math.Clamp(py, 10, 305)));
        }
        var curveLine = new Polyline { Stroke = TideKit.SpikeRedBrush, StrokeThickness = 3.5, Points = points };
        tidePlotCanvas.Children.Add(curveLine);

        // Indicator dot
        double stickX = selectedStick == CurveChannel.LeftStick ? lx : rx;
        double stickY = selectedStick == CurveChannel.LeftStick ? ly : ry;
        double defl = Math.Clamp(Math.Sqrt(stickX * stickX + stickY * stickY), 0, 1);
        double curOut = PadProcessor.Response(curve, tuning, defl);

        var dot = new Ellipse { Width = 14, Height = 14, Fill = TideKit.WhiteBrush, Stroke = TideKit.InkBrush, StrokeThickness = 2 };
        Canvas.SetLeft(dot, defl * 556 - 7);
        Canvas.SetTop(dot, 305 - (curOut * 295) - 7);
        tidePlotCanvas.Children.Add(dot);
    }

    private void RefreshTideAdvancedGraph(CurveChannel channel)
    {
        if (tideFrame != null && channel == selectedStick)
            DrawTideCurve();
    }

    private FrameworkElement BuildTideRow(string rowKey, string title, string? value, string? ctl, bool focused, Action onClick, Slider? slider = null, double step = 1.0)
    {
        var row = TideKit.PersonaRow(title, value: value, ctl: ctl, focused: focused, onClick: onClick);
        row.Focusable = true;
        row.FocusVisualStyle = null;
        row.Tag = "theme:stick:" + rowKey;
        row.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                onClick();
                e.Handled = true;
            }
            else if (slider != null && e.Key is Key.Left or Key.Down)
            {
                slider.Value = Math.Clamp(slider.Value - step, slider.Minimum, slider.Maximum);
                Changed();
                RefreshTideAdvanced();
                e.Handled = true;
            }
            else if (slider != null && e.Key is Key.Right or Key.Up)
            {
                slider.Value = Math.Clamp(slider.Value + step, slider.Minimum, slider.Maximum);
                Changed();
                RefreshTideAdvanced();
                e.Handled = true;
            }
        };
        return row;
    }
}
