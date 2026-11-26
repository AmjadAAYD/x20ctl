using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed class CreatorProfileDialog : Grid
{
    private readonly CommunityClient _client;
    private IReadOnlyDictionary<string, BadgeDef> _badgeCatalog;
    private readonly bool _ogWindowOpen;
    private CreatorProfile? _profile;
    private CreatorStats? _stats;
    private bool _statsFetchStarted;
    private readonly TextBlock _statusText = Kit.Text("", 12, "DS.TextSoft");

    public CreatorProfile? CurrentProfile => _profile;

    public event Action<CreatorProfile>? ProfileUpdated;
    public event Action? CloseRequested;

    public CreatorProfileDialog(
        CommunityClient client,
        CreatorProfile? existingProfile,
        IReadOnlyDictionary<string, BadgeDef> badgeCatalog,
        bool ogWindowOpen,
        CreatorStats? stats = null)
    {
        _client = client;
        _profile = existingProfile;
        _badgeCatalog = badgeCatalog;
        _ogWindowOpen = ogWindowOpen;
        _stats = stats;

        Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 16));
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == this) CloseRequested?.Invoke(); };
        BuildContent();

        if (_profile != null && _stats == null)
            _ = LoadStatsInBackgroundAsync(_profile.Uid);
    }

    public void SetStats(CreatorStats? stats)
    {
        _stats = stats;
        BuildContent();
    }

    public async Task EquipBadgeAsync(string badgeId)
    {
        var res = await _client.UpdateProfileAsync(equippedBadge: badgeId);
        if (res.Ok && res.Data != null)
        {
            _profile = res.Data;
            _statusText.Text = string.IsNullOrEmpty(badgeId)
                ? "Active flair cleared."
                : $"Equipped flair: {(_badgeCatalog.TryGetValue(badgeId, out var d) ? d.Name : badgeId)}";
            ProfileUpdated?.Invoke(res.Data);
            BuildContent();
        }
        else
        {
            _statusText.Text = res.Error ?? "Could not update flair.";
        }
    }

    private async Task LoadStatsInBackgroundAsync(string uid)
    {
        if (_statsFetchStarted) return;
        _statsFetchStarted = true;
        var res = await _client.GetProfileWithStatsAsync(uid);
        if (res.Ok)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                _profile = res.Data.Creator;
                _stats = res.Data.Stats;
                if (res.Data.Badges.Count > 0)
                {
                    var dict = new Dictionary<string, BadgeDef>(StringComparer.OrdinalIgnoreCase);
                    foreach (var b in res.Data.Badges) dict[b.Id] = b;
                    _badgeCatalog = dict;
                }
                BuildContent();
            });
        }
    }

    private void BuildContent()
    {
        if (_statusText.Parent is Panel oldStatusParent)
            oldStatusParent.Children.Remove(_statusText);
        Children.Clear();
        var card = new Border
        {
            Width = 680,
            MaxHeight = 740,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            Background = new SolidColorBrush(CommunityUI.BgCard),
            Padding = new Thickness(22)
        };

        var root = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var closeBtn = CommunityUI.RectButton("Close", RectBtnKind.Secondary, 30, 11.5);
        closeBtn.Click += (_, _) => CloseRequested?.Invoke();
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);
        header.Children.Add(Kit.Text(_profile == null ? "Creator Account" : "Creator Achievements & Flair", 18, "DS.Text", FontWeights.Bold));
        root.Children.Add(header);

        var uidRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var copyUidBtn = CommunityUI.RectButton("Copy UID", RectBtnKind.Ghost, 24, 11);
        copyUidBtn.Click += (_, _) => { Clipboard.SetText(_client.Identity.Uid); _statusText.Text = $"Copied {_client.Identity.Uid}"; };
        DockPanel.SetDock(copyUidBtn, Dock.Right);
        uidRow.Children.Add(copyUidBtn);
        uidRow.Children.Add(Kit.Text($"Device User ID: {_client.Identity.Uid}", 12, "DS.TextMuted").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        root.Children.Add(uidRow);

        if (_profile == null)
        {
            if (_ogWindowOpen)
            {
                root.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(45, 255, 122, 24)),
                    BorderBrush = new SolidColorBrush(BadgeChip.OwnerOrange),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 12),
                    Child = Kit.Text("OG V0.026 launch badge is active for new creators.", 12, "DS.Text", FontWeights.SemiBold).Also(t => t.TextWrapping = TextWrapping.Wrap)
                });
            }

            var handleBox = CommunityUI.RectInput("", 36);
            handleBox.Margin = new Thickness(0, 4, 0, 10);

            var ownerPassPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 10) };
            var ownerPassBox = new PasswordBox
            {
                Height = 34,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(CommunityUI.BgInput),
                BorderBrush = new SolidColorBrush(BadgeChip.OwnerOrange),
                BorderThickness = new Thickness(1),
            };
            ownerPassPanel.Children.Add(Kit.Text("Owner Passphrase (required to claim reserved handle)", 11.5, "DS.Warn").Also(t => t.Margin = new Thickness(0, 0, 0, 4)));
            ownerPassPanel.Children.Add(ownerPassBox);

            handleBox.TextChanged += (_, _) =>
            {
                string low = handleBox.Text.Trim().ToLowerInvariant();
                bool looksReserved = low.Contains("amjad") || low.Contains("x20admin") || low is "admin" or "owner";
                ownerPassPanel.Visibility = looksReserved ? Visibility.Visible : Visibility.Collapsed;
            };

            var claimBtn = CommunityUI.RectButton("Claim Handle & Join", RectBtnKind.Primary, 38, 13);
            claimBtn.Click += async (_, _) =>
            {
                claimBtn.IsEnabled = false;
                _statusText.Text = "Verifying identity with X20ADMIN...";
                string? pass = string.IsNullOrWhiteSpace(ownerPassBox.Password) ? null : ownerPassBox.Password;
                var res = await _client.RegisterProfileAsync(handleBox.Text.Trim(), pass);
                claimBtn.IsEnabled = true;
                if (res.Ok && res.Data != null)
                {
                    _profile = res.Data;
                    _statusText.Text = $"Registered as {res.Data.Handle}!";
                    ProfileUpdated?.Invoke(res.Data);
                    BuildContent();
                    _ = LoadStatsInBackgroundAsync(res.Data.Uid);
                }
                else
                {
                    if ((res.Error ?? "").Contains("reserved", StringComparison.OrdinalIgnoreCase))
                        ownerPassPanel.Visibility = Visibility.Visible;
                    _statusText.Text = res.Error ?? "Registration failed.";
                }
            };
            root.Children.Add(Kit.Text("Unique Handle (3-20 chars)", 12.5, "DS.TextSoft"));
            root.Children.Add(handleBox);
            root.Children.Add(ownerPassPanel);
            root.Children.Add(claimBtn);

            var restoreBox = CommunityUI.RectInput("", 34);
            restoreBox.Margin = new Thickness(0, 8, 0, 6);
            var restoreBtn = CommunityUI.RectButton("Restore Recovery Key", RectBtnKind.Secondary, 32, 12);
            restoreBtn.Click += (_, _) =>
            {
                try
                {
                    var restored = CommunityIdentity.RestoreFromRecoveryKey(restoreBox.Text.Trim(), CommunityIdentity.GetDefaultPath());
                    _statusText.Text = $"Restored identity {restored.Uid}. Reopen X20CTL Community to load profile.";
                }
                catch (Exception ex)
                {
                    _statusText.Text = ex.Message;
                }
            };
            root.Children.Add(new Expander
            {
                Header = "Recover existing key (X20K1...)",
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = DS.Brush("DS.TextSoft"),
                Content = new StackPanel { Children = { restoreBox, restoreBtn } }
            });
        }
        else
        {
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            var pfpView = new AnimatedMediaView(60, 60, pixelArt: false, cornerRadius: 8) { Margin = new Thickness(0, 0, 14, 0) };
            pfpView.SetPlaceholder(_profile.Handle[..Math.Min(2, _profile.Handle.Length)].ToUpperInvariant(), BadgeChip.OwnerOrange);
            if (!string.IsNullOrEmpty(_profile.PfpUrl))
            {
                _ = Task.Run(async () =>
                {
                    var bytes = await _client.DownloadAssetAsync(_profile.PfpUrl);
                    if (bytes != null)
                        pfpView.Dispatcher.Invoke(() => pfpView.LoadBytes(bytes));
                });
            }
            topRow.Children.Add(pfpView);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var hRow = new StackPanel { Orientation = Orientation.Horizontal };
            hRow.Children.Add(Kit.Text(_profile.Handle, 17, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            var flair = BadgeChip.CreateFlair(_profile, _badgeCatalog);
            if (flair != null) hRow.Children.Add(flair);
            info.Children.Add(hRow);

            int uploadsCount = _stats?.ApprovedCount ?? _profile.ApprovedCount;
            info.Children.Add(Kit.Text(
                $"Uploads {uploadsCount}  |  Installs {_profile.InstallsReceived}  |  Karma {_profile.Karma}  |  Watchers {_profile.WatchersCount}",
                12,
                "DS.TextSoft").Also(t => t.Margin = new Thickness(0, 3, 0, 6)));

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var uploadPfpBtn = CommunityUI.RectButton("Upload 64x64 Avatar", RectBtnKind.Secondary, 28, 11.5);
            uploadPfpBtn.Margin = new Thickness(0, 0, 8, 0);
            uploadPfpBtn.Click += async (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "PNG or GIF (*.png;*.gif)|*.png;*.gif", Title = "Choose 64x64 PNG or Animated GIF Avatar" };
                if (dlg.ShowDialog() != true) return;
                var bytes = await File.ReadAllBytesAsync(dlg.FileName);
                var declared = dlg.FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "image";
                _statusText.Text = "Disarming and uploading avatar...";
                var res = await _client.UpdateProfileAsync(pfpBytes: bytes, pfpDeclaredType: declared);
                if (res.Ok && res.Data != null)
                {
                    _profile = res.Data;
                    pfpView.LoadBytes(bytes);
                    _statusText.Text = "Avatar updated.";
                    ProfileUpdated?.Invoke(res.Data);
                }
                else
                {
                    _statusText.Text = res.Error ?? "Avatar upload failed.";
                }
            };
            btnRow.Children.Add(uploadPfpBtn);

            var copyRecBtn = CommunityUI.RectButton("Copy Recovery Key", RectBtnKind.Secondary, 28, 11.5);
            copyRecBtn.Click += (_, _) =>
            {
                Clipboard.SetText(_client.Identity.ExportRecoveryKey());
                _statusText.Text = "Recovery Key copied! Keep it private to restore your handle on another PC.";
            };
            btnRow.Children.Add(copyRecBtn);

            info.Children.Add(btnRow);
            topRow.Children.Add(info);
            root.Children.Add(topRow);

            // Reddit Active Flair Preview Strip
            var flairStrip = new DockPanel();
            if (!string.IsNullOrEmpty(_profile.EquippedBadge))
            {
                var clearFlairBtn = CommunityUI.RectButton("Unequip Flair", RectBtnKind.Ghost, 26, 11);
                clearFlairBtn.Click += async (_, _) => await EquipBadgeAsync("");
                DockPanel.SetDock(clearFlairBtn, Dock.Right);
                flairStrip.Children.Add(clearFlairBtn);
            }
            var flairPreview = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            flairPreview.Children.Add(Kit.Text("Active User Flair: ", 12, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            flairPreview.Children.Add(Kit.Text($"@{_profile.Handle}", 12.5, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            if (flair != null)
            {
                var flairClone = BadgeChip.CreateFlair(_profile, _badgeCatalog, compact: true);
                if (flairClone != null) flairPreview.Children.Add(flairClone);
            }
            else
            {
                flairPreview.Children.Add(Kit.Text("  None equipped", 11.5, "DS.TextMuted").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            }
            flairStrip.Children.Add(flairPreview);
            root.Children.Add(CommunityUI.CardSurface(flairStrip, new Thickness(12, 8, 12, 8)).Also(b => b.Margin = new Thickness(0, 0, 0, 14)));

            // Unlocked Achievements Showcase (2-col Reddit Trophy/Achievement grid)
            var unlockedSet = new HashSet<string>(_profile.Badges, StringComparer.OrdinalIgnoreCase);
            var unlockedDefs = new List<BadgeDef>();
            foreach (var bid in _profile.Badges)
            {
                if (_badgeCatalog.TryGetValue(bid, out var d)) unlockedDefs.Add(d);
                else unlockedDefs.Add(new BadgeDef { Id = bid, Name = bid, Description = "Earned achievement." });
            }

            root.Children.Add(Kit.Text($"Unlocked Achievements ({unlockedDefs.Count})", 13, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
            var unlockedGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 14) };
            foreach (var def in unlockedDefs)
            {
                bool isEq = string.Equals(_profile.EquippedBadge, def.Id, StringComparison.OrdinalIgnoreCase);
                var ac = BadgeChip.CreateAchievementCard(def, unlocked: true, equipped: isEq, _stats, EquipBadgeAsync);
                ac.Margin = new Thickness(4);
                unlockedGrid.Children.Add(ac);
            }
            root.Children.Add(unlockedGrid);

            // Locked Achievements Showcase with real CreatorStats progress bars
            var lockedDefs = _badgeCatalog.Values
                .Where(d => !unlockedSet.Contains(d.Id) && !string.Equals(d.Mode, "owner", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (lockedDefs.Count > 0)
            {
                root.Children.Add(Kit.Text($"Locked Achievements ({lockedDefs.Count})", 13, "DS.TextSoft", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
                var lockedGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 10) };
                foreach (var def in lockedDefs)
                {
                    var lc = BadgeChip.CreateAchievementCard(def, unlocked: false, equipped: false, _stats, null);
                    lc.Margin = new Thickness(4);
                    lockedGrid.Children.Add(lc);
                }
                root.Children.Add(lockedGrid);
            }
        }

        _statusText.Margin = new Thickness(0, 10, 0, 0);
        _statusText.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(_statusText);

        card.Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 0) };
        Children.Add(card);
    }
}
