using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildRebelFeedback(Button back, Panel kinds)
    {
        CornerRadius = new(0); BorderThickness = new(0); Padding = new(0); Background = Brushes.Transparent;
        MaxWidth = double.PositiveInfinity; Width = 1600; Height = 900; Margin = new(0);
        var board = new Canvas { Name = "RebelFeedback", Width = 1600, Height = 900 }; Child = board;
        void At(FrameworkElement element, double x, double y)
        {
            if (element.Parent is Panel panel) panel.Children.Remove(element);
            else if (element.Parent is Decorator decorator) decorator.Child = null;
            Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element);
        }
        int i = 0;
        foreach (var radio in kinds.Children.OfType<RadioButton>().ToArray())
        {
            radio.Style = RebelCategoryStyle(); radio.Width = 590; radio.Height = 52; radio.Margin = new(0); radio.MinWidth = 0;
            var caption = Ink.Text(radio.Content?.ToString() ?? "", 19);
            caption.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = radio }); radio.Content = caption;
            At(radio, 268 + i * 4, 170 + i * 58); i++;
        }
        At(Ink.Label("CONTROLLER (OPTIONAL)"), 268, 418); controller.Width = 580; controller.Height = 48; controller.FontFamily = Ink.Body; At(controller, 268, 446);
        At(Ink.Label("CONNECTED BY (OPTIONAL)"), 268, 518); connection.Width = 580; connection.Height = 48; connection.FontFamily = Ink.Body; At(connection, 268, 546);
        var disclosure = Ink.Text("What's sent: your message, category, controller, connection, X20CTL version and Windows version.", 14, Ink.B(Ink.Dim), false);
        disclosure.Width = 580; disclosure.TextWrapping = TextWrapping.Wrap; At(disclosure, 268, 640);
        var note = new Grid { Width = 600, Height = 440, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(2) };
        note.Children.Add(new TornPaper { Seed = 18 }); At(note, 940, 170);
        note.Children.Add(Ink.Label("Your message", 13, Ink.B(Ink.Cut)).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Left; t.VerticalAlignment = VerticalAlignment.Top; t.Margin = new(36, 34, 0, 0); }));
        if (Message.Parent is Panel original) original.Children.Remove(Message);
        Message.MinHeight = 0; Message.MaxHeight = double.PositiveInfinity; Message.Height = 320; Message.Width = 528; Message.FontFamily = Ink.Body; Message.FontSize = 26;
        Message.Foreground = Message.CaretBrush = Ink.B(Ink.Black); Message.Background = Brushes.Transparent; Message.BorderThickness = new(0); Message.Padding = new(0);
        Message.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"TextBox\"><ScrollViewer Name=\"PART_ContentHost\"/></ControlTemplate>");
        Message.Margin = new(36, 72, 36, 48); Message.VerticalAlignment = VerticalAlignment.Top; note.Children.Add(Message);
        if (counter.Parent is Panel previous) previous.Children.Remove(counter);
        counter.Foreground = Ink.B(Ink.Dim); counter.FontFamily = Ink.Body; counter.Margin = new(0, 0, 26, 18); counter.VerticalAlignment = VerticalAlignment.Bottom; note.Children.Add(counter);
        var words = new WrapPanel { Margin = new(36, 72, 36, 48), Height = 320, ClipToBounds = true, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        note.Children.Add(words);
        void RefreshWords()
        {
            words.Children.Clear(); bool editing = Message.IsKeyboardFocusWithin || Message.Text.Length == 0;
            words.Visibility = editing ? Visibility.Collapsed : Visibility.Visible; Message.Opacity = editing ? 1 : 0;
            int wordIndex = 0;
            foreach (string word in Message.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                bool red = wordIndex % 5 == 2, black = wordIndex % 3 == 1;
                var label = Ink.Text(word, 28, Ink.B(red || black ? Ink.White : Ink.Black)); label.MaxWidth = 490; label.TextWrapping = TextWrapping.Wrap;
                words.Children.Add(new Border { Child = label, Background = Ink.B(red ? Ink.Red : black ? Ink.Black : Ink.White), Padding = new(6, 2, 6, 2), Margin = new(3, 5, 3, 5), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(new double[] { -2, 1, -1, 3, -3 }[wordIndex % 5]) });
                wordIndex++;
            }
        }
        Message.TextChanged += (_, _) => RefreshWords(); Message.GotKeyboardFocus += (_, _) => RefreshWords(); Message.LostKeyboardFocus += (_, _) => RefreshWords(); RefreshWords();
        void Present(Button button, string caption, bool primary)
        {
            var template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; button.Template = template;
            var slab = new Slab { Width = 244, Height = 48, Fill = Ink.B(primary ? Ink.Red : Ink.Black), Line = Ink.B(Ink.White), LineWidth = 2, Rot = -2 };
            var label = Ink.Text(caption, 19); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center; slab.Children.Add(label);
            button.Content = slab; button.Width = 244; button.Height = 48; button.Margin = new(0); button.Padding = new(0); button.MinHeight = 0;
        }
        Present(back, "Back to Tools", false); Present(SendButton, "Send to X20CTL", true);
        SendButton.Opacity = SendButton.IsEnabled ? 1 : .45; SendButton.IsEnabledChanged += (_, _) => SendButton.Opacity = SendButton.IsEnabled ? 1 : .45;
        At(SendButton, 950, 650); At(back, 1218, 650); status.Width = 560; status.FontFamily = Ink.Body; status.FontSize = 16; At(status, 950, 722);
    }

    private static Style RebelCategoryStyle() => (Style)System.Windows.Markup.XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="RadioButton">
          <Setter Property="Background" Value="#161417"/><Setter Property="Foreground" Value="#F7F3EE"/><Setter Property="FontFamily" Value="Franklin Gothic"/><Setter Property="FontWeight" Value="Black"/><Setter Property="FontStyle" Value="Italic"/><Setter Property="FontSize" Value="19"/>
          <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><Border Background="{TemplateBinding Background}" BorderBrush="#E3122D" BorderThickness="0"><Border.RenderTransform><SkewTransform AngleX="-8"/></Border.RenderTransform><ContentPresenter Margin="22,0" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter Property="Background" Value="#F7F3EE"/><Setter Property="Foreground" Value="#0B0A0C"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="Background" Value="#E3122D"/><Setter Property="Foreground" Value="White"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        """);
}
