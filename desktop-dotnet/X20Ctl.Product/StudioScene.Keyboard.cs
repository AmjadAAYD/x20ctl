using System.Windows;
using System.Windows.Controls;
namespace X20Ctl.Product;

/// <summary>The Keyboard tab (PC mode only) and the review hooks for the Mouse and Keyboard pages.</summary>
public partial class StudioScene
{
    /// <summary>PC mode's Keyboard tab: it takes the Macros tab's place while PC mode is on (owner, 7 Nov 2026).</summary>
    internal RadioButton? KeyboardNav { get; private set; }
    public KeyboardWorkbench? KeyboardPage { get; private set; }

    private void BuildKeyboardTab(string group)
    {
        KeyboardNav = new RadioButton { Content = "Keyboard", Style = DS.Style("DS.TopTab"), FontSize = 14, Margin = new(0, 0, 24, 0), Visibility = Visibility.Collapsed, GroupName = group, ToolTip = "Controller buttons that press keys or mouse buttons" };
        KeyboardNav.Click += (_, _) => ShowManagement("Keyboard");
        UiSound.SetCue(KeyboardNav, UiSound.Cue.Tab); KeyboardNav.Checked += (_, _) => QueueNavigation();
        NavigationTabs.Children.Insert(NavigationTabs.Children.IndexOf(MacrosNav) + 1, KeyboardNav);
    }

    private void ShowKeyboardPage()
    {
        if (PcMode == null || !Draft.PcMode) ManagementBodyHost.Children.Add(Kit.Text("Switch PC mode on to use the Keyboard tab.", 16, "DS.TextSoft"));
        else { KeyboardPage ??= new KeyboardWorkbench(PcMode, RegisterFocus, Model, layout); ManagementBodyHost.Children.Add(KeyboardPage.Body); }
        ChangeCount.Text = "Keyboard · PC mode · Saved on this PC"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "Keyboard";
    }

    /// <summary>The tab to come back to, in the tab strip's own names.</summary>
    internal string RememberedPage => ManagementPage == "PcMode" && PcDashboard ? "Pc" + PcModePage.Part : CurrentPage;
    /// <summary>The host put remembered settings back into the drafts after this Studio was built: show them, and light
    /// Apply when they differ from what PC mode has saved.</summary>
    internal void DraftsRestored()
    {
        Curves.Refresh(); Macros?.Refresh(); RefreshEditor(); UpdateCurveContext(); UpdateMacroContext(); SyncPcMode();
    }

    internal Panel ManagementBodyHostForReview => ManagementBodyHost;
    internal string[] TabOrderForReview => TabOrder;
}
