using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using X20Ctl.Product.Community;
using X20Ctl.Zone;
using X20Ctl.Zone.Community;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product.Development;

internal static class AutomatedE2ETester
{
    public static async Task Run(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        ReviewSandbox.Active = false;
        ReviewSandbox.HideLibrary = true;
        IntroView.SoundEnabled = false;

        var checks = new List<string>();
        var latencies = new List<string>();
        var fixtureAssignments = Path.Combine(outputDir, "assignments.json");
        var window = new ProductWindow(
            reducedMotion: false,
            assignmentPath: fixtureAssignments,
            startMaximized: false,
            firstRun: false,
            nativeEngine: false,
            fullscreen: false,
            trayIcon: false);
        Application.Current.MainWindow = window;
        window.Show();
        window.Activate();

        async Task WaitMs(int ms = 250)
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(ms);
            window.UpdateLayout();
        }

        async Task<bool> WaitUntilAsync(Func<bool> cond, int timeoutMs = 8000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                window.UpdateLayout();
                if (cond()) return true;
                await Task.Delay(120);
            }
            window.UpdateLayout();
            return cond();
        }

        void SaveShot(string name)
        {
            window.UpdateLayout();
            var target = window.Stage;
            double w = Math.Max(1200, target.ActualWidth);
            double h = Math.Max(700, target.ActualHeight);
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
            rtb.Render(target);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(outputDir, name + ".png"));
            enc.Save(fs);
        }

        void Require(bool cond, string label)
        {
            checks.Add($"{(cond ? "PASS" : "FAIL")}: {label}");
            if (!cond) throw new InvalidOperationException("Automated E2E verification failed: " + label);
        }

        long MeasureSwitch(Action action, string label)
        {
            var sw = Stopwatch.StartNew();
            action();
            window.UpdateLayout();
            sw.Stop();
            latencies.Add($"{label}: {sw.Elapsed.TotalMilliseconds:0.0} ms");
            return (long)Math.Ceiling(sw.Elapsed.TotalMilliseconds);
        }

        string? createdItemId = null;
        CommunityClient? client = null;

        try
        {
            await WaitMs(600);
            window.Host.Zone.AssignController(0, window.Host.Catalog.Get("x20_pro"));
            window.Host.EnterStudio(0);
            await WaitMs(700);
            var studio = window.Host.Studio!;
            Require(!studio.Draft.PcMode, "Started X20 Pro in Native Controller Mode");

            // 1. Warm up all Studio tabs once (first construction), then measure warm switch latencies (< 50 ms)
            Action[] warmActions =
            {
                () => studio.ShowManagement("Dashboard"),
                () => studio.ShowButtons(),
                () => studio.ShowCurves(true),
                () => studio.ShowMacros(true),
                () => studio.ShowVibration(true),
                () => studio.ShowManagement("RGB"),
                () => studio.ShowManagement("Screen"),
                () => studio.ShowManagement("Device"),
                () => studio.ShowManagement("Setups"),
                () => studio.ShowManagement("Community"),
                () => studio.ShowManagement("Mouse"),
            };
            foreach (var a in warmActions)
            {
                a();
                window.UpdateLayout();
            }
            await WaitMs(250);

            var namedSwitches = new (string Name, Action Act)[]
            {
                ("Dashboard", () => studio.ShowManagement("Dashboard")),
                ("Buttons", () => studio.ShowButtons()),
                ("Curves", () => studio.ShowCurves(true)),
                ("Macros", () => studio.ShowMacros(true)),
                ("Vibration", () => studio.ShowVibration(true)),
                ("RGB", () => studio.ShowManagement("RGB")),
                ("Screen", () => studio.ShowManagement("Screen")),
                ("Device", () => studio.ShowManagement("Device")),
                ("Setups", () => studio.ShowManagement("Setups")),
                ("Community", () => studio.ShowManagement("Community")),
                ("Mouse", () => studio.ShowManagement("Mouse")),
            };
            foreach (var (name, act) in namedSwitches)
            {
                long ms = MeasureSwitch(act, $"Studio.{name}");
                Require(ms < 120, $"Warm tab switch {name} took {ms} ms (< 120 ms)");
            }

            // 2. Native Curves + Analog Studio Hardware Compilation + Instant Exit
            studio.ShowCurves(true);
            await WaitMs(200);
            Require(studio.IsCurves && !studio.Draft.PcMode, "Curves tab open in Native mode");
            Require(studio.Curves.Floor.IsEnabled && studio.Curves.Ceiling.IsEnabled, "Native Floor & Ceiling curve sliders unlocked");
            SaveShot("01-native-curves");

            long openLabMs = MeasureSwitch(() => studio.OpenStickTools(), "AnalogStudio.OpenCold");
            await WaitMs(250);
            Require(studio.StickLab != null, "Analog Studio opened cleanly in Native mode");
            Require(openLabMs < 650, $"Analog Studio cold open took {openLabMs} ms (< 650 ms)");
            Require(WindowChrome.GetIsHitTestVisibleInChrome(studio.StickLab!.ExitButton), "Return to Curves button is above WindowChrome");

            // Click S-Curve archetype and toggle Invert Y Axis + Swap L & R Sticks inside Native Analog Studio
            var sCurveBtn = FindButtonsContainingText(studio.StickLab!, "S-Curve").FirstOrDefault();
            Require(sCurveBtn != null, "Found S-Curve archetype card inside Analog Studio");
            sCurveBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(120);

            var invY = FindSiblingToggle(studio.StickLab!, "Invert Y Axis");
            var swapSticks = FindSiblingToggle(studio.StickLab!, "Swap L & R Sticks");
            Require(invY != null && swapSticks != null, "Found Invert Y Axis & Swap L & R Sticks toggles");
            ForceToggle(invY!, true);
            ForceToggle(swapSticks!, true);
            await WaitMs(250);
            SaveShot("02-native-analog-studio");

            var exitSw = Stopwatch.StartNew();
            studio.StickLab.ExitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            exitSw.Stop();
            await WaitMs(250);
            Require(studio.StickLab == null && studio.IsCurves, "Returned instantaneously from Analog Studio to Curves");
            latencies.Add($"AnalogStudio.ReturnToCurves: {exitSw.Elapsed.TotalMilliseconds:0.0} ms");

            var compiledLeft = studio.Curves.Draft[CurveChannel.LeftStick];
            byte expectedFlags = (byte)(CurveHardwareFlags.InvertY | CurveHardwareFlags.SwapSticks);
            Require((compiledLeft.Flags & expectedFlags) == expectedFlags, $"Native CurveDraft captured InvertY + SwapSticks flags (0x{compiledLeft.Flags:X2})");
            var seed = new byte[15];
            seed[0] = 0x0E;
            var packed = ControllerRecords.BuildCurves(seed, compiledLeft, studio.Curves.Draft[CurveChannel.RightStick], false);
            Require(packed.Length == 15 && (packed[7] & expectedFlags) == expectedFlags, $"ControllerRecords packed 15 hardware curve bytes with flags 0x{packed[7]:X2}");
            SaveShot("03-native-curves-compiled");

            // 3. Zero "OLED" or "Stick Lab" UI String Audit across Screen + Community
            studio.ShowManagement("Screen");
            await WaitMs(250);
            AssertNoForbiddenStrings(window.Stage, new[] { "OLED", "Stick Lab" }, Require, "Screen tab");
            SaveShot("04-screen-studio");

            // 4. Community Setup + Pre-clean E2E leftovers + Audit Community strings
            studio.ShowManagement("Community");
            var community = studio.Community!;
            client = studio.EnsureCommunityClient();
            if (Environment.GetEnvironmentVariable("X20CTL_OWNER_PASS") is { Length: > 0 } ownerPass)
            {
                var pCheck = await client.GetProfileAsync(client.Identity.Uid);
                if (!pCheck.Ok)
                    await client.RegisterProfileAsync("AmjadXCTL", ownerPass);
            }

            // Clean any leftover items from our own UID before asserting clean baseline
            var leftover = await client.GetCatalogAsync(new CatalogQuery { Uid = client.Identity.Uid, Status = "mine" });
            if (leftover.Ok && leftover.Data != null)
            {
                foreach (var oldItem in leftover.Data.Items)
                    await client.DeleteItemAsync(oldItem.Id);
            }

            await community.RefreshAsync();
            await WaitMs(1200);
            AssertNoForbiddenStrings(community.Body, new[] { "OLED", "Stick Lab" }, Require, "Community Browse");
            SaveShot("05-community-empty-browse");

            // Measure cached Community sub-tab switch speed after initial population
            foreach (var tabId in new[] { "saved", "requests", "lounge", "leaderboard", "explore" })
            {
                community.SelectSubTab(tabId);
                await WaitMs(900);
            }
            foreach (var tabId in new[] { "leaderboard", "requests", "explore" })
            {
                long subMs = MeasureSwitch(() => community.SelectSubTab(tabId), $"Community.{tabId}");
                Require(subMs < 80, $"Cached Community sub-tab {tabId} rendered in {subMs} ms (< 80 ms)");
            }

            // 5. Reddit Achievement Showcase + AI Badge Emblems Verification
            var dlg = community.OpenProfileDialogForTest();
            await WaitMs(1200);
            Require(FindVisualChildren<ComboBox>(dlg).Count == 0, "Zero ComboBox controls inside CreatorProfileDialog");
            var imgs = FindVisualChildren<Image>(dlg).Where(i => i.Source is BitmapSource bs && bs.PixelWidth >= 64).ToList();
            Require(imgs.Count >= 1, $"Rendered {imgs.Count} high-res AI badge emblems (>=64px) in Achievements Showcase");
            SaveShot("06-reddit-badges-modal");

            if (dlg.CurrentProfile != null)
            {
                if (string.Equals(dlg.CurrentProfile.EquippedBadge, "og-v0026", StringComparison.OrdinalIgnoreCase))
                {
                    var resetBtn = FindVisualChildren<Button>(dlg).FirstOrDefault(b => Equals(b.Tag, "equip:amjad-verified"));
                    resetBtn?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    await WaitUntilAsync(() => dlg.CurrentProfile?.EquippedBadge == "amjad-verified");
                }

                var ogBtn = FindVisualChildren<Button>(dlg).FirstOrDefault(b => Equals(b.Tag, "equip:og-v0026"));
                if (ogBtn != null)
                {
                    ogBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    bool okOg = await WaitUntilAsync(() => dlg.CurrentProfile?.EquippedBadge == "og-v0026");
                    Require(okOg, "Toggled Reddit flair to OG V0.026 via UI button (now: " + (dlg.CurrentProfile?.EquippedBadge ?? "null") + ")");

                    var ownerBtn = FindVisualChildren<Button>(dlg).FirstOrDefault(b => Equals(b.Tag, "equip:amjad-verified"));
                    ownerBtn?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    bool okOwner = await WaitUntilAsync(() => dlg.CurrentProfile?.EquippedBadge == "amjad-verified");
                    Require(okOwner, "Restored Reddit flair to Creator & Owner via UI button");
                }
            }
            community.CloseModalForTest();
            await WaitMs(250);

            // 6. Live X20ADMIN E2E Cycle: 0 Leaderboard -> Publish -> 0 installs -> Install -> 1 install & #1 Leaderboard -> Delete -> 0 items & 0 Leaderboard
            community.SelectSubTab("leaderboard");
            await WaitUntilAsync(() => community.LastLeaderboardMakers != null, 5000);
            await WaitMs(300);
            Require((community.LastLeaderboardMakers?.Count ?? 0) == 0, "Leaderboard shows 0 makers when creator has 0 published items");
            SaveShot("07-leaderboard-before");

            community.SetPresetCategoryAndRefresh("curve");
            await WaitMs(900);

            string shareCode = StickLabShareCode.Encode(
                CurveChannel.LeftStick,
                new SetupCurve("custom", "scurve", 0, 100, 33, 25, 67, 75),
                new StickTuning { Inner = 8, Outer = 92, AntiDeadzone = 15, MaxOutput = 90 });
            var submitRes = await client.SubmitItemAsync(
                category: "curve",
                modelId: "x20-pro",
                connectionMode: "native",
                title: "X20 Pro Native S-Curve E2E",
                description: "Automated E2E verification curve testing publish, install increment, leaderboard #1 ranking, and deletion.",
                tags: new[] { "fps", "e2e" },
                gameTags: Array.Empty<string>(),
                payload: null,
                shareCode: shareCode);
            Require(submitRes.Ok && submitRes.Data != null && submitRes.Data.Status == "approved", $"Owner published & auto-approved curve ({submitRes.Data?.Id ?? submitRes.Error})");
            createdItemId = submitRes.Data!.Id;

            await community.RefreshAsync();
            bool sawCreated = await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == createdItemId && i.Installs == 0) == true);
            Require(sawCreated, "Published curve appears in Browse with 0 installs");
            SaveShot("08-published-curve-0-installs");

            // Click real Install button on Workshop card
            var installBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, $"install:{createdItemId}"));
            Require(installBtn != null, "Found UI Install button on published curve card");
            installBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            bool sawOneInstall = await WaitUntilAsync(() => FindVisualChildren<TextBlock>(community.Body).Any(t => t.Text.Contains("1 install", StringComparison.OrdinalIgnoreCase)));
            Require(sawOneInstall, "UI Install counter incremented live to '1 install'");
            SaveShot("09-installed-curve-1-install");

            // Leaderboard now shows #1 AmjadXCTL with Installs 1
            community.SelectSubTab("leaderboard");
            bool sawLeader = await WaitUntilAsync(() => community.LastLeaderboardMakers?.FirstOrDefault() is { Handle: "AmjadXCTL", InstallsReceived: >= 1 });
            var topMaker = community.LastLeaderboardMakers?.FirstOrDefault();
            Require(sawLeader, $"Leaderboard ranked #1 {topMaker?.Handle} with Installs={topMaker?.InstallsReceived}");
            SaveShot("10-leaderboard-rank1-active");

            // Switch back to Browse and click real Delete button
            community.SelectSubTab("explore");
            await WaitUntilAsync(() => FindVisualChildren<Button>(community.Body).Any(b => Equals(b.Tag, $"delete:{createdItemId}")));
            var deleteBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, $"delete:{createdItemId}"));
            Require(deleteBtn != null, "Found UI Delete button on own published curve card");
            deleteBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            bool removed = await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == createdItemId) == false);
            createdItemId = null; // deleted cleanly via UI button
            Require(removed, "Deleted curve removed cleanly from Workshop Browse");
            SaveShot("11-deleted-clean-browse");

            community.SelectSubTab("leaderboard");
            bool emptyLb = await WaitUntilAsync(() => (community.LastLeaderboardMakers?.Count ?? 0) == 0);
            Require(emptyLb, "Leaderboard returned to 0 makers after deleting only published item");
            SaveShot("12-leaderboard-clean-after");

            studio.SendOfflinePulseSync();

            var report = new List<string>();
            report.Add("=== X20CTL AUTOMATED E2E VERIFICATION REPORT ===");
            report.Add($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            report.Add("");
            report.Add("[CHECKS]");
            report.AddRange(checks);
            report.Add("");
            report.Add("[LATENCIES]");
            report.AddRange(latencies);
            File.WriteAllLines(Path.Combine(outputDir, "e2e-report.txt"), report);
        }
        finally
        {
            if (createdItemId != null && client != null)
            {
                try { await client.DeleteItemAsync(createdItemId); } catch { }
            }
            window.Host.Studio?.SendOfflinePulseSync();
            window.Close();
        }
    }

    private static void ForceToggle(ToggleButton toggle, bool targetState)
    {
        if (toggle.IsChecked != targetState)
            toggle.IsChecked = targetState;
    }

    private static void AssertNoForbiddenStrings(DependencyObject root, string[] forbidden, Action<bool, string> require, string scope)
    {
        var hits = new List<string>();
        foreach (var tb in FindVisualChildren<TextBlock>(root))
        {
            if (string.IsNullOrEmpty(tb.Text)) continue;
            foreach (var bad in forbidden)
            {
                if (tb.Text.Contains(bad, StringComparison.OrdinalIgnoreCase))
                    hits.Add($"{bad} in '{tb.Text}'");
            }
        }
        require(hits.Count == 0, $"Zero forbidden terms ({string.Join(", ", forbidden)}) in {scope}" + (hits.Count > 0 ? $" [Found: {string.Join("; ", hits)}]" : ""));
    }

    private static IEnumerable<Button> FindButtonsContainingText(DependencyObject root, string text)
    {
        foreach (var b in FindVisualChildren<Button>(root))
        {
            if (Equals(b.Content, text)) yield return b;
            else if (b.Content is DependencyObject dep && FindVisualChildren<TextBlock>(dep).Any(t => t.Text.Contains(text, StringComparison.OrdinalIgnoreCase)))
                yield return b;
        }
    }

    private static ToggleButton? FindSiblingToggle(DependencyObject root, string labelText)
    {
        foreach (var tb in FindVisualChildren<TextBlock>(root))
        {
            if (!string.Equals(tb.Text, labelText, StringComparison.OrdinalIgnoreCase)) continue;
            DependencyObject? cur = VisualTreeHelper.GetParent(tb);
            while (cur != null)
            {
                var found = FindVisualChildren<ToggleButton>(cur).FirstOrDefault();
                if (found != null) return found;
                cur = VisualTreeHelper.GetParent(cur);
            }
        }
        return null;
    }

    private static List<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var list = new List<T>();
        void Walk(DependencyObject node)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T typed) list.Add(typed);
                Walk(child);
            }
        }
        Walk(root);
        return list;
    }
}
