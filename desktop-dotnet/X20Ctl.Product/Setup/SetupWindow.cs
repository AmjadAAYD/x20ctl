using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
namespace X20Ctl.Product.Setup;

/// <summary>
/// The setup and uninstall wizard, drawn like the app: the navy field, the hooked X, the controller, one blue accent.
/// Install: Welcome, License, Options, Install, Finish. Uninstall: Remove, Finish.
/// </summary>
internal sealed class SetupWindow : Window
{
    static readonly string[] InstallSteps = ["Welcome", "License", "Options", "Install", "Finish"], RemoveSteps = ["Remove", "Finish"];
    readonly SetupMode mode; readonly string[] steps;
    SetupOptions options = new(Drivers: !PcModeSetup.Ready); string? driverNote; bool accepted, purge, busy; int step; string licenseTab = "License"; string? failure;
    readonly StackPanel stepList = new(); readonly TextBlock eyebrow, title, subtitle; readonly ContentPresenter body = new(); readonly DockPanel actionRow = new();
    readonly Image faint; Border? fill, track; TextBlock? percent, stepText; double fraction;
    public int ParentPid { get; init; }
    /// <summary>Runs after the wizard finished its work and the window closed (the removal copy deletes itself here).</summary>
    public Action? Closing2 { get; init; }

    public SetupWindow(SetupMode mode)
    {
        this.mode = mode; steps = mode == SetupMode.Install ? InstallSteps : RemoveSteps;
        Title = mode == SetupMode.Install ? "X20CTL Setup" : "Remove X20CTL";
        Width = 1040; Height = 650; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = DS.Brush("Ink"); Foreground = DS.Brush("DS.Text"); FontFamily = DS.Body; UseLayoutRounding = true; SnapsToDevicePixels = true;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 70, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(16), UseAeroCaptionButtons = false });
        SourceInitialized += (_, _) => { int round = 2; _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref round, sizeof(int)); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape && !busy) Close(); };
        Closed += (_, _) => Closing2?.Invoke();

        eyebrow = Kit.Text("", 13, "DS.Accent", FontWeights.SemiBold);
        title = Kit.Text("", 38, "DS.Text", FontWeights.SemiBold);
        subtitle = Kit.Text("", 17, "DS.TextSoft"); subtitle.TextWrapping = TextWrapping.Wrap; subtitle.Margin = new Thickness(0, 8, 0, 0);
        faint = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/brand.png")), Width = 360, Opacity = .05, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(-90, 0, 0, -70), IsHitTestVisible = false };

        var back = new Grid(); back.Children.Add(new GalaxyBackdrop(true, true));
        var shell = new Grid(); shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) }); shell.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new Border { Background = Brush("#99070B14"), BorderBrush = DS.Brush("DS.Hairline"), BorderThickness = new Thickness(0, 0, 1, 0), ClipToBounds = true };
        var leftGrid = new Grid(); leftGrid.Children.Add(faint);
        var brand = new StackPanel { Margin = new Thickness(36, 34, 24, 0) };
        brand.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = {
            new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon.png")), Width = 34, Height = 34, Margin = new Thickness(0, 0, 12, 0) },
            Kit.Text("X20CTL", 27, "DS.Text", FontWeights.Bold) } });
        brand.Children.Add(Kit.Text("Keep it Easy, Keep it 20", 15, "DS.TextSoft")); ((TextBlock)brand.Children[1]).Margin = new Thickness(0, 6, 0, 0);
        stepList.Margin = new Thickness(0, 46, 0, 0); brand.Children.Add(stepList);
        leftGrid.Children.Add(brand);
        var version = Kit.Text(Installer.Version, 13, "DS.TextMuted"); version.VerticalAlignment = VerticalAlignment.Bottom; version.Margin = new Thickness(36, 0, 0, 26); leftGrid.Children.Add(version);
        left.Child = leftGrid; shell.Children.Add(left);

        var right = new Grid { Margin = new Thickness(54, 44, 54, 34) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); right.RowDefinitions.Add(new RowDefinition()); right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var head = new StackPanel { Children = { eyebrow, title, subtitle } }; head.Children[0].SetValue(MarginProperty, new Thickness(0, 0, 0, 8));
        right.Children.Add(head);
        Grid.SetRow(body, 1); body.Margin = new Thickness(0, 26, 0, 22); right.Children.Add(body);
        Grid.SetRow(actionRow, 2); right.Children.Add(actionRow);
        Grid.SetColumn(right, 1); shell.Children.Add(right);

        var close = new TextBlock { Text = "✕", FontSize = 15, Foreground = DS.Brush("DS.TextSoft"), Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 12, 0) };
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.MouseEnter += (_, _) => close.Foreground = DS.Brush("DS.Text"); close.MouseLeave += (_, _) => close.Foreground = DS.Brush("DS.TextSoft");
        close.MouseLeftButtonUp += (_, _) => { if (!busy) Close(); };
        back.Children.Add(shell); back.Children.Add(close); Content = back;
        Go(0);
    }

    /// <summary>For the review harness: show a step in a given state without doing any work.</summary>
    internal void Preview(int index, double progress = 0, bool accept = false, string? fail = null, string tab = "License", bool desktop = false, int art = 0)
    {
        artIndex = art;
        accepted = accept; failure = fail; licenseTab = tab; options = options with { Desktop = desktop }; fraction = progress;
        busy = mode == SetupMode.UninstallRun && index == 1 && fail == null && progress < 1;
        Go(index); UpdateLayout();
        if (percent != null) { Report((progress, progress >= 1 ? "Done" : "Copying X20CTL")); if (track != null) { track.UpdateLayout(); SetFill(progress, false); } }
    }

    static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    // ---------------------------------------------------------------------------------------------- pages

    /// <summary>Shows a step. Used by the wizard itself and by the review harness.</summary>
    internal void Go(int index)
    {
        rotate?.Stop(); rotate = null; step = index; Steps(); actionRow.Children.Clear(); percent = null; stepText = null; fill = null; track = null;
        Page page = mode == SetupMode.Install ? InstallPage(index) : RemovePage(index);
        eyebrow.Text = page.Eyebrow; title.Text = page.Title; subtitle.Text = page.Sub; body.Content = page.Body;
        var leftButtons = new StackPanel { Orientation = Orientation.Horizontal, Children = { } }; foreach (var b in page.Left) leftButtons.Children.Add(b);
        var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; foreach (var b in page.Right) rightButtons.Children.Add(b);
        DockPanel.SetDock(rightButtons, Dock.Right); actionRow.Children.Add(rightButtons); actionRow.Children.Add(leftButtons);
        if (DS.Motion)
        {
            var slide = new TranslateTransform(0, 14); body.RenderTransform = slide; body.Opacity = 0;
            body.BeginAnimation(OpacityProperty, DS.To(1, 320)); slide.BeginAnimation(TranslateTransform.YProperty, DS.To(0, 380));
        }
        else body.Opacity = 1;
    }

    // the welcome page's controllers: the X20 Pro, then the X20, then the rest, sliding on by themselves
    static readonly (string Id, string Name)[] Lineup = [("x20_pro", "X20 Pro"), ("x20", "X20"), ("x15", "X15"), ("x10", "X10"), ("x05_pro", "X05 Pro"), ("d10", "D10"), ("dune", "Dune")];
    int artIndex; System.Windows.Threading.DispatcherTimer? rotate;

    FrameworkElement Carousel()
    {
        Image Slide(int i) => new() { Source = ControllerArt.Front(Lineup[i].Id), Height = 270, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransform = new TranslateTransform(),
            Effect = new DropShadowEffect { Color = Color.FromRgb(52, 126, 241), BlurRadius = 60, ShadowDepth = 0, Opacity = .45 } };
        Image shown = Slide(artIndex), next = Slide((artIndex + 1) % Lineup.Length); next.Opacity = 0;
        var stage = new Grid { Height = 280, ClipToBounds = false }; stage.Children.Add(shown); stage.Children.Add(next);
        var caption = Kit.Text(Lineup[artIndex].Name, 17, "DS.Text", FontWeights.SemiBold); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.Margin = new Thickness(0, 6, 0, 0);
        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        void Dots() { dots.Children.Clear(); for (int i = 0; i < Lineup.Length; i++) dots.Children.Add(new Border { Width = i == artIndex ? 22 : 7, Height = 7, CornerRadius = new CornerRadius(3.5), Margin = new Thickness(3, 0, 3, 0), Background = i == artIndex ? DS.Brush("DS.Accent") : Brush("#40FFFFFF") }); }
        Dots();
        void Advance()
        {
            int to = (artIndex + 1) % Lineup.Length; var leaving = shown; var arriving = next;
            arriving.Source = ControllerArt.Front(Lineup[to].Id); artIndex = to; Dots();
            if (DS.Motion)
            {
                ((TranslateTransform)arriving.RenderTransform).X = 110; arriving.Opacity = 0;
                ((TranslateTransform)arriving.RenderTransform).BeginAnimation(TranslateTransform.XProperty, DS.To(0, 800)); arriving.BeginAnimation(OpacityProperty, DS.To(1, 700));
                ((TranslateTransform)leaving.RenderTransform).BeginAnimation(TranslateTransform.XProperty, DS.To(-110, 800)); leaving.BeginAnimation(OpacityProperty, DS.To(0, 600));
                var fade = DS.To(0, 220); fade.Completed += (_, _) => { caption.Text = Lineup[to].Name; caption.BeginAnimation(OpacityProperty, DS.To(1, 380)); };
                caption.BeginAnimation(OpacityProperty, fade);
            }
            else { leaving.Opacity = 0; arriving.Opacity = 1; ((TranslateTransform)arriving.RenderTransform).X = 0; caption.Text = Lineup[to].Name; }
            shown = arriving; next = leaving;
        }
        rotate = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3.4) }; rotate.Tick += (_, _) => Advance(); rotate.Start();
        stage.Unloaded += (_, _) => rotate?.Stop();
        var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { stage, caption, dots } };
        return column;
    }

    sealed record Page(string Eyebrow, string Title, string Sub, UIElement Body, Button[] Left, Button[] Right);

    Button Btn(string text, bool primary, Action click, bool enabled = true)
    {
        var b = new Button { Content = text, Style = DS.Style(primary ? "PrimaryButton" : "ActionButton"), MinWidth = 140, Margin = new Thickness(primary ? 12 : 0, 0, 0, 0), IsEnabled = enabled };
        b.Click += (_, _) => click(); return b;
    }

    Page InstallPage(int index)
    {
        bool update = Installer.Installed;
        switch (index)
        {
            case 0:
            {
                var points = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 360 };
                foreach (var (head, text) in new[] { ("Your controller, your way", "Remap buttons, shape sticks and triggers, record macros."), ("PC mode", "Games see a virtual Xbox controller that follows your settings. It uses two drivers, ViGEmBus and HidHide, and asks before installing them."), ("Yours to keep", "Free and open source. No account, no analytics.") })
                {
                    var row = new StackPanel { Margin = new Thickness(0, 0, 0, 20) }; row.Children.Add(Kit.Text(head, 17, "DS.Text", FontWeights.SemiBold));
                    var t = Kit.Text(text, 15, "DS.TextSoft"); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 3, 0, 0); row.Children.Add(t); points.Children.Add(row);
                }
                var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition());
                var art = Carousel(); grid.Children.Add(points); Grid.SetColumn(art, 1); grid.Children.Add(art);
                var notice = Kit.Text("X20CTL is an independent project. It is not affiliated with, endorsed by or sponsored by EasySMX. EasySMX and its product names belong to their owners.", 12.5, "DS.TextMuted");
                notice.TextWrapping = TextWrapping.Wrap; notice.Margin = new Thickness(0, 14, 0, 0);
                var welcome = new DockPanel(); DockPanel.SetDock(notice, Dock.Bottom); welcome.Children.Add(notice); welcome.Children.Add(grid);
                return new(update ? "UPDATE" : "WELCOME", update ? "Update X20CTL" : "Install X20CTL", "Set your controller up the way you play.", welcome, [Btn("Cancel", false, Close)], [Btn("Next", true, () => Go(1))]);
            }
            case 1:
            {
                var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
                foreach (string name in new[] { "License", "Notice" })
                {
                    string tab = name; bool on = tab == licenseTab;
                    var t = Kit.Text(tab == "License" ? "License" : "Notice", 15, on ? "DS.Text" : "DS.TextMuted", FontWeights.SemiBold);
                    var item = new Border { Child = t, Padding = new Thickness(2, 4, 2, 6), Margin = new Thickness(0, 0, 22, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderBrush = on ? DS.Brush("DS.Accent") : Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 2) };
                    item.MouseLeftButtonUp += (_, _) => { licenseTab = tab; Go(1); }; tabs.Children.Add(item);
                }
                var text = new TextBlock { Text = Embedded(licenseTab == "License" ? "LICENSE.txt" : "NOTICE.txt"), FontFamily = (FontFamily)Application.Current.FindResource("DS.FontMono"), FontSize = 11.5, Foreground = DS.Brush("DS.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 16, 20, 16) };
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = text };
                var card = new Border { Background = Brush("#80060A14"), BorderBrush = DS.Brush("DS.GlassBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Child = scroll, ClipToBounds = true };
                var accept = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = accepted, VerticalAlignment = VerticalAlignment.Center };
                var acceptRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0), Children = { accept, Kit.Text("I have read the license and accept it", 15.5, "DS.Text") } };
                ((TextBlock)acceptRow.Children[1]).Margin = new Thickness(14, 0, 0, 0); ((TextBlock)acceptRow.Children[1]).VerticalAlignment = VerticalAlignment.Center;
                var next = Btn("Next", true, () => Go(2), accepted);
                accept.Checked += (_, _) => { accepted = true; next.IsEnabled = true; }; accept.Unchecked += (_, _) => { accepted = false; next.IsEnabled = false; };
                var dock = new DockPanel(); DockPanel.SetDock(tabs, Dock.Top); DockPanel.SetDock(acceptRow, Dock.Bottom); dock.Children.Add(tabs); dock.Children.Add(acceptRow); dock.Children.Add(card);
                return new("STEP 2 OF 5", "License", "Read it, then accept to continue.", dock, [Btn("Back", false, () => Go(0))], [next]);
            }
            case 2:
            {
                var list = new StackPanel();
                list.Children.Add(Option("Add X20CTL to the Start menu", "Find it by typing X20CTL.", options.StartMenu, on => options = options with { StartMenu = on }));
                list.Children.Add(Option("Add a desktop shortcut", "A shortcut next to your other apps.", options.Desktop, on => options = options with { Desktop = on }));
                list.Children.Add(Option("Keep X20CTL up to date", "Checks GitHub at launch and installs new versions by itself. Change it any time in Tools.", options.KeepUpToDate, on => options = options with { KeepUpToDate = on }));
                if (!PcModeSetup.Ready) list.Children.Add(Option("Set up PC mode drivers now", "Installs ViGEmBus and HidHide so PC mode works right away. Windows asks for permission.", options.Drivers, on => options = options with { Drivers = on }));
                var where = Kit.Text("Installs for your account only, in " + Installer.InstallDir + ". Without the drivers, PC mode asks before it installs them.", 13.5, "DS.TextMuted");
                where.TextWrapping = TextWrapping.Wrap; where.Margin = new Thickness(2, 2, 0, 0); list.Children.Add(where);
                return new("STEP 3 OF 5", "Options", "Choose how X20CTL fits in.", list, [Btn("Back", false, () => Go(1))], [Btn(update ? "Update" : "Install", true, RunInstall)]);
            }
            case 3: return new("STEP 4 OF 5", update ? "Updating" : "Installing", "This takes a moment.", Progress(), [], []);
            default:
            {
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                if (failure != null)
                {
                    stack.Children.Add(Kit.Text("Setup stopped.", 20, "DS.Warn", FontWeights.SemiBold));
                    var why = Kit.Text(failure, 15, "DS.TextSoft"); why.TextWrapping = TextWrapping.Wrap; why.Margin = new Thickness(0, 8, 0, 0); stack.Children.Add(why);
                    return new("SOMETHING WENT WRONG", "Couldn't install", "Nothing was changed on your PC beyond what you see here.", stack, [], [Btn("Close", true, Close)]);
                }
                stack.Children.Add(Done("X20CTL is installed."));
                var launch = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = options.Launch, VerticalAlignment = VerticalAlignment.Center };
                launch.Checked += (_, _) => options = options with { Launch = true }; launch.Unchecked += (_, _) => options = options with { Launch = false };
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 28, 0, 0), Children = { launch, Kit.Text("Open X20CTL now", 16, "DS.Text") } };
                ((TextBlock)row.Children[1]).Margin = new Thickness(14, 0, 0, 0); stack.Children.Add(row);
                if (driverNote != null) { var note = Kit.Text(driverNote + " PC mode will ask again when you switch it on.", 14, "DS.TextSoft"); note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 22, 0, 0); stack.Children.Add(note); }
                return new("ALL DONE", "You're set", "Plug in your controller and open X20CTL.", stack, [], [Btn("Finish", true, FinishInstall)]);
            }
        }
    }

    Page RemovePage(int index)
    {
        if (index == 0)
        {
            var list = new StackPanel();
            var what = Kit.Text("This removes X20CTL, its shortcuts and its entry in Windows Apps.", 15.5, "DS.TextSoft"); what.TextWrapping = TextWrapping.Wrap; list.Children.Add(what);
            var keep = Option("Also delete my settings", "Your setups, macros and PC mode profiles. Leave this off to keep them for a later install.", purge, on => purge = on); keep.Margin = new Thickness(0, 22, 0, 0); list.Children.Add(keep);
            var drivers = Kit.Text("The PC mode drivers (ViGEmBus and HidHide) stay installed. Remove them from Windows Apps if you don't use them elsewhere.", 13.5, "DS.TextMuted"); drivers.TextWrapping = TextWrapping.Wrap; drivers.Margin = new Thickness(2, 6, 0, 0); list.Children.Add(drivers);
            return new("REMOVE", "Remove X20CTL", "You can install it again any time.", list, [Btn("Cancel", false, Close)], [Btn("Remove", true, RunRemove)]);
        }
        if (index == 1 && failure == null && busy) return new("REMOVING", "Removing", "This takes a moment.", Progress(), [], []);
        if (failure != null) { var s = Kit.Text(failure, 15, "DS.TextSoft"); s.TextWrapping = TextWrapping.Wrap; return new("SOMETHING WENT WRONG", "Couldn't finish", "Part of X20CTL may remain.", s, [], [Btn("Close", true, Close)]); }
        return new("ALL DONE", "X20CTL is removed", "Thanks for trying it.", new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Done("X20CTL has been removed.") } }, [], [Btn("Close", true, Close)]);
    }

    FrameworkElement Option(string head, string detail, bool on, Action<bool> set)
    {
        var toggle = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = on, VerticalAlignment = VerticalAlignment.Center };
        toggle.Checked += (_, _) => set(true); toggle.Unchecked += (_, _) => set(false); DockPanel.SetDock(toggle, Dock.Right);
        var text = new StackPanel { Children = { Kit.Text(head, 16.5, "DS.Text", FontWeights.SemiBold) } }; var d = Kit.Text(detail, 14, "DS.TextSoft"); d.TextWrapping = TextWrapping.Wrap; d.Margin = new Thickness(0, 2, 0, 0); text.Children.Add(d);
        var row = new DockPanel { Margin = new Thickness(0) }; row.Children.Add(toggle); row.Children.Add(text);
        return new Border { Background = DS.Brush("DS.Glass"), BorderBrush = DS.Brush("DS.GlassBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 11, 20, 11), Margin = new Thickness(0, 0, 0, 8), Child = row };
    }

    UIElement Done(string text)
    {
        var ring = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(36), BorderBrush = DS.Brush("DS.Accent"), BorderThickness = new Thickness(2.5), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "✓", FontSize = 36, Foreground = DS.Brush("DS.Accent"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold }, Effect = new DropShadowEffect { Color = Color.FromRgb(52, 126, 241), BlurRadius = 30, ShadowDepth = 0, Opacity = .5 } };
        var t = Kit.Text(text, 20, "DS.Text", FontWeights.SemiBold); t.Margin = new Thickness(0, 18, 0, 0);
        return new StackPanel { Children = { ring, t } };
    }

    UIElement Progress()
    {
        percent = Kit.Text("0%", 64, "DS.Text", FontWeights.SemiBold); stepText = Kit.Text("", 16, "DS.TextSoft"); stepText.Margin = new Thickness(0, 6, 0, 22);
        fill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(6), Background = DS.Brush("DS.AccentFill"), Effect = new DropShadowEffect { Color = Color.FromRgb(52, 126, 241), BlurRadius = 18, ShadowDepth = 0, Opacity = .7 } };
        track = new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = DS.Brush("DS.Track"), Child = fill, ClipToBounds = false };
        track.SizeChanged += (_, _) => SetFill(fraction, false);
        return new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { percent, stepText, track } };
    }

    void SetFill(double f, bool animate)
    {
        if (fill == null || track == null || track.ActualWidth <= 0) return;
        double w = track.ActualWidth * Math.Clamp(f, 0, 1);
        if (animate && DS.Motion) fill.BeginAnimation(WidthProperty, DS.To(w, 350)); else { fill.BeginAnimation(WidthProperty, null); fill.Width = w; }
    }

    void Report((double Fraction, string Step) p)
    {
        fraction = p.Fraction; if (percent != null) percent.Text = (int)Math.Round(p.Fraction * 100) + "%"; if (stepText != null) stepText.Text = p.Step; SetFill(p.Fraction, true);
    }

    void Steps()
    {
        stepList.Children.Clear();
        for (int i = 0; i < steps.Length; i++)
        {
            bool done = i < step, now = i == step;
            var dot = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1.6), BorderBrush = now || done ? DS.Brush("DS.Accent") : Brush("#3DFFFFFF"), Background = now ? DS.Brush("DS.Accent") : Brushes.Transparent,
                Child = new TextBlock { Text = done ? "✓" : (i + 1).ToString(), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = now ? Brushes.White : done ? DS.Brush("DS.Accent") : DS.Brush("DS.TextMuted"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var label = Kit.Text(steps[i], 16, now ? "DS.Text" : done ? "DS.TextSoft" : "DS.TextMuted", now ? FontWeights.SemiBold : FontWeights.Normal); label.Margin = new Thickness(14, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center;
            stepList.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20), Children = { dot, label } });
        }
    }

    // ---------------------------------------------------------------------------------------------- work

    async void RunInstall()
    {
        busy = true; failure = null; Go(3); var started = Stopwatch.StartNew();
        var progress = new Progress<(double, string)>(Report); string? error = null;
        try { driverNote = await Task.Run(() => Installer.Install(options, progress)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException) { error = e.Message; }
        if (started.ElapsedMilliseconds < 1700) await Task.Delay(1700 - (int)started.ElapsedMilliseconds);
        busy = false; failure = error; Go(4);
    }

    async void RunRemove()
    {
        busy = true; failure = null; step = 1; var started = Stopwatch.StartNew(); Go(1);
        var progress = new Progress<(double, string)>(Report); string? error = null; bool keep = !purge; int pid = ParentPid;
        try { await Task.Run(() => Installer.Uninstall(!keep, pid, progress)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException) { error = e.Message; }
        if (started.ElapsedMilliseconds < 1700) await Task.Delay(1700 - (int)started.ElapsedMilliseconds);
        busy = false; failure = error; Go(1);
    }

    void FinishInstall()
    {
        if (failure == null && options.Launch && !Installer.InSandbox)
        {
            try { Process.Start(new ProcessStartInfo(Installer.ExePath) { UseShellExecute = true, WorkingDirectory = Installer.InstallDir }); }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { }
        }
        Close();
    }

    internal static string Embedded(string name)
    {
        using var stream = typeof(SetupWindow).Assembly.GetManifestResourceStream(name);
        if (stream == null) return "(" + name + " is not part of this build.)";
        using var reader = new StreamReader(stream); return reader.ReadToEnd().Replace("\r\n", "\n");
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
