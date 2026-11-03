using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product.Rebel;

/// <summary>Rebel's compact chrome. The controls keep their own content, events and accessibility names.</summary>
internal static class RebelChrome
{
    private static readonly ControlTemplate ButtonTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" Padding="8,1">
            <Border.RenderTransform><SkewTransform AngleX="-10"/></Border.RenderTransform>
            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="BorderBrush" Value="#E3122D"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter Property="BorderBrush" Value="#E3122D"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    internal static Viewbox Fit(Panel controls, double width)
    {
        controls.LayoutTransform = Transform.Identity; controls.Margin = new(0);
        controls.HorizontalAlignment = HorizontalAlignment.Left; controls.VerticalAlignment = VerticalAlignment.Top;
        Style(controls);
        return new Viewbox { Child = controls, Width = width, Height = 26, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 0, 12, 0) };
    }

    internal static void Style(DependencyObject node)
    {
        if (node is Button button)
        {
            button.Height = 28; button.MinHeight = 0; button.FontSize = 13;
            button.Background = Ink.B(Ink.Black); button.Foreground = Ink.B(Ink.White); button.BorderBrush = Ink.B(Ink.A(Ink.White, .4)); button.BorderThickness = new(1);
            button.FocusVisualStyle = null;
            if (button.Content == null) button.Content = System.Windows.Automation.AutomationProperties.GetName(button);
            button.Template = ButtonTemplate;
        }
        if (node is Border border)
        {
            border.CornerRadius = new(0); border.Background = Ink.B(Ink.Black); border.BorderBrush = Ink.B(Ink.White); border.Effect = null;
            border.RenderTransformOrigin = new(.5, .5); border.RenderTransform = new SkewTransform(-10, 0);
        }
        foreach (object child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject nested) Style(nested);
    }
}
