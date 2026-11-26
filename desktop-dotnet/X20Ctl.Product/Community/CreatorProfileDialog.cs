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
    private readonly TextBlock _statusText = Kit.Text("", 12.5, "DS.TextSoft");

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

        Background = new SolidColorBrush(Color.FromArgb(200, 3, 6, 16));
        BuildContent();
    }

    private void BuildContent()
    {
        Children.Clear();
        var card = new Border
        {
            MaxWidth = 640,
            MaxHeight = 680,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 100, 160, 240)),
            Background = new SolidColorBrush(Color.FromRgb(10, 16, 30)),
            Padding = new Thickness(24)
        };

        var root = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var closeBtn = new Button { Content = "Close", Style = DS.Style("ActionButton"), Height = 32, MinHeight = 32, Padding = new Thickness(12, 0, 12, 0) };
        closeBtn.Click += (_, _) => CloseRequested?.Invoke();
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);
        header.Children.Add(Kit.Text("Creator Identity & Badges", 20, "DS.Text", FontWeights.Bold));
        root.Children.Add(header);

        root.Children.Add(Kit.Text($"Device User ID: {_client.Identity.Uid}", 12.5, "DS.TextMuted").Also(t => t.Margin = new Thickness(0, 0, 0, 12)));

        if (_profile == null)
        {
            if (_ogWindowOpen)
            {
                root.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(60, 255, 122, 24)),
                    BorderBrush = new SolidColorBrush(BadgeChip.OwnerOrange),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 12),
                    Child = Kit.Text("OG V0.026 launch window is OPEN! Claim your handle now to unlock permanent OG V0.026 status.", 12.5, "DS.Text", FontWeights.SemiBold).Also(t => t.TextWrapping = TextWrapping.Wrap)
                });
            }

            var handleBox = new TextBox { Padding = new Thickness(10, 8, 10, 8), FontSize = 14, Margin = new Thickness(0, 4, 0, 10) };
            var claimBtn = new Button { Content = "Claim Unique Handle & Join", Style = DS.Style("DS.ConsolePrimary"), Height = 40 };
            claimBtn.Click += async (_, _) =>
            {
                claimBtn.IsEnabled = false;
                _statusText.Text = "Solving Proof-of-Work and registering ECDSA P-256 identity...";
                var res = await _client.RegisterProfileAsync(handleBox.Text.Trim());
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
                    _statusText.Text = res.Error ?? "Registration failed.";
                }
            };
            root.Children.Add(Kit.Text("Choose Unique Handle (3-20 chars)", 13, "DS.TextSoft"));
            root.Children.Add(handleBox);
            root.Children.Add(claimBtn);

            var restoreBox = new TextBox { Padding = new Thickness(10, 7, 10, 7), FontSize = 12, Margin = new Thickness(0, 8, 0, 6) };
            var restoreBtn = new Button { Content = "Restore Recovery Key (X20K1...)", Style = DS.Style("ActionButton"), Height = 34 };
            restoreBtn.Click += (_, _) =>
            {
                try
                {
                    var restored = CommunityIdentity.RestoreFromRecoveryKey(restoreBox.Text.Trim(), CommunityIdentity.GetDefaultPath());
                    _statusText.Text = $"Restored identity {restored.Uid}. Restart or reopen Community to load profile.";
                }
                catch (Exception ex)
                {
                    _statusText.Text = ex.Message;
                }
            };
            root.Children.Add(new Expander
            {
                Header = "Already have a Recovery Key?",
                Margin = new Thickness(0, 14, 0, 0),
                Foreground = DS.Brush("DS.TextSoft"),
                Content = new StackPanel { Children = { restoreBox, restoreBtn } }
            });
        }
        else
        {
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            var pfpView = new AnimatedMediaView(64, 64, pixelArt: false, cornerRadius: 32) { Margin = new Thickness(0, 0, 14, 0) };
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
            hRow.Children.Add(Kit.Text(_profile.Handle, 18, "DS.Text", FontWeights.Bold));
            if (_profile.IsOwner) hRow.Children.Add(BadgeChip.CreateOwnerCheckmark(16));
            info.Children.Add(hRow);
            info.Children.Add(Kit.Text($"Karma {_profile.Karma} | Watchers {_profile.WatchersCount} | Watching {_profile.WatchingCount} | Installs {_profile.InstallsReceived}", 12.5, "DS.TextSoft"));

            var uploadPfpBtn = new Button { Content = "Upload 64x64 PNG/GIF PFP", Style = DS.Style("ActionButton"), Height = 30, MinHeight = 30, FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            uploadPfpBtn.Click += async (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "PNG or GIF (*.png;*.gif)|*.png;*.gif", Title = "Choose 64x64 PNG or Animated GIF PFP" };
                if (dlg.ShowDialog() != true) return;
                var bytes = await File.ReadAllBytesAsync(dlg.FileName);
                var declared = dlg.FileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "image";
                _statusText.Text = "Disarming and scanning avatar...";
                var res = await _client.UpdateProfileAsync(pfpBytes: bytes, pfpDeclaredType: declared);
                if (res.Ok && res.Data != null)
                {
                    _profile = res.Data;
                    pfpView.LoadBytes(bytes);
                    _statusText.Text = "Profile picture updated!";
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

            root.Children.Add(Kit.Text("Equipped Badge (server-verified earned badges only)", 12.5, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 4, 0, 6)));
            var badgeCombo = new ComboBox { MinHeight = 34, Margin = new Thickness(0, 0, 0, 8) };
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
                    _statusText.Text = "Equipped badge saved.";
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

            var copyRecBtn = new Button { Content = "Copy Recovery Key (X20K1...)", Style = DS.Style("ActionButton"), Height = 32, MinHeight = 32, HorizontalAlignment = HorizontalAlignment.Left };
            copyRecBtn.Click += (_, _) =>
            {
                Clipboard.SetText(_client.Identity.ExportRecoveryKey());
                _statusText.Text = "Recovery Key copied! Store it privately to recover your handle on another PC.";
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
