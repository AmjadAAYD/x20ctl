using System.Windows;
using System.Windows.Controls;
namespace X20Ctl.Desktop.Controls;
public partial class TriggerMeter : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(byte), typeof(TriggerMeter), new PropertyMetadata((byte)0, Changed));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TriggerMeter), new PropertyMetadata("LT"));
    public byte Value { get => (byte)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ValueText => $"{Value} / 255";
    public string PercentText => $"{Value / 255d:P0} travel";
    public TriggerMeter() => InitializeComponent();
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((TriggerMeter)d).Refresh();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        if (Fill == null) return;
        Fill.Width = Math.Max(0, ActualWidth * Value / 255d);
        // ValueText/PercentText bindings read the dependency property when the frame changes.
        foreach (var text in FindText(this)) text.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
    }
    private static IEnumerable<TextBlock> FindText(DependencyObject node)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
            if (child is TextBlock text) yield return text;
            foreach (var descendant in FindText(child)) yield return descendant;
        }
    }
}
