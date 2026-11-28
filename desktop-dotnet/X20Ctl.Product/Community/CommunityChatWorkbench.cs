using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

/// <summary>
/// Discord-style 3-column community Chat tab: channels &amp; DMs (left), live message feed with
/// replies/reactions/role colors (center), and live online/offline claimed member roster (right).
/// </summary>
public sealed class CommunityChatWorkbench : Grid
{
    private static readonly string[] QuickEmojis = ["🔥", "❤️", "🎮", "👍"];

    private readonly CommunityClient _client;
    private IReadOnlyDictionary<string, BadgeDef> _badgeCatalog;
    private CreatorProfile? _myProfile;

    private readonly ConcurrentDictionary<string, byte[]> _avatarCache = new(StringComparer.OrdinalIgnoreCase);

    private string _activeChannel = "general";
    private string? _activePeerUid;
    private CreatorProfile? _activePeerProfile;
    private LoungeMessage? _replyTo;
    private ChatChannelResponse? _lastChat;
    private CommunityMembersResponse? _lastMembers;
    private string _lastRenderSig = string.Empty;
    private bool _refreshInFlight;

    private DispatcherTimer? _pollTimer;

    // UI Regions
    private readonly StackPanel _channelsStack = new();
    private readonly StackPanel _feedHeaderRight = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _feedTitle = Kit.Text("# general", 15, "DS.Text", FontWeights.Bold);
    private readonly TextBlock _feedTopic = Kit.Text("General X20CTL discussion and hanging out", 11.5, "DS.TextMuted");
    private readonly ScrollViewer _messagesScroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly StackPanel _messagesStack = new();
    private readonly Border _replyBanner = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _replyBannerText = Kit.Text("", 11.5, "DS.AccentHi", FontWeights.SemiBold);
    private readonly TextBox _composerBox;
    private readonly Button _sendBtn;
    private readonly Button _signInPromptBtn;
    private readonly TextBlock _composerHint = Kit.Text("", 11, "DS.TextMuted");
    private readonly StackPanel _rosterStack = new();

    public event Action<CreatorProfile>? MemberClicked;
    public event Action? SignInRequested;
    public event Action<int, int>? RosterUpdated;

    public CommunityChatWorkbench(
        CommunityClient client,
        CreatorProfile? myProfile,
        IReadOnlyDictionary<string, BadgeDef> badgeCatalog)
    {
        _client = client;
        _myProfile = myProfile;
        _badgeCatalog = badgeCatalog;

        _composerBox = CommunityUI.RectInput("", 36);
        _sendBtn = CommunityUI.RectButton("Send", RectBtnKind.Primary, 36, 12);
        _signInPromptBtn = CommunityUI.RectButton("Claim Handle to Chat", RectBtnKind.Primary, 34, 12);

        BuildLayout();
        Unloaded += (_, _) => StopPolling();
    }

    public void SetCurrentProfile(CreatorProfile? profile, IReadOnlyDictionary<string, BadgeDef> badgeCatalog)
    {
        _myProfile = profile;
        _badgeCatalog = badgeCatalog;
        UpdateComposerState();
    }

    public void StartPolling()
    {
        if (ReviewSandbox.Active) return;
        if (_pollTimer == null)
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _pollTimer.Tick += async (_, _) =>
            {
                if (IsVisible && !_client.IsNetworkDisabled)
                    await RefreshAsync();
            };
        }
        if (!_pollTimer.IsEnabled)
            _pollTimer.Start();
    }

    public void StopPolling()
    {
        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer = null;
        }
    }

    public async Task OpenDirectMessageAsync(CreatorProfile peer)
    {
        _activeChannel = "dm";
        _activePeerUid = peer.Uid;
        _activePeerProfile = peer;
        _replyTo = null;
        UpdateReplyBanner();
        _lastRenderSig = string.Empty;
        await RefreshAsync();
    }

    public void ApplySnapshotForTest(ChatChannelResponse chat, CommunityMembersResponse members)
    {
        _lastChat = chat;
        _lastMembers = members;
        RenderAll(forceScrollBottom: true);
    }

    public async Task RefreshAsync()
    {
        if (_refreshInFlight) return;
        _refreshInFlight = true;
        try
        {
            if (_client.IsNetworkDisabled)
            {
                _composerHint.Text = "Offline mode active. Reconnect in Tools to send messages.";
                UpdateComposerState();
                return;
            }

            var chatTask = _client.GetChatAsync(_activeChannel, _activePeerUid, 60);
            var membersTask = _client.GetMembersAsync();
            await Task.WhenAll(chatTask, membersTask);

            bool changed = false;
            if (chatTask.Result.Ok && chatTask.Result.Data != null)
            {
                _lastChat = chatTask.Result.Data;
                changed = true;
            }
            if (membersTask.Result.Ok && membersTask.Result.Data != null)
            {
                _lastMembers = membersTask.Result.Data;
                RosterUpdated?.Invoke(_lastMembers.OnlineCount, _lastMembers.TotalMembers);
                changed = true;
            }

            if (changed)
                RenderAll(forceScrollBottom: false);
            else if (!chatTask.Result.Ok)
                _composerHint.Text = chatTask.Result.Error ?? "Chat unavailable right now.";
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private void BuildLayout()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) });

        // Column 0: Channels & DMs Sidebar
        var leftSidebar = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(CommunityUI.BgSidebar),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _channelsStack
            }
        };
        Children.Add(leftSidebar);

        // Column 2: Center Chat Panel (Header + Scrolling Messages + Reply Bar + Composer)
        var centerBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(CommunityUI.BgCard),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14)
        };
        var centerGrid = new Grid();
        centerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        centerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        centerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Top Channel Header
        var headerDock = new DockPanel();
        var refreshBtn = CommunityUI.RectButton("Refresh", RectBtnKind.Secondary, 28, 11);
        refreshBtn.Margin = new Thickness(8, 0, 0, 0);
        refreshBtn.Click += (_, _) => _ = RefreshAsync();
        _feedHeaderRight.Children.Add(refreshBtn);
        DockPanel.SetDock(_feedHeaderRight, Dock.Right);
        headerDock.Children.Add(_feedHeaderRight);

        var titleStack = new StackPanel();
        titleStack.Children.Add(_feedTitle);
        titleStack.Children.Add(_feedTopic.Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new Thickness(0, 2, 0, 0); }));
        headerDock.Children.Add(titleStack);

        var headerBar = new Border
        {
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = headerDock
        };
        centerGrid.Children.Add(headerBar);

        // Scrolling Messages
        _messagesScroller.Content = _messagesStack;
        _messagesScroller.Padding = new Thickness(0, 0, 6, 6);
        Grid.SetRow(_messagesScroller, 1);
        centerGrid.Children.Add(_messagesScroller);

        // Composer Footer
        var footerStack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        _replyBanner.CornerRadius = new CornerRadius(6);
        _replyBanner.Background = new SolidColorBrush(Color.FromRgb(16, 28, 52));
        _replyBanner.BorderBrush = new SolidColorBrush(CommunityUI.BorderFocus);
        _replyBanner.BorderThickness = new Thickness(1);
        _replyBanner.Padding = new Thickness(10, 6, 10, 6);
        _replyBanner.Margin = new Thickness(0, 0, 0, 6);
        var replyDock = new DockPanel();
        var cancelReplyBtn = CommunityUI.RectButton("Cancel", RectBtnKind.Ghost, 22, 10.5);
        cancelReplyBtn.Click += (_, _) => { _replyTo = null; UpdateReplyBanner(); };
        DockPanel.SetDock(cancelReplyBtn, Dock.Right);
        replyDock.Children.Add(cancelReplyBtn);
        replyDock.Children.Add(_replyBannerText.Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; }));
        _replyBanner.Child = replyDock;
        footerStack.Children.Add(_replyBanner);

        var composeDock = new DockPanel();
        _sendBtn.Tag = "chat:send";
        _sendBtn.Margin = new Thickness(8, 0, 0, 0);
        _sendBtn.Click += async (_, _) => await SendCurrentMessageAsync();
        DockPanel.SetDock(_sendBtn, Dock.Right);

        _signInPromptBtn.Visibility = Visibility.Collapsed;
        _signInPromptBtn.Margin = new Thickness(8, 0, 0, 0);
        _signInPromptBtn.Click += (_, _) => SignInRequested?.Invoke();
        DockPanel.SetDock(_signInPromptBtn, Dock.Right);

        _composerBox.Tag = "chat:input";
        _composerBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendCurrentMessageAsync();
            }
        };

        composeDock.Children.Add(_signInPromptBtn);
        composeDock.Children.Add(_sendBtn);
        composeDock.Children.Add(_composerBox);
        footerStack.Children.Add(composeDock);
        footerStack.Children.Add(_composerHint.Also(t => t.Margin = new Thickness(0, 5, 0, 0)));

        Grid.SetRow(footerStack, 2);
        centerGrid.Children.Add(footerStack);

        centerBorder.Child = centerGrid;
        Grid.SetColumn(centerBorder, 2);
        Children.Add(centerBorder);

        // Column 4: Online & Claimed Member Roster Sidebar
        var rightSidebar = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(CommunityUI.BgSidebar),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _rosterStack
            }
        };
        Grid.SetColumn(rightSidebar, 4);
        Children.Add(rightSidebar);

        RenderChannelsSidebar();
        UpdateComposerState();
    }

    private void RenderAll(bool forceScrollBottom)
    {
        RenderChannelsSidebar();
        RenderFeedHeader();
        RenderMessages(forceScrollBottom);
        RenderRosterSidebar();
        UpdateComposerState();
    }

    private void RenderChannelsSidebar()
    {
        _channelsStack.Children.Clear();
        _channelsStack.Children.Add(Kit.Text("TEXT CHANNELS", 10, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(4, 2, 0, 6)));

        var channels = _lastChat?.Channels is { Count: > 0 }
            ? _lastChat.Channels
            : new List<ChatChannelInfo>
            {
                new() { Id = "general", Name = "general", Topic = "General X20CTL discussion and hanging out" },
                new() { Id = "setups-help", Name = "setups-help", Topic = "Controller curves, deadzones, and PC mode help" },
                new() { Id = "screen-art", Name = "screen-art", Topic = "160x80 screen displays, GIFs, and artwork" },
                new() { Id = "announcements", Name = "announcements", Topic = "Official X20CTL drops and changelog notes", OwnerOnly = true }
            };

        foreach (var ch in channels)
        {
            bool active = _activeChannel == ch.Id && string.IsNullOrEmpty(_activePeerUid);
            string label = ch.OwnerOnly ? $"# {ch.Name} (Lock)" : $"# {ch.Name}";
            var btn = CommunityUI.SidebarRowButton(label, active);
            btn.Tag = $"chat:channel:{ch.Id}";
            btn.ToolTip = ch.Topic;
            var capturedId = ch.Id;
            btn.Click += async (_, _) =>
            {
                _activeChannel = capturedId;
                _activePeerUid = null;
                _activePeerProfile = null;
                _replyTo = null;
                UpdateReplyBanner();
                _lastRenderSig = string.Empty;
                await RefreshAsync();
            };
            _channelsStack.Children.Add(btn);
        }

        _channelsStack.Children.Add(Kit.Text("DIRECT MESSAGES", 10, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(4, 14, 0, 6)));

        var peers = new List<CreatorProfile>();
        if (_lastChat?.DmPeers != null) peers.AddRange(_lastChat.DmPeers);
        if (_activePeerProfile != null && peers.All(p => p.Uid != _activePeerProfile.Uid))
            peers.Insert(0, _activePeerProfile);

        if (peers.Count == 0)
        {
            _channelsStack.Children.Add(Kit.Text("Click any member on the right to start a DM.", 11, "DS.TextMuted")
                .Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(4, 2, 4, 0); }));
        }
        else
        {
            foreach (var peer in peers)
            {
                bool active = _activeChannel == "dm" && _activePeerUid == peer.Uid;
                var row = BuildSidebarMemberBtn(peer, active);
                var captured = peer;
                row.Click += async (_, _) => await OpenDirectMessageAsync(captured);
                _channelsStack.Children.Add(row);
            }
        }
    }

    private Button BuildSidebarMemberBtn(CreatorProfile peer, bool active)
    {
        var inner = new DockPanel();
        var dot = new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(peer.IsOnline ? Color.FromRgb(34, 197, 94) : Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(dot, Dock.Left);
        inner.Children.Add(dot);
        inner.Children.Add(new TextBlock
        {
            Text = $"@{peer.Handle}",
            FontSize = 12,
            FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ParseColor(peer.RoleColor, Colors.White)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var baseBg = active ? Color.FromRgb(20, 34, 62) : Colors.Transparent;
        var btn = new Button
        {
            Height = 30,
            MinHeight = 30,
            Margin = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(8, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(baseBg),
            BorderBrush = new SolidColorBrush(active ? Color.FromArgb(90, 56, 189, 248) : Colors.Transparent),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Content = inner
        };
        return btn;
    }

    private void RenderFeedHeader()
    {
        while (_feedHeaderRight.Children.Count > 1)
            _feedHeaderRight.Children.RemoveAt(0);

        if (_activeChannel == "dm" && _activePeerUid != null)
        {
            var peer = _activePeerProfile
                ?? _lastChat?.DmPeers.FirstOrDefault(p => p.Uid == _activePeerUid)
                ?? FindMember(_activePeerUid);
            string handle = peer?.Handle ?? _activePeerUid;
            _feedTitle.Text = $"@ {handle}";
            _feedTitle.Foreground = new SolidColorBrush(ParseColor(peer?.RoleColor, Colors.White));
            _feedTopic.Text = peer?.IsOnline == true
                ? $"Direct Message - Online ({peer.RoleTitle})"
                : $"Direct Message - Offline ({peer?.RoleTitle ?? "Member"})";

            if (peer != null)
            {
                var profBtn = CommunityUI.RectButton("Profile", RectBtnKind.Secondary, 28, 11);
                profBtn.Click += (_, _) => MemberClicked?.Invoke(peer);
                _feedHeaderRight.Children.Insert(0, profBtn);
            }
        }
        else
        {
            var info = _lastChat?.Channels.FirstOrDefault(c => c.Id == _activeChannel);
            _feedTitle.Text = $"# {info?.Name ?? _activeChannel}";
            _feedTitle.Foreground = DS.Brush("DS.Text");
            _feedTopic.Text = info?.Topic ?? "X20CTL community channel";

            if (_lastChat != null)
            {
                if (_lastChat.Paused)
                    _feedHeaderRight.Children.Insert(0, CommunityUI.SquareTag("PAUSED", Color.FromRgb(254, 202, 202), Color.FromArgb(80, 185, 28, 28)));
                else if (_lastChat.SlowmodeSeconds > 0)
                    _feedHeaderRight.Children.Insert(0, CommunityUI.SquareTag($"SLOWMODE {_lastChat.SlowmodeSeconds}s"));
            }
        }
    }

    private void RenderMessages(bool forceScrollBottom)
    {
        var msgs = _lastChat?.Messages ?? new List<LoungeMessage>();
        string sig = string.Join("|", msgs.Select(m => $"{m.Id}:{m.Pinned}:{string.Join(",", m.Reactions.Select(r => $"{r.Key}={r.Value.Count}"))}"));
        if (!forceScrollBottom && sig == _lastRenderSig && _messagesStack.Children.Count > 0)
            return;
        _lastRenderSig = sig;

        _messagesStack.Children.Clear();
        if (msgs.Count == 0)
        {
            _messagesStack.Children.Add(CommunityUI.CardSurface(
                Kit.Text(
                    _activeChannel == "dm"
                        ? "Start of your direct message history. Say hello!"
                        : $"No messages in #{_activeChannel} yet. Break the ice!",
                    12.5,
                    "DS.TextMuted"),
                new Thickness(16)));
            return;
        }

        // Messages come newest-first or oldest-first from server? Show oldest at top, newest at bottom like Discord.
        var ordered = msgs.OrderBy(m => m.CreatedAt, StringComparer.Ordinal).ToList();
        foreach (var msg in ordered)
            _messagesStack.Children.Add(BuildMessageCard(msg));

        if (!ReviewSandbox.Active)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => _messagesScroller.ScrollToEnd()));
    }

    private Border BuildMessageCard(LoungeMessage m)
    {
        var row = new DockPanel();
        var creator = m.Creator;
        string handle = creator?.Handle ?? m.Uid;
        Color roleColor = ParseColor(creator?.RoleColor, Color.FromRgb(226, 232, 240));

        // 32x32 Avatar (moving GIF for Supporters/Owner)
        var avatar = new AnimatedMediaView(34, 34, pixelArt: false, cornerRadius: 6)
        {
            Margin = new Thickness(0, 2, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(creator?.IsOwner == true || creator?.IsSupporter == true ? roleColor : CommunityUI.BorderSubtle)
        };
        LoadAvatarInto(avatar, creator, handle);
        DockPanel.SetDock(avatar, Dock.Left);
        row.Children.Add(avatar);

        var bodyStack = new StackPanel();

        // Top line: colored @handle + flair badge + timestamp + pinned tag
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        var handleLabel = new TextBlock
        {
            Text = $"@{handle}",
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(roleColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var handleBtn = new Button
        {
            Content = handleLabel,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (creator != null)
            handleBtn.Click += (_, _) => MemberClicked?.Invoke(creator);
        head.Children.Add(handleBtn);

        if (creator != null)
        {
            var flair = BadgeChip.CreateFlair(creator, _badgeCatalog, compact: true);
            if (flair != null) head.Children.Add(flair);
        }
        if (m.Pinned)
            head.Children.Add(CommunityUI.SquareTag("PINNED", Color.FromRgb(253, 230, 138), Color.FromArgb(65, 245, 158, 11)));

        head.Children.Add(Kit.Text(FormatShortTime(m.CreatedAt), 10.5, "DS.TextMuted")
            .Also(t => { t.Margin = new Thickness(6, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; }));
        bodyStack.Children.Add(head);

        // Reply quote bar
        if (!string.IsNullOrEmpty(m.ReplyToId) && !string.IsNullOrEmpty(m.ReplyToBody))
        {
            bodyStack.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(CommunityUI.BorderFocus),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(7, 2, 4, 2),
                Margin = new Thickness(0, 2, 0, 4),
                Child = Kit.Text($"@{m.ReplyToHandle ?? "member"}: {m.ReplyToBody}", 11, "DS.TextMuted")
                    .Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis)
            });
        }

        // Message text
        bodyStack.Children.Add(Kit.Text(m.Body, 13, "DS.Text").Also(t => t.TextWrapping = TextWrapping.Wrap));

        // Reactions + compact action bar
        var bottomWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        if (m.Reactions != null)
        {
            foreach (var (emoji, uids) in m.Reactions)
            {
                if (uids == null || uids.Count == 0) continue;
                bool mine = uids.Contains(_client.Identity.Uid, StringComparer.OrdinalIgnoreCase);
                var rBtn = CommunityUI.RectButton($"{emoji} {uids.Count}", mine ? RectBtnKind.Primary : RectBtnKind.Secondary, 22, 10.5);
                rBtn.Padding = new Thickness(6, 0, 6, 0);
                rBtn.Margin = new Thickness(0, 0, 4, 2);
                var capEmoji = emoji;
                rBtn.Click += async (_, _) =>
                {
                    var res = await _client.ToggleReactionAsync(m.Id, capEmoji);
                    if (res.Ok) await RefreshAsync();
                };
                bottomWrap.Children.Add(rBtn);
            }
        }

        var replyBtn = CommunityUI.RectButton("Reply", RectBtnKind.Ghost, 22, 10.5);
        replyBtn.Padding = new Thickness(6, 0, 6, 0);
        replyBtn.Margin = new Thickness(0, 0, 2, 2);
        replyBtn.Click += (_, _) => { _replyTo = m; UpdateReplyBanner(); _composerBox.Focus(); };
        bottomWrap.Children.Add(replyBtn);

        foreach (var em in QuickEmojis)
        {
            var capEm = em;
            var qBtn = CommunityUI.RectButton(em, RectBtnKind.Ghost, 22, 10.5);
            qBtn.Padding = new Thickness(4, 0, 4, 0);
            qBtn.Margin = new Thickness(0, 0, 1, 2);
            qBtn.ToolTip = $"React with {em}";
            qBtn.Click += async (_, _) =>
            {
                var res = await _client.ToggleReactionAsync(m.Id, capEm);
                if (res.Ok) await RefreshAsync();
                else _composerHint.Text = res.Error ?? "Sign in first to react.";
            };
            bottomWrap.Children.Add(qBtn);
        }

        if (m.Uid == _client.Identity.Uid || _myProfile?.IsOwner == true)
        {
            var delBtn = CommunityUI.RectButton("Delete", RectBtnKind.Danger, 22, 10);
            delBtn.Padding = new Thickness(6, 0, 6, 0);
            delBtn.Margin = new Thickness(4, 0, 0, 2);
            delBtn.Click += async (_, _) =>
            {
                var d = await _client.DeleteChatMessageAsync(m.Id);
                if (d.Ok)
                {
                    _lastRenderSig = string.Empty;
                    await RefreshAsync();
                }
            };
            bottomWrap.Children.Add(delBtn);
        }

        bodyStack.Children.Add(bottomWrap);
        row.Children.Add(bodyStack);

        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(m.Pinned ? Color.FromArgb(40, 245, 158, 11) : Color.FromRgb(9, 15, 30)),
            BorderBrush = new SolidColorBrush(m.Pinned ? Color.FromRgb(245, 158, 11) : Color.FromArgb(110, 29, 43, 70)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Child = row
        };
    }

    private void RenderRosterSidebar()
    {
        _rosterStack.Children.Clear();
        var online = _lastMembers?.Online ?? new List<CreatorProfile>();
        var offline = _lastMembers?.Offline ?? new List<CreatorProfile>();

        _rosterStack.Children.Add(Kit.Text($"ONLINE - {online.Count}", 10, "DS.AccentHi", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(4, 2, 0, 6)));

        if (online.Count == 0)
        {
            _rosterStack.Children.Add(Kit.Text("No claimed members online right now.", 11, "DS.TextMuted")
                .Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(4, 0, 4, 8); }));
        }
        else
        {
            foreach (var member in online)
                _rosterStack.Children.Add(BuildRosterRow(member, isOnline: true));
        }

        _rosterStack.Children.Add(Kit.Text($"OFFLINE - {offline.Count}", 10, "DS.TextMuted", FontWeights.Bold)
            .Also(t => t.Margin = new Thickness(4, 12, 0, 6)));

        if (offline.Count == 0)
        {
            _rosterStack.Children.Add(Kit.Text("No offline claimed members.", 11, "DS.TextMuted")
                .Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(4, 0, 4, 0); }));
        }
        else
        {
            foreach (var member in offline)
                _rosterStack.Children.Add(BuildRosterRow(member, isOnline: false));
        }
    }

    private Button BuildRosterRow(CreatorProfile member, bool isOnline)
    {
        var roleColor = ParseColor(member.RoleColor, Color.FromRgb(226, 232, 240));
        var dock = new DockPanel();

        var avHost = new Grid { Width = 28, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
        var av = new AnimatedMediaView(26, 26, pixelArt: false, cornerRadius: 5);
        LoadAvatarInto(av, member, member.Handle);
        avHost.Children.Add(av);

        var statusDot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(isOnline ? Color.FromRgb(34, 197, 94) : Color.FromRgb(100, 116, 139)),
            BorderBrush = new SolidColorBrush(CommunityUI.BgSidebar),
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        avHost.Children.Add(statusDot);
        DockPanel.SetDock(avHost, Dock.Left);
        dock.Children.Add(avHost);

        var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = member.Handle,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(roleColor),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 112,
            VerticalAlignment = VerticalAlignment.Center
        });
        if (!string.IsNullOrEmpty(member.EquippedBadge) || member.IsOwner)
        {
            var badgeId = !string.IsNullOrEmpty(member.EquippedBadge) ? member.EquippedBadge : "amjad-verified";
            _badgeCatalog.TryGetValue(badgeId, out var def);
            nameRow.Children.Add(BadgeChip.CreateEmblem(def, badgeId, 14));
        }
        textCol.Children.Add(nameRow);
        textCol.Children.Add(Kit.Text(member.RoleTitle, 10, "DS.TextMuted").Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis));
        dock.Children.Add(textCol);

        var btn = new Button
        {
            Height = 40,
            MinHeight = 40,
            Margin = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(6, 3, 6, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Opacity = isOnline ? 1.0 : 0.72,
            Content = dock
        };
        btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(Color.FromArgb(150, 20, 34, 62));
        btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
        btn.Click += (_, _) => MemberClicked?.Invoke(member);
        return btn;
    }

    private void UpdateReplyBanner()
    {
        if (_replyTo == null)
        {
            _replyBanner.Visibility = Visibility.Collapsed;
            _replyBannerText.Text = string.Empty;
        }
        else
        {
            _replyBanner.Visibility = Visibility.Visible;
            _replyBannerText.Text = $"Replying to @{_replyTo.Creator?.Handle ?? _replyTo.Uid}: {_replyTo.Body}";
        }
    }

    private void UpdateComposerState()
    {
        bool signedIn = _myProfile != null;
        bool isOwnerAnnounceBlocked = _activeChannel == "announcements" && _myProfile?.IsOwner != true;
        bool paused = _lastChat?.Paused == true && _myProfile?.IsOwner != true;

        _signInPromptBtn.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        _sendBtn.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        _composerBox.IsEnabled = signedIn && !isOwnerAnnounceBlocked && !paused && !_client.IsNetworkDisabled;
        _sendBtn.IsEnabled = _composerBox.IsEnabled;

        if (!signedIn)
            _composerHint.Text = "Claim a handle to chat in channels and send direct messages.";
        else if (isOwnerAnnounceBlocked)
            _composerHint.Text = "Only the Verified Owner can post in #announcements.";
        else if (paused)
            _composerHint.Text = "Chat is currently paused by the Owner.";
        else if (_myProfile?.IsOwner == true || _myProfile?.IsSupporter == true)
            _composerHint.Text = $"{_myProfile.RoleTitle} perk active: 0s slowmode, colored handle, extended messages.";
        else
            _composerHint.Text = "Press Enter to send. Click any member on the right to view profile or DM.";
    }

    private async Task SendCurrentMessageAsync()
    {
        var text = _composerBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _sendBtn.IsEnabled = false;
        var res = await _client.PostChatAsync(
            text,
            _activeChannel,
            _activeChannel == "dm" ? _activePeerUid : null,
            _replyTo?.Id);
        _sendBtn.IsEnabled = true;

        if (res.Ok)
        {
            _composerBox.Text = string.Empty;
            _replyTo = null;
            UpdateReplyBanner();
            _lastRenderSig = string.Empty;
            await RefreshAsync();
        }
        else
        {
            _composerHint.Text = res.Error ?? "Could not send message.";
        }
    }

    private void LoadAvatarInto(AnimatedMediaView view, CreatorProfile? creator, string handle)
    {
        string initials = string.IsNullOrWhiteSpace(handle) ? "X2" : handle[..Math.Min(2, handle.Length)].ToUpperInvariant();
        var tint = ParseColor(creator?.RoleColor, BadgeChip.OwnerOrange);
        view.SetPlaceholder(initials, tint);

        if (creator == null || string.IsNullOrEmpty(creator.PfpUrl))
            return;

        if (_avatarCache.TryGetValue(creator.PfpUrl, out var cached))
        {
            view.LoadBytes(cached, initials);
            return;
        }

        var url = creator.PfpUrl;
        _ = Task.Run(async () =>
        {
            var bytes = await _client.DownloadAssetAsync(url);
            if (bytes != null)
            {
                _avatarCache[url] = bytes;
                _ = view.Dispatcher.BeginInvoke(() => view.LoadBytes(bytes, initials));
            }
        });
    }

    private CreatorProfile? FindMember(string uid) =>
        _lastMembers?.Online.FirstOrDefault(m => m.Uid == uid)
        ?? _lastMembers?.Offline.FirstOrDefault(m => m.Uid == uid);

    private static string FormatShortTime(string? iso)
    {
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return dto.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return "";
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return fallback;
    }
}
