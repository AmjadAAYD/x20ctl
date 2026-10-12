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

    string status = "X20CTL · Controller Studio", battery = "";
    /// <summary>The icon's tooltip: shows when PC mode is applying settings. Windows allows 63 characters.</summary>
    public void SetStatus(string text) { status = text; Compose(); }
    /// <summary>The controller's battery, as Windows reports it ("Battery low"), or empty when it isn't known.</summary>
    public void SetBattery(string text) { battery = text; Compose(); }
    void Compose() { string text = battery.Length > 0 ? status + " · " + battery : status; icon.Text = text.Length > 63 ? text[..63] : text; }
    public void Notify(string title, string text) => icon.ShowBalloonTip(3000, title, text, System.Windows.Forms.ToolTipIcon.None);
    /// <summary>A checkable menu line (like "Controller as mouse"), kept in step with its state whenever the menu opens.</summary>
    public void AddToggle(string text, Func<bool> get, Action<bool> set)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text) { CheckOnClick = true };
        item.Click += (_, _) => set(item.Checked);
        icon.ContextMenuStrip!.Opening += (_, _) => item.Checked = get();
        icon.ContextMenuStrip.Items.Insert(1, item);
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
