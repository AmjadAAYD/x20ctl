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
    private bool _ogWindowOpen = true;
    private CreatorProfile? _myProfile;
    private readonly Dictionary<string, BadgeDef> _badgeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommunityItem> _cachedItems = new();
    private readonly TextBlock _onlinePill = Kit.Text("Online: 0", 12, "DS.AccentHi", FontWeights.SemiBold);
    private readonly TextBlock _profileLabel = Kit.Text("Sign In / Join", 12.5, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock _statusLine = Kit.Text("Ready", 12, "DS.TextMuted");
    private readonly StackPanel _profileChipRow = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _feedStack = new();
    private readonly Dictionary<string, Button> _subTabButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Grid _modalHost = new() { Visibility = Visibility.Collapsed };
    private readonly TextBox _searchBox = new() { Width = 220, Padding = new Thickness(10, 6, 10, 6), FontSize = 12.5 };
    private readonly ComboBox _modelPicker = new() { MinWidth = 140, MinHeight = 32, FontSize = 12.5 };
    private readonly ComboBox _modePicker = new() { MinWidth = 110, MinHeight = 32, FontSize = 12.5 };
    private readonly ComboBox _sortPicker = new() { MinWidth = 115, MinHeight = 32, FontSize = 12.5 };
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

        BuildLayout();
    }

    public void SetPresetCategoryAndRefresh(string category)
    {
        _subTab = "explore";
        _categoryFilter = category;
        _ = RefreshAsync();
    }

    public void OpenShareDialog(string category, string? title = null, object? payload = null, string? shareCode = null, byte[]? mediaBytes = null, string? mediaType = null, string? remixSourceId = null, string? fulfillRequestId = null)
    {
        if (_client.IsNetworkDisabled)
        {
            _statusLine.Text = "Disable Offline Mode in Tools to submit creations to X20ADMIN.";
            return;
        }
        if (_myProfile == null)
        {
            OpenProfileDialog();
            return;
        }
        var dlg = new CommunitySubmitDialog(
            _client, category, _selectedModelId, _selectedMode,
            title, "", payload, shareCode, mediaBytes, mediaType, remixSourceId, fulfillRequestId);
        dlg.CloseRequested += CloseModal;
        dlg.Submitted += _ => { _statusLine.Text = "Submitted to X20ADMIN queue!"; };
        ShowModal(dlg);
    }

    private void BuildLayout()
    {
        Body.Margin = new Thickness(28, 6, 28, 18);
        Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header Bar
        var headerBar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var rightControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(rightControls, Dock.Right);

        var onlineBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 56, 189, 248)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _onlinePill
        };
        rightControls.Children.Add(onlineBorder);

        var profileBtn = new Button
        {
            Style = DS.Style("ActionButton"),
            Height = 34,
            MinHeight = 34,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Content = _profileChipRow
        };
        _profileChipRow.Children.Add(_profileLabel);
        profileBtn.Click += (_, _) => OpenProfileDialog();
        rightControls.Children.Add(profileBtn);

        var uploadBtn = new Button
        {
            Content = "+ Share to Community",
            Style = DS.Style("DS.ConsolePrimary"),
            Height = 34,
            MinHeight = 34,
            Padding = new Thickness(14, 0, 14, 0),
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 8, 0)
        };
        uploadBtn.Click += (_, _) =>
        {
            var cat = _categoryFilter == "all" ? "setup" : _categoryFilter;
            var exp = _exportCurrentForSubmit?.Invoke(cat, _selectedMode);
            OpenShareDialog(cat, payload: exp?.Payload, shareCode: exp?.ShareCode, mediaBytes: exp?.MediaBytes, mediaType: exp?.MediaType);
        };
        rightControls.Children.Add(uploadBtn);

        var refreshBtn = new Button { Content = "Refresh", Style = DS.Style("ActionButton"), Height = 34, MinHeight = 34, Padding = new Thickness(12, 0, 12, 0) };
        refreshBtn.Click += (_, _) => _ = RefreshAsync();
        rightControls.Children.Add(refreshBtn);

        headerBar.Children.Add(rightControls);

        var titleStack = new StackPanel();
        titleStack.Children.Add(Kit.Text("X20CTL Community Hub", 24, "DS.Text", FontWeights.Bold));
        titleStack.Children.Add(_statusLine);
        headerBar.Children.Add(titleStack);
        Body.Children.Add(headerBar);

        // Navigation + Capability Filter Bar
        var filterCard = Kit.Panel(null, null, null, BuildFilterToolbar(), new Thickness(12, 10, 12, 10));
        filterCard.Margin = new Thickness(0, 0, 0, 10);
        Grid.SetRow(filterCard, 1);
        Body.Children.Add(filterCard);

        // Long Scrollable Feed
        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 6, 12),
            Content = _feedStack
        };
        Grid.SetRow(scroller, 2);
        Body.Children.Add(scroller);

        Grid.SetRowSpan(_modalHost, 3);
        Panel.SetZIndex(_modalHost, 50);
        Body.Children.Add(_modalHost);

        Body.Loaded += (_, _) => _ = RefreshAsync();
    }

    private StackPanel BuildFilterToolbar()
    {
        var stack = new StackPanel();

        // Sub-tabs row
        _subTabButtons.Clear();
        var subTabRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        Button SubTabBtn(string id, string label)
        {
            var b = new Button
            {
                Content = label,
                Style = DS.Style(_subTab == id ? "DS.ConsolePrimary" : "ActionButton"),
                Height = 32,
                MinHeight = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 4)
            };
            _subTabButtons[id] = b;
            b.Click += (_, _) => { _subTab = id; _filterUid = null; UpdateSubTabStyles(); _ = RefreshAsync(); };
            return b;
        }
        subTabRow.Children.Add(SubTabBtn("explore", "Explore Catalog"));
        subTabRow.Children.Add(SubTabBtn("requests", "Requests & Challenges"));
        subTabRow.Children.Add(SubTabBtn("lounge", "Community Lounge"));
        subTabRow.Children.Add(SubTabBtn("social", "Leaderboard, Watching & Saved"));
        stack.Children.Add(subTabRow);

        // Controller Capability Matrix Selector + Search Row
        var filterRow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        _syncingSelectors = true;
        foreach (var m in CommunityCapabilities.Models) _modelPicker.Items.Add(m.Id);
        _modelPicker.SelectedItem = _selectedModelId;
        _modePicker.Items.Add("native");
        _modePicker.Items.Add("pc-mode");
        _modePicker.SelectedItem = _selectedMode;
        foreach (var s in new[] { "trending", "top", "new", "installs", "remixes" }) _sortPicker.Items.Add(s);
        _sortPicker.SelectedItem = _sort;
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
        _sortPicker.SelectionChanged += (_, _) =>
        {
            if (_syncingSelectors || _sortPicker.SelectedItem is not string s) return;
            _sort = s;
            _ = RefreshAsync();
        };

        var searchBtn = new Button { Content = "Search", Style = DS.Style("ActionButton"), Height = 32, MinHeight = 32, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(4, 0, 10, 0) };
        searchBtn.Click += (_, _) => { _searchQuery = _searchBox.Text.Trim(); _ = RefreshAsync(); };

        filterRow.Children.Add(Kit.Text("Controller:", 12, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 6, 0); }));
        filterRow.Children.Add(_modelPicker.Also(c => c.Margin = new Thickness(0, 0, 8, 0)));
        filterRow.Children.Add(_modePicker.Also(c => c.Margin = new Thickness(0, 0, 12, 0)));
        filterRow.Children.Add(_sortPicker.Also(c => c.Margin = new Thickness(0, 0, 12, 0)));
        filterRow.Children.Add(_searchBox);
        filterRow.Children.Add(searchBtn);
        stack.Children.Add(filterRow);

        return stack;
    }

    private void UpdateSubTabStyles()
    {
        foreach (var (id, btn) in _subTabButtons)
            btn.Style = DS.Style(_subTab == id ? "DS.ConsolePrimary" : "ActionButton");
    }

    public async Task RefreshAsync()
    {
        UpdateSubTabStyles();
        if (_client.IsNetworkDisabled)
        {
            _onlinePill.Text = "Offline Mode";
            _statusLine.Text = ReviewSandbox.Active
                ? "Review sandbox active (offline preview). Showing cached catalog if available."
                : "Offline Mode enabled in Tools. Network requests are paused.";
        }
        else
        {
            _statusLine.Text = "Syncing with X20ADMIN...";
        }

        try
        {
            // Refresh profile quietly if registered
            var profRes = await _client.GetProfileAsync(_client.Identity.Uid);
            if (profRes.Ok)
            {
                _myProfile = profRes.Data.Creator;
                UpdateProfileChip();
            }

            if (_subTab == "requests")
            {
                await RenderRequestsAndChallengesAsync();
                return;
            }
            if (_subTab == "lounge")
            {
                await RenderLoungeAsync();
                return;
            }
            if (_subTab == "social")
            {
                await RenderLeaderboardWatchingSavedAsync();
                return;
            }

            await RenderExploreAsync();
        }
        catch (Exception ex)
        {
            _statusLine.Text = $"Community sync interrupted: {ex.Message}";
            _feedStack.Children.Clear();
            var retryBtn = new Button { Content = "Retry Connection", Style = DS.Style("DS.ConsolePrimary"), Height = 32, MinHeight = 32, Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            retryBtn.Click += (_, _) => _ = RefreshAsync();
            _feedStack.Children.Add(Kit.Panel(null, null, null, new StackPanel
            {
                Children =
                {
                    Kit.Text("Community Feed Unavailable", 15, "DS.Text", FontWeights.SemiBold),
                    Kit.Text(ex.Message, 12.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 0); }),
                    retryBtn
                }
            }, new Thickness(16)));
        }
    }

    private void UpdateProfileChip()
    {
        _profileChipRow.Children.Clear();
        if (_myProfile == null)
        {
            _profileLabel.Text = "Claim Handle";
            _profileChipRow.Children.Add(_profileLabel);
            return;
        }
        _profileLabel.Text = _myProfile.Handle;
        _profileChipRow.Children.Add(_profileLabel);
        if (_myProfile.IsOwner) _profileChipRow.Children.Add(BadgeChip.CreateOwnerCheckmark(14));
        if (!string.IsNullOrEmpty(_myProfile.EquippedBadge) && _badgeMap.TryGetValue(_myProfile.EquippedBadge, out var bdef))
        {
            var pill = BadgeChip.Create(bdef, compact: true);
            pill.Margin = new Thickness(6, 0, 0, 0);
            _profileChipRow.Children.Add(pill);
        }
    }

    private async Task RenderExploreAsync()
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
            Uid = _filterUid
        });

        _feedStack.Children.Clear();
        if (!catRes.Ok || catRes.Data == null)
        {
            _statusLine.Text = catRes.Error ?? "Failed to load catalog.";
            _feedStack.Children.Add(Kit.Info("Could not reach X20CTL Community right now."));
            return;
        }

        var data = catRes.Data;
        _ogWindowOpen = data.OgV0026Open;
        _onlinePill.Text = $"Online Now: {data.OnlineCount}";
        _badgeMap.Clear();
        foreach (var b in data.BadgeCatalog) _badgeMap[b.Id] = b;
        UpdateProfileChip();

        _cachedItems.Clear();
        _cachedItems.AddRange(data.Items);
        _statusLine.Text = $"{caps.ModelName} ({caps.ConnectionMode}) | Showing {data.Items.Count} verified items";

        // Category pills (strictly gated by Capability Matrix!)
        var catPills = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        void AddCatPill(string key, string label)
        {
            var btn = new Button
            {
                Content = label,
                Style = DS.Style(_categoryFilter == key ? "DS.ConsolePrimary" : "ActionButton"),
                Height = 30,
                MinHeight = 30,
                Padding = new Thickness(11, 0, 11, 0),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 6, 4)
            };
            btn.Click += (_, _) => { _categoryFilter = key; _ = RefreshAsync(); };
            catPills.Children.Add(btn);
        }
        AddCatPill("all", "All Allowed");
        foreach (var cat in caps.Categories)
        {
            AddCatPill(cat, cat switch
            {
                "curve" => "Curves & Analog Studio",
                "screen" => "OLED Screen",
                "rgb" => "RGB Lighting",
                "vibration" => "Vibration",
                "macro" => "Macros",
                "pack" => "Curated Packs",
                _ => "Full Setups"
            });
        }
        if (_categoryFilter == "screen" && caps.AllowsCategory("screen"))
        {
            foreach (var (mk, ml) in new[] { ("all", "All Art"), ("image", "Images"), ("gif", "GIFs") })
            {
                var mb = new Button
                {
                    Content = ml,
                    Style = DS.Style(_mediaFilter == mk ? "DS.ConsolePrimary" : "ActionButton"),
                    Height = 30,
                    MinHeight = 30,
                    Padding = new Thickness(10, 0, 10, 0),
                    FontSize = 11.5,
                    Margin = new Thickness(8, 0, 4, 4)
                };
                mb.Click += (_, _) => { _mediaFilter = mk; _ = RefreshAsync(); };
                catPills.Children.Add(mb);
            }
        }
        _feedStack.Children.Add(catPills);

        // Pinned Owner Notes
        foreach (var note in data.Notes.Take(2))
        {
            _feedStack.Children.Add(BuildOwnerNoteBanner(note));
        }

        // Active Poll
        var activePoll = data.Polls.FirstOrDefault(p => !p.Closed);
        if (activePoll != null)
        {
            _feedStack.Children.Add(BuildPollCard(activePoll));
        }

        // "For Your Games" strip from matched GameTags
        var allGameTags = data.Items.SelectMany(i => i.GameTags).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        if (allGameTags.Count > 0)
        {
            var gameRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center };
            gameRow.Children.Add(Kit.Text("FOR YOUR GAMES:", 11, "DS.AccentHi", FontWeights.Bold).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 8, 4); }));
            foreach (var g in allGameTags)
            {
                var gBtn = new Button { Content = g, Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 4) };
                gBtn.Click += (_, _) => { _searchQuery = g; _searchBox.Text = g; _ = RefreshAsync(); };
                gameRow.Children.Add(gBtn);
            }
            _feedStack.Children.Add(gameRow);
        }

        if (!string.IsNullOrEmpty(_filterUid))
        {
            var clearBtn = new Button { Content = $"Showing uploads by {_filterUid} (Click to Clear)", Style = DS.Style("ActionButton"), Height = 30, Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            clearBtn.Click += (_, _) => { _filterUid = null; _ = RefreshAsync(); };
            _feedStack.Children.Add(clearBtn);
        }

        if (data.Items.Count == 0)
        {
            var emptyActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var shareFirst = new Button
            {
                Content = "+ Share Current Setup",
                Style = DS.Style("DS.ConsolePrimary"),
                Height = 34, MinHeight = 34, Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            shareFirst.Click += (_, _) =>
            {
                var cat = _categoryFilter == "all" ? "setup" : _categoryFilter;
                var exp = _exportCurrentForSubmit?.Invoke(cat, _selectedMode);
                OpenShareDialog(cat, payload: exp?.Payload, shareCode: exp?.ShareCode, mediaBytes: exp?.MediaBytes, mediaType: exp?.MediaType);
            };
            emptyActions.Children.Add(shareFirst);
            if (_myProfile == null)
            {
                var claimBtn = new Button
                {
                    Content = "Claim Creator Handle",
                    Style = DS.Style("ActionButton"),
                    Height = 34, MinHeight = 34, Padding = new Thickness(14, 0, 14, 0)
                };
                claimBtn.Click += (_, _) => OpenProfileDialog();
                emptyActions.Children.Add(claimBtn);
            }
            _feedStack.Children.Add(Kit.Panel(null, null, null, new StackPanel
            {
                Children =
                {
                    Kit.Text($"No approved creations yet for {caps.ModelName} ({caps.ConnectionMode})", 15.5, "DS.Text", FontWeights.SemiBold),
                    Kit.Text("Published curves, OLED screen art, RGB profiles, and full controller setups approved in X20ADMIN appear here automatically.", 12.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 0); }),
                    emptyActions
                }
            }, new Thickness(18)));
            return;
        }

        var cardsGrid = new WrapPanel();
        foreach (var item in data.Items)
        {
            cardsGrid.Children.Add(BuildItemCard(item));
        }
        _feedStack.Children.Add(cardsGrid);
    }

    private Border BuildOwnerNoteBanner(OwnerNote note)
    {
        var stack = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(Kit.Text("PINNED OWNER NOTE", 10.5, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 8, 0)));
        titleRow.Children.Add(Kit.Text("AmjadXCTL", 12, "DS.Text", FontWeights.Bold));
        titleRow.Children.Add(BadgeChip.CreateOwnerCheckmark(14));
        head.Children.Add(titleRow);
        stack.Children.Add(head);
        stack.Children.Add(Kit.Text(note.Title, 14.5, "DS.Text", FontWeights.SemiBold));
        stack.Children.Add(Kit.Text(note.Body, 12.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 8); }));

        var reacts = new StackPanel { Orientation = Orientation.Horizontal };
        Button ReactBtn(string key, string label, int count)
        {
            var b = new Button { Content = $"{label} {count}", Style = DS.Style("ActionButton"), Height = 28, MinHeight = 28, FontSize = 11.5, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 0) };
            b.Click += async (_, _) =>
            {
                var r = await _client.ReactNoteAsync(note.Id, key);
                if (r.Ok) await RefreshAsync();
            };
            return b;
        }
        reacts.Children.Add(ReactBtn("heart", "Heart", note.HeartsCount));
        reacts.Children.Add(ReactBtn("thumb", "Helpful", note.ThumbsCount));
        reacts.Children.Add(ReactBtn("fire", "Fire", note.FireCount));
        stack.Children.Add(reacts);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(45, 255, 122, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 122, 24)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack
        };
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
            var btn = new Button
            {
                Content = $"{opt.Label}  ({opt.Votes} votes | {pct}%)",
                Style = DS.Style("ActionButton"),
                Height = 30,
                MinHeight = 30,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 0, 5)
            };
            btn.Click += async (_, _) =>
            {
                var r = await _client.VotePollAsync(poll.Id, idx);
                if (r.Ok) await RefreshAsync();
            };
            stack.Children.Add(btn);
        }
        return Kit.Panel(null, null, null, stack, new Thickness(14)).Also(b => b.Margin = new Thickness(0, 0, 0, 12));
    }

    private Border BuildItemCard(CommunityItem item)
    {
        var stack = new StackPanel();

        // Top Visual Zone (160x80 OLED Art Preview OR Setup/Curve Badge Header)
        if (item.Category == "screen" && !string.IsNullOrEmpty(item.AssetUrl))
        {
            var oled = new AnimatedMediaView(220, 110, pixelArt: true, cornerRadius: 10)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 52, 74)),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            oled.SetPlaceholder("Loading OLED...");
            _ = Task.Run(async () =>
            {
                var bytes = await _client.DownloadAssetAsync(item.AssetUrl, item.AssetSha256);
                if (bytes != null)
                    oled.Dispatcher.Invoke(() => oled.LoadBytes(bytes, "OLED Art"));
            });
            stack.Children.Add(oled);
            // Bottom explicit Images vs GIFs tag
            stack.Children.Add(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(80, 30, 58, 120)),
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 0, 0, 8),
                Child = Kit.Text(item.MediaType == "gif" ? "GIF" : "Image", 10.5, "DS.AccentHi", FontWeights.Bold)
            });
        }
        else
        {
            stack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                Children =
                {
                    MakeTagPill(item.Category.ToUpperInvariant()),
                    MakeTagPill($"{item.ModelId} | {item.ConnectionMode}"),
                    MakeTagPill($"v{item.Version}")
                }
            });
        }

        stack.Children.Add(Kit.Text(item.Title, 15, "DS.Text", FontWeights.Bold).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis));

        // Maker Row (Clickable for CreatorHoverCard!)
        if (item.Creator != null)
        {
            var makerRow = new StackPanel { Orientation = Orientation.Horizontal };
            makerRow.Children.Add(Kit.Text($"by {item.Creator.Handle}", 12, "DS.AccentHi", FontWeights.SemiBold));
            if (item.Creator.IsOwner) makerRow.Children.Add(BadgeChip.CreateOwnerCheckmark(13));
            if (!string.IsNullOrEmpty(item.Creator.EquippedBadge) && _badgeMap.TryGetValue(item.Creator.EquippedBadge, out var bdef))
            {
                var bc = BadgeChip.Create(bdef, compact: true);
                bc.Margin = new Thickness(6, 0, 0, 0);
                makerRow.Children.Add(bc);
            }
            var makerBtn = new Button
            {
                Content = makerRow,
                Style = DS.Style("ActionButton"),
                Height = 26,
                MinHeight = 26,
                Padding = new Thickness(6, 0, 8, 0),
                Margin = new Thickness(0, 4, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            makerBtn.Click += async (_, _) => await ShowCreatorCardAsync(item.Creator);
            stack.Children.Add(makerBtn);
        }

        stack.Children.Add(Kit.Text(item.Description, 12, "DS.TextSoft").Also(t =>
        {
            t.TextWrapping = TextWrapping.Wrap;
            t.MaxHeight = 42;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.Margin = new Thickness(0, 0, 0, 8);
        }));

        stack.Children.Add(Kit.Text($"Score {item.Score}  |  {item.Installs} installs  |  Works on {item.WorksCount} pads  |  {item.CommentsCount} comments", 11, "DS.TextMuted")
            .Also(t => t.Margin = new Thickness(0, 0, 0, 8)));

        // Action Row 1: Install, Compare/Test Live, Up/Down Vote, Works Great
        var row1 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        var installBtn = new Button { Content = "Install", Style = DS.Style("DS.ConsolePrimary"), Height = 30, MinHeight = 30, FontSize = 11.5, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 5, 4) };
        installBtn.Click += async (_, _) =>
        {
            await _client.RecordInstallAsync(item.Id);
            InstallItemRequested?.Invoke(item, Array.Empty<string>());
            _statusLine.Text = $"Installed '{item.Title}'.";
        };
        row1.Children.Add(installBtn);

        if (item.Category != "screen")
        {
            var cmpBtn = new Button { Content = "Compare", Style = DS.Style("ActionButton"), Height = 30, MinHeight = 30, FontSize = 11.5, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 5, 4) };
            cmpBtn.Click += (_, _) =>
            {
                var drawer = new CommunityDiffDrawer(item, _currentDraftSummary());
                drawer.InstallSelectedRequested += (it, secs) => { _ = _client.RecordInstallAsync(it.Id); InstallItemRequested?.Invoke(it, secs); };
                drawer.TestLiveRequested += (it, secs) => TestLiveRequested?.Invoke(it, secs);
                drawer.RevertLiveRequested += () => RevertLiveRequested?.Invoke();
                drawer.CloseRequested += CloseModal;
                ShowModal(drawer);
            };
            row1.Children.Add(cmpBtn);
        }

        var upBtn = new Button { Content = $"▲ {item.Upvotes}", Style = DS.Style("ActionButton"), Height = 30, MinHeight = 30, FontSize = 11.5, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 4) };
        upBtn.Click += async (_, _) => { var r = await _client.VoteItemAsync(item.Id, 1); if (r.Ok) await RefreshAsync(); else _statusLine.Text = r.Error ?? "Join first to vote."; };
        row1.Children.Add(upBtn);

        var worksBtn = new Button { Content = "Works Great", Style = DS.Style("ActionButton"), Height = 30, MinHeight = 30, FontSize = 11.5, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 4) };
        worksBtn.Click += async (_, _) => { var r = await _client.MarkWorksAsync(item.Id, _selectedModelId); if (r.Ok) await RefreshAsync(); else _statusLine.Text = r.Error ?? "Join first to verify."; };
        row1.Children.Add(worksBtn);
        stack.Children.Add(row1);

        // Action Row 2: Remix, Share Code, Bookmark, Comments, Report
        var row2 = new WrapPanel();
        if (!string.IsNullOrEmpty(item.ShareCode))
        {
            var codeBtn = new Button { Content = "Copy Code", Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 0) };
            codeBtn.Click += (_, _) => { Clipboard.SetText(item.ShareCode); _statusLine.Text = "Copied curve / setup share code."; };
            row2.Children.Add(codeBtn);
        }
        var remixBtn = new Button { Content = "Remix", Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 0) };
        remixBtn.Click += (_, _) =>
        {
            var exp = _exportCurrentForSubmit?.Invoke(item.Category, _selectedMode);
            OpenShareDialog(item.Category, $"Remix of {item.Title}", exp?.Payload ?? item.Payload, exp?.ShareCode ?? item.ShareCode, remixSourceId: item.Id);
        };
        row2.Children.Add(remixBtn);

        var saveBtn = new Button { Content = "Save", Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 0) };
        saveBtn.Click += async (_, _) => { var r = await _client.ToggleBookmarkAsync(item.Id); _statusLine.Text = r.Ok ? (r.Data ? "Bookmarked!" : "Removed bookmark.") : (r.Error ?? "Join first to bookmark."); };
        row2.Children.Add(saveBtn);

        var cmtBtn = new Button { Content = $"Comments ({item.CommentsCount})", Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 0) };
        cmtBtn.Click += async (_, _) => await ShowCommentsDialogAsync(item);
        row2.Children.Add(cmtBtn);

        var reportBtn = new Button { Content = "Report", Style = DS.Style("ActionButton"), Height = 26, MinHeight = 26, FontSize = 11, Padding = new Thickness(8, 0, 8, 0) };
        reportBtn.Click += async (_, _) => { var r = await _client.ReportAsync("item", item.Id, "Inappropriate or broken"); _statusLine.Text = r.Ok ? "Report sent to moderation." : (r.Error ?? "Join first to report."); };
        row2.Children.Add(reportBtn);
        stack.Children.Add(row2);

        return new Border
        {
            Width = 340,
            Background = new SolidColorBrush(Color.FromArgb(145, 13, 21, 42)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(75, 110, 160, 235)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 12, 12),
            Child = stack
        };
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

        var wrap = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 3, 6, 16)) };
        var closeOverlayBtn = new Button { Content = "Close Card", Style = DS.Style("ActionButton"), Height = 32, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        closeOverlayBtn.Click += (_, _) => CloseModal();
        wrap.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { card, closeOverlayBtn }
        });
        ShowModal(wrap);
    }

    private async Task ShowCommentsDialogAsync(CommunityItem item)
    {
        var listStack = new StackPanel();
        var res = await _client.GetCommentsAsync(item.Id);
        if (res.Ok && res.Data != null && res.Data.Count > 0)
        {
            foreach (var c in res.Data)
            {
                var line = new StackPanel();
                var h = new StackPanel { Orientation = Orientation.Horizontal };
                h.Children.Add(Kit.Text(c.Creator?.Handle ?? c.Uid, 12, "DS.AccentHi", FontWeights.Bold));
                if (c.Creator?.IsOwner == true) h.Children.Add(BadgeChip.CreateOwnerCheckmark(12));
                line.Children.Add(h);
                line.Children.Add(Kit.Text(c.Body, 12.5, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap));
                listStack.Children.Add(Kit.Panel(null, null, null, line, new Thickness(10)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }
        else
        {
            listStack.Children.Add(Kit.Text("No comments yet. Be the first to leave feedback.", 12.5, "DS.TextMuted"));
        }

        var box = new TextBox { Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 8, 8, 0) };
        var send = new Button { Content = "Post", Style = DS.Style("DS.ConsolePrimary"), Height = 34, MinWidth = 80, Margin = new Thickness(0, 8, 0, 0) };
        var status = Kit.Text("", 11.5, "DS.TextMuted");
        send.Click += async (_, _) =>
        {
            var r = await _client.AddCommentAsync(item.Id, box.Text.Trim());
            if (r.Ok)
            {
                CloseModal();
                await RefreshAsync();
            }
            else status.Text = r.Error ?? "Claim a handle first to comment.";
        };

        var closeBtn = new Button { Content = "Close", Style = DS.Style("ActionButton"), Height = 30 };
        closeBtn.Click += (_, _) => CloseModal();

        var root = new StackPanel
        {
            Children =
            {
                new DockPanel { Children = { closeBtn.Also(b => DockPanel.SetDock(b, Dock.Right)), Kit.Text($"Comments: {item.Title}", 16, "DS.Text", FontWeights.Bold) } }.Also(d => d.Margin = new Thickness(0, 0, 0, 12)),
                new ScrollViewer { MaxHeight = 320, Content = listStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                new DockPanel { Children = { send.Also(b => DockPanel.SetDock(b, Dock.Right)), box } },
                status.Also(t => t.Margin = new Thickness(0, 6, 0, 0))
            }
        };
        var sheet = new Border
        {
            MaxWidth = 560,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 96, 165, 250)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(10, 16, 30)),
            Padding = new Thickness(20),
            Child = root
        };
        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(195, 3, 6, 16)), Children = { sheet } };
        ShowModal(overlay);
    }

    private async Task RenderRequestsAndChallengesAsync()
    {
        _feedStack.Children.Clear();
        var reqRes = await _client.GetRequestsAsync();

        var createStack = new StackPanel();
        createStack.Children.Add(Kit.Text("Request a Setup, Curve, or OLED Screen Art", 14, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        var rTitle = new TextBox { Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
        var rGame = new TextBox { Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8) };
        var rBtn = new Button { Content = "Post Request", Style = DS.Style("DS.ConsolePrimary"), Height = 34, HorizontalAlignment = HorizontalAlignment.Left };
        rBtn.Click += async (_, _) =>
        {
            var res = await _client.CreateRequestAsync(rTitle.Text.Trim(), "Requested from X20CTL Community", "setup", _selectedModelId, _selectedMode, rGame.Text.Trim());
            if (res.Ok) await RefreshAsync();
            else _statusLine.Text = res.Error ?? "Claim a handle first to post requests.";
        };
        createStack.Children.Add(Kit.Text("Request Title", 11.5, "DS.TextSoft"));
        createStack.Children.Add(rTitle);
        createStack.Children.Add(Kit.Text("Target Game (optional)", 11.5, "DS.TextSoft"));
        createStack.Children.Add(rGame);
        createStack.Children.Add(rBtn);
        _feedStack.Children.Add(Kit.Panel(null, null, null, createStack, new Thickness(14)).Also(b => b.Margin = new Thickness(0, 0, 0, 12)));

        if (reqRes.Ok && reqRes.Data != null)
        {
            foreach (var r in reqRes.Data)
            {
                var row = new DockPanel();
                var actions = new StackPanel { Orientation = Orientation.Horizontal };
                DockPanel.SetDock(actions, Dock.Right);
                var voteBtn = new Button { Content = $"▲ {r.Upvotes}", Style = DS.Style("ActionButton"), Height = 30, Margin = new Thickness(0, 0, 6, 0) };
                voteBtn.Click += async (_, _) => { await _client.VoteRequestAsync(r.Id); await RefreshAsync(); };
                var fulfillBtn = new Button { Content = "Fulfill Request", Style = DS.Style("DS.ConsolePrimary"), Height = 30 };
                fulfillBtn.Click += (_, _) => OpenShareDialog(r.Category, r.Title, fulfillRequestId: r.Id);
                actions.Children.Add(voteBtn);
                actions.Children.Add(fulfillBtn);
                row.Children.Add(actions);

                row.Children.Add(new StackPanel
                {
                    Children =
                    {
                        Kit.Text($"{r.Title}  [{r.Status.ToUpperInvariant()}]", 14, "DS.Text", FontWeights.SemiBold),
                        Kit.Text($"{r.ModelId} / {r.ConnectionMode} | By {r.Creator?.Handle ?? r.Uid} | {r.GameTag}", 12, "DS.TextMuted")
                    }
                });
                _feedStack.Children.Add(Kit.Panel(null, null, null, row, new Thickness(12)).Also(b => b.Margin = new Thickness(0, 0, 0, 8)));
            }
        }
    }

    private async Task RenderLoungeAsync()
    {
        _feedStack.Children.Clear();
        var lRes = await _client.GetLoungeAsync();
        if (!lRes.Ok)
        {
            _feedStack.Children.Add(Kit.Info(lRes.Error ?? "Lounge unavailable right now."));
            return;
        }
        var (msgs, slowSec, paused) = lRes.Data;
        _feedStack.Children.Add(Kit.Text($"Community Lounge | Slow-mode: {slowSec}s | {(paused ? "PAUSED by Owner" : "Open")}", 13, "DS.AccentHi", FontWeights.SemiBold).Also(t => t.Margin = new Thickness(0, 0, 0, 10)));

        var composer = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var sendBtn = new Button { Content = "Send", Style = DS.Style("DS.ConsolePrimary"), Height = 36, MinWidth = 90 };
        DockPanel.SetDock(sendBtn, Dock.Right);
        var box = new TextBox { Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 8, 0) };
        sendBtn.Click += async (_, _) =>
        {
            var r = await _client.PostLoungeAsync(box.Text.Trim());
            if (r.Ok) await RefreshAsync();
            else _statusLine.Text = r.Error ?? "Message could not be posted.";
        };
        composer.Children.Add(sendBtn);
        composer.Children.Add(box);
        _feedStack.Children.Add(composer);

        foreach (var m in msgs.AsEnumerable().Reverse())
        {
            var mRow = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(Kit.Text(m.Creator?.Handle ?? m.Uid, 12.5, "DS.AccentHi", FontWeights.Bold));
            if (m.Creator?.IsOwner == true) top.Children.Add(BadgeChip.CreateOwnerCheckmark(13));
            mRow.Children.Add(top);
            mRow.Children.Add(Kit.Text(m.Body, 13, "DS.Text").Also(t => t.TextWrapping = TextWrapping.Wrap));
            _feedStack.Children.Add(Kit.Panel(null, null, null, mRow, new Thickness(10)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
        }
    }

    private async Task RenderLeaderboardWatchingSavedAsync()
    {
        _feedStack.Children.Clear();
        var lbRes = await _client.GetLeaderboardAsync("karma");
        var watchRes = await _client.GetWatchListsAsync();
        var savedRes = await _client.GetCatalogAsync(new CatalogQuery { BookmarkedBy = _client.Identity.Uid, Sort = "new" });
        var mineRes = await _client.GetCatalogAsync(new CatalogQuery { Uid = _client.Identity.Uid, Status = "mine", Sort = "new" });

        if (watchRes.Ok)
        {
            var wPanel = new StackPanel();
            wPanel.Children.Add(Kit.Text($"Watching ({watchRes.Data.Watching.Count}) & Watchers ({watchRes.Data.Watchers.Count})", 15, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
            var chips = new WrapPanel();
            foreach (var maker in watchRes.Data.Watching)
            {
                var btn = new Button { Content = $"Watching: {maker.Handle} (Karma {maker.Karma})", Style = DS.Style("ActionButton"), Height = 30, Margin = new Thickness(0, 0, 6, 6) };
                btn.Click += (_, _) => { _filterUid = maker.Uid; _subTab = "explore"; _ = RefreshAsync(); };
                chips.Children.Add(btn);
            }
            if (chips.Children.Count == 0)
                wPanel.Children.Add(Kit.Text("Not Watching any Makers yet. Click any creator handle to Watch their drops.", 12.5, "DS.TextMuted"));
            else
                wPanel.Children.Add(chips);
            _feedStack.Children.Add(Kit.Panel(null, null, null, wPanel, new Thickness(14)).Also(b => b.Margin = new Thickness(0, 0, 0, 12)));
        }

        if (savedRes.Ok && savedRes.Data != null && savedRes.Data.Items.Count > 0)
        {
            _feedStack.Children.Add(Kit.Text("Saved Bookmarks", 16, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 4, 0, 8)));
            var wrap = new WrapPanel();
            foreach (var sItem in savedRes.Data.Items) wrap.Children.Add(BuildItemCard(sItem));
            _feedStack.Children.Add(wrap);
        }

        if (mineRes.Ok && mineRes.Data != null && mineRes.Data.Items.Count > 0)
        {
            _feedStack.Children.Add(Kit.Text("My Community Uploads", 16, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 8, 0, 8)));
            foreach (var myItem in mineRes.Data.Items)
            {
                var delBtn = new Button { Content = "Delete", Style = DS.Style("ActionButton"), Height = 30 };
                delBtn.Click += async (_, _) =>
                {
                    var dRes = await _client.DeleteItemAsync(myItem.Id);
                    if (dRes.Ok) await RefreshAsync();
                };
                var row = new DockPanel();
                DockPanel.SetDock(delBtn, Dock.Right);
                row.Children.Add(delBtn);
                row.Children.Add(new StackPanel
                {
                    Children =
                    {
                        Kit.Text($"{myItem.Title}  [{myItem.Status.ToUpperInvariant()} | v{myItem.Version}]", 13.5, "DS.Text", FontWeights.SemiBold),
                        Kit.Text($"Category: {myItem.Category} | Score {myItem.Score} | Installs {myItem.Installs}", 12, "DS.TextMuted")
                    }
                });
                _feedStack.Children.Add(Kit.Panel(null, null, null, row, new Thickness(11)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }

        if (lbRes.Ok)
        {
            _feedStack.Children.Add(Kit.Text("Top Community Makers", 16, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new Thickness(0, 4, 0, 8)));
            int rank = 1;
            foreach (var maker in lbRes.Data.Makers)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                line.Children.Add(Kit.Text($"#{rank++}  {maker.Handle}", 14, "DS.Text", FontWeights.SemiBold));
                if (maker.IsOwner) line.Children.Add(BadgeChip.CreateOwnerCheckmark(14));
                line.Children.Add(Kit.Text($"   |   Karma {maker.Karma}   |   Watchers {maker.WatchersCount}   |   Installs {maker.InstallsReceived}", 12.5, "DS.TextSoft"));
                _feedStack.Children.Add(Kit.Panel(null, null, null, line, new Thickness(11)).Also(b => b.Margin = new Thickness(0, 0, 0, 6)));
            }
        }
    }

    private void OpenProfileDialog()
    {
        var dlg = new CreatorProfileDialog(_client, _myProfile, _badgeMap, _ogWindowOpen);
        dlg.CloseRequested += CloseModal;
        dlg.ProfileUpdated += p => { _myProfile = p; UpdateProfileChip(); _ = RefreshAsync(); };
        ShowModal(dlg);
    }

    private void ShowModal(UIElement modal)
    {
        _modalHost.Children.Clear();
        _modalHost.Children.Add(modal);
        _modalHost.Visibility = Visibility.Visible;
    }

    private void CloseModal()
    {
        _modalHost.Visibility = Visibility.Collapsed;
        _modalHost.Children.Clear();
    }

    private static Border MakeTagPill(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 30, 50, 90)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 96, 165, 250)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 6, 0),
            Child = Kit.Text(text, 10.5, "DS.Text", FontWeights.SemiBold)
        };
    }
}
