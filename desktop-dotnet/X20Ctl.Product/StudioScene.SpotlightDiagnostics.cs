using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SpotlightShowDiagnostics()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        if (Diagnostics is not { } diagnostics)
        {
            var none = new TextBlock { Text = "Sensor diagnostics are not available.", FontFamily = SpotlightKit.Body, FontSize = 18, Foreground = SpotlightKit.MuteBrush };
            Canvas.SetLeft(none, 302); Canvas.SetTop(none, 240);
            canvas.Children.Add(none);
        }
        else
        {
            // Live values come from the same blocks every theme reads
            var rows = new StackPanel { Width = 900 };
            foreach (var (label, sub, block) in new (string, string, TextBlock)[]
            {
                ("Left stick", "Observed position", diagnostics.LeftView.Stats),
                ("Right stick", "Observed position", diagnostics.RightView.Stats),
                ("Left trigger", "Observed travel", diagnostics.LtView.Stats),
                ("Right trigger", "Observed travel", diagnostics.RtView.Stats),
                ("State changes", "Per second, as seen by this PC", diagnostics.RateBlock),
            })
            {
                var live = new TextBlock { FontFamily = SpotlightKit.Body, FontSize = 15.5, Foreground = SpotlightKit.ChalkDimBrush, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Width = 520 };
                live.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = block });
                rows.Children.Add(SpotlightKit.Row(label, sub, null, false, live, 62));
            }
            Canvas.SetLeft(rows, 292);
            Canvas.SetTop(rows, 260);
            canvas.Children.Add(rows);
        }

        var note = new TextBlock { Text = "Listening only. Nothing is written to the controller.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 16, Foreground = SpotlightKit.ChalkDimBrush };
        Canvas.SetLeft(note, 302); Canvas.SetTop(note, 700);
        canvas.Children.Add(note);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "device",
            title: "Diagnostics",
            lead: "What this PC sees from the controller right now.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Device"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("B", "Back") },
            leadW: 1220,
            titleSize: 36);

        spotlightPageHost.Children.Add(frame);
    }

    private void SpotlightShowMouse()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        if (Mouse is not { } mouse)
        {
            var none = new TextBlock { Text = "Mouse mode is not available for this controller.", FontFamily = SpotlightKit.Body, FontSize = 18, Foreground = SpotlightKit.MuteBrush };
            Canvas.SetLeft(none, 302); Canvas.SetTop(none, 240);
            canvas.Children.Add(none);
        }
        else
        {
            // Left: switch and the real choices
            var left = new StackPanel { Width = 640 };
            var enabled = new CheckBox { Tag = "theme:mouse:switch", VerticalAlignment = VerticalAlignment.Center };
            enabled.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Source = mouse.Switch, Mode = System.Windows.Data.BindingMode.TwoWay });
            left.Children.Add(SpotlightKit.Row("Controller as mouse", "Move the cursor with a stick", null, false, enabled, 46));
            foreach (var (label, source) in new[] { ("Input source", mouse.SourceBox), ("Pointer stick", mouse.PointerBox), ("Shortcut first", mouse.Chord1Box), ("Shortcut second", mouse.Chord2Box), ("Hold behavior", mouse.HoldBox) })
            {
                var choice = new ComboBox { Width = 230, Height = 32, DisplayMemberPath = source.DisplayMemberPath, SelectedValuePath = source.SelectedValuePath, Tag = "theme:mouse:" + label };
                choice.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding("ItemsSource") { Source = source });
                choice.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, new System.Windows.Data.Binding("SelectedIndex") { Source = source, Mode = System.Windows.Data.BindingMode.TwoWay });
                left.Children.Add(SpotlightKit.Row(label, null, null, false, choice, 46));
            }
            foreach (var (label, source) in new[] { ("Scroll", mouse.ScrollsBox), ("Shortcut toggle", mouse.ComboToggle), ("Horizontal scroll", mouse.HorizontalBox), ("Invert vertical", mouse.InvertVBox), ("Invert horizontal", mouse.InvertHBox) })
            {
                var toggle = new CheckBox { Tag = "theme:mouse:" + label, VerticalAlignment = VerticalAlignment.Center };
                toggle.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Source = source, Mode = System.Windows.Data.BindingMode.TwoWay });
                left.Children.Add(SpotlightKit.Row(label, null, null, false, toggle, 46));
            }
            var scroll = new ScrollViewer { Width = 660, Height = 640, Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Canvas.SetLeft(scroll, 292);
            Canvas.SetTop(scroll, 176);
            canvas.Children.Add(scroll);

            // Right: the four sliders
            var cardInner = new StackPanel();
            foreach (var (name, sub, source) in new[] { ("Pointer speed", "Base cursor velocity", mouse.SpeedSlider), ("Response curve", "Stick speed ramp", mouse.CurveSlider), ("Scroll speed", "Vertical scroll rate", mouse.ScrollSlider), ("Dead zone", "Minimum movement", mouse.DeadzoneSlider) })
            {
                var slider = ThemeSlider(source, "theme:mouse:" + name, 190, true); slider.TickFrequency = source.TickFrequency;
                var value = new TextBlock { Width = 48, FontFamily = SpotlightKit.Body, FontSize = 15.5, Foreground = SpotlightKit.ChalkDimBrush, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                value.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Value") { Source = source, StringFormat = "{0:0.##}" });
                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(slider); right.Children.Add(value);
                cardInner.Children.Add(SpotlightKit.Row(name, sub, null, false, right, 56));
            }
            var status = new TextBlock { FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 14, 20, 0) };
            void UpdateState() { status.Text = mouse.StatusText + " · " + mouse.MappedText + " · " + mouse.SavedText; }
            mouse.Watch(UpdateState); UpdateState();
            cardInner.Children.Add(status);
            var reset = SpotlightKit.TheatricalButton("Reset mouse settings", false, null, onClick: mouse.ResetMode, height: 40, fontSize: 11);
            reset.Margin = new Thickness(20, 16, 0, 0); reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Tag = "theme:mouse:reset";
            cardInner.Children.Add(reset);

            var cueCard = SpotlightKit.CueCard(cardInner, 560, label: "Motion Profile", padding: new Thickness(26, 22, 26, 22));
            Canvas.SetLeft(cueCard, 1000);
            Canvas.SetTop(cueCard, 176);
            canvas.Children.Add(cueCard);
        }

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "mouse",
            title: "Mouse mode",
            lead: "Control your desktop pointer, scrolling and mouse clicks using the gamepad.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("B", "Back") },
            leadW: 600,
            titleSize: 36);

        spotlightPageHost.Children.Add(frame);
    }
}
