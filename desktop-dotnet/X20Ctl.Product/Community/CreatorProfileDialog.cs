using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed class CreatorProfileDialog : Grid
{
    private readonly CommunityClient _client;
    private readonly IReadOnlyDictionary<string, BadgeDef> _badgeCatalog;
    private readonly bool _ogWindowOpen;
    private CreatorProfile? _profile;
    private readonly TextBlock _statusText = Kit.Text("", 12, "DS.TextSoft");

    public event Action<CreatorProfile>? ProfileUpdated;
    public event Action? CloseRequested;

    public CreatorProfileDialog(
        CommunityClient client,
        CreatorProfile? existingProfile,
        IReadOnlyDictionary<string, BadgeDef> badgeCatalog,
        bool ogWindowOpen)
    {
        _client = client;
        _profile = existingProfile;
        _badgeCatalog = badgeCatalog;
        _ogWindowOpen = ogWindowOpen;

        Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 16));
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == this) CloseRequested?.Invoke(); };
        BuildContent();
    }

    private void BuildContent()
    {
        Children.Clear();
        var card = new Border
        {
            Width = 540,
            MaxHeight = 680,
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
        header.Children.Add(Kit.Text("Creator Account", 18, "DS.Text", FontWeights.Bold));
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
            hRow.Children.Add(Kit.Text(_profile.Handle, 17, "DS.Text", FontWeights.Bold));
            if (_profile.IsOwner) hRow.Children.Add(BadgeChip.CreateOwnerCheckmark(15));
            info.Children.Add(hRow);
            info.Children.Add(Kit.Text($"Karma {_profile.Karma}  |  Watchers {_profile.WatchersCount}  |  Watching {_profile.WatchingCount}  |  Installs {_profile.InstallsReceived}", 12, "DS.TextSoft"));

            var uploadPfpBtn = CommunityUI.RectButton("Upload 64x64 Avatar", RectBtnKind.Secondary, 28, 11.5);
            uploadPfpBtn.Margin = new Thickness(0, 6, 0, 0);
            uploadPfpBtn.HorizontalAlignment = HorizontalAlignment.Left;
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
            info.Children.Add(uploadPfpBtn);
            topRow.Children.Add(info);
            root.Children.Add(topRow);

            root.Children.Add(Kit.Text("Equipped Badge", 12, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 4, 0, 6)));
            var badgeCombo = new ComboBox { MinHeight = 32, Margin = new Thickness(0, 0, 0, 10) };
            badgeCombo.Items.Add("(None)");
            foreach (var bid in _profile.Badges) badgeCombo.Items.Add(bid);
            badgeCombo.SelectedItem = string.IsNullOrEmpty(_profile.EquippedBadge) ? "(None)" : _profile.EquippedBadge;
            badgeCombo.SelectionChanged += async (_, _) =>
            {
                if (badgeCombo.SelectedItem is not string chosen) return;
                var next = chosen == "(None)" ? "" : chosen;
                var res = await _client.UpdateProfileAsync(equippedBadge: next);
                if (res.Ok && res.Data != null)
                {
                    _profile = res.Data;
                    _statusText.Text = "Equipped badge updated.";
                    ProfileUpdated?.Invoke(res.Data);
                }
            };
            root.Children.Add(badgeCombo);

            var rack = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var bid in _profile.Badges)
            {
                if (_badgeCatalog.TryGetValue(bid, out var def))
                    rack.Children.Add(BadgeChip.Create(def));
            }
            root.Children.Add(rack);

            var copyRecBtn = CommunityUI.RectButton("Copy Recovery Key (X20K1...)", RectBtnKind.Secondary, 30, 11.5);
            copyRecBtn.HorizontalAlignment = HorizontalAlignment.Left;
            copyRecBtn.Click += (_, _) =>
            {
                Clipboard.SetText(_client.Identity.ExportRecoveryKey());
                _statusText.Text = "Recovery Key copied! Keep it private to restore your handle on another PC.";
            };
            root.Children.Add(copyRecBtn);
        }

        _statusText.Margin = new Thickness(0, 10, 0, 0);
        _statusText.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(_statusText);

        card.Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Children.Add(card);
    }
}
