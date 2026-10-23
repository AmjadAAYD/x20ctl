using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using X20Ctl.Product.Scanner;

namespace X20Ctl.Product;

public enum VerifyOutcome { Match, Mismatch, Unconfirmed, NoController }

/// <summary>What the scan on entering a controller found. Detected is a model id when Windows named the pad as one.</summary>
public sealed record DetectedController(string Name, string? ModelId, string Detail);
public sealed record VerifyResult(VerifyOutcome Outcome, string? DetectedId, string? DetectedName, string Detail, IReadOnlyList<DetectedController>? Controllers = null, string? Transport = null);

public static class ControllerVerify
{
    // model ids by the name ScanFlow.DetectModel returns
    private static readonly Dictionary<string, string> ByName = new()
    {
        ["Dune / D15"] = "dune", ["X20 Pro"] = "x20_pro", ["X20"] = "x20", ["X15"] = "x15", ["X10"] = "x10", ["X05 Pro"] = "x05_pro", ["X05"] = "x05", ["D10"] = "d10",
    };
    // identities seen on real hardware that name a model outright (docs/00-findings.md); the shared Xbox identities never do
    private static readonly Dictionary<(int, int), string> ById = new() { [(0x0079, 0x181C)] = "x20", [(0x1D57, 0xFA60)] = "x20", [(0x413D, 0x2131)] = "x20_pro" };

    /// <summary>Asks Windows what is plugged in and whether it names itself as the model the player clicked. A name or ID is
    /// the pad's own claim; the shared Xbox identities prove nothing, so such a pad comes back Unconfirmed, never Match.</summary>
    public static async Task<VerifyResult> Run(string modelId)
    {
        Inventory inventory;
        try { inventory = await Inventory.Bound(Task.Run(() => WindowsInventory.Read(false)), 8000); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException)
        { return new(VerifyOutcome.NoController, null, null, "Windows would not list devices."); }
        var found = Describe(inventory);
        string? transport = ScanFlow.DetectTransport(inventory);
        VerifyResult Result(VerifyOutcome outcome, string? id, string? name, string detail) => new(outcome, id, name, detail, found, transport);
        string? id = null, name = null;
        if (ScanFlow.DetectModel(inventory) is { } named && ByName.TryGetValue(named.Model, out var byName)) { id = byName; name = named.Name; }
        else foreach (var i in inventory.interfaces) if (ById.TryGetValue((i.vid, i.pid), out var byId)) { id = byId; name = $"{i.vid:X4}:{i.pid:X4}"; break; }
        if (id == modelId) return Result(VerifyOutcome.Match, id, name, name ?? "");
        if (id != null) return Result(VerifyOutcome.Mismatch, id, name, name ?? "");
        bool pad = found.Count > 0;
        return pad ? Result(VerifyOutcome.Unconfirmed, null, null, "A gamepad is connected but doesn't name its model.")
                   : Result(VerifyOutcome.NoController, null, null, "No gamepad was listed.");
    }
    public static IReadOnlyList<DetectedController> Describe(Inventory inventory)
    {
        var found = new List<DetectedController>();
        foreach (var group in Inventory.Group(inventory.interfaces))
        {
            var subset = new Inventory { interfaces = group };
            var named = ScanFlow.DetectModel(subset);
            var pad = group.FirstOrDefault(i => i.kind == "HID" && i.usagePage == 1 && i.usage is 4 or 5);
            var known = group.FirstOrDefault(i => ById.ContainsKey((i.vid, i.pid)));
            pad ??= group.FirstOrDefault(i => i.vid == 0x045E && (i.product.Contains("Xbox", StringComparison.OrdinalIgnoreCase) || i.product.Contains("Controller", StringComparison.OrdinalIgnoreCase)));
            if (pad == null && known == null && named == null) continue;
            var device = pad ?? known ?? group[0];
            string? id = named is { } n && ByName.TryGetValue(n.Model, out var mapped) ? mapped : known != null ? ById[(known.vid, known.pid)] : null;
            string name = named?.Name ?? (!string.IsNullOrWhiteSpace(device.product) ? device.product : "Windows gamepad");
            found.Add(new(name, id, $"{device.kind} · {device.vid:X4}:{device.pid:X4}" + (id == null ? " · model unconfirmed" : " · reported identity")));
        }
        return found;
    }

}

/// <summary>The popup shown when a controller is opened: a glowing gamepad outline that a light runs round while Windows is
/// asked who is connected, then the verdict. Decisions are the host's; this only shows and reports which button was pressed.</summary>
public sealed class ControllerVerifyView : Grid
{
    private readonly TextBlock status = Kit.Text("", 15, "DS.TextMuted");
    private readonly TextBlock title = Kit.Text("", 20, "DS.Text", FontWeights.SemiBold);
    private readonly WrapPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 20, 0, 0) };
    private readonly Path rim = new() { Data = GamepadGlyph.Outline, StrokeThickness = 8.5, StrokeLineJoin = PenLineJoin.Round, Stroke = new SolidColorBrush(Color.FromRgb(240, 245, 255)) };
    private readonly Path runner = new() { Data = GamepadGlyph.Outline, StrokeThickness = 8.5, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly SolidColorBrush runnerBrush = new(Color.FromRgb(120, 215, 255));
    private readonly System.Windows.Media.Effects.DropShadowEffect rimGlow = new() { Color = Colors.White, BlurRadius = 20, ShadowDepth = 0, Opacity = .6 };
    private readonly Ellipse[] dots = new Ellipse[3];
    private readonly TextBlock verdict = new() { FontFamily = DS.Display, FontSize = 54, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
    private readonly Border card = new();
    private readonly StackPanel deviceList = new();
    private readonly TextBlock deviceCount = Kit.Text("Scanning", 13, "DS.TextMuted");
    private readonly TranslateTransform scanMove = new();
    private readonly Rectangle scanBeam = new() { Height = 24, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly bool animate;
    private readonly string selectedName;

    public event Action? Cancelled;
    public void RaiseCancel() => Cancelled?.Invoke();

    public ControllerVerifyView(string modelId, string modelName, bool animate)
    {
        this.animate = animate; selectedName = modelName;
        Background = new SolidColorBrush(Color.FromArgb(170, 2, 6, 18));
        var accent = Color.FromRgb(86, 190, 255);
        var size = GamepadGlyph.Size;
        // dark glass body, white tube rim with a soft glow, a light running round it
        rim.Fill = new LinearGradientBrush(Color.FromRgb(46, 54, 76), Color.FromRgb(22, 27, 42), 90);
        rim.Effect = rimGlow;
        runner.Stroke = runnerBrush;
        double dash = GamepadGlyph.Length * .16 / runner.StrokeThickness, gap = GamepadGlyph.Length * .84 / runner.StrokeThickness;
        runner.StrokeDashArray = [dash, gap];
        runner.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accent, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 };
        double pad = GamepadGlyph.Pad;
        var art = new Grid { Width = size.Width + pad * 2, Height = size.Height + pad * 2 };
        // Padding must be inside the Path's drawing box: a margin cannot protect a
        // stroke centred on y=0 from the shape's own layout clip.
        var padded = GamepadGlyph.Outline.Clone();
        padded.Transform = new TranslateTransform(pad, pad); padded.Freeze();
        foreach (var layer in new Path[] { rim, runner })
        {
            layer.Data = padded; layer.Width = art.Width; layer.Height = art.Height;
            layer.Stretch = Stretch.None; art.Children.Add(layer);
        }
        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, -150, 0, 0) };
        for (int i = 0; i < 3; i++)
        {
            dots[i] = new Ellipse { Width = 9, Height = 9, Margin = new(5, 0, 5, 0), Fill = Brushes.White, Opacity = .35,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.White, BlurRadius = 10, ShadowDepth = 0, Opacity = 1 } };
            dotRow.Children.Add(dots[i]);
        }
        var scanClip = padded.Clone();
        var scanSurface = new Grid { Clip = scanClip, IsHitTestVisible = false };
        scanBeam.Fill = new LinearGradientBrush(Color.FromArgb(0, 70, 170, 255), Color.FromArgb(190, 110, 220, 255), 90);
        scanBeam.RenderTransform = scanMove; scanSurface.Children.Add(scanBeam);
        art.Children.Add(scanSurface); art.Children.Add(dotRow); art.Children.Add(verdict);
        var stage = new Viewbox { MaxHeight = 300, Stretch = Stretch.Uniform, Child = art, Margin = new(0, 22, 0, 14) };
        title.TextWrapping = status.TextWrapping = TextWrapping.Wrap;
        title.HorizontalAlignment = status.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextAlignment = status.TextAlignment = TextAlignment.Center;
        var left = new Grid { Margin = new(0, 0, 26, 0) };
        left.RowDefinitions.Add(new() { Height = GridLength.Auto }); left.RowDefinitions.Add(new()); left.RowDefinitions.Add(new() { Height = GridLength.Auto }); left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var scanHeading=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center };
        scanHeading.Children.Add(Kit.Text("CONTROLLER CHECK",10,"DS.TextMuted",FontWeights.SemiBold).Also(t=>{t.HorizontalAlignment=HorizontalAlignment.Center;t.Margin=new(0,0,0,8);}));
        scanHeading.Children.Add(title);
        left.Children.Add(scanHeading); Grid.SetRow(stage, 1); left.Children.Add(stage); Grid.SetRow(status, 2); left.Children.Add(status); Grid.SetRow(buttons, 3); left.Children.Add(buttons);
        var right = new DockPanel { Margin = new(26, 0, 0, 0) };
        var listHead = new DockPanel { Margin = new(0, 0, 0, 18) }; DockPanel.SetDock(deviceCount, Dock.Right); listHead.Children.Add(deviceCount); listHead.Children.Add(Kit.Text("Connected controllers", 22, "DS.Text", FontWeights.SemiBold));
        DockPanel.SetDock(listHead, Dock.Top); right.Children.Add(listHead);
        var note = Kit.Text("Windows-reported devices · an unknown model stays unconfirmed.", 12, "DS.TextMuted"); note.TextWrapping = TextWrapping.Wrap; note.Margin = new(0, 14, 0, 0); DockPanel.SetDock(note, Dock.Bottom); right.Children.Add(note);
        right.Children.Add(new ScrollViewer { Content = deviceList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var columns = new Grid(); columns.ColumnDefinitions.Add(new() { Width = new GridLength(1.2, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        columns.Children.Add(left); Grid.SetColumn(right, 1); columns.Children.Add(right);
        var divider = new Border { Width = 1, Background = new SolidColorBrush(Color.FromArgb(45, 140, 190, 255)), HorizontalAlignment = HorizontalAlignment.Left }; Grid.SetColumn(divider, 1); columns.Children.Add(divider);
        card.CornerRadius = new(24); card.Padding = new(30); card.MaxWidth = 1500; card.Height = 540; card.Margin = new(28);
        card.Background = new LinearGradientBrush(Color.FromRgb(17, 29, 54), Color.FromRgb(7, 14, 32), 90);
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(130, accent.R, accent.G, accent.B)); card.BorderThickness = new(1);
        card.HorizontalAlignment = HorizontalAlignment.Stretch; card.VerticalAlignment = VerticalAlignment.Center;
        card.Child = columns; Children.Add(card);
        SizeChanged += (_, _) => card.Height = Math.Clamp(ActualHeight - 48, 300, 540);
        BeginScan();
        Unloaded += (_, _) => StopAnimation();
    }

    public void BeginScan()
    {
        StopAnimation(); title.Text = "Finding your controller";
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(130, 86, 190, 255));
        rimGlow.Color = Colors.White; scanMove.Y = 0;
        status.Text = $"Reading Windows gamepad inventory · selected {selectedName}";
        deviceCount.Text = "Scanning"; deviceList.Children.Clear();
        foreach (var width in new[] { 220.0, 170.0, 200.0 })
            deviceList.Children.Add(new Border { Height = 64, Margin = new(0, 0, 0, 12), Padding = new(18), CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(50, 100, 145, 205)), Child = new Border { Width = width, MaxWidth = width, Height = 9, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, CornerRadius = new(4), Background = new SolidColorBrush(Color.FromArgb(70, 165, 200, 245)) } });
        buttons.Children.Clear(); buttons.Children.Add(Action("Cancel scan", false, RaiseCancel));
        runner.Visibility = scanBeam.Visibility = Visibility.Visible; verdict.Opacity = 0;
        rim.Stroke = new SolidColorBrush(Color.FromRgb(230, 240, 255));
        foreach (var d in dots) { d.Visibility = Visibility.Visible; d.Opacity = .35; }
        if (animate) Start();
    }
    void StopAnimation()
    {
        scanMove.BeginAnimation(TranslateTransform.YProperty, null);
        runner.BeginAnimation(Shape.StrokeDashOffsetProperty, null); runnerBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        rimGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
        foreach (var d in dots) if (d != null) d.BeginAnimation(OpacityProperty, null);
    }

    private void Start()
    {
        var forever = RepeatBehavior.Forever;
        scanMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(30, 310, TimeSpan.FromSeconds(1.7)) { AutoReverse = true, RepeatBehavior = forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        // the light runs the whole way round: the dash and its gap add up to one lap
        double lap = GamepadGlyph.Length / runner.StrokeThickness;
        runner.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(0, -lap, TimeSpan.FromSeconds(2.4)) { RepeatBehavior = forever });
        runnerBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Color.FromRgb(120, 215, 255), Color.FromRgb(190, 130, 255), TimeSpan.FromSeconds(2.4)) { RepeatBehavior = forever, AutoReverse = true });
        rimGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(.3, .75, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = forever, AutoReverse = true, EasingFunction = new SineEase() });
        for (int i = 0; i < 3; i++)
            dots[i].BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = forever, BeginTime = TimeSpan.FromMilliseconds(i * 220), Duration = TimeSpan.FromSeconds(1.1),
                KeyFrames = { new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.3)), new LinearDoubleKeyFrame(.3, KeyTime.FromPercent(.7)) },
            });
    }

    private Button Action(string text, bool primary, Action click)
    {
        var b = new Button { Style = DS.Style(primary ? "PrimaryButton" : "ActionButton"), Height = 42, MinHeight = 0, Padding = new(22, 0, 22, 0), Margin = new(5, 0, 5, 0), Content = text };
        b.Click += (_, _) => click(); return b;
    }

    /// <summary>Shows the verdict. enter = go on into the Studio, switchTo = open it as the model Windows named, rescan = ask again.</summary>
    public void ShowResult(VerifyResult r, string modelName, string? detectedName, Action enter, Action<string> switchTo, Action rescan)
    {
        StopAnimation(); scanBeam.Visibility = Visibility.Collapsed;
        runner.BeginAnimation(Shape.StrokeDashOffsetProperty, null); runnerBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        rimGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
        foreach (var d in dots) { d.BeginAnimation(OpacityProperty, null); d.Visibility = Visibility.Collapsed; }
        Color tone = r.Outcome switch { VerifyOutcome.Match => Color.FromRgb(90, 220, 150), VerifyOutcome.Mismatch => Color.FromRgb(255, 105, 95), VerifyOutcome.Unconfirmed => Color.FromRgb(255, 190, 80), _ => Color.FromRgb(160, 175, 210) };
        // the whole outline takes the verdict's colour, and the running light is put away
        rim.Stroke = new SolidColorBrush(tone);
        rimGlow.Color = tone; rimGlow.Opacity = .8;
        runner.Visibility = Visibility.Collapsed;
        verdict.Text = r.Outcome switch { VerifyOutcome.Match => "✓", VerifyOutcome.Mismatch => "✕", VerifyOutcome.Unconfirmed => "?", _ => "…" };
        verdict.Foreground = new SolidColorBrush(tone); verdict.Margin = new(0, -150, 0, 0); verdict.Opacity = 1;
        card.BorderBrush = new SolidColorBrush(Color.FromArgb(190, tone.R, tone.G, tone.B));
        deviceList.Children.Clear();
        var found = r.Controllers ?? [];
        deviceCount.Text = found.Count == 1 ? "1 found" : $"{found.Count} found";
        if (found.Count == 0)
        {
            var empty = new StackPanel { Margin = new(0, 10, 0, 0) };
            empty.Children.Add(Kit.Text("Let's get you connected",20,"DS.Text",FontWeights.SemiBold));
            var hint = Kit.Text("Choose the connection your controller supports, then scan again.",13,"DS.TextMuted");hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new(0,7,0,16);empty.Children.Add(hint);
            foreach(var (glyph,label,detail) in new[]{("usb","USB cable","Use a data-capable cable, not a charge-only cable."),("receiver","Wireless receiver","Plug the controller's receiver into a USB port."),("bluetooth","Bluetooth","Pair the controller in Windows Bluetooth settings.")})
            {
                var text=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
                text.Children.Add(Kit.Text(label,15,"DS.Text",FontWeights.SemiBold));
                text.Children.Add(Kit.Text(detail,12,"DS.TextMuted").Also(t=>{t.TextWrapping=TextWrapping.Wrap;t.Margin=new(0,5,0,0);}));
                var vector=new Path { Data=Geometry.Parse(glyph switch {
                    "usb"=>"M 8,4 L 16,4 L 16,12 L 8,12 Z M 10,1 L 10,4 M 14,1 L 14,4 M 12,12 L 12,17 Q 12,22 7,22 L 3,22",
                    "receiver"=>"M 8,12 L 16,12 L 16,23 L 8,23 Z M 10,8 L 14,8 L 14,12 M 8,6 Q 12,2 16,6 M 5,3 Q 12,-3 19,3",
                    _=>"M 12,1 L 18,6 L 6,17 M 6,6 L 18,17 L 12,23 L 12,1" }),Stroke=new SolidColorBrush(Color.FromRgb(166,211,255)),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round };
                var iconArt=new Canvas { Width=24,Height=24,Children={vector} };
                var icon=new Border { Width=42,Height=42,CornerRadius=new(12),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,Background=new LinearGradientBrush(Color.FromArgb(120,55,103,157),Color.FromArgb(70,22,48,82),90),BorderBrush=new SolidColorBrush(Color.FromArgb(65,140,190,245)),BorderThickness=new(1),Child=new Viewbox { Width=22,Height=22,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=iconArt } };
                var content=new Grid();content.ColumnDefinitions.Add(new(){Width=new GridLength(58)});content.ColumnDefinitions.Add(new());content.Children.Add(icon);Grid.SetColumn(text,1);content.Children.Add(text);
                empty.Children.Add(new Border { MinHeight=76,Padding=new(16,12,16,12),Margin=new(0,0,0,10),CornerRadius=new(14),Background=new LinearGradientBrush(Color.FromArgb(70,55,82,120),Color.FromArgb(35,24,39,65),90),BorderBrush=new SolidColorBrush(Color.FromArgb(50,150,190,240)),BorderThickness=new(1),Child=content });
            }
            deviceList.Children.Add(empty);
        }
        foreach (var device in found)
        {
            bool selected = device.ModelId != null && device.ModelId == r.DetectedId && r.Outcome == VerifyOutcome.Match;
            var name = Kit.Text(device.Name, 16, "DS.Text", FontWeights.SemiBold); name.TextWrapping = TextWrapping.Wrap;
            var info = Kit.Text(device.Detail, 12, "DS.TextMuted"); info.TextWrapping = TextWrapping.Wrap; info.Margin = new(0, 6, 0, 0);
            deviceList.Children.Add(new Border { Padding = new(18), Margin = new(0, 0, 0, 12), CornerRadius = new(14), Background = new SolidColorBrush(Color.FromArgb(selected ? (byte)70 : (byte)32, 50, 150, 220)), BorderBrush = new SolidColorBrush(Color.FromArgb(100, 100, 180, 230)), BorderThickness = new(1), Child = new StackPanel { Children = { name, info } } });
        }
        buttons.Children.Clear();
        switch (r.Outcome)
        {
            case VerifyOutcome.Match:
                title.Text = $"Model reported · {modelName}"; status.Text = (r.DetectedName ?? "") + " · opening";
                break;
            case VerifyOutcome.Mismatch:
                string other = ModelLabel(r.DetectedId!, detectedName);
                title.Text = $"This is a {other}, not a {modelName}"; status.Text = $"The controller names itself \"{r.DetectedName}\". {modelName} settings stay off it.";
                buttons.Children.Add(Action($"Open as {other}", true, () => switchTo(r.DetectedId!)));
                buttons.Children.Add(Action("Cancel", false, () => Cancelled?.Invoke()));
                break;
            case VerifyOutcome.Unconfirmed:
                title.Text = "Controller found"; status.Text = $"It doesn't name its model, so it can't be confirmed as a {modelName}.";
                buttons.Children.Add(Action("Continue", true, enter));
                buttons.Children.Add(Action("Cancel", false, () => Cancelled?.Invoke()));
                break;
            default:
                title.Text = "No controller found"; status.Text = $"Connect your {modelName}, or edit offline.";
                buttons.Children.Add(Action("Continue offline", true, enter));
                buttons.Children.Add(Action("Scan again", false, rescan));
                break;
        }
        if (buttons.Children.Count > 0) buttons.Children[0].Focus();
    }

    private static string ModelLabel(string id, string? fallback) => id switch
    {
        "x20" => "X20", "x20_pro" => "X20 Pro", "x15" => "X15", "x10" => "X10", "x05" => "X05", "x05_pro" => "X05 Pro", "d10" => "D10", "dune" => "Dune 8K", _ => fallback ?? id,
    };
}
