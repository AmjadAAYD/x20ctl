using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X20Ctl.Simulation;
using X20Ctl.Desktop.ViewModels;
namespace X20Ctl.Desktop.Views;
public partial class InputWorkspace : UserControl, IInputWorkspace
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch wallClock = new();
    private double timeMs, lastWallMs;
    public WorkspaceModel Model => (WorkspaceModel)DataContext;
    public InputWorkspace() { InitializeComponent(); timer.Tick += Tick; SizeChanged += (_, _) => { if (DataContext is WorkspaceModel model) model.IsCompact = ActualHeight < 620; }; }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceModel) return;
        Model.IsCompact = ActualHeight < 620; Model.PropertyChanged += Changed; wallClock.Restart(); lastWallMs = 0; timer.Start();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) { timer.Stop(); wallClock.Stop(); if (DataContext is WorkspaceModel model) model.PropertyChanged -= Changed; }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(WorkspaceModel.Scenario) or nameof(WorkspaceModel.Slot)) timeMs = 0; }
    private void Tick(object? sender, EventArgs e)
    {
        double now = wallClock.Elapsed.TotalMilliseconds, delta = now - lastWallMs; lastWallMs = now;
        if (Model.Paused) return;
        timeMs += delta; Model.Update(timeMs);
    }
    private void ScenarioChanged(object sender, SelectionChangedEventArgs e) { if (DataContext is WorkspaceModel model && ScenarioChoice.SelectedIndex >= 0) model.Scenario = (Scenario)ScenarioChoice.SelectedIndex; }
    private void SegmentChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceModel model || sender is not RadioButton tab) return;
        model.SelectedSegment = (string)tab.Tag;
        if (InspectorContent != null) InspectorContent.BeginAnimation(OpacityProperty, model.ReducedMotion ? null : new System.Windows.Media.Animation.DoubleAnimation(.65, 1, TimeSpan.FromMilliseconds(150)));
    }
    public void CycleInspector(int direction)
    {
        var tabs = InspectorTabs.Children.OfType<RadioButton>().ToArray(); int index = Array.FindIndex(tabs, t => t.IsChecked == true);
        var target = tabs[(index + direction + tabs.Length) % tabs.Length]; target.IsChecked = true; target.Focus();
    }
    private void OpenOriginalScanner(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "pyproject.toml"))) root = root.Parent;
            if (root == null) throw new IOException("Open the preserved Scanner 2.0.0 package supplied with this checkout.");
            string path = Path.Combine(root.FullName, "artifacts", "input-diagnostic-20261008", "X20CTL-Input-Scan.exe");
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() != "b41713918187eb85a71524d49bea80b824e9b9f24631f8a25d3591f51363ba97") throw new IOException("Scanner integrity check failed. Use the preserved original package.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { MessageBox.Show(Window.GetWindow(this), error.Message, "Original Scanner 2.0.0", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
    public void LaunchOriginalScanner() => OpenOriginalScanner(this, new RoutedEventArgs());
}
