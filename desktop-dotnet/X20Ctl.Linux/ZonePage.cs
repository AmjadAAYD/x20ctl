using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Player slots: the selected player's card grows into the hero spot on the left, the other three wait on the right.</summary>
internal sealed class ZonePage : UserControl
{
    public ZonePage(Session session, Action rebuild, Action enter, Action<int> choose, Action<int> remove)
    {
        var grid = new Grid { Margin = new Thickness(60, 30, 60, 26), RowDefinitions = new("Auto,*"), ColumnDefinitions = new("2.08*,1*") };
        var title = T.Stack(2, T.Label("Player slots", 46, null, FontWeight.Bold), T.Label("Choose a player to set up its controller", 17, T.Soft));
        Grid.SetColumnSpan(title, 2); grid.Children.Add(title);

        var cards = new Grid { RowDefinitions = new("*,*,*"), Margin = new Thickness(0, 28, 0, 0) };
        Grid.SetRow(cards, 1); Grid.SetColumnSpan(cards, 2);
        var inner = new Grid { ColumnDefinitions = new("2.08*,1*") };
        cards.Children.Clear(); inner.RowDefinitions = new("*,*,*");
        Grid.SetRow(inner, 1); Grid.SetColumnSpan(inner, 2); inner.Margin = new Thickness(0, 28, 0, 0);

        var hero = Hero(session, session.Selected, enter, choose, remove);
        Grid.SetRowSpan(hero, 3); inner.Children.Add(hero);
        int row = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == session.Selected) continue;
            int slot = i; var card = Side(session, slot, () => { session.Selected = slot; rebuild(); }, choose);
            card.Margin = new Thickness(26, row == 0 ? 0 : 18, 0, 0); Grid.SetColumn(card, 1); Grid.SetRow(card, row++); inner.Children.Add(card);
        }
        grid.Children.Add(inner);
        Content = grid;
    }

    static Control Hero(Session s, int slot, Action enter, Action<int> choose, Action<int> remove)
    {
        var model = s.Players[slot];
        var g = new Grid { RowDefinitions = new("Auto,*,Auto,Auto,Auto") };
        g.Children.Add(T.Label("Player " + (slot + 1), 25, null, FontWeight.Bold));
        if (model != null)
        {
            var art = new ControllerArt(model.Id) { Margin = new Thickness(10, 4, 10, 4) }; Grid.SetRow(art, 1); g.Children.Add(art);
            var name = T.Label(model.Name, 36, null, FontWeight.SemiBold); name.Margin = new Thickness(2, 0, 0, 8); Grid.SetRow(name, 2); g.Children.Add(name);
            var status = T.StatusChip("Not connected", T.Warn); status.Margin = new Thickness(0, 0, 0, 20); Grid.SetRow(status, 3); g.Children.Add(status);
            var buttons = new Grid { ColumnDefinitions = new("Auto,*,Auto,Auto") };
            buttons.Children.Add(new Pill("Switch Controller", Pill.Kinds.Ghost, () => choose(slot)) { Padding = new Thickness(24, 12) });
            var rm = new Pill("Remove", Pill.Kinds.Ghost, () => remove(slot)) { Padding = new Thickness(26, 12) }; Grid.SetColumn(rm, 2); rm.Margin = new Thickness(0, 0, 14, 0); buttons.Children.Add(rm);
            var go = new Pill("Enter Studio", Pill.Kinds.Primary, enter) { Padding = new Thickness(26, 12) }; Grid.SetColumn(go, 3); buttons.Children.Add(go);
            Grid.SetRow(buttons, 4); g.Children.Add(buttons);
        }
        else
        {
            var empty = T.Stack(16, Silhouette(1.7), new Pill("+ Add controller", Pill.Kinds.Primary, () => choose(slot)) { Padding = new Thickness(26, 12), HorizontalAlignment = HorizontalAlignment.Center });
            empty.VerticalAlignment = VerticalAlignment.Center; empty.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(empty, 1); g.Children.Add(empty);
        }
        var card = T.Card(g, 26, 20, raised: true); card.BorderBrush = T.B("#7FA5E0FF"); card.BorderThickness = new Thickness(1.6); return card;
    }

    static Control Side(Session s, int slot, Action select, Action<int> choose)
    {
        var model = s.Players[slot];
        var g = new Grid { RowDefinitions = new("Auto,*") };
        g.Children.Add(T.Label("Player " + (slot + 1), 17, null, FontWeight.Bold));
        Control mid;
        if (model != null)
        {
            var art = new ControllerArt(model.Id, shadow: false) { Margin = new Thickness(0, 4, 0, 26) };
            var name = T.Label(model.Name, 16, null, FontWeight.SemiBold); name.VerticalAlignment = VerticalAlignment.Bottom;
            var host = new Grid(); host.Children.Add(art); host.Children.Add(name); mid = host;
        }
        else
        {
            mid = T.Stack(12, Silhouette(1), new Pill("+ Add controller", Pill.Kinds.Primary, () => choose(slot)) { HorizontalAlignment = HorizontalAlignment.Center });
            mid.VerticalAlignment = VerticalAlignment.Center; mid.HorizontalAlignment = HorizontalAlignment.Center;
        }
        Grid.SetRow(mid, 1); g.Children.Add(mid);
        var card = T.Card(g, 14, 16); card.Cursor = new Cursor(StandardCursorType.Hand);
        if (model != null) card.PointerPressed += (_, _) => select();
        return card;
    }

    /// <summary>An empty slot: a pad outline with a plus in it.</summary>
    public static Control Silhouette(double scale)
    {
        var pad = new Path
        {
            Data = Geometry.Parse("M14,30 C12,10 24,3 44,3 L70,3 C90,3 102,10 100,30 C99,46 93,60 84,60 C75,60 73,48 64,44 L50,44 C41,48 39,60 30,60 C21,60 15,46 14,30 Z"),
            Stroke = T.B("#8FA4C8"), StrokeThickness = 1.8, Width = 114, Height = 64, Stretch = Stretch.None
        };
        var dots = T.Line(5, Enumerable.Range(0, 3).Select(_ => (Control)new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), Background = T.B("#8FA4C8") }).ToArray());
        dots.HorizontalAlignment = HorizontalAlignment.Center; dots.VerticalAlignment = VerticalAlignment.Top; dots.Margin = new Thickness(0, 12, 0, 0);
        var plus = T.Label("+", 22, T.Text); plus.HorizontalAlignment = HorizontalAlignment.Center; plus.VerticalAlignment = VerticalAlignment.Center; plus.Margin = new Thickness(0, 6, 0, 0);
        var g = new Grid { Width = 114, Height = 64 }; g.Children.Add(pad); g.Children.Add(dots); g.Children.Add(plus);
        return new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = g, HorizontalAlignment = HorizontalAlignment.Center };
    }
}

/// <summary>Which controller is this? Eight model cards; picking one sets the layout and tools for that player.</summary>
internal sealed class ChooserPage : UserControl
{
    public ChooserPage(Session s, int slot, Control back, Action<ControllerModel> pick)
    {
        var facts = ReadFacts();
        var top = new Grid { ColumnDefinitions = new("*,Auto") };
        var badge = new Border { Background = T.B("#4D5F7F"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6), HorizontalAlignment = HorizontalAlignment.Left, Child = T.Label("PLAYER " + (slot + 1), 17, null, FontWeight.Medium) };
        var heading = T.Stack(8, badge, T.Label("Which controller is this?", 40, null, FontWeight.Bold), T.Label("Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.", 16.5, T.Soft));
        top.Children.Add(heading); back.VerticalAlignment = VerticalAlignment.Top; back.Margin = new Thickness(0, 28, 0, 0); Grid.SetColumn(back, 1); top.Children.Add(back);
        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 22, 0, 0) };
        foreach (var model in s.Catalog.Models)
        {
            var m = model; facts.TryGetValue(m.Id, out var f);
            var art = new ControllerArt(m.Id, shadow: false) { Height = 190 };
            var support = (f?.Support ?? "Not available yet") is var sup ? sup : "";
            IBrush dot = support.StartsWith("Full") ? T.Accent : T.B("#7D8AA6");
            var line = T.Line(7, new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = dot, VerticalAlignment = VerticalAlignment.Center }, T.Label(support, 13, support.StartsWith("Full") ? T.AccentHi : T.Soft));
            var col = T.Stack(7, art, T.Label(m.Name, 21, null, FontWeight.SemiBold), T.Label(f?.Description ?? "", 13, T.Soft), line);
            var card = T.Card(col, 16, 14); card.Margin = new Thickness(0, 0, 14, 14); card.Cursor = new Cursor(StandardCursorType.Hand);
            card.PointerEntered += (_, _) => card.BorderBrush = T.B("#66AFC8FF"); card.PointerExited += (_, _) => card.BorderBrush = (s.Players[slot]?.Id == m.Id) ? Brushes.White : T.Hairline;
            if (s.Players[slot]?.Id == m.Id) { card.BorderBrush = Brushes.White; card.BorderThickness = new Thickness(2); }
            card.PointerPressed += (_, _) => pick(m);
            grid.Children.Add(card);
        }
        Content = new ScrollViewer { Content = T.Stack(0, top, grid), Padding = new Thickness(0), Margin = new Thickness(60, 20, 46, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    sealed record Facts(string Description, string Support);
    static Dictionary<string, Facts> ReadFacts()
    {
        var facts = new Dictionary<string, Facts>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string id = item.GetProperty("id").GetString()!; var parts = new List<string>();
                if (item.TryGetProperty("hardware", out var hw))
                {
                    string? Text(string key) => hw.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    bool Has(string key) => hw.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
                    if (Text("sticks") is string st) parts.Add($"{st} sticks");
                    if (Text("triggers") is string tr) parts.Add($"{tr} triggers");
                    if (Has("rgb")) parts.Add("RGB");
                    if (Has("display")) parts.Add("screen");
                    if (Has("triggerHaptics")) parts.Add("trigger haptics");
                }
                if (item.TryGetProperty("macroSlots", out var slots) && slots.GetArrayLength() > 0) parts.Add($"{slots.GetArrayLength()} macro paddles");
                string availability = item.TryGetProperty("availability", out var a) ? a.GetString() ?? "" : "";
                string support = availability switch { "configuration" => "Full configuration", "preview" => "Preview · layout and art", "input_experimental" => "Input testing · experimental", _ => "Not available yet" };
                facts[id] = new(string.Join(" · ", parts), support);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return facts;
    }
}
