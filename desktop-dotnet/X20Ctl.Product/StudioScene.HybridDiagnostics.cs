using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private Diagnostics.DiagnosticsWorkbench? hybridDiagSource;
    private bool hybridDiagDetails;
    private StackPanel? hybridDiagRows;
    private static void HDetach(FrameworkElement element) { if (element.Parent is Panel p) p.Children.Remove(element); else if (element.Parent is Decorator d) d.Child = null; }
    private void HybridDiagnosticsPage()
    {
        var w = Diagnostics; if (hybridDiagnostics == null || w == null) return;
        if (!ReferenceEquals(hybridDiagSource, w)) { hybridDiagSource = w; w.Refreshed += HybridDiagnosticsRefresh; }
        if (hybridDiagnostics.Children.Count > 0) return;
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridDiagnostics.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0); HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, HText("Diagnostics", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14); HAt(board, HText("Read-only input capture · Use Esc to leave.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "device", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        void Tool(string label, string tag, Action action, double width = 156) { var button = HybridButton(label, "hybrid:diag:" + tag, false, width, 40); button.Margin = new(0, 0, 6, 0); button.Click += (_, _) => action(); bar.Children.Add(button); }
        Tool("Player input", "player", () => { w.ChooseSlot(((w.ChosenSlot < 0 ? w.PreferredSlot : w.ChosenSlot) + 1) % 4); });
        Tool("Retry input", "retry", () => w.ChooseSlot(-1)); Tool("Integer values", "raw", w.ToggleRaw); Tool("Measure center", "measure", w.Measure); Tool("Clear", "clear", w.ClearAll, 110); Tool("Copy report", "copy", w.CopyReport);
        Tool("Details", "details", () => { hybridDiagDetails = !hybridDiagDetails; hybridDiagnostics.Children.Clear(); HybridDiagnosticsPage(); }, 110); HAt(board, bar, 216, 120);
        if (!hybridDiagDetails)
        {
            HybridDiagnosticField(board, w, true, 216); HybridDiagnosticField(board, w, false, 824);
            HAt(board, new Rectangle { Width = 3, Height = 390, Fill = HBrush("#52dcff") }, 800, 248);
        }
        else
        {
            HAt(board, HText("BUTTONS AND REPORTS", 16, "#d5bff6", FontWeights.Bold), 216, 202);
            hybridDiagRows = new StackPanel { Width = 680 }; var viewport = new ScrollViewer { Content = hybridDiagRows, Width = 700, Height = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; HAt(board, viewport, 216, 240);
            var info = new StackPanel { Width = 430 };
            foreach (var source in new[] { w.NumericBlock, w.RawBlock, w.DriftBlock }) { var text = HText("", 14, "#c8bdd1", FontWeights.Normal); text.TextWrapping = TextWrapping.Wrap; text.Margin = new(0, 0, 0, 16); text.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = source }); info.Children.Add(text); } HAt(board, info, 940, 240); HybridDiagnosticsRefresh();
        }
        var status = HText("", 15, "#c8bdd1", FontWeights.SemiBold); status.Width = 1160; status.TextWrapping = TextWrapping.Wrap; status.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = w.SourceBlock }); HAt(board, status, 216, 690);
        var rate = HText("", 15, "#f5f1fa", FontWeights.Bold); rate.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = w.RateBlock }); HAt(board, rate, 216, 742);
    }
    private void HybridDiagnosticField(Canvas board, Diagnostics.DiagnosticsWorkbench workbench, bool left, double x)
    {
        var frame = new Border { Width = 560, Height = 390, Background = HBrush(left ? "#40354a" : "#28252f"), CornerRadius = new(30) }; HAt(board, frame, x, 248);
        var (reach, dot, rest, stats) = left ? workbench.LeftView : workbench.RightView;
        foreach (var item in new FrameworkElement[] { reach, dot, rest, stats }) HDetach(item);
        var plot = new Canvas { Width = 250, Height = 250, IsHitTestVisible = false };
        plot.Children.Add(new Ellipse { Width = 250, Height = 250, Stroke = HBrush("#776c80"), StrokeThickness = 2 });
        plot.Children.Add(new Ellipse { Width = 100, Height = 100, Stroke = HBrush("#514b58"), StrokeThickness = 2 }.Also(e => { Canvas.SetLeft(e, 75); Canvas.SetTop(e, 75); }));
        double size = global::X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize, scale = 118 / global::X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.UnitSize;
        var live = new Canvas { Width = size, Height = size, RenderTransform = new ScaleTransform(scale, scale, size / 2, size / 2), IsHitTestVisible = false };
        live.Children.Add(reach); live.Children.Add(rest); live.Children.Add(dot); Canvas.SetLeft(live, 125 - size / 2); Canvas.SetTop(live, 125 - size / 2); plot.Children.Add(live); HAt(board, plot, x + 50, 284);
        HAt(board, HText(left ? "Left stick" : "Right stick", 19, "#f5f1fa", FontWeights.Bold), x + 75, 555);
        stats.Width = 245; stats.FontSize = 13; stats.Foreground = HBrush("#c8bdd1"); stats.TextAlignment = TextAlignment.Center; stats.TextWrapping = TextWrapping.Wrap; HAt(board, stats, x + 50, 584);
        var (bar, range, triggerStats) = left ? workbench.LtView : workbench.RtView;
        foreach (var item in new FrameworkElement[] { bar, range, triggerStats }) HDetach(item);
        var gauge = new Grid { Width = 42, Height = 150, Background = HBrush("#36303d") }; gauge.Children.Add(range); gauge.Children.Add(bar);
        var scaled = new Viewbox { Width = 60, Height = 250, Stretch = Stretch.Fill, Child = gauge, IsHitTestVisible = false }; HAt(board, scaled, x + 380, 284);
        HAt(board, HText(left ? "LT" : "RT", 19, "#f5f1fa", FontWeights.Bold), x + 392, 555);
        triggerStats.Width = 130; triggerStats.FontSize = 13; triggerStats.Foreground = HBrush("#c8bdd1"); triggerStats.TextAlignment = TextAlignment.Center; HAt(board, triggerStats, x + 345, 584);
        if (workbench.UseFor(left) is { } use) { HDetach(use.Use); HDetach(use.Said); HAt(board, use.Use, x + 50, 620); HAt(board, use.Said, x + 50, 655); }
    }
    private void HybridDiagnosticsRefresh()
    {
        if (!hybridDiagDetails || hybridDiagRows == null || Diagnostics == null || ManagementPage != "Diagnostics") return;
        hybridDiagRows.Children.Clear(); foreach (var row in Diagnostics.ButtonRows()) hybridDiagRows.Children.Add(HText($"{row.Name}   {row.Count}", 15, row.Warn ? "#ffc05b" : row.Down ? "#52dcff" : "#ded4e5", FontWeights.SemiBold));
    }
}
