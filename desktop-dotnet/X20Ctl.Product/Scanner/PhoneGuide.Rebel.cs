using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product.Scanner;

public sealed partial class PhoneGuide
{
    private void BuildRebelGuide(StackPanel body, Button next, Button back, Button help, PhoneScreen screen)
    {
        RebelCommands.Dress(next, true); RebelCommands.Dress(back); RebelCommands.Dress(help);
        if (above is DependencyObject picker) StyleRebelPicker(picker);
        var left = new Grid { Name = "RebelPhoneGuide", Margin = new(0, 0, 6, 0) };
        var slab = new Slab { Seed = 811, Fill = Ink.B(Ink.Panel), Line = Ink.B(Ink.A(Ink.White, .25)), LineWidth = 1.5 }; left.Children.Add(slab);
        var content = new StackPanel { Margin = new(24, 20, 24, 20) }; content.Children.Add(Ink.Label($"{title} · Step {index + 1} of {steps.Length}", 12));
        content.Children.Add(body); slab.Children.Add(content);
        Children.Add(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var right = new Grid { Margin = new(0, 2, 0, 2) }; right.Children.Add(new TornPaper { Seed = 812 });
        var stack = new DockPanel { Margin = new(24) }; var heading = Ink.Text("Where to tap", 24, Ink.B(Ink.Black)); DockPanel.SetDock(heading, Dock.Top); stack.Children.Add(heading);
        stack.Children.Add(new Viewbox { MaxHeight = 460, Stretch = Stretch.Uniform, Child = Phone.Draw(screen), Margin = new(0, 12, 0, 0) }); right.Children.Add(stack); SetColumn(right, 2); Children.Add(right);
    }
    private static void StyleRebelPicker(DependencyObject item)
    {
        if (item is RadioButton radio) radio.Style = RebelChoiceStyle;
        if (item is Border border) { border.CornerRadius = new(0); border.Background = Brushes.Transparent; border.BorderThickness = new(0); }
        foreach (object child in LogicalTreeHelper.GetChildren(item)) if (child is DependencyObject nested) StyleRebelPicker(nested);
    }
    private static readonly Style RebelChoiceStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
      <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="RadioButton"><Setter Property="Height" Value="34"/><Setter Property="Margin" Value="0,0,8,0"/><Setter Property="Foreground" Value="#f7f3ee"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><Border x:Name="Face" Background="#161417" BorderBrush="#8f878a" BorderThickness="1" Padding="12,4" RenderTransformOrigin=".5,.5"><Border.RenderTransform><SkewTransform AngleX="-10"/></Border.RenderTransform><ContentPresenter HorizontalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Face" Property="Background" Value="#e3122d"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Face" Property="BorderBrush" Value="#f7f3ee"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
      """);
}
