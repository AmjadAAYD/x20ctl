using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;
internal static class UpdateStatesReview
{
    public static void Run(string directory)
    {
        ReviewSandbox.Active = true;
        Directory.CreateDirectory(directory);
        foreach (var (state, text) in new[] { ("ready", UpdateKit.Label("V0.099", false)), ("downloading", UpdateKit.Label("V0.099", true, .42)), ("failed", UpdateKit.Label("V0.099", false, failed:true)), ("restarting", "Restarting…") })
        {
            var label = Kit.Text(text,14,"DS.Text",FontWeights.SemiBold);
            label.TextTrimming=TextTrimming.CharacterEllipsis; label.Foreground=DS.Brush("Zone.TextSecondary");
            var button = ProductHost.CreateUpdateNoticeButton(label,null); button.IsEnabled=state!="restarting";
            var root = new Border { Width=560, Height=100, Padding=new Thickness(20), Background=DS.Brush("Ink"), Child=button };
            root.Measure(new Size(560,100)); root.Arrange(new Rect(0,0,560,100)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(560,100,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory,state+".png")); png.Save(file);
        }
        File.WriteAllText(Path.Combine(directory,"review.txt"),"Shared production button factory; synthetic version/progress; no window, downloads, installation or restart.");
    }
}
