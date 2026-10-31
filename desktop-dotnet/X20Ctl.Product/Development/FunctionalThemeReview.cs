using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;

/// <summary>One theme applied, its pages rendered (--review-themes DIRECTORY THEME). Nothing is shown: the interface is
/// hosted in a hidden HwndSource, so it loads and lays out as in the app, and each picture is the element's own render,
/// never the screen.</summary>
internal static class FunctionalThemeReview
{
    public static async Task Run(string directory, string id)
    {
        if (id != "rebel") throw new ArgumentException("Functional regression suite is scoped to Rebel.");
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var theme = ThemeCatalog.Find(id); if (theme.Id != id) throw new ArgumentException("Unknown theme " + id);
        ThemeCatalog.Apply(theme);
        var bindings = new StringWriter(); using var listener = new TextWriterTraceListener(bindings);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var shots = new List<string>();
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments-fixture.json")) { Background = DS.Brush("Ink") };
        if (id == "rebel")
        {
            if (!PadColours.Catalog.Has("x20", "black") || !PadColours.Catalog.Has("x15", "starfield")) throw new InvalidOperationException("The approved Rebel comparison finishes are unavailable.");
            PadColours.Set(0, "x20", "black"); PadColours.Set(1, "x15", "starfield");
        }
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("x20ctl theme review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        try
        {
            await Shot("1-zone");
            host.Zone.AssignController(0, host.Catalog.Get("x20")); host.Zone.AssignController(1, host.Catalog.Get("x15")); await host.Zone.FocusPlayerAsync(null); await Shot("2-zone-player");
            await host.Zone.FocusPlayerAsync(0); await Shot("2b-hero");

            await host.Zone.FocusPlayerAsync(null);
            host.EnterStudio(0); await Shot("3-dashboard", 1400);
            var studio = host.Studio!;

            if (id == "rebel") studio.Draft.Set("A", "B");
            studio.ShowButtons(); studio.SelectControl("A"); await Shot("4-buttons");


            if (id == "rebel")
            {
                if (!studio.CapturingButtonEditor || studio.InputContext != X20Ctl.Zone.InputOwner.RemapCapture) throw new InvalidOperationException("The Buttons editor must retain physical control capture.");
                studio.SelectPhysicalButton("X"); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (studio.SelectedControl != "X" || !Descendants(studio).OfType<System.Windows.Controls.Button>().Any(button => button.Tag as string == "row:X")) throw new InvalidOperationException("A physical selection must update the Rebel Buttons page.");
                studio.SelectControl("A"); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var outputButtons = Descendants(studio).OfType<System.Windows.Controls.Button>().Where(button => button.Tag is string tag && tag.StartsWith("out:")).ToArray();
                foreach (string target in X20Ctl.Zone.ButtonDraft.Targets) if (!outputButtons.Any(button => button.Tag as string == "out:" + target)) throw new InvalidOperationException("Missing Rebel mapping target: " + target);
                var viewTarget = outputButtons.Single(button => button.Tag as string == "out:SELECT");
                DependencyObject? ancestor = viewTarget; while (ancestor != null && ancestor is not System.Windows.Controls.ScrollViewer) ancestor = VisualTreeHelper.GetParent(ancestor);
                if (ancestor is not System.Windows.Controls.ScrollViewer outputScroll) throw new InvalidOperationException("All mapping targets must be reachable through a scrolling picker.");
                viewTarget.BringIntoView(); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); outputScroll.UpdateLayout();
                var targetBounds = viewTarget.TransformToAncestor(outputScroll).TransformBounds(new Rect(viewTarget.RenderSize));
                if (targetBounds.Top < 0 || targetBounds.Bottom > outputScroll.ActualHeight + 1) throw new InvalidOperationException("The system mapping targets must scroll into view.");
                viewTarget.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (studio.Draft.Target("A") != "SELECT") throw new InvalidOperationException("The Rebel output picker must apply the selected supported target.");
                studio.Draft.Set("A", "B");
                var system = Descendants(studio).OfType<System.Windows.Controls.Button>().First(button => button.Tag as string == "group:System");
                system.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (studio.SelectedControl is not ("SELECT" or "START" or "HOME" or "CAPTURE" or "TURBO") || studio.Draft.CanEdit(studio.SelectedControl))
                    throw new InvalidOperationException("Rebel must expose system controls without enabling unsupported remapping.");
                await Shot("4b-buttons-system");
                var paddles = Descendants(studio).OfType<System.Windows.Controls.Button>().FirstOrDefault(button => button.Tag as string == "group:Paddles");
                if (paddles != null)
                {
                    paddles.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                    var paddle = Descendants(studio).OfType<System.Windows.Controls.Button>().First(button => button.Tag as string == "row:" + studio.SelectedControl);
                    paddle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    if (!studio.IsMacros) throw new InvalidOperationException("A paddle must open its existing macro slot.");
                    studio.ShowButtons();
                }
                studio.SelectControl("A");
            }
            studio.ShowCurves(true); await Shot("5-curves");

            studio.ShowMacros(true); if (studio.Macros is { } macros && macros.Draft.CanEdit && macros.Draft[macros.Slot].Count == 0) { for (int i = 0; i < 3; i++) macros.Add(); macros.Select(1); } await Shot("6-macros");

            studio.ShowVibration(true);
            await Shot("7-vibration");

            studio.ShowManagement("Device"); await Shot("8-device");
            foreach (var (page, shot) in new[] { ("PcMode", "8b-pcmode"), ("Mouse", "8c-mouse"), ("Setups", "8d-setups"), ("Tester", "8e-tester"), ("Diagnostics", "8f-diagnostics") }) { studio.ShowManagement(page); if (page == "Setups" && studio.Setups is { } setups && setups.Cards.Count == 0) { for (int i = 0; i < 3; i++) setups.Create(false); setups.Select(setups.Store.Items[0].Id); } await Shot(shot); }
            studio.ShowCurves(true); studio.OpenStickTools(); await Shot("8g-advanced");
            if (id == "rebel" && !Descendants(studio.StickTools!).OfType<FrameworkElement>().Any(e => e.Name == "RebelAdvanced"))
                throw new InvalidOperationException("Rebel Advanced stick tools must use the case-file composition.");
            if (id == "rebel")
            {
                var advanced = studio.StickTools!;
                var inner = Descendants(advanced).OfType<System.Windows.Controls.Slider>().Single(control => System.Windows.Automation.AutomationProperties.GetName(control) == "Inner dead zone");
                double original = inner.Value; inner.Value = 3.1;
                var copy = Descendants(advanced).OfType<Rebel.SlabButton>().Single(button => button.Caption == "Copy to right stick"); copy.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                advanced.SelectStick(X20Ctl.Zone.CurveChannel.RightStick);
                var right = Descendants(advanced).OfType<System.Windows.Controls.Slider>().Single(control => System.Windows.Automation.AutomationProperties.GetName(control) == "Inner dead zone");
                if (ReferenceEquals(inner, right) || Math.Abs(right.Value - 3.1) > .001) throw new InvalidOperationException("Rebel advanced copy must update the other native stick settings.");
                var raw = Descendants(advanced).OfType<System.Windows.Controls.CheckBox>().Single(control => System.Windows.Automation.AutomationProperties.GetName(control) == "Bypass X20CTL processing"); raw.IsChecked = true;
                if (right.IsEnabled) throw new InvalidOperationException("Bypassed stick tuning must be disabled in the Rebel controls.");
                raw.IsChecked = false; right.Value = original; advanced.SelectStick(X20Ctl.Zone.CurveChannel.LeftStick); inner.Value = original;
                File.WriteAllText(Path.Combine(directory, "advanced-interactions.json"), JsonSerializer.Serialize(new { nativeSliders = true, copyToOtherStick = true, rawDisablesTuning = true }));
            }
            studio.CloseStickTools();
            studio.OpenAgent(); await Shot("8h-agent-empty");
            if (id == "rebel" && !Descendants(studio.AgentWindow!.Body).OfType<FrameworkElement>().Any(e => e.Name == "RebelAgent"))
                throw new InvalidOperationException("Rebel Agent must use the crew-phone conversation composition.");
            var explanation = await studio.AgentWindow!.Ask("what does the dead zone do");
            if (explanation is { Outcome.Changes.Count: > 0 }) throw new InvalidOperationException("An Agent explanation must not modify settings.");
            await Shot("8i-agent-answer");
            studio.AgentWindow.ShowSettingsPage(true); await Shot("8j-agent-settings"); studio.AgentWindow.ShowSettingsPage(false);
            studio.CloseAgent();
            host.ReturnToZone(); host.ShowSettings(); await Shot("9-tools");
            if (id == "rebel")
            {
                host.Settings!.ShowPcMode(true); await Shot("9b-tools-pcmode");
                if (!Descendants(host.Settings).OfType<FrameworkElement>().Any(e => e.Name == "RebelToolsPcMode")) throw new InvalidOperationException("Tools PC mode must reuse the Rebel heist-map composition.");
                host.Settings.ShowPcMode(false); host.Settings.ShowPcMode(true); await Shot("9c-tools-pcmode-return"); host.Settings.ShowPcMode(false);
            }
            host.Settings!.ShowThemes(true); await Shot("10-themes");
            if (id == "rebel")
            {
                var expectedTags = ThemeCatalog.All.Select(theme => "th:" + theme.Id).ToHashSet();
                var cards = Descendants(host.Settings).OfType<System.Windows.Controls.Button>().Where(button => button.Tag is string tag && expectedTags.Contains(tag)).ToArray();
                if (cards.Length != 9) throw new InvalidOperationException("Rebel's calling-card theme wall must contain all nine themes, including Default.");
                cards.Single(button => button.Tag as string == "th:dusk").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (ThemeCatalog.Current.Id != "rebel" || !Descendants(host.Settings).OfType<System.Windows.Controls.Button>().Any(button => button.Tag as string == "th:dusk"))
                    throw new InvalidOperationException("Selecting a future theme must keep the picker in the active Rebel composition.");
                await Shot("10b-themes-next");
                Descendants(host.Settings).OfType<System.Windows.Controls.Button>().Single(button => button.Tag as string == "th:rebel").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
            host.Settings.ShowThemes(false); host.Settings.ShowAbout(true); await Shot("11-about");
            host.Settings.ShowAbout(false); host.Settings.ShowFeedback(true); await Shot("11b-feedback");
            if (id == "rebel" && !Descendants(host.Settings).OfType<FrameworkElement>().Any(e => e.Name == "RebelFeedback"))
                throw new InvalidOperationException("Rebel Feedback must use the ransom-note form composition.");
            host.Settings.ShowFeedback(false);
            host.CloseSettings(); host.OpenSelector(2); await Shot("11c-choose");
            if (id == "rebel" && !Descendants(host.Selector!).OfType<FrameworkElement>().Any(e => e.Name == "RebelChoose"))
                throw new InvalidOperationException("Rebel Choose controller must use the lineup composition.");
            host.CloseSelector();
            host.OpenCheck(); await Shot("11d-check");
            if (id == "rebel" && !Descendants(host.Check!).OfType<FrameworkElement>().Any(e => e.Name == "RebelReadScan"))
                throw new InvalidOperationException("Rebel Read scan must use the planning-board composition.");
            if (id == "rebel")
            {
                host.Check!.ReviewShowLive(); await Shot("11d2-check-live");
                if (!Descendants(host.Check).OfType<FrameworkElement>().Any(e => e.Name == "RebelReadFlow")) throw new InvalidOperationException("The Read scan live page must use the Rebel composition.");
                var slots = Descendants(host.Check).OfType<System.Windows.Controls.Button>().Where(button => System.Windows.Automation.AutomationProperties.GetName(button).StartsWith("Player slot ")).ToArray();
                if (slots.Length != 4 || !Descendants(host.Check).OfType<System.Windows.Controls.ScrollViewer>().Any(scroll => slots.All(scroll.IsAncestorOf))) throw new InvalidOperationException("All four player slots must remain reachable in the live scan.");
                host.Check.ReviewShowResults(); await Shot("11d3-check-results");
                if (!Descendants(host.Check).OfType<FrameworkElement>().Any(e => e.Name == "RebelReadResults")) throw new InvalidOperationException("Read scan results must use the Rebel composition.");
                if (!Descendants(host.Check).OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text == "Not saved or sent.")) throw new InvalidOperationException("An unsaved scan must not claim its report was kept.");
            }
            host.CloseCheck(); host.OpenWriteScan(); await Shot("11e-writescan");
            if (id == "rebel" && !Descendants(host.WriteScan!).OfType<FrameworkElement>().Any(e => e.Name == "RebelWriteScan"))
                throw new InvalidOperationException("Rebel Write scan must use the time-card composition.");
            if (id == "rebel")
            {
                host.WriteScan!.ReviewOpen(0, new X20Ctl.Zone.Capture.TestPlan("X20", "KeyLinker", "Vibration strength", "20", "30"));
                await Shot("11f-write-plan");
                if (!Descendants(host.WriteScan).OfType<FrameworkElement>().Any(e => e.Name == "RebelWritePlan")) throw new InvalidOperationException("The Write scan plan must use the Rebel form composition.");
                var settingField = Descendants(host.WriteScan).OfType<System.Windows.Controls.TextBox>().Single(field => System.Windows.Automation.AutomationProperties.GetName(field) == "The setting you'll change");
                var next = Descendants(host.WriteScan).OfType<System.Windows.Controls.Button>().Single(button => button.Content is System.Windows.Controls.StackPanel content && content.Children.OfType<System.Windows.Controls.TextBlock>().Any(text => text.Text == "Next: prepare the phone"));
                settingField.Text = ""; if (next.IsEnabled) throw new InvalidOperationException("The Write scan plan must reject an empty setting.");
                settingField.Text = "Vibration strength"; if (!next.IsEnabled) throw new InvalidOperationException("The valid Write scan plan must enable Next.");
                host.WriteScan.ReviewOpen(1); await Shot("11g-write-phone");
                var guide = Descendants(host.WriteScan).OfType<Scanner.PhoneGuide>().Single();
                if (!Descendants(guide).OfType<FrameworkElement>().Any(e => e.Name == "RebelPhoneGuide")) throw new InvalidOperationException("The phone guide must use the Rebel composition.");
                var guideBounds = guide.TransformToAncestor(host).TransformBounds(new Rect(guide.RenderSize));
                if (guideBounds.Top < 250 || guideBounds.Bottom > 825) throw new InvalidOperationException($"Phone guide escapes the themed stage: {guideBounds}.");
                guide.Go(1); if (guide.Index != 1) throw new InvalidOperationException("Phone guide navigation failed."); guide.Stuck(true); await Shot("11i-write-phone-help");
                host.WriteScan.ReviewOpen(2); await Shot("11j-write-test");
                if (!Descendants(host.WriteScan).OfType<FrameworkElement>().Any(e => e.Name == "RebelWriteTest")) throw new InvalidOperationException("The guided test must use the Rebel composition.");
                host.WriteScan.ReviewOpen(3); await Shot("11k-write-recording");
                host.WriteScan.ReviewFixtureResults(); await Shot("11l-write-results");
                if (!Descendants(host.WriteScan).OfType<FrameworkElement>().Any(e => e.Name == "RebelWriteResults")) throw new InvalidOperationException("Write scan results must use the Rebel composition.");
                var sendChoice = Descendants(host.WriteScan).OfType<System.Windows.Controls.Button>().First(button => button.Content is System.Windows.Controls.StackPanel content && content.Children.OfType<System.Windows.Controls.TextBlock>().Any(text => text.Text == "Send online"));
                sendChoice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (!host.WriteScan.ReviewConsentOpen || host.WriteScan.ReviewSendEnabled) throw new InvalidOperationException("Write scan must require consent before sending.");
                var resultScroll = Descendants(host.WriteScan).OfType<System.Windows.Controls.ScrollViewer>().First(scroll => scroll.Content is System.Windows.Controls.DockPanel);
                resultScroll.ScrollToEnd(); await Shot("11m-write-send-consent");
                if (!host.WriteScan.ReviewSaveFixture()) throw new InvalidOperationException("Fixture save must complete without writing a file.");
                host.WriteScan.ReviewOpen(0); await Shot("11h-write-plan-return");
            }
            host.CloseWriteScan();
            if (id == "rebel" && host.WriteScan != null) throw new InvalidOperationException("The fixture Write scan must close after saving its findings.");
            if (id == "rebel") { await CheckRebelNavigation(); await CheckRebelIntro(); }
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
        listener.Flush(); File.WriteAllText(Path.Combine(directory, "bindings.txt"), bindings.ToString());
        string Hex(string key) => DS.Brush(key) is SolidColorBrush b ? b.Color.ToString() : DS.Brush(key).GetType().Name;
        File.WriteAllText(Path.Combine(directory, "review.json"), JsonSerializer.Serialize(new
        {
            theme = theme.Id, windowShown = false, shots, bindingErrors = bindings.ToString().Length,
            resolved = new { text = Hex("DS.Text"), accent = Hex("DS.Accent"), backdrop = Hex("Zone.Backdrop"), ink = Hex("Ink"), display = DS.Display.Source, body = DS.Body.Source },
            frozen = new { text = DS.Brush("DS.Text").IsFrozen, glass = DS.Brush("DS.Glass").IsFrozen }
        }, new JsonSerializerOptions { WriteIndented = true }));

        async Task Shot(string name, int settle = 700)
        {
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var filter = Environment.GetEnvironmentVariable("X20CTL_REVIEW_SHOTS");
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(name))
            { stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout(); await Task.Delay(60); return; }
            await Task.Delay(settle);
            foreach (var (width, height, dpi, suffix) in new[] { (1600, 900, 96, ""), (1040, 700, 96, "-minimum"), (1600, 900, 120, "-dpi125"), (1600, 900, 144, "-dpi150") })
            {
                stage.Measure(new Size(width, height)); stage.Arrange(new Rect(0, 0, width, height)); stage.UpdateLayout();
                var image = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); image.Render(stage);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(directory, name + suffix + ".png")); encoder.Save(stream);
            }
            stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout();
            shots.Add(name);
        }

        async Task CheckRebelIntro()
        {
            var spec = ThemeCatalog.Current.Intro!;
            var expectedLanding = host.Zone.MarkTarget(host)!.Value;
            var intro = new IntroView(false, true, host.Zone);
            host.Children.Add(intro); System.Windows.Controls.Panel.SetZIndex(intro, 50); intro.Seek(0);
            for (int i = 0; i < 100 && !intro.FilmOpened; i++) await Task.Delay(100);
            bool opened = intro.FilmOpened; if (!opened) throw new InvalidOperationException($"{id} intro film did not open in the offscreen player within 10 seconds.");
            var cover = Descendants(intro).OfType<System.Windows.Controls.Image>().Single(i => i.Source is BitmapSource bitmap && bitmap.PixelWidth == spec.Image.Width && bitmap.PixelHeight == spec.Image.Height);
            Rect InkBounds()
            {
                double k = cover.ActualWidth / spec.Image.Width;
                return cover.TransformToAncestor(host).TransformBounds(new Rect(spec.Ink.X * k, spec.Ink.Y * k, spec.Ink.Width * k, spec.Ink.Height * k));
            }
            foreach (double t in new[] { .2, .5, .9, 1.3, 1.5, 1.9, 2.2, spec.CoverAt, spec.FlyAt, spec.FlyAt + .2, spec.FlyAt + .6, spec.FlyAt + .8 })
            {
                intro.Seek(t); await Shot("intro-" + t.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), 180);
                if (Math.Abs(t - spec.CoverAt) < .0001)
                {
                    var box = InkBounds(); var expected = new Rect(spec.FilmLogo.X / 1.2, spec.FilmLogo.Y / 1.2, spec.FilmLogo.Width / 1.2, spec.FilmLogo.Height / 1.2);
                    if (!Near(box, expected)) throw new InvalidOperationException($"Rebel intro cover mismatch: {box} vs {expected}.");
                }
            }
            var landed = InkBounds();
            if (!Near(landed, expectedLanding)) throw new InvalidOperationException($"Rebel intro landing mismatch: {landed} vs {expectedLanding}.");
            bool muteHidden = intro.Mute.Visibility == Visibility.Collapsed; intro.Finish(); host.Children.Remove(intro);
            var reduced = new IntroView(true, true, host.Zone); host.Children.Add(reduced); reduced.Seek(.4);
            bool reducedDone = reduced.Opacity == 0 && !reduced.FilmOpened; host.Children.Remove(reduced);
            File.WriteAllText(Path.Combine(directory, "intro-integration.json"), JsonSerializer.Serialize(new { filmOpened = opened, coverAligned = true, landingAligned = true, muteHidden, reducedDone, expectedLanding, landed }, new JsonSerializerOptions { WriteIndented = true }));
            if (!muteHidden || !reducedDone) throw new InvalidOperationException("Rebel intro mute or reduced-motion behavior failed.");
        }

        static bool Near(Rect a, Rect b) => Math.Abs(a.X - b.X) < .15 && Math.Abs(a.Y - b.Y) < .15 && Math.Abs(a.Width - b.Width) < .15 && Math.Abs(a.Height - b.Height) < .15;

        async Task CheckRebelNavigation()
        {
            host.CloseSettings(); host.EnterStudio(0); await Task.Delay(250);
            var studio = host.Studio!; studio.Draft.Set("A", "B");
            var tools = Descendants(studio).OfType<System.Windows.Controls.Button>().First(b => b.Tag as string == "tools");
            tools.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            if (!ReferenceEquals(studio, host.Studio)) throw new InvalidOperationException("Opening Rebel Tools must preserve the Studio instance and its current page.");
            host.CloseSettings();
            if (!ReferenceEquals(studio, host.Studio) || studio.ManagementPage != "Dashboard" || studio.Draft.Target("A") != "B")
                throw new InvalidOperationException("Back from Rebel Tools must restore Dashboard and its working draft.");
            studio.ShowButtons();
            if (host.ExpectedNativeContext != "TESTER_CAPTURE") throw new InvalidOperationException("Buttons must own native capture before opening Tools.");
            host.ShowSettings();
            if (host.ExpectedNativeContext != "UI_NAVIGATION" || host.NativeInputStudio != null)
                throw new InvalidOperationException("Rebel Tools must own navigation while the preserved editor is behind it.");
            if (!host.HasUnsavedThemeDrafts) throw new InvalidOperationException("The edited controller draft must be detected before a restart.");
            host.RequestRebelThemeRestart(); await Shot("13-restart-notice");
            if (host.ActiveRebelNotice == null || host.ExpectedNativeContext != "UI_NAVIGATION") throw new InvalidOperationException("Restart with unsaved drafts must open the themed confirmation and own navigation.");
            host.ActiveRebelNotice.No.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (host.ActiveRebelNotice != null || studio.Draft.Target("A") != "B") throw new InvalidOperationException("Declining restart must preserve the current draft and dismiss its notice.");
            bool? noticeResult = null;
            host.ShowRebelNotice("PC mode drivers", "PC mode needs two small drivers.", ["ViGEmBus: the virtual Xbox controller that games see", "HidHide: hides the physical pad so games don't see it twice"], "They are the signed installers from Nefarius Software Solutions, packaged with X20CTL. Windows will ask for permission. You can remove them in Windows Apps.", "Install now?", accepted => noticeResult = accepted);
            await Shot("14-driver-notice");
            host.ActiveRebelNotice!.No.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (noticeResult != false || host.ActiveRebelNotice != null) throw new InvalidOperationException("Driver notice must decline without running setup.");
            host.CloseSettings();
            if (host.ExpectedNativeContext != "TESTER_CAPTURE" || !ReferenceEquals(host.NativeInputStudio, studio))
                throw new InvalidOperationException("Closing Rebel Tools must restore the editor input owner.");
            foreach (string target in new[] { "buttons", "curves", "macros", "vibration", "pcmode", "device", "mouse", "setups" })
            {
                host.ShowSettings(); await Task.Delay(80);
                var choice = Descendants(host.Settings!).OfType<System.Windows.Controls.Button>().First(b => b.Tag as string == target);
                choice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.Delay(100);
                if (!ReferenceEquals(studio, host.Studio) || host.Settings != null || studio.Draft.Target("A") != "B")
                    throw new InvalidOperationException("Tools navigation lost Studio context: " + target);
                string active = studio.ManagementPage?.ToLowerInvariant() ?? (studio.IsCurves ? "curves" : studio.IsMacros ? "macros" : studio.IsVibration ? "vibration" : "buttons");
                if (active != target) throw new InvalidOperationException($"Tools navigation requested {target}, opened {active}.");
                if (target == "setups" && !Descendants(studio).OfType<Rebel.PageSlab>().Any(b => b.Tag as string == "setups" && b.On))
                    throw new InvalidOperationException("The Setups navigation icon must be lit on Setups.");
            }
            File.WriteAllText(Path.Combine(directory, "navigation.json"), JsonSerializer.Serialize(new { toolsPreservesStudio = true, backRestoresDashboard = true, draftsPreserved = true, setupsLit = true, targets = 8 }));
            var headerTools = Descendants(studio).OfType<System.Windows.Controls.Button>().First(b => b.Tag as string == "rebel:chrome:tools");
            headerTools.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (host.Settings == null || !ReferenceEquals(studio, host.Studio)) throw new InvalidOperationException("Top-bar Tools lost the current Studio.");
            host.Settings.Back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            if (host.Settings != null || studio.ManagementPage != "Setups") throw new InvalidOperationException("Tools Back must restore the previous Setups page.");
            host.Zone.UpdateNotice.Content = new System.Windows.Controls.Button { Content = "Update available · X20CTL V0.0xx", Height = 32, Foreground = Rebel.Ink.B(Rebel.Ink.White), Background = Rebel.Ink.B(Rebel.Ink.Black) };
            host.ReturnToZone(); await Task.Delay(350); await Shot("12-zone-chrome");
            var chrome = Bounds(host.Zone.HeaderActions); var brand = Bounds(host.Zone.BrandTile);
            if (chrome.IntersectsWith(brand)) throw new InvalidOperationException("Rebel window controls must not overlap the brand mark.");
            var title = Descendants(host.Zone).OfType<FrameworkElement>().Single(e => e.Name == "RebelZoneTitle");
            if (Bounds(host.Zone.UpdateNotice).IntersectsWith(Bounds(title))) throw new InvalidOperationException("Rebel update notices must not overlap the page title.");
            var spec = ThemeCatalog.Current.Intro!;
            var mark = host.Zone.BrandMark;
            double scale = Math.Min(mark.ActualWidth / spec.Image.Width, mark.ActualHeight / spec.Image.Height);
            var ink = new Rect((mark.ActualWidth - spec.Image.Width * scale) / 2 + spec.Ink.X * scale,
                (mark.ActualHeight - spec.Image.Height * scale) / 2 + spec.Ink.Y * scale, spec.Ink.Width * scale, spec.Ink.Height * scale);
            var expected = mark.TransformToAncestor(host).TransformBounds(ink);
            var actual = host.Zone.MarkTarget(host);
            if (actual == null || Math.Abs(actual.Value.Width - expected.Width) > .1 || Math.Abs(actual.Value.X - expected.X) > .1 || Math.Abs(actual.Value.Y - expected.Y) > .1)
                throw new InvalidOperationException($"Rebel intro must land on the themed logo ink: expected {expected}, got {actual}.");
        }

        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(host).TransformBounds(new Rect(element.RenderSize));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child == null) continue; yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}


