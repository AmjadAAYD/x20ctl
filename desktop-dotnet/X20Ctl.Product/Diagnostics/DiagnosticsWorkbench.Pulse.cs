using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
namespace X20Ctl.Product.Diagnostics;

/// <summary>Workbench accessors for custom-themed Diagnostics pages.</summary>
public sealed partial class DiagnosticsWorkbench
{
    private Button? measureButton, copyButton;
    internal event Action? Refreshed;
    internal static double PlotSize => Plot;
    internal static double UnitSize => Unit;
    internal TextBlock SourceBlock => source; internal TextBlock RateBlock => rateText; internal TextBlock RawBlock => rawText; internal TextBlock NumericBlock => numericValues; internal TextBlock DriftBlock => driftDetails;
    internal (Polygon Reach, Ellipse Dot, Ellipse Resting, TextBlock Stats) LeftView => leftView;
    internal (Polygon Reach, Ellipse Dot, Ellipse Resting, TextBlock Stats) RightView => rightView;
    internal (Rectangle Bar, Rectangle Range, TextBlock Stats) LtView => ltView;
    internal (Rectangle Bar, Rectangle Range, TextBlock Stats) RtView => rtView;
    internal (Button Use, TextBlock Said)? UseFor(bool leftStick) => apply.TryGetValue(leftStick ? left : right, out var a) ? (a.Use, a.Said) : null;
    internal int ChosenSlot => chosen; internal int ActiveSlot => slot; internal int PreferredSlot => preferredSlot;
    internal void ChooseSlot(int value) => Choose(value);
    internal bool RawValues => rawValues;
    internal void ToggleRaw() { rawValues = !rawValues; Refresh(); }
    internal bool Measuring => measureButton is { IsEnabled: false };
    internal void Measure() => measureButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal void CopyReport() => copyButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal void ClearAll() => Clear();
    internal IReadOnlyList<(string Name, bool Down, string Count, bool Warn, bool Used, string Tip)> ButtonRows()
    {
        lock (gate)
            return Buttons.Select(x => { var (cell, count) = cells[x.Mask]; var b = presses[x.Mask]; return (x.Name, b.Down, count.Text, b.Bounces > 0, b.Presses > 0, cell.ToolTip as string ?? ""); }).ToList();
    }
}
