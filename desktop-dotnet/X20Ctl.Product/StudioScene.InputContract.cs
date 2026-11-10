using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// The controller input contract, shared by every theme (3 Nov 2026): LB/RB walk the visible tabs; on Buttons the
/// controller browses until remap capture is engaged, and then every button is capture data until it completes,
/// Esc or Cancel is used, or ten seconds pass without a press.
/// </summary>
public partial class StudioScene
{
    private readonly HashSet<string> keyboardHeld = new();
    private HashSet<string> controllerHeld = new();
    private Border? captureBanner; private TextBlock? capturePrompt;
    private DispatcherTimer? captureTimeout;
    internal static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Remap capture or macro recording: nothing else may act on a press.</summary>
    public bool OwnsAllInput => CapturingRemap || InputContext == InputOwner.MacroRecording;
    /// <summary>Buttons is browsing and capture can begin (R3 on the controller, or a slot on the page).</summary>
    public bool CanEngageRemap => OnButtonsPage && buttonsPage != null && !CapturingRemap && !IsTrying && !AgentOpen && stickToolsLayer == null && trial == null;
    public void BeginRemapCapture() { if (CanEngageRemap) buttonsPage!.BeginListening(ButtonsConsolePage.Listen.Source); }
    public void CancelRemapCapture() => buttonsPage?.CancelListening();
    /// <summary>The pad's held buttons, from every accepted native sample; capture starts from them.</summary>
    public void NoteControllerHeld(IEnumerable<string> now) => controllerHeld = now.Where(b => !b.StartsWith("LS_")).ToHashSet();
    public void ObserveCapture(IEnumerable<string> now) { NoteControllerHeld(now); buttonsPage?.Observe(HeldCaptureInputs()); }
    /// <summary>Held buttons on the pad and its keyboard mirror.</summary>
    private IReadOnlyCollection<string> HeldCaptureInputs() => controllerHeld.Union(keyboardHeld).ToArray();
    /// <summary>What capture starts from: a key counts only while Windows still reports it down, so a release lost
    /// to another window can't hold capture back.</summary>
    private IReadOnlyCollection<string> HeldAtCaptureStart()
    {
        keyboardHeld.RemoveWhere(b => InputMap.Controller.FirstOrDefault(c => c.Button == b).Key is var k && (k == Key.None || !Keyboard.IsKeyDown(k)));
        return HeldCaptureInputs();
    }
    private void NotifyInputOwner() { var owner = InputContext; if (lastPageOwner != owner) { lastPageOwner = owner; InputContextChanged?.Invoke(); } UpdateHints(); }

    // ----- one tab order -----
    private string PageOfTab(RadioButton t) => Array.IndexOf(pcTabs, t) >= 0 ? "Pc" + (string)t.Content : t == DashboardNav ? "Dashboard" : t == ButtonsNav ? "Buttons" : t == CurvesNav ? "Curves" : t == MacrosNav ? "Macros" : t == VibrationNav ? "Vibration" : t == PcModeNav ? "PcMode" : t == RgbNav ? "RGB" : t == ScreenNav ? "Screen" : t == DeviceNav ? "Device" : t == MouseNav ? "Mouse" : t == KeyboardNav ? "Keyboard" : "Setups";
    /// <summary>The tabs actually on the strip, in order: the one list behind LB/RB, PageUp/PageDown and every theme.
    /// Tabs collapsed into More stay reachable from its menu, but are not a step of their own.</summary>
    internal string[] TabOrder => visibleNavigation.Select(PageOfTab).ToArray();
    internal void StepTab(int step)
    {
        string current = ManagementPage == "PcMode" && PcDashboard ? "Pc" + PcModePage.Part : CurrentPage;
        var shown = visibleNavigation;
        if (shown.Length == 0) return;
        // Tester/Diagnostics are not tabs: they step from Device, the tab they open from.
        string anchor = current is "Tester" or "Diagnostics" && navigation.Any(t => PageOfTab(t) == "Device") ? "Device" : current;
        int at = Array.FindIndex(navigation, t => PageOfTab(t) == anchor);
        if (at >= 0 && Array.IndexOf(shown, navigation[at]) < 0)
        {
            // The active page is a tab collapsed into More. Next/previous means the nearest tab still on the strip.
            int direction = Math.Sign(step);
            for (int n = 1; n <= navigation.Length; n++)
            {
                int i = ((at + direction * n) % navigation.Length + navigation.Length) % navigation.Length;
                if (Array.IndexOf(shown, navigation[i]) >= 0) { GoToTab(PageOfTab(navigation[i])); return; }
            }
            return;
        }
        if (StudioTabs.Step(shown.Select(PageOfTab).ToArray(), current, step) is { } page) GoToTab(page);
    }
    internal void GoToTab(string page)
    {
        navigation.FirstOrDefault(t => PageOfTab(t) == page)?.RaiseEvent(new RoutedEventArgs(RadioButton.ClickEvent));
    }

    // ----- text fields -----
    /// <summary>Esc (or the pad's B) leaves a field instead of going back a page.</summary>
    private void LeaveField(TextBox field)
    {
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(field), null);
        // the visible tab when there is one; themes without a tab strip keep focus on the Studio itself
        if (GetActiveTab() is { IsVisible: true } tab && tab.Focus()) return;
        FocusVisualStyle = null; Focusable = true; Focus();
    }

    // ----- the capture banner, the same on every theme -----
    private void BuildCaptureBanner()
    {
        capturePrompt = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        var hint = new TextBlock { Text = "Esc to cancel · 10 s", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(190, 220, 230, 245)), VerticalAlignment = VerticalAlignment.Center, Margin = new(18, 0, 14, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new(14, 4, 14, 4), Focusable = false, Cursor = Cursors.Hand, Tag = "capture:cancel" };
        if (TryFindResource("ConsoleAction") is Style style) cancel.Style = style;
        cancel.Click += (_, _) => CancelRemapCapture();
        captureBanner = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { capturePrompt, hint, cancel } },
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(9, 22, 45))), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(200, 125, 182, 255))),
            BorderThickness = new(1.5), CornerRadius = new(10), Padding = new(20, 10, 12, 10),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 104, 0, 0), Visibility = Visibility.Collapsed
        };
        System.Windows.Automation.AutomationProperties.SetLiveSetting(capturePrompt, System.Windows.Automation.AutomationLiveSetting.Polite);
        Panel.SetZIndex(captureBanner, 260); StudioShell.Children.Add(captureBanner);
        buttonsPage!.HeldInputs = HeldAtCaptureStart;
        buttonsPage.ListeningChanged += CaptureChanged;
    }
    private string? lastCapturePrompt;
    private void CaptureChanged()
    {
        bool on = CapturingRemap; string? prompt = on ? buttonsPage!.CapturePrompt : null;
        if (captureBanner != null) { captureBanner.Visibility = on ? Visibility.Visible : Visibility.Collapsed; if (prompt != null) capturePrompt!.Text = prompt; }
        if (on) { captureTimeout ??= new DispatcherTimer(CaptureTimeout, DispatcherPriority.Input, (_, _) => { captureTimeout!.Stop(); CancelRemapCapture(); }, Dispatcher); if (prompt != lastCapturePrompt || !captureTimeout.IsEnabled) { captureTimeout.Stop(); captureTimeout.Start(); } }
        else captureTimeout?.Stop();
        bool changed = prompt != lastCapturePrompt; lastCapturePrompt = prompt;
        NotifyInputOwner();
    }
}
