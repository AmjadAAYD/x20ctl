using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;
internal static class ProductReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();var shots=new List<string>();var links=new List<string>();var bindings=new StringWriter();using var listener=new TextWriterTraceListener(bindings);PresentationTraceSources.DataBindingSource.Listeners.Add(listener);PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
        void Check(bool value,string name){checks.Add(new{name,passed=value});if(!value)throw new InvalidOperationException(name);}
        string assignments=Path.Combine(directory,"first-run","assignments.json");Directory.CreateDirectory(Path.GetDirectoryName(assignments)!);var window=new ProductWindow(false,assignments,firstRun:true,openSupport:links.Add);
        try
        {
            window.Show();await Task.Delay(40);var frames=new List<BitmapFrame>();var times=new List<double>();var recordingClock=Stopwatch.StartNew();
            for(int i=0;i<60;i++){await Task.Delay(50);window.UpdateLayout();times.Add(recordingClock.Elapsed.TotalMilliseconds);frames.Add(BitmapFrame.Create(Render(window.Host,.7)));if(i is 3 or 8 or 13)Save(window.Host,"intro-stage-"+i+".png",1);if(window.Host.Intro==null)break;}
            var gif=new GifBitmapEncoder();foreach(var frame in frames)gif.Frames.Add(frame);string gifPath=Path.Combine(directory,"first-run-intro.gif");using(var stream=File.Create(gifPath))gif.Save(stream);var delays=times.Select((time,index)=>(ushort)Math.Clamp(Math.Round(((index+1<times.Count?times[index+1]:recordingClock.Elapsed.TotalMilliseconds)-time)/10),1,65535)).ToArray();StudioReview.SetGifDelays(gifPath,5,delays);
            Check(window.Host.IntroWasPlayed && window.Host.Intro==null && window.Host.Studio==null,"first-run completes directly into Controller Zone");Check(links.Count==0,"startup never launches a browser");Save(window.Host,"intro-final-zone.png",1);
            window.Scene.SupportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(links.SequenceEqual(new[]{ProductLinks.Support}),"support mouse action uses exact preserved Ko-fi URL");
            window.Scene.SupportButton.Focus();SendKey(Key.Enter,true);Check(links.Count==2,"support keyboard activation works");window.Scene.SetSimulationNavigation(true);window.Scene.SupportButton.Focus();SendKey(Key.A,true);SendKey(Key.A,false);Check(links.Count==3 && window.Scene.State.FocusedPlayer==null,"support simulated-gamepad activation does not focus a player");
            window.Scene.ToolsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Save(window.Host,"settings-support-replay.png",1);window.Host.Settings!.Replay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.Host.Intro!=null,"Replay Intro works without deleting preferences");SendKey(Key.Escape,true);Check(window.Host.Intro==null && window.Host.Zone.Opacity==1,"Escape skips immediately into Zone");Save(window.Host,"intro-skipped-zone.png",1);
            var second=new ProductWindow(false,assignments,firstRun:true,openSupport:links.Add);try{second.Show();await Ready(second);Check(second.Host.IntroWasPlayed,"every launch opens with the intro");second.Host.Intro?.Finish();Save(second.Host,"subsequent-launch-zone.png",1);}finally{second.Close();}
            var reduced=new ProductWindow(true,Path.Combine(directory,"reduced","assignments.json"),openSupport:links.Add);try{reduced.Show();await Ready(reduced);reduced.Host.StartIntro();await Task.Delay(140);reduced.UpdateLayout();Save(reduced.Host,"intro-reduced-motion.png",1);Check(reduced.Host.Intro?.Reduced==true,"reduced-motion intro has static branded composition");await Task.Delay(300);Check(reduced.Host.Intro==null,"reduced-motion intro finishes quickly");}finally{reduced.Close();}
            window.Scene.AssignController(0,window.Host.Catalog.Get("x20"));window.Host.EnterStudio(0);var scene=window.Host.Studio!;scene.ShowManagement("Tester");var tester=scene.Tester!;tester.KeyboardMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("keyboard-01-idle.png");Check(tester.SourceLabel.Text=="KEYBOARD SIMULATION","keyboard mode is explicitly labelled");
            foreach(var scenario in new[]{("keyboard-02-w.png",new[]{Key.W}),("keyboard-03-w-d.png",new[]{Key.W,Key.D}),("keyboard-04-up.png",new[]{Key.Up}),("keyboard-05-up-right.png",new[]{Key.Up,Key.Right}),("keyboard-06-lt.png",new[]{Key.Q}),("keyboard-07-rt.png",new[]{Key.E}),("keyboard-08-both-triggers.png",new[]{Key.Q,Key.E})})
            {tester.ClearKeyboardKeys();tester.ShowRear(false);foreach(var key in scenario.Item2)SendKey(key,true);await Capture(scenario.Item1);Check(tester.KeyboardTest.Held.Count==scenario.Item2.Length && scene.ManagementPage=="Tester",scenario.Item1+" remains held without UI navigation");}
            foreach(var key in new[]{Key.W,Key.A,Key.S,Key.D,Key.Up,Key.Down,Key.Left,Key.Right,Key.Q,Key.E})SendKey(key,false);await Capture("keyboard-09-release-neutral.png");Check(tester.State.Frame is {LeftX:0,LeftY:0,RightX:0,RightY:0,LT:0,RT:0},"all releases restore neutral and zero");SendKey(Key.W,true);SendKey(Key.Q,true);window.Host.ResetTransientInput();Check(tester.KeyboardTest.Held.Count==0 && tester.State.Frame?.LT==0,"deactivation reset clears held input");
            tester.KeyboardMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("keyboard-10-real-input-waiting.png");Check(tester.State.Frame==null && !tester.KeyboardTest.Enabled,"source switch clears simulation before waiting for real input");tester.SetKeyboardSimulation(true);SendKey(Key.W,true);SendKey(Key.E,true);tester.Exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Capture("keyboard-11-exit-clean.png");Check(!tester.State.OwnsInput && tester.KeyboardTest.Held.Count==0 && tester.State.Frame==null,"Tester exit releases ownership with no stuck input");
            window.WindowState=WindowState.Normal;window.Width=1040;window.Height=700;scene.ShowManagement("Tester");tester.SetKeyboardSimulation(true);SendKey(Key.Q,true);await Capture("keyboard-minimum.png");window.Host.ReturnToZone();window.Host.StartIntro();await Task.Delay(800);window.UpdateLayout();Save(window.Host,"intro-minimum-replay.png",1);window.Host.Intro?.Finish();
            foreach(double scale in new[]{1d,1.25,1.5}){var visual=new IntroView(false){Width=1920/scale,Height=1040/scale};VisualTreeHelper.SetRootDpi(visual,new(scale,scale));visual.Measure(new(visual.Width,visual.Height));visual.Arrange(new(0,0,visual.Width,visual.Height));visual.UpdateLayout();visual.Seek(2.6);visual.UpdateLayout();Save(visual,$"intro-render-dpi-{scale*100:0}.png",scale);}
            await window.Host.InitializeNativeEngineAsync();Check(window.Host.Engine!=null && window.Host.NativeEngineStatus.Contains("running"),"WPF connects to bundled C++ engine");var locked=await window.Host.Engine!.Request("capabilities",new{model="x20"});Check(!locked.GetProperty("configurationConnected").GetBoolean(),"native configuration remains independently locked");await window.Host.StopNativeEngineAsync();
            Check(bindings.ToString().Length==0,"no WPF binding errors");File.WriteAllText(Path.Combine(directory,"bindings.txt"),bindings.ToString());File.WriteAllText(Path.Combine(directory,"review.json"),JsonSerializer.Serialize(new{status="passed",checks,shots,hardwareAccess=false,desktopInputInjected=false,browserInvocations="injected recording delegate; no external browser launched during review",supportUrl=ProductLinks.Support,introFrames=frames.Count,simulationOnly=true},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{window.Close();PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);}
        void SendKey(Key key,bool down){var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,0,key){RoutedEvent=down?Keyboard.PreviewKeyDownEvent:Keyboard.PreviewKeyUpEvent};if(down)window.Host.OnKeyDown(window,e);else window.Host.OnKeyUp(window,e);}
        async Task Capture(string file){await Ready(window);Save(window.Host,file,1);}
        static async Task Ready(Window target){await target.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(300);target.UpdateLayout();}
        void Save(FrameworkElement target,string file,double scale){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(target,scale)));using(var stream=File.Create(Path.Combine(directory,file)))encoder.Save(stream);shots.Add(file);}
        static RenderTargetBitmap Render(FrameworkElement target,double scale){var bitmap=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth*scale),(int)Math.Ceiling(target.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(target);return bitmap;}
    }
}
