using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using X20Ctl.Product.Community;
using X20Ctl.Zone;
using X20Ctl.Zone.Community;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public partial class StudioScene
{
    public CommunityBrowserWorkbench? Community { get; private set; }
    private CommunityClient? _communityClient;
    private DispatcherTimer? _pulseTimer;
    private SetupSnapshot? _sandboxBackupSnapshot;
    private bool _pulseHooksBound;

    public CommunityClient EnsureCommunityClient()
    {
        CommunityClient.SandboxOfflineGuard = ReviewSandbox.Active;
        bool offline = pcLink?.Preferences?.CommunityOffline == true;
        if (_communityClient == null)
        {
            _communityClient = new CommunityClient
            {
                OfflineMode = offline
            };
        }
        else
        {
            _communityClient.OfflineMode = offline;
        }
        EnsurePulseTimer();
        return _communityClient;
    }

    private void EnsurePulseTimer()
    {
        if (_pulseHooksBound) return;
        _pulseHooksBound = true;

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _pulseTimer.Tick += async (_, _) => await RefreshPulseAsync();
        Loaded += async (_, _) =>
        {
            _pulseTimer.Start();
            await RefreshPulseAsync();
        };
        Unloaded += (_, _) => _pulseTimer.Stop();
    }

    public async Task RefreshPulseAsync()
    {
        var client = EnsureCommunityClient();
        bool isActive = Window.GetWindow(this)?.IsActive ?? true;
        if (client.IsNetworkDisabled || !isActive)
        {
            Dashboard?.UpdatePulse(null, true);
            return;
        }

        var res = await client.GetPulseAsync();
        if (res.Ok && res.Data != null && !res.Data.NotModified)
        {
            Dashboard?.UpdatePulse(res.Data, client.IsNetworkDisabled);
        }
    }

    public CommunityBrowserWorkbench EnsureCommunityWorkbench()
    {
        if (Community != null) return Community;

        var client = EnsureCommunityClient();
        string mode = Draft.PcMode ? "pc-mode" : "native";
        Community = new CommunityBrowserWorkbench(
            client,
            Model.Id,
            mode,
            BuildCurrentDraftSummary,
            ExportCurrentForCommunity);

        Community.InstallItemRequested += (item, sections) => _ = ApplyCommunityItemAsync(item, sections);
        Community.TestLiveRequested += (item, sections) =>
        {
            _sandboxBackupSnapshot ??= SetupSnapshot.Capture(Model.Id, Draft, Curves.Draft, macroDraft, vibrationDraft);
            _ = ApplyCommunityItemAsync(item, sections);
        };
        Community.RevertLiveRequested += () =>
        {
            if (_sandboxBackupSnapshot != null)
            {
                LoadSetup(_sandboxBackupSnapshot);
                _sandboxBackupSnapshot = null;
            }
        };

        return Community;
    }

    private void HookWorkbenchCommunityEvents()
    {
        Curves.ShareCurveRequested -= OnShareCurveRequested;
        Curves.ShareCurveRequested += OnShareCurveRequested;
    }

    private void OnShareCurveRequested()
    {
        ShowManagement("Community");
        var exp = ExportCurrentForCommunity("curve", Draft.PcMode ? "pc-mode" : "native");
        Community?.OpenShareDialog("curve", "Custom Curve / Analog Studio", exp.Payload, exp.ShareCode);
    }

    private string BuildCurrentDraftSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Controller: {Model.Name} ({(Draft.PcMode ? "PC Mode" : "Native")})");
        sb.AppendLine($"Button Remaps: {Draft.UnsentChanges}");
        sb.AppendLine($"Left Stick Curve: {Curves.Draft[CurveChannel.LeftStick].Preset}");
        sb.AppendLine($"Right Stick Curve: {Curves.Draft[CurveChannel.RightStick].Preset}");
        sb.AppendLine($"Vibration Strength: {vibrationDraft.Strength}%");
        return sb.ToString();
    }

    private (object? Payload, string? ShareCode, byte[]? MediaBytes, string? MediaType) ExportCurrentForCommunity(string category, string targetMode)
    {
        if (category == "curve")
        {
            var left = Curves.Draft[CurveChannel.LeftStick];
            var right = Curves.Draft[CurveChannel.RightStick];
            var tuning = pcLink?.Store.Get(PcModePage.StoreKey);
            var leftTuning = tuning?.Sticks.TryGetValue("LeftStick", out var lt) == true ? lt : new StickTuning();
            var setupCurve = new SetupCurve(left.Preset, left.BasePreset, left.Inner, left.Outer, left.Point1.X, left.Point1.Y, left.Point2.X, left.Point2.Y);
            string shareCode = StickLabShareCode.Encode(
                CurveChannel.LeftStick,
                setupCurve,
                leftTuning,
                tuning?.DPadSocd ?? SocdMode.Off,
                tuning?.LeftStickSocd ?? SocdMode.Off);
            return (new
            {
                leftPreset = left.Preset,
                leftInner = left.Inner,
                leftOuter = left.Outer,
                rightPreset = right.Preset,
                rightInner = right.Inner,
                rightOuter = right.Outer,
                deadzone = left.Inner,
                leftStick = leftTuning
            }, shareCode, null, "none");
        }
        if (category == "rgb")
        {
            return (new
            {
                rgb = rgbWindow != null ? $"#{rgbWindow.SelectedRgb:X6}" : "#38BDF8",
                mode = rgbWindow?.SelectedModeIndex ?? 0,
                brightness = rgbWindow?.SelectedLevelByte ?? 0x50
            }, null, null, "none");
        }
        if (category == "vibration")
        {
            return (new
            {
                strength = vibrationDraft.Strength
            }, null, null, "none");
        }
        if (category == "macro")
        {
            return (new
            {
                slot = Macros?.Slot ?? "M1",
                events = macroDraft[Macros?.Slot ?? "M1"].Count
            }, null, null, "none");
        }
        var snap = SetupSnapshot.Capture(Model.Id, Draft, Curves.Draft, macroDraft, vibrationDraft);
        return (snap, null, null, "none");
    }

    private async Task ApplyCommunityItemAsync(CommunityItem item, IReadOnlyList<string> sections)
    {
        bool all = sections.Count == 0;

        if (item.Category == "screen" && !string.IsNullOrEmpty(item.AssetUrl))
        {
            var client = EnsureCommunityClient();
            var bytes = await client.DownloadAssetAsync(item.AssetUrl, item.AssetSha256);
            if (bytes != null)
            {
                var dir = Path.Combine(AppDataPaths.RoamingRoot, "community-screens");
                Directory.CreateDirectory(dir);
                var ext = item.MediaType == "gif" ? "gif" : "png";
                var outPath = Path.Combine(dir, $"{item.Id}.{ext}");
                await File.WriteAllBytesAsync(outPath, bytes);
                ChangeCount.Text = $"Saved OLED art to {Path.GetFileName(outPath)}";
            }
            return;
        }

        if (item.Category == "curve")
        {
            if (!string.IsNullOrEmpty(item.ShareCode) && StickLabShareCode.TryDecode(item.ShareCode, out var decoded, out _) && decoded != null)
            {
                var ch = Enum.TryParse<CurveChannel>(decoded.Channel, true, out var parsed) ? parsed : CurveChannel.LeftStick;
                if (Curves.Draft.CanEdit && decoded.Curve is { } sc)
                {
                    int inner = Math.Clamp(sc.Inner, 0, 99);
                    int outer = Math.Clamp(sc.Outer, inner + 1, 100);
                    var next = new CurveSetting(sc.Preset, inner, outer, (Math.Clamp(sc.X1, 0, 255), Math.Clamp(sc.Y1, 0, 255)), (Math.Clamp(sc.X2, 0, 255), Math.Clamp(sc.Y2, 0, 255)))
                    {
                        BasePreset = sc.BasePreset
                    };
                    Curves.Draft.Set(ch, next);
                    Curves.Refresh();
                }
                if (pcLink is { } link && decoded.Tuning != null)
                {
                    try
                    {
                        var p = link.Store.Get(PcModePage.StoreKey);
                        p.Sticks[ch.ToString()] = decoded.Tuning;
                        p.DPadSocd = decoded.DPadSocd;
                        p.LeftStickSocd = decoded.LeftStickSocd;
                        p.Validate();
                        link.Store.Save(PcModePage.StoreKey, p);
                        link.Service.Reload();
                    }
                    catch
                    {
                        // Guard PC mode notice constraints cleanly
                    }
                }
            }
            ChangeCount.Text = $"Applied curve: {item.Title}";
            return;
        }

        if ((item.Category == "setup" || item.Category == "pack") && item.Payload is JsonElement payloadEl)
        {
            try
            {
                var snap = JsonSerializer.Deserialize<SetupSnapshot>(payloadEl.GetRawText(), SetupStore.JsonOptions);
                if (snap != null && snap.ModelId == Model.Id)
                {
                    if (all || sections.Contains("Buttons & Remaps"))
                        LoadSetup(snap);
                    var created = setupStore.Create(item.Title, snap);
                    setupStore.UpdateCommunityMetadata(created.Id, item.Description, item.Tags, item.Creator?.Handle, item.Uid, item.Id, item.Version, item.RemixSourceId);
                    Setups?.Refresh();
                    ChangeCount.Text = $"Saved setup '{item.Title}'";
                }
                else
                {
                    var fallbackSnap = SetupSnapshot.Capture(Model.Id, Draft, Curves.Draft, macroDraft, vibrationDraft);
                    var created = setupStore.Create(item.Title, fallbackSnap);
                    setupStore.UpdateCommunityMetadata(created.Id, item.Description, item.Tags, item.Creator?.Handle, item.Uid, item.Id, item.Version, item.RemixSourceId);
                    Setups?.Refresh();
                    ChangeCount.Text = $"Saved metadata for '{item.Title}'";
                }
            }
            catch
            {
                ChangeCount.Text = $"Saved metadata for '{item.Title}'";
            }
        }
    }
}
