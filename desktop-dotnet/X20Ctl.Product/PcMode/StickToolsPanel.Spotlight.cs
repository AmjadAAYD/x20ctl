using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? SpotlightNavigate { get; set; }
    private Canvas? spotlightHost;
    private Action? spotlightCloseAction;
    private Button? spotlightResetButton;
    private string selectedSpotlightRow = "inner";

    private void BuildSpotlightAdvanced(Action close, Button reset)
    {
        spotlightCloseAction = close;
        spotlightResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = SpotlightKit.HouseBrush;

        spotlightHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Child = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "curves",
            title: "Advanced stick tools",
            lead: "Calibrate physical stick gate, dead zones, and diagonal curve scaling.",
            backLabel: "Done",
            onBack: () => close(),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: spotlightHost,
            hints: new[] { ("A", "Apply"), ("B", "Done") });

        RefreshSpotlightAdvanced();
    }

    private void RefreshSpotlightAdvanced()
    {
        if (spotlightHost == null) return;
        spotlightHost.Children.Clear();

        var r = rows[selectedStick];
        var other = selectedStick == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick;

        // Stick Selection Chips at (302, 186)
        var chips = new StackPanel { Orientation = Orientation.Horizontal };
        chips.Children.Add(SpotlightKit.Chip("Left stick", selectedStick == CurveChannel.LeftStick, () => SelectStick(CurveChannel.LeftStick)));
        chips.Children.Add(new Border { Width = 10 });
        chips.Children.Add(SpotlightKit.Chip("Right stick", selectedStick == CurveChannel.RightStick, () => SelectStick(CurveChannel.RightStick)));
        Canvas.SetLeft(chips, 302);
        Canvas.SetTop(chips, 186);
        spotlightHost.Children.Add(chips);

        // Desk setting rows at (292, 236, w = 620)
        var rowsStack = new StackPanel { Width = 620 };

        var rowGate = SpotlightKit.Row("Gate shape", "Circular or square gate geometry", r.Square.IsChecked == true ? "Square" : "Circular", selectedSpotlightRow == "gate", null, 44);
        rowGate.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_gate = () => { selectedSpotlightRow = "gate"; r.Square.IsChecked = r.Square.IsChecked != true; Changed(); RefreshSpotlightAdvanced(); };
        rowGate.MouseLeftButtonDown += (_, _) => { act_gate(); RefocusSpotlight("theme:stick:row:gate"); };
        rowGate.Focusable = true; rowGate.FocusVisualStyle = null; rowGate.Tag = "theme:stick:row:gate";
        rowGate.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_gate(); RefocusSpotlight("theme:stick:row:gate"); } };
        rowsStack.Children.Add(rowGate);

        var rowInner = SpotlightKit.Row("Inner dead zone", "Resting movement ignored · 0.1% steps", $"{r.Inner.Value:0.0}%", selectedSpotlightRow == "inner", SpotlightKit.HorizontalFader(r.Inner.Value, 110), 44);
        rowInner.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_inner = () => { selectedSpotlightRow = "inner"; RefreshSpotlightAdvanced(); };
        rowInner.MouseLeftButtonDown += (_, _) => { act_inner(); RefocusSpotlight("theme:stick:row:inner"); };
        rowInner.Focusable = true; rowInner.FocusVisualStyle = null; rowInner.Tag = "theme:stick:row:inner";
        rowInner.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_inner(); RefocusSpotlight("theme:stick:row:inner"); } };
        rowsStack.Children.Add(rowInner);

        var rowOuter = SpotlightKit.Row("Full output at", "Outer deadzone edge", $"{r.Outer.Value:0.0}%", selectedSpotlightRow == "outer", SpotlightKit.HorizontalFader(r.Outer.Value, 110), 44);
        rowOuter.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_outer = () => { selectedSpotlightRow = "outer"; RefreshSpotlightAdvanced(); };
        rowOuter.MouseLeftButtonDown += (_, _) => { act_outer(); RefocusSpotlight("theme:stick:row:outer"); };
        rowOuter.Focusable = true; rowOuter.FocusVisualStyle = null; rowOuter.Tag = "theme:stick:row:outer";
        rowOuter.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_outer(); RefocusSpotlight("theme:stick:row:outer"); } };
        rowsStack.Children.Add(rowOuter);

        var rowAnti = SpotlightKit.Row("Minimum output", "Anti-deadzone start", $"{r.Anti.Value:0.0}%", selectedSpotlightRow == "anti", SpotlightKit.HorizontalFader(r.Anti.Value, 110), 44);
        rowAnti.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_anti = () => { selectedSpotlightRow = "anti"; RefreshSpotlightAdvanced(); };
        rowAnti.MouseLeftButtonDown += (_, _) => { act_anti(); RefocusSpotlight("theme:stick:row:anti"); };
        rowAnti.Focusable = true; rowAnti.FocusVisualStyle = null; rowAnti.Tag = "theme:stick:row:anti";
        rowAnti.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_anti(); RefocusSpotlight("theme:stick:row:anti"); } };
        rowsStack.Children.Add(rowAnti);

        var rowMax = SpotlightKit.Row("Maximum output", "Outer saturation clamp", $"{r.Max.Value:0.0}%", selectedSpotlightRow == "max", SpotlightKit.HorizontalFader(r.Max.Value, 110), 44);
        rowMax.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_max = () => { selectedSpotlightRow = "max"; RefreshSpotlightAdvanced(); };
        rowMax.MouseLeftButtonDown += (_, _) => { act_max(); RefocusSpotlight("theme:stick:row:max"); };
        rowMax.Focusable = true; rowMax.FocusVisualStyle = null; rowMax.Tag = "theme:stick:row:max";
        rowMax.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_max(); RefocusSpotlight("theme:stick:row:max"); } };
        rowsStack.Children.Add(rowMax);

        var rowLink = SpotlightKit.Row("Link sticks", "Apply settings to both sticks", linkSticks.IsChecked == true ? "On" : "Off", selectedSpotlightRow == "link", null, 44);
        rowLink.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_link = () => { selectedSpotlightRow = "link"; linkSticks.IsChecked = linkSticks.IsChecked != true; Changed(); RefreshSpotlightAdvanced(); };
        rowLink.MouseLeftButtonDown += (_, _) => { act_link(); RefocusSpotlight("theme:stick:row:link"); };
        rowLink.Focusable = true; rowLink.FocusVisualStyle = null; rowLink.Tag = "theme:stick:row:link";
        rowLink.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_link(); RefocusSpotlight("theme:stick:row:link"); } };
        rowsStack.Children.Add(rowLink);

        var rowRaw = SpotlightKit.Row("Bypass X20CTL processing", "Uses unadjusted input", r.Raw.IsChecked == true ? "On" : "Off", selectedSpotlightRow == "raw", null, 44);
        rowRaw.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_raw = () => { selectedSpotlightRow = "raw"; r.Raw.IsChecked = r.Raw.IsChecked != true; Changed(); RefreshSpotlightAdvanced(); };
        rowRaw.MouseLeftButtonDown += (_, _) => { act_raw(); RefocusSpotlight("theme:stick:row:raw"); };
        rowRaw.Focusable = true; rowRaw.FocusVisualStyle = null; rowRaw.Tag = "theme:stick:row:raw";
        rowRaw.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_raw(); RefocusSpotlight("theme:stick:row:raw"); } };
        rowsStack.Children.Add(rowRaw);

        var rowRatio = SpotlightKit.Row("Vertical sensitivity", "Y-axis ratio", $"{r.Ratio.Value:0}%", selectedSpotlightRow == "ratio", SpotlightKit.HorizontalFader((r.Ratio.Value - 50) / 1.5, 110), 44);
        rowRatio.Cursor = System.Windows.Input.Cursors.Hand;
        Action act_ratio = () => { selectedSpotlightRow = "ratio"; RefreshSpotlightAdvanced(); };
        rowRatio.MouseLeftButtonDown += (_, _) => { act_ratio(); RefocusSpotlight("theme:stick:row:ratio"); };
        rowRatio.Focusable = true; rowRatio.FocusVisualStyle = null; rowRatio.Tag = "theme:stick:row:ratio";
        rowRatio.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { e.Handled = true; act_ratio(); RefocusSpotlight("theme:stick:row:ratio"); } };
        rowsStack.Children.Add(rowRatio);

        Canvas.SetLeft(rowsStack, 292);
        Canvas.SetTop(rowsStack, 236);
        spotlightHost.Children.Add(rowsStack);

        // Follow spot on tall fader at x = 1250, y = 250, h = 330
        var spot = SpotlightKit.FollowSpot(1250, 640, 200, -60, 0.20, pool: false);
        spotlightHost.Children.Add(spot);

        Slider activeSlider = selectedSpotlightRow switch
        {
            "outer" => r.Outer,
            "anti" => r.Anti,
            "max" => r.Max,
            "ratio" => r.Ratio,
            _ => r.Inner
        };

        string faderLabel = selectedSpotlightRow switch
        {
            "gate" => "Gate shape",
            "outer" => "Full output at",
            "anti" => "Minimum output",
            "max" => "Maximum output",
            "link" => "Link sticks",
            "raw" => "Bypass X20CTL",
            "ratio" => "Vertical sensitivity",
            _ => "Inner dead zone"
        };

        string faderVal = selectedSpotlightRow switch
        {
            "gate" => r.Square.IsChecked == true ? "Square" : "Circular",
            "link" => linkSticks.IsChecked == true ? "On" : "Off",
            "raw" => r.Raw.IsChecked == true ? "On" : "Off",
            "ratio" => $"{r.Ratio.Value:0}%",
            _ => $"{activeSlider.Value:0.0}%"
        };

        double faderPct = selectedSpotlightRow switch
        {
            "gate" => r.Square.IsChecked == true ? 100 : 0,
            "link" => linkSticks.IsChecked == true ? 100 : 0,
            "raw" => r.Raw.IsChecked == true ? 100 : 0,
            "ratio" => (r.Ratio.Value - 50) / 1.5,
            _ => activeSlider.Value
        };

        var tallFader = SpotlightKit.Fader(1250, 250, 330, faderPct, true, faderLabel, faderVal);
        tallFader.Cursor = System.Windows.Input.Cursors.Hand;
        tallFader.Focusable = true;
        tallFader.FocusVisualStyle = null;
        tallFader.Tag = "theme:stick:fader";
        bool isDragging = false;

        void ApplyFaderY(double y)
        {
            double relY = Math.Clamp(330 - y, 0, 330);
            double pct = (relY / 330.0) * 100.0;
            if (selectedSpotlightRow == "ratio")
            {
                r.Ratio.Value = Math.Round(50 + pct * 1.5);
            }
            else if (selectedSpotlightRow != "gate" && selectedSpotlightRow != "link" && selectedSpotlightRow != "raw")
            {
                activeSlider.Value = Math.Round(activeSlider.Minimum + (pct / 100.0) * (activeSlider.Maximum - activeSlider.Minimum), 1);
            }
            Changed();
            // Swap the fader's visuals in place so the captured control survives the drag.
            double livePct = selectedSpotlightRow == "ratio" ? (r.Ratio.Value - 50) / 1.5 : activeSlider.Value;
            string liveVal = selectedSpotlightRow == "ratio" ? $"{r.Ratio.Value:0}%" : $"{activeSlider.Value:0.0}%";
            var fresh = SpotlightKit.Fader(1250, 250, 330, livePct, true, faderLabel, liveVal);
            var moved = fresh.Children.Cast<UIElement>().ToList();
            fresh.Children.Clear();
            tallFader.Children.Clear();
            foreach (var child in moved) tallFader.Children.Add(child);
        }

        tallFader.MouseLeftButtonDown += (_, e) =>
        {
            isDragging = true;
            tallFader.CaptureMouse();
            tallFader.Focus();
            var pos = e.GetPosition(tallFader);
            ApplyFaderY(pos.Y);
        };
        tallFader.MouseMove += (_, e) =>
        {
            if (isDragging && tallFader.IsMouseCaptured)
            {
                var pos = e.GetPosition(tallFader);
                ApplyFaderY(pos.Y);
            }
        };
        tallFader.MouseLeftButtonUp += (_, _) =>
        {
            if (isDragging)
            {
                isDragging = false;
                tallFader.ReleaseMouseCapture();
                RefreshSpotlightAdvanced();
                RefocusSpotlight("theme:stick:fader");
            }
        };
        tallFader.KeyDown += (_, e) =>
        {
            double step = (e.Key is Key.Up or Key.Right) ? 1.0 : (e.Key is Key.Down or Key.Left) ? -1.0 : 0.0;
            if (step != 0)
            {
                if (selectedSpotlightRow == "ratio")
                {
                    r.Ratio.Value = Math.Clamp(r.Ratio.Value + step * 5, 50, 200);
                }
                else if (selectedSpotlightRow != "gate" && selectedSpotlightRow != "link" && selectedSpotlightRow != "raw")
                {
                    activeSlider.Value = Math.Clamp(activeSlider.Value + step * 1.0, activeSlider.Minimum, activeSlider.Maximum);
                }
                Changed();
                RefreshSpotlightAdvanced();
                RefocusSpotlight("theme:stick:fader");
                e.Handled = true;
            }
        };
        spotlightHost.Children.Add(tallFader);

        // Preset chips below fader at (1070, 660)
        var presetChips = new StackPanel { Orientation = Orientation.Horizontal, Width = 360, HorizontalAlignment = HorizontalAlignment.Center };
        presetChips.Children.Add(SpotlightKit.Chip("Linear", false, () =>
        {
            r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100;
            Changed(); RefreshSpotlightAdvanced();
        }));
        presetChips.Children.Add(new Border { Width = 6 });
        presetChips.Children.Add(SpotlightKit.Chip("5% DZ", false, () =>
        {
            r.Inner.Value = 5;
            Changed(); RefreshSpotlightAdvanced();
        }));
        presetChips.Children.Add(new Border { Width = 6 });
        presetChips.Children.Add(SpotlightKit.Chip("80% Max", false, () =>
        {
            r.Outer.Value = 80;
            Changed(); RefreshSpotlightAdvanced();
        }));
        presetChips.Children.Add(new Border { Width = 6 });
        presetChips.Children.Add(SpotlightKit.Chip("8% Anti", false, () =>
        {
            r.Anti.Value = 8;
            Changed(); RefreshSpotlightAdvanced();
        }));
        Canvas.SetLeft(presetChips, 1070);
        Canvas.SetTop(presetChips, 660);
        spotlightHost.Children.Add(presetChips);

        // Cue card at (980, 712, w = 580)
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock
        {
            Text = "Gate & Travel Limits",
            FontFamily = SpotlightKit.Body,
            FontSize = 21,
            Foreground = SpotlightKit.ChalkBrush
        });
        cardInner.Children.Add(new TextBlock
        {
            Text = "Calibrate physical stick range and diagonal acceleration.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var applyBtn = SpotlightKit.TheatricalButton(
            selectedStick == CurveChannel.LeftStick ? "Apply to right stick" : "Apply to left stick",
            true, "A", () => CopyStick(selectedStick, other), height: 40, fontSize: 11);
        btnRow.Children.Add(applyBtn);

        var resetBtn = SpotlightKit.TheatricalButton("Reset selected", false, null, () =>
        {
            r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100;
            r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100;
            Changed(); RefreshSpotlightAdvanced();
        }, height: 40, fontSize: 11);
        resetBtn.Margin = new Thickness(10, 0, 0, 0);
        btnRow.Children.Add(resetBtn);

        var driftBtn = SpotlightKit.TheatricalButton("Measure drift", false, null, async () => await MeasureDrift(), height: 40, fontSize: 11);
        driftBtn.Margin = new Thickness(10, 0, 0, 0);
        btnRow.Children.Add(driftBtn);

        cardInner.Children.Add(btnRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 580, padding: new Thickness(24, 20, 24, 20));
        Canvas.SetLeft(cueCard, 980);
        Canvas.SetTop(cueCard, 712);
        spotlightHost.Children.Add(cueCard);
    }

    private void RefocusSpotlight(string tag)
    {
        if (spotlightHost == null) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var el in spotlightHost.Children.OfType<FrameworkElement>())
                if (Equals(el.Tag, tag)) { el.Focus(); Keyboard.Focus(el); break; }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RefreshSpotlightAdvancedGraph(CurveChannel channel)
    {
        if (spotlightHost != null && channel == selectedStick)
            RefreshSpotlightAdvanced();
    }
}
