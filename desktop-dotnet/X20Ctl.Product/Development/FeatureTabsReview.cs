using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace X20Ctl.Product.Development;

internal static class FeatureTabsReview
{
    public static async Task Run(string directory,string theme)
    {
        ReviewSandbox.Active=true;ReviewSandbox.HideLibrary=true;IntroView.SoundEnabled=false;
        ThemeCatalog.Apply(ThemeCatalog.Find(theme));Directory.CreateDirectory(directory);
        var host=new ProductHost(new ZoneScene(true),Path.Combine(directory,"assignments.json"));
        var stage=new DesignStage {Child=host};
        using var source=new HwndSource(new HwndSourceParameters("Feature tabs offscreen review"){WindowStyle=unchecked((int)0x80000000),Width=1600,Height=900,PositionX=-32000,PositionY=-32000}){RootVisual=stage};
        host.Zone.AssignController(0,host.Catalog.Get("x20_pro"));host.EnterStudio(0);
        var studio=host.Studio!;
        foreach(var page in new[]{"RGB","Screen","Device","RGB"})
        {
            if(page=="Device")studio.ShowManagement(page);
            else ((RadioButton)studio.FindName(page=="RGB"?"RgbNav":"ScreenNav")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await studio.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(100);
            stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();
            if(studio.ManagementPage!=page)throw new InvalidOperationException("Wrong page: "+page);
            if(page is "RGB" or "Screen")
            {
                var root=(Grid)studio.FindName("RootLayout");
                if(!root.IsVisible)throw new InvalidOperationException(page+" tab is hidden.");
                if(page=="RGB")
                {
                    var body=(Grid)studio.FindName("ManagementBodyHost");
                    var buttons=Descendants(body).OfType<Button>().Where(b=>b.IsVisible&&Convert.ToString(b.Content)?.StartsWith("Apply")==true).ToArray();
                    if(buttons.Length!=1||Convert.ToString(buttons[0].Content)!="Apply")throw new InvalidOperationException("RGB must have exactly one Apply button.");
                }
                else if(studio.Screen?.Body.IsVisible!=true)throw new InvalidOperationException("Screen editor hidden.");
            }
            Capture(stage,Path.Combine(directory,page+".png"));
            if(page=="Device")
            {
                // opens the confirm step only; Reset controller is never pressed
                var body=(Grid)studio.FindName("ManagementBodyHost");
                var ask=Descendants(body).OfType<Button>().Single(b=>Convert.ToString(b.Content)=="Factory reset");
                ask.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await studio.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);
                stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();
                Descendants(body).OfType<ScrollViewer>().First(s=>Descendants(s).Contains(ask)).ScrollToEnd();
                await studio.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(100);
                stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();
                if(!Descendants(body).OfType<Button>().Any(b=>b.IsVisible&&Convert.ToString(b.Content)=="Reset controller"))throw new InvalidOperationException("Factory reset has no confirm step.");
                Capture(stage,Path.Combine(directory,"Device-reset.png"));
            }
        }
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: RGB and Screen navigation, exactly one RGB Apply, Device and RGB return, factory reset asks first. No controller IO.");
    }
    static void Capture(Visual stage,string path)
    {
        var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);bitmap.Render(stage);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path);png.Save(file);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
}
