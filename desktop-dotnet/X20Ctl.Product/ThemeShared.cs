using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>What the remaining themes share: tag lookup, page grouping, curve rows and the events their pages raise.</summary>
public sealed partial class FeedbackPage
{
    internal static void Detach(FrameworkElement? element)
    {
        if (element == null) return;
        if (element.Parent is Panel p) p.Children.Remove(element);
        else if (element.Parent is ContentControl c) c.Content = null;
        else if (element.Parent is Border b) b.Child = null;
        else if (element.Parent is Decorator d) d.Child = null;
    }
}

public partial class DockView
{
    public event Action? BackRequested;
    public event Action<string>? PageRequested;
}

public partial class StudioScene
{
    public event Action? ToolsRequested;
    private static readonly string[] CurvePresets = ["default", "quick", "slow", "smooth", "fine"];
    private static readonly (string name, string[] keys)[] ControlGroups =
    [("Face", ["A", "B", "X", "Y"]), ("Shoulders", ["LB", "RB", "LT", "RT"]), ("Sticks", ["L3", "R3", "LSTICK_ANALOG", "RSTICK_ANALOG"]), ("System", ["SELECT", "START", "HOME", "CAPTURE", "TURBO"])];
    private (string name, string[] keys)[] AvailableGroups()
    {
        var groups = ControlGroups.Select(group => (group.name, keys: group.keys.Where(key => layout.Controls.Any(control => control.Key == key)).ToArray())).Where(group => group.keys.Length > 0).ToList();
        var paddles = layout.Controls.Where(control => control.Role == "macro").Select(control => control.Key).Distinct().ToArray();
        if (paddles.Length > 0) groups.Add(("Paddles", paddles));
        return groups.ToArray();
    }
    private (string label, System.Windows.Controls.Primitives.RangeBase slider)[] CurveRows => Curves.Channel is CurveChannel.LeftStick or CurveChannel.RightStick
        ? [("Inner dead zone", Curves.Inner), ("Anti-dead zone", Curves.Floor), ("Full output at", Curves.Outer), ("Max output", Curves.Ceiling)]
        : [("Start point", Curves.Inner), ("Anti-dead zone", Curves.Floor), ("Full press point", Curves.Outer), ("Max output", Curves.Ceiling)];
    private static FrameworkElement? FindTagged(DependencyObject root, string tag)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child == null) continue;
            if (child is FrameworkElement { Tag: string t } fe && t == tag && fe.Focusable) return fe;
            if (FindTagged(child, tag) is { } found) return found;
        }
        return null;
    }
}

public partial class StudioScene
{
    private static string InputGlyph(string key) => key switch { "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", "SELECT" => "View", "START" => "Menu", "HOME" => "Home", _ => key };
    private static string StickArrow(StickHeading h) => h switch
    {
        StickHeading.Up => "↑", StickHeading.UpRight => "↗", StickHeading.Right => "→", StickHeading.DownRight => "↘", StickHeading.Down => "↓", StickHeading.DownLeft => "↙", StickHeading.Left => "←", StickHeading.UpLeft => "↖", _ => "·"
    };
    private static string MacroPresses(MacroEvent e) => string.Join(" + ", e.Buttons.Select(InputGlyph)
        .Concat(e.Left == StickHeading.Neutral ? [] : ["L " + StickArrow(e.Left)]).Concat(e.Right == StickHeading.Neutral ? [] : ["R " + StickArrow(e.Right)])) is { Length: > 0 } t ? t : "Neutral";
}

public partial class ZoneScene
{
    private DockBounds[] Bounds()
    {
        if(!IllustratedThemeUI.Matched||State.FocusedPlayer.HasValue)return ZoneLayout.Calculate(DockStage.ActualWidth,DockStage.ActualHeight,State.FocusedPlayer);
        string id=IllustratedThemeUI.Current!.Id;
        double gap=id=="manga"?14:22,wide=DockStage.ActualWidth,high=DockStage.ActualHeight;
        if(id is "frutiger-aero" or "tokyo-night" or "crimson-rebellion")
        {
            double cw=Math.Max(1,(wide-gap)/2),ch=Math.Max(1,(high-gap-20)/2);
            return Enumerable.Range(0,4).Select(i=>new DockBounds(i%2*(cw+gap),10+i/2*(ch+gap),cw,ch)).ToArray();
        }
        double w=Math.Max(1,(wide-gap*3)/4),h=Math.Max(1,high*(id=="vaporwave"?.82:id=="synthwave"?.76:1)-24);
        return Enumerable.Range(0,4).Select(i=>new DockBounds(i*(w+gap),id=="synthwave"?high-h-12:(high-h)/2,w,h)).ToArray();
    }
}
