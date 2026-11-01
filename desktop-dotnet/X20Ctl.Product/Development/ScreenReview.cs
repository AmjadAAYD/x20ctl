using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;
internal static class ScreenReview
{
 public static async Task Run(string directory,string inputs,string? theme=null)
 {
  ReviewSandbox.Active=true;ReviewSandbox.HideLibrary=true;if(theme!=null)ThemeCatalog.Apply(ThemeCatalog.Find(theme));Directory.CreateDirectory(directory);IntroView.SoundEnabled=false;
  var host=new ProductHost(new ZoneScene(true),Path.Combine(directory,"assignments.json"));var stage=new DesignStage{Child=host};
  using var source=new HwndSource(new HwndSourceParameters("Screen offscreen review"){WindowStyle=unchecked((int)0x80000000),Width=1600,Height=900,PositionX=-32000,PositionY=-32000}){RootVisual=stage};
  host.Zone.AssignController(0,host.Catalog.Get("x20_pro"));host.EnterStudio(0);host.Studio!.ShowManagement("Device");await Shot("device-entry");host.Studio.ShowManagement("Screen");var screen=host.Studio.Screen!;await Shot("screen-empty");if(!screen.Body.IsVisible)throw new InvalidOperationException("Screen editor is hidden by the theme page layer.");
  if(screen.Upload.IsEnabled||screen.Reset.IsEnabled)throw new InvalidOperationException("Physical writes must remain disabled.");
  screen.Load(Path.Combine(inputs,"orientation.png"));await screen.BuildPreviewAsync();await Shot("screen-wallpaper");
  var still=screen.EncodedAsset!;if(still.Length!=25612)throw new InvalidOperationException("Unexpected wallpaper size.");
  screen.Load(Path.Combine(inputs,"uniform.gif"));await screen.BuildPreviewAsync();await Shot("screen-gif-static-default");
  // Exercise the same visible animation checkbox, not an alternative import path.
  var animation=Walk(screen.Body).OfType<CheckBox>().Single();animation.IsChecked=true;await screen.BuildPreviewAsync();await Shot("screen-animation");
  var animated=screen.EncodedAsset!;
  foreach(string name in new[]{"unequal","transparent-disposal"}){screen.Load(Path.Combine(inputs,name+".gif"));animation.IsChecked=true;await screen.BuildPreviewAsync();File.WriteAllBytes(Path.Combine(directory,name+"-wpf.bin"),screen.EncodedAsset!);}
  using(var locked=new ScreenSerialTransport("COM1"))
  {
   bool rejected=false;try{await locked.ExchangeAsync(new byte[64],CancellationToken.None);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new InvalidOperationException("Locked serial adapter must reject before opening.");
  }
  var slow=new SlowEmulator();screen.UseReviewTransport(slow);screen.Upload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(200);await Shot("screen-upload-emulator");screen.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  for(int i=0;i<50&&screen.IsBusy;i++)await Task.Delay(50);if(screen.IsBusy||!screen.StatusText.Contains("cancelled"))throw new InvalidOperationException("UI cancellation did not settle.");await Shot("screen-cancelled-emulator");
  screen.Upload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(int i=0;i<100&&screen.IsBusy;i++)await Task.Delay(50);if(screen.IsBusy||!screen.StatusText.Contains("completed"))throw new InvalidOperationException("UI upload did not complete.");await Shot("screen-completed-emulator");
  var transport=new Zone.Screen.ScreenEmulator();await new Zone.Screen.ScreenSender(transport).UploadAsync(animated,true,100,null,CancellationToken.None);
  host.Studio.ShowManagement("Device");host.Studio.ShowManagement("Screen");await Shot("screen-return");
  File.WriteAllBytes(Path.Combine(directory,"uniform-wpf.bin"),animated);
  File.WriteAllText(Path.Combine(directory,"verification.json"),JsonSerializer.Serialize(new{offscreen=true,physicalWritesEnabled=false,staticDefault=true,stillBytes=still.Length,animationBytes=animated.Length,emulatorRequests=transport.Requests.Count,navigationReturned=true},new JsonSerializerOptions{WriteIndented=true}));
  async Task Shot(string name){await host.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(500);stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);bitmap.Render(stage);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,name+".png"));png.Save(file);}
 }
 sealed class SlowEmulator:Zone.Screen.IScreenTransport
 {
  readonly Zone.Screen.ScreenEmulator emulator=new();public bool IsEmulated=>true;
  public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token){await Task.Delay(120,token);return await emulator.ExchangeAsync(request,token);}
 }
 static IEnumerable<DependencyObject> Walk(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
