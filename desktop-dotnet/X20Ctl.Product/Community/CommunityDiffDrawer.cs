using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed class CommunityDiffDrawer : Grid
{
    private readonly CommunityItem _item;
    private readonly Dictionary<string, CheckBox> _sectionChecks = new();
    private DispatcherTimer? _sandboxTimer;

    public event Action<CommunityItem, IReadOnlyList<string>>? InstallSelectedRequested;
    public event Action<CommunityItem, IReadOnlyList<string>>? TestLiveRequested;
    public event Action? RevertLiveRequested;
    public event Action? CloseRequested;

    public CommunityDiffDrawer(CommunityItem item, string currentSummary)
    {
        _item = item;
        Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 16));

        var card = new Border
        {
            MaxWidth = 760,
            MaxHeight = 640,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 96, 165, 250)),
            Background = new SolidColorBrush(Color.FromRgb(10, 16, 30)),
            Padding = new Thickness(24)
        };

        var root = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var closeBtn = new Button { Content = "Close", Style = DS.Style("ActionButton"), Height = 32, MinHeight = 32, Padding = new Thickness(12, 0, 12, 0) };
        closeBtn.Click += (_, _) =>
        {
            StopSandboxTimer();
            CloseRequested?.Invoke();
        };
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);
        header.Children.Add(Kit.Text($"Compare With Mine: {item.Title}", 18, "DS.Text", FontWeights.Bold));
        root.Children.Add(header);

        var cols = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        cols.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new() { Width = new GridLength(14) });
        cols.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });

        var mineBox = MakeSummaryBox("YOUR CURRENT DRAFT", currentSummary);
        cols.Children.Add(mineBox);

        var theirsBox = MakeSummaryBox($"COMMUNITY DROP ({item.ModelId} / {item.ConnectionMode})", FormatItemDetails(item));
        Grid.SetColumn(theirsBox, 2);
        cols.Children.Add(theirsBox);
        root.Children.Add(cols);

        root.Children.Add(Kit.Text("Cherry-Pick Sections to Apply", 13.5, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        var checksWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        foreach (var sec in DetectSections(item))
        {
            var cb = new CheckBox
            {
                Content = sec,
                IsChecked = true,
                Foreground = DS.Brush("DS.Text"),
                Margin = new Thickness(0, 0, 16, 8)
            };
            _sectionChecks[sec] = cb;
            checksWrap.Children.Add(cb);
        }
        root.Children.Add(checksWrap);

        var statusLine = Kit.Text("Preview safely or install selected sections directly into your local draft.", 12.5, "DS.TextSoft");
        statusLine.Margin = new Thickness(0, 0, 0, 12);
        root.Children.Add(statusLine);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var installBtn = new Button
        {
            Content = "Install Selected",
            Style = DS.Style("DS.ConsolePrimary"),
            Height = 38,
            Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(0, 0, 10, 0)
        };
        installBtn.Click += (_, _) =>
        {
            var picked = SelectedSections();
            if (picked.Count == 0) { statusLine.Text = "Pick at least one section first."; return; }
            InstallSelectedRequested?.Invoke(_item, picked);
            CloseRequested?.Invoke();
        };

        var testBtn = new Button
        {
            Content = "Test Live (5s Sandbox)",
            Style = DS.Style("ActionButton"),
            Height = 38,
            Padding = new Thickness(14, 0, 14, 0)
        };
        testBtn.Click += (_, _) =>
        {
            var picked = SelectedSections();
            if (picked.Count == 0) { statusLine.Text = "Pick at least one section first."; return; }
            TestLiveRequested?.Invoke(_item, picked);
            int remaining = 5;
            statusLine.Text = $"Sandbox active ({remaining}s remaining)...";
            testBtn.IsEnabled = false;
            StopSandboxTimer();
            _sandboxTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sandboxTimer.Tick += (_, _) =>
            {
                remaining--;
                if (remaining <= 0)
                {
                    StopSandboxTimer();
                    RevertLiveRequested?.Invoke();
                    statusLine.Text = "Sandbox ended. Your previous draft was restored.";
                    testBtn.IsEnabled = true;
                }
                else
                {
                    statusLine.Text = $"Sandbox active ({remaining}s remaining)...";
                }
            };
            _sandboxTimer.Start();
        };

        actions.Children.Add(installBtn);
        actions.Children.Add(testBtn);
        root.Children.Add(actions);

        card.Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Children.Add(card);
        Unloaded += (_, _) => StopSandboxTimer();
    }

    private void StopSandboxTimer()
    {
        _sandboxTimer?.Stop();
        _sandboxTimer = null;
    }

    private List<string> SelectedSections() =>
        _sectionChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();

    private static Border MakeSummaryBox(string heading, string body)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(110, 16, 26, 48)),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Children =
                {
                    Kit.Text(heading, 11.5, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)),
                    Kit.Text(body, 12.5, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap)
                }
            }
        };
    }

    private static IReadOnlyList<string> DetectSections(CommunityItem item)
    {
        if (item.Category == "curve") return new[] { "Curves & Deadzones", "Analog Studio Tuning" };
        if (item.Category == "screen") return new[] { "OLED Screen Art" };
        if (item.Category == "rgb") return new[] { "RGB Lighting" };
        if (item.Category == "vibration") return new[] { "Vibration Motors" };
        if (item.Category == "macro") return new[] { "Macro Slots" };

        var caps = CommunityCapabilities.GetCapabilitySet(item.ModelId, item.ConnectionMode);
        var list = new List<string> { "Buttons & Remaps", "Curves & Deadzones", "Macros" };
        if (caps.Supports("rgb")) list.Add("RGB Lighting");
        if (caps.Supports("vibration")) list.Add("Vibration Motors");
        return list;
    }

    private static string FormatItemDetails(CommunityItem item)
    {
        var lines = new List<string>
        {
            $"Category: {item.Category.ToUpperInvariant()} (v{item.Version})",
            $"Hardware: {item.ModelId} ({item.ConnectionMode})",
            item.Description
        };
        if (!string.IsNullOrWhiteSpace(item.ShareCode))
            lines.Add($"Share Code: {item.ShareCode[..Math.Min(42, item.ShareCode.Length)]}...");
        if (item.Payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject().Take(8))
                lines.Add($"{prop.Name}: {prop.Value.ValueKind}");
        }
        return string.Join("\n", lines);
    }
}
