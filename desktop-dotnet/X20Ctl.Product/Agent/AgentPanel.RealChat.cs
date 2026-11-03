using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace X20Ctl.Product.Agent;

/// <summary>Themed agent pages host the real chat, ideas, check-up, settings and input, never a drawn copy of them.</summary>
public partial class AgentPanel
{
    static ControlTemplate BareButtonTemplate() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }

    Button ChipButton(UIElement visual, Action click)
    {
        var button = new Button { Content = visual, FocusVisualStyle = null, Cursor = Cursors.Hand, Template = BareButtonTemplate() };
        button.Click += (_, _) => click();
        register(button);
        return button;
    }

    static void Detach(FrameworkElement element)
    {
        if (element.Parent is Panel panel) panel.Children.Remove(element);
        else if (element.Parent is Decorator decorator) decorator.Child = null;
        else if (element.Parent is ContentControl content) content.Content = null;
    }

    void MountRealViews(Canvas host, double x, double y, double width, double height, Brush settingsBackground, Brush settingsBorder)
    {
        foreach (var element in new FrameworkElement[] { scroller, ideasView, checkupView, settingsBox }) Detach(element);
        transcript.LayoutTransform = Transform.Identity; transcript.Margin = new(0, 10, 0, 10);
        var views = new Grid { Width = width, Height = height, Children = { scroller, ideasView, checkupView } };
        Canvas.SetLeft(views, x); Canvas.SetTop(views, y); host.Children.Add(views);
        settingsBox.Width = width; settingsBox.Height = height; settingsBox.CornerRadius = new(6);
        settingsBox.Background = settingsBackground; settingsBox.BorderBrush = settingsBorder; settingsBox.BorderThickness = new(1);
        Canvas.SetLeft(settingsBox, x); Canvas.SetTop(settingsBox, y); Panel.SetZIndex(settingsBox, 25); host.Children.Add(settingsBox);
    }

    void StyleRealInput(FontFamily font, double size, Brush ink)
    {
        Detach(Input);
        Input.FontFamily = font; Input.FontSize = size; Input.MinHeight = 40; Input.MaxHeight = 40; Input.Foreground = ink; Input.CaretBrush = ink;
        Input.Background = Brushes.Transparent; Input.BorderThickness = new(0); Input.VerticalContentAlignment = VerticalAlignment.Center; Input.Margin = new(0);
    }

    void ClickSend() => sendRound.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
}
