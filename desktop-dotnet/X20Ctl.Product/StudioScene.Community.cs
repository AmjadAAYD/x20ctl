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
    private bool _windowPulseHooksBound;

    public CommunityClient EnsureCommunityClient()
    {
        WindowsIdentityVault.Install();
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

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _pulseTimer.Tick += async (_, _) =>
        {
            if (Application.Current?.MainWindow is { } mw &&
                (!mw.IsVisible || mw.WindowState == WindowState.Minimized) &&
                Community?.Body?.IsVisible != true)
            {
                return;
            }
            await RefreshPulseAsync();
        };
        Loaded += async (_, _) =>
        {
            if (!_windowPulseHooksBound && Application.Current?.MainWindow is { } mw)
            {
                _windowPulseHooksBound = true;
                mw.StateChanged += (_, _) =>
                {
                    if (mw.IsVisible && mw.WindowState != WindowState.Minimized)
                        _ = RefreshPulseAsync();
                };
                mw.IsVisibleChanged += (_, _) =>
                {
                    if (mw.IsVisible && mw.WindowState != WindowState.Minimized)
                        _ = RefreshPulseAsync();
                };
            }
            _pulseTimer.Start();
            await RefreshPulseAsync();
        };
        Unloaded += (_, _) => _pulseTimer.Stop();
        if (Application.Current != null)
        {
            Application.Current.Exit += (_, _) => SendOfflinePulseSync();
        }
    }

    internal void SendOfflinePulseSync()
    {
        try { _communityClient?.LeavePulseAsync().GetAwaiter().GetResult(); } catch { }
    }

    public async Task RefreshPulseAsync()
    {
        var client = EnsureCommunityClient();
        if (client.IsNetworkDisabled)
        {
            Dashboard?.UpdatePulse(null, true);
            await client.LeavePulseAsync();
            return;
        }

        var res = await client.GetPulseAsync();
        if (res.Ok && res.Data != null && !res.Data.NotModified)
        {
            Dashboard?.UpdatePulse(res.Data, false);
            Community?.ApplyPulse(res.Data);
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
        if (category == "screen")
        {
            if (Screen != null)
            {
                var (bytes, mType) = Screen.ExportCommunityMedia();
                if (bytes is { Length: > 0 })
                    return (new { frames = Screen.FrameCount, animated = Screen.IsAnimated }, null, bytes, mType);
            }
            return (null, null, null, "none");
        }
        if (category == "curve")
        {
            var left = Curves.Draft[CurveChannel.LeftStick];
            var right = Curves.Draft[CurveChannel.RightStick];
            var tuning = liveNativeProfile ?? pcLink?.Store.Get(PcModePage.StoreKey);
            var leftTuning = tuning?.Sticks.TryGetValue("LeftStick", out var lt) == true ? lt : new StickTuning();
            byte hwFlags = 0;
            if (leftTuning.Lab?.InvertX == true) hwFlags |= (byte)CurveHardwareFlags.InvertX;
            if (leftTuning.Lab?.InvertY == true) hwFlags |= (byte)CurveHardwareFlags.InvertY;
            if (tuning?.SwapSticks == true) hwFlags |= (byte)CurveHardwareFlags.SwapSticks;
            if (hwFlags == 0) hwFlags = left.Flags;
            var setupCurve = new SetupCurve(left.Preset, left.BasePreset, left.Inner, left.Outer, left.Point1.X, left.Point1.Y, left.Point2.X, left.Point2.Y);
            string shareCode = StickLabShareCode.Encode(
                CurveChannel.LeftStick,
                setupCurve,
                leftTuning,
                tuning?.DPadSocd ?? SocdMode.Off,
                tuning?.LeftStickSocd ?? SocdMode.Off,
                hwFlags);
            return (new
            {
                leftPreset = left.Preset,
                leftInner = left.Inner,
                leftOuter = left.Outer,
                rightPreset = right.Preset,
                rightInner = right.Inner,
                rightOuter = right.Outer,
                deadzone = left.Inner,
                flags = hwFlags,
                leftStick = leftTuning
            }, shareCode, null, "none");
        }
        if (category == "rgb")
        {
            return (new
            {
                rgb = rgbWindow != null ? $"#{rgbWindow.SelectedRgb:X6}" : "#38BDF8",
                mode = rgbWindow?.SelectedModeIndex ?? 0,
                brightness = rgbWindow?.SelectedLevelByte ?? (byte)0x50
            }, null, null, "none");
        }
        if (category == "vibration")
        {
            return (new
            {
                strength = vibrationDraft.Strength,
                left = vibrationDraft.Left,
                right = vibrationDraft.Right,
                floor = vibrationDraft.Floor
            }, null, null, "none");
        }
        if (category == "macro")
        {
            string slot = Macros?.Slot ?? (macroDraft.SlotNames.FirstOrDefault() ?? "M1");
            var events = macroDraft[slot].Select(e => new SetupMacro(e.Id, e.Buttons.ToArray(), e.Left, e.Right, e.HoldMs, e.PauseMs)).ToArray();
            var opts = macroDraft.Options(slot);
            return (new
            {
                slot,
                trigger = macroDraft.Trigger(slot),
                repeat = opts.RepeatCount,
                mode = opts.Mode.ToString(),
                events
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
                var scr = EnsureScreenWorkbench();
                scr.LoadImageBytes(bytes, Path.GetFileName(outPath));
                ChangeCount.Text = $"Loaded Screen Art '{item.Title}' ({scr.FrameCount}f)";
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
                        BasePreset = sc.BasePreset,
                        Flags = decoded.Flags
                    };
                    Curves.Draft.Set(ch, next);
                }
                if (decoded.Tuning != null || decoded.Flags != 0)
                {
                    try
                    {
                        var p = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : new PcModeProfile());
                        if (decoded.Tuning != null)
                        {
                            if (decoded.Tuning.AntiDeadzone > 0)
                                (decoded.Tuning.Lab ??= new StickLabSettings()).RadialAntiDeadzone = decoded.Tuning.AntiDeadzone;
                            p.Sticks[ch.ToString()] = decoded.Tuning;
                        }
                        if (decoded.Flags != 0)
                        {
                            if ((decoded.Flags & (byte)CurveHardwareFlags.SwapSticks) != 0) p.SwapSticks = true;
                            if ((decoded.Flags & (byte)(CurveHardwareFlags.InvertX | CurveHardwareFlags.InvertY)) != 0)
                            {
                                var st = p.Sticks.TryGetValue(ch.ToString(), out var ex) ? ex : (p.Sticks[ch.ToString()] = new StickTuning());
                                var lab = st.Lab ??= new StickLabSettings();
                                if ((decoded.Flags & (byte)CurveHardwareFlags.InvertX) != 0) lab.InvertX = true;
                                if ((decoded.Flags & (byte)CurveHardwareFlags.InvertY) != 0) lab.InvertY = true;
                            }
                        }
                        p.DPadSocd = decoded.DPadSocd;
                        p.LeftStickSocd = decoded.LeftStickSocd;
                        p.Validate();
                        ApplyNativeAndBoost(p, saveImmediate: false);
                    }
                    catch
                    {
                        // Guard PC mode notice constraints cleanly
                    }
                }
                Curves.Refresh();
                RefreshQuickStickControls?.Invoke();
                UpdateCurveContext();
            }
            ChangeCount.Text = $"Applied curve: {item.Title}";
            return;
        }

        if (item.Category == "rgb" && item.Payload is JsonElement rgbEl && rgbEl.ValueKind == JsonValueKind.Object)
        {
            string? hexStr = rgbEl.TryGetProperty("rgb", out var rProp) && rProp.ValueKind == JsonValueKind.String ? rProp.GetString() : null;
            int modeIdx = rgbEl.TryGetProperty("mode", out var mProp) && mProp.TryGetInt32(out int mVal) ? mVal : 0;
            byte levelByte = rgbEl.TryGetProperty("brightness", out var bProp) && bProp.TryGetInt32(out int bVal) ? (byte)Math.Clamp(bVal, 0, 255) : (byte)0x50;
            EnsureRgbWorkbench().ApplyCommunityPreset(hexStr, modeIdx, levelByte);
            ChangeCount.Text = $"Loaded RGB lighting: {item.Title}";
            return;
        }

        if (item.Category == "vibration" && item.Payload is JsonElement vibEl && vibEl.ValueKind == JsonValueKind.Object)
        {
            if (vibEl.TryGetProperty("strength", out var sProp) && sProp.TryGetInt32(out int sVal) && vibrationDraft.CanEdit)
            {
                vibrationDraft.Set(Math.Clamp(sVal, 0, vibrationDraft.Max));
                if (vibrationDraft.PcMode &&
                    vibEl.TryGetProperty("left", out var lProp) && lProp.TryGetInt32(out int lVal) &&
                    vibEl.TryGetProperty("right", out var rProp) && rProp.TryGetInt32(out int rVal) &&
                    vibEl.TryGetProperty("floor", out var fProp) && fProp.TryGetInt32(out int fVal))
                {
                    vibrationDraft.SetMotors(lVal, rVal, fVal);
                }
                Vibration?.Refresh();
                SyncPcMode();
            }
            ChangeCount.Text = $"Applied vibration ({vibrationDraft.Strength}%): {item.Title}";
            return;
        }

        if (item.Category == "macro" && item.Payload is JsonElement macEl && macEl.ValueKind == JsonValueKind.Object)
        {
            string slot = macEl.TryGetProperty("slot", out var slProp) && slProp.ValueKind == JsonValueKind.String
                ? (slProp.GetString() ?? "M1")
                : (macroDraft.SlotNames.FirstOrDefault() ?? "M1");
            if (macroDraft.CanEdit && macroDraft.SlotNames.Contains(slot) &&
                macEl.TryGetProperty("events", out var evProp) && evProp.ValueKind == JsonValueKind.Array)
            {
                var steps = JsonSerializer.Deserialize<SetupMacro[]>(evProp.GetRawText(), SetupStore.JsonOptions);
                if (steps != null)
                {
                    var converted = steps.Select(s => new MacroEvent(s.Id == Guid.Empty ? Guid.NewGuid() : s.Id, s.Buttons ?? Array.Empty<string>(), s.Left, s.Right, s.HoldMs, s.PauseMs)).ToList();
                    macroDraft.Replace(slot, converted);
                    if (macEl.TryGetProperty("trigger", out var tProp) && tProp.ValueKind == JsonValueKind.String && macroDraft.PcMode)
                        macroDraft.SetTrigger(slot, tProp.GetString());
                    Macros?.SelectSlot(slot);
                    Macros?.Refresh();
                    UpdateMacroContext();
                }
            }
            ChangeCount.Text = $"Loaded macro {slot} ({macroDraft[slot].Count} steps): {item.Title}";
            return;
        }

        if ((item.Category == "setup" || item.Category == "pack") && item.Payload is JsonElement payloadEl)
        {
            try
            {
                var snap = JsonSerializer.Deserialize<SetupSnapshot>(payloadEl.GetRawText(), SetupStore.JsonOptions);
                if (snap != null && snap.ModelId == Model.Id)
                {
                    if (all)
                    {
                        LoadSetup(snap);
                    }
                    else
                    {
                        snap.Validate();
                        if (sections.Contains("Buttons & Remaps"))
                        {
                            Draft.ResetAll();
                            foreach (var (key, value) in snap.Mappings) Draft.Set(key, value);
                        }
                        if (sections.Contains("Curves & Deadzones"))
                        {
                            foreach (var (key, value) in snap.Curves)
                                Curves.Draft.Set(Enum.Parse<CurveChannel>(key), new(value.Preset, value.Inner, value.Outer, (value.X1, value.Y1), (value.X2, value.Y2)) { BasePreset = value.BasePreset });
                            Curves.Refresh();
                            UpdateCurveContext();
                        }
                        if (sections.Contains("Macros") && macroDraft.CanEdit)
                        {
                            foreach (var slot in macroDraft.SlotNames)
                            {
                                if (snap.Macros.TryGetValue(slot, out var steps))
                                    macroDraft.Replace(slot, steps.Select(s => new MacroEvent(s.Id == Guid.Empty ? Guid.NewGuid() : s.Id, s.Buttons ?? Array.Empty<string>(), s.Left, s.Right, s.HoldMs, s.PauseMs)));
                            }
                            Macros?.Refresh();
                            UpdateMacroContext();
                        }
                        if (sections.Contains("Vibration Motors") && vibrationDraft.CanEdit)
                        {
                            vibrationDraft.Set(Math.Clamp(snap.Vibration, 0, vibrationDraft.Max));
                            if (vibrationDraft.PcMode && snap.Motors is { Length: 3 })
                                vibrationDraft.SetMotors(snap.Motors[0], snap.Motors[1], snap.Motors[2]);
                            Vibration?.Refresh();
                        }
                        SyncPcMode();
                        RefreshEditor();
                    }
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
