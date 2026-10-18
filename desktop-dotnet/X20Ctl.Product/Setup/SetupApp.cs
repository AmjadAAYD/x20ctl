using System.IO;
using System.Windows;
namespace X20Ctl.Product.Setup;

/// <summary>Starts the setup, the uninstaller, or their silent forms:
///   X20CTL-…-setup.exe [--silent] [--desktop] [--no-updates] [--no-start-menu] [--drivers]     install (silent exits 0, or 1 on failure)
///   X20Ctl.exe --uninstall [--silent] [--purge]                                     remove, keeping the player's settings unless --purge</summary>
internal static class SetupApp
{
    public static void Run(Application app, SetupMode mode, string[] args)
    {
        bool silent = args.Contains("--silent");
        try
        {
            switch (mode)
            {
                case SetupMode.Install when silent:
                {
                    var options = new SetupOptions(!args.Contains("--no-start-menu"), args.Contains("--desktop"), !args.Contains("--no-updates"), false, args.Contains("--drivers"));
                    Installer.Install(options, new Progress<(double, string)>(_ => { })); app.Shutdown(0); return;
                }
                case SetupMode.Install: Show(app, new SetupWindow(mode)); return;
                case SetupMode.Uninstall:
                    app.Shutdown(Installer.HandOffUninstall(silent, args.Contains("--purge")) ? 0 : 1); return;
                case SetupMode.UninstallRun:
                {   // --uninstall-run DIR PID --silent|--ask --purge|--keep, from the copy in the temp folder
                    int pid = int.TryParse(args[2], out int parsed) ? parsed : 0; bool purge = args.Contains("--purge");
                    if (args.Contains("--silent"))
                    {
                        try { Installer.Uninstall(purge, pid, new Progress<(double, string)>(_ => { })); app.Shutdown(0); }
                        finally { Installer.RemoveSelfLater(); }
                        return;
                    }
                    Show(app, new SetupWindow(mode) { ParentPid = pid, Closing2 = Installer.RemoveSelfLater }); return;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            if (silent) app.Shutdown(1); else { MessageBox.Show(e.Message, "X20CTL Setup"); app.Shutdown(1); }
        }
    }

    static void Show(Application app, SetupWindow window)
    {
        app.ShutdownMode = ShutdownMode.OnMainWindowClose; app.MainWindow = window; window.Show();
    }
}
