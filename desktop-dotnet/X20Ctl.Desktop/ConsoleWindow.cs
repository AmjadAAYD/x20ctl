using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using X20Ctl.Desktop.Views;
using X20Ctl.Simulation;
namespace X20Ctl.Desktop;
public class ConsoleWindow : Window
{
    private readonly Navigation navigation = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private bool navigationDemo;
    private Point mousePosition;
    private readonly PointerMode pointerMode = new();
    protected IInputWorkspace? Workspace { get; set; }
    public ConsoleWindow()
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowChrome.SetWindowChrome(this, new() { CaptionHeight = 64, ResizeBorderThickness = new(7), GlassFrameThickness = new(0), CornerRadius = new(16), UseAeroCaptionButtons = false });
        SourceInitialized += (_, _) => { int rounded = 2; _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref rounded, sizeof(int)); };
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, _) => { if (navigationDemo) navigation.Read(new(0, true, "simulation:keyboard-nav", clock.Elapsed.TotalMilliseconds, Buttons.None), IsActive, Workspace?.Model.CaptureOwnsInput == true); };
        MouseMove += (_, e) => { mousePosition = e.GetPosition(this); if (navigationDemo && pointerMode.Observe(mousePosition.X, mousePosition.Y)) SetDemo(false); };
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F6) { SetDemo(!navigationDemo); e.Handled = true; return; }
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { if (Workspace?.Model.CaptureOwnsInput != true) Workspace?.CycleInspector(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1); e.Handled = true; return; }
        if (!navigationDemo || Keyboard.FocusedElement is TextBox) return;
        var buttons = e.Key switch { Key.Up => Buttons.Up, Key.Down => Buttons.Down, Key.Left => Buttons.Left, Key.Right => Buttons.Right, Key.Enter => Buttons.A, Key.Escape => Buttons.B, Key.Q => Buttons.LB, Key.E => Buttons.RB, _ => Buttons.None };
        if (buttons == Buttons.None) return;
        var action = navigation.Read(new(0, true, "simulation:keyboard-nav", clock.Elapsed.TotalMilliseconds, buttons), IsActive, Workspace?.Model.CaptureOwnsInput == true);
        switch (action)
        {
            case NavigationAction.PreviousTab: Workspace?.CycleInspector(-1); break;
            case NavigationAction.NextTab: Workspace?.CycleInspector(1); break;
            case NavigationAction.Activate:
                if (Keyboard.FocusedElement is RadioButton radio) radio.IsChecked = true;
                else if (Keyboard.FocusedElement is CheckBox check) check.IsChecked = check.IsChecked != true;
                else if (Keyboard.FocusedElement is ComboBox combo) combo.IsDropDownOpen = !combo.IsDropDownOpen;
                else if (Keyboard.FocusedElement is ButtonBase button) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                break;
            case NavigationAction.Back: SetDemo(false); break;
            case NavigationAction.Up: MoveFocus(new(FocusNavigationDirection.Up)); break;
            case NavigationAction.Down: MoveFocus(new(FocusNavigationDirection.Down)); break;
            case NavigationAction.Left: MoveFocus(new(FocusNavigationDirection.Left)); break;
            case NavigationAction.Right: MoveFocus(new(FocusNavigationDirection.Right)); break;
        }
        e.Handled = true;
    }
    private void SetDemo(bool enabled)
    {
        navigationDemo = enabled; navigation.Reset();
        if (enabled) pointerMode.EnterDemo(mousePosition.X, mousePosition.Y);
        if (Workspace != null) Workspace.Model.NavigationDemo = enabled;
    }
}
