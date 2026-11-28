using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public sealed partial class CommunitySubmitDialog : Grid
{
    private sealed record CategoryChoice(string Key, string Label);

    private static readonly string[] PopularGames =
    [
        "Apex Legends", "Call of Duty", "Valorant", "Fortnite", "Rocket League", "EA FC", "Elden Ring"
    ];

    private readonly CommunityClient _client;
    private readonly string _modelId;
    private readonly string _connectionMode;
    private readonly Func<string, (object? Payload, string? ShareCode, byte[]? MediaBytes, string? MediaType)>? _exportForCategory;
    private object? _currentPayload;
    private string? _currentShareCode;
    private readonly string? _parentItemId;
    private readonly string? _remixSourceId;
    private readonly string? _fulfillRequestId;
    private byte[]? _mediaBytes;
    private string _mediaType = "none";
    private readonly AnimatedMediaView _oledPreview = new(160, 80, pixelArt: true, cornerRadius: 6);
    private readonly TextBlock _mediaBadge = Kit.Text("Screen Art 160x80 preview", 11.5, "DS.TextMuted");
    private readonly TextBlock _attachmentSummaryText = Kit.Text("", 11.5, "DS.AccentHi", FontWeights.SemiBold);
    private readonly TextBlock _statusText = Kit.Text("", 12, "DS.TextSoft");

    public ComboBox CategoryInput { get; }
    public TextBox TitleInput { get; }
    public TextBox DescriptionInput { get; }
    public TextBox GameInput { get; }
    public Button SubmitButton { get; }
    public string StatusText => _statusText.Text;
    public string AttachmentSummary => _attachmentSummaryText.Text;
    public bool HasMediaBytes => _mediaBytes is { Length: > 0 };

    public event Action<CommunityItem>? Submitted;
    public event Action? CloseRequested;

    private static string CategoryDisplayLabel(string key) => key switch
    {
        "curve" => "Analog Curve",
        "screen" => "Screen Art (160x80)",
        "rgb" => "RGB Lighting",
        "vibration" => "Vibration Preset",
        "macro" => "Macro Sequence",
        "pack" => "Curated Pack",
        _ => "Full Setup"
    };

    public void LoadMediaBytes(byte[] bytes)
    {
        _mediaBytes = bytes;
        bool isGif = bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F';
        _mediaType = isGif ? "gif" : "image";
        _mediaBadge.Text = isGif ? "Format: Animated GIF (160x80)" : "Format: Static PNG (160x80)";
        _oledPreview.LoadBytes(bytes, "160x80");
        UpdateAttachmentSummary("screen");
    }

    private void UpdateAttachmentSummary(string cat)
    {
        if (cat == "screen")
        {
            _attachmentSummaryText.Text = _mediaBytes is { Length: > 0 }
                ? $"Attached: 160x80 {(_mediaType == "gif" ? "Animated GIF" : "Static PNG")} ({Math.Max(1, _mediaBytes.Length / 1024)} KB)"
                : "Attached: Select a 160x80 PNG or GIF below";
            return;
        }
        if (cat == "curve")
        {
            var codeHint = !string.IsNullOrEmpty(_currentShareCode) ? $" ({_currentShareCode[..Math.Min(12, _currentShareCode.Length)]}...)" : "";
            _attachmentSummaryText.Text = $"Attached: Analog Curve + Share Code{codeHint}";
            return;
        }
        _attachmentSummaryText.Text = cat switch
        {
            "rgb" => "Attached: RGB Lighting configuration",
            "vibration" => "Attached: Vibration & Haptics preset",
            "macro" => "Attached: Paddle Macro sequence",
            "pack" => "Attached: Curated Pack profile",
            _ => "Attached: Full Controller Setup Snapshot"
        };
    }

    [GeneratedRegex(@"(https?://|www\.|discord\.gg/|[a-z0-9-]+\.(com|net|org|io|gg|xyz|ru|cn)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrlRegex();

    public CommunitySubmitDialog(
        CommunityClient client,
        string initialCategory,
        string modelId,
        string connectionMode,
        string? initialTitle = null,
        string? initialDescription = null,
        object? payload = null,
        string? shareCode = null,
        byte[]? initialMediaBytes = null,
        string? initialMediaType = null,
        string? remixSourceId = null,
        string? fulfillRequestId = null,
        Func<string, (object? Payload, string? ShareCode, byte[]? MediaBytes, string? MediaType)>? exportForCategory = null,
        string? parentItemId = null,
        IReadOnlyList<string>? initialGameTags = null,
        CreatorProfile? creatorProfile = null)
    {
        _client = client;
        _modelId = CommunityCapabilities.NormalizeModelId(modelId);
        _connectionMode = connectionMode == "native" ? "native" : "pc-mode";
        _exportForCategory = exportForCategory;
        _currentPayload = payload;
        _currentShareCode = shareCode;
        _parentItemId = parentItemId;
        _remixSourceId = remixSourceId;
        _fulfillRequestId = fulfillRequestId;
        _mediaBytes = initialMediaBytes;
        _mediaType = initialMediaType ?? "none";

        Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 16));
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == this) CloseRequested?.Invoke(); };
        var caps = CommunityCapabilities.GetCapabilitySet(_modelId, _connectionMode);

        var card = new Border
        {
            Width = 620,
            MaxHeight = 720,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle),
            Background = new SolidColorBrush(CommunityUI.BgCard),
            Padding = new Thickness(22)
        };

        var root = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var closeBtn = CommunityUI.RectButton("Close", RectBtnKind.Secondary, 30, 11.5);
        closeBtn.Click += (_, _) => CloseRequested?.Invoke();
        DockPanel.SetDock(closeBtn, Dock.Right);
        header.Children.Add(closeBtn);
        header.Children.Add(Kit.Text(!string.IsNullOrEmpty(_parentItemId) ? "Push Version Update" : "Publish to Workshop", 18, "DS.Text", FontWeights.Bold));
        root.Children.Add(header);

        string reviewLine = creatorProfile?.InstantApproval == true
            ? $"Target: {caps.ModelName} ({caps.ConnectionMode})  |  {creatorProfile.RoleTitle} Perk: Instant live publishing with zero cooldown."
            : $"Target: {caps.ModelName} ({caps.ConnectionMode})  |  Reviewed in X20ADMIN before publishing to the catalog.";
        root.Children.Add(Kit.Text(
            !string.IsNullOrEmpty(_parentItemId)
                ? $"Updating existing item {_parentItemId} (auto-increments v2+) on {caps.ModelName} ({caps.ConnectionMode})."
                : reviewLine,
            12, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 0, 0, 8); }));

        if (!string.IsNullOrEmpty(_remixSourceId))
        {
            root.Children.Add(Kit.Text($"Remix of item {_remixSourceId}", 12, "DS.AccentHi", FontWeights.SemiBold)
                .Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        }

        var categoryCombo = new ComboBox
        {
            MinHeight = 32,
            Margin = new Thickness(0, 4, 0, 8),
            DisplayMemberPath = "Label",
            SelectedValuePath = "Key",
            IsEnabled = string.IsNullOrEmpty(_parentItemId)
        };
        foreach (var cat in caps.Categories) categoryCombo.Items.Add(new CategoryChoice(cat, CategoryDisplayLabel(cat)));
        string activeCat = caps.AllowsCategory(initialCategory) ? initialCategory : caps.Categories[0];
        categoryCombo.SelectedValue = activeCat;
        CategoryInput = categoryCombo;

        var attachCard = CommunityUI.CardSurface(_attachmentSummaryText.Also(t => t.TextWrapping = TextWrapping.Wrap), new Thickness(10, 7, 10, 7), CommunityUI.BorderFocus)
            .Also(b => b.Margin = new Thickness(0, 0, 0, 10));

        var titleBox = CommunityUI.RectInput(initialTitle, 34);
        titleBox.Margin = new Thickness(0, 4, 0, 10);
        TitleInput = titleBox;
        var descBox = CommunityUI.RectInput(initialDescription, 72, multiline: true);
        descBox.Margin = new Thickness(0, 4, 0, 10);
        DescriptionInput = descBox;
        var gameBox = CommunityUI.RectInput(initialGameTags is { Count: > 0 } ? string.Join(", ", initialGameTags) : "", 32);
        gameBox.Margin = new Thickness(0, 4, 0, 6);
        GameInput = gameBox;

        var chipWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var g in PopularGames)
        {
            var gameName = g;
            var chip = CommunityUI.RectButton($"+ {gameName}", RectBtnKind.Secondary, 24, 10.5);
            chip.Tag = $"gamechip:{gameName}";
            chip.Padding = new Thickness(8, 0, 8, 0);
            chip.Margin = new Thickness(0, 0, 5, 5);
            chip.Click += (_, _) =>
            {
                var current = gameBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                int idx = current.FindIndex(x => string.Equals(x, gameName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) current.RemoveAt(idx);
                else if (current.Count < 5) current.Add(gameName);
                gameBox.Text = string.Join(", ", current);
            };
            chipWrap.Children.Add(chip);
        }

        var mediaPanel = new StackPanel
        {
            Visibility = string.Equals(activeCat, "screen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _oledPreview.BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle);
        _oledPreview.BorderThickness = new Thickness(1);
        _oledPreview.Margin = new Thickness(0, 0, 14, 0);
        if (_mediaBytes is { Length: > 0 }) LoadMediaBytes(_mediaBytes);
        else _oledPreview.SetPlaceholder("160x80");
        previewRow.Children.Add(_oledPreview);

        var pickMediaBtn = CommunityUI.RectButton("Choose 160x80 PNG or GIF", RectBtnKind.Secondary, 32, 12);
        pickMediaBtn.Click += async (_, _) =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PNG or Animated GIF (*.png;*.gif)|*.png;*.gif",
                Title = "Select 160x80 Screen Art Image or GIF"
            };
            if (dlg.ShowDialog() != true) return;
            LoadMediaBytes(await File.ReadAllBytesAsync(dlg.FileName));
        };
        previewRow.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { pickMediaBtn, _mediaBadge.Also(t => t.Margin = new Thickness(0, 6, 0, 0)) } });
        mediaPanel.Children.Add(previewRow);

        UpdateAttachmentSummary(activeCat);

        categoryCombo.SelectionChanged += (_, _) =>
        {
            var newCat = categoryCombo.SelectedValue as string ?? "setup";
            mediaPanel.Visibility = string.Equals(newCat, "screen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrEmpty(_parentItemId) && string.IsNullOrEmpty(_remixSourceId) && _exportForCategory != null)
            {
                var exp = _exportForCategory(newCat);
                _currentPayload = exp.Payload;
                _currentShareCode = exp.ShareCode;
                if (newCat == "screen" && _mediaBytes == null && exp.MediaBytes is { Length: > 0 })
                    LoadMediaBytes(exp.MediaBytes);
            }
            UpdateAttachmentSummary(newCat);
        };

        root.Children.Add(Kit.Text("Category", 12, "DS.TextSoft"));
        root.Children.Add(categoryCombo);
        root.Children.Add(attachCard);
        root.Children.Add(Kit.Text("Title", 12, "DS.TextSoft"));
        root.Children.Add(titleBox);
        root.Children.Add(Kit.Text("Description (no external URLs)", 12, "DS.TextSoft"));
        root.Children.Add(descBox);
        root.Children.Add(Kit.Text("Games (comma separated or quick tags)", 12, "DS.TextSoft"));
        root.Children.Add(gameBox);
        root.Children.Add(chipWrap);
        root.Children.Add(mediaPanel);

        var submitBtn = CommunityUI.RectButton(!string.IsNullOrEmpty(_parentItemId) ? "Push Update to Workshop" : "Submit to Workshop", RectBtnKind.Primary, 38, 13);
        SubmitButton = submitBtn;
        submitBtn.Click += async (_, _) =>
        {
            var t = titleBox.Text.Trim();
            var d = descBox.Text.Trim();
            if (t.Length < 3) { _statusText.Text = "Title must be at least 3 characters."; return; }
            if (ExternalUrlRegex().IsMatch($"{t} {d}")) { _statusText.Text = "External URLs are blocked for community safety."; return; }
            var cat = categoryCombo.SelectedValue as string ?? "setup";
            if (cat == "screen" && (_mediaBytes == null || _mediaBytes.Length == 0))
            {
                _statusText.Text = "Please choose a 160x80 PNG or GIF image first.";
                return;
            }
            var gTags = gameBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5).ToArray();
            submitBtn.IsEnabled = false;
            _statusText.Text = "Solving Proof-of-Work and uploading...";
            var res = await _client.SubmitItemAsync(
                title: t,
                description: d,
                category: cat,
                modelId: _modelId,
                connectionMode: _connectionMode,
                payload: _currentPayload,
                shareCode: _currentShareCode,
                mediaBytes: cat == "screen" ? _mediaBytes : null,
                mediaType: cat == "screen" ? _mediaType : "none",
                tags: gTags,
                gameTags: gTags,
                parentItemId: _parentItemId,
                remixSourceId: _remixSourceId,
                fulfillRequestId: _fulfillRequestId);
            submitBtn.IsEnabled = true;
            if (res.Ok && res.Data != null)
            {
                _statusText.Text = res.Data.Status == "approved"
                    ? "Published live to Workshop!"
                    : $"Submitted! ID {res.Data.Id} queued in X20ADMIN.";
                Submitted?.Invoke(res.Data);
            }
            else
            {
                _statusText.Text = res.Error ?? "Submission failed.";
            }
        };
        root.Children.Add(submitBtn);
        _statusText.Margin = new Thickness(0, 10, 0, 0);
        _statusText.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(_statusText);

        card.Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        CommunityUI.AnimateModalIn(card);
        Children.Add(card);
    }
}
