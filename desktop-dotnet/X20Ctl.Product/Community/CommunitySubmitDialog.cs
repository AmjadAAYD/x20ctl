using System;
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
    private readonly CommunityClient _client;
    private readonly string _modelId;
    private readonly string _connectionMode;
    private readonly object? _payload;
    private readonly string? _shareCode;
    private readonly string? _remixSourceId;
    private readonly string? _fulfillRequestId;
    private byte[]? _mediaBytes;
    private string _mediaType = "none";
    private readonly AnimatedMediaView _oledPreview = new(160, 80, pixelArt: true, cornerRadius: 6);
    private readonly TextBlock _mediaBadge = Kit.Text("Screen Art 160x80 preview", 11.5, "DS.TextMuted");
    private readonly TextBlock _statusText = Kit.Text("", 12, "DS.TextSoft");

    public event Action<CommunityItem>? Submitted;
    public event Action? CloseRequested;

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
        string? fulfillRequestId = null)
    {
        _client = client;
        _modelId = CommunityCapabilities.NormalizeModelId(modelId);
        _connectionMode = connectionMode == "native" ? "native" : "pc-mode";
        _payload = payload;
        _shareCode = shareCode;
        _remixSourceId = remixSourceId;
        _fulfillRequestId = fulfillRequestId;
        _mediaBytes = initialMediaBytes;
        _mediaType = initialMediaType ?? "none";

        Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 16));
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource == this) CloseRequested?.Invoke(); };
        var caps = CommunityCapabilities.GetCapabilitySet(_modelId, _connectionMode);

        var card = new Border
        {
            Width = 600,
            MaxHeight = 700,
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
        header.Children.Add(Kit.Text("Publish to Workshop", 18, "DS.Text", FontWeights.Bold));
        root.Children.Add(header);

        root.Children.Add(Kit.Text(
            $"Target: {caps.ModelName} ({caps.ConnectionMode})  |  Reviewed in X20ADMIN before publishing to the catalog.",
            12, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 0, 0, 10); }));

        if (!string.IsNullOrEmpty(_remixSourceId))
        {
            root.Children.Add(Kit.Text($"Remix of item {_remixSourceId}", 12, "DS.AccentHi", FontWeights.SemiBold)
                .Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
        }

        var categoryCombo = new ComboBox { MinHeight = 32, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var cat in caps.Categories) categoryCombo.Items.Add(cat);
        categoryCombo.SelectedItem = caps.AllowsCategory(initialCategory) ? initialCategory : caps.Categories[0];

        var titleBox = CommunityUI.RectInput(initialTitle, 34);
        titleBox.Margin = new Thickness(0, 4, 0, 10);
        var descBox = CommunityUI.RectInput(initialDescription, 72, multiline: true);
        descBox.Margin = new Thickness(0, 4, 0, 10);
        var gameBox = CommunityUI.RectInput("", 32);
        gameBox.Margin = new Thickness(0, 4, 0, 10);

        var mediaPanel = new StackPanel
        {
            Visibility = string.Equals(categoryCombo.SelectedItem as string, "screen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _oledPreview.BorderBrush = new SolidColorBrush(CommunityUI.BorderSubtle);
        _oledPreview.BorderThickness = new Thickness(1);
        _oledPreview.Margin = new Thickness(0, 0, 14, 0);
        if (_mediaBytes != null) _oledPreview.LoadBytes(_mediaBytes, "No media");
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
            _mediaBytes = await File.ReadAllBytesAsync(dlg.FileName);
            bool isGif = _mediaBytes.Length >= 6 && _mediaBytes[0] == 'G' && _mediaBytes[1] == 'I' && _mediaBytes[2] == 'F';
            _mediaType = isGif ? "gif" : "image";
            _mediaBadge.Text = isGif ? "Format: GIF (animated)" : "Format: Image (PNG)";
            _oledPreview.LoadBytes(_mediaBytes);
        };
        previewRow.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { pickMediaBtn, _mediaBadge.Also(t => t.Margin = new Thickness(0, 6, 0, 0)) } });
        mediaPanel.Children.Add(previewRow);

        categoryCombo.SelectionChanged += (_, _) =>
        {
            mediaPanel.Visibility = string.Equals(categoryCombo.SelectedItem as string, "screen", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
        };

        root.Children.Add(Kit.Text("Category", 12, "DS.TextSoft"));
        root.Children.Add(categoryCombo);
        root.Children.Add(Kit.Text("Title", 12, "DS.TextSoft"));
        root.Children.Add(titleBox);
        root.Children.Add(Kit.Text("Description (no external URLs)", 12, "DS.TextSoft"));
        root.Children.Add(descBox);
        root.Children.Add(Kit.Text("Games (comma separated, optional)", 12, "DS.TextSoft"));
        root.Children.Add(gameBox);
        root.Children.Add(mediaPanel);

        var submitBtn = CommunityUI.RectButton("Submit for Review", RectBtnKind.Primary, 38, 13);
        submitBtn.Click += async (_, _) =>
        {
            var t = titleBox.Text.Trim();
            var d = descBox.Text.Trim();
            if (t.Length < 3) { _statusText.Text = "Title must be at least 3 characters."; return; }
            if (ExternalUrlRegex().IsMatch($"{t} {d}")) { _statusText.Text = "External URLs are blocked for community safety."; return; }
            var cat = categoryCombo.SelectedItem as string ?? "setup";
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
                payload: _payload,
                shareCode: _shareCode,
                mediaBytes: cat == "screen" ? _mediaBytes : null,
                mediaType: cat == "screen" ? _mediaType : "none",
                tags: gTags,
                gameTags: gTags,
                remixSourceId: _remixSourceId,
                fulfillRequestId: _fulfillRequestId);
            submitBtn.IsEnabled = true;
            if (res.Ok && res.Data != null)
            {
                _statusText.Text = $"Submitted! ID {res.Data.Id} queued in X20ADMIN.";
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
        Children.Add(card);
    }
}
