using System.IO;
using System.Windows;
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
            foreach (var name in new[] { "pc-mode-fixture.json", "agent-setups.json" })
                foreach (var file in new[] { name, name + ".bak" }) File.Delete(Path.Combine(directory, file));
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(500);
            window.Scene.AssignController(0, window.Host.Catalog.Get("x20_pro")); window.Host.EnterStudio(0); await Task.Delay(1400);
            var studio = window.Host.Studio!; studio.ShowButtons();
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
            // every model: the same controller, opened with each one's name
            foreach (var model in new[] { "x20", "x05", "x05_pro", "x10", "d10", "x15", "dune" })
            {
                studio.CloseAgent(); window.Host.ReturnToZone(); await Task.Delay(500);
                window.Scene.AssignController(0, window.Host.Catalog.Get(model)); window.Host.EnterStudio(0); await Task.Delay(1200);
                studio = window.Host.Studio!; studio.OpenAgent(); await Shot("6-model-" + model);
            }
        }
        finally { window.Close(); }

        async Task Shot(string name)
        {
            await Task.Delay(900); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
}
