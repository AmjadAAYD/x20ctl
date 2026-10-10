using System.IO;
using System.Windows;
namespace X20Ctl.Product;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            if (e.Args.Length == 3 && e.Args[0] == "--review-scanner-replay")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await Development.ScannerReplay.Run(e.Args[1], e.Args[2]); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] is "--review-zone" or "--review-studio" or "--review-console" or "--review-management" or "--review-product" or "--review-models" or "--review-live" or "--review-design" or "--review-showcase" or "--review-dashboard" or "--review-input" or "--review-intro" or "--review-zone-motion" or "--review-windowed" or "--review-pages" or "--review-stress" or "--review-probe" or "--review-readme")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown; IntroView.SoundEnabled = false;
                if(e.Args[0]=="--review-readme")await Development.ReadmeReview.Run(e.Args[1]);
            else if(e.Args[0]=="--review-models")await Development.ModelAudit.Run(e.Args[1]);
                else if(e.Args[0]=="--review-live")await Development.LiveReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-design")await Development.DesignReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-showcase")await Development.ShowcaseReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-dashboard")await Development.DashboardReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-input")await Development.InputReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-intro")await Development.IntroReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-zone-motion")await Development.ZoneMotionReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-windowed")await Development.WindowedReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-pages")await Development.PagesReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-stress")await Development.StressReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-probe")await Development.StressProbe.Run(e.Args[1]);
                else if(e.Args[0]=="--review-product")await Development.ProductReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-management")await Development.ManagementReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-console")await Development.ConsoleReview.Run(e.Args[1]);
                else if (e.Args[0] == "--review-studio") await Development.StudioReview.Run(e.Args[1]);
                else await Development.ZoneReview.Run(e.Args[1]);
                Shutdown(0); return;
            }
            if (e.Args.Length > 0 && !e.Args.SequenceEqual(new[] { "--reduced-motion" }))
                throw new ArgumentException("Use --reduced-motion, --review-zone DIRECTORY, or --review-studio DIRECTORY.");
            // a slip in the UI is logged and survived, never a closed app: the log names what happened for the next fix
            DispatcherUnhandledException += (_, failure) =>
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "native"); Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "crash.log"), $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{failure.Exception}\n\n");
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
                failure.Handled = true;
            };
            var window = new ProductWindow(e.Args.Contains("--reduced-motion"),firstRun:true,nativeEngine:true,fullscreen:true,trayIcon:true);
            MainWindow = window; window.Show();
        }
        catch (Exception error)
        {
            if (e.Args.Length == 2 && e.Args[0] is "--review-zone" or "--review-studio" or "--review-console" or "--review-management" or "--review-product" or "--review-models" or "--review-live" or "--review-design" or "--review-showcase" or "--review-dashboard" or "--review-input" or "--review-intro" or "--review-zone-motion" or "--review-windowed" or "--review-pages" or "--review-stress" or "--review-probe" or "--review-readme")
            {
                Directory.CreateDirectory(e.Args[1]);
                File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), error.ToString());
            }
            else MessageBox.Show(error.Message, "X20CTL development preview");
            Shutdown(1);
        }
    }
}


