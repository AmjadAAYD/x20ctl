using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Motor strength as a preview: each grip lights along its own edge and pulses with the setting. Nothing is read from or sent to the motors.</summary>
internal sealed class VibrationPage : UserControl
{
    static readonly string[] Regions = ["All", "Both grips", "Left grip", "Right grip"];
    readonly StudioState state; string region = "All"; readonly OutlineLayer layer; readonly TextBlock value;
    public VibrationPage(StudioState state)
    {
        this.state = state;
        layer = new OutlineLayer(state.Model.Id) { Strength = state.VibrationPreview, Region = region };
        value = T.Label(state.VibrationPreview + "%", 64, null, FontWeight.SemiBold);
        Build();
    }

    void Build()
    {
        var page = new Grid { Margin = new Thickness(36, 2, 36, 14), RowDefinitions = new("Auto,*,Auto") };
        var notice = Parts.Notice("Vibration preview", "Native configuration is unavailable for this model. Preview stays local."); notice.Margin = new Thickness(0, 0, 0, 12); page.Children.Add(notice);

        var body = new Grid { ColumnDefinitions = new("1.45*,1*"), MinHeight = 520 }; Grid.SetRow(body, 1);
        var art = new ControllerArt(state.Model.Id) { Margin = new Thickness(8) }; art.Children.Add(layer);
        body.Children.Add(T.Card(art, 10));

        var slider = new FlatSlider { Minimum = 0, Maximum = 100, Value = state.VibrationPreview };
        slider.ValueChanged += v => Set((int)v, false);
        var presets = T.Line(8, new[] { 0, 25, 50, 75, 100 }.Select(p => (Control)new Pill(p == 0 ? "Off" : p + "%", Pill.Kinds.Ghost, () => { slider.Value = p; Set(p, false); }) { Padding = new Thickness(15, 8) }).ToArray());
        bool triggers = state.Model.Id is "x20_pro" or "dune";
        var segs = Segmented.Make(triggers ? [.. Regions, "Triggers"] : Regions, region, v => { region = v; layer.Region = v; Build(); }, 13.5);
        var right = T.Stack(12,
            T.Label("VIBRATION PREVIEW", 14, T.Soft), value, T.Label("Visual preview only · Native settings unavailable", 16, T.Soft), new Border { Height = 4 }, slider, presets,
            T.Label("Preview region", 15, T.Soft), segs,
            T.Label("Region selection changes the illustration only. This backend has one linked strength; independent trigger tuning is unavailable.", 13.5, T.Soft),
            new Pill("Test on controller", Pill.Kinds.Ghost) { Enabled = false, Padding = new Thickness(22, 11), HorizontalAlignment = HorizontalAlignment.Left }, T.Label("Physical output unavailable", 14, T.Soft));
        right.VerticalAlignment = VerticalAlignment.Center;
        var rc = T.Card(right, 30); rc.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(rc, 1); body.Children.Add(rc);
        page.Children.Add(body);

        var foot = T.Label("Preview animation · No motor telemetry or physical vibration is generated", 14, T.Soft); foot.Margin = new Thickness(6, 10, 0, 0); Grid.SetRow(foot, 2); page.Children.Add(foot);
        Content = new ScrollViewer { Content = page };
    }

    void Set(int v, bool rebuild) { state.VibrationPreview = v; layer.Strength = v; value.Text = v + "%"; if (rebuild) Build(); }
}

/// <summary>The animated strokes along the pad's own edges (vibration-outlines.json): a soft glow that breathes with the strength and a bright sweep that runs along each stroke.</summary>
internal sealed class OutlineLayer : Control
{
    sealed record Stroke(string Side, string Part, Point[] Points, double[] Along, double Length);
    static readonly Dictionary<string, List<Stroke>> All = Load();
    readonly List<Stroke> strokes; readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) }; readonly DateTime start = DateTime.UtcNow;
    public double Strength { get; set; } = 70; public string Region { get; set; } = "All";
    public OutlineLayer(string modelId) { strokes = All.GetValueOrDefault(modelId) ?? []; IsHitTestVisible = false; timer.Tick += (_, _) => InvalidateVisual(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); timer.Start(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { timer.Stop(); base.OnDetachedFromVisualTree(e); }

    static Dictionary<string, List<Stroke>> Load()
    {
        var map = new Dictionary<string, List<Stroke>>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContent.Root, "Data", "vibration-outlines.json")));
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateObject())
            {
                var list = new List<Stroke>();
                foreach (var s in m.Value.EnumerateArray())
                {
                    var pts = s.GetProperty("points").EnumerateArray().Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToArray();
                    var along = new double[pts.Length]; double len = 0;
                    for (int i = 1; i < pts.Length; i++) { len += Math.Sqrt(Math.Pow((pts[i].X - pts[i - 1].X) * 1536, 2) + Math.Pow((pts[i].Y - pts[i - 1].Y) * 1024, 2)); along[i] = len; }
                    list.Add(new(s.GetProperty("side").GetString()!, s.GetProperty("part").GetString()!, pts, along, len));
                }
                map[m.Name] = list;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException) { }
        return map;
    }

    bool Lit(Stroke s) => Region switch { "Left grip" => s.Side == "left" && s.Part == "grip", "Right grip" => s.Side == "right" && s.Part == "grip", "Both grips" => s.Part == "grip", _ => true };

    public override void Render(DrawingContext ctx)
    {
        if (Parent is not ControllerArt art || Strength <= 0) return;
        double t = (DateTime.UtcNow - start).TotalSeconds, level = Strength / 100.0, pulse = .55 + .45 * Math.Sin(t * (3 + 8 * level));
        foreach (var s in strokes.Where(Lit))
        {
            var pts = s.Points.Select(p => { var m = art.Map(p.X, p.Y); return new Point(m.X - Bounds.X, m.Y - Bounds.Y); }).ToArray();
            var geo = Poly(pts, 0, pts.Length - 1);
            byte glow = (byte)(40 + 130 * level * pulse);
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(glow / 2), 255, 190, 80)), 11 * (.6 + level), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(90 + 140 * level), 255, 205, 110)), 3.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
            // the sweep: a bright stretch about a third of the stroke long
            double head = ((t * (.25 + .5 * level)) % 1.0) * s.Length * 1.3, tail = head - s.Length * .33; int a = 0, b = 0;
            for (int i = 0; i < s.Along.Length; i++) { if (s.Along[i] <= Math.Max(tail, 0)) a = i; if (s.Along[i] <= Math.Min(head, s.Length)) b = i; }
            if (b > a) ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 238, 190)), 4.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), Poly(pts, a, b));
        }
    }

    static StreamGeometry Poly(Point[] p, int from, int to)
    {
        var g = new StreamGeometry(); using var c = g.Open(); c.BeginFigure(p[from], false);
        for (int i = from + 1; i <= to; i++) c.LineTo(p[i]);
        c.EndFigure(false); return g;
    }
}
