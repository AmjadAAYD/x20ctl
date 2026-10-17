using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product;
using X20Ctl.Zone;

internal static class ZoneSurfaceChecks
{
    public static void Run()
    {
        var zone=new ZoneScene(true);
        void Layout(double w,double h){zone.Measure(new Size(w,h));zone.Arrange(new Rect(0,0,w,h));zone.UpdateLayout();}
        T Named<T>(FrameworkElement root,string name)where T:class=>(T)root.FindName(name);
        var directory=Path.Combine(Environment.CurrentDirectory,"artifacts","zone-surfaces-20261016");Directory.CreateDirectory(directory);
        void Save(string file,double w,double h){Layout(w,h);var b=new RenderTargetBitmap((int)w,(int)h,96,96,PixelFormats.Pbgra32);b.Render(zone);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(directory,file));png.Save(f);}
        Layout(1920,1080);
        if(Named<Image>(zone,"BrandMark").Source is not BitmapSource brand||brand.PixelWidth<1)throw new Exception("Brand artwork not resolved");
        var supportShape=Named<Button>(zone,"SupportButton");supportShape.ApplyTemplate();var supportFace=(Border)supportShape.Template.FindName("Face",supportShape);
        if(Math.Abs(supportFace.CornerRadius.TopLeft-supportFace.ActualHeight/2)>.01)throw new Exception("Support is oval instead of height-based capsule");
        if(supportFace.Background is not LinearGradientBrush supportGradient||supportGradient.GradientStops.Count<3)throw new Exception("Detailed Support gradient missing");
        var headerClock=new HeaderClock();var beforeTime=DateTime.Now.ToString("HH:mm");headerClock.Refresh();if(headerClock.Text!=beforeTime&&headerClock.Text!=DateTime.Now.ToString("HH:mm"))throw new Exception("Clock is not local HH:mm");
        var supportOutline=(CyclingSupportOutline)supportShape.Template.FindName("SupportOutline",supportShape);if(supportOutline.IsCycling)throw new Exception("Reduced-motion Zone animates the Support outline");
        if(zone.Docks.Count!=4)throw new Exception("Four player slots changed");
        foreach(var dock in zone.Docks)
        {
            if(!ReferenceEquals(Named<Border>(dock,"Card").Background,Application.Current.FindResource("Zone.ContentSurface")))throw new Exception("Cards do not consume the shared surface material");
            if(Named<System.Windows.Shapes.Path>(dock,"SilhouettePath").Effect!=null)throw new Exception("Empty placeholder still glows");
            var bounds=dock.RenderedBounds;if(bounds.Width<=0||bounds.Height<=0)throw new Exception("Dock layout invalid");
        }
        foreach(string name in new[]{"ToolsButton","SupportButton","OverviewBack","MaximizeButton"})
        {
            Style? style=Named<Button>(zone,name).Style;
            while(style?.BasedOn!=null)style=style.BasedOn;
            if(!ReferenceEquals(style,Application.Current.FindResource("ActionButton")))throw new Exception(name+" is outside the shared button family");
        }
        int tools=0,support=0,selected=-1;
        zone.SettingsRequested+=()=>tools++;zone.SupportRequested+=()=>support++;
        Named<Button>(zone,"ToolsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Named<Button>(zone,"SupportButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        zone.Docks[2].Selected+=i=>selected=i;Named<Button>(zone.Docks[2],"FocusButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        // The reduced-motion selection completes synchronously, preserving the existing focused-player workflow.
        if(tools!=1||support!=1||selected!=2)throw new Exception("Existing commands lost");
        zone.FocusPlayerAsync(null).GetAwaiter().GetResult();Layout(1920,1080);
        Save("zone-empty-1920.png",1920,1080);Save("zone-empty-1280.png",1280,760);
        foreach(var dock in zone.Docks){var art=Named<Grid>(dock,"Art");var details=Named<Canvas>(dock,"Details");if(Canvas.GetTop(art)+art.ActualHeight>Canvas.GetTop(details)+1)throw new Exception("Artwork overlaps grid-card text at compact size");}
        var plus=Named<Border>(zone.Docks[1],"PlusBadge");
        if(!plus.IsHitTestVisible || !Named<Canvas>(zone.Docks[1],"ContentCanvas").IsHitTestVisible)throw new Exception("Center plus input disabled by ancestor");
        int addRequests=0;zone.ControllerActionRequested+=(index,change)=>{if(index==1&&!change)addRequests++;};
        Named<Button>(zone.Docks[1],"AddControllerButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(addRequests!=1)throw new Exception("Explicit add action lost");
        var catalog=new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));
        zone.AssignController(0,catalog.Models.First(m=>m.Id=="x20_pro"));
        zone.AssignController(1,catalog.Models.First(m=>m.Id=="x20"));
        zone.FocusPlayerAsync(null).GetAwaiter().GetResult();
        Save("zone-assigned-fixture.png",1920,1080);
        zone.FocusPlayerAsync(0).GetAwaiter().GetResult();Layout(1920,1080);
        if(!ReferenceEquals(Named<Border>(zone.Docks[0],"Card").Background,Application.Current.FindResource("Zone.ContentSurfaceSelected")))throw new Exception("Selected surface bypasses tokens");
        if(Named<Button>(zone.Docks[0],"Primary").Visibility!=Visibility.Visible)throw new Exception("Selected-player action lost");
        Save("zone-selected-fixture.png",1920,1080);
        var heroRemove=Named<Button>(zone.Docks[0],"RemoveControllerButton");var heroEnter=Named<Button>(zone.Docks[0],"Primary");var heroRemoveAt=heroRemove.TranslatePoint(new Point(),zone.Docks[0]);var heroEnterAt=heroEnter.TranslatePoint(new Point(),zone.Docks[0]);
        if(Math.Abs(heroRemoveAt.Y-heroEnterAt.Y)>1||heroRemoveAt.X+heroRemove.ActualWidth>heroEnterAt.X-8)throw new Exception("Focused Remove not beside Enter Studio");
        var compactRemove=Named<Button>(zone.Docks[1],"RemoveControllerButton");var compactStatus=Named<Border>(zone.Docks[1],"ConnectionBadge");var compactAt=compactRemove.TranslatePoint(new Point(),zone.Docks[1]);var statusAt=compactStatus.TranslatePoint(new Point(0,compactStatus.ActualHeight),zone.Docks[1]);
        if(compactAt.Y<statusAt.Y+4||Math.Abs(compactAt.X-statusAt.X)>1)throw new Exception("Compact Remove not beneath status");

        foreach(var dock in zone.Docks)
        {
            var art=Named<Grid>(dock,"Art");var bounds=dock.RenderedBounds;
            if(Canvas.GetLeft(art)<0||Canvas.GetTop(art)<0||Canvas.GetLeft(art)+art.ActualWidth>bounds.Width+1||Canvas.GetTop(art)+art.ActualHeight>bounds.Height+1)throw new Exception("Controller art escapes final dock bounds");
        }
        string fixture=Path.Combine(Path.GetTempPath(),"x20ctl-removal-"+Guid.NewGuid().ToString("N"),"assignments.json");
        var host=new ProductHost(zone,fixture);
        var remove=Named<Button>(zone.Docks[0],"RemoveControllerButton");if(remove.Visibility!=Visibility.Visible)throw new Exception("Assigned controller has no removal action");
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(zone.State.Players[0].ModelId!=null || zone.State.FocusedPlayer!=null || zone.State.Players[1].ModelId!="x20" || zone.Docks.Count!=4)throw new Exception("Removal changed other players");
        var restored=new AssignmentStore(fixture).Load();if(restored[0]!=null || restored[1]!="x20")throw new Exception("Removal not persisted");
        if(remove.Visibility!=Visibility.Collapsed)throw new Exception("Empty dock still offers removal");
        Save("zone-after-remove.png",1920,1080);
        Console.WriteLine("PASS: X action removes assigned slot, collapses focused player, preserves other slots, persists removal and hides on empty cards. Fixture store only.");
        var picker=new ModelSelectorView(catalog,1);string? chosen=null;picker.Chosen+=model=>chosen=model.Id;
        foreach(var model in catalog.Models){picker.ChooseModel(model.Id);if(chosen!=model.Id)throw new Exception("Model picker route failed");}
        foreach(var size in new[]{new Size(1920,1080),new Size(1280,760)}){picker.Measure(size);picker.Arrange(new Rect(size));picker.UpdateLayout();var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(picker);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,$"model-picker-{size.Width}.png"));png.Save(file);}
        Console.WriteLine("PASS: explicit Add controller route and every model-picker choice; picker rendered at 1920 and 1280 widths.");
        var animated=new ZoneBackdrop(false,true);var still=new ZoneBackdrop(true,true);
        byte[] BackdropPixels(ZoneBackdrop layer,double time){layer.Measure(new Size(640,400));layer.Arrange(new Rect(0,0,640,400));layer.SetTime(time);layer.UpdateLayout();var b=new RenderTargetBitmap(640,400,96,96,PixelFormats.Pbgra32);b.Render(layer);var p=new byte[640*400*4];b.CopyPixels(p,640*4,0);return p;}
        if(BackdropPixels(animated,0).SequenceEqual(BackdropPixels(animated,20)))throw new Exception("Ambient backdrop is static");
        if(!BackdropPixels(still,0).SequenceEqual(BackdropPixels(still,20)))throw new Exception("Reduced motion background moves");
        double Luma(Color c){double Linear(byte v){double x=v/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
        foreach(string token in new[]{"Zone.ContentSurface","Zone.ContentSurfaceHover","Zone.ContentSurfaceSelected"})
        {
            var surface=(LinearGradientBrush)Application.Current.FindResource(token);
            foreach(var stop in surface.GradientStops)foreach(string text in new[]{"Zone.TextPrimary","Zone.TextSecondary","Zone.TextMuted"})
            {var ink=(SolidColorBrush)Application.Current.FindResource(text);if((Luma(ink.Color)+.05)/(Luma(stop.Color)+.05)<4.5)throw new Exception("Insufficient token text contrast: "+text);}
        }
        Console.WriteLine("PASS: four persistent slots, shared card/button materials, quiet placeholders, Tools/Support/select routes, assigned artwork and focused-player actions. Assigned models are fixtures, not connected hardware.");
        Console.WriteLine("PASS: final artwork bounds; animated/reduced backdrop; card text token contrast >=4.5:1.");
    }
}
