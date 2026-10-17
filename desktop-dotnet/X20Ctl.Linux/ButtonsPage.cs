using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Pick a control on the pad, then pick what it sends.</summary>
internal sealed class ButtonsPage : UserControl
{
    readonly StudioState state; string? selected;
    public ButtonsPage(StudioState state) { this.state = state; Build(); }

    void Build()
    {
        var art = new Grid { MinWidth = 520 };
        var bmp = Ui.Art(state.Model.Id, "front-full");
        if (bmp != null) art.Children.Add(new Image { Source = bmp, Stretch = Stretch.Uniform });
        art.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = Spots(bmp) });
        var right = new StackPanel { Spacing = 12, Width = 340 };
        right.Children.Add(Ui.Label(selected == null ? "Select a control" : selected + " sends " + Name(state.Buttons.Target(selected)), 18, null, FontWeight.SemiBold));
        if (selected == null) right.Children.Add(Ui.Label("Click a button on the controller, then choose what it should send.", 13, Ui.Quiet));
        else
        {
            string from = selected;
            var wrap = new WrapPanel();
            foreach (string t in ButtonDraft.Targets.Append("NONE"))
            {
                string target = t;
                var chip = Ui.Chip(Name(target), state.Buttons.Target(from) == target, () => { state.Buttons.Set(from, target); Build(); });
                chip.Margin = new Thickness(0, 0, 6, 6); wrap.Children.Add(chip);
            }
            right.Children.Add(wrap);
            right.Children.Add(Ui.Chip("Restore " + from + " → " + from, false, () => { state.Buttons.Reset(from); Build(); }));
        }
        right.Children.Add(Ui.Label(state.Buttons.UnsentChanges + " changed", 12, Ui.Quiet));
        var grid = new Grid { ColumnDefinitions = new("*,Auto") };
        grid.Children.Add(Ui.Panel(art, 10));
        var side = Ui.Panel(right); side.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(side, 1); grid.Children.Add(side);
        Content = grid;
    }

    static string Name(string target) => target == "NONE" ? "Disabled" : target;

    Control Spots(Bitmap? bmp)
    {
        double w = bmp?.Size.Width ?? 1536, h = bmp?.Size.Height ?? 1024;
        var canvas = new Canvas { Width = w, Height = h };
        foreach (var (name, p) in state.Model.FrontButtons)
        {
            if (!ButtonDraft.Sources.Contains(name)) continue;
            bool on = selected == name, changed = state.Buttons.Target(name) != name;
            string key = name;
            var dot = new Border
            {
                Width = 62, Height = 62, CornerRadius = new CornerRadius(31), Background = on ? Ui.Accent : changed ? Ui.Brush("#2A5FB8") : Brushes.Transparent,
                BorderBrush = on ? Brushes.White : Ui.Brush("#6FA8FF"), BorderThickness = new Thickness(on ? 4 : 2), Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock { Text = changed ? Name(state.Buttons.Target(name)) : "", Foreground = Brushes.White, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            dot.PointerPressed += (_, _) => { selected = key; Build(); };
            Canvas.SetLeft(dot, p.X * w - 31); Canvas.SetTop(dot, p.Y * h - 31); canvas.Children.Add(dot);
        }
        return canvas;
    }
}
