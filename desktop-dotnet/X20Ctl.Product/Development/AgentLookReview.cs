using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product.Agent;
using X20Ctl.Zone.Agent;
namespace X20Ctl.Product.Development;

/// <summary>
/// --review-agent-look DIRECTORY: how the agent looks, without the model. Offline mode answers, so it is quick and the
/// same every run: the window open, a search as it's typed and as it's sent, a change with its walk-through, and the window after. Captures are
/// rendered, never the screen.
/// </summary>
internal static class AgentLookReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        // every run starts from the defaults: the fixture settings and saved setups a run before left in this folder go,
        // and never the real ones, should the folder given be the app's own
        if (!string.Equals(Path.GetFullPath(directory).TrimEnd('\\'), Path.GetDirectoryName(AgentSetupStore.DefaultPath), StringComparison.OrdinalIgnoreCase))
            foreach (var name in new[] { "pc-mode-fixture.json", "agent-setups.json", "pad-colours-fixture.json", "agent-look.txt" })
                foreach (var file in new[] { name, name + ".bak" }) File.Delete(Path.Combine(directory, file));
        AgentPanel.UseLookFile(Path.Combine(directory, "agent-look.txt"));
        // the controller is never touched by a review: the lights and the stored settings are pretend, and every lights call
        // is logged, so a run shows what the agent would have written and that it put things back
        bool wasSandbox = ReviewSandbox.Active; ReviewSandbox.Active = true;
        var lights = Convert.FromHexString("0050FF0000140201001080400000FF408080808080A080FF");
        var lightsLog = new System.Text.StringBuilder();
        AgentPanel.LightsDevice = async (colour, mode) =>
        {
            await Task.Delay(900); // about what the pad takes over Bluetooth
            if (colour is { } c) lights = X20Ctl.Zone.Lighting.LightingRecord.WithColour(lights, c[0], c[1], c[2]);
            if (mode is { } m) lights = X20Ctl.Zone.Lighting.LightingRecord.WithMode(lights, m);
            lightsLog.AppendLine($"{DateTime.Now:HH:mm:ss.fff} {(colour == null && mode == null ? "read " : "write")} {Convert.ToHexString(lights)}");
            File.WriteAllText(Path.Combine(directory, "lights-log.txt"), lightsLog.ToString());
            return (byte[])lights.Clone();
        };
        AgentPanel.SettingsProbe = () => Task.FromResult<string?>(null);
        ReviewSandbox.HideLibrary = true;
        IntroView.SoundEnabled = false;
        ThemeCatalog.Apply(ThemeCatalog.Default);
        PadColours.Use(Path.Combine(directory, "pad-colours-fixture.json"));
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments.json"));
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("Offline agent review")
        {
            WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900,
            PositionX = -32000, PositionY = -32000
        }) { RootVisual = stage };
        try
        {
            Layout();
            host.Zone.AssignController(0, host.Catalog.Get("x20_pro")); host.EnterStudio(0); await Task.Delay(1400);
            var studio = host.Studio!; studio.ShowButtons();

            ReviewBattery(studio, host);
            await ReviewVibration(studio, "x20_pro");
            studio.ShowButtons();
            await Shot("0-closed");
            int stop = 0;
            studio.AgentTourStarted += tour => tour.Held += step => _ = Shot($"3-tour-{++stop}-{step.Target}");
            studio.OpenAgent(); var talk = studio.AgentWindow!;
            await Shot("1-open");
            talk.Search.Text = "sti"; await Shot("2-typing-sti"); talk.Search.Text = "sticks"; await Task.Delay(1500); await Shot("2-searched-sticks");
            await talk.Ask("What's 12 times 7?"); await Shot("2-silly");
            await talk.Ask("banana telescope"); await Shot("2-lost");
            await talk.Ask("EasySMX X20 Pro"); await Shot("2-about-controller");
            await talk.Ask("What's my current battery?"); await Shot("2-battery");
            await talk.Ask("Turbo on X, 12 a second"); await Shot("4-after");
            // a stick held for the review, not the real one: the right stick most of the way out, so the dots sit on the curves
            talk.LiveStick = () => (0, 0, 21000, 6000);
            await talk.Ask("Make my right stick good for sniping"); await Shot("5-after-curve");
            // a setup saved, a second change, the list of changes and setups, then back through them
            await talk.Ask("save this as Sniper"); await Shot("7-saved-setup");
            await talk.Ask("I want faster triggers for shooting"); await Shot("7b-triggers");
            talk.ShowSettingsPage(true); await Shot("8-settings-changes");
            talk.ShowSettingsPage(false);
            await talk.Ask("undo"); await Shot("9-undo");
            await talk.Ask("undo everything"); await Shot("9b-undo-everything");
            await talk.Ask("load Sniper"); await Shot("9c-load-sniper");
            await talk.Ask("what setups do I have?"); await Shot("9d-setups");
            foreach (var page in new[] { "Setups", "History", "Live", "Checkup", "Look" }) { talk.ReviewShow(page); await Task.Delay(400); await Shot("10-page-" + page.ToLowerInvariant()); }
            talk.ReviewShow("Chat");
            await talk.Ask("change my RGB to red"); await Task.Delay(3500); await Shot("9e-red-lights");
            // the heartbeat (owner, 8 Nov 2026: 4 seconds longer, 7.6 s): still beating late on, over afterwards; the rumbles it
            // asks for are only noted (a review never touches the real controller), to check their count and timing
            var pulses = new List<string>(); var pulseClock = System.Diagnostics.Stopwatch.StartNew();
            Agent.AgentRumble.ReviewPulse = (ms, left, right) => { lock (pulses) pulses.Add($"{pulseClock.ElapsedMilliseconds,6} ms  {ms} ms  {left}/{right}"); };
            await talk.Ask("I love you"); await Task.Delay(400); await Shot("11-love-thinking"); await Task.Delay(1700); await Shot("11-love-beat"); await Task.Delay(900); await Shot("11-love"); await Task.Delay(4000); await Shot("11-love-still-beating"); await Task.Delay(5600); await Shot("11-love-after");
            Agent.AgentRumble.ReviewPulse = null; lock (pulses) File.WriteAllLines(Path.Combine(directory, "love-rumble.txt"), pulses);
            talk.ReviewSpin(2); await Task.Delay(300); await Shot("12-two-turns-not-dizzy");
            talk.ReviewSpin(3); await Task.Delay(700); await Shot("12-dizzy"); talk.ReviewSpin(3); await Task.Delay(400); await Shot("12-dizzier");
            await Task.Delay(5200); await Shot("12-recovered");
            // the whole sentence is read: a colour for the controller is its lights, not another controller
            talk.ReviewShow("Chat");
            await talk.Ask("Change my controller to green"); await Task.Delay(3500); await Shot("13-controller-green");
            // what it can reach, and the check-up on its own screen, with a pretend pad whose left stick drifts 8%
            uint packet = 0; ushort[] flags = [0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x0020, 0x0010, 0x0040, 0x0080, 0x0001, 0x0002, 0x0004, 0x0008];
            AgentPanel.CheckInput = (step, t) =>
            {
                packet++;
                short C(double v) => (short)Math.Clamp(v * 32767, -32768, 32767);
                return step switch
                {
                    "Rest" => (2600, 1000, 120, -90, 0, 0, 0, packet),
                    "Left" => (C(.98 * Math.Cos(t * 3)), C(.98 * Math.Sin(t * 3)), 0, 0, 0, 0, 0, packet),
                    "Right" => (0, 0, C(Math.Cos(t * 3)), C(Math.Sin(t * 3)), 0, 0, 0, packet),
                    "Triggers" => (0, 0, 0, 0, (byte)(t < 1.2 ? Math.Min(255, t * 400) : 0), (byte)(t < 1.4 ? Math.Min(255, t * 300) : 0), 0, packet),
                    "Buttons" => (0, 0, 0, 0, 0, 0, t % .4 < .3 ? flags[(int)(t / .4) % flags.Length] : (ushort)0, packet),
                    _ => (0, 0, 0, 0, 0, 0, 0, packet),
                };
            };
            await talk.Ask("can you read my controller?"); await Task.Delay(600); await Shot("13-access");
            talk.ReviewShow("Checkup"); await Task.Delay(800); await Shot("13-checkup-menu");
            await talk.Ask("test my sticks"); await Task.Delay(2000); await Shot("13-check-rest");
            await Task.Delay(3200); await Shot("13-check-left");
            await Task.Delay(2400); await Shot("13-check-right");
            await Task.Delay(2600); await Shot("13-check-triggers");
            await Task.Delay(2600); await Shot("13-check-buttons");
            await Task.Delay(5000); await Shot("13-check-done");
            talk.ReviewShow("Chat"); await Task.Delay(400); await Shot("13-check-in-chat");
            AgentPanel.CheckInput = null;
            // every model: the same controller, opened with each one's name
            foreach (var model in new[] { "x20", "x05_pro", "x10", "d10", "x15", "dune" })
            {
                studio.CloseAgent(); host.ReturnToZone(); await Task.Delay(500);
                host.Zone.AssignController(0, host.Catalog.Get(model)); host.EnterStudio(0); await Task.Delay(1200);
                studio = host.Studio!; await ReviewVibration(studio, model);
                studio.ShowButtons(); studio.OpenAgent(); await Shot("6-model-" + model);
            }
            // every colour each model is sold in: picked in the Zone while the agent is open, the handheld changes with it
            foreach (var model in new[] { "x20_pro", "x20", "x05_pro", "x10", "d10", "x15", "dune" })
            {
                studio.CloseAgent(); host.ReturnToZone(); await Task.Delay(400);
                host.Zone.AssignController(0, host.Catalog.Get(model)); host.EnterStudio(0); await Task.Delay(1000);
                studio = host.Studio!; studio.OpenAgent(); await Task.Delay(300);
                foreach (var finish in PadColours.Catalog.For(model))
                {
                    PadColours.Set(0, model, finish.Id); await Task.Delay(250); await Shot($"6c-{model}-{finish.Id}");
                }
            }
            studio.CloseAgent();
            File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: offline Agent, seven controller motor layouts, and PC Mode battery visibility. No controller writes.");
        }
        finally { AgentPanel.LightsDevice = null; AgentPanel.SettingsProbe = null; AgentPanel.CheckInput = null; AgentRumble.ReviewPulse = null; ReviewSandbox.Active = wasSandbox; }

        void Layout()
        {
            stage.Measure(new Size(1600, 900));
            stage.Arrange(new Rect(0, 0, 1600, 900));
            stage.UpdateLayout();
        }

        void ReviewBattery(StudioScene studio, ProductHost product)
        {
            studio.SetGameplayConnected(true);
            product.UpdateBatteryLevels(["medium", null, null, null]);
            var header = Find<HeaderBattery>(studio) ?? throw new InvalidOperationException("Battery header is missing.");
            if (header.Visibility != Visibility.Visible) throw new InvalidOperationException("Battery is hidden while PC Mode is off.");
            product.PcMode!.Enabled = true;
            if (header.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Battery is visible while PC Mode is on.");
            product.UpdateBatteryLevels(["low", null, null, null]);
            if (header.Visibility != Visibility.Collapsed) throw new InvalidOperationException("A battery update revealed the hidden header.");
            product.PcMode.Enabled = false;
            if (header.Visibility != Visibility.Visible || !System.Windows.Automation.AutomationProperties.GetName(header).Contains("Low", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The latest battery reading did not return when PC Mode was switched off.");
            studio.SetGameplayConnected(false);
        }

        static T? Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (Find<T>(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
            return null;
        }

        async Task ReviewVibration(StudioScene studio, string model)
        {
            studio.ShowVibration(true);
            await studio.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            Layout();
            var workbench = studio.Vibration!;
            bool hasTriggers = model is "x20_pro" or "dune";
            foreach (var zone in Enum.GetValues<VibrationWorkbench.Zone>())
            {
                var tile = workbench.ZoneButton(zone);
                var slider = workbench.ZoneSlider(zone);
                if (!hasTriggers && zone is VibrationWorkbench.Zone.LeftTrigger or VibrationWorkbench.Zone.RightTrigger)
                {
                    if (tile != null || slider != null) throw new InvalidOperationException(model + ": unexpected trigger tile.");
                    continue;
                }
                if (tile == null || !tile.IsVisible || Math.Abs(tile.ActualWidth - tile.ActualHeight) > 1 ||
                    slider?.Orientation != Orientation.Vertical || slider.ActualHeight <= 0)
                    throw new InvalidOperationException(model + ": missing square tile or vertical slider for " + zone);
                slider.Value = 45;
                if (slider.Value != 45) throw new InvalidOperationException(model + ": motor strength did not update.");
            }
            if (!workbench.Test.IsVisible || workbench.Test.IsEnabled) throw new InvalidOperationException(model + ": test all isn't sandboxed.");
            await Shot("vibration-" + model);
        }

        async Task Shot(string name)
        {
            await Task.Delay(320); Layout();
            var bmp = new RenderTargetBitmap((int)stage.ActualWidth, (int)stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
}
