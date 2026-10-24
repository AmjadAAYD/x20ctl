using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product;
using X20Ctl.Zone;

static class DockMotionChecks
{
    public static async Task Run()
    {
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(Application.Current.Dispatcher));
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        T Named<T>(DockView d, string name) where T : FrameworkElement => (T)d.FindName(name);
        var zone = new ZoneScene(false);
        var window = new Window { Content = zone, Width = 1600, Height = 1000, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
        window.Show(); await Task.Delay(50); window.UpdateLayout();
        try
        {
            var dock = zone.Docks[0];
            dock.FadeHeroDetails(0, 0);
            Check(Named<Canvas>(dock, "Details").Opacity == 0 && Named<Button>(dock, "AddControllerButton").Opacity == 0,
                "Transition fade ignores its requested value or leaves Add controller visible");
            dock.FadeHeroDetails(1, 0);
            var stage = (Canvas)zone.FindName("DockStage");
            var overview = ZoneLayout.Calculate(stage.ActualWidth, stage.ActualHeight, null)[0];
            var hero = ZoneLayout.Calculate(stage.ActualWidth, stage.ActualHeight, 0)[0];
            var prepare = typeof(DockView).GetMethod("PrepareMove")!;
            var progress = typeof(DockView).GetMethod("SetMoveProgress")!;
            Check(prepare != null && progress != null, "Dock artwork has no shared transition progress");
            prepare!.Invoke(dock, new object[] { hero, true, false });
            double lastY = double.NaN; int direction = 0;
            for (int frame = 0; frame <= 30; frame++)
            {
                double p = frame / 30.0;
                dock.SetBounds(new DockBounds(overview.X + (hero.X - overview.X) * p, overview.Y + (hero.Y - overview.Y) * p,
                    overview.Width + (hero.Width - overview.Width) * p, overview.Height + (hero.Height - overview.Height) * p), true, false);
                progress!.Invoke(dock, new object[] { p });
                dock.UpdateLayout();
                var art = Named<Grid>(dock, "Art");
                Check(Canvas.GetTop(art) >= 0 && Canvas.GetLeft(art) >= 0 && Canvas.GetLeft(art) + art.Width <= dock.Width + 1 && Canvas.GetTop(art) + art.Height <= dock.Height + 1,
                    "Artwork escapes its card during the transition");
                double y = Named<Button>(dock, "AddControllerButton").Margin.Top;
                if (!double.IsNaN(lastY) && Math.Abs(y - lastY) > .1)
                {
                    int next = Math.Sign(y - lastY);
                    Check(direction == 0 || next == direction, "Add controller reverses direction during a single move"); direction = next;
                    Check(Math.Abs(y - lastY) < 30, "Add controller jumps between frames");
                }
                lastY = y;
            }
            dock.SetMoving(false);
            // Return to the real layout, then interrupt a move twice as repeated clicks would.
            zone.LoadScenario(ZoneScenario.Empty); window.UpdateLayout();
            var first = zone.FocusPlayerAsync(0); await Task.Delay(190);
            var second = zone.FocusPlayerAsync(1); await Task.Delay(45);
            var third = zone.FocusPlayerAsync(2);
            await Task.WhenAll(first, second, third); await Task.Delay(70); window.UpdateLayout();
            Check(zone.State.FocusedPlayer == 2 && !zone.IsTransitioning, "Rapid clicks leave an unfinished or incorrect selection");
            var expected = ZoneLayout.Calculate(stage.ActualWidth, stage.ActualHeight, 2);
            for (int i = 0; i < 4; i++)
            {
                var b = zone.Docks[i].RenderedBounds; var target = expected[i];
                Check(Math.Abs(b.X - target.X) + Math.Abs(b.Y - target.Y) + Math.Abs(b.Width - target.Width) + Math.Abs(b.Height - target.Height) < 1,
                    "Interrupted cards do not land at the final selected layout");
                Check(Named<Button>(zone.Docks[i], "AddControllerButton").Opacity == 1, "Interrupted move leaves actions faded out");
            }
            await zone.FocusPlayerAsync(null);
            var swap = zone.FocusPlayerAsync(1); await Task.Delay(190); window.Width = 1250; window.Height = 820;
            await swap; await Task.Delay(60); window.UpdateLayout();
            Check(!zone.IsTransitioning && zone.Docks.All(d => Named<Button>(d, "AddControllerButton").Opacity == 1), "Resize leaves a half-finished transition");
            var output = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "dock-motion-fix"); System.IO.Directory.CreateDirectory(output);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)zone.ActualWidth, (int)zone.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(zone);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "settled.png"))) png.Save(file);
            Console.WriteLine("PASS: action fades, synchronized artwork/button trajectory, in-card motion, rapid interrupted selection, resize recovery and rendered final layout.");
        }
        finally { window.Close(); }
    }
}
