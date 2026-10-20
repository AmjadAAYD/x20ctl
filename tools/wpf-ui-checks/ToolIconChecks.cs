using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product;

static class ToolIconChecks
{
    public static void Run()
    {
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var method = typeof(ProductSettings).GetMethod("Card", BindingFlags.Static | BindingFlags.NonPublic)!;
        var output = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "tool-icon-motion"); System.IO.Directory.CreateDirectory(output);
        var sheet = new Grid { Background = new SolidColorBrush(Color.FromRgb(12, 21, 42)) };
        var preview = new Grid { Background = new SolidColorBrush(Color.FromRgb(12, 21, 42)) };
        preview.RowDefinitions.Add(new RowDefinition()); preview.RowDefinitions.Add(new RowDefinition());
        foreach (int _ in Enumerable.Range(0, 4)) preview.ColumnDefinitions.Add(new ColumnDefinition());
        var previewIcons = new List<FrameworkElement>();
        foreach (int _ in Enumerable.Range(0, 8)) sheet.RowDefinitions.Add(new RowDefinition());
        foreach (int _ in Enumerable.Range(0, 6)) sheet.ColumnDefinitions.Add(new ColumnDefinition());
        int row = 0;
        foreach (string title in new[] { "Controller not working?", "Send feedback", "Replay Intro", "Write scan", "PC mode", "Support X20CTL", "Star on GitHub", "About" })
        {
            var button = (Button)method.Invoke(null, new object[] { "", title, "fixture", Colors.CornflowerBlue, false })!;
            var art = (Grid)((Border)button.Content).Child;
            var icon = art.Children.OfType<Border>().Select(b => b.Child).OfType<FrameworkElement>().SingleOrDefault(c => c.GetType().Name == "ToolTileIcon");
            if (icon == null) throw new Exception(title + " has no animated icon");
            var type = icon.GetType();
            if (row == 0)
            {
                var host = new Window { Width = 80, Height = 80, Opacity = 0, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
                // Temporarily detach from the chip while checking a real presentation lifecycle.
                ((Border)art.Children.OfType<Border>().First(b => ReferenceEquals(b.Child, icon))).Child = null;
                host.Content = icon;
                host.Show(); host.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                type.GetMethod("SetActive")!.Invoke(icon, new object[] { true });
                if ((bool)type.GetProperty("IsAnimating")!.GetValue(icon)! != SystemParameters.ClientAreaAnimation) throw new Exception("Motion preference not respected");
                host.Hide();
                if ((bool)type.GetProperty("IsAnimating")!.GetValue(icon)!) throw new Exception("Hidden icon keeps rendering");
                host.Close();
            }
            type.GetMethod("SetActive")!.Invoke(icon, new object[] { true });
            type.GetMethod("SetActive")!.Invoke(icon, new object[] { false });
            if ((bool)type.GetProperty("IsAnimating")!.GetValue(icon)!) throw new Exception("Icon clock survives deactivation");
            var frames = new HashSet<string>();
            for (int frame = 0; frame < 6; frame++)
            {
                var copy = (FrameworkElement)Activator.CreateInstance(type, title)!;
                type.GetMethod("Seek")!.Invoke(copy, new object[] { frame * 1.1 });
                copy.Measure(new Size(48, 48)); copy.Arrange(new Rect(0, 0, 48, 48)); copy.UpdateLayout();
                var bitmap = new RenderTargetBitmap(96, 96, 192, 192, PixelFormats.Pbgra32); bitmap.Render(copy);
                byte[] pixels = new byte[96 * 96 * 4]; bitmap.CopyPixels(pixels, 96 * 4, 0);
                frames.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
                var cell = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = { copy, new TextBlock { Text = frame == 0 ? title : $"{frame * 1.1:0.0}s", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) } } };
                Grid.SetRow(cell, row); Grid.SetColumn(cell, frame); sheet.Children.Add(cell);
            }
            if (frames.Count < 3) throw new Exception(title + " does not have distinct motion phases");
            var animated = (FrameworkElement)Activator.CreateInstance(type, title)!;
            previewIcons.Add(animated);
            var tile = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Children = { new Border { Width = 58, Height = 58, CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromRgb(37, 64, 104)), Child = animated },
                    new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 12, 0, 0) } } };
            Grid.SetRow(tile, row / 4); Grid.SetColumn(tile, row % 4); preview.Children.Add(tile);
            row++;
        }
        sheet.Measure(new Size(1080, 880)); sheet.Arrange(new Rect(0, 0, 1080, 880)); sheet.UpdateLayout();
        var capture = new RenderTargetBitmap(1080, 880, 96, 96, PixelFormats.Pbgra32); capture.Render(sheet);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(capture));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(output, "motion-phases.png"))) png.Save(file);
        var framesPath = System.IO.Path.Combine(output, "frames"); System.IO.Directory.CreateDirectory(framesPath);
        preview.Measure(new Size(880, 320)); preview.Arrange(new Rect(0, 0, 880, 320)); preview.UpdateLayout();
        for (int frame = 0; frame < 132; frame++)
        {
            foreach (var item in previewIcons) item.GetType().GetMethod("Seek")!.Invoke(item, new object[] { frame / 20.0 });
            preview.UpdateLayout();
            var image = new RenderTargetBitmap(880, 320, 96, 96, PixelFormats.Pbgra32); image.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = System.IO.File.Create(System.IO.Path.Combine(framesPath, $"{frame:D3}.png")); encoder.Save(file);
        }
        Console.WriteLine("PASS: all eight Tools cards use animated icons; each has distinct phases; deactivation stops the clock; phase sheet rendered.");
    }
}
