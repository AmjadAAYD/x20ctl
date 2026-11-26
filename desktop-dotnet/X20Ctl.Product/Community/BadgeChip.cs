using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public static class BadgeChip
{
    public static readonly Color OwnerOrange = Color.FromRgb(0xFF, 0x7A, 0x18);
    private static readonly ConcurrentDictionary<string, BitmapImage?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);

    internal static string ResolveAssetFile(string? badgeId, string? iconKey = null, string? imageUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            int slash = imageUrl.LastIndexOf('/');
            string file = slash >= 0 ? imageUrl[(slash + 1)..] : imageUrl;
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return file;
        }

        string key = (badgeId ?? iconKey ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "amjad-verified" or "owner" => "badge-owner.png",
            "og-v0026" or "og" => "badge-og-v0026.png",
            "founding-100" or "founding" => "badge-founding-100.png",
            "first-upload" => "badge-first-upload.png",
            "curve-architect" or "curve-craft" => "badge-curve-architect.png",
            "pixel-smith" or "screen-smith" => "badge-screen-smith.png",
            "macro-wizard" or "macro-alchemist" => "badge-macro-wizard.png",
            "light-sculptor" => "badge-light-sculptor.png",
            "bundle-curator" => "badge-bundle-curator.png",
            "pro-tuner" => "badge-pro-tuner.png",
            "master-crafter" => "badge-master-crafter.png",
            "crowd-favorite" => "badge-crowd-favorite.png",
            "hall-of-fame" => "badge-hall-of-fame.png",
            "upvoted-10" or "rising-spark" => "badge-rising-spark.png",
            "upvoted-100" or "community-pillar" => "badge-community-pillar.png",
            "upvoted-500" or "karma-legend" => "badge-karma-legend.png",
            "watcher-magnet" => "badge-watcher-magnet.png",
            "remix-spark" => "badge-remix-spark.png",
            "remix-artist" => "badge-remix-artist.png",
            "verified-tester" or "community-hero" => "badge-community-hero.png",
            "bounty-hunter" => "badge-bounty-hunter.png",
            "challenge-winner" => "badge-challenge-winner.png",
            "night-owl" => "badge-night-owl.png",
            "lounge-regular" => "badge-lounge-regular.png",
            "zero-deadzone" => "badge-zero-deadzone.png",
            "bug-finder" => "badge-bug-finder.png",
            "supporter" => "badge-supporter.png",
            _ => "badge-first-upload.png"
        };
    }

    public static BitmapImage? GetBadgeBitmap(BadgeDef? def, string? badgeId = null)
    {
        string file = ResolveAssetFile(badgeId ?? def?.Id, def?.IconKey, def?.ImageUrl);
        return BitmapCache.GetOrAdd(file, f =>
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/badges/{f}", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        });
    }

    public static FrameworkElement CreateOwnerCheckmark(double size = 16)
        => CreateEmblem(null, "amjad-verified", size, "AmjadXCTL - Verified X20CTL Creator & Owner");

    public static FrameworkElement CreateEmblem(BadgeDef? def, string? badgeId = null, double size = 18, string? tooltip = null)
    {
        var borderColor = ParseColor(def?.BorderHex, OwnerOrange);
        var bgColor = ParseColor(def?.BgHex, Color.FromRgb(0x18, 0x20, 0x33));
        var bmp = GetBadgeBitmap(def, badgeId);

        var holder = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(180, bgColor.R, bgColor.G, bgColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(145, borderColor.R, borderColor.G, borderColor.B)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            ClipToBounds = true,
            ToolTip = tooltip ?? (def != null ? $"{def.Name} ({def.Rarity})\n{def.Description}" : "X20CTL Achievement Badge")
        };

        if (bmp != null)
        {
            var img = new Image
            {
                Source = bmp,
                Width = size - 2,
                Height = size - 2,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            holder.Child = img;
        }
        else
        {
            string initials = string.IsNullOrWhiteSpace(def?.Name) ? "X2" : def.Name[..Math.Min(2, def.Name.Length)].ToUpperInvariant();
            holder.Child = new TextBlock
            {
                Text = initials,
                FontSize = Math.Max(8, size * 0.48),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(borderColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        return holder;
    }

    public static Border? CreateFlair(
        CreatorProfile creator,
        IReadOnlyDictionary<string, BadgeDef> badgeCatalog,
        bool compact = false)
    {
        string? badgeId = !string.IsNullOrWhiteSpace(creator.EquippedBadge)
            ? creator.EquippedBadge
            : (creator.IsOwner ? "amjad-verified" : creator.Badges.Count > 0 ? creator.Badges[0] : null);

        if (string.IsNullOrWhiteSpace(badgeId))
            return null;

        BadgeDef? def = null;
        badgeCatalog.TryGetValue(badgeId, out def);
        if (def == null && badgeId == "amjad-verified")
        {
            def = new BadgeDef
            {
                Id = "amjad-verified",
                Name = "Creator & Owner",
                Description = "Verified creator of X20CTL and X20ADMIN.",
                BgHex = "#2A180D",
                BorderHex = "#FF7A18",
                TextHex = "#FFE3C4",
                Rarity = "mythic"
            };
        }

        if (def == null)
        {
            def = new BadgeDef
            {
                Id = badgeId,
                Name = HumanizeBadgeId(badgeId),
                Description = "Earned community achievement.",
                BgHex = "#1B2234",
                BorderHex = "#60A5FA",
                TextHex = "#DBEAFE",
                Rarity = "rare"
            };
        }

        return Create(def, compact);
    }

    public static Border Create(BadgeDef def, bool compact = false)
    {
        var bg = ParseColor(def.BgHex, Color.FromRgb(0x1B, 0x22, 0x34));
        var border = ParseColor(def.BorderHex, Color.FromRgb(0x60, 0xA5, 0xFA));
        var text = ParseColor(def.TextHex, Color.FromRgb(0xDB, 0xEA, 0xFE));

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var bmp = GetBadgeBitmap(def, def.Id);
        if (bmp != null)
        {
            var img = new Image
            {
                Source = bmp,
                Width = compact ? 15 : 18,
                Height = compact ? 15 : 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            row.Children.Add(img);
        }

        row.Children.Add(new TextBlock
        {
            Text = def.Name,
            FontSize = compact ? 10.5 : 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(text),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Padding = compact ? new Thickness(5, 2, 7, 2) : new Thickness(7, 3, 9, 3),
            Margin = new Thickness(4, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = $"{def.Name} ({def.Rarity.ToUpperInvariant()})\n{def.Description}",
            Child = row
        };
    }

    public static Border CreateAchievementCard(
        BadgeDef def,
        bool unlocked,
        bool equipped,
        CreatorStats? stats,
        Func<string, Task>? onEquipToggle)
    {
        var bg = ParseColor(def.BgHex, Color.FromRgb(0x16, 0x1E, 0x32));
        var border = ParseColor(def.BorderHex, Color.FromRgb(0x52, 0x8B, 0xFF));
        var text = ParseColor(def.TextHex, Color.FromRgb(0xEB, 0xF3, 0xFF));
        var borderBrush = equipped
            ? new SolidColorBrush(border)
            : unlocked
                ? new SolidColorBrush(Color.FromArgb(145, border.R, border.G, border.B))
                : new SolidColorBrush(CommunityUI.BorderSubtle);

        var dock = new DockPanel();

        var emblemFrame = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(unlocked ? (byte)215 : (byte)110, bg.R, bg.G, bg.B)),
            BorderBrush = new SolidColorBrush(unlocked ? border : CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Opacity = unlocked ? 1.0 : 0.42
        };

        var bmp = GetBadgeBitmap(def, def.Id);
        if (bmp != null)
        {
            var img = new Image { Source = bmp, Width = 46, Height = 46, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            emblemFrame.Child = img;
        }
        else
        {
            emblemFrame.Child = Kit.Text(def.Name[..Math.Min(2, def.Name.Length)].ToUpperInvariant(), 16, "DS.Text", FontWeights.Bold)
                .Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; });
        }
        DockPanel.SetDock(emblemFrame, Dock.Left);
        dock.Children.Add(emblemFrame);

        var body = new StackPanel();
        var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
        var rarityTag = CommunityUI.SquareTag(
            def.Rarity.ToUpperInvariant(),
            text,
            Color.FromArgb(75, bg.R, bg.G, bg.B),
            Color.FromArgb(170, border.R, border.G, border.B));
        DockPanel.SetDock(rarityTag, Dock.Right);
        topRow.Children.Add(rarityTag);
        topRow.Children.Add(new TextBlock
        {
            Text = def.Name,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = unlocked ? new SolidColorBrush(Colors.White) : DS.Brush("DS.TextSoft"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });
        body.Children.Add(topRow);

        body.Children.Add(new TextBlock
        {
            Text = def.Description,
            FontSize = 11.5,
            Foreground = DS.Brush(unlocked ? "DS.TextSoft" : "DS.TextMuted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (unlocked)
        {
            var actionRow = new DockPanel();
            var statusTag = Kit.Text(equipped ? "ACTIVE FLAIR" : "UNLOCKED", 10, equipped ? "DS.AccentHi" : "DS.TextMuted", FontWeights.Bold);
            statusTag.VerticalAlignment = VerticalAlignment.Center;
            actionRow.Children.Add(statusTag);

            if (onEquipToggle != null)
            {
                var btn = CommunityUI.RectButton(
                    equipped ? "Unequip" : "Equip Flair",
                    equipped ? RectBtnKind.Primary : RectBtnKind.Secondary,
                    26,
                    11);
                btn.Padding = new Thickness(10, 0, 10, 0);
                btn.HorizontalAlignment = HorizontalAlignment.Right;
                btn.Tag = $"equip:{def.Id}";
                btn.Click += async (_, _) =>
                {
                    btn.IsEnabled = false;
                    try { await onEquipToggle(equipped ? "" : def.Id); }
                    finally { btn.IsEnabled = true; }
                };
                DockPanel.SetDock(btn, Dock.Right);
                actionRow.Children.Add(btn);
            }
            body.Children.Add(actionRow);
        }
        else if (def.RuleThreshold is > 0 && stats != null)
        {
            int cur = Math.Min(def.RuleThreshold.Value, stats.ReadMetric(def.RuleMetric));
            int target = def.RuleThreshold.Value;
            double pct = Math.Clamp(cur / (double)target, 0, 1);

            var progTrack = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(18, 25, 42)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var progGrid = new Grid();
            progGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.02, pct), GridUnitType.Star) });
            progGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 1.0 - pct), GridUnitType.Star) });
            var fill = new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(border)
            };
            progGrid.Children.Add(fill);
            progTrack.Child = progGrid;
            body.Children.Add(progTrack);
            body.Children.Add(Kit.Text($"Locked  {cur} / {target}", 10.5, "DS.TextMuted"));
        }
        else
        {
            body.Children.Add(Kit.Text("Locked achievement", 10.5, "DS.TextMuted"));
        }

        dock.Children.Add(body);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(unlocked ? Color.FromRgb(17, 25, 45) : Color.FromRgb(12, 18, 34)),
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(equipped ? 1.5 : 1),
            Padding = new Thickness(12),
            Child = dock
        };
    }

    private static string HumanizeBadgeId(string id)
    {
        var parts = id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }
        return string.Join(" ", parts);
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
