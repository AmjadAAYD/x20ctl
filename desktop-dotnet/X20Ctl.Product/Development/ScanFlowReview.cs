using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>The Read scan to Write scan hand-over (--review-scanflow DIRECTORY): the Tools card with its computer picture,
/// and the Write scan's warning page with a Read scan riding along. Rendered from the window's own visual tree.</summary>
internal static class ScanFlowReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>(); ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(600); window.Host.Intro?.Finish(); await Task.Delay(500);
            window.Host.ShowSettings(); log.Add("check after ShowSettings: " + (window.Host.Check != null)); await Shot("1-tools-a", 300); log.Add("check after 300ms: " + (window.Host.Check != null)); await Shot("1-tools-b", 900); log.Add("check after 1.2s: " + (window.Host.Check != null));
            // a Read scan's compact report, as the Read scan itself would hand it on
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                foreach (var name in new[] { "device.json", "input-captures.json", "input-mapping.json", "system.json", "scanner-version.txt" })
                    using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write(name.EndsWith(".txt") ? "Controller Scanner" : "{}");
            var read = new Scanner.ReadReport(stream.ToArray(), "EasySMX X20", "Bluetooth", null, DateTime.UtcNow);
            var files = read.Files(4 * 1024 * 1024);
            File.WriteAllText(Path.Combine(directory, "files.txt"), string.Join("\n", files.Select(f => f.Path + " " + f.Data.Length)));
            window.Host.CloseSettings(); await Task.Delay(400);
            window.Host.OpenWriteScan(read); await Shot("2-write-warning-with-read", 900);
        }
        finally { File.WriteAllLines(Path.Combine(directory, "log.txt"), log); window.Close(); }

        async Task Shot(string name, int delay)
        {
            await Task.Delay(delay); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
}
