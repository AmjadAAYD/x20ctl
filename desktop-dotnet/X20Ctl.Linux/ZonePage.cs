using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Player slots: the selected player's card is the hero on the left, the other three wait on the right. Choosing
/// another player glides the cards to their new places with the Windows timings (ZoneScene: the words step aside over
/// 130 ms, the cards move over 460 ms, fast through the middle, then the words come back over 220 ms while the art
/// settles), and a soft blue light travels with the chosen card to rest behind the hero.</summary>
internal sealed class ZonePage : UserControl
{
    const int RetractMs = 130, MoveMs = 460, SettleMs = 220;
    readonly Session session; readonly Action enter; readonly Action<int> choose, remove;
    readonly Stage stage; readonly Dock[] docks = new Dock[4];
    Action stopMotion = () => { };
    public Control Heading { get; }
    /// <summary>The cards in reading order: the hero, then the three on the right from the top.</summary>
    public IReadOnlyList<Control> Cards => new[] { stage.Hero }.Concat(Enumerable.Range(0, 4).Where(i => i != stage.Hero)).Select(i => (Control)docks[i]).ToArray();

    public ZonePage(Session session, Action enter, Action<int> choose, Action<int> remove)
    {
        this.session = session; this.enter = enter; this.choose = choose; this.remove = remove;
        var grid = new Grid { Margin = new Thickness(60, 30, 60, 26), RowDefinitions = new("Auto,*") };
        Heading = T.Stack(2, T.Label("Player slots", 46, null, FontWeight.Bold), T.Label("Choose a player to set up its controller", 17, T.Soft));
        grid.Children.Add(Heading);
        stage = new Stage(this, session.Selected) { Margin = new Thickness(0, 28, 0, 0) }; Grid.SetRow(stage, 1); grid.Children.Add(stage);
        for (int i = 0; i < 4; i++) { docks[i] = new Dock(this, i); docks[i].Fill(i == session.Selected); stage.Children.Add(docks[i]); }
        Content = grid;
    }

    /// <summary>Makes <paramref name="slot"/> the hero, gliding the cards there. A glide already under way lands first.</summary>
    public void Select(int slot)
    {
        if (slot is < 0 or > 3) return;
        stopMotion(); Settle();
        session.Selected = slot; if (slot == stage.Hero) return;
        stopMotion = Motion.Run(RetractMs, t => { foreach (var d in docks) d.Details = 1 - t; }, () =>
        {
            foreach (var d in docks) d.BeginMove(d.Slot == slot);
            stage.BeginMove(slot);
            stopMotion = Motion.Run(MoveMs, t => stage.Progress = Motion.QuintInOut(t), () =>
            {
                Settle();
                foreach (var d in docks) { d.Details = 0; d.ArtScale = .97; }
                stopMotion = Motion.Run(SettleMs, t => { double e = Motion.CubicOut(t); foreach (var d in docks) { d.Details = e; d.ArtScale = Motion.Lerp(.97, 1, e); } });
            });
        });
    }
    /// <summary>Everything at rest for the hero the cards now show.</summary>
    void Settle() { stage.EndMove(); foreach (var d in docks) { d.EndMove(); d.Details = 1; d.ArtScale = 1; } }

    /// <summary>Lays the four cards out (the hero 2.08 parts wide, the others stacked beside it) and, while they move,
    /// between where they were and where they go. Draws the light behind the hero.</summary>
    sealed class Stage : Panel
    {
        readonly ZonePage page; readonly Glow glow;
        Rect[]? from; double progress = 1;
        public Stage(ZonePage page, int hero) { this.page = page; Hero = hero; Children.Add(glow = new Glow(this)); }
        public int Hero { get; private set; }
        public double Progress { get => progress; set { progress = value; InvalidateMeasure(); glow.InvalidateVisual(); } }
        public bool Moving => from != null;
        public void BeginMove(int slot) { from = page.docks.Select(d => d.Bounds).ToArray(); Hero = slot; progress = 0; foreach (var d in page.docks) d.ZIndex = d.Slot == slot ? 2 : 0; InvalidateMeasure(); }
        public void EndMove() { if (from == null) return; from = null; progress = 1; InvalidateMeasure(); glow.InvalidateVisual(); }

        Rect[] Places(Size size)
        {
            double heroWidth = Math.Round(size.Width * 2.08 / 3.08), x = heroWidth + 26, width = Math.Max(0, size.Width - x), height = Math.Max(0, (size.Height - 36) / 3);
            var places = new Rect[4]; int row = 0;
            for (int i = 0; i < 4; i++) places[i] = i == Hero ? new Rect(0, 0, heroWidth, size.Height) : new Rect(x, row++ * (height + 18), width, height);
            if (from != null)
                for (int i = 0; i < 4; i++)
                {
                    var a = from[i]; var b = places[i];
                    places[i] = new Rect(Motion.Lerp(a.X, b.X, progress), Motion.Lerp(a.Y, b.Y, progress), Motion.Lerp(a.Width, b.Width, progress), Motion.Lerp(a.Height, b.Height, progress));
                }
            return places;
        }
        protected override Size MeasureOverride(Size available)
        {
            var size = new Size(double.IsInfinity(available.Width) ? 1200 : available.Width, double.IsInfinity(available.Height) ? 620 : available.Height);
            var places = Places(size); for (int i = 0; i < 4; i++) page.docks[i].Measure(places[i].Size);
            glow.Measure(size); return size;
        }
        protected override Size ArrangeOverride(Size final)
        {
            var places = Places(final); for (int i = 0; i < 4; i++) page.docks[i].Arrange(places[i]);
            glow.Arrange(new Rect(final)); return final;
        }
        /// <summary>The soft blue light behind the hero, following it as it moves; drawn under the cards.</summary>
        sealed class Glow(Stage stage) : Control
        {
            public override void Render(DrawingContext dc)
            {
                var at = stage.page.docks[stage.Hero].Bounds; if (at.Width <= 0) return;
                var blue = Color.FromArgb(40, 0x3D, 0x8B, 0xFF);
                dc.DrawEllipse(new RadialGradientBrush { GradientStops = { new(blue, 0), new(Color.FromArgb(0, blue.R, blue.G, blue.B), 1) } }, null, at.Center, at.Width * 1.15 / 2, at.Height * .95 / 2);
            }
        }
    }

    /// <summary>One player's card. Its words sit in a layout for its role (hero or beside it); its controller art is its own
    /// layer, so while the cards move the art glides from where the old layout held it to where the new one does.</summary>
    sealed class Dock : Border
    {
        readonly ZonePage page; readonly ControllerModel? model; readonly ControllerArt? art; readonly Layers layers;
        bool hero, hover;
        public int Slot { get; }
        public Dock(ZonePage page, int slot)
        {
            this.page = page; Slot = slot; model = page.session.Players[slot];
            if (model != null) art = new ControllerArt(model.Id) { RenderTransformOrigin = RelativePoint.Center };
            Child = layers = new Layers(this, art); ClipToBounds = true;
            PointerEntered += (_, _) => { hover = true; Paint(); }; PointerExited += (_, _) => { hover = false; Paint(); };
            PointerPressed += (_, e) => { if (!hero && model != null && !e.Handled) { e.Handled = true; page.Select(Slot); } };
        }
        public double Details { set => layers.Words.Opacity = value; }
        public double ArtScale { set { if (art != null) art.RenderTransform = value == 1 ? null : new ScaleTransform(value, value); } }
        public double Progress => page.stage.Moving ? page.stage.Progress : 1;

        void Paint()
        {
            Background = hero ? T.GlassRaised : T.Glass; CornerRadius = new CornerRadius(hero ? 20 : 16);
            BorderBrush = hero ? T.B("#7FA5E0FF") : hover && model != null ? T.B("#66AFC8FF") : T.Hairline; BorderThickness = new Thickness(hero ? 1.6 : 1);
            Cursor = new Cursor(!hero && model != null ? StandardCursorType.Hand : StandardCursorType.Arrow);
        }

        /// <summary>Lays the card out for its role. The art keeps its place in a blank cell of the layout.</summary>
        public void Fill(bool asHero)
        {
            hero = asHero; Paint(); if (art != null) art.Shadow = asHero;
            var spot = new Border();
            var g = asHero ? new Grid { RowDefinitions = new("Auto,*,Auto,Auto,Auto"), Margin = new Thickness(26) } : new Grid { RowDefinitions = new("Auto,*"), Margin = new Thickness(14) };
            g.Children.Add(T.Label("Player " + (Slot + 1), asHero ? 25 : 17, null, FontWeight.Bold));
            if (model != null && asHero)
            {
                spot.Margin = new Thickness(10, 4, 10, 4); Grid.SetRow(spot, 1); g.Children.Add(spot);
                var name = T.Label(model.Name, 36, null, FontWeight.SemiBold); name.Margin = new Thickness(2, 0, 0, 8); Grid.SetRow(name, 2); g.Children.Add(name);
                var status = T.StatusChip("Not connected", T.Warn); status.Margin = new Thickness(0, 0, 0, 20); Grid.SetRow(status, 3); g.Children.Add(status);
                var buttons = new Grid { ColumnDefinitions = new("Auto,*,Auto,Auto") };
                buttons.Children.Add(new Pill("Switch Controller", Pill.Kinds.Ghost, () => page.choose(Slot)) { Padding = new Thickness(24, 12) });
                var rm = new Pill("Remove", Pill.Kinds.Ghost, () => page.remove(Slot)) { Padding = new Thickness(26, 12), Margin = new Thickness(0, 0, 14, 0) }; Grid.SetColumn(rm, 2); buttons.Children.Add(rm);
                var go = new Pill("Enter Studio", Pill.Kinds.Primary, page.enter) { Padding = new Thickness(26, 12) }; Grid.SetColumn(go, 3); buttons.Children.Add(go);
                Grid.SetRow(buttons, 4); g.Children.Add(buttons);
            }
            else if (model != null)
            {
                var cell = new Grid(); spot.Margin = new Thickness(0, 4, 0, 26); cell.Children.Add(spot);
                var name = T.Label(model.Name, 16, null, FontWeight.SemiBold); name.VerticalAlignment = VerticalAlignment.Bottom; cell.Children.Add(name);
                Grid.SetRow(cell, 1); g.Children.Add(cell);
            }
            else
            {
                var add = new Pill("+ Add controller", Pill.Kinds.Primary, () => page.choose(Slot)) { Padding = asHero ? new Thickness(26, 12) : new Thickness(18, 9), HorizontalAlignment = HorizontalAlignment.Center };
                var empty = T.Stack(asHero ? 16 : 12, Silhouette(asHero ? 1.7 : 1), add);
                empty.VerticalAlignment = VerticalAlignment.Center; empty.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(empty, 1); g.Children.Add(empty);
            }
            layers.Lay(g, spot);
        }
        public void BeginMove(bool asHero) { layers.Leaving = layers.ArtNow; if (asHero != hero) Fill(asHero); layers.Words.Opacity = 0; }
        public void EndMove() { layers.Leaving = null; layers.InvalidateArrange(); }
    }

    /// <summary>A card's two layers: its words (with a blank cell where the art belongs) and the art, placed in that cell;
    /// while moving, between the cell it left (as insets from the card's edges) and the one it is going to.</summary>
    sealed class Layers(Dock owner, ControllerArt? art) : Panel
    {
        Control? spot;
        public Control Words { get; private set; } = new Border();
        /// <summary>Where the art was when the move began: its insets from the card's edges.</summary>
        public Thickness? Leaving { get; set; }
        public Thickness ArtNow => art == null ? default : new Thickness(art.Bounds.Left, art.Bounds.Top, Bounds.Width - art.Bounds.Right, Bounds.Height - art.Bounds.Bottom);
        public void Lay(Control words, Control cell)
        {
            Children.Clear(); Words = words; spot = cell; Children.Add(words); if (art != null) Children.Add(art);
        }
        protected override Size MeasureOverride(Size available)
        {
            Words.Measure(available); art?.Measure(available);
            return Words.DesiredSize;
        }
        protected override Size ArrangeOverride(Size final)
        {
            Words.Arrange(new Rect(final));
            if (art == null || spot == null) return final;
            var cell = spot.Bounds;
            for (Visual? v = spot.GetVisualParent(); v != null && v != this; v = v.GetVisualParent()) cell = cell.Translate(v.Bounds.Position);
            var place = cell;
            if (Leaving is { } a)
            {
                double k = owner.Progress; var b = new Thickness(cell.Left, cell.Top, final.Width - cell.Right, final.Height - cell.Bottom);
                double left = Motion.Lerp(a.Left, b.Left, k), top = Motion.Lerp(a.Top, b.Top, k), right = Motion.Lerp(a.Right, b.Right, k), bottom = Motion.Lerp(a.Bottom, b.Bottom, k);
                place = new Rect(left, top, Math.Max(0, final.Width - left - right), Math.Max(0, final.Height - top - bottom));
            }
            art.Arrange(place);
            return final;
        }
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
            var art = new ControllerArt(m.Id, shadow: false, small: true) { Height = 190 };
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
            using var doc = JsonDocument.Parse(File.ReadAllBytes(System.IO.Path.Combine(AppContent.Root, "Data", "catalog.json")));
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
