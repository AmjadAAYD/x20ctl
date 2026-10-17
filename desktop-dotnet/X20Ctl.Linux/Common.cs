using Path = Avalonia.Controls.Shapes.Path;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

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
    readonly Dictionary<CurveChannel, Zone.PcMode.StickTuning> tunes = Enum.GetValues<CurveChannel>().ToDictionary(c => c, _ => new Zone.PcMode.StickTuning());
    public Zone.PcMode.StickTuning Tune(CurveChannel c) => tunes[c];
    public void ResetTune(CurveChannel c) => tunes[c] = new();
    public void ResetTunes() { foreach (var c in tunes.Keys.ToList()) tunes[c] = new(); }
    public void SetInner(CurveChannel c, double v)
    {
        var t = tunes[c]; var cur = Curves[c]; int inner = (int)Math.Round(v); if (inner >= cur.Outer) inner = cur.Outer - 1;
        try { Curves.Set(c, cur with { Inner = inner }); t.Inner = inner; } catch (ArgumentException) { }
    }
    public void SetOuter(CurveChannel c, double v)
    {
        var t = tunes[c]; var cur = Curves[c]; int outer = (int)Math.Round(v); if (outer <= cur.Inner) outer = cur.Inner + 1;
        try { Curves.Set(c, cur with { Outer = outer }); t.Outer = outer; } catch (ArgumentException) { }
    }
    public int Changes => Buttons.UnsentChanges + Curves.ModifiedChannels + Macros.ModifiedSlots;
    public void ResetAll() { Buttons.ResetAll(); Curves.ResetAll(); ResetTunes(); foreach (string s in Macros.SlotNames) Macros.Clear(s); }
}

/// <summary>Names the way the app says them: View and Menu for Select and Start, arrows for the D-pad.</summary>
internal static class Names
{
    public static string Of(string key) => key switch
    {
        "SELECT" => "View", "START" => "Menu", "NONE" => "Disabled", "HOME" => "Guide", "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→",
        "LSTICK_ANALOG" => "Left stick", "RSTICK_ANALOG" => "Right stick", _ => key
    };
    public static string Long(string key) => key switch
    {
        "A" or "B" or "X" or "Y" => key + " button", "LB" => "Left bumper", "RB" => "Right bumper", "LT" => "Left trigger", "RT" => "Right trigger", "L3" => "Left stick click", "R3" => "Right stick click",
        "DPAD_UP" => "D-pad up", "DPAD_DOWN" => "D-pad down", "DPAD_LEFT" => "D-pad left", "DPAD_RIGHT" => "D-pad right", "SELECT" => "View", "START" => "Menu", "NONE" => "Disabled", _ => key
    };
    public static string? FaceColour(string key) => key switch { "A" => "#34D399", "B" => "#FF5C7A", "X" => "#3D8BFF", "Y" => "#F5B45B", _ => null };
}

/// <summary>Pieces several pages share: the draft strip, the notice bar, the round key caps.</summary>
internal static class Parts
{
    public static Control Draft(StudioState state, Action? reset = null, bool withReset = true)
    {
        var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = T.Warn, VerticalAlignment = VerticalAlignment.Center };
        var status = T.Line(20, T.Label("Working Draft", 22, null, FontWeight.SemiBold), T.Line(6, dot, T.Label("No input", 15, T.Warn)), T.Label("PC mode", 15, T.Soft));
        status.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { ColumnDefinitions = new("*,Auto") }; row.Children.Add(status);
        if (withReset) { var r = new Pill("Reset draft", Pill.Kinds.Ghost, () => { state.ResetAll(); reset?.Invoke(); }) { Padding = new Thickness(26, 12) }; Grid.SetColumn(r, 1); row.Children.Add(r); }
        return row;
    }

    public static Control Notice(string bold, string rest)
    {
        var icon = new Path { Data = Geometry.Parse("M2,3 H16 V12 H2 Z M0,14 H18"), Stroke = T.AccentHi, StrokeThickness = 1.4, Width = 18, Height = 15, Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock { FontSize = 13.5, Foreground = T.Soft, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, FontFamily = T.Font };
        text.Inlines!.Add(new Avalonia.Controls.Documents.Run(bold) { FontWeight = FontWeight.SemiBold, Foreground = T.Text });
        text.Inlines.Add(new Avalonia.Controls.Documents.Run(" · " + rest));
        return new Border { Background = T.B("#4D0B1430"), BorderBrush = T.B("#334F7FD0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 11), Child = T.Line(14, icon, text) };
    }

    /// <summary>A round key cap. <paramref name="ring"/> colours the face buttons; <paramref name="current"/> lights the one the selected control sends.</summary>
    public static Border Cap(string text, double size, string? ring, bool current = false, bool selected = false, double font = 0)
    {
        IBrush edge = ring != null ? T.B(ring) : T.B("#5C6F95");
        var fill = current ? new RadialGradientBrush { GradientOrigin = new(.35, .3, RelativeUnit.Relative), GradientStops = { new(Color.Parse("#5AA6FF"), 0), new(Color.Parse("#1E5FD0"), 1) } }
            : (IBrush)new RadialGradientBrush { GradientOrigin = new(.4, .3, RelativeUnit.Relative), GradientStops = { new(Color.Parse("#2D4468"), 0), new(Color.Parse("#14233F"), 1) } };
        var cap = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = fill, BorderBrush = current ? T.B("#9CCBFF") : edge, BorderThickness = new Thickness(selected || current ? 2.6 : 1.8),
            Child = T.Label(text, font > 0 ? font : size * .36, ring != null && !current ? T.B(ring) : T.Text, FontWeight.SemiBold) is var l ? Center(l) : null,
            BoxShadow = current ? new BoxShadows(new BoxShadow { Blur = 22, Color = Color.Parse("#AA3D8BFF") }) : default
        };
        return cap;
    }

    /// <summary>A rounded key (shoulders, system, special).</summary>
    public static Border Key(string text, double width, double height, bool current = false, double font = 15, string? icon = null)
    {
        var fill = current ? T.PrimaryFill : T.Vertical("#2A4468", "#14233F");
        var label = T.Label(text, font, T.Text, FontWeight.SemiBold); label.TextWrapping = TextWrapping.NoWrap;
        Control content = icon == null ? Center(label) : Center(T.Line(8, T.Label(icon, font, T.Soft), label));
        return new Border
        {
            Width = width, Height = height, CornerRadius = new CornerRadius(height * .32), Background = fill, BorderBrush = current ? T.B("#9CCBFF") : T.B("#33FFFFFF"), BorderThickness = new Thickness(1.4), Child = content,
            BoxShadow = current ? new BoxShadows(new BoxShadow { Blur = 20, Color = Color.Parse("#AA3D8BFF") }) : default
        };
    }

    public static Control Center(Control c) { c.HorizontalAlignment = HorizontalAlignment.Center; c.VerticalAlignment = VerticalAlignment.Center; return c; }
    public static TextBlock Caps(string text) { var t = T.Label(text, 11.5, T.Soft, FontWeight.SemiBold); t.LetterSpacing = 1.6; return t; }
    public static Control Heading(string title, string? sub = null, double size = 21)
    {
        var s = T.Stack(2, T.Label(title, size, null, FontWeight.SemiBold)); if (sub != null) s.Children.Add(T.Label(sub, 13.5, T.Soft)); return s;
    }
    public static Control Tile(string label, string value)
    {
        var inner = T.Stack(3, T.Label(label, 12.5, T.Muted), T.Label(value, 17, T.Text));
        return new Border { Background = T.B("#33FFFFFF") is var _ ? T.B("#240B1430") : null, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 11), Child = inner, BorderBrush = T.B("#10FFFFFF"), BorderThickness = new Thickness(1) };
    }
}
