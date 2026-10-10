using System.Windows;
namespace X20Ctl.Product;

/// <summary>Notification-area icon: closing the window hides X20CTL here so it keeps running;
/// double-click or "Open X20CTL" brings it back fullscreen, "Quit X20CTL" exits for real.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Window window;
    private readonly System.Windows.Forms.NotifyIcon icon;
    private bool announced;
    public bool Quitting { get; private set; }

    public TrayIcon(Window window)
    {
        this.window = window;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open X20CTL", null, (_, _) => Restore());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit X20CTL", null, (_, _) => Quit());
        icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "X20CTL · Controller Studio",
            Icon = Environment.ProcessPath is { } exe ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu, Visible = true
        };
        icon.DoubleClick += (_, _) => Restore();
    }

    public void HideToTray()
    {
        window.Hide();
        if (!announced) { announced = true; icon.ShowBalloonTip(3500, "X20CTL is still running", "It is in the notification area. Right-click the icon to quit.", System.Windows.Forms.ToolTipIcon.None); }
    }
    public void Restore()
    {
        window.Show(); window.WindowState = WindowState.Maximized; // always back to fullscreen
        window.Activate(); window.Focus();
    }
    public void Quit() { Quitting = true; icon.Visible = false; window.Close(); Application.Current.Shutdown(); }
    public void Dispose() { icon.Visible = false; icon.Dispose(); }
}
