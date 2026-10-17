using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>The palette and small builders every page shares: greys, navy, one blue accent.</summary>
internal static class Ui
{
    public static readonly IBrush Back = Brush("#0E1420"), Card = Brush("#17202F"), Raised = Brush("#1F2A3D"), Line = Brush("#26334A");
    public static readonly IBrush Accent = Brush("#3D8BFF"), Quiet = Brush("#8A97AD"), Text = Brush("#F2F5FA"), Warn = Brush("#F5B23D");
    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static TextBlock Label(string text, double size = 14, IBrush? color = null, FontWeight weight = FontWeight.Normal) =>
        new() { Text = text, FontSize = size, Foreground = color ?? Text, FontWeight = weight, TextWrapping = TextWrapping.Wrap };

    public static Border Panel(Control child, double pad = 18) => new() { Background = Card, CornerRadius = new CornerRadius(14), Padding = new Thickness(pad), BorderBrush = Line, BorderThickness = new Thickness(1), Child = child };

    public static Button Chip(string text, bool on, Action click)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(9), Background = on ? Accent : Raised, Foreground = Text, BorderThickness = new Thickness(0), FontSize = 13 };
        b.Click += (_, _) => click(); return b;
    }

    public static StackPanel Column(params Control[] children) { var s = new StackPanel { Spacing = 12 }; foreach (var c in children) s.Children.Add(c); return s; }
    public static WrapPanel Row(params Control[] children) { var s = new WrapPanel { Orientation = Orientation.Horizontal }; foreach (var c in children) { c.Margin = new Thickness(0, 0, 8, 8); s.Children.Add(c); } return s; }

    public static Bitmap? Art(string modelId, string file)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri($"avares://x20ctl-studio/Assets/{modelId}/native/{file}.png"))); }
        catch (Exception) { return null; }
    }
}

/// <summary>One controller's drafts, shared by every Studio page. PC mode is what applies them on Linux, so every model is editable.</summary>
internal sealed class StudioState
{
    public ControllerModel Model { get; }
    public ButtonDraft Buttons { get; }
    public CurveDraft Curves { get; }
    public MacroDraft Macros { get; }
    public int VibrationPreview { get; set; } = VibrationDraft.DefaultStrength;
    public StudioState(ControllerModel model)
    {
        Model = model;
        Buttons = new(model.Id) { PcMode = true };
        Curves = new(model.Id) { PcMode = true };
        Macros = new(model.Id) { PcMode = true };
    }
}
