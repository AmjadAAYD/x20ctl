using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    private void BuildHybridWriteWarning(Panel cards, Button go, Button leave)
    {
        var board = HybridToolFrame.Create("HybridWriteScan", "Before you start the Write scan", "Change one setting while your phone records Bluetooth traffic, then inspect the captured log.", close, key => RebelNavigate?.Invoke(key));
        warning.Background = HybridToolFrame.Color("#17151c"); warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        var route = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string text in new[] { "1  Plan", "2  Prepare phone", "3  Test", "4  Import log", "5  Results" }) { var block = new Border { Width = 210, Height = 58, CornerRadius = new(14), Background = HybridToolFrame.Color("#2d2933"), BorderBrush = HybridToolFrame.Color("#52dcff"), BorderThickness = new(2), Margin = new(0, 0, 14, 0), Child = HybridToolFrame.Text(text, 15, "#f4f1f8", FontWeights.Bold) }; ((TextBlock)block.Child).VerticalAlignment = VerticalAlignment.Center; ((TextBlock)block.Child).HorizontalAlignment = HorizontalAlignment.Center; route.Children.Add(block); }
        HybridToolFrame.At(board, route, 216, 155);
        HybridToolFrame.Detach(cards); cards.Width = 1168; cards.Margin = new(0);
        var originalCards = cards.Children.OfType<Border>().ToArray(); cards.Children.Clear();
        var faces = new List<Border>();
        foreach (var card in originalCards)
        {
            card.CornerRadius = new(24); card.Padding = new(24, 22, 24, 22); card.MinHeight = 200; faces.Add(card);
            var button = new Button { Content = card, Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(0), Tag = "hybrid:write:warning:" + (faces.Count - 1) };
            var template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; template.Seal(); button.Template = template;
            int index = faces.Count - 1; button.GotKeyboardFocus += (_, _) => SelectCard(index); button.MouseEnter += (_, _) => SelectCard(index);
            cards.Children.Add(button);
        }
        void SelectCard(int selected)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                bool active = i == selected; var card = faces[i]; card.Background = HybridToolFrame.Color(active ? "#d5bff6" : "#1f1c25"); card.BorderBrush = HybridToolFrame.Color(active ? "#52dcff" : "#514a58"); card.BorderThickness = new(active ? 2 : 1);
                if (card.Child is Panel content) foreach (var text in content.Children.OfType<TextBlock>()) text.Foreground = HybridToolFrame.Color(active ? "#1b1622" : "#f4f1f8");
            }
        }
        if (faces.Count > 0) SelectCard(0);
        var scroll = new ScrollViewer { Width = 1168, Height = 440, Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HybridToolFrame.At(board, scroll, 216, 235);
        HybridToolFrame.At(board, go, 216, 720); HybridToolFrame.At(board, leave, 500, 720);
        var note = HybridToolFrame.Text("Nothing leaves this PC until you choose Send online and agree at the end.", 14, "#bdb4c8", FontWeights.Normal); note.Width = 1100; HybridToolFrame.At(board, note, 216, 810);
    }
    private void BuildHybridWriteFlow(Grid originalPage)
    {
        HybridToolFrame.Detach(strip); HybridToolFrame.Detach(stage); originalPage.Visibility = Visibility.Collapsed;
        var board = HybridToolFrame.Create("HybridWriteFlow", "Write scan", "One setting per recording. X20CTL reads the phone log and sends nothing to the controller.", close, key => RebelNavigate?.Invoke(key));
        strip.Width = 1168; strip.Height = 58; strip.Margin = new(0); HybridToolFrame.At(board, strip, 216, 150);
        stage.Width = 1168; stage.ClearValue(FrameworkElement.HeightProperty); stage.Margin = new(0);
        var viewport = new ScrollViewer { Width = 1168, Height = 610, Content = stage, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; HybridToolFrame.At(board, viewport, 216, 230);
        var view = new Viewbox { Child = board, Stretch = Stretch.Uniform }; Children.Add(view); Panel.SetZIndex(view, 10); Panel.SetZIndex(warning, 20);
    }
    private void BuildHybridWriteSteps()
    {
        for (int i = 0; i < StepNames.Length; i++)
        {
            bool current = i == step; var label = HybridToolFrame.Text($"{i + 1}  {StepNames[i]}", 14, current ? "#1b1622" : "#f4f1f8", FontWeights.SemiBold); label.VerticalAlignment = VerticalAlignment.Center;
            var card = new Border { Width = 220, Height = 48, CornerRadius = new(12), Background = HybridToolFrame.Color(current ? "#d5bff6" : "#2d2933"), BorderBrush = HybridToolFrame.Color(current ? "#52dcff" : "#514a58"), BorderThickness = new(current ? 3 : 1), Padding = new(12, 0, 12, 0), Margin = new(0, 0, 14, 0), Child = label }; strip.Children.Add(card);
        }
    }
    private FrameworkElement HybridWritePlan()
    {
        var root = new Grid { Name = "HybridWritePlan", Width = 1168 }; root.ColumnDefinitions.Add(new() { Width = new GridLength(600) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); root.ColumnDefinitions.Add(new());
        var left = new StackPanel { Margin = new(18) }; left.Children.Add(HybridToolFrame.Text("What you'll test", 25, "#f5f1fa", FontWeights.Bold));
        foreach (var (caption, field) in new[] { ("Controller model (your claim)", model), ("App", app), ("The setting you'll change", setting), ("Its value now", original), ("Change it to", test) })
        {
            var label = HybridToolFrame.Text(caption, 14, "#c8bdd1", FontWeights.SemiBold); label.Margin = new(0, 10, 0, 4); left.Children.Add(label);
            HybridToolFrame.Detach(field); field.Width = 550; field.MinHeight = 38; field.Margin = new(0); field.HorizontalAlignment = HorizontalAlignment.Left; System.Windows.Automation.AutomationProperties.SetName(field, caption); left.Children.Add(field);
        }
        HybridToolFrame.Detach(undoable); var consent = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 16, 0, 12), Children = { undoable, HybridToolFrame.Text("I know how to restore the original value", 14, "#f5f1fa", FontWeights.Normal) } }; left.Children.Add(consent);
        HybridToolFrame.Detach(planNext); left.Children.Add(planNext); root.Children.Add(new Border { Background = HybridToolFrame.Color("#29242f"), CornerRadius = new(22), Child = left });
        var guidance = new StackPanel { Margin = new(24) }; guidance.Children.Add(HybridToolFrame.Text("Picking a good first test", 23, "#f5f1fa", FontWeights.Bold));
        foreach (string text in new[] { "Choose one small, reversible change such as vibration strength, a dead zone or a button remap.", "Avoid firmware updates, resets, calibration, pairing and mode switches.", "Write down the original value. One change per recording keeps the result readable." }) { var line = HybridToolFrame.Text(text, 15, "#c8bdd1", FontWeights.Normal); line.Margin = new(0, 16, 0, 0); guidance.Children.Add(line); }
        var right = new Border { Background = HybridToolFrame.Color("#302b36"), CornerRadius = new(22), Child = guidance }; Grid.SetColumn(right, 2); root.Children.Add(right); return root;
    }
    private FrameworkElement HybridWriteTest(Button restart, Button stuck, Border help)
    {
        var root = new Grid { Name = "HybridWriteTest", Width = 1168 }; root.ColumnDefinitions.Add(new() { Width = new GridLength(600) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); root.ColumnDefinitions.Add(new());
        var left = new StackPanel { Margin = new(22) }; left.Children.Add(HybridToolFrame.Text("Do the test", 25, "#f5f1fa", FontWeights.Bold));
        foreach (var text in new[] { partTitle, partSay, partCount }) { HybridToolFrame.Detach(text); text.Margin = new(0, 12, 0, 0); left.Children.Add(text); }
        var actions = new WrapPanel { Margin = new(0, 24, 0, 16) }; foreach (var button in new[] { partDone, restart, stuck }) { HybridToolFrame.Detach(button); button.Margin = new(0, 0, 10, 8); actions.Children.Add(button); } left.Children.Add(actions); HybridToolFrame.Detach(help); left.Children.Add(help);
        root.Children.Add(new Border { Background = HybridToolFrame.Color("#29242f"), CornerRadius = new(22), Child = left });
        var list = new StackPanel { Margin = new(24) }; list.Children.Add(HybridToolFrame.Text("The test", 23, "#f5f1fa", FontWeights.Bold)); var parts = Parts();
        for (int i = 0; i < parts.Length; i++) { var line = HybridToolFrame.Text((i < part ? "✓  " : i == part ? "▶  " : "    ") + parts[i].Title, 16, "#c8bdd1", FontWeights.SemiBold); line.Margin = new(0, 12, 0, 0); list.Children.Add(line); }
        var right = new Border { Background = HybridToolFrame.Color("#302b36"), CornerRadius = new(22), Child = list }; Grid.SetColumn(right, 2); root.Children.Add(right); return root;
    }
    private FrameworkElement HybridWriteResults(DockPanel source, string setting, string origin)
    {
        var parts = source.Children.OfType<FrameworkElement>().ToArray();
        if (parts.Length != 4) return source;
        foreach (var part in parts) HybridToolFrame.Detach(part);
        headline.Rows = 2; headline.Columns = 2;
        var grid = new Grid { Name = "HybridWriteResults", Width = 1168 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(588) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); grid.ColumnDefinitions.Add(new());
        var left = new StackPanel { Margin = new(20) };
        left.Children.Add(HybridToolFrame.Text("Results", 25, "#f5f1fa", FontWeights.Bold));
        var context = HybridToolFrame.Text(setting.Length > 0 ? setting + " · " + origin : origin, 14, "#c8bdd1", FontWeights.SemiBold); context.TextWrapping = TextWrapping.Wrap; context.Margin = new(0, 4, 0, 16); left.Children.Add(context);
        left.Children.Add(parts[0]); left.Children.Add(parts[1]); parts[3].Height = 260; left.Children.Add(parts[3]);
        grid.Children.Add(new Border { Background = HybridToolFrame.Color("#29242f"), CornerRadius = new(22), Child = left });
        var right = new StackPanel { Margin = new(22) }; right.Children.Add(HybridToolFrame.Text("Save and share", 23, "#f5f1fa", FontWeights.Bold));
        var help = HybridToolFrame.Text("Review the findings before saving or choosing what to send.", 14, "#c8bdd1", FontWeights.Normal); help.TextWrapping = TextWrapping.Wrap; help.Margin = new(0, 8, 0, 12); right.Children.Add(help); right.Children.Add(parts[2]);
        var card = new Border { Background = HybridToolFrame.Color("#302b36"), CornerRadius = new(22), Child = right }; Grid.SetColumn(card, 2); grid.Children.Add(card);
        return grid;
    }

}
