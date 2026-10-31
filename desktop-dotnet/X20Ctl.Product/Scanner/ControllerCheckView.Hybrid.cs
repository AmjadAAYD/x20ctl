using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    private void BuildHybridReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var board = HybridToolFrame.Create("HybridReadScan", "Before you scan", "The scan guides you through each control while recording what the controller reports.", close, key => RebelNavigate?.Invoke(key));
        warning.Background = HybridToolFrame.Color("#17151c"); warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        systemRow.Width = 560; systemRow.Margin = new(0); HybridToolFrame.At(board, systemRow, 216, 145);
        var sequence = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string step in new[] { "1  Start", "2  Find slot", "3  Confirm", "4  Test controls", "5  Save report" }) { var block = new Border { Width = 210, Height = 58, Background = HybridToolFrame.Color("#2d2933"), BorderBrush = HybridToolFrame.Color("#52dcff"), BorderThickness = new(2), CornerRadius = new(14), Margin = new(0, 0, 14, 0), Child = HybridToolFrame.Text(step, 15, "#f4f1f8", FontWeights.Bold) }; ((TextBlock)block.Child).VerticalAlignment = VerticalAlignment.Center; ((TextBlock)block.Child).HorizontalAlignment = HorizontalAlignment.Center; sequence.Children.Add(block); }
        HybridToolFrame.At(board, sequence, 216, 235);
        HybridToolFrame.Detach(cards);
        var expectations = ScanFlow.Expectations.ToArray();
        var rows = new StackPanel { Width = 588, Tag = "hybrid:scan:expectations" };
        var rowScroll = new ScrollViewer { Content = rows, Width = 588, Height = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        HybridToolFrame.At(board, rowScroll, 216, 320);
        HybridToolFrame.At(board, new Rectangle { Width = 2, Height = 300, Fill = HybridToolFrame.Color("#52dcff") }, 846, 320);
        var title = HybridToolFrame.Text("", 23, "#f5f1fa", FontWeights.Bold); title.Tag = "hybrid:scan:expectation-title";
        var description = HybridToolFrame.Text("", 16, "#c8bdd1", FontWeights.Normal); description.Margin = new(0, 18, 0, 0);
        var body = new StackPanel { Margin = new(26), Children = { title, description } };
        HybridToolFrame.At(board, new Border { Width = 492, Height = 300, CornerRadius = new(22), Background = HybridToolFrame.Color("#29242f"), Child = body }, 892, 320);
        void SelectExpectation(int selected)
        {
            rows.Children.Clear(); title.Text = expectations[selected].Item1; description.Text = expectations[selected].Item2;
            for (int i = 0; i < expectations.Length; i++)
            {
                int index = i; var item = HybridToolFrame.Button(expectations[i].Item1, "hybrid:scan:expectation:" + i, 588, 52, i == selected);
                item.Margin = new(0, 0, 0, 8); item.Click += (_, _) => { SelectExpectation(index); rows.Children.OfType<Button>().FirstOrDefault(b => b.Tag as string == "hybrid:scan:expectation:" + index)?.Focus(); };
                rows.Children.Add(item);
            }
        }
        if (expectations.Length > 0) SelectExpectation(0);
        choices.Width = 1168; choices.Margin = new(0); HybridToolFrame.At(board, choices, 216, 680);
        linux.Width = 1168; linux.Margin = new(0); HybridToolFrame.At(board, linux, 216, 680);
        var disclosure = HybridToolFrame.Text("The ZIP is saved locally. Scan and send also sends a compact summary when the scan ends.", 13, "#bdb4c8", FontWeights.Normal); disclosure.Width = 1168; HybridToolFrame.At(board, disclosure, 216, 825);
    }
    private void BuildHybridReadFlow(Grid page)
    {
        HybridToolFrame.Detach(page); page.Margin = new(0); page.Width = 1168; page.Height = 690; page.ClipToBounds = true;
        if (page.RowDefinitions.Count > 0) page.RowDefinitions[0].Height = new GridLength(0);
        var board = HybridToolFrame.Create("HybridReadFlow", "Controller Check", "Read-only input · hardware configuration remains separate.", close, key => RebelNavigate?.Invoke(key));
        HybridToolFrame.At(board, page, 216, 150); var view = new Viewbox { Child = board, Stretch = Stretch.Uniform }; Children.Add(view); Panel.SetZIndex(view, 10); Panel.SetZIndex(warning, 20); Panel.SetZIndex(overlay, 30); Panel.SetZIndex(resultsLayer, 40);
    }
    private FrameworkElement HybridReadResults(DockPanel body)
    {
        body.Margin = new(0); var board = HybridToolFrame.Create("HybridReadResults", "Scan complete", "These findings describe only what the scan observed.", close, key => RebelNavigate?.Invoke(key));
        var scroll = new ScrollViewer { Content = body, Width = 1168, Height = 680, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HybridToolFrame.At(board, scroll, 216, 150); return new Viewbox { Child = board, Stretch = Stretch.Uniform };
    }
}
