using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed class CreatorHoverCard : Border
{
    public event Action<string>? FilterByCreatorRequested;
    public event Func<string, Task<(bool Watching, int WatchersCount)>>? WatchToggled;

    public CreatorHoverCard(
        CreatorProfile creator,
        IReadOnlyDictionary<string, BadgeDef> badgeCatalog,
        byte[]? pfpBytes,
        bool isSelf)
    {
        Width = 340;
        CornerRadius = new CornerRadius(14);
        BorderThickness = new Thickness(1);
        BorderBrush = creator.IsOwner
            ? new SolidColorBrush(BadgeChip.OwnerOrange)
            : new SolidColorBrush(Color.FromArgb(140, 90, 150, 230));
        Background = new SolidColorBrush(Color.FromArgb(245, 10, 16, 30));
        Padding = new Thickness(16);

        var root = new StackPanel();
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };

        var pfp = new AnimatedMediaView(52, 52, pixelArt: false, cornerRadius: 26)
        {
            Margin = new Thickness(0, 0, 12, 0),
            BorderThickness = new Thickness(1.5),
            BorderBrush = creator.IsOwner ? new SolidColorBrush(BadgeChip.OwnerOrange) : DS.Brush("DS.Hairline")
        };
        if (pfpBytes != null) pfp.LoadBytes(pfpBytes);
        else pfp.SetPlaceholder(GetInitials(creator.Handle), BadgeChip.OwnerOrange);
        DockPanel.SetDock(pfp, Dock.Left);
        top.Children.Add(pfp);

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(Kit.Text(creator.Handle, 16, "DS.Text", FontWeights.Bold));
        if (creator.IsOwner) titleRow.Children.Add(BadgeChip.CreateOwnerCheckmark(15));
        nameStack.Children.Add(titleRow);
        nameStack.Children.Add(Kit.Text($"{creator.Uid} | {creator.TrustTier.ToUpperInvariant()}", 11.5, "DS.TextMuted"));
        top.Children.Add(nameStack);
        root.Children.Add(top);

        var statsGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 10) };
        var watchersValueText = Kit.Text(creator.WatchersCount.ToString(), 14, "DS.Text", FontWeights.Bold);
        void AddStat(string label, TextBlock valBlock)
        {
            var box = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 20, 32, 58)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 6, 6, 6),
                Margin = new Thickness(2)
            };
            valBlock.HorizontalAlignment = HorizontalAlignment.Center;
            var lbl = Kit.Text(label, 10, "DS.TextMuted");
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            box.Child = new StackPanel { Children = { valBlock, lbl } };
            statsGrid.Children.Add(box);
        }
        AddStat("Karma", Kit.Text(creator.Karma.ToString(), 14, "DS.Text", FontWeights.Bold));
        AddStat("Watchers", watchersValueText);
        AddStat("Watching", Kit.Text(creator.WatchingCount.ToString(), 14, "DS.Text", FontWeights.Bold));
        AddStat("Installs", Kit.Text(creator.InstallsReceived.ToString(), 14, "DS.Text", FontWeights.Bold));
        root.Children.Add(statsGrid);

        if (creator.Badges.Count > 0)
        {
            var rack = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var bid in creator.Badges.Take(8))
            {
                if (badgeCatalog.TryGetValue(bid, out var def))
                    rack.Children.Add(BadgeChip.Create(def, compact: true));
            }
            root.Children.Add(rack);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        if (!isSelf)
        {
            var watchBtn = new Button
            {
                Content = creator.IsWatching ? "Watching Maker" : "Watch Maker",
                Style = DS.Style(creator.IsWatching ? "DS.ConsolePrimary" : "ActionButton"),
                Height = 34,
                MinHeight = 34,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0)
            };
            watchBtn.Click += async (_, _) =>
            {
                if (WatchToggled == null) return;
                watchBtn.IsEnabled = false;
                try
                {
                    var res = await WatchToggled(creator.Uid);
                    creator.IsWatching = res.Watching;
                    creator.WatchersCount = res.WatchersCount;
                    watchersValueText.Text = res.WatchersCount.ToString();
                    watchBtn.Content = res.Watching ? "Watching Maker" : "Watch Maker";
                    watchBtn.Style = DS.Style(res.Watching ? "DS.ConsolePrimary" : "ActionButton");
                }
                finally
                {
                    watchBtn.IsEnabled = true;
                }
            };
            actions.Children.Add(watchBtn);
        }

        var viewBtn = new Button
        {
            Content = "View Uploads",
            Style = DS.Style("ActionButton"),
            Height = 34,
            MinHeight = 34,
            Padding = new Thickness(12, 0, 12, 0),
            FontSize = 12
        };
        viewBtn.Click += (_, _) => FilterByCreatorRequested?.Invoke(creator.Uid);
        actions.Children.Add(viewBtn);
        root.Children.Add(actions);

        Child = root;
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "X2";
        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}
