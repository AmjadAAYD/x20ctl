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

        var createdItemIds = new List<string>();
        var createdSetupIds = new List<string>();
        CommunityClient? client = null;

        try
        {
            await WaitMs(600);
            window.Host.Zone.AssignController(0, window.Host.Catalog.Get("x20_pro"));
            window.Host.EnterStudio(0);
            await WaitMs(700);
            var studio = window.Host.Studio!;
            var initialSetupIds = studio.SetupStore.Items.Select(s => s.Id).ToHashSet();
            Require(!studio.Draft.PcMode, "Started X20 Pro in Native Controller Mode");

            // 1. Warm up all Studio tabs once (first construction), then measure warm switch latencies (< 120 ms)
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

            // 2. Native Curves + Analog Studio Hardware Compilation + Radial Anti-DZ + Warm Reopen + Zero Oval Pills
            studio.ShowCurves(true);
            await WaitMs(200);
            Require(studio.IsCurves && !studio.Draft.PcMode, "Curves tab open in Native mode");
            Require(studio.Curves.Floor.IsEnabled && studio.Curves.Ceiling.IsEnabled, "Native Floor & Ceiling curve sliders unlocked");
            var ovalInCurves = FindButtonsWithStyle(studio.Curves.Inspector, "ActionButton");
            Require(ovalInCurves.Count == 0, $"Zero oval ActionButton controls in Curves workspace (found {ovalInCurves.Count})");
            SaveShot("01-native-curves");

            long openLabMs = MeasureSwitch(() => studio.OpenStickTools(), "AnalogStudio.OpenCold");
            await WaitMs(250);
            Require(studio.StickLab != null, "Analog Studio opened cleanly in Native mode");
            Require(openLabMs < 650, $"Analog Studio cold open took {openLabMs} ms (< 650 ms)");
            Require(WindowChrome.GetIsHitTestVisibleInChrome(studio.StickLab!.ExitButton), "Return to Curves button is above WindowChrome");
            var ovalInLab = FindButtonsWithStyle(studio.StickLab!, "ActionButton");
            Require(ovalInLab.Count == 0, $"Zero oval ActionButton controls inside Analog Studio (found {ovalInLab.Count})");

            // Click S-Curve archetype, set Radial Anti-DZ to 15%, toggle Invert Y Axis + Swap L & R Sticks
            var sCurveBtn = FindButtonsContainingText(studio.StickLab!, "S-Curve").FirstOrDefault();
            Require(sCurveBtn != null, "Found S-Curve archetype card inside Analog Studio");
            sCurveBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(120);

            var radialSlider = FindSiblingSlider(studio.StickLab!, "Radial Anti-DZ");
            Require(radialSlider != null, "Found Radial Anti-DZ slider inside Analog Studio");
            radialSlider!.Value = 15;
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
            await WaitMs(200);
            Require(studio.StickLab == null && studio.IsCurves, "Returned instantaneously from Analog Studio to Curves");
            latencies.Add($"AnalogStudio.ReturnToCurves: {exitSw.Elapsed.TotalMilliseconds:0.0} ms");

            // Verify warm cached Analog Studio reopen (< 120 ms)
            long warmLabMs = MeasureSwitch(() => studio.OpenStickTools(), "AnalogStudio.OpenWarm");
            await WaitMs(120);
            Require(studio.StickLab != null && warmLabMs < 120, $"Warm Analog Studio reopen took {warmLabMs} ms (< 120 ms)");
            studio.StickLab!.ExitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(200);

            var compiledLeft = studio.Curves.Draft[CurveChannel.LeftStick];
            byte expectedFlags = (byte)(CurveHardwareFlags.InvertY | CurveHardwareFlags.SwapSticks);
            Require((compiledLeft.Flags & expectedFlags) == expectedFlags, $"Native CurveDraft captured InvertY + SwapSticks flags (0x{compiledLeft.Flags:X2})");
            Require(Math.Abs(studio.Curves.Floor.Value - 15) < 0.5, $"Radial Anti-DZ (15%) synced back to Curves Floor slider ({studio.Curves.Floor.Value:0.#}%)");
            bool footerHasFlags = FindVisualChildren<TextBlock>(studio.Curves.Shelf).Any(t => t.Text.Contains("Invert Y") && t.Text.Contains("Swap L/R"));
            Require(footerHasFlags, "Curves footer displays active Native Hardware Flags (Invert Y & Swap L/R)");
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

            foreach (var tabId in new[] { "saved", "requests", "lounge", "leaderboard", "explore" })
            {
                community.SelectSubTab(tabId);
                await WaitMs(800);
            }
            foreach (var tabId in new[] { "leaderboard", "requests", "explore" })
            {
                long subMs = MeasureSwitch(() => community.SelectSubTab(tabId), $"Community.{tabId}");
                Require(subMs < 80, $"Cached Community sub-tab {tabId} rendered in {subMs} ms (< 80 ms)");
            }

            // 5. All 27 Unique AI Badge Bitmaps + Reddit Achievement Showcase + Filters + Alerts + 64x64 Avatar
            string[] all27BadgeIds =
            {
                "amjad-verified", "og-v0026", "founding-100", "first-upload", "curve-architect",
                "pixel-smith", "macro-wizard", "light-sculptor", "bundle-curator", "pro-tuner",
                "master-crafter", "crowd-favorite", "hall-of-fame", "upvoted-10", "upvoted-100",
                "upvoted-500", "watcher-magnet", "remix-spark", "remix-artist", "verified-tester",
                "bounty-hunter", "challenge-winner", "night-owl", "lounge-regular", "zero-deadzone",
                "bug-finder", "supporter"
            };
            var uniqueBadgeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bid in all27BadgeIds)
            {
                var assetFile = BadgeChip.ResolveAssetFile(bid);
                var bmp = BadgeChip.GetBadgeBitmap(null, bid);
                Require(uniqueBadgeFiles.Add(assetFile) && bmp != null && bmp.PixelWidth >= 64, $"Unique 64px+ badge artwork loaded for {bid} -> {assetFile}");
            }
            Require(uniqueBadgeFiles.Count == 27, $"Verified all {uniqueBadgeFiles.Count}/27 built-in badges use unique artwork");

            var alertsBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, "community:alerts"));
            Require(alertsBtn != null, "Found Alerts Bell button on Community top bar");
            alertsBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(200);
            Require(community.ActiveModalContent.Visibility == Visibility.Visible, "Notifications drawer opened cleanly from Alerts Bell");
            community.CloseModalForTest();
            await WaitMs(150);

            var dlg = community.OpenProfileDialogForTest();
            await WaitMs(1100);
            Require(FindVisualChildren<ComboBox>(dlg).Count == 0, "Zero ComboBox controls inside CreatorProfileDialog");
            var imgs = FindVisualChildren<Image>(dlg).Where(i => i.Source is BitmapSource bs && bs.PixelWidth >= 64).ToList();
            Require(imgs.Count >= 1, $"Rendered {imgs.Count} high-res AI badge emblems (>=64px) in Achievements Showcase");

            foreach (var fTag in new[] { "achfilter:unlocked", "achfilter:locked", "achfilter:rare", "achfilter:all" })
            {
                var fBtn = FindVisualChildren<Button>(dlg).FirstOrDefault(b => Equals(b.Tag, fTag));
                Require(fBtn != null, $"Found achievement filter button {fTag}");
                fBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await WaitMs(80);
            }

            byte[] avatarPng = GenerateAvatarPng(64, 64);
            bool pfpOk = await dlg.UploadAvatarBytesAsync(avatarPng, "image");
            Require(pfpOk && !string.IsNullOrEmpty(dlg.CurrentProfile?.PfpUrl), $"Uploaded 64x64 avatar PNG cleanly ({dlg.CurrentProfile?.PfpUrl ?? "none"})");
            await WaitMs(400);
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
                    Require(okOg, "Toggled Reddit flair to OG V0.026 via UI button");

                    var ownerBtn = FindVisualChildren<Button>(dlg).FirstOrDefault(b => Equals(b.Tag, "equip:amjad-verified"));
                    ownerBtn?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    bool okOwner = await WaitUntilAsync(() => dlg.CurrentProfile?.EquippedBadge == "amjad-verified");
                    Require(okOwner, "Restored Reddit flair to Creator & Owner via UI button");
                }
            }
            community.CloseModalForTest();
            await WaitMs(250);

            // Helper to install, verify UI install counter increment, and delete via UI buttons
            async Task InstallAndDeleteCardAsync(string itemId, string itemTitle, string expectedCategory, Action? onBeforeInstall, Action verifyApplied, string? shotName = null)
            {
                community.SetPresetCategoryAndRefresh(expectedCategory);
                bool sawItem = await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == itemId) == true, 10000);
                Require(sawItem, $"Published '{itemTitle}' appeared live in Browse");
                if (shotName != null)
                {
                    await WaitMs(600);
                    SaveShot(shotName);
                }

                onBeforeInstall?.Invoke();
                var installBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, $"install:{itemId}"));
                Require(installBtn != null, $"Found UI Install button for '{itemTitle}'");
                installBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                bool sawInstallCount = await WaitUntilAsync(() => FindVisualChildren<TextBlock>(community.Body).Any(t => t.Text.Contains("1 install", StringComparison.OrdinalIgnoreCase)));
                Require(sawInstallCount, $"Install counter incremented live for '{itemTitle}'");
                await WaitMs(450);
                verifyApplied();

                var deleteBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, $"delete:{itemId}"));
                Require(deleteBtn != null, $"Found UI Delete button for '{itemTitle}'");
                deleteBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                bool removed = await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == itemId) == false);
                Require(removed, $"Deleted '{itemTitle}' cleanly via UI");
                createdItemIds.Remove(itemId);
            }

            // 6. Real UI Share -> Submit Dialog -> Install -> Delete: 160x80 Static PNG Screen Art
            studio.ShowManagement("Screen");
            await WaitMs(250);
            byte[] staticPng = GenerateScreenArtworkPng(160, 80);
            studio.Screen!.LoadImageBytes(staticPng, "e2e-static-screen.png");
            await WaitMs(250);
            var shareScreenBtn = FindButtonsContainingText(studio.Screen.Body, "Share to Community").FirstOrDefault();
            Require(shareScreenBtn != null, "Found 'Share to Community' button inside Screen Studio");
            shareScreenBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            bool submitDlgOpen = await WaitUntilAsync(() => community.ActiveSubmitDialog is { HasMediaBytes: true });
            Require(submitDlgOpen, "Submit Dialog opened pre-loaded with 160x80 PNG from Screen Studio");

            var subDlg = community.ActiveSubmitDialog!;
            subDlg.TitleInput.Text = "E2E Cyber Grid 160x80 PNG";
            subDlg.DescriptionInput.Text = "Automated E2E verification of static 160x80 Screen Art upload and direct install into Screen Studio.";
            SaveShot("07-submit-screen-png-dialog");
            subDlg.SubmitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            bool dlgClosed = await WaitUntilAsync(() => community.ActiveSubmitDialog == null, 15000);
            Require(dlgClosed, "Static PNG submitted & modal auto-closed cleanly");

            community.SetPresetCategoryAndRefresh("screen");
            bool sawPngItem = await WaitUntilAsync(() => community.LastCatalog?.Items.FirstOrDefault(i => i.Title == "E2E Cyber Grid 160x80 PNG") != null, 10000);
            var pngItem = community.LastCatalog?.Items.FirstOrDefault(i => i.Title == "E2E Cyber Grid 160x80 PNG");
            Require(sawPngItem && pngItem != null && pngItem.MediaType == "image", "Static PNG appears in Browse with PNG format tag");
            createdItemIds.Add(pngItem!.Id);

            await InstallAndDeleteCardAsync(
                pngItem.Id, pngItem.Title, "screen",
                onBeforeInstall: () => studio.Screen!.LoadWallpaper(0),
                verifyApplied: () => Require(studio.Screen!.FrameCount == 1 && !studio.Screen.IsAnimated, $"Installed static 160x80 PNG directly into Screen Studio ({studio.Screen!.FrameCount}f)"),
                shotName: "08-browse-screen-png");

            // 7. Real UI Share -> Submit Dialog -> Install -> Delete: 160x80 4-Frame Animated GIF Screen Art
            studio.ShowManagement("Screen");
            await WaitMs(200);
            byte[] animatedGif = GenerateScreenArtworkGif(160, 80, 4);
            studio.Screen!.LoadImageBytes(animatedGif, "e2e-animated-pulse.gif");
            await WaitMs(250);
            Require(studio.Screen.FrameCount == 4 && studio.Screen.IsAnimated, $"Screen Studio decoded 4-frame GIF ({studio.Screen.FrameCount}f)");

            shareScreenBtn = FindButtonsContainingText(studio.Screen.Body, "Share to Community").FirstOrDefault();
            shareScreenBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveSubmitDialog is { HasMediaBytes: true }), "Submit Dialog pre-loaded with 4-frame 160x80 GIF");
            subDlg = community.ActiveSubmitDialog!;
            subDlg.TitleInput.Text = "E2E Pulse Wave 160x80 GIF";
            subDlg.DescriptionInput.Text = "Automated E2E verification of multi-frame animated GIF upload and Screen Studio decode.";
            subDlg.SubmitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            bool gifDlgClosed = await WaitUntilAsync(() => community.ActiveSubmitDialog == null, 20000);
            Require(gifDlgClosed, $"Animated GIF submitted & modal auto-closed cleanly (status: {subDlg.StatusText})");

            community.SetPresetCategoryAndRefresh("screen");
            bool sawGifItem = await WaitUntilAsync(() => community.LastCatalog?.Items.FirstOrDefault(i => i.Title == "E2E Pulse Wave 160x80 GIF") != null, 10000);
            var gifItem = community.LastCatalog?.Items.FirstOrDefault(i => i.Title == "E2E Pulse Wave 160x80 GIF");
            Require(sawGifItem && gifItem != null && gifItem.MediaType == "gif", "Animated GIF appears in Browse with GIF badge");
            createdItemIds.Add(gifItem!.Id);

            await community.OpenItemInspectorForTestAsync(gifItem);
            await WaitMs(250);
            var zoomBtn = FindVisualChildren<Button>(community.ActiveModalContent).FirstOrDefault(b => Equals(b.Tag, "inspector:zoom2x"));
            var screenView = FindVisualChildren<AnimatedMediaView>(community.ActiveModalContent).FirstOrDefault(v => Equals(v.Tag, "inspector:screenview"));
            Require(zoomBtn != null && screenView != null, "Found 2x Zoom control inside Screen Art Inspector");
            zoomBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(200);
            Require(Math.Abs(screenView!.Width - 320) < 1 && Math.Abs(screenView.Height - 160) < 1, $"Inspector zoomed Screen Art to {screenView.Width}x{screenView.Height}");
            SaveShot("09b-inspector-gif-2x");
            community.CloseModalForTest();
            await WaitMs(150);

            await InstallAndDeleteCardAsync(
                gifItem.Id, gifItem.Title, "screen",
                onBeforeInstall: () => studio.Screen!.LoadWallpaper(0),
                verifyApplied: () => Require(studio.Screen!.FrameCount == 4 && studio.Screen.IsAnimated, $"Installed 4-frame GIF directly into Screen Studio ({studio.Screen!.FrameCount}f)"),
                shotName: "09-browse-screen-gif");

            // 7B. Real-User Dynamic Category Switch + Game Quick-Chip + Upvote + Comment + Bookmark + Saved & Mine v2 Push Update
            community.SetPresetCategoryAndRefresh("all");
            await WaitUntilAsync(() => community.LastCatalog != null, 8000);
            var pubTopBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, "community:publish"));
            Require(pubTopBtn != null, "Found top Publish Setup button");
            pubTopBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveSubmitDialog != null), "Opened Publish modal from Browse bar");
            subDlg = community.ActiveSubmitDialog!;
            Require(subDlg.AttachmentSummary.Contains("Snapshot", StringComparison.OrdinalIgnoreCase), $"Initial attachment summary ready ({subDlg.AttachmentSummary})");

            subDlg.CategoryInput.SelectedValue = "curve";
            await WaitMs(120);
            Require(subDlg.AttachmentSummary.Contains("Curve", StringComparison.OrdinalIgnoreCase), $"Dynamic category switch updated attachment summary ({subDlg.AttachmentSummary})");

            var rlChip = FindVisualChildren<Button>(subDlg).FirstOrDefault(b => Equals(b.Tag, "gamechip:Rocket League"));
            Require(rlChip != null, "Found Rocket League 1-click game chip");
            rlChip!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(subDlg.GameInput.Text.Contains("Rocket League", StringComparison.OrdinalIgnoreCase), "1-click game chip populated GameInput");

            subDlg.TitleInput.Text = "E2E Dynamic Curve v1";
            subDlg.DescriptionInput.Text = "Real-user flow: switch category in modal, tag game, upvote, comment, bookmark, and push v2.";
            subDlg.SubmitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveSubmitDialog == null, 15000), "Submitted v1 Dynamic Curve cleanly");

            community.SetPresetCategoryAndRefresh("curve");
            Require(await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Title == "E2E Dynamic Curve v1") == true, 10000), "E2E Dynamic Curve v1 live in Browse");
            var dynItem = community.LastCatalog!.Items.First(i => i.Title == "E2E Dynamic Curve v1");
            createdItemIds.Add(dynItem.Id);

            var voteBtn = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, $"vote:{dynItem.Id}"));
            Require(voteBtn != null, "Found Upvote button on E2E Dynamic Curve v1 card");
            voteBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(450);

            await community.OpenItemInspectorForTestAsync(dynItem);
            await WaitMs(200);
            var modalTextBoxes = FindVisualChildren<TextBox>(community.ActiveModalContent);
            var commentBox = modalTextBoxes.LastOrDefault();
            var postCommentBtn = FindButtonsContainingText(community.ActiveModalContent, "Post").FirstOrDefault();
            var bookmarkBtn = FindButtonsContainingText(community.ActiveModalContent, "Bookmark").FirstOrDefault();
            if (commentBox != null && postCommentBtn != null)
            {
                commentBox.Text = "Clean spline response!";
                postCommentBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await WaitMs(450);
            }
            bookmarkBtn = FindButtonsContainingText(community.ActiveModalContent, "Bookmark").FirstOrDefault();
            bookmarkBtn?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(350);
            community.CloseModalForTest();
            await WaitMs(200);

            community.SelectSubTab("saved");
            Require(await WaitUntilAsync(() => FindVisualChildren<Button>(community.Body).Any(b => Equals(b.Tag, $"update:{dynItem.Id}")), 8000), "Found Push Update button in Saved & Mine");
            FindVisualChildren<Button>(community.Body).First(b => Equals(b.Tag, $"update:{dynItem.Id}")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveSubmitDialog != null), "Opened Push Version Update modal from Saved & Mine");
            subDlg = community.ActiveSubmitDialog!;
            Require(!subDlg.CategoryInput.IsEnabled, "Version update locks category to original preset type");
            subDlg.TitleInput.Text = "E2E Dynamic Curve v2";
            subDlg.SubmitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveSubmitDialog == null, 15000), "Submitted v2 update cleanly");

            var mineCheck = await client.GetCatalogAsync(new CatalogQuery { Uid = client.Identity.Uid, Status = "mine", Sort = "new" });
            var v2Item = mineCheck.Data?.Items.FirstOrDefault(i => i.Title == "E2E Dynamic Curve v2");
            Require(v2Item != null && v2Item.Version >= 2, $"Server issued versioned update v{v2Item?.Version ?? 0} linked to {dynItem.Id}");
            if (v2Item != null)
            {
                await client.DeleteItemAsync(v2Item.Id);
                createdItemIds.Remove(v2Item.Id);
            }
            await client.DeleteItemAsync(dynItem.Id);
            createdItemIds.Remove(dynItem.Id);

            // 8. Analog Curve -> Leaderboard Metrics + #1 -> Install -> Delete
            community.SelectSubTab("leaderboard");
            await WaitUntilAsync(() => community.LastLeaderboardMakers != null, 5000);
            Require((community.LastLeaderboardMakers?.Count ?? 0) == 0, "Leaderboard shows 0 makers before publishing persistent item");

            string shareCode = StickLabShareCode.Encode(
                CurveChannel.LeftStick,
                new SetupCurve("custom", "scurve", 4, 96, 33, 25, 67, 75),
                new StickTuning { Inner = 4, Outer = 96, AntiDeadzone = 15, MaxOutput = 90 },
                flags: expectedFlags);
            var curveRes = await client.SubmitItemAsync(
                category: "curve", modelId: "x20-pro", connectionMode: "native",
                title: "E2E Native Curve + Flags", description: "Verifying Flags + Radial Anti-DZ community install.",
                tags: new[] { "fps" }, gameTags: Array.Empty<string>(),
                payload: new { leftPreset = "custom", deadzone = 4, antiDeadzone = 15, flags = "Invert Y, Swap L/R" },
                shareCode: shareCode);
            Require(curveRes.Ok && curveRes.Data != null && curveRes.Data.Status == "approved", "Published curve item live");
            createdItemIds.Add(curveRes.Data!.Id);

            await InstallAndDeleteCardAsync(
                curveRes.Data.Id, curveRes.Data.Title, "curve",
                onBeforeInstall: () => { studio.Curves.Draft.SetPreset(CurveChannel.LeftStick, "default"); },
                verifyApplied: () =>
                {
                    var lc = studio.Curves.Draft[CurveChannel.LeftStick];
                    Require((lc.Flags & expectedFlags) == expectedFlags && Math.Abs(studio.Curves.Floor.Value - 15) < 0.5,
                        $"Installed community curve restored Flags=0x{lc.Flags:X2} and Floor={studio.Curves.Floor.Value:0.#}%");
                },
                shotName: "10-browse-curve");

            // 9. RGB Lighting + Vibration + Macro (real steps) + Full Setup via Diff Drawer
            var rgbRes = await client.SubmitItemAsync(
                category: "rgb", modelId: "x20-pro", connectionMode: "native",
                title: "E2E Ember Orange RGB", description: "RGB lighting preset #FF5500.",
                tags: new[] { "rgb" }, gameTags: Array.Empty<string>(),
                payload: new { rgb = "#FF5500", mode = 0, brightness = 0x50 });
            Require(rgbRes.Ok && rgbRes.Data != null, "Published RGB preset");
            createdItemIds.Add(rgbRes.Data!.Id);
            await InstallAndDeleteCardAsync(
                rgbRes.Data.Id, rgbRes.Data.Title, "rgb",
                onBeforeInstall: null,
                verifyApplied: () => Require(studio.RgbSelectedColor == 0xFF5500, $"Installed RGB applied color #FF5500 (actual #{studio.RgbSelectedColor:X6})"));

            var vibRes = await client.SubmitItemAsync(
                category: "vibration", modelId: "x20-pro", connectionMode: "native",
                title: "E2E Tactile 85% Rumble", description: "Vibration preset at 85%.",
                tags: new[] { "rumble" }, gameTags: Array.Empty<string>(),
                payload: new { strength = 85 });
            Require(vibRes.Ok && vibRes.Data != null, "Published Vibration preset");
            createdItemIds.Add(vibRes.Data!.Id);
            await InstallAndDeleteCardAsync(
                vibRes.Data.Id, vibRes.Data.Title, "vibration",
                onBeforeInstall: null,
                verifyApplied: () => Require(studio.VibrationStrength == 85, $"Installed Vibration applied strength {studio.VibrationStrength}%"));

            var macroRes = await client.SubmitItemAsync(
                category: "macro", modelId: "x20-pro", connectionMode: "native",
                title: "E2E Double Jump Macro", description: "2-step M1 macro sequence.",
                tags: new[] { "macro" }, gameTags: Array.Empty<string>(),
                payload: new
                {
                    slot = "M1",
                    events = new[]
                    {
                        new SetupMacro(Guid.NewGuid(), new[] { "A" }, StickHeading.Neutral, StickHeading.Neutral, 40, 20),
                        new SetupMacro(Guid.NewGuid(), new[] { "X" }, StickHeading.Neutral, StickHeading.Neutral, 40, 20),
                    }
                });
            Require(macroRes.Ok && macroRes.Data != null, "Published Macro preset");
            createdItemIds.Add(macroRes.Data!.Id);
            await InstallAndDeleteCardAsync(
                macroRes.Data.Id, macroRes.Data.Title, "macro",
                onBeforeInstall: () => studio.MacroDraft.Clear("M1"),
                verifyApplied: () => Require(studio.MacroDraft["M1"].Count == 2, $"Installed Macro loaded {studio.MacroDraft["M1"].Count} steps into M1"));

            // Full Setup + Compare & Cherry-Pick Diff Drawer verification
            studio.Draft.Set("A", "B");
            var setupSnap = SetupSnapshot.Capture(studio.Model.Id, studio.Draft, studio.Curves.Draft, studio.MacroDraft, studio.VibrationStrength == 85 ? new VibrationDraft(studio.Model.Id) : new VibrationDraft(studio.Model.Id));
            studio.Draft.Set("A", "A");
            var setupRes = await client.SubmitItemAsync(
                category: "setup", modelId: "x20-pro", connectionMode: "native",
                title: "E2E Complete Pro Setup", description: "Full setup for Diff Drawer verification.",
                tags: new[] { "pro" }, gameTags: Array.Empty<string>(),
                payload: setupSnap);
            Require(setupRes.Ok && setupRes.Data != null, "Published Full Setup");
            createdItemIds.Add(setupRes.Data!.Id);

            community.SetPresetCategoryAndRefresh("setup");
            Require(await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == setupRes.Data.Id) == true, 10000), "Full Setup visible in Browse");
            await community.OpenItemInspectorForTestAsync(setupRes.Data);
            await WaitMs(200);
            var compareBtn = FindButtonsContainingText(community.Body, "Compare & 5s Test").FirstOrDefault();
            Require(compareBtn != null, "Found Compare & 5s Test button inside Inspector");
            compareBtn!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveDiffDrawer != null), "Diff Drawer opened cleanly");
            var drawerText = community.ActiveDiffDrawer!.TheirsSummaryText;
            Require(!drawerText.Contains("ValueKind", StringComparison.OrdinalIgnoreCase), $"Diff Drawer formats real JSON values without ValueKind bugs");
            SaveShot("11-diff-drawer-inspect");

            community.ActiveDiffDrawer.InstallSelectedButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.ActiveDiffDrawer == null, 8000), "Diff Drawer closed after Install Selected");
            await WaitMs(350);
            Require(studio.Draft.Target("A") == "B", $"Diff Drawer cherry-pick applied A->B remap (actual: {studio.Draft.Target("A")})");
            foreach (var added in studio.SetupStore.Items.Where(s => !initialSetupIds.Contains(s.Id)))
                createdSetupIds.Add(added.Id);

            // Delete setup item & verify clean 0-maker leaderboard
            community.SetPresetCategoryAndRefresh("setup");
            await WaitUntilAsync(() => FindVisualChildren<Button>(community.Body).Any(b => Equals(b.Tag, $"delete:{setupRes.Data.Id}")));
            FindVisualChildren<Button>(community.Body).First(b => Equals(b.Tag, $"delete:{setupRes.Data.Id}")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Require(await WaitUntilAsync(() => community.LastCatalog?.Items.Any(i => i.Id == setupRes.Data.Id) == false), "Deleted Full Setup via UI Delete button");
            createdItemIds.Remove(setupRes.Data.Id);

            community.SelectSubTab("leaderboard");
            await WaitUntilAsync(() => community.LastLeaderboardMakers != null, 5000);
            foreach (var lbMetricTag in new[] { "lbmetric:installs", "lbmetric:watchers", "lbmetric:karma" })
            {
                var mb = FindVisualChildren<Button>(community.Body).FirstOrDefault(b => Equals(b.Tag, lbMetricTag));
                Require(mb != null, $"Found Leaderboard metric switcher {lbMetricTag}");
                mb!.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await WaitMs(250);
            }
            bool emptyLb = await WaitUntilAsync(() => (community.LastLeaderboardMakers?.Count ?? 0) == 0);
            Require(emptyLb, "Leaderboard returned to 0 makers after deleting all E2E items");
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
            if (client != null)
            {
                foreach (var leftoverId in createdItemIds)
                {
                    try { await client.DeleteItemAsync(leftoverId); } catch { }
                }
            }
            if (window.Host.Studio is { } st)
            {
                foreach (var setupId in createdSetupIds)
                {
                    try { st.SetupStore.Delete(setupId); } catch { }
                }
            }
            window.Host.Studio?.SendOfflinePulseSync();
            window.Close();
        }
    }

    private static byte[] GenerateAvatarPng(int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12, 22, 48)), null, new Rect(0, 0, width, height));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(56, 189, 248)), new Pen(Brushes.White, 3), new Point(width / 2.0, height / 2.0), width * 0.34, height * 0.34);
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateScreenArtworkPng(int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 16, 36)), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2), new Rect(4, 4, width - 8, height - 8));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 122, 24)), null, new Point(width * 0.25, height * 0.5), 18, 18);
            dc.DrawLine(new Pen(Brushes.White, 3), new Point(70, 20), new Point(145, 60));
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateScreenArtworkGif(int width, int height, int frameCount)
    {
        var enc = new GifBitmapEncoder();
        for (int f = 0; f < frameCount; f++)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(6, 12, 28)), null, new Rect(0, 0, width, height));
                double barX = 16 + f * 32;
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(56, 189, 248)), null, new Rect(barX, 16, 28, height - 32));
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            enc.Frames.Add(BitmapFrame.Create(rtb));
        }
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static List<Button> FindButtonsWithStyle(DependencyObject root, string styleKey)
    {
        var list = new List<Button>();
        var targetStyle = Application.Current?.TryFindResource(styleKey) as Style;
        if (targetStyle == null) return list;
        foreach (var b in FindVisualChildren<Button>(root))
        {
            if (ReferenceEquals(b.Style, targetStyle))
                list.Add(b);
        }
        return list;
    }

    private static Slider? FindSiblingSlider(DependencyObject root, string labelText)
    {
        foreach (var tb in FindVisualChildren<TextBlock>(root))
        {
            if (!string.Equals(tb.Text, labelText, StringComparison.OrdinalIgnoreCase)) continue;
            DependencyObject? cur = VisualTreeHelper.GetParent(tb);
            while (cur != null)
            {
                var found = FindVisualChildren<Slider>(cur).FirstOrDefault();
                if (found != null) return found;
                cur = VisualTreeHelper.GetParent(cur);
            }
        }
        return null;
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
