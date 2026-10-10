using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

internal static class ConsoleReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();var shots=new List<object>();var windows=new List<object>();
        var bindings=new StringWriter();using var listener=new TextWriterTraceListener(bindings);PresentationTraceSources.DataBindingSource.Listeners.Add(listener);PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        void Check(bool pass,string name){checks.Add(new{name,passed=pass});if(!pass)throw new InvalidOperationException(name);}
        var window=new ProductWindow(false,Path.Combine(directory,"assignments-fixture.json"));
        try
        {
            window.Show();await Ready();Check(window.WindowState==WindowState.Maximized,"fresh normal constructor starts maximized");var startup=JsonSerializer.SerializeToElement(window.NativeSnapshot());
            var actual=startup.GetProperty("window");var work=startup.GetProperty("work");Check(new[]{"Left","Top","Width","Height"}.All(k=>Math.Abs(actual.GetProperty(k).GetInt32()-work.GetProperty(k).GetInt32())<=1),"native maximized bounds match startup monitor work area");windows.Add(window.NativeSnapshot());
            window.Scene.AssignController(0,window.Host.Catalog.Get("x20"));await window.Scene.FocusPlayerAsync(null);
            foreach(string mode in new[]{"zone","buttons","curves","macros","vibration"}){Show(mode);await Capture("maximized-"+mode+".png");if(mode!="zone")Check(window.Host.Studio!.Apply.ActualWidth>=155 && !window.Host.Studio.Apply.IsEnabled,mode+" maximized footer retains disabled Apply");}
            var studio=window.Host.Studio!;studio.ShowMacros(true);var macro=studio.Macros!;macro.SelectSlot("M4");macro.Draft.Clear("M4");for(int i=0;i<47;i++)macro.Draft.Add("M4",0);macro.Refresh();studio.ShowMacros(false);studio.ShowMacros(true);
            foreach(int index in new[]{0,23,46}){macro.Select(index);await Capture($"maximized-macros-47-selected-{index+1}.png");Check(macro.SelectedCardInView && macro.EventButtons.All(b=>b.ActualWidth>=189.5),"47-event shelf retains full selected card "+(index+1));}
            macro.SetZoom(2);macro.Select(46);await Capture("maximized-macros-detail-zoom.png");
            studio.ShowVibration(true);var vibration=studio.Vibration!;
            foreach(int percent in new[]{0,25,50,75,100,63}){vibration.Set(percent);await Capture($"maximized-vibration-{percent}.png");Check(!vibration.Test.IsEnabled && !studio.Apply.IsEnabled,"visual strength "+percent+" never gains hardware authority");if(percent==0)Check(!vibration.Visualizer.IsRunning && vibration.Visualizer.Phase==0,"zero stops all visual vibration clocks");}
            SendKey(Key.F6);vibration.Set(75);vibration.PresetButtons.First().Focus();await Capture("maximized-vibration-focused-off-selected-75.png");Check(vibration.Draft.Strength==75 && Keyboard.FocusedElement==vibration.PresetButtons.First(),"focus Off remains distinct from selected 75 percent");
            window.WindowState=WindowState.Normal;window.Width=1220;window.Height=800;await Ready();Check(window.WindowState==WindowState.Normal,"manual restore is retained during the session");
            foreach(string mode in new[]{"zone","buttons","curves","macros","vibration"}){Show(mode);await Capture("restored-"+mode+".png");}
            window.Width=1040;window.Height=700;await Ready();foreach(string mode in new[]{"zone","buttons","curves","macros","vibration"}){Show(mode);await Capture("minimum-"+mode+".png");if(mode!="zone"){var scene=window.Host.Studio!;var apply=scene.Apply.TransformToAncestor(scene).TransformBounds(new Rect(scene.Apply.RenderSize));Check(apply.Right<=scene.ActualWidth+1 && scene.Apply.ActualWidth>=155,mode+" minimum Apply stays in bounds");}}
            var minStudio=window.Host.Studio!;minStudio.ShowMacros(true);var minMacro=minStudio.Macros!;minMacro.SelectSlot("M4");foreach(int index in new[]{0,46}){minMacro.Select(index);await Capture($"minimum-macros-47-selected-{index+1}.png");Check(minMacro.SelectedCardInView,"minimum first/last macro card fully visible "+index);}
            window.WindowState=WindowState.Maximized;Show("vibration");studio=window.Host.Studio!;vibration=studio.Vibration!;var frames=new List<BitmapFrame>();
            foreach(int percent in new[]{0,25,50,75,100,0}){vibration.Set(percent);for(int i=0;i<12;i++){await Task.Delay(100);window.UpdateLayout();frames.Add(BitmapFrame.Create(Render(window.Host,.7)));}}
            var gif=new GifBitmapEncoder();foreach(var frame in frames)gif.Frames.Add(frame);string motionFile=Path.Combine(directory,"vibration-visual-0-to-100-to-0.gif");using(var stream=File.Create(motionFile))gif.Save(stream);StudioReview.SetGifDelays(motionFile,10);Check(!vibration.Visualizer.IsRunning,"return to zero removes continuous visual motion");
            var reducedWindow=new ProductWindow(true,Path.Combine(directory,"reduced-fixture.json"));try{reducedWindow.Show();await Task.Delay(240);reducedWindow.Scene.AssignController(0,reducedWindow.Host.Catalog.Get("x20"));reducedWindow.Host.EnterStudio(0);reducedWindow.Host.Studio!.ShowVibration(true);reducedWindow.Host.Studio.Vibration!.Set(100);await Task.Delay(240);reducedWindow.UpdateLayout();Save(reducedWindow.Host,"maximized-vibration-reduced-motion-100.png",1);Check(!reducedWindow.Host.Studio.Vibration.Visualizer.IsRunning && reducedWindow.Host.Studio.Vibration.Visualizer.ReducedMotion,"reduced motion retains static strength contour only");}finally{reducedWindow.Close();}
            // Representative render-DPI matrix: no display settings or monitor identity are fabricated.
            foreach(var (pixelsW,pixelsH,scale) in new[]{(1920,1040,1d),(1920,1040,1.25),(1920,1040,1.5),(2560,1400,1.25),(3840,2120,1.5)})
            foreach(string mode in new[]{"zone","buttons","curves","macros","vibration"})
            {
                double width=pixelsW/scale,height=pixelsH/scale;FrameworkElement root;
                if(mode=="zone")root=new ZoneScene(true){Width=width,Height=height};
                else {var scene=new StudioScene(0,window.Host.Catalog.Get("x20"),new ButtonDraft("x20"),true){Width=width,Height=height};root=scene;if(mode=="curves")scene.ShowCurves(true);else if(mode=="macros"){scene.ShowMacros(true);scene.Macros!.Add();}else if(mode=="vibration"){scene.ShowVibration(true);scene.Vibration!.Set(75);}else scene.SelectControl("A");}
                VisualTreeHelper.SetRootDpi(root,new DpiScale(scale,scale));root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();Save(root,$"render-dpi-{scale*100:0}-{pixelsW}x{pixelsH}-{mode}.png",scale);Check(root.ActualWidth>=width-.5,"DPI render consumes representative full width "+mode+"/"+scale);
            }
            listener.Flush();File.WriteAllText(Path.Combine(directory,"bindings.txt"),bindings.ToString());Check(bindings.ToString().Length==0,"no WPF binding errors across shared responsive views");
            File.WriteAllText(Path.Combine(directory,"review.json"),JsonSerializer.Serialize(new{status="passed",hardwareAccess=false,desktopInputInjected=false,systemDpiChanged=false,actualWindows=windows,availableMonitorWorkAreas=ProductWindow.AvailableWorkAreas(),actualMonitorDpiTransitions=window.DpiTransitionCount,dpiMatrixKind="offscreen WPF root-DPI renders at representative work-area resolutions, not physical monitor scaling transitions",captureKind="shown own-window client renders and labelled detached DPI matrix",visualPreviewOnly=true,checks,shots,motionFrames=frames.Count},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{window.Close();PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);}
        void Show(string mode){if(mode=="zone"){window.Host.ReturnToZone();return;}if(window.Host.Studio==null)window.Host.EnterStudio(0);var scene=window.Host.Studio!;if(mode=="vibration")scene.ShowVibration(true);else if(mode=="macros")scene.ShowMacros(true);else if(mode=="curves")scene.ShowCurves(true);else {scene.ShowCurves(false);scene.SelectControl("A");}}
        void SendKey(Key key){window.Host.Studio!.OnKeyDown(window,new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
        async Task Ready(){await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(300);window.UpdateLayout();}
        async Task Capture(string file){await Ready();Save(window.Host,file,1);}
        void Save(FrameworkElement root,string file,double scale){var image=Render(root,scale);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(directory,file)))encoder.Save(stream);shots.Add(new{file,width=image.PixelWidth,height=image.PixelHeight,rootDpi=VisualTreeHelper.GetDpi(root).PixelsPerDip});}
        static RenderTargetBitmap Render(FrameworkElement root,double scale){var image=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*scale),(int)Math.Ceiling(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(root);return image;}
    }
}
