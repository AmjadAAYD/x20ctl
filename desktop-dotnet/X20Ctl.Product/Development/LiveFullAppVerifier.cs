using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using X20Ctl.Zone;

namespace X20Ctl.Product.Development;

internal static class LiveFullAppVerifier
{
    public static async Task Run(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        ReviewSandbox.Active = false;
        ReviewSandbox.HideLibrary = true;
        IntroView.SoundEnabled = false;

        var checks = new List<string>();
        var fixtureAssignments = Path.Combine(outputDir, "assignments.json");
        var window = new ProductWindow(reducedMotion: false, assignmentPath: fixtureAssignments, startMaximized: false, firstRun: false, nativeEngine: false, fullscreen: false, trayIcon: false);
        Application.Current.MainWindow = window;
        window.Show();
        window.Activate();

        async Task WaitMs(int ms = 350)
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(ms);
            window.UpdateLayout();
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
            if (!cond) throw new InvalidOperationException("Live verification failed: " + label);
        }

        try
        {
            await WaitMs(700);
            SaveShot("01-zone");

            window.Host.Zone.AssignController(0, window.Host.Catalog.Get("x20_pro"));
            window.Host.EnterStudio(0);
            await WaitMs(900);
            var studio = window.Host.Studio!;
            Require(studio.CurrentPage == "Dashboard", "Entered X20 Pro Studio on Dashboard");
            await studio.RefreshPulseAsync();
            await WaitMs(300);
            SaveShot("02-dashboard");

            // Test Eco details toggle
            studio.Dashboard!.ToggleEcoDetails(true);
            await WaitMs(200);
            Require(studio.Dashboard.IsEcoDetailsOpen, "Dashboard Eco details modal opened");
            SaveShot("03-dashboard-eco-open");
            studio.Dashboard.ToggleEcoDetails(false);
            Require(!studio.Dashboard.IsEcoDetailsOpen, "Dashboard Eco details modal closed");

            // Buttons tab
            studio.ShowButtons();
            studio.SelectControl("A");
            await WaitMs(300);
            Require(studio.CurrentPage == "Buttons" && studio.SelectedControl == "A", "Buttons tab + selection");
            SaveShot("04-buttons");

            // Curves tab + Analog Studio + REAL Return to Curves click!
            studio.ShowCurves(true);
            await WaitMs(350);
            Require(studio.IsCurves, "Curves tab active");
            SaveShot("05-curves");

            studio.OpenStickTools();
            await WaitMs(450);
            Require(studio.StickLab != null, "Analog Studio opened");
            Require(WindowChrome.GetIsHitTestVisibleInChrome(studio.StickLab!.ExitButton), "Return to Curves button is hit-test visible above WindowChrome");
            SaveShot("06-analog-studio");

            // Switch channels inside Analog Studio & back
            studio.StickLab.SelectChannel(StickLabZone.LabChannel.RightStick);
            await WaitMs(120);
            studio.StickLab.SelectChannel(StickLabZone.LabChannel.LeftTrigger);
            await WaitMs(120);
            studio.StickLab.SelectChannel(StickLabZone.LabChannel.Competitive);
            await WaitMs(120);
            studio.StickLab.SelectChannel(StickLabZone.LabChannel.LeftStick);
            await WaitMs(120);

            // Real UI click on "< Return to Curves" button!
            studio.StickLab.ExitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await WaitMs(500);
            Require(studio.StickLab == null && studio.IsCurves, "Clicking '< Return to Curves' returned cleanly to Curves tab");
            SaveShot("07-returned-to-curves");

            // Macros, Vibration, RGB, Screen, Device, Mouse, Setups, PC Mode
            studio.ShowMacros(true); await WaitMs(250); SaveShot("08-macros");
            studio.ShowVibration(true); await WaitMs(250); SaveShot("09-vibration");
            studio.ShowManagement("RGB"); await WaitMs(250); SaveShot("10-rgb");
            studio.ShowManagement("Screen"); await WaitMs(250); SaveShot("11-screen");
            studio.ShowManagement("Device"); await WaitMs(250); SaveShot("12-device");
            studio.ShowManagement("Mouse"); await WaitMs(250); SaveShot("13-mouse");
            studio.ShowManagement("Setups"); await WaitMs(250); SaveShot("14-setups");
            studio.ShowManagement("PcMode"); await WaitMs(250); SaveShot("15-pcmode");

            // Community Hub LIVE against X20ADMIN
            if (Environment.GetEnvironmentVariable("X20CTL_OWNER_PASS") is { Length: > 0 } ownerPass)
            {
                var client = studio.EnsureCommunityClient();
                var prof = await client.GetProfileAsync(client.Identity.Uid);
                if (!prof.Ok || !prof.Data.Creator.IsOwner)
                {
                    var claim = await client.RegisterProfileAsync("AmjadXCTL", ownerPass);
                    Require(claim.Ok && claim.Data is { IsOwner: true, Handle: "AmjadXCTL" }, "Claimed verified owner AmjadXCTL on live X20ADMIN (" + (claim.Error ?? "ok") + ")");
                }
                else
                {
                    Require(true, "Already registered as verified owner " + prof.Data.Creator.Handle);
                }
            }

            studio.ShowManagement("Community");
            Require(studio.CurrentPage == "Community" && studio.Community != null, "Community tab opened");
            await WaitMs(2200);
            SaveShot("16-community-browse");

            // Open Profile dialog if unregistered or inspect profile
            studio.Community!.SetPresetCategoryAndRefresh("curve");
            await WaitMs(1000);
            SaveShot("17-community-curves-filter");

            // Switch Community sub-tabs
            // Note: click Leaderboard tab cleanly
            foreach (var child in FindButtonsByText(studio.Community.Body, "Leaderboard"))
            {
                child.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await WaitMs(1200);
                SaveShot("18-community-leaderboard");
                break;
            }

            File.WriteAllLines(Path.Combine(outputDir, "checks.txt"), checks);
        }
        finally
        {
            window.Host.Studio?.SendOfflinePulseSync();
            window.Close();
        }
    }

    private static IEnumerable<Button> FindButtonsByText(DependencyObject root, string text)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button b)
            {
                if (b.Content is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb && tb.Text == text)
                    yield return b;
                else if (Equals(b.Content, text))
                    yield return b;
            }
            foreach (var sub in FindButtonsByText(child, text))
                yield return sub;
        }
    }
}
