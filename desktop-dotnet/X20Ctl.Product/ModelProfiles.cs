using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>
/// What each controller is, for the placeholder pages (owner direction 10 Oct 2026: Curves, Macros, Vibration,
/// Device and Setups for every controller, no exception). Facts come from the catalog plus the owner's research notes;
/// anything the sources disagree on, or never state, says so instead of picking a number. Only the X20 is verified,
/// so every other model's pages are read-only previews.
/// </summary>
public sealed record ModelProfile(string Id, string Sticks, string Triggers, string[] Paddles, string Motors, string Polling, string Motion, string Battery, int BatteryLevels, string Extras)
{
    public bool Verified => Id == "x20";
    /// <summary>One line naming what is known, for banners and the Device page.</summary>
    public string Summary => $"{Sticks} sticks · {Triggers} triggers · {(Paddles.Length == 0 ? "no back buttons" : string.Join("/", Paddles))} · {Motors}";
}

public static class ModelProfiles
{
    private static readonly Dictionary<string, ModelProfile> all = new()
    {
        ["x20"] = new("x20", "Hall", "Hall", ["M1", "M2", "M3", "M4"], "2 grip motors", "Not published", "6-axis gyro", "4-level report", 4, "RGB stick rings · KeyLinker app"),
        ["x20_pro"] = new("x20_pro", "TMR, adjustable tension", "Hall / microswitch", ["M1", "M2", "M3", "M4", "M5", "M6"], "4 motors (grips + triggers), instant brake", "Not published", "Gyro", "1000–1200 mAh (sources disagree)", 0, "Smart display · RGB"),
        ["x05"] = new("x05", "Hall", "Hall", ["M1", "M2"], "2 grip motors", "1000 / 250 / 125 Hz", "None", "Unknown", 0, "M1/M2 back buttons: sources disagree"),
        ["x05_pro"] = new("x05_pro", "Hall", "Dual-stage Hall / microswitch", ["M1", "M2"], "2 grip motors", "1000 Hz wired and 2.4G", "Unconfirmed", "Unknown", 0, "RGB"),
        ["x10"] = new("x10", "Hall", "Hall", ["M1", "M2"], "2 grip motors", "Not published", "6-axis (Switch mode)", "Unknown", 0, "Mechanical ABXY · QMacro"),
        ["d10"] = new("d10", "TMR", "Hall / microswitch", ["M1", "M2"], "2 grip motors, 4 strength levels", "1000 Hz wired and 2.4G", "Gyro", "Unknown", 0, "Charging dock · no KeyLinker · receiver 2345:E062"),
        ["dune"] = new("dune", "TMR", "Unknown", ["M1", "M2", "M3", "M4"], "4 motors (grips + triggers)", "8000 Hz (8K edition)", "Unknown", "Unknown", 0, "1.3 in. LCD · charging dock · M1/M2 on the top, M3/M4 on the back"),
        ["x15"] = new("x15", "Hall", "Hall", ["M1", "M2"], "2 grip motors", "1000 / 250 / 125 Hz", "Unconfirmed", "Unknown", 0, "KeyLinker app · receiver 1A34:F517"),
    };
    public static ModelProfile For(string id) => all.TryGetValue(id, out var p) ? p : new(id, "Unknown", "Unknown", [], "Unknown", "Unknown", "Unknown", "Unknown", 0, "");

    /// <summary>Puts the banner on top of a page grid (a new Auto row above everything already there); verified models wear nothing.</summary>
    public static void Wear(Grid page, string id, string name)
    {
        if (For(id).Verified) return;
        if (page.RowDefinitions.Count == 0) page.RowDefinitions.Add(new());
        foreach (UIElement child in page.Children) Grid.SetRow(child, Grid.GetRow(child) + 1);
        page.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
        var banner = Banner(id, name); Grid.SetColumnSpan(banner, Math.Max(1, page.ColumnDefinitions.Count)); page.Children.Add(banner);
    }
    /// <summary>The strip a placeholder page wears: it says the page is a preview and what this controller has.</summary>
    public static Border Banner(string id, string page)
    {
        var p = For(id);
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13.5, Foreground = DS.Brush("DS.TextSoft") };
        text.Inlines.Add(new System.Windows.Documents.Run($"{page} preview · ") { Foreground = DS.Brush("DS.Text"), FontWeight = FontWeights.SemiBold });
        text.Inlines.Add(new System.Windows.Documents.Run($"Read-only until this controller's settings are verified.   {p.Summary}"));
        return new Border
        {
            CornerRadius = new(12), Padding = new(14, 9, 14, 9), Margin = new(0, 0, 0, 10), BorderThickness = new(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 125, 182, 255)), Background = new SolidColorBrush(Color.FromArgb(70, 30, 60, 130)),
            Child = new DockPanel { Children = { new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = DS.Brush("DS.AccentHi"), Margin = new(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center }, text } }
        };
    }
}
