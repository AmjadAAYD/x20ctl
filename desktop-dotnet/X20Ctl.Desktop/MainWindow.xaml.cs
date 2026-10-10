using System.Windows;
using X20Ctl.Desktop.ViewModels;

namespace X20Ctl.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : ConsoleWindow
{
    private ScannerWindow? scanner;
    public WorkspaceModel Model { get; } = new();
    public MainWindow()
    {
        InitializeComponent();
        InputView.DataContext = Model; Workspace = InputView;
        Closed += (_, _) => scanner?.Close();
    }
    public void OpenScanner()
    {
        if (scanner == null) { scanner = new() { Owner = this }; scanner.Closed += (_, _) => scanner = null; scanner.Show(); }
        else scanner.Activate();
    }
}
