using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using X20Ctl.Desktop.ViewModels;
using X20Ctl.Simulation;
namespace X20Ctl.Desktop.Views;
public partial class ScannerInputsPage : UserControl, IInputWorkspace
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock = new();
    private double time, previous;
    public WorkspaceModel Model => (WorkspaceModel)DataContext;
    public ScannerInputsPage() { InitializeComponent(); timer.Tick += Tick; SizeChanged += (_, _) => { if (DataContext is WorkspaceModel model) model.IsCompact = ActualHeight < 650; }; }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Model.IsCompact = ActualHeight < 650; Model.PropertyChanged += Changed; clock.Restart(); previous = 0; timer.Start();
        if (!Model.ReducedMotion)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            var move = new TranslateTransform(); RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220)));
        }
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) { timer.Stop(); clock.Stop(); Model.PropertyChanged -= Changed; }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(Model.Scenario) or nameof(Model.Slot)) time = 0; }
    private void Tick(object? sender, EventArgs e) { double now = clock.Elapsed.TotalMilliseconds, delta = now - previous; previous = now; if (Model.Paused) return; time += delta; Model.Update(time); }
    private void SegmentChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceModel model || sender is not RadioButton tab) return;
        model.SelectedSegment = (string)tab.Tag;
        InspectorContent?.BeginAnimation(OpacityProperty, model.ReducedMotion ? null : new DoubleAnimation(.6, 1, TimeSpan.FromMilliseconds(150)));
    }
    private void ScenarioChanged(object sender, SelectionChangedEventArgs e) { if (DataContext is WorkspaceModel model && ScenarioChoice.SelectedIndex >= 0) model.Scenario = (Scenario)ScenarioChoice.SelectedIndex; }
    public void CycleInspector(int direction) { var tabs = InspectorTabs.Children.OfType<RadioButton>().ToArray(); int i = Array.FindIndex(tabs, t => t.IsChecked == true); var next = tabs[(i + direction + tabs.Length) % tabs.Length]; next.IsChecked = true; next.Focus(); }
    public void ToggleDeveloperDrawer() => DeveloperDrawer.Visibility = DeveloperDrawer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
}
