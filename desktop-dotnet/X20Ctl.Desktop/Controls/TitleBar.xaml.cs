using System.Windows;
using System.Windows.Controls;
namespace X20Ctl.Desktop.Controls;
public partial class TitleBar : UserControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(TitleBar), new PropertyMetadata("EasySMX X20 / Player 1"));
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public TitleBar() { InitializeComponent(); Loaded += (_, _) => { if (Window.GetWindow(this) is ScannerWindow) { ScannerLink.Content = "Original scanner ↗"; System.Windows.Automation.AutomationProperties.SetName(ScannerLink, "Open preserved Scanner 2.0.0"); } }; }
    private void Minimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(Window.GetWindow(this));
    private void MaximizeRestore(object sender, RoutedEventArgs e) { var window = Window.GetWindow(this); if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window); else SystemCommands.MaximizeWindow(window); }
    private void CloseWindow(object sender, RoutedEventArgs e) => Window.GetWindow(this).Close();
    private void OpenScanner(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is MainWindow main) main.OpenScanner(); else if (Window.GetWindow(this) is ScannerWindow scanner) scanner.OpenOriginalScanner(); }
}
