using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product.Scanner;

public sealed partial class PhoneGuide
{
    private void BuildHybridGuide(StackPanel body, PhoneScreen screen)
    {
        var left = new Border { Name = "HybridPhoneGuide", CornerRadius = new(22), Background = HybridToolFrame.Color("#29242f"), BorderBrush = HybridToolFrame.Color("#52dcff"), BorderThickness = new(1), Padding = new(24), Margin = new(0, 0, 6, 0) };
        var content = new StackPanel(); content.Children.Add(HybridToolFrame.Text($"{title} · Step {index + 1} of {steps.Length}", 14, "#d5bff6", FontWeights.Bold)); content.Children.Add(body); left.Child = content;
        Children.Add(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var right = new Border { CornerRadius = new(22), Background = HybridToolFrame.Color("#302b36"), Padding = new(24) };
        var stack = new DockPanel(); var heading = HybridToolFrame.Text("Where to tap", 23, "#f5f1fa", FontWeights.Bold); DockPanel.SetDock(heading, Dock.Top); stack.Children.Add(heading);
        stack.Children.Add(new Viewbox { MaxHeight = 460, Stretch = Stretch.Uniform, Child = Phone.Draw(screen), Margin = new(0, 12, 0, 0) }); right.Child = stack; SetColumn(right, 2); Children.Add(right);
    }
}
