using System.IO;
using Path = System.Windows.Shapes.Path;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>Building blocks of the console screens, kept close to the X20CTL scanner concept art:
/// glass panels, title bar with a status pill, LB/RB tabs, Apple-style segmented control, choice cards,
/// paired label/field rows, interface rows, identifier cards, status hero, note box, action row,
/// stick and trigger readouts. Sizes follow the concepts' proportions.</summary>
internal static class Kit
{
    public static readonly Color Ice = Color.FromRgb(125, 182, 255), Blue = Color.FromRgb(61, 139, 255), Line = Color.FromArgb(46, 140, 175, 255), Green = Color.FromRgb(52, 211, 153);
    static Brush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Brush B(byte a, byte r, byte g, byte b) => B(Color.FromArgb(a, r, g, b));
    static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static TextBlock Text(string s, double size = 14.5, string brush = "DS.Text", FontWeight? weight = null) => new() { Text = s, FontFamily = DS.Display, FontSize = size, Foreground = DS.Brush(brush), FontWeight = weight ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public static TextBlock Icon(string glyph, double size = 16, string brush = "DS.TextSoft") => new() { Text = glyph, FontFamily = Icons, FontSize = size, Foreground = DS.Brush(brush), VerticalAlignment = VerticalAlignment.Center };
    static TextBlock Center(this TextBlock t) { t.HorizontalAlignment = HorizontalAlignment.Center; return t; }
    static Border Box(UIElement child, Thickness pad, double radius = 10, byte fill = 16, byte line = 38) => new() { CornerRadius = new(radius), BorderThickness = new(1), BorderBrush = B(line, 255, 255, 255), Background = B(fill, 255, 255, 255), Padding = pad, Child = child };

    /// <summary>Large rounded glass panel: bold title, optional grey subtitle, optional blue accent on the right.</summary>
    public static Border Panel(string? title, string? subtitle, string? accent, UIElement content, Thickness? pad = null)
    {
        var dock = new DockPanel();
        if (title != null)
        {
            var head = new DockPanel { Margin = new(0, 0, 0, 14) };
            if (accent != null) { var a = Text(accent, 13.5, "DS.AccentHi"); a.VerticalAlignment = VerticalAlignment.Top; a.Margin = new(0, 4, 0, 0); DockPanel.SetDock(a, Dock.Right); head.Children.Add(a); }
            var t = new StackPanel { Children = { Text(title, 18.5, "DS.Text", FontWeights.SemiBold) } };
            if (subtitle != null) t.Children.Add(Text(subtitle, 14, "DS.TextSoft"));
            head.Children.Add(t); DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head);
        }
        dock.Children.Add(content);
        return new Border
        {
            CornerRadius = new(16), BorderThickness = new(1), Padding = pad ?? new(20, 16, 20, 16), Child = dock,
            Background = new LinearGradientBrush(Color.FromArgb(205, 12, 22, 50), Color.FromArgb(190, 7, 13, 32), 90),
            BorderBrush = new LinearGradientBrush(Color.FromArgb(105, 120, 170, 255), Color.FromArgb(30, 120, 170, 255), 90)
        };
    }

    public static Grid TitleBar(string subtitle, StatusChip status)
    {
        var g = new Grid { Height = 56 }; g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var tile = new Border { Width = 40, Height = 40, CornerRadius = new(10), Background = B(255, 12, 16, 28), BorderBrush = B(60, 255, 255, 255), BorderThickness = new(1), Child = Icon("", 20, "DS.Text").Center() };
        var name = Text("X20CTL", 22, "DS.Text", FontWeights.Bold); name.Margin = new(18, 0, 12, 0);
        g.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { tile, name, Text(subtitle, 17, "DS.TextSoft") } });
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        status.Margin = new(0, 0, 24, 0); right.Children.Add(status);
        foreach (string glyph in new[] { "", "", "" }) { var i = Icon(glyph, 13); i.Margin = new(20, 0, 4, 0); right.Children.Add(i); }
        Grid.SetColumn(right, 1); g.Children.Add(right);
        return g;
    }

    public static StackPanel Tabs(string selected, params string[] tabs)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 46 };
        row.Children.Add(Bumper("LB", new(0, 0, 30, 0)));
        string group = "tabs" + Guid.NewGuid().ToString("N");
        foreach (var t in tabs) row.Children.Add(new RadioButton { Content = t, Style = DS.Style("DS.Tab"), GroupName = group, IsChecked = t == selected, Margin = new(0, 0, 36, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Bumper("RB", new(0)));
        return row;
    }
    static Border Bumper(string label, Thickness margin) => new() { Width = 46, Height = 28, CornerRadius = new(14), BorderBrush = B(110, 170, 195, 235), BorderThickness = new(1.3), Margin = margin, VerticalAlignment = VerticalAlignment.Center, Child = Text(label, 12.5, "DS.Text", FontWeights.SemiBold).Center() };

    public static TextBlock Section(string title, double top = 14) { var t = Text(title, 15.5, "DS.Text"); t.Margin = new(0, top, 0, 8); return t; }
    public static Border Rule() => new() { Height = 1, Background = B(Line), Margin = new(0, 14, 0, 2) };

    /// <summary>Apple-style segmented control: one rounded track, the selection is a raised white segment.</summary>
    public static Border Segmented(string selected, params string[] items)
    {
        var grid = new UniformGrid { Rows = 1 }; string group = "s" + Guid.NewGuid().ToString("N");
        foreach (var t in items) grid.Children.Add(new RadioButton { Content = t, Style = DS.Style("DS.Segment"), GroupName = group, IsChecked = t == selected });
        return new Border { CornerRadius = new(12), Background = B(30, 255, 255, 255), BorderBrush = B(34, 255, 255, 255), BorderThickness = new(1), Padding = new(1), Child = grid };
    }

    /// <summary>Choice cards as in "Connection Method": icon, label, radio; the selection is white.</summary>
    public static UniformGrid Choices(string selected, params (string Icon, string Label)[] items)
    {
        var grid = new UniformGrid { Rows = 1, Margin = new(0, 0, -10, 0) };
        foreach (var (icon, label) in items)
        {
            bool on = label == selected; string fg = on ? "DS.TextOnLight" : "DS.Text";
            var radio = new Grid { Width = 16, Height = 16, Children = { new Ellipse { Stroke = on ? B(Blue) : B(120, 200, 210, 230), StrokeThickness = 1.6 } } };
            if (on) radio.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = B(Blue) });
            var row = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
            var lead = on ? (UIElement)radio : Icon(icon, 16, fg); DockPanel.SetDock(lead, Dock.Left); ((FrameworkElement)lead).Margin = new(0, 0, 10, 0); row.Children.Add(lead);
            if (!on) { radio.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(radio, Dock.Right); row.Children.Add(radio); }
            row.Children.Add(Text(label, 14, fg, on ? FontWeights.SemiBold : FontWeights.Normal));
            var card = new Border { Height = 48, CornerRadius = new(12), Margin = new(0, 0, 10, 0), Padding = new(14, 0, 12, 0), BorderThickness = new(1), Child = row,
                Background = on ? B(255, 245, 247, 252) : B(22, 255, 255, 255), BorderBrush = on ? B(255, 255, 255, 255) : B(40, 255, 255, 255) };
            if (on) card.Effect = new DropShadowEffect { Color = Ice, BlurRadius = 16, ShadowDepth = 0, Opacity = .45 };
            grid.Children.Add(card);
        }
        return grid;
    }

    /// <summary>Paired row: label in its own bordered cell, value in a darker field with optional icon and trailing glyph.</summary>
    public static Grid Field(string label, string? icon, string value, string trailing = "", string valueBrush = "DS.Text")
    {
        var g = new Grid { Margin = new(0, 0, 0, 6), Height = 38 }; g.ColumnDefinitions.Add(new() { Width = new(.36, GridUnitType.Star) }); g.ColumnDefinitions.Add(new() { Width = new(8) }); g.ColumnDefinitions.Add(new() { Width = new(.64, GridUnitType.Star) });
        g.Children.Add(Box(Text(label, 13.5, "DS.TextSoft"), new(14, 0, 14, 0)));
        var v = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        if (trailing.Length > 0) { var t = Icon(trailing, 14); DockPanel.SetDock(t, Dock.Right); v.Children.Add(t); }
        if (icon != null) { var i = Icon(icon, 15, "DS.Text"); i.Margin = new(0, 0, 12, 0); DockPanel.SetDock(i, Dock.Left); v.Children.Add(i); }
        v.Children.Add(Text(value, 13.5, valueBrush));
        var vb = Box(v, new(14, 0, 14, 0), 10, 34, 34); Grid.SetColumn(vb, 2); g.Children.Add(vb);
        return g;
    }

    /// <summary>One OS interface line: icon, name + explanation, reported value, copy glyph.</summary>
    public static Border Interfaces(params (string Icon, string Title, string Detail, string Value)[] rows)
    {
        var stack = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            var (icon, title, detail, value) = rows[i];
            var g = new Grid { Height = 50 }; g.ColumnDefinitions.Add(new() { Width = new(40) }); g.ColumnDefinitions.Add(new() { Width = new(1.1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = new(24) });
            g.Children.Add(Icon(icon, 20, "DS.Text"));
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text(title, 13.5, "DS.Text", FontWeights.SemiBold), Text(detail, 11.5, "DS.TextSoft") } }; Grid.SetColumn(t, 1); g.Children.Add(t);
            var v = Text(value, 13, "DS.TextSoft"); v.TextWrapping = TextWrapping.Wrap; v.TextTrimming = TextTrimming.None; Grid.SetColumn(v, 2); g.Children.Add(v);
            var c = Icon("", 13); Grid.SetColumn(c, 3); g.Children.Add(c);
            stack.Children.Add(g);
            if (i < rows.Length - 1) stack.Children.Add(new Border { Height = 1, Background = B(28, 255, 255, 255) });
        }
        return Box(stack, new(14, 2, 12, 2), 12, 14, 36);
    }

    /// <summary>Identifier card: icon, heading, label/value lines.</summary>
    public static Border Identifiers(string icon, string title, params (string Label, string Value)[] rows)
    {
        var g = new Grid(); g.ColumnDefinitions.Add(new() { Width = new(38) }); g.ColumnDefinitions.Add(new());
        g.Children.Add(Icon(icon, 20, "DS.Text"));
        var lines = new Grid(); lines.ColumnDefinitions.Add(new() { Width = new(70) }); lines.ColumnDefinitions.Add(new());
        lines.RowDefinitions.Add(new() { Height = new(24) });
        var head = Text(title, 13.5, "DS.Text", FontWeights.SemiBold); Grid.SetColumnSpan(head, 2); lines.Children.Add(head);
        for (int i = 0; i < rows.Length; i++)
        {
            lines.RowDefinitions.Add(new() { Height = new(21) });
            var l = Text(rows[i].Label, 13, "DS.TextSoft"); Grid.SetRow(l, i + 1); lines.Children.Add(l);
            var v = Text(rows[i].Value, 13, "DS.Text"); Grid.SetRow(v, i + 1); Grid.SetColumn(v, 1); lines.Children.Add(v);
        }
        Grid.SetColumn(lines, 1); g.Children.Add(lines);
        return Box(g, new(14, 10, 14, 10), 12, 14, 36);
    }

    /// <summary>The big status card: glowing check ring and a large green word.</summary>
    public static Border StatusHero(string caption, string word, string detail, bool ok = true)
    {
        var c = ok ? Green : Color.FromRgb(245, 180, 91);
        var ring = new Grid { Width = 92, Height = 92, Margin = new(0, 0, 26, 0) };
        ring.Children.Add(new Ellipse { Fill = new RadialGradientBrush(Color.FromArgb(70, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)), Margin = new(-14) });
        ring.Children.Add(new Ellipse { Stroke = B(c), StrokeThickness = 5, Fill = B(70, c.R, c.G, c.B), Effect = new DropShadowEffect { Color = c, BlurRadius = 24, ShadowDepth = 0, Opacity = .9 } });
        ring.Children.Add(new Path { Data = Geometry.Parse(ok ? "M 28,47 L 41,60 L 65,34" : "M 46,26 L 46,52 M 46,64 L 46,66"), Stroke = Brushes.White, StrokeThickness = 7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Effect = new DropShadowEffect { Color = c, BlurRadius = 16, ShadowDepth = 0, Opacity = 1 } });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text(caption, 14, "DS.TextSoft"), Text(word, 34, "DS.Text", FontWeights.Bold), Text(detail, 14, "DS.TextSoft") } };
        ((TextBlock)text.Children[1]).Foreground = B(c);
        return new Border { CornerRadius = new(16), BorderThickness = new(1.2), Padding = new(28, 18, 28, 18), Margin = new(0, 12, 0, 0),
            BorderBrush = B(110, c.R, c.G, c.B), Background = new LinearGradientBrush(Color.FromArgb(60, c.R, c.G, c.B), Color.FromArgb(14, c.R, c.G, c.B), 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { ring, text } } };
    }

    /// <summary>Blue-tinted note box with a filled info glyph and a bold lead-in.</summary>
    public static Border Note(string lead, string text)
    {
        var t = new TextBlock { FontFamily = DS.Display, FontSize = 13, Foreground = DS.Brush("DS.TextSoft"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        t.Inlines.Add(new System.Windows.Documents.Run(lead + " ") { FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text") }); t.Inlines.Add(new System.Windows.Documents.Run(text));
        var d = new DockPanel(); var i = Icon("", 18, "DS.AccentHi"); i.Margin = new(0, 0, 12, 0); DockPanel.SetDock(i, Dock.Left); d.Children.Add(i); d.Children.Add(t);
        return new Border { CornerRadius = new(12), BorderThickness = new(1), BorderBrush = B(70, 90, 150, 255), Background = B(46, 40, 90, 200), Padding = new(14, 10, 14, 10), Margin = new(0, 12, 0, 0), Child = d };
    }

    public static UniformGrid Options(params (int N, string Title, string Detail, bool On)[] items)
    {
        var g = new UniformGrid { Rows = 1, Margin = new(0, 0, -10, 0) };
        foreach (var (n, title, detail, on) in items)
        {
            var badge = new Grid { Width = 30, Height = 30, Margin = new(0, 0, 12, 0), Children = { new Ellipse { Fill = on ? B(Blue) : B(40, 255, 255, 255) }, Text(n.ToString(), 14, "DS.Text", FontWeights.SemiBold).Center() } };
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text(title, 14.5, "DS.Text", FontWeights.SemiBold), Text(detail, 12, "DS.TextSoft") } };
            var card = new Border { CornerRadius = new(12), BorderThickness = new(on ? 1.6 : 1), Padding = new(12, 10, 12, 10), Margin = new(0, 0, 10, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { badge, text } },
                Background = on ? B(150, 18, 44, 96) : B(22, 255, 255, 255), BorderBrush = on ? B(Blue) : B(40, 255, 255, 255) };
            if (on) card.Effect = new DropShadowEffect { Color = Blue, BlurRadius = 16, ShadowDepth = 0, Opacity = .55 };
            g.Children.Add(card);
        }
        return g;
    }

    /// <summary>Right-panel layout: sections from the top, actions and notes pinned to the bottom (as in the concepts).</summary>
    public static DockPanel Stack(UIElement top, params UIElement[] bottom)
    {
        var d = new DockPanel(); var foot = new StackPanel(); foreach (var b in bottom) foot.Children.Add(b);
        DockPanel.SetDock(foot, Dock.Bottom); d.Children.Add(foot); d.Children.Add(top); return d;
    }

    /// <summary>Full-width outputs shelf: every target grouped spatially like a controller
    /// (face diamond, shoulders, D-pad cross, sticks/system, special). The chosen target glows blue.</summary>
    public static Border Outputs(string source, string selected)
    {
        var g = new Grid();
        var groups = new (string Name, double Weight)[] { ("FACE", 1), ("SHOULDERS", 1.15), ("D-PAD", 1), ("STICKS / SYSTEM", 1.25), ("SPECIAL", .6) };
        foreach (var (_, w) in groups) g.ColumnDefinitions.Add(new() { Width = new(w, GridUnitType.Star) });
        for (int i = 0; i < groups.Length; i++)
        {
            var col = new DockPanel(); Grid.SetColumn(col, i); g.Children.Add(col);
            var label = Text(groups[i].Name, 12.5, "DS.TextSoft", FontWeights.SemiBold).Center(); label.Margin = new(0, 0, 0, 8); DockPanel.SetDock(label, Dock.Top); col.Children.Add(label);
            if (i < groups.Length - 1) g.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Background = B(26, 255, 255, 255), Margin = new(0, 6, 0, 6), Tag = i }.Also(b => Grid.SetColumn(b, i)));
            col.Children.Add(i switch
            {
                0 => Diamond(selected, ("Y", "Y"), ("X", "X"), ("B", "B"), ("A", "A"), round: true),
                1 => Pairs(selected, ("LB", "LB"), ("RB", "RB"), ("LT", "LT"), ("RT", "RT")),
                2 => Diamond(selected, ("DPAD_UP", "↑"), ("DPAD_LEFT", "←"), ("DPAD_RIGHT", "→"), ("DPAD_DOWN", "↓"), round: false),
                3 => Pairs(selected, ("L3", "L3"), ("R3", "R3"), ("SELECT", "View"), ("START", "Menu")),
                _ => new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Tile("HOME", "Guide", selected, false, 96).Also(t => t.Height = 50) } }
            });
        }
        return Panel("Outputs", $"Choose what {source} sends", "Local draft · not applied", g, new(20, 14, 20, 14));
    }
    static Canvas Diamond(string selected, (string Key, string Label) top, (string Key, string Label) left, (string Key, string Label) right, (string Key, string Label) bottom, bool round)
    {
        double s = round ? 50 : 48, w = 172, h = 150; var c = new Canvas { Width = w, Height = h, HorizontalAlignment = HorizontalAlignment.Center };
        void Put((string Key, string Label) k, double x, double y) { var t = Tile(k.Key, k.Label, selected, round, s); Canvas.SetLeft(t, x); Canvas.SetTop(t, y); c.Children.Add(t); }
        Put(top, (w - s) / 2, 0); Put(left, 0, (h - s) / 2); Put(right, w - s, (h - s) / 2); Put(bottom, (w - s) / 2, h - s);
        return c;
    }
    static UniformGrid Pairs(string selected, params (string Key, string Label)[] items)
    {
        var u = new UniformGrid { Columns = 2, Rows = 2, Width = 220, Height = 124, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, l) in items) { var t = Tile(k, l, selected, false, 0); t.Margin = new(5); u.Children.Add(t); }
        return u;
    }
    static Border Tile(string key, string label, string selected, bool round, double size)
    {
        bool on = key == selected;
        var face = key switch { "Y" => Color.FromRgb(245, 205, 80), "X" => Color.FromRgb(70, 170, 255), "B" => Color.FromRgb(255, 100, 125), "A" => Color.FromRgb(70, 220, 150), _ => (Color?)null };
        var text = Text(label, round ? 20 : 18, "DS.Text", FontWeights.SemiBold).Center(); if (face != null && !on) text.Foreground = B(face.Value);
        var t = new Border
        {
            Child = text, BorderThickness = new(on ? 2 : 1.2), CornerRadius = new(round ? size / 2 : 12),
            Background = on ? new LinearGradientBrush(Color.FromRgb(70, 150, 255), Color.FromRgb(31, 104, 240), 90) : B(30, 255, 255, 255),
            BorderBrush = on ? Brushes.White : face != null ? B(150, face.Value.R, face.Value.G, face.Value.B) : B(46, 255, 255, 255),
            Effect = on ? new DropShadowEffect { Color = Blue, BlurRadius = 22, ShadowDepth = 0, Opacity = .9 } : null
        };
        if (size > 0) { t.Width = size; t.Height = size; }
        return t;
    }

    public static Grid Actions(string primaryIcon, string primary, string secondaryIcon, string secondary, bool primaryEnabled = true)
    {
        var g = new Grid { Margin = new(0, 14, 0, 0) }; g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = new(12) }); g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = new(12) }); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var p = new Button { Content = IconLabel(primaryIcon, primary), Style = DS.Style("DS.ActionPrimary"), IsEnabled = primaryEnabled };
        var s = new Button { Content = IconLabel(secondaryIcon, secondary), Style = DS.Style("DS.ActionSecondary") }; Grid.SetColumn(s, 2);
        var more = new Button { Content = "", Style = DS.Style("DS.ActionRound") }; Grid.SetColumn(more, 4);
        g.Children.Add(p); g.Children.Add(s); g.Children.Add(more); return g;
    }
    static StackPanel IconLabel(string icon, string label) => new() { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = icon, FontFamily = Icons, FontSize = 17, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0) }, new TextBlock { Text = label, FontFamily = DS.Display, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } } };

    public static Border Info(string text) => new()
    {
        CornerRadius = new(12), BorderThickness = new(1), BorderBrush = B(34, 255, 255, 255), Background = B(14, 255, 255, 255), Padding = new(14, 9, 14, 9), Margin = new(0, 12, 0, 0),
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "", FontFamily = Icons, FontSize = 14, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) }, Text(text, 13, "DS.TextSoft") } }
    };

    public static Border Table(params (string Label, string Value, string? Brush)[] rows)
    {
        var g = new Grid(); g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        for (int i = 0; i < rows.Length; i++)
        {
            g.RowDefinitions.Add(new() { Height = new(29) });
            var l = Text(rows[i].Label, 13.5, "DS.TextSoft"); Grid.SetRow(l, i); g.Children.Add(l);
            var v = Text(rows[i].Value, 13.5, rows[i].Brush ?? "DS.Text"); v.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetRow(v, i); Grid.SetColumn(v, 1); g.Children.Add(v);
        }
        return Box(g, new(14, 6, 14, 6), 12, 16, 38);
    }

    public static StackPanel Stick(string label, double x, double y, double size = 110)
    {
        var c = new Canvas { Width = size, Height = size };
        c.Children.Add(new Ellipse { Width = size, Height = size, Stroke = B(110, 125, 182, 255), StrokeThickness = 1.4, Fill = B(30, 20, 40, 90) });
        c.Children.Add(new Line { X1 = size / 2, Y1 = 6, X2 = size / 2, Y2 = size - 6, Stroke = B(Line), StrokeThickness = 1 });
        c.Children.Add(new Line { X1 = 6, Y1 = size / 2, X2 = size - 6, Y2 = size / 2, Stroke = B(Line), StrokeThickness = 1 });
        var dot = new Ellipse { Width = 16, Height = 16, Fill = Brushes.White, Effect = new DropShadowEffect { Color = Blue, BlurRadius = 18, ShadowDepth = 0, Opacity = 1 } };
        Canvas.SetLeft(dot, size / 2 - 8 + x * size * .38); Canvas.SetTop(dot, size / 2 - 8 - y * size * .38); c.Children.Add(dot);
        return new StackPanel { Children = { c, Text(label, 14).Center(), Text($"X {x:0.00}   Y {y:0.00}", 12.5, "DS.TextSoft").Center() } };
    }

    public static StackPanel Trigger(string label, double value, double height = 110)
    {
        var track = new Grid { Width = 20, Height = height };
        track.Children.Add(new Border { CornerRadius = new(10), Background = B(60, 70, 90, 140), BorderBrush = B(70, 125, 182, 255), BorderThickness = new(1) });
        track.Children.Add(new Border { CornerRadius = new(10), Height = Math.Max(20, height * value), VerticalAlignment = VerticalAlignment.Bottom, Background = new LinearGradientBrush(Color.FromRgb(90, 165, 255), Color.FromRgb(40, 110, 255), 90), Effect = new DropShadowEffect { Color = Blue, BlurRadius = 14, ShadowDepth = 0, Opacity = .8 } });
        return new StackPanel { Children = { track, Text($"{label} {value * 100:0}%", 14).Center() } };
    }
}

/// <summary>The hologram controller for a model: line art plus interactive control outlines.
/// A/B/X/Y wear their console colours; the selected control glows; sticks show a live dot.</summary>
public sealed class HologramController : Grid
{
    private readonly Canvas overlay = new() { Width = 1536, Height = 1024 };
    private readonly Dictionary<string, Path> shapes = new();
    public HologramController(string model, bool rear = false)
    {
        Width = 1536; Height = 1024;
        var image = new Image { Source = LiveController.Bitmap($"Assets/native/{model}/native/holo-{(rear ? "rear" : "front")}.png"), Width = 1536, Height = 1024 };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); Children.Add(image); Children.Add(overlay);
        using var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")));
        if (!doc.RootElement.GetProperty("models").TryGetProperty(model, out var m) || !m.TryGetProperty(rear ? "back" : "front", out var view)) return;
        foreach (var c in view.EnumerateObject())
        {
            if (c.Name is "CAPTURE" or "TURBO") continue;
            var face = c.Name switch { "Y" => Color.FromRgb(245, 205, 80), "X" => Color.FromRgb(60, 170, 255), "B" => Color.FromRgb(255, 92, 120), "A" => Color.FromRgb(60, 220, 150), _ => (Color?)null };
            var path = new Path { Data = Geometry.Parse(c.Value.GetProperty("path").GetString()!), StrokeThickness = face != null ? 5 : 2.5, Stroke = new SolidColorBrush(face ?? Color.FromArgb(150, 125, 190, 255)), Fill = new SolidColorBrush(Color.FromArgb(face != null ? (byte)30 : (byte)10, (face ?? Kit.Ice).R, (face ?? Kit.Ice).G, (face ?? Kit.Ice).B)) };
            if (face != null) { path.Effect = new DropShadowEffect { Color = face.Value, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 }; overlay.Children.Add(Letter(c.Name, path.Data.Bounds, face.Value)); }
            overlay.Children.Insert(0, path); shapes[c.Name] = path;
        }
    }
    private static TextBlock Letter(string text, Rect bounds, Color color)
    {
        var t = new TextBlock { Text = text, FontFamily = DS.Display, FontSize = bounds.Height * .58, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(color), Width = bounds.Width, TextAlignment = TextAlignment.Center };
        Canvas.SetLeft(t, bounds.X); Canvas.SetTop(t, bounds.Y + bounds.Height * .14); return t;
    }
    /// <summary>Light one control the way the concepts light the triggers: bright fill and glow.</summary>
    public void Select(string key)
    {
        foreach (var (name, p) in shapes) if (name is not ("A" or "B" or "X" or "Y")) { p.Stroke = new SolidColorBrush(Color.FromArgb(150, 125, 190, 255)); p.Fill = new SolidColorBrush(Color.FromArgb(10, 125, 182, 255)); p.Effect = null; }
        if (shapes.TryGetValue(key, out var s)) { s.Stroke = Brushes.White; s.Fill = new SolidColorBrush(Color.FromArgb(150, 61, 139, 255)); s.Effect = new DropShadowEffect { Color = Kit.Blue, BlurRadius = 30, ShadowDepth = 0, Opacity = 1 }; }
    }
}

/// <summary>The real controller (layered transparent photo with moving sticks and triggers) with a
/// soft floor shadow and the selected control lit by a glowing outline that follows its exact shape.</summary>
public sealed class PhotoController : Grid
{
    private readonly Canvas overlay = new() { Width = 1536, Height = 1024, IsHitTestVisible = false };
    private readonly Dictionary<string, Path> shapes = new();
    public LiveController Live { get; } = new();
    public PhotoController(string model, bool rear = false)
    {
        Width = 1536; Height = 1024;
        Children.Add(new Ellipse { Width = 980, Height = 80, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 30), Fill = new RadialGradientBrush(Color.FromArgb(170, 0, 3, 10), Color.FromArgb(0, 0, 3, 10)) });
        Live.Show(model, rear); Children.Add(Live); Children.Add(overlay);
        using var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")));
        if (!doc.RootElement.GetProperty("models").TryGetProperty(model, out var m) || !m.TryGetProperty(rear ? "back" : "front", out var view)) return;
        foreach (var c in view.EnumerateObject())
        {
            if (c.Name is "CAPTURE" or "TURBO") continue;
            var path = new Path { Data = Geometry.Parse(c.Value.GetProperty("path").GetString()!), StrokeThickness = 4, Visibility = Visibility.Collapsed };
            overlay.Children.Add(path); shapes[c.Name] = path;
        }
    }
    public void Select(string key)
    {
        foreach (var p in shapes.Values) p.Visibility = Visibility.Collapsed;
        if (!shapes.TryGetValue(key, out var s)) return;
        s.Visibility = Visibility.Visible; s.Stroke = new SolidColorBrush(Color.FromRgb(150, 200, 255)); s.Fill = new SolidColorBrush(Color.FromArgb(90, 61, 139, 255));
        s.Effect = new DropShadowEffect { Color = Kit.Blue, BlurRadius = 26, ShadowDepth = 0, Opacity = 1 };
    }
}
