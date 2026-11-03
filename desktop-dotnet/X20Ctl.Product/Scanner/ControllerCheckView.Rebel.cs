using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    internal Action<string>? RebelNavigate { get; set; }
    private void BuildRebelReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var board = RebelToolFrame.Create("RebelReadScan", "Before you scan", "This is a full controller scan; you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports.", close, key => RebelNavigate?.Invoke(key));
        warning.Background = Ink.B(Ink.Black); warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        systemRow.Margin = new(0);
        if (systemRow is StackPanel systemStack) { systemStack.Orientation = Orientation.Horizontal; if (systemStack.Children.OfType<TextBlock>().FirstOrDefault() is { } caption) { caption.Foreground = Ink.B(Ink.Red); caption.FontFamily = Ink.Body; caption.FontSize = 12; caption.VerticalAlignment = VerticalAlignment.Center; caption.Margin = new(0, 0, 16, 0); } }
        if (systemRow is Panel systemsPanel) foreach (var edge in systemsPanel.Children.OfType<Border>()) { edge.CornerRadius = new(0); edge.Background = Brushes.Transparent; edge.BorderThickness = new(0); if (edge.Child is Panel choicesPanel) foreach (var radio in choicesPanel.Children.OfType<RadioButton>()) radio.Style = RebelSystemChoiceStyle; }
        RebelToolFrame.At(board, systemRow, 268, 190);
        string[] steps = ["Start the check", "Find your slot", "Confirm it's yours", "Test every control", "Save the report"];
        var wire = new Polyline { Stroke = Ink.B(Ink.Red), StrokeThickness = 2, IsHitTestVisible = false };
        for (int i = 0; i < steps.Length; i++) wire.Points.Add(new(380 + i * 266, 262 + i % 2 * 12)); board.Children.Add(wire);
        for (int i = 0; i < steps.Length; i++)
        {
            double x = 380 + i * 266, y = 262 + i % 2 * 12;
            var slab = new Slab { Width = 208, Height = 64, Seed = 220 + i, Rot = new double[] { -3, 2, -2, 3, -1 }[i], Fill = Ink.B(i == 0 ? Ink.Red : Ink.Paper) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(Ink.Text((i + 1).ToString(), 26, Ink.B(i == 0 ? Ink.White : Ink.Red)));
            var label = Ink.Text(steps[i], 15, Ink.B(i == 0 ? Ink.White : Ink.Black)); label.Width = 140; label.TextWrapping = TextWrapping.Wrap; label.Margin = new(12, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label); slab.Children.Add(row);
            RebelToolFrame.At(board, slab, x - 104, y + 8); RebelToolFrame.At(board, new Ellipse { Width = 16, Height = 16, Fill = Ink.B(Ink.Red), Stroke = Ink.B(Ink.White), StrokeThickness = 1 }, x - 8, y - 8);
        }
        var descriptions = cards.Children.OfType<Border>().ToArray();
        var paper = new Grid { Width = 530, Height = 350, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-1.5) }; RebelToolFrame.At(board, paper, 300, 392);
        var list = new StackPanel();
        for (int i = 1; i < descriptions.Length; i++)
        {
            int index = i; var row = new SlabButton($"{i + 1}   {ScanFlow.Expectations[i].Title}", 650, 44, 300 + i, -1, 18, Ink.White) { Margin = new(i * 4, 0, 0, 6) };
            row.Click += (_, _) => Show(index); row.GotKeyboardFocus += (_, _) => Show(index); list.Children.Add(row);
        }
        RebelToolFrame.At(board, new ScrollViewer { Content = list, Width = 670, Height = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 870, 390);
        choices.Margin = new(0); choices.Width = 1240; if (choices is Panel actions) { int actionIndex = 0; foreach (var button in actions.Children.OfType<Button>()) { button.Margin = new(0, 0, 12, 0); RebelCommands.Dress(button, actionIndex++ == 0); } } RebelToolFrame.At(board, choices, 300, 756);
        linux.Width = 1200; linux.Height = 430; linux.Margin = new(0); RebelToolFrame.At(board, linux, 300, 354);
        var report = Ink.Text("Your report: the full ZIP is always saved to Documents\\X20CTLInputReports. \"Scan and send\" also sends a compact summary (controller names, IDs and results, no personal files) to X20CTLADMIN, the X20CTL team's report site, when the scan ends. You can send the ZIP to @" + ScanFlow.Discord + " on Discord too.", 12.5, Ink.B(Ink.Dim), false);
        report.Width = 1180; report.TextWrapping = TextWrapping.Wrap; RebelToolFrame.At(board, report, 300, 812);
        Show(0);
        void Show(int index)
        {
            paper.Children.Clear(); paper.Children.Add(new TornPaper { Seed = 19, UnderSheet = Ink.B(Ink.Red) });
            var description = descriptions[index]; RebelToolFrame.Detach(description); description.Background = Brushes.Transparent; description.BorderThickness = new(0); description.CornerRadius = new(0); description.Margin = new(0); description.Padding = new(30, 32, 30, 24);
            RebelToolFrame.InkText(description, Ink.Black);
            if (description.Child is StackPanel body)
            {
                var texts = body.Children.OfType<TextBlock>().Where(t => t.Tag as string != "rebel-card-index").ToArray();
                if (texts.FirstOrDefault() is { } heading) { heading.FontFamily = Ink.Heavy; heading.FontSize = 28; heading.Margin = new(0, 6, 0, 0); }
                if (texts.Length > 1) { texts[1].FontSize = 17; texts[1].Margin = new(0, 12, 0, 0); }
                if (!body.Children.OfType<TextBlock>().Any(t => t.Tag as string == "rebel-card-index")) { var number = Ink.Label((index + 1).ToString(), 12, Ink.B(Ink.Red)); number.Tag = "rebel-card-index"; body.Children.Insert(0, number); }
            }
            paper.Children.Add(description);
        }
    }
    private void BuildRebelReadFlow(Grid originalPage)
    {
        RebelToolFrame.Detach(originalPage); originalPage.Margin = new(0); originalPage.Width = 1280; originalPage.Height = 650; originalPage.ClipToBounds = true;
        if (originalPage.RowDefinitions.Count > 0) originalPage.RowDefinitions[0].Height = new GridLength(0);
        var board = RebelToolFrame.Create("RebelReadFlow", "Controller Check", "Read-only input · configuration and hardware writes remain separate.", close, key => RebelNavigate?.Invoke(key), 52);
        StyleRebelReadControls(originalPage); RebelToolFrame.At(board, originalPage, 268, 160);
        var view = new Viewbox { Child = board, Stretch = Stretch.Uniform }; Children.Add(view); Panel.SetZIndex(view, 10); Panel.SetZIndex(warning, 20); Panel.SetZIndex(overlay, 30); Panel.SetZIndex(resultsLayer, 40);
    }
    private static void StyleRebelReadControls(DependencyObject item)
    {
        if (item is SlotCard) return; // Its live input and selection paint are owned by the native widget.
        if (item is Button button) RebelCommands.Dress(button);
        if (item is RadioButton radio) radio.Style = RebelSystemChoiceStyle;
        if (item is Border border) { border.CornerRadius = new(0); if (border.Background != null) border.Background = Ink.B(Ink.Panel); }
        foreach (object child in LogicalTreeHelper.GetChildren(item)) if (child is DependencyObject nested) StyleRebelReadControls(nested);
    }
    private FrameworkElement RebelReadResults(DockPanel body)
    {
        body.Margin = new(0); StyleRebelReadControls(body);
        var board = RebelToolFrame.Create("RebelReadResults", "Scan complete", "These findings describe only what the scan observed.", close, key => RebelNavigate?.Invoke(key), 52);
        RebelToolFrame.At(board, new ScrollViewer { Content = body, Width = 1280, Height = 650, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 268, 160);
        return new Viewbox { Child = board, Stretch = Stretch.Uniform };
    }
    internal void ReviewShowResults()
    {
        if (!ReviewSandbox.Active) throw new InvalidOperationException("Review sandbox required.");
        ShowResults([new Finding("Controller identity", "Unknown", "Not collected in the review fixture.", "unavailable")], false, null, null);
    }

    internal void ReviewShowLive()
    {
        if (!ReviewSandbox.Active) throw new InvalidOperationException("Review sandbox required.");
        warning.Visibility = Visibility.Collapsed;
    }

    private static readonly Style RebelSystemChoiceStyle = (Style)System.Windows.Markup.XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="RadioButton"><Setter Property="Foreground" Value="#f7f3ee"/><Setter Property="FontSize" Value="15"/><Setter Property="Height" Value="32"/><Setter Property="Margin" Value="0,0,10,0"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><Border x:Name="Face" BorderBrush="#8f878a" BorderThickness="1.4" Padding="12,3" Background="Transparent" RenderTransformOrigin=".5,.5"><Border.RenderTransform><SkewTransform AngleX="-12"/></Border.RenderTransform><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Face" Property="Background" Value="#e3122d"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Face" Property="BorderBrush" Value="#f7f3ee"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
        """);
}
