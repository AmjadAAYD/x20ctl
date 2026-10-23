using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>The design system, as in the Windows app: a deep navy stage, glass cards with hairline edges, light pills, one blue accent.</summary>
internal static class T
{
    public static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    public static readonly Color Void = Color.Parse("#04070F"), Deep = Color.Parse("#070C1B"), Night = Color.Parse("#0B1430");
    public static readonly IBrush Accent = B("#3D8BFF"), AccentHi = B("#7DB6FF"), Success = B("#34D399"), Warn = B("#F5B45B"), Danger = B("#FF5C7A");
    public static readonly IBrush Text = B("#F4F7FD"), Soft = B("#B4C0D8"), Muted = B("#76839F"), Hairline = B("#1CFFFFFF"), Track = B("#2A3656");
    public static readonly IBrush Subtle = B("#14FFFFFF"), Hover = B("#22FFFFFF"), Well = B("#66060B18"), WellEdge = B("#14FFFFFF");
    public static readonly IBrush Glass = Vertical("#C0121C3A", "#B00C1430"), GlassRaised = Vertical("#D81A2750", "#C8111B3C");
    public static readonly IBrush ButtonFill = Vertical("#30384C", "#272E40"), ButtonHover = Vertical("#3C4660", "#323B52");
    public static readonly IBrush PrimaryFill = Diagonal("#4598F5", "#287CE9"), PrimaryHover = Diagonal("#5AA6FF", "#3A8BF2");
    public static readonly IBrush TrackFill = Horizontal("#2F7BFF", "#5AA2FF");
    public static readonly FontFamily Font = new("Inter, Segoe UI, Cantarell, sans-serif");

    public static LinearGradientBrush Vertical(string a, string b) => new() { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(0, 1, RelativeUnit.Relative), GradientStops = { new(Color.Parse(a), 0), new(Color.Parse(b), 1) } };
    public static LinearGradientBrush Horizontal(string a, string b) => new() { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 0, RelativeUnit.Relative), GradientStops = { new(Color.Parse(a), 0), new(Color.Parse(b), 1) } };
    public static LinearGradientBrush Diagonal(string a, string b) => new() { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 1, RelativeUnit.Relative), GradientStops = { new(Color.Parse(a), 0), new(Color.Parse(b), 1) } };

    public static TextBlock Label(string text, double size = 14, IBrush? color = null, FontWeight weight = FontWeight.Normal) =>
        new() { Text = text, FontSize = size, Foreground = color ?? Text, FontWeight = weight, TextWrapping = TextWrapping.Wrap, FontFamily = Font };

    /// <summary>A glass card: translucent navy with a light edge.</summary>
    public static Border Card(Control? child = null, double pad = 18, double radius = 18, bool raised = false) => new()
    {
        Background = raised ? GlassRaised : Glass, CornerRadius = new CornerRadius(radius), Padding = new Thickness(pad), BorderBrush = Hairline, BorderThickness = new Thickness(1), Child = child, ClipToBounds = true
    };

    public static StackPanel Stack(double spacing, params Control[] children) { var s = new StackPanel { Spacing = spacing }; foreach (var c in children) s.Children.Add(c); return s; }
    public static StackPanel Line(double spacing, params Control[] children) { var s = new StackPanel { Spacing = spacing, Orientation = Orientation.Horizontal }; foreach (var c in children) s.Children.Add(c); return s; }
    /// <summary>A row on the desktop; on a phone it wraps onto more lines rather than running off the edge.</summary>
    public static Panel Flow(double gap, params Control[] children) => Phone.Active ? Wrap(gap, children) : Line(gap, children);
    public static WrapPanel Wrap(double gap, params Control[] children) { var w = new WrapPanel(); foreach (var c in children) { c.Margin = new Thickness(0, 0, gap, gap); w.Children.Add(c); } return w; }

    public static Control StatusChip(string text, IBrush dot)
    {
        var d = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = dot, VerticalAlignment = VerticalAlignment.Center };
        var row = Line(7, d, Label(text, 12.5, Soft)); row.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Background = Subtle, CornerRadius = new CornerRadius(20), Padding = new Thickness(10, 5), Child = row, HorizontalAlignment = HorizontalAlignment.Left };
    }

    public static Bitmap? Art(string modelId, string file)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri($"avares://x20ctl-studio/Assets/{modelId}/native/{file}.png"))); }
        catch (Exception) { return null; }
    }

    static readonly Dictionary<string, Bitmap?> Small = new();
    /// <summary>A photo decoded at <paramref name="width"/> pixels and kept: the chooser's eight cards then cost about
    /// what two full photos do, and open at once the second time.</summary>
    public static Bitmap? Art(string modelId, string file, int width)
    {
        string key = $"{modelId}/{file}@{width}";
        if (Small.TryGetValue(key, out var kept)) return kept;
        try { using var s = AssetLoader.Open(new Uri($"avares://x20ctl-studio/Assets/{modelId}/native/{file}.png")); kept = Bitmap.DecodeToWidth(s, width, BitmapInterpolationMode.HighQuality); }
        catch (Exception) { kept = null; }
        return Small[key] = kept;
    }
}

/// <summary>A pill button: ghost, primary (blue), light (selected) or flat. Drawn here so hover and press behave the same on every desktop.</summary>
internal sealed class Pill : Border
{
    public enum Kinds { Ghost, Primary, Light, Flat }
    readonly TextBlock label; Kinds kind; bool hover, enabled = true;
    public event Action? Click;
    public Pill(string text, Kinds kind = Kinds.Ghost, Action? click = null, double size = 13.5)
    {
        this.kind = kind; if (click != null) Click += click;
        label = T.Label(text, size, null, FontWeight.Medium); label.TextWrapping = TextWrapping.NoWrap; label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
        Child = label; Padding = new Thickness(18, 9); CornerRadius = new CornerRadius(40); BorderThickness = new Thickness(1); Cursor = new Cursor(StandardCursorType.Hand);
        PointerEntered += (_, _) => { hover = true; Paint(); }; PointerExited += (_, _) => { hover = false; Paint(); Motion.Press(this, false); };
        // it presses in and springs back (Windows: .94 over 80 ms, back over 260 ms); the click happens on the press
        // on a phone it presses in on touch but acts on the tap, so a swipe that starts on it still scrolls
        PointerPressed += (_, e) => { if (enabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { Motion.Press(this, true); if (!Phone.Active) { Click?.Invoke(); e.Handled = true; } } };
        if (Phone.Active) Tapped += (_, e) => { if (enabled) { Click?.Invoke(); e.Handled = true; } };
        PointerReleased += (_, _) => Motion.Press(this, false); PointerCaptureLost += (_, _) => Motion.Press(this, false);
        Paint();
    }
    public string Text { get => label.Text ?? ""; set => label.Text = value; }
    public Kinds Kind { get => kind; set { kind = value; Paint(); } }
    public bool Enabled { get => enabled; set { enabled = value; Cursor = new Cursor(value ? StandardCursorType.Hand : StandardCursorType.Arrow); Paint(); } }
    void Paint()
    {
        bool h = hover && enabled;
        (Background, BorderBrush, label.Foreground) = kind switch
        {
            Kinds.Primary => (h ? T.PrimaryHover : T.PrimaryFill, (IBrush)T.B("#3F9BFF"), T.Text),
            Kinds.Light => (T.B("#F4F7FD"), Brushes.Transparent, T.B("#0A1226")),
            Kinds.Flat => (h ? T.Hover : Brushes.Transparent, Brushes.Transparent, h ? T.Text : T.Soft),
            _ => (h ? T.ButtonHover : T.ButtonFill, T.Hairline, T.Text)
        };
        Opacity = enabled ? 1 : .45;
    }
}

/// <summary>A page that fills the height it is given when there is room, and scrolls when there isn't: it is laid out at
/// the viewport's height, but never below <paramref name="minHeight"/>. Its star rows grow on a tall window.</summary>
internal sealed class FillScroll : ScrollViewer
{
    public FillScroll(Control page, double minHeight)
    {
        Content = new Filler(this, minHeight) { Child = page };
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        // on a phone the Studio already scrolls each tab: this one only keeps the page at least this tall
        if (Phone.Active) VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewportProperty && Content is Control filler) filler.InvalidateMeasure();
    }
    sealed class Filler(FillScroll owner, double minHeight) : Decorator
    {
        protected override Size MeasureOverride(Size available)
        {
            double height = Phone.Active ? Math.Max(minHeight, double.IsInfinity(available.Height) ? 0 : available.Height) : Math.Max(minHeight, owner.Viewport.Height);
            // a phone's narrower layout can need more than the desktop's height: the page then grows, and the tab scrolls
            if (Phone.Active && Child != null) { Child.Measure(new Size(available.Width, double.PositiveInfinity)); height = Math.Max(height, Child.DesiredSize.Height); }
            Child?.Measure(new Size(available.Width, height));
            return new Size(Child?.DesiredSize.Width ?? 0, height);
        }
    }
}

/// <summary>The segmented control: a dark track, the chosen segment a white pill.</summary>
internal static class Segmented
{
    public static Control Make(IReadOnlyList<string> items, string selected, Action<string> pick, double size = 13.5)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (string item in items) { string v = item; row.Children.Add(new Pill(v, v == selected ? Pill.Kinds.Light : Pill.Kinds.Flat, () => pick(v), size) { Padding = new Thickness(20, 7) }); }
        return new Border { Background = T.B("#80101A33"), BorderBrush = T.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(40), Padding = new Thickness(3), Child = row, HorizontalAlignment = HorizontalAlignment.Left };
    }
}

/// <summary>A thin rail with a pale thumb, like the Windows sliders. Value runs Minimum to Maximum.</summary>
internal sealed class FlatSlider : Control
{
    public double Minimum { get; init; } = 0;
    public double Maximum { get; init; } = 100;
    public bool Fill { get; init; } = true;
    /// <summary>The smallest step the slider moves in (1 by default; 0.1 for the fine stick tools).</summary>
    public double Step { get; init; } = 1;
    double value; bool dragging;
    public event Action<double>? ValueChanged;
    public FlatSlider() { Height = 26; Cursor = new Cursor(StandardCursorType.Hand); MinWidth = 80; }
    public double Value { get => value; set { this.value = Math.Clamp(value, Minimum, Maximum); InvalidateVisual(); } }
    double Fraction => Maximum > Minimum ? (value - Minimum) / (Maximum - Minimum) : 0;
    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, cy = Bounds.Height / 2, x = 11 + (w - 22) * Fraction;
        ctx.DrawRectangle(T.Track, null, new RoundedRect(new Rect(0, cy - 2, w, 4), 2));
        if (Fill) ctx.DrawRectangle(T.TrackFill, null, new RoundedRect(new Rect(0, cy - 2, Math.Max(x, 4), 4), 2));
        ctx.DrawEllipse(T.B("#66000000"), null, new Point(x, cy + 1.5), 11, 11);
        ctx.DrawEllipse(T.B("#DCEBFF"), new Pen(T.B("#7DB6FF"), 2), new Point(x, cy), 10, 10);
    }
    void Set(PointerEventArgs e)
    {
        double w = Math.Max(1, Bounds.Width - 22), f = Math.Clamp((e.GetPosition(this).X - 11) / w, 0, 1);
        double next = Math.Round((Minimum + f * (Maximum - Minimum)) / Step) * Step; next = Math.Round(next, 4); if (next != value) { value = next; InvalidateVisual(); ValueChanged?.Invoke(value); }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { dragging = true; e.Pointer.Capture(this); Set(e); }
    protected override void OnPointerMoved(PointerEventArgs e) { if (dragging) Set(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { dragging = false; e.Pointer.Capture(null); }
}

/// <summary>The stage: a near-black navy with a soft blue light from the upper right.</summary>
internal sealed class Backdrop : Control
{
    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size);
        ctx.DrawRectangle(new LinearGradientBrush { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 1, RelativeUnit.Relative), GradientStops = { new(T.Void, 0), new(T.Deep, .45), new(T.Night, 1) } }, null, r);
        ctx.DrawRectangle(new RadialGradientBrush { Center = new(.92, .3, RelativeUnit.Relative), GradientOrigin = new(.92, .3, RelativeUnit.Relative), RadiusX = new(.75, RelativeUnit.Relative), RadiusY = new(.85, RelativeUnit.Relative), GradientStops = { new(Color.Parse("#7A1E4FA8"), 0), new(Color.Parse("#00000000"), 1) } }, null, r);
    }
}

/// <summary>A controller photo cropped to the pad itself (framing.json), with children anchored on its controls by their
/// position on the 1536x1024 photo. Draws the soft floor shadow under the pad.</summary>
internal sealed class ControllerArt : Panel
{
    public static readonly AttachedProperty<Point?> AnchorProperty = AvaloniaProperty.RegisterAttached<ControllerArt, Control, Point?>("Anchor");
    public static void SetAnchor(Control c, Point? p) => c.SetValue(AnchorProperty, p);
    public static readonly Size Photo = new(1536, 1024);
    static readonly Dictionary<string, Rect> Frames = LoadFrames();

    readonly Bitmap? bitmap; readonly Rect crop, source; bool shadow;
    Rect dest;
    /// <summary>The soft floor shadow under the pad (the Zone's hero has one, the cards beside it don't).</summary>
    public bool Shadow { get => shadow; set { if (shadow == value) return; shadow = value; Children[0].InvalidateVisual(); } }
    /// <param name="small">Drawn small (a card): a smaller copy of the photo, decoded once and kept.</param>
    public ControllerArt(string modelId, bool rear = false, bool shadow = true, bool small = false)
    {
        this.shadow = shadow; string file = rear ? "rear-full" : "front-full";
        bitmap = small ? T.Art(modelId, file, 768) : T.Art(modelId, file);
        crop = Frames.TryGetValue(modelId + (rear ? ":back" : ":front"), out var f) ? f : new Rect(Photo);
        // the crop is in the full photo's pixels; a smaller copy takes it scaled down
        double k = bitmap == null ? 1 : bitmap.Size.Width / Photo.Width; source = new Rect(crop.X * k, crop.Y * k, crop.Width * k, crop.Height * k);
        Children.Add(new Surface(this));
    }

    /// <summary>The floor shadow and the photo, drawn under every overlay.</summary>
    sealed class Surface(ControllerArt owner) : Control
    {
        public override void Render(DrawingContext ctx) => owner.Paint(ctx);
    }
    /// <summary>Where a photo position (0-1) lands in this control.</summary>
    public Point Map(double x, double y) => new(dest.X + (x * Photo.Width - crop.X) / crop.Width * dest.Width, dest.Y + (y * Photo.Height - crop.Y) / crop.Height * dest.Height);
    public double Scale => crop.Width > 0 ? dest.Width / crop.Width : 1;

    static Dictionary<string, Rect> LoadFrames()
    {
        var map = new Dictionary<string, Rect>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContent.Root, "Data", "framing.json")));
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateObject())
                foreach (var side in new[] { "front", "back" })
                    if (m.Value.TryGetProperty(side, out var a) && a.GetArrayLength() == 4) map[m.Name + ":" + side] = new Rect(a[0].GetDouble(), a[1].GetDouble(), a[2].GetDouble(), a[3].GetDouble());
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        return map;
    }

    Rect Fit(Size available)
    {
        double aspect = crop.Width / crop.Height, w = available.Width, h = w / aspect;
        if (h > available.Height) { h = available.Height; w = h * aspect; }
        return new Rect((available.Width - w) / 2, (available.Height - h) / 2, w, h);
    }
    protected override Size MeasureOverride(Size available)
    {
        var size = new Size(double.IsInfinity(available.Width) ? crop.Width / 2 : available.Width, double.IsInfinity(available.Height) ? crop.Height / 2 : available.Height);
        foreach (var c in Children) c.Measure(Size.Infinity);
        return size;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        dest = Fit(finalSize);
        foreach (var c in Children)
        {
            if (c is Surface) c.Arrange(new Rect(finalSize));
            else if (c.GetValue(AnchorProperty) is { } a) { var p = Map(a.X, a.Y); var s = c.DesiredSize; c.Arrange(new Rect(p.X - s.Width / 2, p.Y - s.Height / 2, s.Width, s.Height)); }
            else c.Arrange(dest);
        }
        return finalSize;
    }
    void Paint(DrawingContext ctx)
    {
        if (dest.Width <= 0) return;
        if (shadow)
        {
            var c = new Point(dest.Center.X, dest.Bottom - dest.Height * .02);
            ctx.DrawEllipse(new RadialGradientBrush { GradientStops = { new(Color.Parse("#A0000000"), 0), new(Color.Parse("#00000000"), 1) } }, null, c, dest.Width * .36, dest.Height * .075);
        }
        if (bitmap != null) ctx.DrawImage(bitmap, source, dest);
    }
}

/// <summary>The bottom row of controller hints: a coloured glyph, then what it does.</summary>
internal static class Hints
{
    public static Control Row(params (string Glyph, string Text)[] items)
    {
        if (Phone.Active) return new Border();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 26 };
        foreach (var (g, t) in items) row.Children.Add(Item(g, t));
        return row;
    }
    static Control Item(string glyph, string text)
    {
        Control mark;
        var colour = glyph switch { "A" => "#34D399", "B" => "#FF5C7A", "X" => "#3D8BFF", "Y" => "#F5B45B", _ => null };
        if (colour != null)
            mark = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderBrush = T.B(colour), BorderThickness = new Thickness(1.6), Background = T.B("#22" + colour[1..]), Child = T.Label(glyph, 13, T.Soft, FontWeight.SemiBold) is var l && l != null ? Center(l) : null };
        else if (glyph == "≡") mark = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderBrush = T.Soft, BorderThickness = new Thickness(1.6), Child = Center(T.Label("≡", 15, T.Soft)) };
        else mark = new Border { Padding = new Thickness(8, 3), CornerRadius = new CornerRadius(7), BorderBrush = T.Soft, BorderThickness = new Thickness(1.4), Background = T.B("#22FFFFFF"), VerticalAlignment = VerticalAlignment.Center, Child = T.Label(glyph, 11.5, T.Text, FontWeight.SemiBold) };
        var row = T.Line(9, mark, T.Label(text, 14, T.Soft)); row.VerticalAlignment = VerticalAlignment.Center; return row;
    }
    static Control Center(TextBlock t) { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; return t; }
}
