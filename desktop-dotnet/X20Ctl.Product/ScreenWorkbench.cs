using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using X20Ctl.Zone.Screen;
using X20Ctl.Product.Scanner;
namespace X20Ctl.Product;

/// <summary>On-demand 160x80 LCD artwork studio with synchronized hardware preview and White flash mode upload.</summary>
public sealed class ScreenWorkbench
{
    static readonly Color LcdBacklightColor = Color.FromRgb(0x16, 0x1A, 0x24);
    static readonly Brush LcdBacklightBrush = FreezeBrush(new SolidColorBrush(LcdBacklightColor));

    static Brush FreezeBrush(Brush b) { b.Freeze(); return b; }

    public Grid Body { get; } = new();
    public Grid Shelf { get; } = new();
    public Button Upload { get; }
    public Button Reset { get; }
    public Button Import { get; }
    public Button Preview { get; }
    internal Button Retry => retry;
    internal Button Cancel => cancel;
    internal bool IsBusy => busy;
    internal string StatusText => status.Text;
    public byte[]? EncodedAsset => asset?.ToArray();

    readonly Image image = new() { Stretch = Stretch.Uniform, Opacity = 0.92 };
    readonly Image controllerImage = new() { Stretch = Stretch.UniformToFill, Opacity = 0.91 };
    readonly TextBlock details = Kit.Text("Choose an image or animated template", 12.5, "DS.Text", FontWeights.SemiBold);
    readonly TextBlock status = Kit.Text("Controller", 12.5, "DS.TextSoft");
    readonly TextBlock sizeText = Kit.Text("Upload 0 / 3,145,728 bytes", 11.5, "DS.TextMuted");
    readonly TextBlock notice = Kit.Text("", 11.5, "DS.TextMuted");
    readonly TextBox portName = new() { Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
    readonly ProgressBar transferProgress = new() { Minimum = 0, Maximum = 100, Height = 5, Margin = new(0, 6, 0, 4) };

    readonly CheckBox animation = new() { Content = "Animation", Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center };
    readonly CheckBox simulate = new() { Content = "Simulate", IsChecked = false, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center };
    readonly CheckBox confirmReset = new() { Content = "Confirm reset", Foreground = DS.Brush("DS.Text") };
    readonly CheckBox before = new() { Content = "Before: current screen noted", Foreground = DS.Brush("DS.Text") };
    readonly CheckBox after = new() { Content = "After: image and colors match", Foreground = DS.Brush("DS.Text") };

    readonly Slider zoom = new() { Minimum = 1, Maximum = 4, Value = 1, SmallChange = 0.05, LargeChange = 0.25, Style = DS.Style("DS.Slider") };
    readonly Slider panX = new() { Minimum = -1, Maximum = 1, Value = 0, SmallChange = 0.05, LargeChange = 0.2, Style = DS.Style("DS.Slider") };
    readonly Slider panY = new() { Minimum = -1, Maximum = 1, Value = 0, SmallChange = 0.05, LargeChange = 0.2, Style = DS.Style("DS.Slider") };
    readonly Slider brightness = new() { Minimum = -.5, Maximum = .5, Value = 0, SmallChange = 0.05, LargeChange = 0.1, Style = DS.Style("DS.Slider") };
    readonly Slider contrast = new() { Minimum = .25, Maximum = 2, Value = 1, SmallChange = 0.05, LargeChange = 0.2, Style = DS.Style("DS.Slider") };
    readonly Slider fps = new() { Minimum = 1, Maximum = 20, Value = 5, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider") };
    readonly Slider frameLimit = new() { Minimum = 1, Maximum = 1, Value = 1, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider") };

    readonly ComboBox rotation = new() { ItemsSource = new[] { "0 deg", "90 deg", "180 deg", "270 deg" }, SelectedIndex = 0, MinWidth = 96, MinHeight = 32, FontSize = 12 };
    readonly ComboBox fit = new() { ItemsSource = new[] { "Auto (Fill 2:1)", "Manual (Fit Inside)" }, SelectedIndex = 0, MinWidth = 152, MinHeight = 32, FontSize = 12 };

    readonly DispatcherTimer previewClock = new(), editClock = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly List<BitmapSource> frames = [], encodedFrames = [];
    readonly ScreenEmulator emulator = new();
    readonly Button cancel, retry, checkTransfer;
    readonly StackPanel editor = new(), firstRun = new();
    readonly ScrollViewer scroll;
    readonly TextBlock flashStatus = ManagementVisuals.Copy("Flash ID -", 14), deviceStatus = ManagementVisuals.Copy("Device -", 13);
    readonly TextBlock firstStatus = ManagementVisuals.Copy("1 · Connect USB, then Detect", 14);
    readonly Stopwatch firstTimer = new();
    uint? plannedNonce;
    byte[]? editorProject;
    UIElement[] animationOnly = [];
    bool waiting;
    readonly TextBlock[] steps =
    [
        Kit.Text("1 · Choose image or template", 11.5, "DS.TextMuted"),
        Kit.Text("2 · Preview & Upload", 11.5, "DS.TextMuted"),
        Kit.Text("3 · Pad off -> hold M -> USB", 11.5, "DS.TextMuted"),
    ];
    CancellationTokenSource? operation;
    IScreenTransport? reviewTransport;
    byte[]? asset, sourceBytes, sourceAsset, uncertainAsset;
    bool uncertainAnimation;
    int uncertainDelay;
    int sourceBuiltin = -1, frameIndex, revision, delay = 200, retries, transferEpoch;
    bool testUploaded;
    long inputBytes;
    DateTime previewUntil;
    bool busy, restoring, uncertain, checkedFailure, planReady, identityReady, firstMode, loading;
    string sourceName = "";

    public ScreenWorkbench(FrameworkElement resources, Action<Button> register, Action back)
    {
        Button ConsoleBtn(string label, Action action)
        {
            var b = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(10, 5, 10, 5),
                Margin = new(2),
                FontSize = 11.5,
                MinHeight = 30,
                Height = double.NaN,
            };
            b.Click += (_, _) => action();
            register(b);
            return b;
        }

        static Border MakeCard(FrameworkElement inner, Thickness? margin = null) => new()
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Margin = margin ?? new(0, 0, 0, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 190)),
            Child = inner,
        };

        FrameworkElement SliderRow(string label, Slider sl, Func<double, string> fmt)
        {
            var row = new StackPanel { Margin = new(0, 2, 0, 4) };
            var head = new DockPanel { Margin = new(0, 0, 0, 2) };
            var valText = Kit.Text(fmt(sl.Value), 11.5, "DS.Text", FontWeights.SemiBold);
            DockPanel.SetDock(valText, Dock.Right);
            head.Children.Add(valText);
            head.Children.Add(Kit.Text(label, 11.5, "DS.TextSoft"));
            sl.ValueChanged += (_, _) => valText.Text = fmt(sl.Value);
            row.Children.Add(head);
            row.Children.Add(sl);
            return row;
        }

        // Outer 2-column split matching RGB & Vibration studios: Left Editor (1.15*) | Right Previews (1.10*)
        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new() { Width = new(1.15, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new() { Width = new(1.10, GridUnitType.Star) });

        // ---- LEFT COLUMN: Decluttered Bento Controls + Pinned Upload Footer ----
        Import = ConsoleBtn("Choose image...", Choose);
        var openBtn = ConsoleBtn("Open project...", Open);
        var saveBtn = ConsoleBtn("Save project...", SaveProject);
        var exportBtn = ConsoleBtn("Export .bin...", Export);

        var fileGrid = new UniformGrid { Columns = 4, Margin = new(-2, 0, -2, 8) };
        foreach (var b in new[] { Import, openBtn, saveBtn, exportBtn })
            fileGrid.Children.Add(b);
        editor.Children.Add(fileGrid);

        // Card 1: Built-in Wallpapers & Animated White-Mode Templates
        var sourceStack = new StackPanel();
        sourceStack.Children.Add(Kit.Text("BUILT-IN WALLPAPERS & TEMPLATES", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 6)));
        var wallpapers = new UniformGrid { Columns = 3, Margin = new(-2, 0, -2, 4) };
        for (int i = 0; i < 6; i++)
        {
            int n = i;
            wallpapers.Children.Add(ConsoleBtn(ScreenArtwork.WallpaperNames[i], () => LoadWallpaper(n)));
        }
        sourceStack.Children.Add(wallpapers);

        var tmplLabel = new TextBox { Text = "X20 PRO", MaxLength = 14, Width = 148, Padding = new(8, 4, 8, 4), Margin = new(0, 4, 0, 6), HorizontalAlignment = HorizontalAlignment.Left };
        var tmplGrid = new UniformGrid { Columns = 4, Margin = new(-2, 0, -2, 2) };
        foreach (var (tKind, tTitle) in new (ScreenTemplateKind, string)[]
        {
            (ScreenTemplateKind.GamertagBadge, "Gamertag"),
            (ScreenTemplateKind.RetroGrid, "Retro Grid"),
            (ScreenTemplateKind.AcousticWave, "Acoustic"),
            (ScreenTemplateKind.MinimalEmblem, "Minimal"),
        })
        {
            var capturedKind = tKind;
            tmplGrid.Children.Add(ConsoleBtn(tTitle, () => LoadTemplate(capturedKind, tmplLabel.Text)));
        }
        var tmplBody = new StackPanel
        {
            Margin = new(0, 6, 0, 2),
            Children =
            {
                Kit.Text("Generates a 16-frame 160x80 animation ready for White flash mode.", 11.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 4); }),
                tmplLabel,
                tmplGrid,
            }
        };
        sourceStack.Children.Add(new Expander { Header = "Animated Templates (White Mode Upload)", IsExpanded = false, Content = tmplBody, Margin = new(0, 4, 0, 0), Foreground = DS.Brush("DS.Text"), FontSize = 12, FontWeight = FontWeights.SemiBold });
        editor.Children.Add(MakeCard(sourceStack));

        // Card 2: Resolution Mode, Orientation, Framing & Color
        var frameStack = new StackPanel();
        var topModeRow = new DockPanel { Margin = new(0, 0, 0, 6) };
        var rightSelectors = new StackPanel { Orientation = Orientation.Horizontal, Children = { fit.Also(c => c.Margin = new(0, 0, 6, 0)), rotation } };
        DockPanel.SetDock(rightSelectors, Dock.Right);
        topModeRow.Children.Add(rightSelectors);
        topModeRow.Children.Add(Kit.Text("FRAMING & COLOR (160 x 80)", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        frameStack.Children.Add(topModeRow);

        var panCols = new Grid();
        panCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        panCols.ColumnDefinitions.Add(new() { Width = new(12) });
        panCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var hEl = SliderRow("Horizontal", panX, v => $"{v:0.00}");
        var vEl = SliderRow("Vertical", panY, v => $"{v:0.00}");
        Grid.SetColumn(vEl, 2);
        panCols.Children.Add(hEl);
        panCols.Children.Add(vEl);

        var toneCols = new Grid();
        toneCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        toneCols.ColumnDefinitions.Add(new() { Width = new(12) });
        toneCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var bEl = SliderRow("Brightness", brightness, v => $"{v * 100:+0;-0;0}%");
        var cEl = SliderRow("Contrast", contrast, v => $"{v:0.00}x");
        Grid.SetColumn(cEl, 2);
        toneCols.Children.Add(bEl);
        toneCols.Children.Add(cEl);

        frameStack.Children.Add(SliderRow("Zoom", zoom, v => $"{v:0.00}x"));
        frameStack.Children.Add(panCols);
        frameStack.Children.Add(toneCols);
        editor.Children.Add(MakeCard(frameStack));

        // Card 3: Animation Playback & Frame Caps
        var animStack = new StackPanel();
        var animHeader = new DockPanel { Margin = new(0, 0, 0, 4) };
        Preview = ConsoleBtn("Rebuild preview", async () => await BuildPreviewAsync());
        var playPauseBtn = ConsoleBtn("Play / Pause", () =>
        {
            if (previewClock.IsEnabled) StopPreview();
            else if (encodedFrames.Count > 1) { previewUntil = DateTime.UtcNow.AddSeconds(30); previewClock.Start(); }
        });
        var animBtns = new StackPanel { Orientation = Orientation.Horizontal, Children = { Preview, playPauseBtn } };
        DockPanel.SetDock(animBtns, Dock.Right);
        animHeader.Children.Add(animBtns);
        animHeader.Children.Add(animation);
        animStack.Children.Add(animHeader);

        var animCols = new Grid { Margin = new(0, 4, 0, 0) };
        animCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        animCols.ColumnDefinitions.Add(new() { Width = new(12) });
        animCols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var fpsRow = SliderRow("Speed (FPS)", fps, v => $"{v:0} fps");
        var limRow = SliderRow("Max frames", frameLimit, v => $"{v:0}");
        Grid.SetColumn(limRow, 2);
        animCols.Children.Add(fpsRow);
        animCols.Children.Add(limRow);
        animStack.Children.Add(animCols);
        animationOnly = [animCols];
        editor.Children.Add(MakeCard(animStack, new(0)));

        // Optional First-Run Serial Verification sub-view inside the same left scroll host
        firstRun.Children.Add(ManagementVisuals.Copy("First-run check", 22, true));
        firstRun.Children.Add(firstStatus);
        firstRun.Children.Add(deviceStatus);
        firstRun.Children.Add(flashStatus);
        firstRun.Children.Add(ConsoleBtn("1 · Detect", async () => await Detect()));
        firstRun.Children.Add(ManagementVisuals.Copy("2 · Choose controller COM port", 13));
        firstRun.Children.Add(portName);
        firstRun.Children.Add(ConsoleBtn("3 · Read flash ID", async () => await ProbeAsync()));
        firstRun.Children.Add(ConsoleBtn("4 · Dry run", DryRun));
        firstRun.Children.Add(before);
        firstRun.Children.Add(ConsoleBtn("5 · Upload test image", async () =>
        {
            if (!identityReady || !planReady || before.IsChecked != true) { firstStatus.Text = "Complete steps 1-4 and Before"; return; }
            await TransferAsync(false);
        }));
        firstRun.Children.Add(after);
        firstRun.Children.Add(ConsoleBtn("Record result", () =>
        {
            if (!testUploaded) { firstStatus.Text = "Upload test image first"; return; }
            firstTimer.Stop();
            firstStatus.Text = after.IsChecked == true ? $"Result recorded · {firstTimer.Elapsed.TotalSeconds:0}s" : "Confirm the image and colors";
            SaveSession("physical-observation", new { before = before.IsChecked, after = after.IsChecked, elapsedSeconds = firstTimer.Elapsed.TotalSeconds, port = portName.Text });
        }));
        firstRun.Children.Add(ConsoleBtn("Back to editor", () => ShowFirstRun(false)));
        before.Checked += (_, _) => UpdateWriteGates();
        before.Unchecked += (_, _) => UpdateWriteGates();
        portName.TextChanged += (_, _) => { identityReady = planReady = false; plannedNonce = null; UpdateWriteGates(); };

        // Footer (pinned bottom of Left Editor Panel)
        Upload = new Button { Content = "Upload to Screen", Style = DS.Style("DS.ConsolePrimary"), HorizontalAlignment = HorizontalAlignment.Stretch, Height = 40, Margin = new(0, 6, 0, 2) };
        Upload.Click += async (_, _) => await TransferAsync(false);
        register(Upload);

        Reset = ConsoleBtn("Restore stock", async () =>
        {
            if (confirmReset.IsChecked != true) { status.Text = "Select Confirm reset"; return; }
            await TransferAsync(true);
        });
        cancel = ConsoleBtn("Cancel", () => operation?.Cancel());
        cancel.IsEnabled = false;
        retry = ConsoleBtn("Retry once", async () => { if (checkedFailure && !restoring && retries++ == 0) { uncertain = false; await TransferAsync(false); } });
        checkTransfer = ConsoleBtn("Check transfer", async () => await CheckAsync());

        var stepRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 4, 0, 4) };
        for (int i = 0; i < steps.Length; i++)
        {
            if (i > 0) stepRow.Children.Add(Kit.Text(" | ", 11, "DS.TextMuted"));
            stepRow.Children.Add(steps[i]);
        }

        var modeAndRecovery = new DockPanel { Margin = new(0, 4, 0, 0) };
        var recovery = new WrapPanel { Children = { cancel, checkTransfer, retry, Reset, confirmReset } };
        DockPanel.SetDock(simulate, Dock.Left);
        modeAndRecovery.Children.Add(simulate.Also(s => s.Margin = new(0, 0, 12, 0)));
        modeAndRecovery.Children.Add(recovery);

        var footer = new StackPanel { Children = { Kit.Rule().Also(r => r.Margin = new(0, 6, 0, 4)), stepRow, modeAndRecovery, Upload, transferProgress, status } };

        var leftHeader = new StackPanel { Margin = new(0, 0, 0, 8), Children = { Kit.Section("SCREEN STUDIO · 160x80 LCD", 0) } };
        scroll = new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 6, 0) };
        var leftDock = new DockPanel();
        DockPanel.SetDock(leftHeader, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        leftDock.Children.Add(leftHeader);
        leftDock.Children.Add(footer);
        leftDock.Children.Add(scroll);
        var leftPanel = Kit.Panel(null, null, null, leftDock, new Thickness(16)).Also(p => p.Margin = new(0, 0, 12, 0));
        contentGrid.Children.Add(leftPanel);

        // ---- RIGHT COLUMN: Top 160x80 LCD Target Frame + Bottom Live X20 Pro Controller Preview ----
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new() { Height = new(0.88, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new() { Height = new(1.12, GridUnitType.Star) });

        // Top Card: True 2:1 (160x80) LCD panel with non-AMOLED IPS backlight (#161A24) + glass sheen
        var lcdSurface = new Grid { Width = 360, Height = 180, Background = LcdBacklightBrush };
        lcdSurface.Children.Add(image);
        lcdSurface.Children.Add(BuildGlassSheen());

        var lcdBezel = new Border
        {
            CornerRadius = new(10),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(2),
            Padding = new(4),
            Background = LcdBacklightBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Border { CornerRadius = new(6), ClipToBounds = true, Child = lcdSurface },
        };
        var topPreviewBody = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Kit.Text("TARGET RESOLUTION · 160 x 80 IPS LCD", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 8)),
                new Viewbox { MaxHeight = 200, Stretch = Stretch.Uniform, Child = lcdBezel },
                new DockPanel
                {
                    Margin = new(0, 10, 0, 0),
                    Children =
                    {
                        sizeText.Also(t => DockPanel.SetDock(t, Dock.Right)),
                        details,
                    }
                },
                notice.Also(t => t.Margin = new(0, 2, 0, 0)),
            }
        };
        var topCard = Kit.Panel(null, null, null, topPreviewBody, new Thickness(16)).Also(c => c.Margin = new(0, 0, 0, 10));
        Grid.SetRow(topCard, 0);
        rightGrid.Children.Add(topCard);

        // Bottom Card: Real X20 Pro controller with "EasySMX" screen replaced cleanly by synchronized non-AMOLED LCD
        var padContainer = new Grid { Width = 1536, Height = 1024, IsHitTestVisible = false };
        padContainer.Children.Add(new PhotoController("x20_pro"));

        var padOverlay = new Canvas { Width = 1536, Height = 1024, IsHitTestVisible = false };
        var screenStage = new Grid { Background = LcdBacklightBrush };
        screenStage.Children.Add(controllerImage);
        screenStage.Children.Add(BuildGlassSheen());

        var embeddedLcd = new Border
        {
            Width = 294,
            Height = 144,
            CornerRadius = new(13),
            BorderThickness = new(2.5),
            BorderBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(0x0B, 0x0E, 0x15))),
            Background = LcdBacklightBrush,
            ClipToBounds = true,
            Child = new Border { CornerRadius = new(10), ClipToBounds = true, Child = screenStage },
        };
        Canvas.SetLeft(embeddedLcd, 621);
        Canvas.SetTop(embeddedLcd, 128);
        padOverlay.Children.Add(embeddedLcd);
        padContainer.Children.Add(padOverlay);

        var bottomPreviewGrid = new Grid();
        bottomPreviewGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        bottomPreviewGrid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        bottomPreviewGrid.Children.Add(Kit.Text("ON-CONTROLLER LIVE PREVIEW · X20 PRO", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 4)));
        var padViewbox = new Viewbox { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = padContainer };
        Grid.SetRow(padViewbox, 1);
        bottomPreviewGrid.Children.Add(padViewbox);

        var bottomCard = Kit.Panel(null, null, null, bottomPreviewGrid, new Thickness(14));
        Grid.SetRow(bottomCard, 1);
        rightGrid.Children.Add(bottomCard);

        Grid.SetColumn(rightGrid, 1);
        contentGrid.Children.Add(rightGrid);

        var (ecoOverlay, syncEco) = Kit.EcoOverlay("Screen", contentGrid, locked => { if (locked) StopPreview(); });
        Body.Children.Add(contentGrid);
        Body.Children.Add(ecoOverlay);
        void OnEcoChanged() => Body.Dispatcher.BeginInvoke(syncEco, DispatcherPriority.Background);
        Body.Loaded += (_, _) => { UniversalHapticsService.Changed -= OnEcoChanged; UniversalHapticsService.Changed += OnEcoChanged; syncEco(); };

        foreach (var slider in new[] { zoom, panX, panY, brightness, contrast, fps, frameLimit }) slider.ValueChanged += (_, _) => Refresh();
        rotation.SelectionChanged += (_, _) => Refresh();
        fit.SelectionChanged += (_, _) => Refresh();
        animation.Checked += (_, _) => Refresh();
        animation.Unchecked += (_, _) => Refresh();
        simulate.Checked += (_, _) => ModeChanged();
        simulate.Unchecked += (_, _) => ModeChanged();
        editClock.Tick += async (_, _) => { editClock.Stop(); await BuildPreviewAsync(); };
        previewClock.Tick += (_, _) =>
        {
            if (DateTime.UtcNow >= previewUntil || !Body.IsVisible) { StopPreview(); return; }
            if (encodedFrames.Count > 0)
            {
                frameIndex = (frameIndex + 1) % encodedFrames.Count;
                SetPreviewSource(encodedFrames[frameIndex]);
            }
        };
        Body.Unloaded += (_, _) => { StopPreview(); UniversalHapticsService.Changed -= OnEcoChanged; };
        Body.AllowDrop = true;
        Body.DragOver += (_, e) => { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Body.Drop += (_, e) => { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1) Try(() => Load(paths[0])); };
        UpdateWriteGates();
        syncEco();
    }

    static Border BuildGlassSheen() => new()
    {
        IsHitTestVisible = false,
        Background = new LinearGradientBrush(
            Color.FromArgb(24, 255, 255, 255),
            Color.FromArgb(4, 180, 210, 255),
            new Point(0, 0),
            new Point(0.9, 1.0)),
    };

    void SetPreviewSource(ImageSource? src)
    {
        image.Source = src;
        controllerImage.Source = src;
    }

    void Try(Action action) { try { action(); } catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException or JsonException or System.Runtime.InteropServices.COMException) { status.Text = e.Message; } }
    void Choose() { var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" }; if (dialog.ShowDialog() == true) Try(() => Load(dialog.FileName)); }
    void Open() { var dialog = new OpenFileDialog { Filter = "Screen projects and assets|*.x20screen;*.bin|Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" }; if (dialog.ShowDialog() == true) Try(() => Load(dialog.FileName)); }

    public void Load(string path)
    {
        if (busy) throw new InvalidOperationException("A transfer is running.");
        var info = new FileInfo(path);
        long cap = path.EndsWith(".x20screen", StringComparison.OrdinalIgnoreCase) ? 8 * 1024 * 1024 : path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ? ScreenCodec.AssetLimit : ScreenCodec.InputLimit;
        if (info.Length > cap) throw new ArgumentException("File exceeds its size limit.");
        if (path.EndsWith(".x20screen", StringComparison.OrdinalIgnoreCase)) { ReadProject(File.ReadAllBytes(path)); return; }
        if (path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) { LoadEncoded(File.ReadAllBytes(path), 100); sourceName = info.Name; return; }
        LoadImageBytes(File.ReadAllBytes(path), info.Name);
    }

    void LoadImageBytes(byte[] bytes, string name)
    {
        if (bytes.Length > ScreenCodec.InputLimit) throw new ArgumentException("Choose a file up to 64 MiB.");
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count > 512) throw new ArgumentException("Use 512 frames or fewer.");
        var decoded = ScreenMedia.Decode(decoder);
        ResetEdits(); frames.Clear(); frames.AddRange(decoded.Frames); sourceBytes = bytes; sourceAsset = null; sourceBuiltin = -1; sourceName = name; inputBytes = bytes.Length; FinishLoad();
        notice.Text = frames.Count > 1 ? "GIF delays normalized · non-AMOLED IPS backlight preview" : "Non-AMOLED IPS backlight preview";
    }

    public void LoadWallpaper(int index) { ResetEdits(); frames.Clear(); frames.Add(Bitmap(ScreenArtwork.Wallpaper(index))); sourceBytes = sourceAsset = null; sourceBuiltin = index; sourceName = ScreenArtwork.WallpaperNames[index]; inputBytes = 0; FinishLoad(); }

    void LoadTemplate(ScreenTemplateKind kind, string? tag)
    {
        var c = ThemeCatalog.Current.Accent;
        uint accent = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        var generated = ScreenTemplates.Generate(kind, tag, accent, 16);
        ResetEdits(); frames.Clear(); foreach (var rgba in generated) frames.Add(Bitmap(rgba));
        sourceBytes = sourceAsset = null; sourceBuiltin = -1; sourceName = $"{kind} template"; inputBytes = 0; fps.Value = 8; FinishLoad(); _ = BuildPreviewAsync();
    }

    void LoadEncoded(byte[] bytes, int playbackDelay)
    {
        bool animated = ScreenCodec.IsAnimationAsset(bytes);
        var decoded = ScreenCodec.DecodeAsset(bytes, animated);
        ResetEdits(); frames.Clear(); frames.AddRange(decoded.Select(Bitmap)); sourceBytes = null; sourceAsset = bytes.ToArray(); sourceBuiltin = -1; sourceName = "Encoded artwork"; inputBytes = 0; FinishLoad();
        animation.IsChecked = animated; fps.Value = Math.Clamp(1000d / playbackDelay, 1, 20);
        notice.Text = "Encoded asset · delay stored in project";
    }

    void ResetEdits() { loading = true; transferProgress.Value = 0; StopPreview(); zoom.Value = 1; panX.Value = panY.Value = brightness.Value = 0; contrast.Value = 1; rotation.SelectedIndex = fit.SelectedIndex = 0; animation.IsChecked = false; fps.Value = 5; planReady = false; }
    void FinishLoad() { notice.Text = "Non-AMOLED IPS backlight preview"; frameLimit.Maximum = Math.Max(1, frames.Count); frameLimit.Value = frames.Count; animation.IsChecked = frames.Count > 1; loading = false; Refresh(); details.Text = $"{sourceName} · {frames.Count} frame{(frames.Count == 1 ? "" : "s")}"; }

    void Refresh()
    {
        if (loading) return;
        revision++; planReady = false; plannedNonce = null; asset = null; encodedFrames.Clear(); StopPreview(); UpdateWriteGates();
        if (frames.Count > 0) { SetPreviewSource(Crop(frames[0])); editClock.Stop(); editClock.Start(); }
        sizeText.Text = $"Input {inputBytes:N0} / 2,097,152 · Upload calculating / 3,145,728";
    }

    internal byte[] FirstCroppedPixels => Rgba(Crop(frames[0]));
    internal byte[] CroppedFramesBytes => frames.SelectMany(f => Rgba(Crop(f))).ToArray();

    BitmapSource Crop(BitmapSource source)
    {
        BitmapSource oriented = rotation.SelectedIndex == 0 ? source : new TransformedBitmap(source, new RotateTransform(rotation.SelectedIndex * 90));
        double scale = (fit.SelectedIndex == 1 ? Math.Min(160d / oriented.PixelWidth, 80d / oriented.PixelHeight) : Math.Max(160d / oriented.PixelWidth, 80d / oriented.PixelHeight)) * zoom.Value;
        double w = oriented.PixelWidth * scale, h = oriented.PixelHeight * scale, x = (160 - w) * (panX.Value + 1) / 2, y = (80 - h) * (panY.Value + 1) / 2;
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var drawing = visual.RenderOpen()) { drawing.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 160, 80)); drawing.DrawImage(oriented, new Rect(x, y, w, h)); }
        var result = new RenderTargetBitmap(160, 80, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }

    internal static BitmapSource Bitmap(byte[] rgba) { var bytes = rgba.ToArray(); for (int i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]); var bitmap = BitmapSource.Create(160, 80, 96, 96, PixelFormats.Bgra32, null, bytes, 640); bitmap.Freeze(); return bitmap; }
    static byte[] Rgba(BitmapSource source) { var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); var bytes = new byte[51200]; converted.CopyPixels(bytes, 640, 0); for (int i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]); return bytes; }

    public async Task BuildPreviewAsync()
    {
        editClock.Stop(); if (busy || frames.Count == 0) return; busy = true; StopPreview(); UpdateWriteGates();
        try
        {
            int version = revision; bool animated = animation.IsChecked == true; delay = (int)Math.Clamp(Math.Round(100d / fps.Value) * 10, 10, 2550); previewClock.Interval = TimeSpan.FromMilliseconds(delay);
            var pixels = (animated ? frames.Take((int)frameLimit.Value) : frames.Take(1)).Select(f => ScreenArtwork.Adjust(Rgba(Crop(f)), brightness.Value, contrast.Value)).ToArray();
            int usedFrames = pixels.Length, usedDelay = delay;
            var built = await Task.Run(() => animated ? ScreenCodec.EncodeAnimationFitting(pixels, delay, out usedFrames, out usedDelay) : ScreenCodec.EncodeStill(pixels[0]));
            if (animated && usedDelay != delay) { delay = usedDelay; previewClock.Interval = TimeSpan.FromMilliseconds(delay); }
            if (version != revision) return;
            asset = built; encodedFrames.Clear(); encodedFrames.AddRange(ScreenCodec.DecodeAsset(asset, animated).Select(Bitmap));
            SetPreviewSource(encodedFrames[0]);
            if (encodedFrames.Count > 1) { previewUntil = DateTime.UtcNow.AddSeconds(30); previewClock.Start(); }
            sizeText.Text = $"Upload {asset.Length:N0} / 3,145,728";
            status.Text = $"{(animated ? $"{encodedFrames.Count} frames{(encodedFrames.Count < pixels.Length ? $" (every {(pixels.Length + encodedFrames.Count - 1) / encodedFrames.Count}th kept to fit)" : "")} · {1000d / delay:0.#} FPS" : "Static wallpaper")} · {(simulate.IsChecked == true ? "Simulate" : "Controller")}";
        }
        catch (ArgumentException e) { asset = null; status.Text = e.Message; }
        finally { busy = false; UpdateWriteGates(); if (asset == null && frames.Count > 0 && revision > 0) sizeText.Text = "Upload unavailable · reduce frames or size"; }
    }

    void ModeChanged() { if (busy) { simulate.IsChecked = reviewTransport?.IsEmulated == true; return; } identityReady = planReady = false; plannedNonce = null; UpdateWriteGates(); status.Text = simulate.IsChecked == true ? "Simulate" : "Controller"; }
    IScreenTransport Transport(string? port = null) => simulate.IsChecked == true ? reviewTransport ?? emulator : new ScreenSerialTransport(port ?? portName.Text.Trim(), plannedNonce, NewTracePath());
    bool Ready => (simulate.IsChecked == true || (ScreenHardwareEvidence.VerifiedDevice && (!firstMode || ScreenSerialTransport.ValidPortName(portName.Text.Trim())))) && !busy;

    /// <summary>The screen chip's port, waiting for the M plug-in if it is not there yet; the chip needs a few seconds before it answers.</summary>
    async Task<string?> ScreenPortAsync(CancellationToken token)
    {
        if (simulate.IsChecked == true) return null; if (firstMode) return portName.Text.Trim();
        var port = ScreenSerialTransport.FindScreenPort(); if (port != null) return port;
        var beforePorts = SerialPort.GetPortNames(); string? Arrived() => ScreenSerialTransport.FindScreenPort() ?? SerialPort.GetPortNames().Except(beforePorts).FirstOrDefault();
        status.Text = "Switch the controller off, hold M and plug it in"; waiting = true; UpdateWriteGates();
        try { while ((port = Arrived()) == null) await Task.Delay(100, token); } finally { waiting = false; UpdateWriteGates(); }
        status.Text = "Connecting · " + port; await Task.Delay(3000, token); return SerialPort.GetPortNames().Contains(port) ? port : throw new IOException("The controller was unplugged.");
    }

    void UpdateWriteGates()
    {
        editor.IsEnabled = firstRun.IsEnabled = !busy;
        Upload.IsEnabled = Ready && asset != null && !uncertain && (!firstMode || (identityReady && planReady && before.IsChecked == true));
        Reset.IsEnabled = Ready && simulate.IsChecked == true;
        retry.IsEnabled = Ready && checkedFailure && !restoring && retries == 0;
        checkTransfer.IsEnabled = Ready && uncertain && !restoring;
        cancel.IsEnabled = busy;
        Import.IsEnabled = Preview.IsEnabled = !busy;
        simulate.IsEnabled = !busy;
        bool live = simulate.IsChecked != true;
        Reset.Visibility = confirmReset.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
        cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        checkTransfer.Visibility = uncertain && !busy ? Visibility.Visible : Visibility.Collapsed;
        retry.Visibility = retry.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        foreach (var element in animationOnly) element.Visibility = animation.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        int current = waiting ? 2 : asset == null && !busy ? 0 : 1;
        for (int i = 0; i < steps.Length; i++) { steps[i].Opacity = i == current ? 1 : .45; steps[i].FontWeight = i == current ? FontWeights.SemiBold : FontWeights.Normal; steps[i].Visibility = live ? Visibility.Visible : Visibility.Collapsed; }
    }

    async Task TransferAsync(bool reset)
    {
        if (!Ready || (!reset && (asset == null || uncertain || firstMode && (!identityReady || !planReady || before.IsChecked != true)))) return;
        int epoch = ++transferEpoch; transferProgress.Value = 0; busy = true; restoring = reset;
        if (!reset) { uncertainAsset = asset!.ToArray(); uncertainAnimation = animation.IsChecked == true; uncertainDelay = delay; }
        StopPreview(); editClock.Stop(); UpdateWriteGates(); operation = new();
        try
        {
            var transport = Transport(await ScreenPortAsync(operation.Token)); using var lease = transport as IDisposable; var sender = new ScreenSender(transport);
            var progress = new Progress<ScreenProgress>(p => { if (busy && epoch == transferEpoch) { transferProgress.Value = p.Total == 0 ? 0 : 100d * p.Sent / p.Total; status.Text = $"{(simulate.IsChecked == true ? "Simulate · " : "")}{p.State} · {p.Sent:N0} / {p.Total:N0}"; } });
            if (reset) await sender.ResetAsync(progress, operation.Token); else await sender.UploadAsync(asset!, animation.IsChecked == true, delay, progress, operation.Token, plannedNonce);
            uncertain = checkedFailure = false; retries = 0; transferProgress.Value = 100; if (firstMode && !reset) testUploaded = true; status.Text = reset ? "Restored · restart controller" : "Upload completed"; confirmReset.IsChecked = false;
            SaveSession(reset ? "restore" : "upload", new { simulated = simulate.IsChecked, bytes = reset ? 4 : asset!.Length, port = portName.Text });
        }
        catch (OperationCanceledException) { uncertain = true; status.Text = "Transfer cancelled · check transfer"; }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or TimeoutException or UnauthorizedAccessException) { uncertain = true; status.Text = "Transfer stopped · " + e.Message + (simulate.IsChecked == true ? "" : " · Replug holding M"); SaveSession("failure", new { error = e.Message, reset, port = portName.Text }); }
        finally { operation.Dispose(); operation = null; busy = false; UpdateWriteGates(); }
    }

    async Task CheckAsync()
    {
        if (!Ready || uncertainAsset == null || !uncertain) return;
        busy = true; UpdateWriteGates(); using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(100)); operation = stop;
        try { var transport = Transport(await ScreenPortAsync(stop.Token)); using var lease = transport as IDisposable; bool match = await new ScreenSender(transport).VerifyAsync(uncertainAsset, uncertainAnimation, uncertainDelay, stop.Token); checkedFailure = !match; status.Text = match ? "Transfer verified" : "Readback differs · Retry once or Restore stock"; if (match) uncertain = false; }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or OperationCanceledException) { checkedFailure = false; status.Text = "Readback unavailable · " + e.Message; }
        finally { operation = null; busy = false; UpdateWriteGates(); }
    }

    internal void ShowFirstRun(bool show)
    {
        StopPreview();
        if (show && !firstMode) { testUploaded = false; before.IsChecked = after.IsChecked = false; editorProject = frames.Count > 0 ? ProjectBytes() : null; var pixels = ScreenArtwork.TestImage(); ResetEdits(); frames.Clear(); frames.Add(Bitmap(pixels)); sourceAsset = ScreenCodec.EncodeStill(pixels); sourceBytes = null; sourceBuiltin = -1; sourceName = "Upload test image"; inputBytes = 0; FinishLoad(); firstTimer.Restart(); firstStatus.Text = "1 · Connect USB, then Detect"; }
        else if (!show && firstMode && editorProject != null) ReadProject(editorProject);
        firstMode = show; scroll.Content = show ? firstRun : editor; UpdateWriteGates();
    }

    async Task Detect()
    {
        if (!firstTimer.IsRunning) firstTimer.Start(); var ports = ReviewSandbox.Active ? new[] { "COM7" } : SerialPort.GetPortNames();
        var inventory = ReviewSandbox.Active ? new Inventory() : await Inventory.Bound(Task.Run(() => WindowsInventory.Read()), 3000);
        var candidates = inventory.interfaces.Where(d => d.vid is 0x1020 or 0x413d || (d.product + " " + d.manufacturer).Contains("EasySMX", StringComparison.OrdinalIgnoreCase)).Select(d => d.product).Distinct().ToArray();
        var screenPort = ReviewSandbox.Active ? null : ScreenSerialTransport.FindScreenPort(); if (screenPort != null) { portName.Text = screenPort; candidates = [.. candidates, "Screen " + screenPort]; }
        deviceStatus.Text = candidates.Length == 0 ? "Device not identified" : string.Join(" · ", candidates);
        firstStatus.Text = $"Ports: {(ports.Length == 0 ? "none" : string.Join(", ", ports))} · choose explicitly · {firstTimer.Elapsed.TotalSeconds:0}s";
        SaveSession("detect", new { ports, candidates, errors = inventory.errors, model = "x20_pro" });
    }

    async Task ProbeAsync()
    {
        if (!Ready) { firstStatus.Text = "Choose COM port · enable owner writes"; return; }
        busy = true; UpdateWriteGates(); using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10)); operation = stop;
        try { var transport = Transport(); using var lease = transport as IDisposable; uint id = await new ScreenSender(transport).ProbeAsync(stop.Token); flashStatus.Text = $"Flash 0x{id:X6}"; firstStatus.Text = $"Identity read · {firstTimer.Elapsed.TotalSeconds:0}s"; ScreenSender.RequireSupportedFlash(id); identityReady = true; SaveSession("identity", new { flashId = $"0x{id:X6}", port = portName.Text, simulated = simulate.IsChecked }); }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException or OperationCanceledException) { identityReady = false; firstStatus.Text = e.Message; }
        finally { operation = null; busy = false; UpdateWriteGates(); }
    }

    void DryRun() { if (asset == null) { firstStatus.Text = "Build preview first"; return; } uint nonce = ScreenProtocol.Nonce(DateTime.Now); plannedNonce = nonce; uint magic = simulate.IsChecked == true ? ScreenProtocol.ModelMagic : ScreenProtocol.X20ProMagic; var f = ScreenTransferPlan.Build(asset, animation.IsChecked == true, delay, nonce, magic); var opening = new[] { ScreenProtocol.Handshake(0x30, nonce, 9, magic), ScreenProtocol.Handshake(0x30, nonce, magic: magic) }; SaveSession("dry-run", new { port = portName.Text, nonce, opening = opening.Select(Convert.ToHexString), frames = f.Select(Convert.ToHexString), bytes = f.Sum(x => x.Length) + 128, delay }); planReady = true; UpdateWriteGates(); firstStatus.Text = $"Dry run · {f.Count + 2} frames · no bytes sent"; }
    string SessionRoot() { string root = ReviewSandbox.Active ? Path.Combine(Path.GetTempPath(), "X20CTL-ScreenReview") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X20CTL", "Screen", "FirstRun"); Directory.CreateDirectory(root); return root; }
    string NewTracePath() => Path.Combine(SessionRoot(), $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-serial.jsonl");
    void SaveSession(string name, object value) { File.WriteAllText(Path.Combine(SessionRoot(), $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{name}.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })); }

    sealed record Project(int Version, byte[]? Source, byte[]? Encoded, int Builtin, string Name, bool Animation, double Zoom, double X, double Y, int Rotation, int Fit, double Brightness, double Contrast, double Fps, int Frames);
    internal byte[] ProjectBytes() => JsonSerializer.SerializeToUtf8Bytes(new Project(1, sourceBytes, sourceAsset, sourceBuiltin, sourceName, animation.IsChecked == true, zoom.Value, panX.Value, panY.Value, rotation.SelectedIndex, fit.SelectedIndex, brightness.Value, contrast.Value, fps.Value, (int)frameLimit.Value));
    internal void ReadProject(byte[] bytes)
    {
        if (bytes.Length > 8 * 1024 * 1024) throw new ArgumentException("Project exceeds 8 MiB.");
        var p = JsonSerializer.Deserialize<Project>(bytes) ?? throw new InvalidDataException("Invalid project.");
        if (p.Version != 1 || p.Zoom is < 1 or > 4 || p.X is < -1 or > 1 || p.Y is < -1 or > 1 || p.Rotation is < 0 or > 3 || p.Fit is < 0 or > 1 || p.Brightness is < -.5 or > .5 || p.Contrast is < .25 or > 2 || p.Fps is < 1 or > 20 || p.Frames is < 1 or > 512)
            throw new InvalidDataException("Invalid project settings.");
        if (p.Source != null) LoadImageBytes(p.Source, p.Name); else if (p.Encoded != null) LoadEncoded(p.Encoded, 200); else LoadWallpaper(p.Builtin);
        loading = true; animation.IsChecked = p.Animation; zoom.Value = p.Zoom; panX.Value = p.X; panY.Value = p.Y; rotation.SelectedIndex = p.Rotation; fit.SelectedIndex = p.Fit; brightness.Value = p.Brightness; contrast.Value = p.Contrast; fps.Value = p.Fps; frameLimit.Value = Math.Min(p.Frames, frames.Count); loading = false; Refresh();
    }
    void SaveProject() { var dialog = new SaveFileDialog { Filter = "X20CTL Screen project|*.x20screen", DefaultExt = ".x20screen" }; if (frames.Count > 0 && dialog.ShowDialog() == true) Try(() => File.WriteAllBytes(dialog.FileName, ProjectBytes())); }
    void Export() { if (asset == null) { status.Text = "Build preview first"; return; } var dialog = new SaveFileDialog { Filter = "Screen binary|*.bin", DefaultExt = ".bin" }; if (dialog.ShowDialog() == true) Try(() => File.WriteAllBytes(dialog.FileName, asset)); }
    internal void UseReviewTransport(IScreenTransport transport) { if (!ReviewSandbox.Active || !transport.IsEmulated) throw new InvalidOperationException("Review transport must be emulated."); reviewTransport = transport; simulate.IsChecked = true; UpdateWriteGates(); }
    public void StopPreview() { previewClock.Stop(); frameIndex = 0; }
    internal void SetReviewAnimation(bool enabled) { animation.IsChecked = enabled; }
    internal void SetReviewResetConfirmation() => confirmReset.IsChecked = true;
    internal async Task ReviewProbeAsync() { await ProbeAsync(); DryRun(); }
    internal async Task ReviewFirstUploadAsync() { if (Upload.IsEnabled) throw new InvalidOperationException("First-run upload bypassed Before confirmation."); before.IsChecked = true; if (!Upload.IsEnabled) throw new InvalidOperationException("Completed first-run checks did not enable upload."); await TransferAsync(false); if (!testUploaded) throw new InvalidOperationException("First-run test upload failed."); after.IsChecked = true; }
    internal Task ReviewCheckAsync() => CheckAsync();
}
