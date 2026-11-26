using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        Width = 380;
        CornerRadius = new CornerRadius(10);
        BorderThickness = new Thickness(1);
        BorderBrush = creator.IsOwner
            ? new SolidColorBrush(BadgeChip.OwnerOrange)
            : new SolidColorBrush(CommunityUI.BorderSubtle);
        Background = new SolidColorBrush(CommunityUI.BgCard);
        Padding = new Thickness(18);

        var root = new StackPanel();
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };

        var pfp = new AnimatedMediaView(52, 52, pixelArt: false, cornerRadius: 8)
        {
            Margin = new Thickness(0, 0, 12, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = creator.IsOwner ? new SolidColorBrush(BadgeChip.OwnerOrange) : new SolidColorBrush(CommunityUI.BorderSubtle)
        };
        if (pfpBytes != null) pfp.LoadBytes(pfpBytes);
        else pfp.SetPlaceholder(GetInitials(creator.Handle), BadgeChip.OwnerOrange);
        DockPanel.SetDock(pfp, Dock.Left);
        top.Children.Add(pfp);

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(Kit.Text(creator.Handle, 16, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        var flair = BadgeChip.CreateFlair(creator, badgeCatalog, compact: true);
        if (flair != null) titleRow.Children.Add(flair);
        nameStack.Children.Add(titleRow);
        nameStack.Children.Add(Kit.Text($"{creator.Uid}  |  {creator.TrustTier.ToUpperInvariant()}", 11.5, "DS.TextMuted").Also(t => t.Margin = new Thickness(0, 3, 0, 0)));
        top.Children.Add(nameStack);
        root.Children.Add(top);

        var statsGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };
        var watchersValueText = Kit.Text(creator.WatchersCount.ToString(), 14, "DS.Text", FontWeights.Bold);
        void AddStat(string label, TextBlock valBlock)
        {
            var box = new Border
            {
                Background = new SolidColorBrush(CommunityUI.BgInput),
                BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6),
                Margin = new Thickness(2)
            };
            valBlock.HorizontalAlignment = HorizontalAlignment.Center;
            var lbl = Kit.Text(label, 10, "DS.TextMuted");
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            box.Child = new StackPanel { Children = { valBlock, lbl } };
            statsGrid.Children.Add(box);
        }
        AddStat("Uploads", Kit.Text(creator.ApprovedCount.ToString(), 14, "DS.Text", FontWeights.Bold));
        AddStat("Installs", Kit.Text(creator.InstallsReceived.ToString(), 14, "DS.Text", FontWeights.Bold));
        AddStat("Karma", Kit.Text(creator.Karma.ToString(), 14, "DS.Text", FontWeights.Bold));
        AddStat("Watchers", watchersValueText);
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
            var watchBtn = CommunityUI.RectButton(creator.IsWatching ? "Watching Maker" : "Watch Maker", creator.IsWatching ? RectBtnKind.Primary : RectBtnKind.Secondary, 32, 12);
            watchBtn.Margin = new Thickness(0, 0, 8, 0);
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
                }
                finally
                {
                    watchBtn.IsEnabled = true;
                }
            };
            actions.Children.Add(watchBtn);
        }

        var viewBtn = CommunityUI.RectButton("View Uploads", RectBtnKind.Secondary, 32, 12);
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
