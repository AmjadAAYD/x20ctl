using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace X20Ctl.Product;

/// <summary>Workbench accessors for custom-themed Device pages (Nexus, Dusk, Hollow, Nocturne, Sunrise).</summary>
public sealed partial class DeviceWorkbench
{
    private FrameworkElement? batteryTile;
    private FrameworkElement? x10BatteryRow;
    private bool batteryVisible = true;
    internal void ShowBattery(bool visible)
    {
        batteryVisible = visible;
        if (batteryTile != null) batteryTile.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (x10BatteryRow != null) x10BatteryRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    internal string ConnectionText => connection.Text;
    internal string BatteryText => battery.Text;
    internal string FirmwareText => firmware.Text;
    internal string RevisionText => hardwareRevision.Text;
    internal string TransportText => transport.Text;
    internal bool CanRefreshBattery => batteryTile != null;
    internal void RefreshBattery() => batteryTile?.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
    internal void OpenDiagnostics() => OpenTester.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal void Watch(Action changed)
    {
        var text = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        foreach (var t in new[] { connection, battery, firmware, hardwareRevision, transport }) text.AddValueChanged(t, (_, _) => changed());
    }
}
