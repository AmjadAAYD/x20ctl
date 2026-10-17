using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product;

/// <summary>Local artwork overrides are keyed by launcher identity, independent of display name.</summary>
internal static class GameArtwork
{
    private static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "game-art", "custom");
    private static string FileFor(GameEntry game) => Path.Combine(Root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(game.Store + "\n" + game.Id + "\n" + game.LaunchUri))) + ".png");
    public static string? PathFor(GameEntry game) => File.Exists(FileFor(game)) ? FileFor(game) : game.Cover;
    public static void Save(GameEntry game, string source)
    {
        // Decode before touching the existing override. Canonical PNG avoids extension/codec ambiguity.
        using var input = File.OpenRead(source);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth > 8192 || frame.PixelHeight > 8192) throw new ArgumentException("Image too large");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
        Directory.CreateDirectory(Root); string target = FileFor(game), temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = File.Create(temp)) encoder.Save(output); File.Move(temp, target, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

/// <summary>Front by default; the owner can switch sides manually. No timed turn or idle sway.</summary>
internal sealed class DashboardControllerPreview : Grid
{
    private readonly Grid body = new();
    private readonly string model;
    private readonly PhotoController front;
    private PhotoController? rear;
    public bool IsRear {get;private set;}
    public void Flip()
    {
        IsRear=!IsRear;body.Children.Clear();
        body.Children.Add(IsRear ? rear??=new PhotoController(model,true) : front);
    }
    public DashboardControllerPreview(string model, bool animate)
    {
        Width=1536;Height=1024;
        this.model=model;front=new PhotoController(model);body.Children.Add(front);Children.Add(body);
    }
    public void Start() { }
    public void Stop() { }
}