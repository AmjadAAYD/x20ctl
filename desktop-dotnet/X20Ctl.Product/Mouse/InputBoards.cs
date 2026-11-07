using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>The colours the Mouse and Keyboard pages share: mapped is green, selected is the accent blue, idle is grey.</summary>
static class BoardInk
{
    public static readonly Color Green = Kit.Green, Blue = Kit.Blue, Ice = Kit.Ice;
    public static SolidColorBrush Solid(byte a, Color c) => new(Color.FromArgb(a, c.R, c.G, c.B));
    public static readonly Brush Idle = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(235, 16, 26, 46)));
    public static readonly Brush IdleLine = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(70, 160, 190, 235)));
    public static readonly Brush Text = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(232, 240, 252)));
    public static readonly Brush Soft = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(170, 205, 220, 240)));
    public static readonly Brush MappedFill = Solid(70, Green), MappedLine = Solid(235, Green), SelectedFill = Solid(80, Blue), SelectedLine = new SolidColorBrush(Ice);
    /// <summary>A button with no chrome of its own: its content is the whole look.</summary>
    public static readonly ControlTemplate Bare = (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate TargetType=\"Button\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><ContentPresenter/></ControlTemplate>");
    /// <summary>A button's name small enough for a key cap's corner.</summary>
    public static string ShortName(string key) => key switch { "DPAD_UP" => "D↑", "DPAD_DOWN" => "D↓", "DPAD_LEFT" => "D←", "DPAD_RIGHT" => "D→", _ => InputName(key) };
    public static string InputName(string key) => key switch
    {
        "SELECT" => "View", "START" => "Menu", "HOME" => "Home", "DPAD_UP" => "D-pad ↑", "DPAD_DOWN" => "D-pad ↓", "DPAD_LEFT" => "D-pad ←", "DPAD_RIGHT" => "D-pad →", _ => key,
    };
}

/// <summary>
/// The controller with every button labelled from the side: a pill per button, joined to it by a thin line, saying what
/// it does. Mapped pills are green. Click a pill or the button itself to pick it; held buttons light up live.
/// </summary>
public sealed class ControllerBoard : Viewbox
{
    const double ArtLeft = 420, Width0 = 2376, Height0 = 1024;
    readonly Canvas canvas = new() { Width = Width0, Height = Height0 };
    readonly Dictionary<string, (Border Pill, TextBlock Name, TextBlock Value, Line Lead, Ellipse Dot, PhysicalHotspot? Hot)> parts = new();
    string? selected;
    public event Action<string>? Picked;
    public IReadOnlyCollection<string> Inputs => parts.Keys;

    public ControllerBoard(ControllerModel model, ControllerLayout? layout, IEnumerable<string> inputs, Action<Button> register)
    {
        Stretch = Stretch.Uniform; Child = canvas;
        var art = new PhotoController(model.Id) { IsHitTestVisible = false }; Canvas.SetLeft(art, ArtLeft); canvas.Children.Add(art);
        var regions = layout?.Controls.Where(r => r.View == "front").GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.First()) ?? new();
        var placed = new List<(string Key, Point At, bool Left, PhysicalHotspot? Hot)>();
        foreach (var key in inputs)
        {
            PhysicalHotspot? hot = null; Point at;
            if (regions.TryGetValue(key, out var region))
            {
                hot = new PhysicalHotspot(region, false) { ToolTip = BoardInk.InputName(key) };
                var p = LiveController.Place(model.Id, region); Canvas.SetLeft(hot, ArtLeft + p.X); Canvas.SetTop(hot, p.Y);
                string k = key; hot.Click += (_, _) => Pick(k); register(hot); canvas.Children.Add(hot);
                at = new(ArtLeft + p.X + hot.Width / 2, p.Y + hot.Height / 2);
            }
            else at = new(double.NaN, double.NaN);
            placed.Add((key, at, double.IsNaN(at.X) ? key is "LB" or "LT" or "L3" or "SELECT" or "DPAD_UP" or "DPAD_DOWN" or "DPAD_LEFT" or "DPAD_RIGHT" : at.X < ArtLeft + 768, hot));
        }
        // each side's pills go top to bottom in the order of their buttons, evenly spaced so they never overlap
        foreach (bool left in new[] { true, false })
        {
            var side = placed.Where(p => p.Left == left).OrderBy(p => double.IsNaN(p.At.Y) ? 2000 : p.At.Y).ToList();
            double step = side.Count <= 1 ? 0 : Math.Min(118, (Height0 - 120) / (side.Count - 1)), top = (Height0 - 76 - step * (side.Count - 1)) / 2;
            for (int i = 0; i < side.Count; i++) Add(side[i].Key, side[i].At, left, top + i * step, side[i].Hot, register);
        }
    }

    void Add(string key, Point at, bool left, double y, PhysicalHotspot? hot, Action<Button> register)
    {
        var name = new TextBlock { Text = BoardInk.InputName(key), FontFamily = DS.Display, FontSize = 34, FontWeight = FontWeights.SemiBold, Foreground = BoardInk.Text, MinWidth = 130, VerticalAlignment = VerticalAlignment.Center };
        var value = new TextBlock { FontFamily = DS.Display, FontSize = 32, Foreground = BoardInk.Soft, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, Margin = new(14, 0, 0, 0) };
        var pill = new Border { Width = 400, Height = 76, CornerRadius = new(38), BorderThickness = new(3), Padding = new(26, 0, 22, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { name, value } } };
        var button = new Button { Template = BoardInk.Bare, Content = pill, Cursor = Cursors.Hand, Focusable = true }; string k = key; button.Click += (_, _) => Pick(k); register(button);
        double x = left ? 0 : Width0 - pill.Width; Canvas.SetLeft(button, x); Canvas.SetTop(button, y); canvas.Children.Add(button);
        var lead = new Line { StrokeThickness = 3, IsHitTestVisible = false }; var dot = new Ellipse { Width = 18, Height = 18, IsHitTestVisible = false };
        if (!double.IsNaN(at.X))
        {
            lead.X1 = left ? pill.Width : Width0 - pill.Width; lead.Y1 = y + 38; lead.X2 = at.X; lead.Y2 = at.Y;
            Canvas.SetLeft(dot, at.X - 9); Canvas.SetTop(dot, at.Y - 9);
            canvas.Children.Insert(1, lead); canvas.Children.Add(dot); // over the art, so each line reaches its button
        }
        parts[key] = (pill, name, value, lead, dot, hot);
        Show(key, "", false);
    }

    void Pick(string key) { Select(key); Picked?.Invoke(key); }

    /// <summary>What a button does, shown in its pill. Mapped buttons are green.</summary>
    public void Show(string key, string text, bool mapped)
    {
        if (!parts.TryGetValue(key, out var p)) return;
        p.Value.Text = text.Length == 0 ? "—" : text; p.Value.Foreground = mapped ? BoardInk.MappedLine : BoardInk.Soft;
        Paint(key);
        p.Pill.Tag = mapped;
    }

    public void Select(string? key)
    {
        string? was = selected; selected = key;
        if (was != null) Paint(was);
        if (key != null) Paint(key);
        foreach (var (k, p) in parts) p.Hot?.Illuminate(k == key, false);
    }

    readonly HashSet<string> lit = new();
    /// <summary>Held buttons light up, so a press shows which pill it is.</summary>
    public void Held(IReadOnlySet<string> held)
    {
        foreach (var k in lit.Where(k => !held.Contains(k)).ToList()) { lit.Remove(k); Paint(k); }
        foreach (var k in held.Where(parts.ContainsKey)) if (lit.Add(k)) Paint(k);
    }

    void Paint(string key)
    {
        var p = parts[key];
        bool mapped = p.Value.Foreground == BoardInk.MappedLine, chosen = key == selected, down = lit.Contains(key);
        p.Pill.Background = down ? BoardInk.Solid(150, BoardInk.Blue) : chosen ? BoardInk.SelectedFill : mapped ? BoardInk.MappedFill : BoardInk.Idle;
        p.Pill.BorderBrush = chosen || down ? BoardInk.SelectedLine : mapped ? BoardInk.MappedLine : BoardInk.IdleLine;
        p.Pill.Effect = chosen || down ? new DropShadowEffect { Color = BoardInk.Blue, BlurRadius = 30, ShadowDepth = 0, Opacity = .8 } : null;
        p.Lead.Stroke = chosen || down ? BoardInk.SelectedLine : mapped ? BoardInk.Solid(190, BoardInk.Green) : BoardInk.IdleLine;
        p.Dot.Fill = chosen || down ? BoardInk.SelectedLine : mapped ? BoardInk.MappedLine : BoardInk.IdleLine;
    }
}

/// <summary>
/// A drawn mouse with its seven buttons: M1 left, M2 right, M3 the wheel, M4 and M5 on the side, M6 and M7 the wheel's
/// tilt. Each has a label off to the side saying which controller buttons send it; mapped ones turn green.
/// </summary>
public sealed class MouseDiagram : Viewbox
{
    public static readonly (string Id, string Label)[] Buttons =
    [
        ("LeftClick", "M1 · Left click"), ("RightClick", "M2 · Right click"), ("MiddleClick", "M3 · Wheel click"), ("Back", "M4 · Back"),
        ("Forward", "M5 · Forward"), ("TiltLeft", "M6 · Tilt left"), ("TiltRight", "M7 · Tilt right"),
    ];
    readonly Canvas canvas = new() { Width = 760, Height = 600 };
    readonly Dictionary<string, (Shape Part, Border Pill, TextBlock Who, Line Lead)> parts = new();
    readonly Path body;
    public event Action<string>? Picked;

    public MouseDiagram()
    {
        Stretch = Stretch.Uniform; Child = canvas;
        body = new Path
        {
            Data = Geometry.Parse("M380,46 C468,46 506,120 506,214 L506,410 C506,508 452,566 380,566 C308,566 254,508 254,410 L254,214 C254,120 292,46 380,46 Z"),
            Fill = new LinearGradientBrush(ThemeCatalog.R(Color.FromRgb(30, 42, 66)), ThemeCatalog.R(Color.FromRgb(12, 18, 32)), 90), Stroke = BoardInk.IdleLine, StrokeThickness = 2,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 10, Opacity = .55 },
        };
        canvas.Children.Add(body);
        canvas.Children.Add(new Path { Data = Geometry.Parse("M256,244 C320,256 440,256 504,244"), Stroke = BoardInk.IdleLine, StrokeThickness = 2 });
        Part("LeftClick", "M376,50 C300,54 258,126 258,214 L258,242 C300,250 340,254 376,254 Z", new(0, 70), new(312, 150), true);
        Part("RightClick", "M384,50 C460,54 502,126 502,214 L502,242 C460,250 420,254 384,254 Z", new(760, 70), new(448, 150), false);
        Part("MiddleClick", "M366,96 A14,14 0 0 1 394,96 L394,168 A14,14 0 0 1 366,168 Z", new(760, 150), new(380, 132), false);
        Part("TiltLeft", "M354,112 L338,132 L354,152 Z", new(0, 150), new(344, 132), true);
        Part("TiltRight", "M406,112 L422,132 L406,152 Z", new(760, 230), new(416, 132), false);
        Part("Forward", "M242,276 A10,10 0 0 1 252,266 L262,266 L262,326 L252,326 A10,10 0 0 1 242,316 Z", new(0, 240), new(248, 296), true);
        Part("Back", "M242,346 A10,10 0 0 1 252,336 L262,336 L262,396 L252,396 A10,10 0 0 1 242,386 Z", new(0, 340), new(248, 366), true);
    }

    void Part(string id, string data, Point pillAt, Point anchor, bool left)
    {
        var shape = new Path { Data = Geometry.Parse(data), StrokeThickness = 2, Cursor = Cursors.Hand };
        string label = Buttons.First(b => b.Id == id).Label;
        var who = new TextBlock { FontFamily = DS.Display, FontSize = 17, Foreground = BoardInk.Soft, TextTrimming = TextTrimming.CharacterEllipsis };
        var pill = new Border
        {
            Width = 214, CornerRadius = new(12), BorderThickness = new(1.5), Padding = new(14, 7, 12, 8), Cursor = Cursors.Hand,
            Child = new StackPanel { Children = { new TextBlock { Text = label, FontFamily = DS.Display, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = BoardInk.Text }, who } },
        };
        double x = left ? pillAt.X : pillAt.X - pill.Width; Canvas.SetLeft(pill, x); Canvas.SetTop(pill, pillAt.Y);
        var lead = new Line { X1 = left ? x + pill.Width : x, Y1 = pillAt.Y + 28, X2 = anchor.X, Y2 = anchor.Y, StrokeThickness = 1.5, IsHitTestVisible = false };
        canvas.Children.Insert(1, lead); canvas.Children.Add(shape); canvas.Children.Add(pill);
        shape.MouseLeftButtonUp += (_, _) => Picked?.Invoke(id); pill.MouseLeftButtonUp += (_, _) => Picked?.Invoke(id);
        parts[id] = (shape, pill, who, lead);
        Show(id, []);
    }

    readonly HashSet<string> pressed = new();
    /// <summary>Which controller buttons send each mouse button; an empty list means it isn't mapped.</summary>
    public void Show(string id, IReadOnlyList<string> from)
    {
        if (!parts.TryGetValue(id, out var p)) return;
        p.Who.Text = from.Count == 0 ? "Not mapped" : "From " + string.Join(", ", from);
        p.Pill.Tag = from.Count > 0; Paint(id);
    }
    /// <summary>The mouse buttons being sent right now light up.</summary>
    public void Pressed(IEnumerable<string> ids)
    {
        var now = ids.ToHashSet();
        foreach (var id in parts.Keys) { bool on = now.Contains(id); if (on == pressed.Contains(id)) continue; if (on) pressed.Add(id); else pressed.Remove(id); Paint(id); }
    }
    void Paint(string id)
    {
        var p = parts[id]; bool mapped = p.Pill.Tag is true, down = pressed.Contains(id);
        p.Part.Fill = down ? BoardInk.Solid(220, BoardInk.Ice) : mapped ? BoardInk.Solid(150, BoardInk.Green) : BoardInk.Solid(255, ThemeCatalog.R(Color.FromRgb(38, 52, 80)));
        p.Part.Stroke = down ? BoardInk.SelectedLine : mapped ? BoardInk.MappedLine : BoardInk.IdleLine;
        p.Part.Effect = mapped || down ? new DropShadowEffect { Color = down ? BoardInk.Ice : BoardInk.Green, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 } : null;
        p.Pill.Background = mapped ? BoardInk.MappedFill : BoardInk.Idle; p.Pill.BorderBrush = down ? BoardInk.SelectedLine : mapped ? BoardInk.MappedLine : BoardInk.IdleLine;
        p.Who.Foreground = mapped ? BoardInk.MappedLine : BoardInk.Soft;
        p.Lead.Stroke = mapped ? BoardInk.Solid(200, BoardInk.Green) : BoardInk.IdleLine;
    }
}

/// <summary>
/// A full keyboard: function row, numbers, letters, modifiers, the navigation keys, arrows and the number pad. A key a
/// controller button sends is green and carries that button's name.
/// </summary>
public sealed class KeyboardDiagram : Viewbox
{
    const double Unit = 50, Gap = 5;
    readonly Canvas canvas = new();
    readonly Dictionary<string, (Border Face, TextBlock Badge)> keys = new();
    public event Action<string>? Picked;

    public KeyboardDiagram(Action<Button> register)
    {
        Stretch = Stretch.Uniform; Child = canvas;
        canvas.Width = KeyCatalog.Keys.Max(k => k.Col + k.Width) * Unit; canvas.Height = KeyCatalog.Keys.Max(k => k.Row + k.Height) * Unit;
        foreach (var key in KeyCatalog.Keys)
        {
            var label = new TextBlock { Text = key.Label, FontFamily = DS.Display, FontSize = key.Label.Length > 3 ? 12.5 : 16, FontWeight = FontWeights.SemiBold, Foreground = BoardInk.Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 8, 0, 0) };
            var badge = new TextBlock { FontFamily = DS.Display, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(8, 30, 20)), Margin = new(4, 2, 4, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
            var face = new Border
            {
                Width = key.Width * Unit - Gap, Height = key.Height * Unit - Gap, CornerRadius = new(8), BorderThickness = new(1.5),
                Child = new Grid { Children = { label, new Border { Child = badge, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, CornerRadius = new(4), Margin = new(3) } } },
            };
            var button = new Button { Template = BoardInk.Bare, Content = face, Cursor = Cursors.Hand, ToolTip = key.Name ?? key.Label }; string id = key.Id;
            button.Click += (_, _) => Picked?.Invoke(id); register(button);
            button.MouseEnter += (_, _) => face.BorderBrush = BoardInk.SelectedLine; button.MouseLeave += (_, _) => Paint(id);
            Canvas.SetLeft(button, key.Col * Unit); Canvas.SetTop(button, key.Row * Unit); canvas.Children.Add(button);
            keys[id] = (face, badge);
            Paint(id);
        }
    }

    /// <summary>Marks a key as sent by these controller buttons (none: not mapped).</summary>
    public void Show(string id, IReadOnlyList<string> from)
    {
        if (!keys.TryGetValue(id, out var k)) return;
        k.Badge.Text = string.Join(" ", from); k.Badge.Visibility = from.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ((Border)k.Badge.Parent).Background = from.Count > 0 ? new SolidColorBrush(BoardInk.Green) : null;
        k.Face.Tag = from.Count > 0; Paint(id);
    }
    public void ClearAll() { foreach (var id in keys.Keys) Show(id, []); }
    void Paint(string id)
    {
        var (face, _) = keys[id]; bool mapped = face.Tag is true;
        face.Background = mapped ? BoardInk.Solid(110, BoardInk.Green) : new LinearGradientBrush(ThemeCatalog.R(Color.FromRgb(34, 46, 70)), ThemeCatalog.R(Color.FromRgb(20, 28, 46)), 90);
        face.BorderBrush = mapped ? BoardInk.MappedLine : BoardInk.IdleLine;
        face.Effect = mapped ? new DropShadowEffect { Color = BoardInk.Green, BlurRadius = 14, ShadowDepth = 0, Opacity = .7 } : null;
    }
}
