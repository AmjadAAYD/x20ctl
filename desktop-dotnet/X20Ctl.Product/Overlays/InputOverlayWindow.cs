using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product.Overlays;

/// <summary>
/// Transparent, always-on-top mini controller HUD for OBS / streamers.
/// Supports click-through mode (WS_EX_TRANSPARENT) or unlocked dragging. Never shown in ReviewSandbox.
/// </summary>
internal sealed class InputOverlayWindow : Window
{
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    const int GwlExStyle = -20;
    const int WsExTransparent = 0x00000020;
    const int WsExToolWindow = 0x00000080;

    readonly PcModeService? pcMode;
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    WindowsReader? fallbackReader;

    readonly Ellipse lDot = new() { Width = 8, Height = 8, Fill = Brushes.White };
    readonly Ellipse rDot = new() { Width = 8, Height = 8, Fill = Brushes.White };
    readonly Rectangle ltBar = new() { Width = 10, Height = 0, VerticalAlignment = VerticalAlignment.Bottom, Fill = DS.Brush("DS.AccentHi"), RadiusX = 3, RadiusY = 3 };
    readonly Rectangle rtBar = new() { Width = 10, Height = 0, VerticalAlignment = VerticalAlignment.Bottom, Fill = DS.Brush("DS.AccentHi"), RadiusX = 3, RadiusY = 3 };
    readonly Dictionary<ushort, Border> pips = new();
    readonly TextBlock statusPill = Kit.Text("X20CTL HUD", 11, "DS.TextSoft", FontWeights.SemiBold);
    readonly Border shell;
    bool clickThrough;

    public bool ClickThrough
    {
        get => clickThrough;
        set
        {
            clickThrough = value;
            ApplyExtendedStyle();
            statusPill.Text = value ? "HUD · Locked" : "HUD · Drag to move";
        }
    }

    public InputOverlayWindow(PcModeService? pcMode)
    {
        this.pcMode = pcMode;
        Title = "X20CTL Input Overlay";
        Width = 390;
        Height = 106;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        Left = 36;
        Top = 36;

        MouseLeftButtonDown += (_, _) => { if (!clickThrough) DragMove(); };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(TrigBar("LT", ltBar));
        row.Children.Add(StickRing("L", lDot));
        row.Children.Add(StickRing("R", rDot));
        row.Children.Add(TrigBar("RT", rtBar));

        var buttonsCol = new StackPanel { Margin = new(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        var botRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 5, 0, 0) };
        foreach (var (lbl, mask) in new (string, ushort)[] { ("A", PadButtons.A), ("B", PadButtons.B), ("X", PadButtons.X), ("Y", PadButtons.Y) })
            topRow.Children.Add(Pip(lbl, mask));
        foreach (var (lbl, mask) in new (string, ushort)[] { ("LB", PadButtons.LB), ("RB", PadButtons.RB), ("L3", PadButtons.L3), ("R3", PadButtons.R3) })
            botRow.Children.Add(Pip(lbl, mask));
        buttonsCol.Children.Add(topRow);
        buttonsCol.Children.Add(botRow);
        buttonsCol.Children.Add(statusPill.Also(t => t.Margin = new(0, 6, 0, 0)));
        row.Children.Add(buttonsCol);

        shell = new Border
        {
            CornerRadius = new(18),
            BorderThickness = new(1.2),
            Padding = new(14, 10, 14, 10),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(205, 8, 14, 28))),
            BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(140, 61, 139, 255))),
            Child = row,
        };
        Content = shell;

        SourceInitialized += (_, _) =>
        {
            ApplyExtendedStyle();
            ClickThrough = true;
        };
        Loaded += (_, _) =>
        {
            try { fallbackReader = new WindowsReader("XInput1_4.dll"); } catch { }
            timer.Tick += (_, _) => RefreshState();
            timer.Start();
        };
        Closed += (_, _) =>
        {
            timer.Stop();
            fallbackReader?.Dispose();
            fallbackReader = null;
        };
    }

    void ApplyExtendedStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int ex = GetWindowLong(hwnd, GwlExStyle) | WsExToolWindow;
        if (clickThrough) ex |= WsExTransparent;
        else ex &= ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, ex);
    }

    static Border TrigBar(string label, Rectangle bar) => new()
    {
        Width = 18,
        Height = 64,
        Margin = new(0, 0, 8, 0),
        CornerRadius = new(6),
        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(120, 18, 28, 52))),
        BorderBrush = DS.Brush("DS.Hairline"),
        BorderThickness = new(1),
        ToolTip = label,
        Child = bar,
    };

    static Border StickRing(string label, Ellipse dot)
    {
        var canvas = new Canvas { Width = 54, Height = 54 };
        canvas.Children.Add(new Ellipse { Width = 50, Height = 50, Stroke = DS.Brush("DS.Hairline"), StrokeThickness = 1.2 }.Also(e => { Canvas.SetLeft(e, 2); Canvas.SetTop(e, 2); }));
        Canvas.SetLeft(dot, 23);
        Canvas.SetTop(dot, 23);
        canvas.Children.Add(dot);
        return new Border
        {
            Width = 58,
            Height = 64,
            Margin = new(0, 0, 8, 0),
            CornerRadius = new(12),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(110, 14, 24, 48))),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { canvas, Kit.Text(label, 9.5, "DS.TextMuted").Also(t => t.HorizontalAlignment = HorizontalAlignment.Center) },
            }
        };
    }

    Border Pip(string label, ushort mask)
    {
        var b = new Border
        {
            MinWidth = 28,
            Height = 20,
            Margin = new(0, 0, 4, 0),
            Padding = new(5, 0, 5, 0),
            CornerRadius = new(6),
            BorderThickness = new(1),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(80, 18, 28, 50))),
            BorderBrush = DS.Brush("DS.Hairline"),
            Child = Kit.Text(label, 10.5, "DS.TextSoft", FontWeights.SemiBold).Also(t =>
            {
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
            })
        };
        pips[mask] = b;
        return b;
    }

    void RefreshState()
    {
        PadState pad = default;
        if (pcMode is { State: PcModeState.Active, Latest: { } latest })
        {
            pad = latest.Output;
            statusPill.Text = pcMode.TournamentMode ? "TOURNAMENT" : clickThrough ? "PC MODE · LOCKED" : "PC MODE · DRAG";
        }
        else if (fallbackReader != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!PcModeService.IsVirtual(i) && fallbackReader.Read(i, out var x))
                {
                    pad = new PadState(x.Pad.Buttons, x.Pad.LT, x.Pad.RT, x.Pad.LX, x.Pad.LY, x.Pad.RX, x.Pad.RY);
                    break;
                }
            }
            statusPill.Text = clickThrough ? "LIVE PAD · LOCKED" : "LIVE PAD · DRAG";
        }

        Canvas.SetLeft(lDot, 23 + pad.LeftX / 32767.0 * 19);
        Canvas.SetTop(lDot, 23 - pad.LeftY / 32767.0 * 19);
        Canvas.SetLeft(rDot, 23 + pad.RightX / 32767.0 * 19);
        Canvas.SetTop(rDot, 23 - pad.RightY / 32767.0 * 19);
        ltBar.Height = pad.LeftTrigger / 255.0 * 60.0;
        rtBar.Height = pad.RightTrigger / 255.0 * 60.0;

        var activeBrush = DS.Brush("DS.Accent");
        var idleBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(80, 18, 28, 50)));
        foreach (var (mask, b) in pips)
            b.Background = (pad.Buttons & mask) != 0 ? activeBrush : idleBrush;
    }
}
