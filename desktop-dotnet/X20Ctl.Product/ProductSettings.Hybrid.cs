using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    private Grid? hybridSettingsRoot;
    private Canvas? hybridSettingsBoard;
    private static Brush HSColor(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock HSText(string text, double size, string color, FontWeight weight) => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = HSColor(color), FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
    private static void HSAt(Canvas canvas, UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); canvas.Children.Add(element); }
    private static Button HSButton(string label, string tag, double width, double height, bool selected = false)
    {
        var b = new Button { Content = label, Tag = tag, Width = width, Height = height, MinHeight = 0, FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = HSColor(selected ? "#1b1622" : "#f4f1f8"), Background = HSColor(selected ? "#d8c6ed" : "#2d2933"), BorderBrush = HSColor(selected ? "#52dcff" : "#514a58"), BorderThickness = new(selected ? 3 : 1), Cursor = System.Windows.Input.Cursors.Hand };
        void Paint() { b.BorderBrush = HSColor(b.IsKeyboardFocused || b.IsMouseOver || selected ? "#52dcff" : "#514a58"); b.BorderThickness = new(b.IsKeyboardFocused || selected ? 3 : 1); }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); return b;
    }
    private void BuildHybridSettings()
    {
        hybridSettingsRoot = new Grid { Background = HSColor("#17151c") }; Children.Add(hybridSettingsRoot);
        hybridSettingsBoard = new Canvas { Width = 1600, Height = 900, Background = HSColor("#17151c"), ClipToBounds = true }; hybridSettingsRoot.Children.Add(new Viewbox { Child = hybridSettingsBoard, Stretch = Stretch.Uniform });
        HybridSettingsSync();
    }
    private void HybridSettingsSync()
    {
        if (hybridSettingsRoot == null || hybridSettingsBoard == null || pageGrid == null) return;
        bool custom = hub.Visibility == Visibility.Visible || themes.Visibility == Visibility.Visible || feedback.Visibility == Visibility.Visible || about.Visibility == Visibility.Visible || pcMode.Visibility == Visibility.Visible;
        hybridSettingsRoot.Visibility = custom ? Visibility.Visible : Visibility.Collapsed; pageGrid.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        if (!custom) return;
        var board = hybridSettingsBoard; board.Children.Clear(); bool picker = themes.Visibility == Visibility.Visible;
        if (feedback.Visibility == Visibility.Visible)
        {
            var form = FeedbackForm; if (form.Parent is ScrollViewer original) original.Content = null; else if (form.Parent is Panel panel) panel.Children.Remove(form); HSAt(board, form, 0, 0); return;
        }
        if (about.Visibility == Visibility.Visible) { HybridSettingsAbout(board); return; }
        if (pcMode.Visibility == Visibility.Visible) { HybridSettingsPcMode(board); return; }
        HSAt(board, new Rectangle { Width = 156, Height = 900, Fill = HSColor("#cbb5e4") }, 0, 0); HSAt(board, new Rectangle { Width = 140, Height = 900, Fill = HSColor("#29262f") }, 1460, 0);
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); var logo = new Image { Width = 112, Height = 78, Stretch = Stretch.Uniform }; if (System.IO.File.Exists(path)) logo.Source = new BitmapImage(new Uri(path)); HSAt(board, logo, 22, 20);
        HSAt(board, HSText(picker ? "Themes" : "Tools", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 38);
        HSAt(board, HSText(picker ? "Changes how X20CTL looks. Your controllers and drafts stay as they are." : "Help, extras and everything about X20CTL", 14, "#bdb4c8", FontWeights.SemiBold), 218, 99);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HSButton(label, "hybrid:tools:nav:" + key, 120, 50, key == "tools"); nav.Click += (_, _) => { if (key != "tools") RebelNavigate?.Invoke(key); }; HSAt(board, nav, 18, 150 + i * 67); }
        var back = HSButton(picker ? "Back to Tools" : "Back", "hybrid:tools:back", 130, 40); back.Click += (_, _) => { if (picker) ShowThemes(false); else Click(Back); }; HSAt(board, back, 1250, 55);
        if (picker) HybridSettingsThemes(board); else HybridSettingsHub(board);
        HybridSettingsFocus(picker ? "hybrid:theme:" + ChosenTheme : "hybrid:tools:check");
    }
    private void HybridSettingsHub(Canvas board)
    {
        HSAt(board, new Border { Width = 1168, Height = 176, Background = HSColor("#211e27"), CornerRadius = new(24) }, 216, 150);
        HSAt(board, HSText("Controller not working?", 24, "#f4f1f8", FontWeights.Bold), 244, 170);
        HSAt(board, HSText("Read scan, then Write scan, then send both reports online together.", 14, "#bdb4c8", FontWeights.Normal), 244, 207);
        var check = HSButton("1  Read scan", "hybrid:tools:check", 250, 55, true); check.Click += (_, _) => Click(Check); HSAt(board, check, 244, 246);
        var write = HSButton("2  Write scan", "hybrid:tools:write-quick", 250, 55); write.Click += (_, _) => Click(WriteScan); HSAt(board, write, 512, 246);
        var items = new (string label, string detail, Button target)[] { ("PC mode", "Remaps, turbo and macros", PcMode), ("Write scan", "Learn what an app writes", WriteScan), ("Replay intro", "Watch the opening again", Replay), ("About", "About X20CTL", About), ("Support", "Support X20CTL", Support), ("GitHub", "Open the repository", Star), ("Feedback", "Send feedback", Feedback), ("Themes", "Change the app's look", Themes) };
        for (int i = 0; i < items.Length; i++)
        {
            var (label, detail, target) = items[i]; var tile = HSButton(label + "\n" + detail, "hybrid:tools:tile:" + label, 274, 160); tile.Click += (_, _) => Click(target); HSAt(board, tile, 216 + i % 4 * 298, 356 + i / 4 * 185);
        }
        var auto = new CheckBox { Content = "Check for updates automatically", Foreground = HSColor("#f4f1f8"), FontSize = 14 }; auto.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = autoUpdate, Mode = BindingMode.TwoWay }); HSAt(board, auto, 216, 750);
        var update = HSButton("Check for updates", "hybrid:tools:update", 210, 40); update.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = updateCheck }); update.Click += (_, _) => Click(updateCheck); HSAt(board, update, 620, 740);
        var status = HSText("", 14, "#bdb4c8", FontWeights.Normal); status.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = updateStatus }); HSAt(board, status, 850, 750);
        if (updateGo.Visibility == Visibility.Visible) { var go = HSButton("Update now", "hybrid:tools:update-go", 180, 40); go.Click += (_, _) => Click(updateGo); HSAt(board, go, 1200, 740); }
    }
    private void HybridSettingsThemes(Canvas board)
    {
        string chosen = ChosenTheme; var status = HSText(chosen == ThemeCatalog.Current.Id ? "" : ThemeCatalog.Find(chosen).Name + " starts the next time X20CTL opens.", 14, "#bdb4c8", FontWeights.Normal); HSAt(board, status, 216, 128);
        int i = 0; foreach (var theme in ThemeCatalog.All)
        {
            bool active = theme.Id == ThemeCatalog.Current.Id, selected = theme.Id == chosen; var content = new StackPanel();
            var thumb = new Image { Width = 192, Height = 108, Stretch = Stretch.UniformToFill }; string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", theme.Id, "thumb.png"); if (System.IO.File.Exists(path)) thumb.Source = new BitmapImage(new Uri(path)); content.Children.Add(thumb);
            content.Children.Add(HSText(theme.Name, 18, selected ? "#1b1622" : "#f4f1f8", FontWeights.Bold)); if (active || selected) content.Children.Add(HSText(active ? "In use" : "Next", 13, selected ? "#5c4075" : "#bdb4c8", FontWeights.SemiBold));
            var card = HSButton("", "hybrid:theme:" + theme.Id, 212, 206, selected); card.Content = content; card.IsEnabled = preferences != null;
            card.Click += (_, _) => { try { preferences?.SetTheme(theme.Id); HybridSettingsSync(); } catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { status.Text = "Couldn't save this setting: " + error.Message; } };
            HSAt(board, card, 216 + i % 5 * 238, 168 + i / 5 * 232); i++;
        }
        var launch = new CheckBox { Content = "Opening intro at launch", Foreground = HSColor("#f4f1f8"), FontSize = 16 }; launch.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = introLaunch, Mode = BindingMode.TwoWay }); launch.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = introLaunch }); HSAt(board, launch, 244, 695);
        if (introSound.IsVisible || IntroView.HasIntroScore) { var sound = new CheckBox { Content = "Sound", Foreground = HSColor("#f4f1f8"), FontSize = 16 }; sound.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = introSound, Mode = BindingMode.TwoWay }); HSAt(board, sound, 610, 695); }
        if (chosen != ThemeCatalog.Current.Id && !ReviewSandbox.Active) { var restart = HSButton("Restart now", "hybrid:theme:restart", 190, 44, true); restart.Click += (_, _) => RequestThemeRestart(); HSAt(board, restart, 1140, 685); }
    }
    private void HybridSettingsAbout(Canvas board)
    {
        HSAt(board, new Rectangle { Width = 640, Height = 900, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e7dbf7"), (Color)ColorConverter.ConvertFromString("#b89fd1"), 90) }, 0, 0);
        HSAt(board, new Rectangle { Width = 960, Height = 900, Fill = HSColor("#211e27") }, 640, 0); HSAt(board, new Rectangle { Width = 4, Height = 900, Fill = HSColor("#52dcff") }, 638, 0);
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); var logo = new Image { Width = 440, Height = 310, Stretch = Stretch.Uniform }; if (System.IO.File.Exists(path)) logo.Source = new BitmapImage(new Uri(path)); HSAt(board, logo, 96, 150);
        HSAt(board, HSText("X20CTL", 64, "#1b1622", FontWeights.Black), 110, 480);
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "preview";
        HSAt(board, HSText("Native Controller Studio · " + X20Ctl.Zone.ReleaseVersions.Display(version), 18, "#5c4075", FontWeights.SemiBold), 110, 560);
        var body = new StackPanel { Width = 820 }; body.Children.Add(HSText("About", 34, "#f5f1fa", FontWeights.ExtraBold));
        foreach (string paragraph in new[] { "X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works on your own PC.", "Changes stay local until they can be sent to a connected, verified controller. Nothing leaves your PC unless you choose to send a report or feedback.", "Made by Amjad AAYD.", "Special thanks to Kiin from Team EasySMX for sending a free X20 Pro controller.", "Console-style inspiration: ApexSenseBridge by ReynArts.", "Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0. See the distributed open-source license notices." }) { var text = HSText(paragraph, 16, "#d9d3e3", FontWeights.Normal); text.TextWrapping = TextWrapping.Wrap; text.Margin = new(0, 14, 0, 0); body.Children.Add(text); }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 24, 0, 0) };
        var back = HSButton("Back to Tools", "hybrid:about:back", 170, 45, true); back.Click += (_, _) => ShowAbout(false); actions.Children.Add(back);
        var repo = HSButton("X20CTL on GitHub", "hybrid:about:github", 210, 45); repo.Margin = new(12, 0, 0, 0); repo.Click += (_, _) => Click(Star); actions.Children.Add(repo);
        var support = HSButton("Support X20CTL", "hybrid:about:support", 190, 45); support.Margin = new(12, 0, 0, 0); support.Click += (_, _) => Click(Support); actions.Children.Add(support); body.Children.Add(actions);
        HSAt(board, body, 712, 96); HybridSettingsFocus("hybrid:about:back");
    }
    private ScrollViewer? hybridToolsPcScroll;
    private void HybridSettingsPcMode(Canvas board)
    {
        HSAt(board, new Rectangle { Width = 156, Height = 900, Fill = HSColor("#cbb5e4") }, 0, 0); HSAt(board, new Rectangle { Width = 140, Height = 900, Fill = HSColor("#29262f") }, 1460, 0);
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); var logo = new Image { Width = 112, Height = 78, Stretch = Stretch.Uniform }; if (System.IO.File.Exists(path)) logo.Source = new BitmapImage(new Uri(path)); HSAt(board, logo, 22, 20);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HSButton(label, "hybrid:pc-tools:nav:" + key, 120, 50, key == "pcmode"); nav.Click += (_, _) => { if (key == "tools") ShowPcMode(false); else if (key != "pcmode") RebelNavigate?.Invoke(key); }; HSAt(board, nav, 18, 150 + i * 67); }
        HSAt(board, HSText("PC mode", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 38); HSAt(board, HSText("Route controller input through your saved PC profile.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 99);
        var back = HSButton("Back to Tools", "hybrid:pc-tools:back", 160, 40); back.Click += (_, _) => ShowPcMode(false); HSAt(board, back, 1224, 55);
        var route = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string label in new[] { "Controller", "PC profile", "Virtual controller", "Game" }) { var block = new Border { Width = 250, Height = 58, CornerRadius = new(14), BorderBrush = HSColor("#52dcff"), BorderThickness = new(2), Background = HSColor("#2d2933"), Margin = new(0, 0, 18, 0), Child = HSText(label, 16, "#f5f1fa", FontWeights.Bold) }; ((TextBlock)block.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)block.Child).VerticalAlignment = VerticalAlignment.Center; route.Children.Add(block); }
        HSAt(board, route, 216, 150);
        if (pcMode.Children.OfType<ScrollViewer>().FirstOrDefault() is { } current) { pcMode.Children.Remove(current); hybridToolsPcScroll = current; }
        if (hybridToolsPcScroll is { } content) { content.Width = 588; content.Height = 610; content.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto; content.Tag = "hybrid:pc-tools:workbench"; HSAt(board, content, 216, 225); }
        HSAt(board, new Rectangle { Width = 2, Height = 610, Fill = HSColor("#52dcff") }, 846, 225);
        var detail = new StackPanel { Margin = new(24) };
        detail.Children.Add(HSText("How PC mode works", 25, "#f5f1fa", FontWeights.ExtraBold));
        foreach (string line in new[] { "1   Choose the player whose settings you want to edit.", "2   Turn on PC mode when its required drivers are ready.", "3   Open that controller's Studio to edit buttons, curves and macros.", "4   Games receive the virtual controller's input." })
        { var text = HSText(line, 16, "#d9d3e3", FontWeights.Normal); text.TextWrapping = TextWrapping.Wrap; text.Margin = new(0, 18, 0, 0); detail.Children.Add(text); }
        var note = HSText("Changes apply on this PC. Nothing is written to your controller.", 14, "#bdb4c8", FontWeights.SemiBold); note.TextWrapping = TextWrapping.Wrap; note.Margin = new(0, 26, 0, 0); detail.Children.Add(note);
        HSAt(board, new Border { Width = 492, Height = 548, CornerRadius = new(22), Background = HSColor("#24212b"), Child = detail }, 892, 225);
        HybridSettingsFocus("hybrid:pc-tools:back");
    }
    private void HybridSettingsFocus(string tag) => Dispatcher.BeginInvoke(() => { if (hybridSettingsBoard != null && FindTag(hybridSettingsBoard, tag) is { } control) control.Focus(); }, DispatcherPriority.ApplicationIdle);
    internal void ConfirmHybridRestart(Action restart)
    {
        if (hybridSettingsBoard == null || hybridSettingsRoot?.IsVisible != true) return;
        var board = hybridSettingsBoard;
        HSAt(board, new Rectangle { Width = 1600, Height = 900, Fill = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)) }, 0, 0);
        HSAt(board, new Border { Width = 600, Height = 240, CornerRadius = new(24), Background = HSColor("#2d2933"), BorderBrush = HSColor("#52dcff"), BorderThickness = new(2) }, 500, 300);
        HSAt(board, HSText("Restart X20CTL?", 26, "#f5f1fa", FontWeights.Bold), 534, 328);
        var warning = HSText("Unsaved controller drafts will be lost. Save your work before restarting.", 15, "#c8bdd1", FontWeights.Normal); warning.Width = 520; warning.TextWrapping = TextWrapping.Wrap; HSAt(board, warning, 534, 374);
        var keep = HSButton("Go back", "hybrid:restart:cancel", 220, 45, true); keep.Click += (_, _) => HybridSettingsSync(); HSAt(board, keep, 534, 455);
        var yes = HSButton("Restart", "hybrid:restart:confirm", 220, 45); yes.Click += (_, _) => restart(); HSAt(board, yes, 796, 455); HybridSettingsFocus("hybrid:restart:cancel");
    }
}
