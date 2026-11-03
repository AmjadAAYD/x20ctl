using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product.Rebel;

internal static class RebelCommands
{
    /// <summary>Changes a native command's face while retaining its content, events and enabled state.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, object> dressed = new();
    private static readonly ControlTemplate Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Grid><Border x:Name="Under" Background="#e3122d" Margin="5,5,-5,-5" Opacity="0"/><Border x:Name="Face" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1.5" Padding="20,0"><Border.RenderTransform><SkewTransform AngleX="-8"/></Border.RenderTransform><ContentPresenter VerticalAlignment="Center" HorizontalAlignment="Center"/></Border></Grid>
          <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Face" Property="Background" Value="#f7f3ee"/><Setter TargetName="Under" Property="Opacity" Value="1"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Face" Property="Background" Value="#f7f3ee"/><Setter TargetName="Under" Property="Opacity" Value="1"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    internal static void Dress(Button button, bool primary = false)
    {
        if (dressed.TryGetValue(button, out _)) return;
        dressed.Add(button, new object());
        button.Style = null; button.Padding = new(0); button.Height = 46; button.MinHeight = 0; button.FocusVisualStyle = null;
        button.Background = Ink.B(primary ? Ink.Red : Ink.Panel); button.BorderBrush = Ink.B(primary ? Ink.Red : Ink.Grey); button.Template = Template;
        void Paint(bool focus) { if (button.Content is DependencyObject content) Texts(content, focus ? Ink.Black : Ink.White); }
        button.GotKeyboardFocus += (_, _) => Paint(true); button.LostKeyboardFocus += (_, _) => Paint(button.IsMouseOver); button.MouseEnter += (_, _) => Paint(true); button.MouseLeave += (_, _) => Paint(button.IsKeyboardFocused); Paint(false);
    }
    private static void Texts(DependencyObject root, Color colour)
    {
        if (root is TextBlock text) { text.Foreground = Ink.B(colour); if (!text.FontFamily.Source.Contains("Segoe MDL2") && !text.FontFamily.Source.Contains("Segoe Fluent")) { text.FontFamily = Ink.Heavy; text.FontSize = 17; text.RenderTransform = Ink.Slant; } }
        foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject nested) Texts(nested, colour);
    }
}
