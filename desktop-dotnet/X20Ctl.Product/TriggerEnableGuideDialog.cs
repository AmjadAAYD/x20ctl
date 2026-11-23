using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>
/// Theme-native reminder explaining the one-time controller button press plus M + Back
/// shortcut required to enable trigger vibration on the EasySMX X20 Pro.
/// </summary>
internal static class TriggerEnableGuideDialog
{
    public static bool ShownThisSession { get; private set; }

    public static void Show(Window? owner, bool force = false)
    {
        if (ReviewSandbox.Active) return;
        if (!force && ShownThisSession) return;
        ShownThisSession = true;

        var win = new Window
        {
            Title = "Enable Controller Triggers",
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is { IsLoaded: true, IsVisible: true }
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = DS.Brush("DS.Deep"),
            Foreground = DS.Brush("DS.Text"),
            FontFamily = DS.Body,
        };
        if (owner is { IsLoaded: true, IsVisible: true })
            win.Owner = owner;

        var headline = new Border
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Accent"),
            BorderThickness = new(1),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 210)),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 0, 0, 12),
            Child = Kit.Text("Click any button to enable it, then hold M + Back", 14.5, "DS.AccentHi", FontWeights.Bold)
                .Also(t => t.TextWrapping = TextWrapping.Wrap),
        };

        var steps = new StackPanel { Margin = new(0, 0, 0, 14) };
        steps.Children.Add(Kit.Text(
            "1. After closing this popup, press any button on your controller so Windows activates trigger haptics.",
            12.5,
            "DS.Text").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 6); }));
        steps.Children.Add(Kit.Text(
            "2. Hold M + Back on your controller until it vibrates once to turn ON LT/RT trigger motors (required on 2.4 GHz dongle and USB).",
            12.5,
            "DS.Text").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 8); }));
        steps.Children.Add(Kit.Text(
            "Tip: For independent Lyrical Triggers + Musical Grips, switch the controller screen to Mode -> BT.",
            12,
            "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap));

        var ok = new Button
        {
            Content = "Got it",
            Style = DS.Style("DS.Primary"),
            Padding = new(20, 8, 20, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true,
        };
        ok.Click += (_, _) => win.Close();

        win.Content = new StackPanel
        {
            Margin = new(20),
            Children = { headline, steps, ok },
        };
        win.ShowDialog();
    }
}
