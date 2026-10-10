using X20Ctl.Desktop.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
namespace X20Ctl.Desktop;
public partial class ScannerWindow : ConsoleWindow
{
    public WorkspaceModel Model { get; } = new(true);
    public ScannerWindow()
    {
        InitializeComponent(); DataContext = Model; InputView.DataContext = Model; Workspace = InputView;
        Scene.SizeChanged += (_, _) => Scene.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, Scene.ActualWidth, Scene.ActualHeight), WindowState == WindowState.Maximized ? 0 : 21, WindowState == WindowState.Maximized ? 0 : 21);
        WindowChrome.SetWindowChrome(this, new() { CaptionHeight = 92, ResizeBorderThickness = new(7), GlassFrameThickness = new(0), CornerRadius = new(22), UseAeroCaptionButtons = false });
        PreviewKeyDown += (_, e) => { if (e.Key == Key.F12) { InputView.ToggleDeveloperDrawer(); e.Handled = true; } };
        StateChanged += (_, _) => { ConsoleFrame.Margin = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(20); ConsoleFrame.CornerRadius = new(WindowState == WindowState.Maximized ? 0 : 22); };
    }
    private void Minimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeRestore(object sender, RoutedEventArgs e) { if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    public void OpenOriginalScanner() => new Views.InputWorkspace { DataContext = Model }.LaunchOriginalScanner();
}
