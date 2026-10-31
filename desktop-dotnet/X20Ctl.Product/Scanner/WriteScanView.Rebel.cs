using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    internal Action<string>? RebelNavigate { get; set; }
    private void BuildRebelWriteWarning(Panel cards, Button go, Button leave)
    {
        var board = RebelToolFrame.Create("RebelWriteScan", "Before you start the Write scan", "You'll change one setting in KeyLinker while your phone records its Bluetooth traffic. X20CTL then shows which bytes the app sent for that change. One recording teaches one setting.", close, key => RebelNavigate?.Invoke(key), 40);
        warning.Background = Ink.B(Ink.Black); warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        var paper = new Grid { Width = 820, Height = 470, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-.8) };
        paper.Children.Add(new TornPaper { Seed = 20 }); RebelToolFrame.At(board, paper, 262, 190);
        var list = new StackPanel { Margin = new(26, 20, 26, 16) }; list.Children.Add(Ink.Label("Before you start the Write scan", 12, Ink.B(Ink.Cut))); paper.Children.Add(list);
        int index = 0;
        foreach (var card in cards.Children.OfType<Border>().ToArray())
        {
            RebelToolFrame.Detach(card); card.Background = Brushes.Transparent; card.BorderBrush = Ink.B(Ink.A(Ink.Black, .5)); card.BorderThickness = new(0, 2, 0, 0); card.CornerRadius = new(0); card.Margin = new(0); card.Padding = new(18, 10, 18, 10);
            RebelToolFrame.InkText(card, Ink.Black);
            if (card.Child is StackPanel body && body.Children.OfType<TextBlock>().FirstOrDefault() is { } heading) { heading.FontSize = 19; heading.FontFamily = Ink.Heavy; }
            var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new(48) }); row.ColumnDefinitions.Add(new());
            var number = Ink.Text((++index).ToString(), 22, Ink.B(index == 1 ? Ink.White : Ink.Black)); number.HorizontalAlignment = HorizontalAlignment.Center; number.VerticalAlignment = VerticalAlignment.Top;
            var numberBox = new Border { Child = number, Width = 40, Height = 40, Background = index == 1 ? Ink.B(Ink.Red) : Brushes.Transparent, Margin = new(0, 12, 0, 0) }; row.Children.Add(numberBox);
            Grid.SetColumn(card, 1); row.Children.Add(card); list.Children.Add(row);
        }
        RebelToolFrame.At(board, WriteClock(), 1130, 210);
        var note = Ink.Text("Nothing leaves this PC until you press Send online at the end, and you see what goes first. The findings are saved to Documents\\X20CTLWriteScans only when you press Save.", 15, Ink.B(Ink.White), false);
        note.Width = 980; note.TextWrapping = TextWrapping.Wrap; RebelToolFrame.At(board, note, 300, 690);
        foreach (var button in new[] { go, leave }) { button.Margin = new(0); RebelCommands.Dress(button, button == go); }
        RebelToolFrame.At(board, go, 300, 768); RebelToolFrame.At(board, leave, 570, 768);
    }

    private bool rebelPlanButtonStyled;
    private void BuildRebelWriteFlow(Grid originalPage)
    {
        RebelToolFrame.Detach(strip); RebelToolFrame.Detach(stage); originalPage.Visibility = Visibility.Collapsed;
        var board = RebelToolFrame.Create("RebelWriteFlow", "Write scan", "One setting per recording. X20CTL reads the phone log and sends nothing to the controller.", close, key => RebelNavigate?.Invoke(key), 52);
        strip.Width = 1280; strip.Height = 58; strip.Margin = new(0); RebelToolFrame.At(board, strip, 268, 180);
        stage.Width = 1280; stage.Height = 560; stage.Margin = new(0); RebelToolFrame.At(board, stage, 268, 258);
        var view = new Viewbox { Child = board, Stretch = Stretch.Uniform }; Children.Add(view); Panel.SetZIndex(view, 10); Panel.SetZIndex(warning, 20);
    }
    private void BuildRebelWriteSteps()
    {
        for (int i = 0; i < StepNames.Length; i++)
        {
            bool current = i == step; var card = new Slab { Width = 238, Height = 48, Seed = 420 + i, Rot = i % 2 == 0 ? -1 : 1, Fill = Ink.B(current ? Ink.Red : Ink.Panel), Line = Ink.B(Ink.A(Ink.White, .3)), LineWidth = 1 };
            var label = Ink.Text($"{i + 1}   {StepNames[i]}", 15, Ink.B(Ink.White)); label.TextWrapping = TextWrapping.Wrap; label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new(14, 0, 10, 0); card.Children.Add(label); card.Margin = new(0, 0, 18, 0); strip.Children.Add(card);
        }
    }
    private FrameworkElement RebelWritePlan()
    {
        var canvas = new Canvas { Name = "RebelWritePlan", Width = 1280, Height = 560 };
        var left = new StackPanel { Width = 710 }; left.Children.Add(Ink.Label("What you'll test", 13));
        left.Children.Add(Ink.Text("One setting you can put back", 27));
        void FieldRow(string caption, TextBox field)
        {
            RebelToolFrame.Detach(field); field.Margin = new(0); field.MinHeight = 40; field.Background = Ink.B(Ink.Panel); field.Foreground = Ink.B(Ink.White); field.CaretBrush = Ink.B(Ink.White); field.BorderBrush = Ink.B(Ink.Grey);
            System.Windows.Automation.AutomationProperties.SetName(field, caption);
            var row = new Grid { Height = 60, Background = Ink.B(Ink.Panel), Margin = new(0, 0, 0, 7) };
            var label = Ink.Text(caption, 16); label.Margin = new(16, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label);
            field.Width = 340; field.HorizontalAlignment = HorizontalAlignment.Right; field.VerticalAlignment = VerticalAlignment.Center; field.Margin = new(0, 0, 12, 0); row.Children.Add(field); left.Children.Add(row);
        }
        FieldRow("Controller model (your claim)", model); FieldRow("App", app); FieldRow("The setting you'll change", setting); FieldRow("Its value now", original); FieldRow("Change it to", test);
        RebelToolFrame.Detach(undoable); var consent = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 12) }; consent.Children.Add(undoable); consent.Children.Add(Ink.Text("I know how to restore the original value", 16, Ink.B(Ink.White), false).Also(t => t.Margin = new(12, 0, 0, 0))); left.Children.Add(consent);
        RebelToolFrame.Detach(planNext); if (!rebelPlanButtonStyled) { RebelCommands.Dress(planNext, true); rebelPlanButtonStyled = true; } left.Children.Add(planNext); RebelToolFrame.At(canvas, left, 0, 0);
        var paper = new Grid { Width = 500, Height = 440, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1.2) }; paper.Children.Add(new TornPaper { Seed = 421 });
        var notes = new StackPanel { Margin = new(32, 28, 32, 24) }; notes.Children.Add(Ink.Label("Picking a good first test", 13, Ink.B(Ink.Cut))); notes.Children.Add(Ink.Text("Make one small change", 27, Ink.B(Ink.Black)).Also(t => t.Margin = new(0, 8, 0, 16)));
        foreach (var text in new[] { "Good choices: vibration strength, a trigger or stick dead zone, one button remap, or the auto-off timer.", "Avoid firmware updates, factory reset, calibration, pairing and mode switches. Those cannot be undone by restoring a setting.", "Write down the original value. One change per recording keeps the result readable." })
        { var line = Ink.Text(text, 16, Ink.B(Ink.Black), false); line.TextWrapping = TextWrapping.Wrap; line.Margin = new(0, 0, 0, 16); notes.Children.Add(line); }
        paper.Children.Add(notes); RebelToolFrame.At(canvas, paper, 760, 10); return canvas;
    }

    private FrameworkElement RebelWriteTest(Button restart, Button stuck, Border help)
    {
        var canvas = new Canvas { Name = "RebelWriteTest", Width = 1280, Height = 560 };
        var left = new StackPanel { Width = 710 }; left.Children.Add(Ink.Label("Do the test", 13));
        foreach (var text in new[] { partTitle, partSay, partCount }) { RebelToolFrame.Detach(text); text.Margin = new(0, 12, 0, 0); left.Children.Add(text); }
        partTitle.FontFamily = Ink.Heavy; partTitle.FontWeight = FontWeights.Black; partTitle.FontSize = 34; partTitle.Foreground = Ink.B(Ink.White);
        partSay.FontFamily = Ink.Body; partSay.FontSize = 18; partCount.FontFamily = Ink.Body; partCount.Foreground = Ink.B(Ink.Hot);
        var actions = new WrapPanel { Margin = new(0, 24, 0, 16) };
        foreach (var button in new[] { partDone, restart, stuck }) { RebelToolFrame.Detach(button); RebelCommands.Dress(button, button == partDone); button.Margin = new(0, 0, 12, 8); actions.Children.Add(button); }
        left.Children.Add(actions); RebelToolFrame.Detach(help); help.CornerRadius = new(0); help.Background = Ink.B(Ink.Panel); help.BorderBrush = Ink.B(Ink.Grey); left.Children.Add(help);
        RebelToolFrame.At(canvas, new ScrollViewer { Content = left, Width = 730, Height = 540, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 0, 0);
        var paper = new Grid { Width = 500, Height = 440, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1.2) }; paper.Children.Add(new TornPaper { Seed = 423 });
        var list = new StackPanel { Margin = new(32, 28, 32, 24) }; list.Children.Add(Ink.Label("The test", 13, Ink.B(Ink.Cut))); list.Children.Add(Ink.Text("Five steps", 28, Ink.B(Ink.Black)).Also(t => t.Margin = new(0, 8, 0, 20)));
        var parts = Parts(); for (int i = 0; i < parts.Length; i++) { var line = Ink.Text((i < part ? "✓  " : i == part ? "▶  " : "     ") + parts[i].Title, 17, Ink.B(i == part ? Ink.Red : Ink.Black)); line.TextWrapping = TextWrapping.Wrap; line.Margin = new(0, 0, 0, 12); list.Children.Add(line); }
        var note = Ink.Text("The pauses keep each action apart in the recording, so the change can be distinguished from background traffic.", 15, Ink.B(Ink.Black), false); note.TextWrapping = TextWrapping.Wrap; note.Margin = new(0, 12, 0, 0); list.Children.Add(note);
        paper.Children.Add(list); RebelToolFrame.At(canvas, paper, 760, 10); return canvas;
    }

    private FrameworkElement RebelWriteResults(DockPanel contents, string settingName, string source)
    {
        var root = new Grid { Name = "RebelWriteResults" }; var slab = new Slab { Seed = 424, Fill = Ink.B(Ink.Panel), Line = Ink.B(Ink.A(Ink.White, .3)), LineWidth = 1.5 }; root.Children.Add(slab);
        var layout = new DockPanel { Margin = new(24, 18, 24, 18) }; var heading = new StackPanel { Margin = new(0, 0, 0, 14) };
        heading.Children.Add(Ink.Label("Write scan · Results", 12)); var detail = Ink.Text(settingName.Length > 0 ? settingName + " · " + source : source, 20); detail.TextWrapping = TextWrapping.Wrap; heading.Children.Add(detail);
        DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading); StyleRebelResults(contents);
        layout.Children.Add(new ScrollViewer { Content = contents, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); slab.Children.Add(layout); return root;
    }
    private static void StyleRebelResults(DependencyObject item)
    {
        if (item is Button button) RebelCommands.Dress(button);
        if (item is Border border) { border.CornerRadius = new(0); if (border.Background != null) border.Background = Ink.B(Ink.Panel); }
        foreach (object child in LogicalTreeHelper.GetChildren(item)) if (child is DependencyObject nested) StyleRebelResults(nested);
    }

    private static Canvas WriteClock()
    {
        const double cx = 200, cy = 200, radius = 170;
        var canvas = new Canvas { Width = 400, Height = 400, IsHitTestVisible = false };
        canvas.Children.Add(new Ellipse { Width = 368, Height = 368, Fill = Ink.B(Ink.White), Margin = new(16, 16, 0, 0), Effect = Ink.Shadow(10, 12, 0, Colors.Black, 1) });
        canvas.Children.Add(new Ellipse { Width = 340, Height = 340, Fill = Ink.B("#0f0e10"), Margin = new(30, 30, 0, 0) });
        static Point At(double minute, double r) { double a = (minute * 6 - 90) * Math.PI / 180; return new(cx + r * Math.Cos(a), cy + r * Math.Sin(a)); }
        var arc = new PathFigure { StartPoint = At(20, radius - 26) }; arc.Segments.Add(new ArcSegment(At(40, radius - 26), new(radius - 26, radius - 26), 0, false, SweepDirection.Clockwise, true));
        canvas.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([arc]), Stroke = Ink.B(Ink.Red), StrokeThickness = 36 });
        for (int i = 0; i < 60; i++)
        {
            var start = At(i, radius - 4); var end = At(i, radius - (i % 5 == 0 ? 18 : 9));
            canvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = Ink.B(Ink.White), StrokeThickness = i % 5 == 0 ? 3 : 1.2 });
        }
        var h = At(20, radius * .5); var m = At(40, radius * .78);
        canvas.Children.Add(new Line { X1 = cx, Y1 = cy, X2 = h.X, Y2 = h.Y, Stroke = Ink.B(Ink.White), StrokeThickness = 9 });
        canvas.Children.Add(new Line { X1 = cx, Y1 = cy, X2 = m.X, Y2 = m.Y, Stroke = Ink.B(Ink.Red), StrokeThickness = 6 });
        canvas.Children.Add(Star.Make(cx, cy, 14)); return canvas;
    }
}
