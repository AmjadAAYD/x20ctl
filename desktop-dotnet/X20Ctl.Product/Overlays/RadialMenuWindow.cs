using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone;

namespace X20Ctl.Product.Overlays;

/// <summary>
/// 8-slice centered Radial Shortcut / Weapon Wheel controlled by Right Stick angle while holding a pad chord.
/// Executes configured system or keyboard/mouse actions cleanly upon release. Never shown in ReviewSandbox.
/// </summary>
internal sealed class RadialMenuWindow : Window
{
    readonly Canvas canvas = new() { Width = 360, Height = 360 };
    readonly List<Border> cards = new();
    readonly RadialMenuConfig config;
    int selectedSlice = -1;

    public int SelectedSlice => selectedSlice;
    public RadialSlice? ActiveSlice => selectedSlice >= 0 && selectedSlice < config.Slices.Count ? config.Slices[selectedSlice] : null;

    public RadialMenuWindow(RadialMenuConfig config)
    {
        this.config = config;
        Title = "X20CTL Radial Menu";
        Width = 380;
        Height = 380;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        const double cx = 180, cy = 180, ringRadius = 122;
        canvas.Children.Add(new Ellipse
        {
            Width = 260,
            Height = 260,
            Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(205, 8, 14, 30))),
            Stroke = DS.Brush("DS.Hairline"),
            StrokeThickness = 1.5,
        }.Also(e => { Canvas.SetLeft(e, cx - 130); Canvas.SetTop(e, cy - 130); }));

        for (int i = 0; i < RadialMenuConfig.SliceCount; i++)
        {
            double angle = -Math.PI / 2 + i * (Math.PI * 2 / RadialMenuConfig.SliceCount);
            double px = cx + Math.Cos(angle) * ringRadius;
            double py = cy + Math.Sin(angle) * ringRadius;
            var card = new Border
            {
                Width = 96,
                Height = 36,
                CornerRadius = new(10),
                BorderThickness = new(1.2),
                Padding = new(8, 0, 8, 0),
                Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(220, 14, 24, 52))),
                BorderBrush = DS.Brush("DS.Hairline"),
                Child = Kit.Text(config.Slices[i].Label, 12, "DS.Text", FontWeights.SemiBold).Also(t =>
                {
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                }),
            };
            Canvas.SetLeft(card, px - 48);
            Canvas.SetTop(card, py - 18);
            canvas.Children.Add(card);
            cards.Add(card);
        }

        canvas.Children.Add(new Border
        {
            Width = 96,
            Height = 96,
            CornerRadius = new(48),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(230, 10, 18, 40))),
            BorderBrush = DS.Brush("DS.AccentHi"),
            BorderThickness = new(1.5),
            Child = Kit.Text("RS Pick\nRelease Run", 11.5, "DS.TextSoft", FontWeights.SemiBold).Also(t =>
            {
                t.TextAlignment = TextAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
                t.HorizontalAlignment = HorizontalAlignment.Center;
            }),
        }.Also(b => { Canvas.SetLeft(b, cx - 48); Canvas.SetTop(b, cy - 48); }));

        Content = canvas;
    }

    public void UpdateAim(short rx, short ry)
    {
        double nx = rx / 32768.0, ny = ry / 32768.0;
        double mag = Math.Sqrt(nx * nx + ny * ny);
        if (mag < 0.35)
        {
            Highlight(-1);
            return;
        }
        // Screen Y grows down while XInput +Y is Up
        double angle = Math.Atan2(-ny, nx) + Math.PI / 2;
        if (angle < 0) angle += Math.PI * 2;
        int idx = ((int)Math.Round(angle / (Math.PI * 2) * RadialMenuConfig.SliceCount)) % RadialMenuConfig.SliceCount;
        Highlight(idx);
    }

    void Highlight(int idx)
    {
        if (idx == selectedSlice) return;
        selectedSlice = idx;
        for (int i = 0; i < cards.Count; i++)
        {
            bool on = i == idx;
            cards[i].Background = on
                ? DS.Brush("DS.Accent")
                : new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(220, 14, 24, 52)));
            cards[i].BorderBrush = on ? Brushes.White : DS.Brush("DS.Hairline");
        }
    }
}
