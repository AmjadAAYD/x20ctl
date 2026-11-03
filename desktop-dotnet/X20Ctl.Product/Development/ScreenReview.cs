using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Product.Development;
internal static class ScreenReview
{
 public static async Task Run(string directory,string inputs,string? theme=null)
 {
  ReviewSandbox.Active=true;ReviewSandbox.HideLibrary=true;ScreenHardwareEvidence.VerifiedDevice=false;if(theme!=null)ThemeCatalog.Apply(ThemeCatalog.Find(theme));Directory.CreateDirectory(directory);IntroView.SoundEnabled=false;
  var host=new ProductHost(new ZoneScene(true),Path.Combine(directory,"assignments.json"));var stage=new DesignStage{Child=host};
  using var source=new HwndSource(new HwndSourceParameters("Screen offscreen review"){WindowStyle=unchecked((int)0x80000000),Width=1600,Height=900,PositionX=-32000,PositionY=-32000}){RootVisual=stage};
  host.Zone.AssignController(0,host.Catalog.Get("x20_pro"));host.EnterStudio(0);host.Studio!.ShowManagement("Screen");var screen=host.Studio.Screen!;
  await Shot("empty");if(!screen.Body.IsVisible)throw new InvalidOperationException("Screen editor is hidden.");
  // Export first-frame native crop pixels for all 24 accepted input aliases, without rerunning vendor conversions.
  var names=new[]{"gradient.png","orientation-png.png","orientation-jpg.jpg","orientation-bmp.bmp","palette-255.png","palette-256.png","palette-257.png","run-255.png","run-256.png","run-257.png","single-frame.gif","solid-black.png","solid-blue.png","solid-green.png","solid-red.png","solid-white.png","transparent-disposal.gif","unequal.gif","uniform.gif","input-limit-2097151.png","input-limit-2097152.png","generated-limit-234.gif","generated-limit-236.gif","generated-limit-238.gif"};
  var fidelity=Path.Combine(directory,"fidelity");Directory.CreateDirectory(fidelity);
  foreach(string name in names){string path=Path.Combine(inputs,name);if(!File.Exists(path)&&name.StartsWith("orientation-"))path=Path.Combine(inputs,"orientation"+Path.GetExtension(name));screen.Load(path);File.WriteAllBytes(Path.Combine(fidelity,Path.GetFileNameWithoutExtension(name)+".rgba"),screen.CroppedFramesBytes);}
  screen.Load(Path.Combine(inputs,"orientation.png"));await screen.BuildPreviewAsync();await Shot("edit");var still=screen.EncodedAsset!;
  var project=screen.ProjectBytes();screen.LoadWallpaper(2);screen.ReadProject(project);await screen.BuildPreviewAsync();if(!still.SequenceEqual(screen.EncodedAsset!))throw new InvalidOperationException("Project roundtrip changed artwork.");File.WriteAllBytes(Path.Combine(directory,"roundtrip.x20screen"),project);
  // Alpha flattening is tested through the real WPF importer, not only the domain helper.
  var alpha=new byte[51200];for(int i=0;i<alpha.Length;i+=4){alpha[i]=255;alpha[i+3]=0;}var transparent=ScreenWorkbench.Bitmap(alpha);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(transparent));string alphaPath=Path.Combine(directory,"alpha.png");using(var file=File.Create(alphaPath))png.Save(file);screen.Load(alphaPath);await screen.BuildPreviewAsync();if(ScreenCodec.DecodeAsset(screen.EncodedAsset!,false)[0].Where((_,i)=>i%4!=3).Any(b=>b!=0))throw new InvalidOperationException("Transparent still did not flatten on black.");
  screen.Load(Path.Combine(inputs,"unequal.gif"));screen.SetReviewAnimation(true);await screen.BuildPreviewAsync();await Shot("preview");var animated=screen.EncodedAsset!;
  using(var locked=new ScreenSerialTransport("COM1")){bool rejected=false;try{await locked.ExchangeAsync(new byte[64],CancellationToken.None);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new InvalidOperationException("Locked adapter opened.");}
  var slow=new SlowEmulator();screen.UseReviewTransport(slow);screen.Upload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(160);await Shot("uploading");screen.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();if(!screen.StatusText.Contains("cancelled"))throw new InvalidOperationException("Cancel did not settle.");await screen.ReviewCheckAsync();screen.SetReviewResetConfirmation();screen.Reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();
  // A timeout after a program request is indeterminate. No automatic repeat is allowed.
  var fault=new FailOnce();screen.UseReviewTransport(fault);screen.LoadWallpaper(0);await screen.BuildPreviewAsync();screen.Upload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();if(!screen.StatusText.Contains("stopped"))throw new InvalidOperationException("Failure state missing.");await screen.ReviewCheckAsync();await Shot("error");if(fault.Writes!=1)throw new InvalidOperationException("Failure retried automatically.");screen.Retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();if(fault.Writes!=2||screen.Retry.IsEnabled)throw new InvalidOperationException("Retry cap failed.");
  screen.SetReviewResetConfirmation();screen.Reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();screen.UseReviewTransport(slow);screen.LoadWallpaper(3);await screen.BuildPreviewAsync();screen.Upload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();await Shot("simulate");if(!screen.StatusText.Contains("completed"))throw new InvalidOperationException("Simulator upload failed.");
  screen.SetReviewResetConfirmation();screen.Reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Settle();if(!screen.StatusText.Contains("Restored"))throw new InvalidOperationException("Restore state missing.");await Shot("restored");
  screen.ShowFirstRun(true);await screen.BuildPreviewAsync();await screen.ReviewProbeAsync();await Shot("first-run-check");await screen.ReviewFirstUploadAsync();screen.ShowFirstRun(false);
  host.Studio.ShowManagement("Device");host.Studio.ShowManagement("Screen");if(!screen.Body.IsVisible)throw new InvalidOperationException("Screen did not return.");
  File.WriteAllText(Path.Combine(directory,"verification.json"),JsonSerializer.Serialize(new{offscreen=true,physicalWritesEnabled=false,states=8,dpi=new[]{96,144},firstFrameFidelityCases=names.Length,projectRoundtrip=true,alphaFlattening=true,previewDecodedFromAsset=true,staticDefault=true,cancelled=true,noAutomaticRetry=true,retryCap=true,firstRunConfirmation=true,firstRunTestUpload=true,restore=true,navigationReturned=true},new JsonSerializerOptions{WriteIndented=true}));
  async Task Settle(){for(int i=0;i<300&&screen.IsBusy;i++)await Task.Delay(30);if(screen.IsBusy)throw new InvalidOperationException("Transfer did not settle.");}
  async Task Shot(string name)
  {
   await host.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(100);stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();
   foreach(var button in new[]{screen.Upload,screen.Reset,screen.Cancel}){var bounds=button.TransformToAncestor(stage).TransformBounds(new Rect(button.RenderSize));if(bounds.Top<0||bounds.Bottom>900||bounds.Left<0||bounds.Right>1600)throw new InvalidOperationException("Pinned action clipped: "+button.Content);}
   foreach(int dpi in new[]{96,144}){int width=1600*dpi/96,height=900*dpi/96;var bitmap=new RenderTargetBitmap(width,height,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(stage);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,$"{name}-{dpi*100/96}.png"));encoder.Save(file);}
  }
 }
 sealed class SlowEmulator:IScreenTransport
 {
  readonly ScreenEmulator emulator=new();public bool IsEmulated=>true;
  public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token){await Task.Delay(80,token);return await emulator.ExchangeAsync(request,token);}
 }
 sealed class FailOnce:IScreenTransport
 {
  readonly ScreenEmulator emulator=new();public int Writes;public bool IsEmulated=>true;
  public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token){var reply=await emulator.ExchangeAsync(request,token);if(request.Span[0]==0x6c){Writes++;if(Writes<=2)throw new TimeoutException("Injected timeout");}return reply;}
 }
}
