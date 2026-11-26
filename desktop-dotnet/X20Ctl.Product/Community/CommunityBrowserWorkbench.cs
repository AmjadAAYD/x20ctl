using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed class CommunityBrowserWorkbench
{
    private readonly CommunityClient _client;
    private readonly string _hostModelId;
    private readonly string _hostConnectionMode;
    private readonly Func<string> _currentDraftSummary;
    private readonly Func<string, string, (object? Payload, string? ShareCode, byte[]? MediaBytes, string? MediaType)>? _exportCurrentForSubmit;

    public Grid Body { get; } = new();

    private string _selectedModelId;
    private string _selectedMode;
    private string _subTab = "explore";
    private string _categoryFilter = "all";
    private string _mediaFilter = "all";
    private string _sort = "trending";
    private string _searchQuery = "";
    private string? _filterUid;
    private bool _watchingOnly;
    private string? _gameTagFilter;
    private string _leaderboardMetric = "karma";
    private bool _ogWindowOpen = true;
    private CreatorProfile? _myProfile;
    private CreatorStats? _myStats;
    private List<NotificationItem> _notifications = new();
    private int _unreadCount;
    private readonly HashSet<string> _toastedBadgeNotifIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BadgeDef> _badgeMap = new(StringComparer.OrdinalIgnoreCase);

    // Client-side memory cache so switching tabs renders in 0 ms before background sync
    private string? _cachedCatalogKey;
    private CatalogResponse? _cachedCatalog;
    private (List<CommunityItem> Saved, List<CommunityItem> Mine)? _cachedSavedMine;
    private List<CommunityRequest>? _cachedRequests;
    private (List<LoungeMessage> Messages, int SlowSec, bool Paused)? _cachedLounge;
    private (List<CreatorProfile> Makers, List<CreatorProfile> Watchers, List<CreatorProfile> Watching)? _cachedLeaderboard;

    public string CurrentSubTab => _subTab;
    public CommunitySubmitDialog? ActiveSubmitDialog { get; private set; }
    public CommunityDiffDrawer? ActiveDiffDrawer { get; private set; }
    public Grid ActiveModalContent => _modalHost;
    private Button _publishBtn = null!;
    private Button _alertsBtn = null!;
    private readonly Border _toastBanner = new()
    {
        Visibility = Visibility.Collapsed,
        CornerRadius = new CornerRadius(6),
        Background = new SolidColorBrush(Color.FromArgb(235, 16, 28, 52)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(14, 10, 14, 10),
        Margin = new Thickness(0, 0, 18, 18),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        MaxWidth = 360
    };
    private readonly TextBlock _toastText = Kit.Text("", 12, "DS.Text", FontWeights.SemiBold);
    public CatalogResponse? LastCatalog => _cachedCatalog;
    public List<CreatorProfile>? LastLeaderboardMakers => _cachedLeaderboard?.Makers;

    // Top bar elements
    private readonly StackPanel _navTabHost = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly Border _presenceDot = new() { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _onlineText = Kit.Text("0 online", 12, "DS.TextSoft", FontWeights.SemiBold);
    private readonly StackPanel _profileBtnContent = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _profileLabel = Kit.Text("Sign In", 12, "DS.Text", FontWeights.SemiBold);

    // Body content regions
    private readonly Grid _workspaceHost = new();
    private readonly StackPanel _sidebarStack = new();
    private readonly StackPanel _mainFeedStack = new();
    private readonly ScrollViewer _mainScroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Grid _modalHost = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _statusLine = Kit.Text("Ready", 11.5, "DS.TextMuted");
    private readonly TextBox _searchBox;
    private readonly ComboBox _modelPicker = new() { MinHeight = 34, Padding = new Thickness(10, 4, 10, 4), FontSize = 12.5, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _modePicker = new() { MinHeight = 34, Padding = new Thickness(10, 4, 10, 4), FontSize = 12.5, VerticalContentAlignment = VerticalAlignment.Center };
    private bool _syncingSelectors;

    public event Action<CommunityItem, IReadOnlyList<string>>? InstallItemRequested;
    public event Action<CommunityItem, IReadOnlyList<string>>? TestLiveRequested;
    public event Action? RevertLiveRequested;

    public CommunityBrowserWorkbench(
        CommunityClient client,
        string hostModelId,
        string hostConnectionMode,
        Func<string> currentDraftSummary,
        Func<string, string, (object? Payload, string? ShareCode, byte[]? MediaBytes, string? MediaType)>? exportCurrentForSubmit = null)
    {
        _client = client;
        _hostModelId = CommunityCapabilities.NormalizeModelId(hostModelId);
        _hostConnectionMode = hostConnectionMode == "native" && CommunityCapabilities.GetModel(_hostModelId)?.SupportsNative == true
            ? "native" : "pc-mode";
        _selectedModelId = _hostModelId;
        _selectedMode = _hostConnectionMode;
        _currentDraftSummary = currentDraftSummary;
        _exportCurrentForSubmit = exportCurrentForSubmit;

        _searchBox = CommunityUI.RectInput("", 32);
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _searchQuery = _searchBox.Text.Trim();
                _ = RefreshAsync();
            }
        };

        BuildShell();
    }

    public void SetPresetCategoryAndRefresh(string category)
    {
        _subTab = "explore";
        _categoryFilter = category;
        _ = RefreshAsync();
    }

    public void OpenShareDialog(
        string category,
        string? title = null,
        object? payload = null,
        string? shareCode = null,
        byte[]? mediaBytes = null,
        string? mediaType = null,
        string? remixSourceId = null,
        string? fulfillRequestId = null,
        string? parentItemId = null,
        IReadOnlyList<string>? initialGameTags = null)
    {
        if (_client.IsNetworkDisabled)
        {
            _statusLine.Text = "Disable Offline Mode in Tools to publish.";
            return;
        }
        if (_myProfile == null)
        {
            OpenProfileDialog();
            return;
        }
        var dlg = new CommunitySubmitDialog(
            _client, category, _selectedModelId, _selectedMode,
            title, "", payload, shareCode, mediaBytes, mediaType, remixSourceId, fulfillRequestId,
            exportForCategory: cat => _exportCurrentForSubmit != null
                ? _exportCurrentForSubmit(cat, _selectedMode)
                : (payload, shareCode, mediaBytes, mediaType),
            parentItemId: parentItemId,
            initialGameTags: initialGameTags);
        dlg.CloseRequested += CloseModal;
        dlg.Submitted += item =>
        {
            CloseModal();
            _statusLine.Text = item.Status == "approved"
                ? $"Published '{item.Title}' live!"
                : $"Submitted '{item.Title}' to X20ADMIN queue.";
            _ = RefreshAsync();
        };
        ActiveSubmitDialog = dlg;
        ShowModal(dlg);
    }

    private void UpdatePublishButtonLabel()
    {
        if (_publishBtn == null) return;
        _publishBtn.Content = _categoryFilter switch
        {
            "screen" => "Publish Screen Art",
            "curve" => "Publish Curve",
            "rgb" => "Publish RGB",
            "vibration" => "Publish Vibration",
            "macro" => "Publish Macro",
            "pack" => "Publish Pack",
            _ => "Publish Setup"
        };
    }

    private void BuildShell()
    {
        Body.Margin = new Thickness(26, 4, 26, 14);
        Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Single Top Navigation Bar
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Top Bar: Steam / Modrinth flat underline tabs on Left, status + Account + Publish on Right
        var topDock = new DockPanel();
        var rightBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(rightBar, Dock.Right);

        var onlineWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        _onlineText.VerticalAlignment = VerticalAlignment.Center;
        onlineWrap.Children.Add(_presenceDot);
        onlineWrap.Children.Add(_onlineText);
        rightBar.Children.Add(onlineWrap);

        _alertsBtn = CommunityUI.RectButton("Alerts", RectBtnKind.Secondary, 32, 12);
        _alertsBtn.Tag = "community:alerts";
        _alertsBtn.Margin = new Thickness(0, 0, 8, 0);
        _alertsBtn.Click += (_, _) => OpenNotificationsModal();
        rightBar.Children.Add(_alertsBtn);

        var profileBtn = CommunityUI.RectButton("", RectBtnKind.Secondary, 32, 12);
        profileBtn.Content = _profileBtnContent;
        profileBtn.Margin = new Thickness(0, 0, 8, 0);
        _profileBtnContent.Children.Add(_profileLabel);
        profileBtn.Click += (_, _) => OpenProfileDialog();
        rightBar.Children.Add(profileBtn);

        _publishBtn = CommunityUI.RectButton("Publish Setup", RectBtnKind.Primary, 32, 12);
        _publishBtn.Tag = "community:publish";
        _publishBtn.Click += (_, _) =>
        {
            var cat = _categoryFilter == "all" ? "setup" : _categoryFilter;
            var exp = _exportCurrentForSubmit?.Invoke(cat, _selectedMode);
            OpenShareDialog(cat, payload: exp?.Payload, shareCode: exp?.ShareCode, mediaBytes: exp?.MediaBytes, mediaType: exp?.MediaType);
        };
        rightBar.Children.Add(_publishBtn);

        topDock.Children.Add(rightBar);
        topDock.Children.Add(_navTabHost);

        var headerBar = new Border
        {
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 8),
            Margin = new Thickness(0, 0, 0, 14),
            Child = topDock
        };
        Body.Children.Add(headerBar);

        InitHardwareSelectors();

        Grid.SetRow(_workspaceHost, 1);
        Body.Children.Add(_workspaceHost);

        _toastText.TextWrapping = TextWrapping.Wrap;
        _toastBanner.Child = _toastText;
        Grid.SetRow(_toastBanner, 1);
        Panel.SetZIndex(_toastBanner, 40);
        Body.Children.Add(_toastBanner);

        Grid.SetRowSpan(_modalHost, 2);
        Panel.SetZIndex(_modalHost, 50);
        Body.Children.Add(_modalHost);

        RebuildTopTabs();
        Body.Loaded += (_, _) => _ = RefreshAsync();
    }

    private void InitHardwareSelectors()
    {
        _syncingSelectors = true;
        foreach (var m in CommunityCapabilities.Models) _modelPicker.Items.Add(m.Id);
        _modelPicker.SelectedItem = _selectedModelId;
        _modePicker.Items.Add("native");
        _modePicker.Items.Add("pc-mode");
        _modePicker.SelectedItem = _selectedMode;
        _syncingSelectors = false;

        _modelPicker.SelectionChanged += (_, _) =>
        {
            if (_syncingSelectors || _modelPicker.SelectedItem is not string m) return;
            _selectedModelId = m;
            var info = CommunityCapabilities.GetModel(m);
            if (info?.SupportsNative != true && _selectedMode == "native")
            {
                _syncingSelectors = true;
                _selectedMode = "pc-mode";
                _modePicker.SelectedItem = "pc-mode";
                _syncingSelectors = false;
            }
            _ = RefreshAsync();
        };

        _modePicker.SelectionChanged += (_, _) =>
        {
            if (_syncingSelectors || _modePicker.SelectedItem is not string mode) return;
            var info = CommunityCapabilities.GetModel(_selectedModelId);
            if (mode == "native" && info?.SupportsNative != true)
            {
                _syncingSelectors = true;
                _modePicker.SelectedItem = "pc-mode";
                _syncingSelectors = false;
                _statusLine.Text = $"{info?.DisplayName ?? _selectedModelId} supports PC Mode only.";
                return;
            }
            _selectedMode = mode;
            _ = RefreshAsync();
        };
    }

    private void RebuildTopTabs()
    {
        _navTabHost.Children.Clear();
        foreach (var (id, label) in new (string, string)[]
        {
            ("explore", "Browse"),
            ("saved", "Saved & Mine"),
            ("requests", "Requests"),
            ("lounge", "Lounge"),
            ("leaderboard", "Leaderboard"),
        })
        {
            var captured = id;
            var tab = CommunityUI.UnderlineTabButton(label, _subTab == id);
            tab.Click += (_, _) =>
            {
                SelectSubTab(captured);
            };
            _navTabHost.Children.Add(tab);
        }
    }

    public void SelectSubTab(string subTabId)
    {
        _subTab = subTabId;
        _filterUid = null;
        RebuildTopTabs();
        RenderCachedSubTabSync();
        _ = RefreshAsync();
    }

    private void RenderCachedSubTabSync()
    {
        if (_subTab == "explore" && _cachedCatalog != null && _cachedCatalogKey == CurrentCatalogCacheKey())
        {
            var caps = CommunityCapabilities.GetCapabilitySet(_selectedModelId, _selectedMode);
            RenderExploreContent(caps, _cachedCatalog);
        }
        else if (_subTab == "saved" && _cachedSavedMine is { } sm)
        {
            RenderSavedAndMineContent(sm.Saved, sm.Mine);
        }
        else if (_subTab == "requests" && _cachedRequests != null)
        {
            RenderRequestsContent(_cachedRequests);
        }
        else if (_subTab == "lounge" && _cachedLounge is { } l)
        {
            RenderLoungeContent(l.Messages, l.SlowSec, l.Paused);
        }
        else if (_subTab == "leaderboard" && _cachedLeaderboard is { } lb)
        {
            RenderLeaderboardContent(lb.Makers, lb.Watchers, lb.Watching);
        }
    }

    private string CurrentCatalogCacheKey()
        => $"{_selectedModelId}|{_selectedMode}|{_categoryFilter}|{_mediaFilter}|{_sort}|{_searchQuery}|{_filterUid}|{_watchingOnly}|{_gameTagFilter}";

    public void ApplyPulse(PulseResponse pulse)
    {
        if (!_client.IsNetworkDisabled)
            _onlineText.Text = $"{pulse.OnlineCount} online";
        _unreadCount = pulse.UnreadCount;
        _notifications = pulse.Notifications ?? new List<NotificationItem>();
        if (_alertsBtn != null)
        {
            _alertsBtn.Content = _unreadCount > 0 ? $"Alerts ({_unreadCount})" : "Alerts";
            _alertsBtn.BorderBrush = _unreadCount > 0
                ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
                : new SolidColorBrush(Color.FromRgb(38, 55, 88));
        }

        var badgeNote = _notifications.FirstOrDefault(n => !n.IsRead && string.Equals(n.Kind, "badge", StringComparison.OrdinalIgnoreCase) && !_toastedBadgeNotifIds.Contains(n.Id));
        if (badgeNote != null)
        {
            _toastedBadgeNotifIds.Add(badgeNote.Id);
            ShowAchievementToast($"Achievement Unlocked: {badgeNote.Title} - {badgeNote.Body}");
        }
    }

    private async void ShowAchievementToast(string message)
    {
        _toastText.Text = message;
        _toastBanner.Visibility = Visibility.Visible;
        CommunityUI.AnimateModalIn(_toastBanner);
        await Task.Delay(4000);
        if (_toastText.Text == message)
            _toastBanner.Visibility = Visibility.Collapsed;
    }

    private void OpenNotificationsModal()
    {
        var modalRoot = new StackPanel();
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var closeBtn = CommunityUI.RectButton("Close", RectBtnKind.Secondary, 30, 11.5);
        closeBtn.Click += (_, _) => CloseModal();
        DockPanel.SetDock(closeBtn, Dock.Right);
        top.Children.Add(closeBtn);
        top.Children.Add(Kit.Text($"Community Alerts ({_unreadCount} unread)", 17, "DS.Text", FontWeights.Bold));
        modalRoot.Children.Add(top);

        var markBtn = CommunityUI.RectButton("Mark All Read", RectBtnKind.Primary, 30, 11.5);
        markBtn.Tag = "alerts:markread";
        markBtn.HorizontalAlignment = HorizontalAlignment.Left;
        markBtn.Margin = new Thickness(0, 0, 0, 12);
        markBtn.Click += async (_, _) =>
        {
            await _client.MarkNotificationsReadAsync();
            foreach (var n in _notifications) n.IsRead = true;
            _unreadCount = 0;
            if (_alertsBtn != null) _alertsBtn.Content = "Alerts";
            CloseModal();
        };
        if (_notifications.Count > 0) modalRoot.Children.Add(markBtn);

        if (_notifications.Count == 0)
        {
            modalRoot.Children.Add(CommunityUI.CardSurface(Kit.Text("No alerts yet. Unlocks, upvotes, and watcher updates appear here.", 12.5, "DS.TextMuted"), new Thickness(14)));
        }
        else
        {
            foreach (var n in _notifications.Take(20))
            {
                var row = new StackPanel();
                row.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        CommunityUI.SquareTag(n.Kind.ToUpperInvariant(), n.IsRead ? null : Colors.White, n.IsRead ? null : Color.FromArgb(95, 245, 158, 11)),
                        Kit.Text(n.Title, 13, "DS.Text", n.IsRead ? FontWeights.Normal : FontWeights.Bold)
                    }
                });
                row.Children.Add(Kit.Text(n.Body, 12, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 0); }));
                modalRoot.Children.Add(CommunityUI.CardSurface(row, new Thickness(10), n.IsRead ? null : Color.FromRgb(245, 158, 11)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }

        var sheet = new Border
        {
            Width = 520,
            MaxHeight = 580,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(CommunityUI.BgCard),
            Padding = new Thickness(20),
            Child = new ScrollViewer { Content = modalRoot, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        CommunityUI.AnimateModalIn(sheet);
        var backdrop = new Grid { Background = new SolidColorBrush(Color.FromArgb(200, 3, 6, 16)), Children = { sheet } };
        backdrop.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == backdrop) CloseModal(); };
        ShowModal(backdrop);
    }

    public async Task RefreshAsync()
    {
        RebuildTopTabs();
        RenderCachedSubTabSync();
        if (_client.IsNetworkDisabled)
        {
            _presenceDot.Background = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            _onlineText.Text = ReviewSandbox.Active ? "Offline Preview" : "Offline";
            _statusLine.Text = ReviewSandbox.Active ? "Offline sandbox cache active." : "Offline Mode active in Tools.";
        }
        else
        {
            _presenceDot.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            _statusLine.Text = "Syncing catalog...";
        }

        try
        {
            var profRes = await _client.GetProfileWithStatsAsync(_client.Identity.Uid);
            if (profRes.Ok)
            {
                _myProfile = profRes.Data.Creator;
                _myStats = profRes.Data.Stats;
                foreach (var b in profRes.Data.Badges) _badgeMap[b.Id] = b;
                UpdateProfileButton();
            }

            if (!_client.IsNetworkDisabled)
            {
                var pRes = await _client.GetPulseAsync();
                if (pRes.Ok && pRes.Data != null && !pRes.Data.NotModified)
                    ApplyPulse(pRes.Data);
            }

            if (_subTab == "explore")
                await RenderExploreWorkshopAsync();
            else if (_subTab == "requests")
                await RenderRequestsBoardAsync();
            else if (_subTab == "lounge")
                await RenderLoungeBoardAsync();
            else if (_subTab == "saved")
                await RenderSavedAndMineAsync();
            else
                await RenderLeaderboardAndWatchingAsync();
        }
        catch (Exception ex)
        {
            _statusLine.Text = $"Sync failed: {ex.Message}";
            SetSingleColumnContent(new StackPanel
            {
                Children =
                {
                    CommunityUI.CardSurface(new StackPanel
                    {
                        Children =
                        {
                            Kit.Text("Community Feed Unavailable", 15, "DS.Text", FontWeights.Bold),
                            Kit.Text(ex.Message, 12.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 6, 0, 12); }),
                            CommunityUI.RectButton("Retry Connection", RectBtnKind.Primary, 32, 12).Also(b => { b.HorizontalAlignment = HorizontalAlignment.Left; b.Click += (_, _) => _ = RefreshAsync(); })
                        }
                    }, new Thickness(18))
                }
            });
        }
    }

    private void UpdateProfileButton()
    {
        _profileBtnContent.Children.Clear();
        if (_myProfile == null)
        {
            _profileLabel.Text = "Sign In";
            _profileBtnContent.Children.Add(_profileLabel);
            return;
        }
        _profileLabel.Text = _myProfile.Handle;
        _profileBtnContent.Children.Add(_profileLabel);
        var flair = BadgeChip.CreateFlair(_myProfile, _badgeMap, compact: true);
        if (flair != null) _profileBtnContent.Children.Add(flair);
    }

    private void SetSingleColumnContent(UIElement content)
    {
        _workspaceHost.Children.Clear();
        _workspaceHost.ColumnDefinitions.Clear();
        _mainScroller.Content = content;
        _workspaceHost.Children.Add(_mainScroller);
    }

    private async Task RenderExploreWorkshopAsync()
    {
        var caps = CommunityCapabilities.GetCapabilitySet(_selectedModelId, _selectedMode);
        if (_categoryFilter != "all" && !caps.AllowsCategory(_categoryFilter))
            _categoryFilter = "all";

        var catRes = await _client.GetCatalogAsync(new CatalogQuery
        {
            Category = _categoryFilter == "all" ? null : _categoryFilter,
            ModelId = _selectedModelId,
            ConnectionMode = _selectedMode,
            MediaType = _mediaFilter == "all" ? null : _mediaFilter,
            Search = _searchQuery,
            Sort = _sort,
            GameTag = _gameTagFilter,
            Uid = _filterUid,
            WatchingOf = _watchingOnly ? _client.Identity.Uid : null
        });

        if (!catRes.Ok || catRes.Data == null)
        {
            EnsureExploreLayout(caps);
            _statusLine.Text = catRes.Error ?? "Failed to reach server.";
            _mainFeedStack.Children.Clear();
            _mainFeedStack.Children.Add(CommunityUI.CardSurface(new StackPanel
            {
                Children =
                {
                    Kit.Text("Could not load catalog", 15, "DS.Text", FontWeights.Bold),
                    Kit.Text(catRes.Error ?? "Unable to contact X20ADMIN.", 12.5, "DS.TextSoft").Also(t => t.Margin = new Thickness(0, 4, 0, 12)),
                    CommunityUI.RectButton("Retry", RectBtnKind.Primary, 32, 12).Also(b => { b.HorizontalAlignment = HorizontalAlignment.Left; b.Click += (_, _) => _ = RefreshAsync(); })
                }
            }, new Thickness(18)));
            return;
        }

        _cachedCatalogKey = CurrentCatalogCacheKey();
        _cachedCatalog = catRes.Data;
        RenderExploreContent(caps, catRes.Data);
    }

    private void EnsureExploreLayout(CommunityCapabilitySet caps)
    {
        _workspaceHost.Children.Clear();
        _workspaceHost.ColumnDefinitions.Clear();
        _workspaceHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(248) });
        _workspaceHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        _workspaceHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Faceted Sidebar (Modrinth / Steam Workshop Pattern)
        var sidebarScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _sidebarStack
        };
        var sidebarBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(CommunityUI.BgSidebar),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = sidebarScroll
        };
        BuildSidebarFacets(caps);
        _workspaceHost.Children.Add(sidebarBorder);

        _mainScroller.Content = _mainFeedStack;
        _mainScroller.Padding = new Thickness(0, 0, 4, 8);
        Grid.SetColumn(_mainScroller, 2);
        _workspaceHost.Children.Add(_mainScroller);
    }

    private void RenderExploreContent(CommunityCapabilitySet caps, CatalogResponse data)
    {
        EnsureExploreLayout(caps);
        _mainFeedStack.Children.Clear();

        _ogWindowOpen = data.OgV0026Open;
        if (!_client.IsNetworkDisabled)
            _onlineText.Text = $"{data.OnlineCount} online";
        _badgeMap.Clear();
        foreach (var b in data.BadgeCatalog) _badgeMap[b.Id] = b;
        UpdateProfileButton();
        _statusLine.Text = $"Showing {data.Items.Count} {(data.Items.Count == 1 ? "result" : "results")}";

        // Result Header Row
        var resBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var refreshBtn = CommunityUI.RectButton("Refresh", RectBtnKind.Secondary, 30, 11.5);
        refreshBtn.Click += (_, _) => _ = RefreshAsync();
        DockPanel.SetDock(refreshBtn, Dock.Right);
        resBar.Children.Add(refreshBtn);

        var resTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        resTitle.Children.Add(Kit.Text($"{caps.ModelName} ({caps.ConnectionMode})", 16, "DS.Text", FontWeights.Bold));
        resTitle.Children.Add(Kit.Text($"  ·  {data.Items.Count} setups", 13, "DS.TextMuted").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        if (!string.IsNullOrEmpty(_filterUid))
        {
            var clearCreator = CommunityUI.RectButton($"Creator: {_filterUid}  ×", RectBtnKind.Secondary, 24, 11);
            clearCreator.Margin = new Thickness(10, 0, 0, 0);
            clearCreator.Click += (_, _) => { _filterUid = null; _ = RefreshAsync(); };
            resTitle.Children.Add(clearCreator);
        }
        if (_watchingOnly)
        {
            var clearWatch = CommunityUI.RectButton("Watching Only  ×", RectBtnKind.Secondary, 24, 11);
            clearWatch.Margin = new Thickness(8, 0, 0, 0);
            clearWatch.Click += (_, _) => { _watchingOnly = false; _ = RefreshAsync(); };
            resTitle.Children.Add(clearWatch);
        }
        if (!string.IsNullOrEmpty(_gameTagFilter))
        {
            var clearGame = CommunityUI.RectButton($"Game: {_gameTagFilter}  ×", RectBtnKind.Secondary, 24, 11);
            clearGame.Margin = new Thickness(8, 0, 0, 0);
            clearGame.Click += (_, _) => { _gameTagFilter = null; _ = RefreshAsync(); };
            resTitle.Children.Add(clearGame);
        }
        resBar.Children.Add(resTitle);
        _mainFeedStack.Children.Add(resBar);

        // Pinned Notes, Active Challenges & Polls
        foreach (var note in data.Notes.Take(2))
            _mainFeedStack.Children.Add(BuildOwnerNoteBanner(note));

        var activeChallenge = data.Challenges.FirstOrDefault(c => string.Equals(c.Status, "active", StringComparison.OrdinalIgnoreCase));
        if (activeChallenge != null)
            _mainFeedStack.Children.Add(BuildChallengeBanner(activeChallenge));

        var activePoll = data.Polls.FirstOrDefault(p => !p.Closed);
        if (activePoll != null)
            _mainFeedStack.Children.Add(BuildPollCard(activePoll));

        if (data.Items.Count == 0)
        {
            bool hasFilter = !string.IsNullOrEmpty(_searchQuery) || _categoryFilter != "all" || !string.IsNullOrEmpty(_filterUid) || _watchingOnly || !string.IsNullOrEmpty(_gameTagFilter);
            var emptyBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) };
            emptyBtns.Children.Add(CommunityUI.RectButton("Publish Setup", RectBtnKind.Primary, 34, 12.5).Also(b =>
            {
                b.Margin = new Thickness(0, 0, 8, 0);
                b.Click += (_, _) =>
                {
                    var cat = _categoryFilter == "all" ? "setup" : _categoryFilter;
                    var exp = _exportCurrentForSubmit?.Invoke(cat, _selectedMode);
                    OpenShareDialog(cat, payload: exp?.Payload, shareCode: exp?.ShareCode, mediaBytes: exp?.MediaBytes, mediaType: exp?.MediaType);
                };
            }));
            if (hasFilter)
            {
                emptyBtns.Children.Add(CommunityUI.RectButton("Reset Filters", RectBtnKind.Secondary, 34, 12.5).Also(b =>
                {
                    b.Click += (_, _) => { _categoryFilter = "all"; _searchQuery = ""; _searchBox.Text = ""; _filterUid = null; _watchingOnly = false; _gameTagFilter = null; _ = RefreshAsync(); };
                }));
            }
            else if (_myProfile == null)
            {
                emptyBtns.Children.Add(CommunityUI.RectButton("Sign In & Claim Handle", RectBtnKind.Secondary, 34, 12.5).Also(b =>
                {
                    b.Click += (_, _) => OpenProfileDialog();
                }));
            }

            _mainFeedStack.Children.Add(CommunityUI.CardSurface(new StackPanel
            {
                Margin = new Thickness(12, 24, 12, 24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    Kit.Text(hasFilter ? "No setups match these filters" : $"No published setups yet for {caps.ModelName}", 15.5, "DS.Text", FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
                    Kit.Text($"Approved community setups, analog curves, and screen art for {caps.ModelName} ({caps.ConnectionMode}) appear here once published.", 12.5, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextAlignment = TextAlignment.Center; t.MaxWidth = 480; t.Margin = new Thickness(0, 6, 0, 0); }),
                    emptyBtns
                }
            }, new Thickness(20)));
            return;
        }

        // 2-Column Clean Steam / Modrinth Card Grid
        var grid = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(-5, 0, -5, 0) };
        foreach (var item in data.Items)
        {
            var card = BuildWorkshopCard(item);
            card.Margin = new Thickness(5);
            grid.Children.Add(card);
        }
        _mainFeedStack.Children.Add(grid);
    }

    private void BuildSidebarFacets(CommunityCapabilitySet caps)
    {
        UpdatePublishButtonLabel();
        static void Detach(FrameworkElement el)
        {
            if (el.Parent is Panel p) p.Children.Remove(el);
        }
        Detach(_searchBox);
        Detach(_modelPicker);
        Detach(_modePicker);
        Detach(_statusLine);
        _sidebarStack.Children.Clear();

        void SectionLabel(string text, double top = 8)
        {
            _sidebarStack.Children.Add(Kit.Text(text, 10, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, top, 0, 3)));
        }

        // 1. Compact single-row Search Box + Go/Clear buttons
        SectionLabel("SEARCH", 0);
        var searchRow = new DockPanel { LastChildFill = true };
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            var clrBtn = CommunityUI.RectButton("Clr", RectBtnKind.Ghost, 28, 11);
            clrBtn.Padding = new Thickness(7, 0, 7, 0);
            clrBtn.Margin = new Thickness(4, 0, 0, 0);
            clrBtn.Click += (_, _) => { _searchQuery = ""; _searchBox.Text = ""; _ = RefreshAsync(); };
            DockPanel.SetDock(clrBtn, Dock.Right);
            searchRow.Children.Add(clrBtn);
        }
        var goBtn = CommunityUI.RectButton("Go", RectBtnKind.Secondary, 28, 11);
        goBtn.Padding = new Thickness(9, 0, 9, 0);
        goBtn.Margin = new Thickness(5, 0, 0, 0);
        goBtn.Click += (_, _) => { _searchQuery = _searchBox.Text.Trim(); _ = RefreshAsync(); };
        DockPanel.SetDock(goBtn, Dock.Right);
        searchRow.Children.Add(goBtn);
        _searchBox.Height = 28;
        _searchBox.Padding = new Thickness(7, 3, 7, 3);
        _searchBox.FontSize = 11.5;
        searchRow.Children.Add(_searchBox);
        _sidebarStack.Children.Add(searchRow);

        // 2. Hardware Filter
        SectionLabel("HARDWARE");
        _sidebarStack.Children.Add(Kit.Text("Controller", 10.5, "DS.TextMuted").Also(t => t.Margin = new Thickness(0, 0, 0, 2)));
        _modelPicker.MinHeight = 30;
        _modelPicker.Padding = new Thickness(8, 3, 8, 3);
        _modelPicker.FontSize = 12;
        _modelPicker.Margin = new Thickness(0, 0, 0, 4);
        _sidebarStack.Children.Add(_modelPicker);
        _sidebarStack.Children.Add(Kit.Text("Mode", 10.5, "DS.TextMuted").Also(t => t.Margin = new Thickness(0, 0, 0, 2)));
        _modePicker.MinHeight = 30;
        _modePicker.Padding = new Thickness(8, 3, 8, 3);
        _modePicker.FontSize = 12;
        _sidebarStack.Children.Add(_modePicker);

        // 3. Categories (Sidebar Tree)
        SectionLabel("CATEGORIES");
        void AddCategoryRow(string key, string label)
        {
            var b = CommunityUI.SidebarRowButton(label, _categoryFilter == key, indented: true);
            b.Margin = new Thickness(0, 1, 0, 1);
            b.Height = 25;
            b.MinHeight = 25;
            b.Click += (_, _) => { _categoryFilter = key; _ = RefreshAsync(); };
            _sidebarStack.Children.Add(b);
        }
        AddCategoryRow("all", "All Creations");
        foreach (var cat in caps.Categories)
        {
            AddCategoryRow(cat, cat switch
            {
                "curve" => "Analog Curves",
                "screen" => "Screen Art",
                "rgb" => "RGB Lighting",
                "vibration" => "Vibration",
                "macro" => "Macros",
                "pack" => "Curated Packs",
                _ => "Full Setups"
            });
        }
        if (_categoryFilter == "screen" && caps.AllowsCategory("screen"))
        {
            foreach (var (mk, ml) in new[] { ("all", "All Screen Media"), ("image", "Static PNG"), ("gif", "Animated GIF") })
            {
                var mb = CommunityUI.SidebarRowButton(ml, _mediaFilter == mk, indented: true);
                mb.Margin = new Thickness(12, 1, 0, 1);
                mb.Height = 24;
                mb.MinHeight = 24;
                mb.Click += (_, _) => { _mediaFilter = mk; _ = RefreshAsync(); };
                _sidebarStack.Children.Add(mb);
            }
        }

        // 4. Feed Scope & Sort
        SectionLabel("FEED SCOPE");
        var scopeGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-2, 0, -2, 0) };
        var allScopeBtn = CommunityUI.RectButton("All Makers", !_watchingOnly ? RectBtnKind.Primary : RectBtnKind.Secondary, 24, 10.5);
        allScopeBtn.Margin = new Thickness(2);
        allScopeBtn.Click += (_, _) => { _watchingOnly = false; _ = RefreshAsync(); };
        var watchScopeBtn = CommunityUI.RectButton("Watching", _watchingOnly ? RectBtnKind.Primary : RectBtnKind.Secondary, 24, 10.5);
        watchScopeBtn.Tag = "scope:watching";
        watchScopeBtn.Margin = new Thickness(2);
        watchScopeBtn.Click += (_, _) => { _watchingOnly = true; _ = RefreshAsync(); };
        scopeGrid.Children.Add(allScopeBtn);
        scopeGrid.Children.Add(watchScopeBtn);
        _sidebarStack.Children.Add(scopeGrid);

        SectionLabel("GAMES");
        var gameWrap = new WrapPanel();
        foreach (var g in new[] { "All", "Apex Legends", "Valorant", "Rocket League", "Call of Duty" })
        {
            var target = g == "All" ? null : g;
            bool act = string.Equals(_gameTagFilter, target, StringComparison.OrdinalIgnoreCase) || (_gameTagFilter == null && target == null);
            var gb = CommunityUI.RectButton(g == "Call of Duty" ? "CoD" : g, act ? RectBtnKind.Primary : RectBtnKind.Secondary, 22, 10);
            gb.Padding = new Thickness(6, 0, 6, 0);
            gb.Margin = new Thickness(0, 0, 4, 4);
            gb.Click += (_, _) => { _gameTagFilter = target; _ = RefreshAsync(); };
            gameWrap.Children.Add(gb);
        }
        _sidebarStack.Children.Add(gameWrap);

        // 5. Sort
        SectionLabel("SORT BY");
        var sortGrid = new UniformGrid { Columns = 2, Margin = new Thickness(-2, 0, -2, 0) };
        foreach (var (sKey, sLbl) in new[]
        {
            ("trending", "Trending"),
            ("installs", "Installs"),
            ("top", "Top Rated"),
            ("new", "Newest")
        })
        {
            var act = _sort == sKey;
            var sb = CommunityUI.RectButton(sLbl, act ? RectBtnKind.Primary : RectBtnKind.Secondary, 25, 11);
            sb.Padding = new Thickness(6, 0, 6, 0);
            sb.Margin = new Thickness(2);
            sb.Click += (_, _) => { _sort = sKey; _ = RefreshAsync(); };
            sortGrid.Children.Add(sb);
        }
        _sidebarStack.Children.Add(sortGrid);

        _statusLine.Margin = new Thickness(0, 8, 0, 0);
        _statusLine.TextWrapping = TextWrapping.Wrap;
        _sidebarStack.Children.Add(_statusLine);
    }

    private Border BuildWorkshopCard(CommunityItem item)
    {
        var outer = new DockPanel();

        // Bottom crisp 3-action footer separated by 1px hairline
        var footer = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 29, 43, 70)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 10, 0, 0),
        };
        var footerDock = new DockPanel();

        var voteBtn = CommunityUI.RectButton($"▲ {item.Upvotes}", RectBtnKind.Secondary, 28, 11.5);
        voteBtn.Tag = $"vote:{item.Id}";
        voteBtn.Click += async (_, _) =>
        {
            item.Upvotes += 1;
            voteBtn.Content = $"▲ {item.Upvotes}";
            var r = await _client.VoteItemAsync(item.Id, 1);
            if (r.Ok) await RefreshAsync();
            else
            {
                item.Upvotes = Math.Max(0, item.Upvotes - 1);
                voteBtn.Content = $"▲ {item.Upvotes}";
                _statusLine.Text = r.Error ?? "Sign in first to upvote.";
            }
        };
        DockPanel.SetDock(voteBtn, Dock.Right);
        footerDock.Children.Add(voteBtn);

        var installsChipText = Kit.Text(
            $"\u2B07 {item.Installs} {(item.Installs == 1 ? "install" : "installs")}",
            11,
            "DS.AccentHi",
            FontWeights.SemiBold);
        var installsChip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(45, 59, 130, 246)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 59, 130, 246)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = installsChipText
        };

        var primaryBtns = new StackPanel { Orientation = Orientation.Horizontal };
        var installBtn = CommunityUI.RectButton("Install", RectBtnKind.Primary, 28, 11.5);
        installBtn.Tag = $"install:{item.Id}";
        installBtn.Margin = new Thickness(0, 0, 6, 0);
        installBtn.Click += async (_, _) =>
        {
            item.Installs += 1;
            installsChipText.Text = $"\u2B07 {item.Installs} {(item.Installs == 1 ? "install" : "installs")}";
            var updated = await _client.RecordInstallAsync(item.Id);
            if (updated != null)
            {
                item.Installs = updated.Installs;
                installsChipText.Text = $"\u2B07 {item.Installs} {(item.Installs == 1 ? "install" : "installs")}";
            }
            InstallItemRequested?.Invoke(item, Array.Empty<string>());
            _statusLine.Text = $"Installed '{item.Title}' ({item.Installs} {(item.Installs == 1 ? "install" : "installs")}).";
        };
        var inspectBtn = CommunityUI.RectButton("Inspect", RectBtnKind.Secondary, 28, 11.5);
        inspectBtn.Margin = new Thickness(0, 0, 6, 0);
        inspectBtn.Click += (_, _) => _ = OpenItemInspectorModalAsync(item);
        primaryBtns.Children.Add(installBtn);
        primaryBtns.Children.Add(inspectBtn);
        if (item.Uid == _client.Identity.Uid)
        {
            var deleteBtn = CommunityUI.RectButton("Delete", RectBtnKind.Danger, 28, 11.5);
            deleteBtn.Tag = $"delete:{item.Id}";
            deleteBtn.Click += async (_, _) =>
            {
                deleteBtn.IsEnabled = false;
                var dRes = await _client.DeleteItemAsync(item.Id);
                if (dRes.Ok)
                {
                    _cachedCatalog?.Items.RemoveAll(x => x.Id == item.Id);
                    _statusLine.Text = $"Deleted '{item.Title}'.";
                    await RefreshAsync();
                }
                else
                {
                    deleteBtn.IsEnabled = true;
                    _statusLine.Text = dRes.Error ?? "Could not delete item.";
                }
            };
            primaryBtns.Children.Add(deleteBtn);
        }
        footerDock.Children.Add(primaryBtns);
        footer.Child = footerDock;
        DockPanel.SetDock(footer, Dock.Bottom);
        outer.Children.Add(footer);

        // Body row: Left Preview Thumbnail + Right Metadata
        var mainDock = new DockPanel();
        if (item.Category == "screen" && !string.IsNullOrEmpty(item.AssetUrl))
        {
            var oled = new AnimatedMediaView(136, 68, pixelArt: true, cornerRadius: 6)
            {
                BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            oled.SetPlaceholder("160x80");
            _ = Task.Run(async () =>
            {
                var bytes = await _client.DownloadAssetAsync(item.AssetUrl, item.AssetSha256);
                if (bytes != null) oled.Dispatcher.Invoke(() => oled.LoadBytes(bytes, "160x80"));
            });
            DockPanel.SetDock(oled, Dock.Left);
            mainDock.Children.Add(oled);
        }
        else
        {
            var thumb = CommunityUI.BuildPreviewThumb(item.Category, item.ShareCode, item.Payload);
            thumb.Margin = new Thickness(0, 0, 12, 0);
            thumb.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(thumb, Dock.Left);
            mainDock.Children.Add(thumb);
        }

        var meta = new StackPanel();
        var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
        var rightTags = new StackPanel { Orientation = Orientation.Horizontal };
        if (item.FeaturedRank > 0)
            rightTags.Children.Add(CommunityUI.SquareTag("FEATURED", Color.FromRgb(253, 230, 138), Color.FromArgb(60, 245, 158, 11), Color.FromRgb(245, 158, 11)));
        if (item.Version > 1)
            rightTags.Children.Add(CommunityUI.SquareTag($"v{item.Version}"));
        if (!string.IsNullOrEmpty(item.RemixSourceId))
            rightTags.Children.Add(CommunityUI.SquareTag("REMIX"));
        rightTags.Children.Add(CommunityUI.SquareTag(item.MediaType == "gif" ? "GIF" : item.MediaType == "image" ? "PNG" : item.Category.ToUpperInvariant()));
        DockPanel.SetDock(rightTags, Dock.Right);
        titleRow.Children.Add(rightTags);
        titleRow.Children.Add(Kit.Text(item.Title, 14, "DS.Text", FontWeights.Bold).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis));
        meta.Children.Add(titleRow);

        var creatorLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
        if (item.Creator != null)
        {
            var creatorBtn = CommunityUI.RectButton($"@{item.Creator.Handle}", RectBtnKind.Ghost, 22, 11.5);
            creatorBtn.Padding = new Thickness(4, 0, 4, 0);
            creatorBtn.Click += async (_, _) => await ShowCreatorCardAsync(item.Creator);
            creatorLine.Children.Add(creatorBtn);
            var flair = BadgeChip.CreateFlair(item.Creator, _badgeMap, compact: true);
            if (flair != null) creatorLine.Children.Add(flair);
        }
        creatorLine.Children.Add(installsChip);
        creatorLine.Children.Add(Kit.Text($"  ·  {item.WorksCount} verified", 11, "DS.TextMuted").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        meta.Children.Add(creatorLine);

        meta.Children.Add(Kit.Text(item.Description, 12, "DS.TextSoft").Also(t =>
        {
            t.TextWrapping = TextWrapping.Wrap;
            t.MaxHeight = 34;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
        }));
        if (item.GameTags is { Count: > 0 })
        {
            var gWrap = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            foreach (var g in item.GameTags.Take(2))
            {
                var gameTag = g;
                var gb = CommunityUI.RectButton(gameTag, RectBtnKind.Secondary, 20, 10);
                gb.Tag = $"filtergame:{gameTag}";
                gb.Padding = new Thickness(6, 0, 6, 0);
                gb.Margin = new Thickness(0, 0, 5, 0);
                gb.Click += (_, _) => { _gameTagFilter = gameTag; _ = RefreshAsync(); };
                gWrap.Children.Add(gb);
            }
            meta.Children.Add(gWrap);
        }

        mainDock.Children.Add(meta);
        outer.Children.Add(mainDock);

        return CommunityUI.CardSurface(outer, new Thickness(14), item.FeaturedRank > 0 ? Color.FromRgb(245, 158, 11) : null);
    }

    private async Task OpenItemInspectorModalAsync(CommunityItem item)
    {
        var modalRoot = new StackPanel();
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var closeBtn = CommunityUI.RectButton("Close", RectBtnKind.Secondary, 30, 11.5);
        closeBtn.Click += (_, _) => CloseModal();
        DockPanel.SetDock(closeBtn, Dock.Right);
        top.Children.Add(closeBtn);
        top.Children.Add(Kit.Text(item.Title, 18, "DS.Text", FontWeights.Bold));
        modalRoot.Children.Add(top);

        var tagRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        tagRow.Children.Add(CommunityUI.SquareTag(item.Category.ToUpperInvariant()));
        tagRow.Children.Add(CommunityUI.SquareTag($"{item.ModelId} / {item.ConnectionMode}"));
        tagRow.Children.Add(CommunityUI.SquareTag($"v{item.Version}"));
        tagRow.Children.Add(CommunityUI.SquareTag($"\u2B07 {item.Installs} {(item.Installs == 1 ? "install" : "installs")}"));
        if (item.Creator != null) tagRow.Children.Add(CommunityUI.SquareTag($"Maker: {item.Creator.Handle}"));
        foreach (var g in item.GameTags ?? Enumerable.Empty<string>())
            tagRow.Children.Add(CommunityUI.SquareTag(g, Color.FromRgb(186, 230, 253), Color.FromArgb(65, 56, 189, 248)));
        modalRoot.Children.Add(tagRow);

        var heroRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        if (item.Category == "screen" && !string.IsNullOrEmpty(item.AssetUrl))
        {
            var inspectorScreen = new AnimatedMediaView(160, 80, pixelArt: true, cornerRadius: 6)
            {
                BorderBrush = new SolidColorBrush(CommunityUI.BorderFocus),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 14, 0)
            };
            inspectorScreen.Tag = "inspector:screenview";
            inspectorScreen.SetPlaceholder("160x80");
            _ = Task.Run(async () =>
            {
                var bytes = await _client.DownloadAssetAsync(item.AssetUrl, item.AssetSha256);
                if (bytes != null) inspectorScreen.Dispatcher.Invoke(() => inspectorScreen.LoadBytes(bytes, "160x80"));
            });
            bool isZoomed = false;
            var zoomBtn = CommunityUI.RectButton("Zoom 2x (320x160)", RectBtnKind.Secondary, 28, 11.5);
            zoomBtn.Tag = "inspector:zoom2x";
            zoomBtn.Margin = new Thickness(0, 6, 0, 0);
            zoomBtn.HorizontalAlignment = HorizontalAlignment.Left;
            zoomBtn.Click += (_, _) =>
            {
                isZoomed = !isZoomed;
                inspectorScreen.Width = isZoomed ? 320 : 160;
                inspectorScreen.Height = isZoomed ? 160 : 80;
                zoomBtn.Content = isZoomed ? "Reset 1x (160x80)" : "Zoom 2x (320x160)";
            };
            var leftVisual = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            leftVisual.Children.Add(inspectorScreen);
            leftVisual.Children.Add(zoomBtn);
            DockPanel.SetDock(leftVisual, Dock.Left);
            heroRow.Children.Add(leftVisual);
        }
        else
        {
            var largeThumb = CommunityUI.BuildPreviewThumb(item.Category, item.ShareCode, item.Payload, 156, 112);
            largeThumb.Margin = new Thickness(0, 0, 14, 0);
            DockPanel.SetDock(largeThumb, Dock.Left);
            heroRow.Children.Add(largeThumb);
        }
        heroRow.Children.Add(Kit.Text(item.Description, 13, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.VerticalAlignment = VerticalAlignment.Top; }));
        modalRoot.Children.Add(heroRow);

        var actionRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        actionRow.Children.Add(CommunityUI.RectButton("Install Setup", RectBtnKind.Primary, 32, 12).Also(b =>
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += async (_, _) =>
            {
                await _client.RecordInstallAsync(item.Id);
                InstallItemRequested?.Invoke(item, Array.Empty<string>());
                CloseModal();
            };
        }));
        if (item.Category != "screen")
        {
            actionRow.Children.Add(CommunityUI.RectButton("Compare & 5s Test", RectBtnKind.Secondary, 32, 12).Also(b =>
            {
                b.Margin = new Thickness(0, 0, 6, 6);
                b.Click += (_, _) =>
                {
                    var drawer = new CommunityDiffDrawer(item, _currentDraftSummary());
                    drawer.InstallSelectedRequested += async (it, secs) =>
                    {
                        await _client.RecordInstallAsync(it.Id);
                        InstallItemRequested?.Invoke(it, secs);
                        CloseModal();
                        await RefreshAsync();
                    };
                    drawer.TestLiveRequested += (it, secs) => TestLiveRequested?.Invoke(it, secs);
                    drawer.RevertLiveRequested += () => RevertLiveRequested?.Invoke();
                    drawer.CloseRequested += CloseModal;
                    ActiveDiffDrawer = drawer;
                    ShowModal(drawer);
                };
            }));
        }
        actionRow.Children.Add(CommunityUI.RectButton("Works Great", RectBtnKind.Secondary, 32, 12).Also(b =>
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += async (_, _) => { var r = await _client.MarkWorksAsync(item.Id, _selectedModelId); _statusLine.Text = r.Ok ? "Verified compatibility!" : (r.Error ?? "Sign in first."); };
        }));
        actionRow.Children.Add(CommunityUI.RectButton("Bookmark", RectBtnKind.Secondary, 32, 12).Also(b =>
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += async (_, _) => { var r = await _client.ToggleBookmarkAsync(item.Id); _statusLine.Text = r.Ok ? (r.Data ? "Bookmarked." : "Removed bookmark.") : (r.Error ?? "Sign in first."); };
        }));
        actionRow.Children.Add(CommunityUI.RectButton("Remix", RectBtnKind.Secondary, 32, 12).Also(b =>
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += (_, _) =>
            {
                var exp = _exportCurrentForSubmit?.Invoke(item.Category, _selectedMode);
                OpenShareDialog(item.Category, $"Remix of {item.Title}", exp?.Payload ?? item.Payload, exp?.ShareCode ?? item.ShareCode, remixSourceId: item.Id);
            };
        }));
        if (!string.IsNullOrEmpty(item.ShareCode))
        {
            actionRow.Children.Add(CommunityUI.RectButton("Copy Code", RectBtnKind.Secondary, 32, 12).Also(b =>
            {
                b.Margin = new Thickness(0, 0, 6, 6);
                b.Click += (_, _) => { Clipboard.SetText(item.ShareCode); _statusLine.Text = "Copied share code."; };
            }));
        }
        actionRow.Children.Add(CommunityUI.RectButton("Report", RectBtnKind.Danger, 32, 12).Also(b =>
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += async (_, _) => { var r = await _client.ReportAsync("item", item.Id, "Inappropriate or broken"); _statusLine.Text = r.Ok ? "Reported." : (r.Error ?? "Sign in first."); };
        }));
        if (item.Uid == _client.Identity.Uid)
        {
            actionRow.Children.Add(CommunityUI.RectButton($"Push Update (v{item.Version + 1})", RectBtnKind.Primary, 32, 12).Also(b =>
            {
                b.Tag = $"update:{item.Id}";
                b.Margin = new Thickness(0, 0, 6, 6);
                b.Click += (_, _) =>
                {
                    CloseModal();
                    OpenShareDialog(item.Category, item.Title, item.Payload, item.ShareCode, parentItemId: item.Id, initialGameTags: item.GameTags);
                };
            }));
            actionRow.Children.Add(CommunityUI.RectButton("Delete", RectBtnKind.Danger, 32, 12).Also(b =>
            {
                b.Margin = new Thickness(0, 0, 0, 6);
                b.Click += async (_, _) =>
                {
                    var dRes = await _client.DeleteItemAsync(item.Id);
                    if (dRes.Ok)
                    {
                        _cachedCatalog?.Items.RemoveAll(x => x.Id == item.Id);
                        CloseModal();
                        await RefreshAsync();
                    }
                    else
                    {
                        _statusLine.Text = dRes.Error ?? "Could not delete item.";
                    }
                };
            }));
        }
        modalRoot.Children.Add(actionRow);

        // Comments Section inline inside Inspector
        modalRoot.Children.Add(Kit.Text($"Comments ({item.CommentsCount})", 13.5, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        var cList = new StackPanel();
        var cRes = await _client.GetCommentsAsync(item.Id);
        if (cRes.Ok && cRes.Data != null && cRes.Data.Count > 0)
        {
            foreach (var c in cRes.Data)
            {
                var cStack = new StackPanel();
                var ch = new StackPanel { Orientation = Orientation.Horizontal };
                ch.Children.Add(Kit.Text(c.Creator?.Handle ?? c.Uid, 12, "DS.AccentHi", FontWeights.Bold));
                var cFlair = c.Creator != null ? BadgeChip.CreateFlair(c.Creator, _badgeMap, compact: true) : null;
                if (cFlair != null) ch.Children.Add(cFlair);
                cStack.Children.Add(ch);
                cStack.Children.Add(Kit.Text(c.Body, 12, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap));
                cList.Children.Add(CommunityUI.CardSurface(cStack, new Thickness(10)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }
        else
        {
            cList.Children.Add(Kit.Text("No comments yet.", 12, "DS.TextMuted"));
        }
        modalRoot.Children.Add(new ScrollViewer { MaxHeight = 200, Content = cList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) });

        var cBox = CommunityUI.RectInput("", 32);
        var cSend = CommunityUI.RectButton("Post", RectBtnKind.Primary, 32, 12);
        cSend.Margin = new Thickness(8, 0, 0, 0);
        var cStatus = Kit.Text("", 11.5, "DS.TextMuted");
        cSend.Click += async (_, _) =>
        {
            var r = await _client.AddCommentAsync(item.Id, cBox.Text.Trim());
            if (r.Ok) await OpenItemInspectorModalAsync(item);
            else cStatus.Text = r.Error ?? "Sign in first to comment.";
        };
        var compose = new DockPanel();
        DockPanel.SetDock(cSend, Dock.Right);
        compose.Children.Add(cSend);
        compose.Children.Add(cBox);
        modalRoot.Children.Add(compose);
        modalRoot.Children.Add(cStatus.Also(t => t.Margin = new Thickness(0, 6, 0, 0)));

        var sheet = new Border
        {
            Width = 620,
            MaxHeight = 680,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(CommunityUI.BgCard),
            Padding = new Thickness(22),
            Child = new ScrollViewer { Content = modalRoot, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        var backdrop = new Grid { Background = new SolidColorBrush(Color.FromArgb(200, 3, 6, 16)), Children = { sheet } };
        CommunityUI.AnimateModalIn(sheet);
        backdrop.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == backdrop) CloseModal(); };
        ShowModal(backdrop);
    }

    private Border BuildChallengeBanner(CommunityChallenge challenge)
    {
        var row = new DockPanel();
        var enterBtn = CommunityUI.RectButton("Submit Entry", RectBtnKind.Primary, 30, 11.5);
        enterBtn.Click += (_, _) =>
        {
            var exp = _exportCurrentForSubmit?.Invoke(challenge.Category, _selectedMode);
            OpenShareDialog(challenge.Category, $"Entry: {challenge.Title}", exp?.Payload, exp?.ShareCode, exp?.MediaBytes, exp?.MediaType);
        };
        DockPanel.SetDock(enterBtn, Dock.Right);
        row.Children.Add(enterBtn);

        var text = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        head.Children.Add(CommunityUI.SquareTag("CHALLENGE", Color.FromRgb(253, 230, 138), Color.FromArgb(60, 245, 158, 11), Color.FromRgb(245, 158, 11)));
        head.Children.Add(Kit.Text(challenge.Title, 14, "DS.Text", FontWeights.Bold));
        text.Children.Add(head);
        text.Children.Add(Kit.Text($"{challenge.Description}  |  Category: {challenge.Category}", 12, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap));
        row.Children.Add(text);

        return CommunityUI.CardSurface(row, new Thickness(14), Color.FromRgb(245, 158, 11)).Also(b => b.Margin = new Thickness(0, 0, 0, 12));
    }

    private Border BuildOwnerNoteBanner(OwnerNote note)
    {
        var stack = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        head.Children.Add(CommunityUI.SquareTag("ANNOUNCEMENT", Colors.White, Color.FromArgb(95, 255, 122, 24), BadgeChip.OwnerOrange));
        head.Children.Add(Kit.Text("AmjadXCTL", 12, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        head.Children.Add(BadgeChip.CreateEmblem(null, "amjad-verified", 16, "AmjadXCTL - Verified X20CTL Creator & Owner"));
        stack.Children.Add(head);
        stack.Children.Add(Kit.Text(note.Title, 14, "DS.Text", FontWeights.SemiBold));
        stack.Children.Add(Kit.Text(note.Body, 12.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 3, 0, 8); }));

        var reacts = new StackPanel { Orientation = Orientation.Horizontal };
        Button ReactBtn(string key, string label, int count)
        {
            var b = CommunityUI.RectButton($"{label} ({count})", RectBtnKind.Secondary, 26, 11);
            b.Margin = new Thickness(0, 0, 6, 0);
            b.Click += async (_, _) => { var r = await _client.ReactNoteAsync(note.Id, key); if (r.Ok) await RefreshAsync(); };
            return b;
        }
        reacts.Children.Add(ReactBtn("heart", "Like", note.HeartsCount));
        reacts.Children.Add(ReactBtn("thumb", "Helpful", note.ThumbsCount));
        reacts.Children.Add(ReactBtn("fire", "Featured", note.FireCount));
        stack.Children.Add(reacts);

        return CommunityUI.CardSurface(stack, new Thickness(14), BadgeChip.OwnerOrange).Also(b => b.Margin = new Thickness(0, 0, 0, 12));
    }

    private Border BuildPollCard(CommunityPoll poll)
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Text($"POLL: {poll.Question}", 13.5, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        int total = Math.Max(1, poll.Options.Sum(o => o.Votes));
        for (int i = 0; i < poll.Options.Count; i++)
        {
            int idx = i;
            var opt = poll.Options[i];
            int pct = (int)Math.Round(opt.Votes * 100.0 / total);
            var btn = CommunityUI.RectButton($"{opt.Label}  ({opt.Votes} votes · {pct}%)", RectBtnKind.Secondary, 30, 12);
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Margin = new Thickness(0, 0, 0, 5);
            btn.Click += async (_, _) => { var r = await _client.VotePollAsync(poll.Id, idx); if (r.Ok) await RefreshAsync(); };
            stack.Children.Add(btn);
        }
        return CommunityUI.CardSurface(stack, new Thickness(14)).Also(b => b.Margin = new Thickness(0, 0, 0, 12));
    }

    private async Task ShowCreatorCardAsync(CreatorProfile creatorSummary)
    {
        var fullRes = await _client.GetProfileAsync(creatorSummary.Uid);
        var profile = fullRes.Ok ? fullRes.Data.Creator : creatorSummary;
        byte[]? pfpBytes = !string.IsNullOrEmpty(profile.PfpUrl) ? await _client.DownloadAssetAsync(profile.PfpUrl) : null;

        var card = new CreatorHoverCard(profile, _badgeMap, pfpBytes, profile.Uid == _client.Identity.Uid);
        card.WatchToggled += async targetUid =>
        {
            var r = await _client.ToggleWatchAsync(targetUid);
            return r.Ok ? r.Data : (profile.IsWatching, profile.WatchersCount);
        };
        card.FilterByCreatorRequested += targetUid =>
        {
            _filterUid = targetUid;
            _subTab = "explore";
            CloseModal();
            _ = RefreshAsync();
        };

        var wrap = new Grid { Background = new SolidColorBrush(Color.FromArgb(190, 3, 6, 16)) };
        wrap.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == wrap) CloseModal(); };
        var closeOverlayBtn = CommunityUI.RectButton("Close", RectBtnKind.Secondary, 30, 12);
        closeOverlayBtn.Margin = new Thickness(0, 10, 0, 0);
        closeOverlayBtn.HorizontalAlignment = HorizontalAlignment.Center;
        closeOverlayBtn.Click += (_, _) => CloseModal();
        wrap.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { card, closeOverlayBtn }
        });
        ShowModal(wrap);
    }

    private async Task RenderSavedAndMineAsync()
    {
        var savedRes = await _client.GetCatalogAsync(new CatalogQuery { BookmarkedBy = _client.Identity.Uid, Sort = "new" });
        var mineRes = await _client.GetCatalogAsync(new CatalogQuery { Uid = _client.Identity.Uid, Status = "mine", Sort = "new" });
        var saved = savedRes.Ok && savedRes.Data != null ? savedRes.Data.Items : new List<CommunityItem>();
        var mine = mineRes.Ok && mineRes.Data != null ? mineRes.Data.Items : new List<CommunityItem>();
        _cachedSavedMine = (saved, mine);
        if (_subTab == "saved") RenderSavedAndMineContent(saved, mine);
    }

    private void RenderSavedAndMineContent(List<CommunityItem> saved, List<CommunityItem> mine)
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Text("Saved Bookmarks", 16, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 10)));
        if (saved.Count > 0)
        {
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(-5, 0, -5, 16) };
            foreach (var s in saved) grid.Children.Add(BuildWorkshopCard(s).Also(b => b.Margin = new Thickness(5)));
            stack.Children.Add(grid);
        }
        else
        {
            stack.Children.Add(CommunityUI.CardSurface(Kit.Text("No bookmarked setups yet. Open any Workshop card and click Bookmark.", 12.5, "DS.TextMuted"), new Thickness(16)).Also(b => b.Margin = new Thickness(0, 0, 0, 16)));
        }

        stack.Children.Add(Kit.Text("My Published & Pending Uploads", 16, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 10)));
        if (mine.Count > 0)
        {
            foreach (var myItem in mine)
            {
                var btns = new StackPanel { Orientation = Orientation.Horizontal };
                var updBtn = CommunityUI.RectButton($"Push Update (v{myItem.Version + 1})", RectBtnKind.Primary, 28, 11.5);
                updBtn.Tag = $"update:{myItem.Id}";
                updBtn.Margin = new Thickness(0, 0, 6, 0);
                updBtn.Click += (_, _) => OpenShareDialog(myItem.Category, myItem.Title, myItem.Payload, myItem.ShareCode, parentItemId: myItem.Id, initialGameTags: myItem.GameTags);
                var delBtn = CommunityUI.RectButton("Delete", RectBtnKind.Danger, 28, 11.5);
                delBtn.Tag = $"delete:{myItem.Id}";
                delBtn.Click += async (_, _) =>
                {
                    var dRes = await _client.DeleteItemAsync(myItem.Id);
                    if (dRes.Ok)
                    {
                        _cachedCatalog?.Items.RemoveAll(x => x.Id == myItem.Id);
                        await RefreshAsync();
                    }
                };
                btns.Children.Add(updBtn);
                btns.Children.Add(delBtn);
                var row = new DockPanel();
                DockPanel.SetDock(btns, Dock.Right);
                row.Children.Add(btns);
                row.Children.Add(new StackPanel
                {
                    Children =
                    {
                        Kit.Text($"{myItem.Title}  [{myItem.Status.ToUpperInvariant()} · v{myItem.Version}]", 13.5, "DS.Text", FontWeights.SemiBold),
                        Kit.Text($"{myItem.Category}  ·  Score {myItem.Score}  ·  {myItem.Installs} installs", 12, "DS.TextMuted")
                    }
                });
                stack.Children.Add(CommunityUI.CardSurface(row, new Thickness(12)).Also(b => b.Margin = new Thickness(0, 0, 0, 8)));
            }
        }
        else
        {
            stack.Children.Add(CommunityUI.CardSurface(Kit.Text("You have not submitted any community creations from this device yet.", 12.5, "DS.TextMuted"), new Thickness(16)));
        }

        SetSingleColumnContent(stack);
    }

    private async Task RenderRequestsBoardAsync()
    {
        var reqRes = await _client.GetRequestsAsync();
        var list = reqRes.Ok && reqRes.Data != null ? reqRes.Data : new List<CommunityRequest>();
        _cachedRequests = list;
        if (_subTab == "requests") RenderRequestsContent(list);
    }

    private void RenderRequestsContent(List<CommunityRequest> list)
    {
        var stack = new StackPanel();
        var createStack = new StackPanel();
        createStack.Children.Add(Kit.Text("Request a Setup, Curve, or Screen Art", 14.5, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        var rTitle = CommunityUI.RectInput("", 32);
        rTitle.Margin = new Thickness(0, 3, 0, 8);
        var rGame = CommunityUI.RectInput("", 32);
        rGame.Margin = new Thickness(0, 3, 0, 10);
        var rBtn = CommunityUI.RectButton("Post Request", RectBtnKind.Primary, 32, 12);
        rBtn.HorizontalAlignment = HorizontalAlignment.Left;
        rBtn.Click += async (_, _) =>
        {
            var res = await _client.CreateRequestAsync(rTitle.Text.Trim(), "Requested from X20CTL Workshop", "setup", _selectedModelId, _selectedMode, rGame.Text.Trim());
            if (res.Ok) await RefreshAsync();
            else _statusLine.Text = res.Error ?? "Sign in first to post requests.";
        };
        createStack.Children.Add(Kit.Text("Request Title", 11.5, "DS.TextSoft"));
        createStack.Children.Add(rTitle);
        createStack.Children.Add(Kit.Text("Target Game (optional)", 11.5, "DS.TextSoft"));
        createStack.Children.Add(rGame);
        createStack.Children.Add(rBtn);
        stack.Children.Add(CommunityUI.CardSurface(createStack, new Thickness(16)).Also(b => b.Margin = new Thickness(0, 0, 0, 14)));

        if (list.Count > 0)
        {
            foreach (var r in list)
            {
                var row = new DockPanel();
                var actions = new StackPanel { Orientation = Orientation.Horizontal };
                DockPanel.SetDock(actions, Dock.Right);
                var voteBtn = CommunityUI.RectButton($"▲ {r.Upvotes}", RectBtnKind.Secondary, 28, 11.5);
                voteBtn.Margin = new Thickness(0, 0, 6, 0);
                voteBtn.Click += async (_, _) => { await _client.VoteRequestAsync(r.Id); await RefreshAsync(); };
                var fulfillBtn = CommunityUI.RectButton("Fulfill Request", RectBtnKind.Primary, 28, 11.5);
                fulfillBtn.Click += (_, _) => OpenShareDialog(r.Category, r.Title, fulfillRequestId: r.Id);
                actions.Children.Add(voteBtn);
                actions.Children.Add(fulfillBtn);
                row.Children.Add(actions);
                row.Children.Add(new StackPanel
                {
                    Children =
                    {
                        Kit.Text($"{r.Title}  [{r.Status.ToUpperInvariant()}]", 14, "DS.Text", FontWeights.SemiBold),
                        Kit.Text($"{r.ModelId} / {r.ConnectionMode}  ·  By {r.Creator?.Handle ?? r.Uid}  ·  {r.GameTag}", 12, "DS.TextMuted")
                    }
                });
                stack.Children.Add(CommunityUI.CardSurface(row, new Thickness(12)).Also(b => b.Margin = new Thickness(0, 0, 0, 8)));
            }
        }
        else
        {
            stack.Children.Add(CommunityUI.CardSurface(Kit.Text("No open setup requests yet. Ask the community for a game profile above.", 12.5, "DS.TextMuted"), new Thickness(16)));
        }

        SetSingleColumnContent(stack);
    }

    private async Task RenderLoungeBoardAsync()
    {
        var lRes = await _client.GetLoungeAsync();
        if (!lRes.Ok)
        {
            var errStack = new StackPanel();
            errStack.Children.Add(CommunityUI.CardSurface(Kit.Text(lRes.Error ?? "Lounge unavailable right now.", 12.5, "DS.TextSoft"), new Thickness(16)));
            if (_subTab == "lounge") SetSingleColumnContent(errStack);
            return;
        }
        _cachedLounge = lRes.Data;
        if (_subTab == "lounge") RenderLoungeContent(lRes.Data.Messages, lRes.Data.SlowmodeSec, lRes.Data.Paused);
    }

    private void RenderLoungeContent(List<LoungeMessage> msgs, int slowSec, bool paused)
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Text($"Community Lounge  ·  Slow-mode: {slowSec}s  ·  {(paused ? "Paused by Owner" : "Open")}", 13, "DS.AccentHi", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 10)));

        var composer = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var sendBtn = CommunityUI.RectButton("Send", RectBtnKind.Primary, 34, 12.5);
        DockPanel.SetDock(sendBtn, Dock.Right);
        var box = CommunityUI.RectInput("", 34);
        box.Margin = new Thickness(0, 0, 8, 0);
        sendBtn.Click += async (_, _) =>
        {
            var r = await _client.PostLoungeAsync(box.Text.Trim());
            if (r.Ok) await RefreshAsync();
            else _statusLine.Text = r.Error ?? "Sign in first to chat.";
        };
        composer.Children.Add(sendBtn);
        composer.Children.Add(box);
        stack.Children.Add(composer);

        if (msgs.Count == 0)
        {
            stack.Children.Add(CommunityUI.CardSurface(Kit.Text("No messages in the lounge yet. Say hello!", 12.5, "DS.TextMuted"), new Thickness(16)));
        }
        else
        {
            foreach (var m in msgs.AsEnumerable().Reverse())
            {
                var mRow = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal };
                top.Children.Add(Kit.Text(m.Creator?.Handle ?? m.Uid, 12.5, "DS.AccentHi", FontWeights.Bold));
                var mFlair = m.Creator != null ? BadgeChip.CreateFlair(m.Creator, _badgeMap, compact: true) : null;
                if (mFlair != null) top.Children.Add(mFlair);
                mRow.Children.Add(top);
                mRow.Children.Add(Kit.Text(m.Body, 13, "DS.Text").Also(t => t.TextWrapping = TextWrapping.Wrap));
                stack.Children.Add(CommunityUI.CardSurface(mRow, new Thickness(10)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }
        SetSingleColumnContent(stack);
    }

    private async Task RenderLeaderboardAndWatchingAsync()
    {
        var lbRes = await _client.GetLeaderboardAsync(_leaderboardMetric);
        var watchRes = await _client.GetWatchListsAsync();
        var makers = lbRes.Ok ? lbRes.Data.Makers : new List<CreatorProfile>();
        var watchers = watchRes.Ok ? watchRes.Data.Watchers : new List<CreatorProfile>();
        var watching = watchRes.Ok ? watchRes.Data.Watching : new List<CreatorProfile>();
        if (lbRes.Ok)
        {
            foreach (var b in lbRes.Data.Badges) _badgeMap[b.Id] = b;
        }
        _cachedLeaderboard = (makers, watchers, watching);
        if (_subTab == "leaderboard") RenderLeaderboardContent(makers, watchers, watching);
    }

    private void RenderLeaderboardContent(List<CreatorProfile> makers, List<CreatorProfile> watchers, List<CreatorProfile> watching)
    {
        var stack = new StackPanel();
        var wPanel = new StackPanel();
        wPanel.Children.Add(Kit.Text($"Watching ({watching.Count}) & Watchers ({watchers.Count})", 15, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        var chips = new WrapPanel();
        foreach (var maker in watching)
        {
            var btn = CommunityUI.RectButton($"{maker.Handle} ({maker.Karma} karma)", RectBtnKind.Secondary, 28, 11.5);
            btn.Margin = new Thickness(0, 0, 6, 6);
            btn.Click += (_, _) => { _filterUid = maker.Uid; _subTab = "explore"; _ = RefreshAsync(); };
            chips.Children.Add(btn);
        }
        if (chips.Children.Count == 0)
            wPanel.Children.Add(Kit.Text("Not Watching any Makers yet. Click any creator handle to Watch their setups.", 12.5, "DS.TextMuted"));
        else
            wPanel.Children.Add(chips);
        stack.Children.Add(CommunityUI.CardSurface(wPanel, new Thickness(14)).Also(b => b.Margin = new Thickness(0, 0, 0, 14)));

        var lbHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var metricBar = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (mk, ml) in new (string, string)[] { ("karma", "Top Karma"), ("installs", "Most Installed"), ("watchers", "Most Watched") })
        {
            var metricKey = mk;
            var mb = CommunityUI.RectButton(ml, _leaderboardMetric == metricKey ? RectBtnKind.Primary : RectBtnKind.Secondary, 28, 11.5);
            mb.Tag = $"lbmetric:{metricKey}";
            mb.Margin = new Thickness(6, 0, 0, 0);
            mb.Click += (_, _) => { _leaderboardMetric = metricKey; _ = RefreshAsync(); };
            metricBar.Children.Add(mb);
        }
        DockPanel.SetDock(metricBar, Dock.Right);
        lbHeader.Children.Add(metricBar);
        lbHeader.Children.Add(Kit.Text("Top Community Makers", 16, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        stack.Children.Add(lbHeader);

        if (makers.Count > 0)
        {
            int rank = 1;
            foreach (var maker in makers)
            {
                int currentRank = rank++;
                Color? podiumBorder = currentRank switch
                {
                    1 => Color.FromRgb(245, 158, 11),
                    2 => Color.FromRgb(203, 213, 225),
                    3 => Color.FromRgb(217, 119, 6),
                    _ => null
                };
                var row = new DockPanel();
                var viewBtn = CommunityUI.RectButton("View Profile", RectBtnKind.Secondary, 28, 11.5);
                viewBtn.Click += async (_, _) => await ShowCreatorCardAsync(maker);
                DockPanel.SetDock(viewBtn, Dock.Right);
                row.Children.Add(viewBtn);

                var line = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                line.Children.Add(Kit.Text($"#{currentRank}  {maker.Handle}", 14, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
                var flair = BadgeChip.CreateFlair(maker, _badgeMap, compact: true);
                if (flair != null) line.Children.Add(flair);
                line.Children.Add(Kit.Text($"   ·   Uploads {maker.ApprovedCount}   ·   Installs {maker.InstallsReceived}   ·   Karma {maker.Karma}   ·   Watchers {maker.WatchersCount}", 12.5, "DS.TextSoft").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
                row.Children.Add(line);
                stack.Children.Add(CommunityUI.CardSurface(row, new Thickness(12), podiumBorder).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }
        else
        {
            stack.Children.Add(CommunityUI.CardSurface(Kit.Text("No published makers on the leaderboard yet. Publish your first setup, curve, or Screen Art to claim #1!", 12.5, "DS.TextMuted"), new Thickness(16)));
        }

        SetSingleColumnContent(stack);
    }

    public CreatorProfileDialog OpenProfileDialogForTest()
    {
        var dlg = new CreatorProfileDialog(_client, _myProfile, _badgeMap, _ogWindowOpen, _myStats);
        dlg.CloseRequested += CloseModal;
        dlg.ProfileUpdated += p => { _myProfile = p; UpdateProfileButton(); _ = RefreshAsync(); };
        ShowModal(dlg);
        return dlg;
    }

    public Task OpenItemInspectorForTestAsync(CommunityItem item) => OpenItemInspectorModalAsync(item);

    public void CloseModalForTest() => CloseModal();

    private void OpenProfileDialog()
    {
        OpenProfileDialogForTest();
    }

    private void ShowModal(UIElement modal)
    {
        _modalHost.Children.Clear();
        _modalHost.Children.Add(modal);
        _modalHost.Visibility = Visibility.Visible;
    }

    private void CloseModal()
    {
        ActiveSubmitDialog = null;
        ActiveDiffDrawer = null;
        _modalHost.Visibility = Visibility.Collapsed;
        _modalHost.Children.Clear();
    }
}
