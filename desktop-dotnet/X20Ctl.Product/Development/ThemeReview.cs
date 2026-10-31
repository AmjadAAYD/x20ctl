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
internal static class ThemeReview
{
    public static async Task Run(string directory, string id)
    {
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var theme = ThemeCatalog.Find(id); if (theme.Id != id) throw new ArgumentException("Unknown theme " + id);
        ThemeCatalog.Apply(theme);
        var bindings = new StringWriter(); using var listener = new TextWriterTraceListener(bindings);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var shots = new List<string>();
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments-fixture.json")) { Background = DS.Brush("Ink") };
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("x20ctl theme review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        try
        {
            await Shot("1-zone");
            host.Zone.AssignController(0, host.Catalog.Get("x20")); host.Zone.AssignController(1, host.Catalog.Get("x15")); await host.Zone.FocusPlayerAsync(null); await Shot("2-zone-player");
            await host.Zone.FocusPlayerAsync(0); await Shot("2b-hero"); await host.Zone.FocusPlayerAsync(null);
            host.EnterStudio(0); await Shot("3-dashboard", 1400);
            var studio = host.Studio!;
            studio.ShowButtons(); studio.SelectControl("A"); await Shot("4-buttons");
            if (id is "dusk" or "volt" or "blossom" or "nexus" or "tide" or "spotlight")
            {
                var row = Descendants(studio).OfType<System.Windows.Controls.Button>().First(b => b.Tag as string == "theme:input:A");
                row.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await Idle();
                row = Descendants(studio).OfType<System.Windows.Controls.Button>().First(b => b.Tag as string == "theme:input:A");
                if (row.ContextMenu?.PlacementTarget is not DependencyObject target || !studio.IsAncestorOf(target)) throw new InvalidOperationException("Mapping menu was anchored to a detached row after selection refreshed the themed page.");
                var output = row.ContextMenu!.Items.OfType<System.Windows.Controls.MenuItem>().First(item => item.Tag as string == "theme:map:A:B");
                output.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (studio.Draft.Target("A") != "B" || studio.Draft.CanApplyHardware) throw new InvalidOperationException("Themed mapping must change the local draft and keep hardware locked.");
                host.ShowSettings(); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (!ReferenceEquals(host.Studio, studio) || studio.IsEnabled) throw new InvalidOperationException("Tools must retain and suspend the current Studio.");
                host.NavigateThemePage("Buttons"); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (!ReferenceEquals(host.Studio, studio) || !studio.IsEnabled || studio.Draft.Target("A") != "B") throw new InvalidOperationException("Tools navigation lost the working mapping.");
                File.WriteAllText(Path.Combine(directory, "mapping-regression.json"), JsonSerializer.Serialize(new { theme = id, localMapping = "A:B", toolsPreservesStudio = true, hardwareWrites = false }));
            }

            studio.ShowCurves(true); await Shot("5-curves");
            if (id is "dusk" or "volt" or "blossom" or "nexus" or "spotlight")
            {
                var slider = Descendants(studio).OfType<System.Windows.Controls.Slider>().First(control => control.Tag as string == "theme:curve:Inner dead zone"); slider.Value = 12;
                await Idle();
                var retainedSlider = Descendants(studio).OfType<System.Windows.Controls.Slider>().First(control => control.Tag as string == "theme:curve:Inner dead zone");
                if (!ReferenceEquals(slider, retainedSlider)) throw new InvalidOperationException(id + " replaced its curve slider during a value change, losing drag capture.");
                if (studio.Curves.Inner.Value != 12 || studio.Curves.Draft.CanApplyHardware) throw new InvalidOperationException("Dusk curve slider is not bound to the real local workbench.");
                if (id == "spotlight")
                {
                    slider.ApplyTemplate();
                    if (slider.Template.FindName("PART_Track", slider) is not System.Windows.Controls.Primitives.Track { Thumb: {} thumb }) throw new InvalidOperationException("Spotlight fader has no native drag track.");
                    thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
                    thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,-20) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                    thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,-20,false) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                    if (studio.Curves.Inner.Value <= 12) throw new InvalidOperationException("Dragging a Spotlight fader upward did not increase its real value.");
                }
                ClickTag("theme:curve:channel:RightTrigger"); await Idle();
                if (studio.Curves.Channel != X20Ctl.Zone.CurveChannel.RightTrigger) throw new InvalidOperationException("Dusk trigger channel was not selected.");
                ClickTag("theme:curve:channel:LeftStick"); await Idle();
            }
            studio.ShowMacros(true); if (studio.Macros is { } macros && macros.Draft.CanEdit && macros.Draft[macros.Slot].Count == 0) { for (int i = 0; i < 3; i++) macros.Add(); macros.Select(1); } await Shot("6-macros");
            if (id is "dusk" or "blossom" or "volt")
            {
                var macro = studio.Macros!; int count = macro.Draft[macro.Slot].Count;
                ClickTag("theme:macro:add"); await Idle();
                if (macro.Draft[macro.Slot].Count != count + 1) throw new InvalidOperationException("Dusk add-event button did not edit the sequence.");
                ClickTag("theme:macro:input:X"); await Idle();
                if (!macro.Draft[macro.Slot][macro.SelectedIndex].Buttons.Contains("X")) throw new InvalidOperationException("Dusk macro chord was not saved in the selected event.");
                ClickTag("theme:macro:time:Hold:5"); await Idle();
                if (macro.Draft[macro.Slot][macro.SelectedIndex].HoldMs != 45) throw new InvalidOperationException("Dusk macro timing did not reach the real event.");
                if (id == "blossom")
                {
                    var heading = Descendants(studio).OfType<System.Windows.Controls.ComboBox>().First(control => control.Tag as string == "theme:macro:stick:left");
                    heading.SelectedItem = X20Ctl.Zone.StickHeading.Up; await Idle();
                    if (macro.Draft[macro.Slot][macro.SelectedIndex].Left != X20Ctl.Zone.StickHeading.Up) throw new InvalidOperationException("Yozakura stick heading was not saved.");
                }
                ClickTag("theme:macro:record"); await Idle();
                if (!macro.IsRecording) throw new InvalidOperationException("Dusk Record button did not start the countdown.");
                host.ShowSettings(); await Idle();
                bool hiddenRecording = macro.IsRecording;
                if (hiddenRecording) macro.ToggleRecording(); // Cancel before a failed assertion can leave a recorder active.
                host.CloseSettings();
                if (hiddenRecording) throw new InvalidOperationException("Macro recording countdown continued after Tools hid its themed editor.");
                await Idle();
                var recordButton = Descendants(studio).OfType<System.Windows.Controls.Button>().First(button => button.Tag as string == "theme:macro:record");
                if (id == "dusk" && recordButton.Content is not System.Windows.Controls.Border { Child: System.Windows.Controls.TextBlock { Text: "Record" } }) throw new InvalidOperationException("Dusk recording action retained a stale Stop label after cancellation.");
            }
            studio.ShowVibration(true); await Shot("7-vibration");
            if (id is "dusk" or "volt" or "blossom")
            {
                var slider = Descendants(studio).OfType<System.Windows.Controls.Slider>().First(control => control.Tag as string == "theme:vibration:strength"); slider.Value = 35;
                if (studio.Vibration!.Strength.Value != 35 || studio.Vibration.Draft.Strength != 35) throw new InvalidOperationException("Dusk linked strength was not saved to the local draft.");
            }
            studio.ShowManagement("Device"); await Shot("8-device");
            if (id is "dusk" or "volt" or "blossom" or "nexus" or "spotlight")
            {
                studio.Device!.SetFirmware("fixture-fw"); await Idle();
                if (!Descendants(studio).OfType<System.Windows.Controls.TextBlock>().Any(text => text.Text == "fixture-fw")) throw new InvalidOperationException(id + " Device page did not display reported firmware.");
                studio.Device.SetFirmware(null); await Idle();
            }
            foreach (var (page, shot) in new[] { ("PcMode", "8b-pcmode"), ("Mouse", "8c-mouse"), ("Setups", "8d-setups"), ("Tester", "8e-tester"), ("Diagnostics", "8f-diagnostics") })
            {
                studio.ShowManagement(page);
                if (page == "Setups" && studio.Setups is { } setups && setups.Cards.Count == 0) { for (int i = 0; i < 3; i++) setups.Create(false); setups.Select(setups.Store.Items[0].Id); }
                await Shot(shot);
                if (id is "volt" or "blossom" && page == "PcMode")
                {
                    foreach (var rk in new[] { "switch", "profile", "turbo", "games", "share", "reset", "notice" })
                    {
                        ClickTag("theme:pc:row:" + rk); await Idle();
                    }
                    ClickTag("theme:pc:agree"); await Idle();
                }
                if (id is "volt" or "blossom" && page == "Mouse")
                {
                    var mouse = studio.Mouse ?? throw new InvalidOperationException(id + " missing Mouse workbench");
                    if (mouse.Inputs.Count < 17)
                        throw new InvalidOperationException($"{id} Mouse workbench must expose all inputs (expected >= 17, got {mouse.Inputs.Count})");

                    string pickKey = mouse.Inputs.ElementAt(10);
                    ClickTag("theme:mouse:in:" + pickKey); await Idle();
                    if (mouse.SelectedKey != pickKey)
                        throw new InvalidOperationException($"{id} Mouse picking input {pickKey} failed.");

                    var testAction = X20Ctl.Zone.Mouse.MouseAction.MiddleClick;
                    ClickTag("theme:mouse:assign:" + testAction); await Idle();
                    if (mouse.CurrentAction != testAction)
                        throw new InvalidOperationException($"{id} Mouse failed to assign {testAction} to {pickKey}");

                    ClickTag("theme:mouse:tab:sensitivity"); await Idle();
                    var speedSlider = Descendants(studio).OfType<System.Windows.Controls.Slider>().FirstOrDefault(s => s.Tag as string == "theme:mouse:Pointer speed");
                    if (speedSlider != null)
                    {
                        double oldVal = speedSlider.Value;
                        speedSlider.Value = oldVal + 1.0; await Idle();
                        var retainedSlider = Descendants(studio).OfType<System.Windows.Controls.Slider>().FirstOrDefault(s => s.Tag as string == "theme:mouse:Pointer speed");
                        if (!ReferenceEquals(speedSlider, retainedSlider))
                            throw new InvalidOperationException($"{id} replaced pointer speed slider during ValueChanged.");
                    }
                }
                if (id is "volt" or "blossom" && page == "Setups")
                {
                    var setupBench = studio.Setups ?? throw new InvalidOperationException(id + " missing Setups workbench");

                    ClickTag("theme:setup:save"); await Idle();

                    int countBeforeDup = setupBench.Cards.Count;
                    ClickTag("theme:setup:duplicate"); await Idle();
                    if (setupBench.Cards.Count != countBeforeDup + 1)
                        throw new InvalidOperationException($"{id} duplicate action failed (expected {countBeforeDup + 1}, got {setupBench.Cards.Count})");

                    ClickTag("theme:setup:rename"); await Idle();
                    if (!setupBench.RenameActive)
                        throw new InvalidOperationException($"{id} rename action did not activate rename mode");
                    ClickTag("theme:setup:cancel-rename"); await Idle();
                    if (setupBench.RenameActive)
                        throw new InvalidOperationException($"{id} cancel-rename failed to clear rename mode");

                    ClickTag("theme:setup:rename"); await Idle();
                    setupBench.TakeNameField().Text = "Verified Setup";
                    ClickTag("theme:setup:save-name"); await Idle();
                    if (setupBench.RenameActive)
                        throw new InvalidOperationException($"{id} save-name failed to complete rename");

                    int countBeforeDel = setupBench.Cards.Count;
                    ClickTag("theme:setup:delete"); await Idle();
                    if (!setupBench.DeletePending)
                        throw new InvalidOperationException($"{id} delete must require confirmation");
                    ClickTag("theme:setup:keep"); await Idle();
                    if (setupBench.DeletePending || setupBench.Cards.Count != countBeforeDel)
                        throw new InvalidOperationException($"{id} keep setup failed to preserve library");
                }
                if (id == "dusk" && page == "Setups")
                {
                    ClickTag("theme:setup:duplicate"); await Idle();
                    if (studio.Setups!.Cards.Count != 4) throw new InvalidOperationException("Dusk duplicate action did not create a real saved setup.");
                    ClickTag("theme:setup:delete"); await Idle();
                    if (!studio.Setups.DeletePending) throw new InvalidOperationException("Dusk delete must ask for confirmation.");
                    ClickTag("theme:macro:setup-keep"); await Idle();
                    if (studio.Setups.DeletePending || studio.Setups.Cards.Count != 4) throw new InvalidOperationException("Dusk keep-setup action changed the saved library.");
                }
                if (id == "dusk" && page == "Tester")
                {
                    ClickTag("theme:tester:keyboard"); await Idle();
                    if (!studio.Tester!.KeyboardOn) throw new InvalidOperationException("Dusk keyboard test did not start.");
                    ClickTag("theme:tester:keyboard"); await Idle();
                    if (studio.Tester.KeyboardOn) throw new InvalidOperationException("Dusk keyboard test did not stop.");
                }
            }
            studio.ShowCurves(true); studio.OpenStickTools(); await Shot("8g-advanced");
            if (id == "spotlight")
            {
                var fader = Descendants(studio).OfType<FrameworkElement>().FirstOrDefault(e => e.Tag as string == "theme:stick:fader");
                if (fader != null)
                {
                    fader.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Up) { RoutedEvent = UIElement.KeyDownEvent });
                    await Idle();
                }
            }
            else if (id == "nexus")
            {
                var nexusSlider = Descendants(studio).OfType<System.Windows.Controls.Slider>().FirstOrDefault(s => (s.Tag as string)?.StartsWith("theme:stick:") == true);
                if (nexusSlider != null)
                {
                    double valBefore = nexusSlider.Value;
                    nexusSlider.Value = Math.Min(nexusSlider.Maximum, valBefore + 5.0);
                    await Idle();
                    var retainedSlider = Descendants(studio).OfType<System.Windows.Controls.Slider>().FirstOrDefault(s => (s.Tag as string)?.StartsWith("theme:stick:") == true);
                    if (!ReferenceEquals(nexusSlider, retainedSlider))
                        throw new InvalidOperationException("Nexus stick tools destroyed and recreated slider on value change.");
                }
            }
            else if (id == "tide")
            {
                var tideRow = Descendants(studio).OfType<FrameworkElement>().FirstOrDefault(e => (e.Tag as string)?.StartsWith("theme:stick:") == true);
                if (tideRow != null)
                {
                    tideRow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Right) { RoutedEvent = UIElement.KeyDownEvent });
                    await Idle();
                }
            }
            studio.CloseStickTools();
            studio.OpenAgent(); await Shot("8h-agent-empty");
            await studio.AgentWindow!.Ask("what does the dead zone do"); await Shot("8i-agent-answer");
            studio.AgentWindow.ShowSettingsPage(true); await Shot("8j-agent-settings"); studio.AgentWindow.ShowSettingsPage(false);
            studio.CloseAgent();
            if (id != "default")
            {
                host.ShowSettings(); await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                if (!ReferenceEquals(host.Studio, studio) || studio.IsEnabled) throw new InvalidOperationException("Tools failed to preserve Studio for " + id);
                host.CloseSettings();
                if (!ReferenceEquals(host.Studio, studio) || !studio.IsEnabled) throw new InvalidOperationException("Tools failed to restore Studio for " + id);
            }
            host.ReturnToZone(); host.ShowSettings(); await Shot("9-tools");
            host.Settings!.ShowPcMode(true); await Shot("9b-tools-pcmode"); host.Settings.ShowPcMode(false);
            host.Settings!.ShowThemes(true); await Shot("10-themes"); host.Settings.ShowThemes(false);
            host.Settings.ShowAbout(true); await Shot("11-about"); host.Settings.ShowAbout(false);
            host.Settings.ShowFeedback(true); await Shot("11b-feedback"); host.Settings.ShowFeedback(false);
            host.CloseSettings();
            host.OpenSelector(2); await Shot("11c-choose"); host.CloseSelector();
            host.OpenCheck(); await Shot("11d-check"); host.CloseCheck();
            host.OpenWriteScan(); await Shot("11e-writescan");
            host.WriteScan!.ReviewOpen(1, new X20Ctl.Zone.Capture.TestPlan("X20", "KeyLinker", "Vibration strength", "20", "30")); await Shot("12c-writescan-plan");
            host.CloseWriteScan();

            if (id == "pulse")
            {
                host.ShowPulseThemeRestartNotice("Pulse");
                await Shot("13-notice-restart");
                host.DismissPulseNotice();
                host.ShowPulseDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissPulseNotice();
            }
            if (id == "spotlight")
            {
                host.ShowSpotlightThemeRestartNotice("Spotlight");
                await Shot("13-notice-restart");
                host.DismissSpotlightNotice();
                host.ShowSpotlightDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissSpotlightNotice();
            }
            if (id == "nexus")
            {
                host.ShowNexusThemeRestartNotice("Nexus");
                await Shot("13-notice-restart");
                host.DismissNexusNotice();
                host.ShowNexusDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissNexusNotice();
            }
            if (id == "tide")
            {
                host.ShowTideThemeRestartNotice("Tide");
                await Shot("13-notice-restart");
                host.DismissTideNotice();
                host.ShowTideDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissTideNotice();
            }
            if (id == "hybrid")
            {
                host.ShowHybridNotice("Restart X20CTL?", "Unsaved controller drafts will be lost.", Array.Empty<string>(), "Save your work before restarting.", "Restart", _ => { });
                await Shot("13-notice-restart");
                host.ActiveHybridNotice?.Resolve(false);
                host.ShowHybridNotice("PC mode drivers", "PC mode needs two small drivers.", new[] { "ViGEmBus provides the virtual controller.", "HidHide prevents duplicate input." }, "Install the packaged signed drivers?", "Install", _ => { });
                await Shot("14-notice-drivers");
                host.ActiveHybridNotice?.Resolve(false);
            }
            if (id == "dusk")
            {
                host.ShowDuskThemeRestartNotice("Dusk");
                await Shot("13-notice-restart");
                host.DismissDuskNotice();
                host.ShowDuskDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissDuskNotice();
            }
            if (id == "volt")
            {
                host.ShowVoltThemeRestartNotice("Volt");
                await Shot("13-notice-restart");
                host.DismissVoltNotice();
                host.ShowVoltDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissVoltNotice();
            }
            if (id is "spotlight" or "nexus" or "tide" or "dusk" or "volt" or "blossom")
            {
                bool? consent = null;
                void Answer(bool value) => consent = value;
                switch (id)
                {
                    case "spotlight": host.ShowSpotlightDriverNotice(["ViGEmBus"], Answer); break;
                    case "nexus": host.ShowNexusDriverNotice(["ViGEmBus"], Answer); break;
                    case "tide": host.ShowTideDriverNotice(["ViGEmBus"], Answer); break;
                    case "dusk": host.ShowDuskDriverNotice(["ViGEmBus"], Answer); break;
                    case "volt": host.ShowVoltDriverNotice(["ViGEmBus"], Answer); break;
                    case "blossom": host.ShowBlossomDriverNotice(["ViGEmBus"], Answer); break;
                }
                await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var notice = host.ActiveThemeNotice ?? throw new InvalidOperationException("Driver consent notice is missing.");
                if (Descendants(notice).OfType<System.Windows.Controls.Button>().Count(button => button.Tag as string is "notice:yes" or "notice:no") != 2)
                    throw new InvalidOperationException("Driver consent needs two native keyboard-accessible actions.");
                if (host.NativeInputStudio != null) throw new InvalidOperationException("A modal must own input before the Studio.");
                host.OnKeyDown(host, new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
                if (host.ActiveThemeNotice != null || consent != false) throw new InvalidOperationException("Escape must decline the driver action and close the notice.");
                File.WriteAllText(Path.Combine(directory, "notice-regression.json"), JsonSerializer.Serialize(new { theme = id, nativeActions = 2, escapeDeclines = true, installsStarted = false }));
            }
            if (id is "pulse" or "spotlight")
            {
                host.EnterStudio(0); var before = host.Studio!; before.ShowManagement("Device"); await Idle();
                host.OpenWriteScan(); host.WriteScan!.ReviewFixtureResults(); await Idle(); var pending = host.WriteScan;
                if (id == "pulse") host.NavigatePulsePage("Buttons"); else host.NavigateSpotlightPage("Buttons"); await Idle();
                if (!ReferenceEquals(host.WriteScan, pending) || !ReferenceEquals(host.Studio, before) || before.CurrentPage != "Device") throw new InvalidOperationException("Navigation continued behind Write Scan after its unsaved-findings close guard refused to leave.");
                host.CloseWriteScan();
                if (host.WriteScan != null) throw new InvalidOperationException("Second explicit Back should permit discarding unsaved fixture findings.");
                File.WriteAllText(Path.Combine(directory, "scan-close-regression.json"), JsonSerializer.Serialize(new { navigationBlocked = true, explicitSecondBackCloses = true }));
            }
            if (id == "default")
            {
                foreach (string modelId in new[] { "x20", "x20_pro", "x15" })
                {
                    host.ReturnToZone(); host.Zone.AssignController(0, host.Catalog.Get(modelId)); host.EnterStudio(0);
                    var editor = host.Studio!; editor.ShowButtons(); bool expected = modelId is "x20" or "x20_pro";
                    if (editor.RearViewAvailable != expected) throw new InvalidOperationException("Rear artwork gating after opening Buttons is incorrect for " + modelId);
                    editor.ShowCurves(true); editor.ShowCurves(false);
                    if (editor.RearViewAvailable != expected) throw new InvalidOperationException("Rear artwork gating after leaving Curves is incorrect for " + modelId);
                    if (editor.Draft.CanApplyHardware || editor.Curves.Draft.CanApplyHardware) throw new InvalidOperationException("Artwork availability must not enable native configuration writes.");
                    if (ModelProfiles.For(modelId).SupportsLocalDrafts != expected) throw new InvalidOperationException("Local draft availability is incorrect for " + modelId);
                }
                File.WriteAllText(Path.Combine(directory, "rear-view-regression.json"), JsonSerializer.Serialize(new { x20 = true, x20Pro = true, unrelatedModel = false, hardwareWrites = false }));
            }
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

        async Task Idle() { await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); stage.Measure(new Size(1600,900)); stage.Arrange(new Rect(0,0,1600,900)); stage.UpdateLayout(); await Task.Delay(30); }
        void ClickTag(string tag)
        {
            var button = Descendants(host).OfType<System.Windows.Controls.Button>().FirstOrDefault(button => button.Tag as string == tag) ?? throw new InvalidOperationException("Missing native action " + tag);
            if (!button.IsEnabled) throw new InvalidOperationException("Native action unexpectedly disabled " + tag);
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }

        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (VisualTreeHelper.GetChild(root, i) is {} visualChild)
                    foreach (var child in Descendants(visualChild)) yield return child;
        }

        async Task Shot(string name, int settle = 700)
        {
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var filter = Environment.GetEnvironmentVariable("X20CTL_REVIEW_SHOTS");
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(name))
            { stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout(); await Task.Delay(60); return; }
            await Task.Delay(settle);
            var variants = new[] { (1600, 900, 96, ""), (1040, 700, 96, "-minimum"), (1600, 900, 120, "-dpi125"), (1600, 900, 144, "-dpi150") };
            if (Environment.GetEnvironmentVariable("X20CTL_REVIEW_BASE_ONLY") == "1") variants = variants.Take(1).ToArray();
            foreach (var (width, height, dpi, suffix) in variants)
            {
                stage.Measure(new Size(width, height)); stage.Arrange(new Rect(0, 0, width, height)); stage.UpdateLayout();
                var image = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); image.Render(stage);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(directory, name + suffix + ".png")); encoder.Save(stream);
            }
            stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout();
            shots.Add(name);
        }
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
