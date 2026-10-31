using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private PcModePage? hybridPcSource;
    private void HybridPcModePage()
    {
        if (hybridPcMode == null) return;
        var original = ManagementBodyHost.Children.OfType<ScrollViewer>().FirstOrDefault();
        var source = original?.Content as PcModePage;
        if (source == null && hybridPcSource != null && hybridPcMode.Children.Count > 0) return;
        if (source == null) return;
        if (ReferenceEquals(source, hybridPcSource) && hybridPcMode.Children.Count > 0) return;
        hybridPcSource = source; original!.Content = null; hybridPcMode.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridPcMode.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0);
        HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, HText("PC mode", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14);
        HAt(board, HText("Route controller input through your PC profile.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "pcmode", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        var route = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string label in new[] { "Controller", "PC profile", "Virtual controller", "Game" })
        {
            var block = new Border { Width = 250, Height = 58, CornerRadius = new(14), BorderBrush = HBrush("#52dcff"), BorderThickness = new(2), Background = HBrush("#2d2933"), Margin = new(0, 0, 18, 0), Child = HText(label, 16, "#f5f1fa", FontWeights.Bold) };
            ((TextBlock)block.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)block.Child).VerticalAlignment = VerticalAlignment.Center; route.Children.Add(block);
        }
        HAt(board, route, 216, 102);
        // Keep the native PC-mode page and all its existing status, consent and settings handlers.
        source.Margin = new(0);
        var settings = new StackPanel { Width = 588, Tag = "hybrid:pc:settings" }; HAt(board, settings, 216, 226);
        var status = HText("", 18, "#f5f1fa", FontWeights.Bold); status.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = source.HybridStatus }); settings.Children.Add(status);
        var statusDetail = HText("", 14, "#bdb4c8", FontWeights.Normal); statusDetail.TextWrapping = TextWrapping.Wrap; statusDetail.Margin = new(0, 4, 0, 14); statusDetail.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = source.HybridStatusDetail }); settings.Children.Add(statusDetail);
        var switchRow = new DockPanel { Margin = new(0, 0, 0, 14), Height = 58, Background = HBrush("#29242f") };
        var switchLabel = HText("PC mode", 17, "#f5f1fa", FontWeights.Bold); switchLabel.Margin = new(16, 0, 0, 0); switchLabel.VerticalAlignment = VerticalAlignment.Center; switchRow.Children.Add(switchLabel);
        var toggle = new CheckBox { Style = DS.Style("DS.Toggle"), Margin = new(12), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Tag = "hybrid:pc:switch" };
        toggle.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = source.Switch, Mode = BindingMode.TwoWay }); switchRow.Children.Add(toggle); settings.Children.Add(switchRow);
        var profileLabel = HText("Settings for", 16, "#f5f1fa", FontWeights.Bold); settings.Children.Add(profileLabel);
        var profile = new ComboBox { DisplayMemberPath = "Label", SelectedValuePath = "Id", Width = 400, Height = 42, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 8, 0, 16), Tag = "hybrid:pc:profile" };
        profile.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(ItemsControl.ItemsSource)) { Source = source.HybridProfiles });
        profile.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(ComboBox.SelectedValue)) { Source = source.HybridProfiles, Mode = BindingMode.TwoWay }); settings.Children.Add(profile);
        var openSettings = HybridButton("More PC settings", "hybrid:pc:more", false, 200, 42); settings.Children.Add(openSettings);
        var scroll = new ScrollViewer { Width = 588, Height = 570, Content = source, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed, Tag = "hybrid:pc:workbench" };
        HAt(board, scroll, 216, 226);
        var backSettings = HybridButton("Back to PC overview", "hybrid:pc:overview", false, 200, 42); backSettings.Visibility = Visibility.Collapsed; HAt(board, backSettings, 216, 760); Panel.SetZIndex(backSettings, 3);
        void ShowSettings(bool show) { settings.Visibility = show ? Visibility.Collapsed : Visibility.Visible; scroll.Visibility = show ? Visibility.Visible : Visibility.Collapsed; backSettings.Visibility = show ? Visibility.Visible : Visibility.Collapsed; }
        openSettings.Click += (_, _) => ShowSettings(true); backSettings.Click += (_, _) => { if (!source.NoticeShown) ShowSettings(false); };
        source.Watch(() => { backSettings.IsEnabled = !source.NoticeShown; if (source.NoticeShown && scroll.Visibility != Visibility.Visible) { ShowSettings(true); Dispatcher.BeginInvoke(source.HybridRevealNotice, System.Windows.Threading.DispatcherPriority.Loaded); } });
        backSettings.IsEnabled = !source.NoticeShown;
        if (source.NoticeShown) { ShowSettings(true); Dispatcher.BeginInvoke(source.HybridRevealNotice, System.Windows.Threading.DispatcherPriority.Loaded); }
        HAt(board, new Rectangle { Width = 2, Height = 560, Fill = HBrush("#52dcff") }, 846, 226);
        var detail = new StackPanel { Width = 444, Margin = new(24) };
        detail.Children.Add(HText("How PC mode works", 25, "#f5f1fa", FontWeights.ExtraBold));
        foreach (var text in new[] {
            "1   Connect your controller and choose a player.",
            "2   Turn on PC mode when its required drivers are ready.",
            "3   Edit buttons, curves and macros in this controller's Studio.",
            "4   Games receive the virtual controller's input."
        }) { var line = HText(text, 16, "#d9d3e3", FontWeights.Normal); line.Margin = new(0, 18, 0, 0); detail.Children.Add(line); }
        var note = HText("Changes apply on this PC. Nothing is written to your controller.", 14, "#bdb4c8", FontWeights.SemiBold); note.Margin = new(0, 26, 0, 0); detail.Children.Add(note);
        HAt(board, new Border { Width = 492, Height = 548, CornerRadius = new(22), Background = HBrush("#24212b"), Child = detail }, 892, 226);
    }
}
