using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private Grid? hybridDashboard, hybridButtons, hybridCurves, hybridMacros, hybridVibration, hybridPcMode, hybridDevice, hybridDiagnostics, hybridTester, hybridMouse, hybridSetups;
    private Button? hybridReturn;
    private bool hybridStudio, hybridGames, hybridQueued;
    private Grid? hybridPreviousPage;
    private static Brush HBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    private static void HAt(Canvas canvas, UIElement child, double x, double y) { Canvas.SetLeft(child, x); Canvas.SetTop(child, y); canvas.Children.Add(child); }
    private static TextBlock HText(string value, double size, string color, FontWeight weight) => new() { Text = value, FontSize = size, Foreground = HBrush(color), FontWeight = weight, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
    private void BuildHybridStudio()
    {
        if (ThemeCatalog.Current.Id != "hybrid" || hybridStudio) return;
        hybridStudio = true;
        hybridDashboard = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridDashboard" };
        StudioShell.Children.Add(hybridDashboard);
        hybridButtons = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridButtons" };
        StudioShell.Children.Add(hybridButtons);
        hybridCurves = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridCurves" };
        StudioShell.Children.Add(hybridCurves);
        hybridMacros = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridMacros" };
        StudioShell.Children.Add(hybridMacros);
        hybridVibration = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridVibration" };
        StudioShell.Children.Add(hybridVibration);
        hybridPcMode = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridPcMode" };
        StudioShell.Children.Add(hybridPcMode);
        hybridDevice = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridDevice" };
        StudioShell.Children.Add(hybridDevice);
        hybridDiagnostics = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridDiagnostics" };
        StudioShell.Children.Add(hybridDiagnostics);
        hybridTester = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridTester" };
        StudioShell.Children.Add(hybridTester);
        hybridMouse = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridMouse" };
        StudioShell.Children.Add(hybridMouse);
        hybridSetups = new Grid { Width = 1600, Height = 848, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 52, 0, 0), Visibility = Visibility.Collapsed, ClipToBounds = true, Name = "HybridSetups" };
        StudioShell.Children.Add(hybridSetups);
        hybridReturn = new Button { Content = "Return to Hybrid", Tag = "hybrid:games:return", Width = 150, Height = 34, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 56, 24, 0), Visibility = Visibility.Collapsed };
        hybridReturn.Click += (_, _) => { hybridGames = false; HybridShow(); }; StudioShell.Children.Add(hybridReturn);
        HybridLater();
    }
    private void HybridLater()
    {
        if (!hybridStudio || hybridQueued) return;
        hybridQueued = true; Dispatcher.BeginInvoke(() => { hybridQueued = false; HybridShow(); }, DispatcherPriority.Loaded);
    }
    private void HybridShow()
    {
        if (!hybridStudio || hybridDashboard == null || hybridReturn == null) return;
        bool dashboard = ManagementPage == "Dashboard";
        bool buttons = OnButtonsPage;
        if (hybridButtons != null) { hybridButtons.Visibility = buttons ? Visibility.Visible : Visibility.Collapsed; if (buttons) HybridButtonsPage(); }
        if (hybridCurves != null) { hybridCurves.Visibility = IsCurves ? Visibility.Visible : Visibility.Collapsed; if (IsCurves) HybridCurvesPage(); }
        if (hybridMacros != null) { hybridMacros.Visibility = IsMacros ? Visibility.Visible : Visibility.Collapsed; if (IsMacros) HybridMacrosPage(); }
        if (hybridVibration != null) { hybridVibration.Visibility = IsVibration ? Visibility.Visible : Visibility.Collapsed; if (IsVibration) HybridVibrationPage(); }
        if (hybridPcMode != null) { hybridPcMode.Visibility = ManagementPage == "PcMode" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "PcMode") HybridPcModePage(); }
        if (hybridDevice != null) { hybridDevice.Visibility = ManagementPage == "Device" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "Device") HybridDevicePage(); }
        if (hybridDiagnostics != null) { hybridDiagnostics.Visibility = ManagementPage == "Diagnostics" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "Diagnostics") HybridDiagnosticsPage(); }
        if (hybridTester != null) { hybridTester.Visibility = ManagementPage == "Tester" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "Tester") HybridTesterPage(); }
        if (hybridMouse != null) { hybridMouse.Visibility = ManagementPage == "Mouse" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "Mouse") HybridMousePage(); }
        if (hybridSetups != null) { hybridSetups.Visibility = ManagementPage == "Setups" ? Visibility.Visible : Visibility.Collapsed; if (ManagementPage == "Setups") HybridSetupsPage(); }
        if (!dashboard) hybridGames = false;
        bool newlyVisible = dashboard && !hybridGames && hybridDashboard.Visibility != Visibility.Visible;
        hybridDashboard.Visibility = dashboard && !hybridGames ? Visibility.Visible : Visibility.Collapsed;
        hybridReturn.Visibility = dashboard && hybridGames ? Visibility.Visible : Visibility.Collapsed;
        var visiblePage = new[] { hybridDashboard, hybridButtons, hybridCurves, hybridMacros, hybridVibration, hybridPcMode, hybridDevice, hybridDiagnostics, hybridTester, hybridMouse, hybridSetups }.FirstOrDefault(page => page?.Visibility == Visibility.Visible);
        if (!ReferenceEquals(visiblePage, hybridPreviousPage))
        {
            if (animate && visiblePage != null && hybridPreviousPage != null)
            {
                var slide = new TranslateTransform(); visiblePage.RenderTransform = slide;
                slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
            hybridPreviousPage = visiblePage;
        }
        if (!dashboard || hybridGames) return;
        if (hybridDashboard.Children.Count > 0) { if (newlyVisible) HybridFocusDashboard(); return; }
        Dashboard ??= CreateDashboard(); hybridDashboard.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, ClipToBounds = true }; hybridDashboard.Children.Add(board);
        board.Children.Add(new Rectangle { Width = 800, Height = 848, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e8dcf8"), (Color)ColorConverter.ConvertFromString("#bba4d1"), 90) });
        var right = new Rectangle { Width = 800, Height = 848, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#38333e"), (Color)ColorConverter.ConvertFromString("#19171e"), 90) }; HAt(board, right, 800, 0);
        HAt(board, new Rectangle { Width = 5, Height = 848, Fill = HBrush("#52dcff"), Opacity = .75 }, 798, 0);
        HAt(board, HText($"PLAYER {Player + 1}", 14, "#5c4075", FontWeights.Bold), 52, 216);
        HAt(board, HText(Model.Name, 40, "#1b1622", FontWeights.ExtraBold), 52, 242);
        var finish = PadColours.Catalog.For(Model.Id).FirstOrDefault(f => f.Id == PadColours.For(Player, Model.Id))?.Name;
        HAt(board, HText(finish ?? "", 20, "#5c4075", FontWeights.SemiBold), 52, 296);
        var facts = new (string title, TextBlock source)[] { ("CURRENT SETUP", Dashboard.RebelSetup), ("CONNECTION", Dashboard.RebelConnection), ("BATTERY", Dashboard.RebelBattery), ("CHANGES", Dashboard.RebelChanges) };
        for (int i = 0; i < facts.Length; i++)
        {
            double x = 52 + i % 2 * 190, y = 334 + i / 2 * 70;
            HAt(board, HText(facts[i].title, 11, "#6d5786", FontWeights.Bold), x, y);
            var value = HText("", 16, "#1b1622", FontWeights.SemiBold); value.Width = 180; value.TextTrimming = TextTrimming.CharacterEllipsis; value.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = facts[i].source }); HAt(board, value, x, y + 19);
        }
        HAt(board, Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 690), 455, 60);
        var hints = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var (key, label) in new[] { ("A", "Select"), ("B", "Controller Zone"), ("X", "Configure"), ("Y", "Rescan games") }) hints.Children.Add(HText($"{key}    {label}", 17, "#f4f1f8", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 18)));
        HAt(board, hints, 1350, 210);
        HAt(board, new Border { Width = 564, Height = 128, Background = HBrush("#445c4075"), CornerRadius = new(64, 0, 0, 64) }, 236, 624);
        HAt(board, new Border { Width = 548, Height = 128, Background = HBrush("#203d3846"), CornerRadius = new(0, 64, 64, 0) }, 816, 624);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++)
        {
            var (label, key) = pages[i]; var button = new Button { Content = label, Tag = "hybrid:dashboard:" + key, Width = 112, Height = 78, MinHeight = 0, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = HBrush(i < 4 ? "#1b1622" : "#f4f1f8"), Background = HBrush(i < 4 ? "#f7f1ff" : "#494352"), BorderBrush = HBrush("#52dcff"), BorderThickness = new(2), Cursor = System.Windows.Input.Cursors.Hand };
            void Paint() { bool focused = button.IsKeyboardFocused || button.IsMouseOver; button.BorderThickness = new(focused ? 5 : 2); button.BorderBrush = HBrush(focused ? "#52dcff" : "#777080"); }
            button.GotKeyboardFocus += (_, _) => Paint(); button.LostKeyboardFocus += (_, _) => Paint(); button.MouseEnter += (_, _) => Paint(); button.MouseLeave += (_, _) => Paint(); Paint();
            System.Windows.Automation.AutomationProperties.SetName(button, "Open " + label);
            button.Click += (_, _) => HybridGo(key); HAt(board, button, 304 + i * 124, 650);
        }
        var games = new Button { Content = "Games", Tag = "hybrid:dashboard:games", Width = 120, Height = 38, MinHeight = 0, Background = HBrush("#1b1622"), Foreground = Brushes.White };
        games.Click += (_, _) => { hybridGames = true; HybridShow(); Dashboard?.FocusHero(); }; HAt(board, games, 52, 500);
        if (newlyVisible) HybridFocusDashboard();
    }
    private void HybridFocusDashboard() => Dispatcher.BeginInvoke(() =>
    {
        if (hybridDashboard?.Visibility == Visibility.Visible && hybridDashboard.Children.OfType<Canvas>().FirstOrDefault()?.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == "hybrid:dashboard:buttons") is { } button) button.Focus();
    }, DispatcherPriority.ApplicationIdle);
    private void HybridGo(string key)
    {
        switch (key) { case "buttons": ShowButtons(); break; case "curves": ShowCurves(true); break; case "macros": ShowMacros(true); break; case "vibration": ShowVibration(true); break; case "pcmode": ShowManagement("PcMode"); break; case "device": ShowManagement("Device"); break; case "mouse": ShowManagement("Mouse"); break; case "setups": ShowManagement("Setups"); break; case "tools": ToolsRequested?.Invoke(); break; }
    }
    private bool HybridDashboardKey(KeyEventArgs e)
    {
        if (!hybridStudio || ManagementPage != "Dashboard" || AgentOpen || stickToolsLayer != null || e.Key is not (Key.B or Key.Escape)) return false;
        if (hybridGames) { hybridGames = false; HybridShow(); } else BackRequested?.Invoke();
        e.Handled = true; return true;
    }
}
