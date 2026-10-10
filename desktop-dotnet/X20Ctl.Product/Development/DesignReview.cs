using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Design-system gallery: every token-driven control in normal/selected/focused/disabled states,
/// a dashboard-like composition, and the popup opening captured frame by frame.</summary>
internal static class DesignReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var root = new Grid(); root.Children.Add(new GalaxyBackdrop());
        var page = new Grid { Margin = new(48, 36, 48, 30) };
        page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        // header + tabs
        var header = new DockPanel { Margin = new(0, 0, 0, 10) };
        var chip = new StatusChip("Controller detected", ChipKind.Success); DockPanel.SetDock(chip, Dock.Right); header.Children.Add(chip);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "X20CTL", Style = DS.Style("DS.Heading"), FontSize = 24, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Design system", Style = DS.Style("DS.Body"), Margin = new(16, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center } } });
        page.Children.Add(header);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 22) };
        foreach (var (t, on) in new[] { ("Buttons", true), ("Curves", false), ("Macros", false), ("Vibration", false), ("Device", false), ("Setups", false) }) tabs.Children.Add(new RadioButton { Content = t, Style = DS.Style("DS.Tab"), GroupName = "tabs", IsChecked = on });
        Grid.SetRow(tabs, 1); page.Children.Add(tabs);
        // body: left hero surface, right control surface
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(1.25, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); body.ColumnDefinitions.Add(new());
        Grid.SetRow(body, 2); page.Children.Add(body);
        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = "Ready to configure", Style = DS.Style("DS.Eyebrow") });
        left.Children.Add(new TextBlock { Text = "EasySMX X20", Style = DS.Style("DS.Display"), Margin = new(0, 4, 0, 2) });
        left.Children.Add(new TextBlock { Text = "Player 1 · Local draft · Hardware apply locked", Style = DS.Style("DS.Body") });
        var hero = new LiveController(); hero.Show("x20", false); hero.SetInput(.6, .55, -.4, -.5, 0, 0);
        left.Children.Add(new Viewbox { Child = hero, Height = 330, Margin = new(0, 10, 0, 10) });
        var tiles = new UniformGrid { Columns = 3 };
        foreach (var (i, v, c) in new[] { ("", "14", "Mappable inputs"), ("", "4", "Macro slots"), ("", "7", "Supported models") }) tiles.Children.Add(new StatTile(i, v, c) { Margin = new(0, 0, 12, 0) });
        left.Children.Add(tiles);
        body.Children.Add(left);
        var panel = new Border { Style = DS.Style("DS.Surface") }; Grid.SetColumn(panel, 2); body.Children.Add(panel);
        var p = new StackPanel(); panel.Child = p;
        p.Children.Add(new TextBlock { Text = "Trigger test", Style = DS.Style("DS.Heading"), Margin = new(0, 0, 0, 12) });
        p.Children.Add(Pills("side", ("Right trigger (RT)", true), ("Left trigger (LT)", false), ("Both", false)));
        p.Children.Add(new TextBlock { Text = "Response", Style = DS.Style("DS.Heading"), Margin = new(0, 10, 0, 12) });
        var w = Pills("mode", ("Default", false), ("Quick", true), ("Smooth", false), ("Fine", false)); ((RadioButton)w.Children[3]).IsEnabled = false; p.Children.Add(w);
        p.Children.Add(Row("Vibration strength", "Linked · local draft", new Slider { Style = DS.Style("DS.Slider"), Width = 220, Minimum = 0, Maximum = 100, Value = 75 }));
        p.Children.Add(Row("Reduced motion", "Calmer transitions", new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = false }));
        var updates = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = false }; p.Children.Add(Row("Check for updates", "GitHub releases · asks first", updates));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        var start = new Button { Content = "Start test", Style = DS.Style("DS.Primary") }; buttons.Children.Add(start);
        buttons.Children.Add(new Button { Content = "Export results", Style = DS.Style("DS.Secondary"), Margin = new(12, 0, 0, 0) });
        buttons.Children.Add(new Button { Content = "Apply", Style = DS.Style("DS.Primary"), IsEnabled = false, Margin = new(12, 0, 0, 0) });
        p.Children.Add(buttons);
        var footer = new DockPanel { Margin = new(0, 18, 0, 0) }; var chips = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(chips, Dock.Right);
        chips.Children.Add(new StatusChip("Offline", ChipKind.Warn) { Margin = new(0, 0, 10, 0) }); chips.Children.Add(new StatusChip("Simulation", ChipKind.Info));
        footer.Children.Add(chips); footer.Children.Add(GlyphHint.Footer(("A", "Select"), ("B", "Back"), ("LB/RB", "Tabs"), ("Y", "Try it")));
        Grid.SetRow(footer, 3); page.Children.Add(footer);
        root.Children.Add(page);
        var host = new PopupHost(root);
        var window = new Window { Width = 1600, Height = 960, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = Brushes.Black, Content = host, ShowInTaskbar = false };
        try
        {
            window.Show(); await Task.Delay(400);
            var idle = await ShowcaseReviewProbe.Run(2500);
            var probeHost = host.Show("Probe", "Frame-time probe", new TextBlock { Text = "Measuring popup open", Style = DS.Style("DS.Body") }, 700); var popupOpen = await ShowcaseReviewProbe.Run(600); host.Close(); var popupClose = await ShowcaseReviewProbe.Run(400); await probeHost;
            File.WriteAllText(Path.Combine(directory, "fps.json"), System.Text.Json.JsonSerializer.Serialize(new { idleGalaxyAndGallery = idle, popupOpen, popupClose }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            updates.IsChecked = true; await Task.Delay(300); start.Focus(); await Capture("01-gallery.png");
            var settings = new StackPanel();
            settings.Children.Add(Row("Update channel", "Stable releases from github.com/AmjadAAYD/x20ctl", Pills("ch", ("Stable", true), ("Preview", false))));
            settings.Children.Add(Row("Check automatically", "Never downloads or installs without asking", new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = true }));
            settings.Children.Add(Row("Replay intro", "Play the startup animation again", new Button { Content = "Replay", Style = DS.Style("DS.Secondary") }));
            var popup = host.Show("Settings", "X20CTL settings", settings, 820, new StatusChip("Up to date", ChipKind.Success), new Button { Content = "Done", Style = DS.Style("DS.Primary") });
            foreach (int ms in new[] { 0, 60, 120, 200, 360 }) { await Task.Delay(ms == 0 ? 15 : 60); await Capture($"02-popup-{ms:000}ms.png", 0); }
            await Task.Delay(300); await Capture("03-popup-open.png");
            host.Close(); await Task.Delay(80); await Capture("04-popup-closing.png", 0); await popup; await Capture("05-closed.png");
        }
        finally { window.Close(); }

        async Task Capture(string name, int delay = 120)
        {
            if (delay > 0) await Task.Delay(delay); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(host);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var s = File.Create(Path.Combine(directory, name)); enc.Save(s);
        }
    }
    private static WrapPanel Pills(string group, params (string Text, bool On)[] items)
    {
        var wrap = new WrapPanel(); string name = group + Guid.NewGuid().ToString("N");
        foreach (var (t, on) in items) wrap.Children.Add(new RadioButton { Content = t, Style = DS.Style("DS.Pill"), GroupName = name, IsChecked = on }); return wrap;
    }
    private static Border Row(string title, string detail, FrameworkElement trailing)
    {
        var g = new Grid(); g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        g.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = title, FontFamily = DS.Body, FontSize = 16, Foreground = DS.Brush("DS.Text") }, new TextBlock { Text = detail, Style = DS.Style("DS.Caption"), Margin = new(0, 2, 0, 0) } } });
        trailing.VerticalAlignment = VerticalAlignment.Center; trailing.Margin = new(16, 0, 0, 0); Grid.SetColumn(trailing, 1); g.Children.Add(trailing);
        return new Border { Style = DS.Style("DS.Row"), Child = g };
    }
}
