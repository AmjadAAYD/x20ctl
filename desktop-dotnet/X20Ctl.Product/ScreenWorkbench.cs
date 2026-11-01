using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Product;

/// <summary>Local, on-demand screen editor. Does not open a hardware transport.</summary>
public sealed class ScreenWorkbench
{
 public Grid Body {get;}=new();public Grid Shelf {get;}=new();
 public Button Upload {get;}public Button Reset {get;}public Button Import {get;}public Button Preview {get;}
 readonly Image image=new(){Width=480,Height=240,Stretch=Stretch.Fill};
 readonly TextBlock details=ManagementVisuals.Copy("Choose a PNG, JPG, BMP or GIF · up to 2 MiB",14);
 readonly TextBox portName=new(){Width=140,HorizontalAlignment=HorizontalAlignment.Left,ToolTip="Verified controller COM port, for example COM7"};
 readonly ProgressBar transferProgress=new(){Minimum=0,Maximum=100,Height=8,Margin=new(0,12,0,8)};
 readonly Button cancel;CancellationTokenSource? operation;IScreenTransport? reviewTransport;
 internal Button Cancel=>cancel;internal bool IsBusy=>busy;internal string StatusText=>status.Text;
 readonly TextBlock status=ManagementVisuals.Copy("Static wallpaper · one upload · no streaming",14);
 readonly CheckBox animation=new(){Content="Use animation",Foreground=Brushes.White,Margin=new(0,12,0,12)};
 readonly Slider zoom=new(){Minimum=1,Maximum=4,Value=1},panX=new(){Minimum=-1,Maximum=1},panY=new(){Minimum=-1,Maximum=1};
 readonly ComboBox rotation=new(){ItemsSource=new[]{"0°","90°","180°","270°"},SelectedIndex=0,Width=130,HorizontalAlignment=HorizontalAlignment.Left};
 readonly DispatcherTimer previewClock=new();readonly List<BitmapSource> frames=[];
 int frameIndex,delay=100,revision;DateTime previewUntil;byte[]? asset;bool busy;
 public byte[]? EncodedAsset=>asset?.ToArray();
 public ScreenWorkbench(FrameworkElement resources,Action<Button> register,Action back)
 {

  Body.ColumnDefinitions.Add(new(){Width=new(1.25,GridUnitType.Star)});Body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
  var left=new StackPanel();left.Children.Add(ManagementVisuals.Copy("Screen",28,true));left.Children.Add(ManagementVisuals.Copy("Your artwork on the X20 Pro",15));
  var bezel=new Border{Background=Brushes.Black,BorderBrush=DS.Brush("DS.TextSoft"),BorderThickness=new(2),CornerRadius=new(12),Padding=new(12),Margin=new(0,28,0,12),Child=new Viewbox{Child=image,Stretch=Stretch.Uniform}};left.Children.Add(bezel);
  left.Children.Add(ManagementVisuals.Copy("160 × 80 · preview",13));left.Children.Add(details);
  var backButton=ManagementVisuals.Action(resources,"Back to Device",back,register);backButton.HorizontalAlignment=HorizontalAlignment.Left;backButton.Margin=new(0,28,0,0);left.Children.Add(backButton);
  Body.Children.Add(Kit.Panel(null,null,null,left,new Thickness(24)).Also(b=>b.Margin=new(0,0,16,0)));
  var right=new StackPanel();right.Children.Add(ManagementVisuals.Copy("Artwork",22,true));
  Import=ManagementVisuals.Action(resources,"Choose image…",Choose,register);Import.Margin=new(0,16,0,12);right.Children.Add(Import);right.Children.Add(animation);
  foreach(var (label,control) in new (string,FrameworkElement)[]{("Rotation",rotation),("Zoom",zoom),("Horizontal position",panX),("Vertical position",panY)}){right.Children.Add(ManagementVisuals.Copy(label,13).Also(t=>t.Margin=new(0,10,0,6)));right.Children.Add(control);}
  Preview=ManagementVisuals.Action(resources,"Build preview",async()=>await BuildPreviewAsync(),register);Preview.Margin=new(0,20,0,12);right.Children.Add(Preview);
  var play=ManagementVisuals.Action(resources,"Play / pause",()=>{if(previewClock.IsEnabled)StopPreview();else if(animation.IsChecked==true&&frames.Count>1){previewUntil=DateTime.UtcNow.AddSeconds(30);previewClock.Start();}},register);right.Children.Add(play);
  Upload=ManagementVisuals.Action(resources,"Upload",async()=>await TransferAsync(false),register);Reset=ManagementVisuals.Action(resources,"Reset screen",async()=>await TransferAsync(true),register);Upload.IsEnabled=Reset.IsEnabled=false;
  right.Children.Add(ManagementVisuals.Copy("Controller COM port",13).Also(t=>t.Margin=new(0,14,0,6)));right.Children.Add(portName);portName.TextChanged+=(_,_)=>UpdateWriteGates();
  var actions=new WrapPanel{Margin=new(0,12,0,8),Children={Upload,Reset}};
  cancel=ManagementVisuals.Action(resources,"Cancel",()=>operation?.Cancel(),register);cancel.IsEnabled=false;cancel.HorizontalAlignment=HorizontalAlignment.Left;
  var operationPanel=new StackPanel{Children={actions,ManagementVisuals.Copy("Uploads unlock after device verification. Reset clears custom artwork; it does not restore the factory image.",13),status.Also(t=>t.Margin=new(0,12,0,0)),transferProgress,cancel}};
  var scroll=new ScrollViewer{Content=right,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var editorRoot=new DockPanel();DockPanel.SetDock(operationPanel,Dock.Bottom);editorRoot.Children.Add(operationPanel);editorRoot.Children.Add(scroll);var edit=Kit.Panel(null,null,null,editorRoot,new Thickness(24));Grid.SetColumn(edit,1);Body.Children.Add(edit);
  zoom.ValueChanged+=(_,_)=>Refresh();panX.ValueChanged+=(_,_)=>Refresh();panY.ValueChanged+=(_,_)=>Refresh();rotation.SelectionChanged+=(_,_)=>Refresh();animation.Checked+=(_,_)=>Refresh();animation.Unchecked+=(_,_)=>{StopPreview();Refresh();};
  previewClock.Tick+=(_,_)=>{if(DateTime.UtcNow>=previewUntil||!Body.IsVisible){StopPreview();return;}if(frames.Count==0)return;frameIndex=(frameIndex+1)%frames.Count;Render();};previewClock.Interval=TimeSpan.FromMilliseconds(delay);Body.Unloaded+=(_,_)=>StopPreview();
 }
 void Choose()
 {
  var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",CheckFileExists=true};if(dialog.ShowDialog()!=true)return;
  try{Load(dialog.FileName);}catch(Exception e)when(e is IOException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException){status.Text="Could not open image: "+e.Message;}
 }
 public void Load(string path)
 {
  StopPreview();using var file=File.OpenRead(path);if(file.Length>ScreenCodec.InputLimit)throw new ArgumentException("Choose a file up to 2 MiB.");
  var decoder=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
  if(decoder.Frames.Count>512)throw new ArgumentException("Reduce the GIF to 512 frames or fewer.");
  var decoded=ScreenMedia.Decode(decoder);frames.Clear();frames.AddRange(decoded.Frames);delay=decoded.Delay;previewClock.Interval=TimeSpan.FromMilliseconds(delay);frameIndex=0;animation.IsChecked=false;zoom.Value=1;panX.Value=panY.Value=0;rotation.SelectedIndex=0;Refresh();details.Text=$"{Path.GetFileName(path)} · {frames.Count} frame{(frames.Count==1?"":"s")} · {file.Length:N0} bytes";
 }
 void Refresh(){revision++;asset=null;UpdateWriteGates();frameIndex=0;if(frames.Count>0)Render();status.Text="Static by default · Build preview to calculate upload size";}
 void Render(){image.Source=Crop(frames[frameIndex]);}
 BitmapSource Crop(BitmapSource source)
 {
  BitmapSource oriented=rotation.SelectedIndex==0?source:new TransformedBitmap(source,new RotateTransform(rotation.SelectedIndex*90));
  double scale=Math.Max(160d/oriented.PixelWidth,80d/oriented.PixelHeight)*zoom.Value;
  var scaled=new TransformedBitmap(oriented,new ScaleTransform(scale,scale));int w=scaled.PixelWidth,h=scaled.PixelHeight;
  int x=(int)Math.Round((w-160)*(panX.Value+1)/2),y=(int)Math.Round((h-80)*(panY.Value+1)/2);
  var result=new CroppedBitmap(scaled,new Int32Rect(Math.Clamp(x,0,Math.Max(0,w-160)),Math.Clamp(y,0,Math.Max(0,h-80)),160,80));result.Freeze();return result;
 }
 static byte[] Rgba(BitmapSource source)
 {
  var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[51200];converted.CopyPixels(bytes,640,0);for(int i=0;i<bytes.Length;i+=4)(bytes[i],bytes[i+2])=(bytes[i+2],bytes[i]);return bytes;
 }
 public async Task BuildPreviewAsync()
 {
  if(busy||frames.Count==0){if(frames.Count==0)status.Text="Choose an image first.";return;}busy=true;Preview.IsEnabled=Import.IsEnabled=false;StopPreview();
  try
  {
   int version=revision;bool animated=animation.IsChecked==true;var pixels=(animated?frames:frames.Take(1)).Select(f=>Rgba(Crop(f))).ToArray();
   var built=await Task.Run(()=>animated?ScreenCodec.EncodeAnimation(pixels):ScreenCodec.EncodeStill(pixels[0]));if(version!=revision){status.Text="Artwork changed. Build the preview again.";return;}asset=built;
   status.Text=$"{asset.Length:N0} bytes + 4,096 metadata · {(animated?$"{frames.Count} frames · {delay} ms":"Static wallpaper")} · no streaming";
  }
  catch(ArgumentException e){asset=null;status.Text=e.Message;}
  finally{busy=false;Preview.IsEnabled=Import.IsEnabled=true;UpdateWriteGates();}
 }
 void UpdateWriteGates(){bool ready=(reviewTransport?.IsEmulated==true||(ScreenHardwareEvidence.VerifiedDevice&&ScreenSerialTransport.ValidPortName(portName.Text.Trim())))&&!busy;Upload.IsEnabled=ready&&asset!=null;Reset.IsEnabled=ready;}
 async Task TransferAsync(bool reset)
 {
  if(busy||(reviewTransport==null&&(!ScreenHardwareEvidence.VerifiedDevice||!ScreenSerialTransport.ValidPortName(portName.Text.Trim()))))return;
  if(!reset&&asset==null)return;busy=true;StopPreview();UpdateWriteGates();Preview.IsEnabled=Import.IsEnabled=false;cancel.IsEnabled=true;operation=new CancellationTokenSource();
  try
  {
   IScreenTransport transport=reviewTransport??new ScreenSerialTransport(portName.Text.Trim());using var lease=transport as IDisposable;var sender=new ScreenSender(transport);
   var progress=new Progress<ScreenProgress>(p=>{if(!busy)return;transferProgress.Value=p.Total==0?0:100d*p.Sent/p.Total;status.Text=$"{(reviewTransport!=null?"Emulator · ":"")}{p.State} · {p.Sent:N0} / {p.Total:N0} bytes";});
   if(reset)await sender.ResetAsync(progress,operation.Token);else await sender.UploadAsync(asset!,animation.IsChecked==true,delay,progress,operation.Token);
   status.Text=reset?"Custom artwork cleared.":"Upload completed. Check the controller screen.";
  }
  catch(OperationCanceledException){status.Text="Transfer cancelled. The screen may contain a partial update.";}
  catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or TimeoutException or UnauthorizedAccessException){status.Text="Transfer stopped: "+e.Message;}
  finally{operation.Dispose();operation=null;busy=false;cancel.IsEnabled=false;Preview.IsEnabled=Import.IsEnabled=true;UpdateWriteGates();}
 }
 internal void UseReviewTransport(IScreenTransport transport)
 {
  if(!ReviewSandbox.Active||!transport.IsEmulated)throw new InvalidOperationException("Emulator injection is restricted to offscreen review.");reviewTransport=transport;UpdateWriteGates();
 }
 public void StopPreview(){previewClock.Stop();frameIndex=0;}
}
