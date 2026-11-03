using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    internal FrameworkElement? ActiveThemeNotice => (FrameworkElement?)ActiveSpotlightNotice ?? (FrameworkElement?)ActiveNexusNotice ?? (FrameworkElement?)ActiveTideNotice ?? (FrameworkElement?)ActiveDuskNotice ?? (FrameworkElement?)ActiveVoltNotice ?? ActiveBlossomNotice;
    private IInputElement? themeNoticePreviousFocus;
    private static IEnumerable<DependencyObject> NoticeChildren(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (VisualTreeHelper.GetChild(root, i) is {} visualChild)
                foreach (var child in NoticeChildren(visualChild)) yield return child;
    }
    private static Button? ThemeNoticeButton(FrameworkElement notice, string choice) => NoticeChildren(notice).OfType<Button>().FirstOrDefault(button => button.Tag as string == "notice:" + choice);
    private void ArmThemeNotice(FrameworkElement notice)
    {
        themeNoticePreviousFocus = Keyboard.FocusedElement;
        KeyboardNavigation.SetTabNavigation(notice, KeyboardNavigationMode.Cycle);
        Dispatcher.BeginInvoke(() => { if (ReferenceEquals(ActiveThemeNotice, notice)) ThemeNoticeButton(notice, "no")?.Focus(); }, DispatcherPriority.Input);
        _ = UpdateNativeContext();
    }
    private void ReleaseThemeNotice(FrameworkElement notice)
    {
        if (themeNoticePreviousFocus is UIElement element && element.IsVisible && element.IsEnabled) element.Focus();
        themeNoticePreviousFocus = null;
    }
    private bool HandleThemeNoticeKey(KeyEventArgs e)
    {
        if (ActiveThemeNotice is not {} notice) return false;
        if (e.Key is Key.Escape or Key.B) ThemeNoticeButton(notice, "no")?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else if (e.Key is Key.Enter or Key.A && !e.IsRepeat && Keyboard.FocusedElement is Button button && notice.IsKeyboardFocusWithin) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else if (e.Key is Key.Left or Key.Up) ThemeNoticeButton(notice, "no")?.Focus();
        else if (e.Key is Key.Right or Key.Down) ThemeNoticeButton(notice, "yes")?.Focus();
        if (e.Key != Key.Tab) e.Handled = true;
        return true;
    }
    internal bool ConfirmThemeDrivers(IReadOnlyList<string> names)
    {
        if (ActiveThemeNotice != null || ActivePulseNotice != null) return false;
        bool accepted = false; var frame = new DispatcherFrame();
        void Answer(bool yes) { accepted = yes; frame.Continue = false; }
        switch (ThemeCatalog.Current.Id)
        {
            case "pulse": return ConfirmPulseDrivers(names);
            case "spotlight": ShowSpotlightDriverNotice(names, Answer); break;
            case "nexus": ShowNexusDriverNotice(names, Answer); break;
            case "tide": ShowTideDriverNotice(names, Answer); break;
            case "dusk": ShowDuskDriverNotice(names, Answer); break;
            case "volt": ShowVoltDriverNotice(names, Answer); break;
            case "blossom": ShowBlossomDriverNotice(names, Answer); break;
            default: return false;
        }
        if (ReviewSandbox.Active) { ThemeNoticeButton(ActiveThemeNotice!, "no")?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return false; }
        Dispatcher.PushFrame(frame); return accepted;
    }
}
