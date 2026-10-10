using System.IO;
using System.Windows;

namespace X20Ctl.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            if (e.Args.Length == 2 && e.Args[0] == "--review-scanner") { ShutdownMode = ShutdownMode.OnExplicitShutdown; await ScannerVisualReview.Run(e.Args[1]); Shutdown(0); return; }
            if (e.Args.Length == 2 && e.Args[0] == "--review") { ShutdownMode = ShutdownMode.OnExplicitShutdown; await Review.Run(e.Args[1]); Shutdown(0); return; }
            if (e.Args.Length != 0 && !e.Args.SequenceEqual(new[] { "--scanner" })) throw new ArgumentException("Use --scanner, --review DIRECTORY, or launch without arguments.");
            Window window = e.Args.Contains("--scanner") ? new ScannerWindow() : new MainWindow(); MainWindow = window; window.Show();
        }
        catch (Exception error)
        {
            if (e.Args.Length == 2 && e.Args[0].StartsWith("--review")) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), error.ToString()); }
            else MessageBox.Show(error.Message, "X20CTL native preview", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}

