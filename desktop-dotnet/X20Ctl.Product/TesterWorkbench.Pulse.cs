using System.Windows;
using System.Windows.Controls;
namespace X20Ctl.Product;

/// <summary>Workbench accessors for custom-themed Tester pages.</summary>
public sealed partial class TesterWorkbench
{
    private Grid? stageMap;
    internal event Action? Updated;
    internal bool RearShown => rear;
    internal string SourceText => SourceLabel.Text;
    internal TextBlock PressedBlock => pressed;
    internal TextBlock OwnershipBlock => ownership;
    internal string PressedText => pressed.Text;
    internal string OwnershipText => ownership.Text;
    internal string KeyboardText => KeyboardMode.Content as string ?? "Keyboard Test";
    internal bool KeyboardOn => KeyboardTest.Enabled;
    internal double? LeftX => State.Frame?.LeftX; internal double? LeftY => State.Frame?.LeftY; internal double? RightX => State.Frame?.RightX; internal double? RightY => State.Frame?.RightY;
    internal double? LeftTrigger => State.Frame?.LT; internal double? RightTrigger => State.Frame?.RT;
    internal FrameworkElement TakeStage() { if (stageMap!.Parent is Viewbox v) v.Child = null; else if (stageMap.Parent is Panel p) p.Children.Remove(stageMap); return stageMap; }
    internal void ClickExit() => Exit.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    internal void ClickKeyboard() => KeyboardMode.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
}
