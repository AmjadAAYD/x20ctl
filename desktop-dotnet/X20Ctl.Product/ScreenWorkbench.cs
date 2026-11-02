using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using X20Ctl.Zone.Screen;
using X20Ctl.Product.Scanner;
namespace X20Ctl.Product;

/// <summary>On-demand artwork editing with an explicit simulator/controller boundary.</summary>
public sealed class ScreenWorkbench
{
 public Grid Body {get;}=new();public Grid Shelf {get;}=new();
 public Button Upload {get;}public Button Reset {get;}public Button Import {get;}public Button Preview {get;}
 internal Button Retry=>retry;internal Button Cancel=>cancel;internal bool IsBusy=>busy;internal string StatusText=>status.Text;
 public byte[]? EncodedAsset=>asset?.ToArray();
 readonly Image image=new(){Width=480,Height=240,Stretch=Stretch.Fill};
 readonly TextBlock details=ManagementVisuals.Copy("Choose an image or wallpaper",14),status=ManagementVisuals.Copy("Simulate",14),sizeText=ManagementVisuals.Copy("Input 0 / 2 MiB · Upload 0 / 3 MiB",13),notice=ManagementVisuals.Copy("",13);
 readonly TextBox portName=new(){Width=150,HorizontalAlignment=HorizontalAlignment.Left};
 readonly ProgressBar transferProgress=new(){Minimum=0,Maximum=100,Height=6,Margin=new(0,8,0,6)};
 readonly CheckBox animation=new(){Content="Animation",Foreground=Brushes.White},simulate=new(){Content="Simulate",IsChecked=true,Foreground=Brushes.White},confirmReset=new(){Content="Confirm reset",Foreground=Brushes.White};
 readonly CheckBox before=new(){Content="Before: current screen noted",Foreground=Brushes.White},after=new(){Content="After: image and colors match",Foreground=Brushes.White};
 readonly Slider zoom=new(){Minimum=1,Maximum=4,Value=1},panX=new(){Minimum=-1,Maximum=1},panY=new(){Minimum=-1,Maximum=1},brightness=new(){Minimum=-.5,Maximum=.5},contrast=new(){Minimum=.25,Maximum=2,Value=1},fps=new(){Minimum=1,Maximum=20,Value=5,TickFrequency=1,IsSnapToTickEnabled=true},frameLimit=new(){Minimum=1,Maximum=1,Value=1,TickFrequency=1,IsSnapToTickEnabled=true};
 readonly ComboBox rotation=new(){ItemsSource=new[]{"0°","90°","180°","270°"},SelectedIndex=0,Width=100},fit=new(){ItemsSource=new[]{"Fill","Fit"},SelectedIndex=0,Width=100};
 readonly DispatcherTimer previewClock=new(),editClock=new(){Interval=TimeSpan.FromMilliseconds(250)};readonly List<BitmapSource> frames=[],encodedFrames=[];
 readonly ScreenEmulator emulator=new();readonly Button cancel,retry,checkTransfer;readonly StackPanel editor=new(),firstRun=new();readonly ScrollViewer scroll;
 readonly TextBlock flashStatus=ManagementVisuals.Copy("Flash ID —",14),deviceStatus=ManagementVisuals.Copy("Device —",13);
 readonly TextBlock firstStatus=ManagementVisuals.Copy("1 · Connect USB, then Detect",14);readonly Stopwatch firstTimer=new();
 uint? plannedNonce;byte[]? editorProject;
 CancellationTokenSource? operation;IScreenTransport? reviewTransport;byte[]? asset,sourceBytes,sourceAsset,uncertainAsset;bool uncertainAnimation;int uncertainDelay;int sourceBuiltin=-1,frameIndex,revision,delay=200,retries,transferEpoch;bool testUploaded;long inputBytes;DateTime previewUntil;bool busy,restoring,uncertain,checkedFailure,planReady,identityReady,firstMode,loading;string sourceName="";
 public ScreenWorkbench(FrameworkElement resources,Action<Button> register,Action back)
 {
  Button Action(string label,Action action)=>ManagementVisuals.Action(resources,label,action,register).Also(b=>b.Margin=new(0,4,6,4));
  void Field(string label,FrameworkElement control){editor.Children.Add(ManagementVisuals.Copy(label,13).Also(t=>t.Margin=new(0,8,0,3)));editor.Children.Add(control);}
  Body.ColumnDefinitions.Add(new(){Width=new(1.15,GridUnitType.Star)});Body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
  var left=new StackPanel();left.Children.Add(ManagementVisuals.Copy("Screen",28,true));left.Children.Add(ManagementVisuals.Copy("160 × 80",14));
  var bezel=new Border{Background=Brushes.Black,BorderBrush=DS.Brush("DS.TextSoft"),BorderThickness=new(2),CornerRadius=new(12),Padding=new(8),Margin=new(0,18,0,10),Child=new Viewbox{Child=image,Stretch=Stretch.Uniform}};left.Children.Add(bezel);left.Children.Add(details);left.Children.Add(sizeText);left.Children.Add(notice);
  var local=new WrapPanel{Margin=new(0,12,0,0)};local.Children.Add(Action("Open…",Open));local.Children.Add(Action("Save project…",SaveProject));local.Children.Add(Action("Export .bin…",Export));left.Children.Add(local);
  Body.Children.Add(Kit.Panel(null,null,null,new ScrollViewer{Content=left,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},new Thickness(20)).Also(b=>b.Margin=new(0,0,12,0)));
  editor.Children.Add(ManagementVisuals.Copy("Choose",20,true));Import=Action("Choose image…",Choose);editor.Children.Add(Import);
  var wallpapers=new WrapPanel();for(int i=0;i<6;i++){int n=i;wallpapers.Children.Add(Action(ScreenArtwork.WallpaperNames[i],()=>LoadWallpaper(n)));}editor.Children.Add(wallpapers);
  editor.Children.Add(ManagementVisuals.Copy("Edit",20,true).Also(t=>t.Margin=new(0,10,0,0)));
  var geometry=new WrapPanel();geometry.Children.Add(fit);geometry.Children.Add(rotation);editor.Children.Add(geometry);Field("Zoom",zoom);Field("Horizontal",panX);Field("Vertical",panY);Field("Brightness",brightness);Field("Contrast",contrast);editor.Children.Add(animation);Field("FPS",fps);Field("Frames",frameLimit);
  Preview=Action("Build preview",async()=>await BuildPreviewAsync());editor.Children.Add(Preview);editor.Children.Add(Action("Play / pause",()=>{if(previewClock.IsEnabled)StopPreview();else if(encodedFrames.Count>1){previewUntil=DateTime.UtcNow.AddSeconds(30);previewClock.Start();}}));
  var modes=new WrapPanel();modes.Children.Add(simulate);modes.Children.Add(Action("First-run check",()=>ShowFirstRun(true)));editor.Children.Add(modes);
  firstRun.Children.Add(ManagementVisuals.Copy("First-run check",22,true));firstRun.Children.Add(firstStatus);firstRun.Children.Add(deviceStatus);firstRun.Children.Add(flashStatus);
  firstRun.Children.Add(Action("1 · Detect",async()=>await Detect()));firstRun.Children.Add(ManagementVisuals.Copy("2 · Choose controller COM port",13));firstRun.Children.Add(portName);
  firstRun.Children.Add(Action("3 · Read flash ID",async()=>await ProbeAsync()));firstRun.Children.Add(Action("4 · Dry run",DryRun));firstRun.Children.Add(before);firstRun.Children.Add(Action("5 · Upload test image",async()=>{if(!identityReady||!planReady||before.IsChecked!=true){firstStatus.Text="Complete steps 1–4 and Before";return;}await TransferAsync(false);}));firstRun.Children.Add(after);firstRun.Children.Add(Action("Record result",()=>{if(!testUploaded){firstStatus.Text="Upload test image first";return;}firstTimer.Stop();firstStatus.Text=after.IsChecked==true?$"Result recorded · {firstTimer.Elapsed.TotalSeconds:0}s":"Confirm the image and colors";SaveSession("physical-observation",new{before=before.IsChecked,after=after.IsChecked,elapsedSeconds=firstTimer.Elapsed.TotalSeconds,port=portName.Text});}));firstRun.Children.Add(Action("Back to editor",()=>ShowFirstRun(false)));
  before.Checked+=(_,_)=>UpdateWriteGates();before.Unchecked+=(_,_)=>UpdateWriteGates();
  portName.TextChanged+=(_,_)=>{identityReady=planReady=false;plannedNonce=null;UpdateWriteGates();};
  Upload=Action("Upload",async()=>await TransferAsync(false));Reset=Action("Restore stock",async()=>{if(confirmReset.IsChecked!=true){status.Text="Select Confirm reset";return;}await TransferAsync(true);});
  cancel=Action("Cancel",()=>operation?.Cancel());cancel.IsEnabled=false;retry=Action("Retry once",async()=>{if(checkedFailure&&!restoring&&retries++==0){uncertain=false;await TransferAsync(false);}});checkTransfer=Action("Check transfer",async()=>await CheckAsync());
  var actions=new WrapPanel{Children={Upload,Reset,cancel}};var recovery=new WrapPanel{Children={checkTransfer,retry}};
  modes.Children.Remove(simulate);var footer=new StackPanel{Children={simulate,actions,confirmReset,recovery,status,transferProgress}};
  // The mode checkbox belongs to the pinned footer, keeping the transport choice visible.
  modes.Children.Remove(simulate);
  scroll=new ScrollViewer{Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var dock=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);dock.Children.Add(scroll);var panel=Kit.Panel(null,null,null,dock,new Thickness(20));Grid.SetColumn(panel,1);Body.Children.Add(panel);
  foreach(var slider in new[]{zoom,panX,panY,brightness,contrast,fps,frameLimit})slider.ValueChanged+=(_,_)=>Refresh();rotation.SelectionChanged+=(_,_)=>Refresh();fit.SelectionChanged+=(_,_)=>Refresh();animation.Checked+=(_,_)=>Refresh();animation.Unchecked+=(_,_)=>Refresh();simulate.Checked+=(_,_)=>ModeChanged();simulate.Unchecked+=(_,_)=>ModeChanged();
  editClock.Tick+=async(_,_)=>{editClock.Stop();await BuildPreviewAsync();};previewClock.Tick+=(_,_)=>{if(DateTime.UtcNow>=previewUntil||!Body.IsVisible){StopPreview();return;}if(encodedFrames.Count>0){frameIndex=(frameIndex+1)%encodedFrames.Count;image.Source=encodedFrames[frameIndex];}};Body.Unloaded+=(_,_)=>StopPreview();
  Body.AllowDrop=true;Body.DragOver+=(_,e)=>{e.Effects=!busy&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Body.Drop+=(_,e)=>{if(!busy&&e.Data.GetData(DataFormats.FileDrop) is string[] paths&&paths.Length==1)Try(()=>Load(paths[0]));};UpdateWriteGates();
 }
 void Try(Action action){try{action();}catch(Exception e)when(e is IOException or ArgumentException or NotSupportedException or JsonException or System.Runtime.InteropServices.COMException){status.Text=e.Message;}}
 void Choose(){var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(dialog.ShowDialog()==true)Try(()=>Load(dialog.FileName));}
 void Open(){var dialog=new OpenFileDialog{Filter="Screen projects and assets|*.x20screen;*.bin|Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(dialog.ShowDialog()==true)Try(()=>Load(dialog.FileName));}
 public void Load(string path)
 {
  if(busy)throw new InvalidOperationException("A transfer is running.");var info=new FileInfo(path);long cap=path.EndsWith(".x20screen",StringComparison.OrdinalIgnoreCase)?8*1024*1024:path.EndsWith(".bin",StringComparison.OrdinalIgnoreCase)?ScreenCodec.AssetLimit:ScreenCodec.InputLimit;if(info.Length>cap)throw new ArgumentException("File exceeds its size limit.");
  if(path.EndsWith(".x20screen",StringComparison.OrdinalIgnoreCase)){ReadProject(File.ReadAllBytes(path));return;}
  if(path.EndsWith(".bin",StringComparison.OrdinalIgnoreCase)){LoadEncoded(File.ReadAllBytes(path),100);sourceName=info.Name;return;}
  LoadImageBytes(File.ReadAllBytes(path),info.Name);
 }
 void LoadImageBytes(byte[] bytes,string name)
 {
  if(bytes.Length>ScreenCodec.InputLimit)throw new ArgumentException("Choose a file up to 2 MiB.");using var stream=new MemoryStream(bytes);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);if(decoder.Frames.Count>512)throw new ArgumentException("Use 512 frames or fewer.");var decoded=ScreenMedia.Decode(decoder);ResetEdits();frames.Clear();frames.AddRange(decoded.Frames);sourceBytes=bytes;sourceAsset=null;sourceBuiltin=-1;sourceName=name;inputBytes=bytes.Length;FinishLoad();notice.Text=frames.Count>1?"GIF delays normalized · transparency on black":"Transparency on black";
 }
 public void LoadWallpaper(int index){ResetEdits();frames.Clear();frames.Add(Bitmap(ScreenArtwork.Wallpaper(index)));sourceBytes=sourceAsset=null;sourceBuiltin=index;sourceName=ScreenArtwork.WallpaperNames[index];inputBytes=0;FinishLoad();}
 void LoadEncoded(byte[] bytes,int playbackDelay)
 {
  bool animated=ScreenCodec.IsAnimationAsset(bytes);var decoded=ScreenCodec.DecodeAsset(bytes,animated);ResetEdits();frames.Clear();frames.AddRange(decoded.Select(Bitmap));sourceBytes=null;sourceAsset=bytes.ToArray();sourceBuiltin=-1;sourceName="Encoded artwork";inputBytes=0;FinishLoad();animation.IsChecked=animated;fps.Value=Math.Clamp(1000d/playbackDelay,1,20);notice.Text="Encoded asset · delay stored in project";
 }
 void ResetEdits(){loading=true;transferProgress.Value=0;StopPreview();zoom.Value=1;panX.Value=panY.Value=brightness.Value=0;contrast.Value=1;rotation.SelectedIndex=fit.SelectedIndex=0;animation.IsChecked=false;fps.Value=5;planReady=false;}
 void FinishLoad(){notice.Text="Transparency on black";frameLimit.Maximum=Math.Max(1,frames.Count);frameLimit.Value=frames.Count;loading=false;Refresh();details.Text=$"{sourceName} · {frames.Count} frame{(frames.Count==1?"":"s")}";}
 void Refresh()
 {
  if(loading)return;revision++;planReady=false;plannedNonce=null;asset=null;encodedFrames.Clear();StopPreview();UpdateWriteGates();if(frames.Count>0){image.Source=Crop(frames[0]);editClock.Stop();editClock.Start();}sizeText.Text=$"Input {inputBytes:N0} / 2,097,152 · Upload calculating / 3,145,728";
 }
 internal byte[] FirstCroppedPixels=>Rgba(Crop(frames[0]));
 internal byte[] CroppedFramesBytes=>frames.SelectMany(f=>Rgba(Crop(f))).ToArray();
 BitmapSource Crop(BitmapSource source)
 {
  BitmapSource oriented=rotation.SelectedIndex==0?source:new TransformedBitmap(source,new RotateTransform(rotation.SelectedIndex*90));
  double scale=(fit.SelectedIndex==1?Math.Min(160d/oriented.PixelWidth,80d/oriented.PixelHeight):Math.Max(160d/oriented.PixelWidth,80d/oriented.PixelHeight))*zoom.Value;
  double w=oriented.PixelWidth*scale,h=oriented.PixelHeight*scale,x=(160-w)*(panX.Value+1)/2,y=(80-h)*(panY.Value+1)/2;
  var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.Black,null,new Rect(0,0,160,80));drawing.DrawImage(oriented,new Rect(x,y,w,h));}
  var result=new RenderTargetBitmap(160,80,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
 internal static BitmapSource Bitmap(byte[] rgba){var bytes=rgba.ToArray();for(int i=0;i<bytes.Length;i+=4)(bytes[i],bytes[i+2])=(bytes[i+2],bytes[i]);var bitmap=BitmapSource.Create(160,80,96,96,PixelFormats.Bgra32,null,bytes,640);bitmap.Freeze();return bitmap;}
 static byte[] Rgba(BitmapSource source){var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var bytes=new byte[51200];converted.CopyPixels(bytes,640,0);for(int i=0;i<bytes.Length;i+=4)(bytes[i],bytes[i+2])=(bytes[i+2],bytes[i]);return bytes;}
 public async Task BuildPreviewAsync()
 {
  editClock.Stop();if(busy||frames.Count==0)return;busy=true;StopPreview();UpdateWriteGates();
  try
  {
   int version=revision;bool animated=animation.IsChecked==true;delay=(int)Math.Clamp(Math.Round(100d/fps.Value)*10,10,2550);previewClock.Interval=TimeSpan.FromMilliseconds(delay);
   var pixels=(animated?frames.Take((int)frameLimit.Value):frames.Take(1)).Select(f=>ScreenArtwork.Adjust(Rgba(Crop(f)),brightness.Value,contrast.Value)).ToArray();
   var built=await Task.Run(()=>animated?ScreenCodec.EncodeAnimation(pixels):ScreenCodec.EncodeStill(pixels[0]));if(version!=revision)return;asset=built;encodedFrames.Clear();encodedFrames.AddRange(ScreenCodec.DecodeAsset(asset,animated).Select(Bitmap));image.Source=encodedFrames[0];
   sizeText.Text=$"Input {inputBytes:N0} / 2,097,152 · Upload {asset.Length:N0} / 3,145,728";status.Text=$"{(animated?$"{encodedFrames.Count} frames · {1000d/delay:0.#} FPS":"Static wallpaper")} · {(simulate.IsChecked==true?"Simulate":"Controller")}";
  }
  catch(ArgumentException e){asset=null;status.Text=e.Message;}
  finally{busy=false;UpdateWriteGates();if(asset==null&&frames.Count>0&&revision>0)sizeText.Text="Upload unavailable · reduce frames or size";}
 }
 void ModeChanged(){if(busy){simulate.IsChecked=reviewTransport?.IsEmulated==true;return;}identityReady=planReady=false;plannedNonce=null;UpdateWriteGates();status.Text=simulate.IsChecked==true?"Simulate":"Controller";}
 IScreenTransport Transport()=>simulate.IsChecked==true?reviewTransport??emulator:new ScreenSerialTransport(portName.Text.Trim(),plannedNonce,NewTracePath());
 bool Ready=>(simulate.IsChecked==true||(ScreenHardwareEvidence.VerifiedDevice&&ScreenSerialTransport.ValidPortName(portName.Text.Trim())))&&!busy;
 void UpdateWriteGates(){editor.IsEnabled=firstRun.IsEnabled=!busy;Upload.IsEnabled=Ready&&asset!=null&&!uncertain&&(!firstMode||(identityReady&&planReady&&before.IsChecked==true));Reset.IsEnabled=Ready;retry.IsEnabled=Ready&&checkedFailure&&!restoring&&retries==0;checkTransfer.IsEnabled=Ready&&uncertain&&!restoring;cancel.IsEnabled=busy;Import.IsEnabled=Preview.IsEnabled=!busy;simulate.IsEnabled=!busy;}
 async Task TransferAsync(bool reset)
 {
  if(!Ready||(!reset&&(asset==null||uncertain||firstMode&&(!identityReady||!planReady||before.IsChecked!=true))))return;int epoch=++transferEpoch;transferProgress.Value=0;busy=true;restoring=reset;if(!reset){uncertainAsset=asset!.ToArray();uncertainAnimation=animation.IsChecked==true;uncertainDelay=delay;}StopPreview();editClock.Stop();UpdateWriteGates();operation=new();
  try
  {
   var transport=Transport();using var lease=transport as IDisposable;var sender=new ScreenSender(transport);var progress=new Progress<ScreenProgress>(p=>{if(busy&&epoch==transferEpoch){transferProgress.Value=p.Total==0?0:100d*p.Sent/p.Total;status.Text=$"{(simulate.IsChecked==true?"Simulate · ":"")}{p.State} · {p.Sent:N0} / {p.Total:N0}";}});
   if(reset)await sender.ResetAsync(progress,operation.Token);else await sender.UploadAsync(asset!,animation.IsChecked==true,delay,progress,operation.Token,plannedNonce);
   uncertain=checkedFailure=false;retries=0;transferProgress.Value=100;if(firstMode&&!reset)testUploaded=true;status.Text=reset?"Restored · restart controller":"Upload completed";confirmReset.IsChecked=false;SaveSession(reset?"restore":"upload",new{simulated=simulate.IsChecked,bytes=reset?4:asset!.Length,port=portName.Text});
  }
  catch(OperationCanceledException){uncertain=true;status.Text="Transfer cancelled · check transfer";}
  catch(Exception e)when(e is IOException or InvalidOperationException or ArgumentException or TimeoutException or UnauthorizedAccessException){uncertain=true;status.Text="Transfer stopped · "+e.Message;SaveSession("failure",new{error=e.Message,reset,port=portName.Text});}
  finally{operation.Dispose();operation=null;busy=false;UpdateWriteGates();}
 }
 async Task CheckAsync()
 {
  if(!Ready||uncertainAsset==null||!uncertain)return;busy=true;UpdateWriteGates();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(100));operation=stop;
  try{var transport=Transport();using var lease=transport as IDisposable;bool match=await new ScreenSender(transport).VerifyAsync(uncertainAsset,uncertainAnimation,uncertainDelay,stop.Token);checkedFailure=!match;status.Text=match?"Transfer verified":"Readback differs · Retry once or Restore stock";if(match)uncertain=false;}
  catch(Exception e)when(e is IOException or InvalidOperationException or TimeoutException or OperationCanceledException){checkedFailure=false;status.Text="Readback unavailable · "+e.Message;}
  finally{operation=null;busy=false;UpdateWriteGates();}
 }
 internal void ShowFirstRun(bool show)
 {
  StopPreview();if(show&&!firstMode){testUploaded=false;before.IsChecked=after.IsChecked=false;editorProject=frames.Count>0?ProjectBytes():null;var pixels=ScreenArtwork.TestImage();ResetEdits();frames.Clear();frames.Add(Bitmap(pixels));sourceAsset=ScreenCodec.EncodeStill(pixels);sourceBytes=null;sourceBuiltin=-1;sourceName="Upload test image";inputBytes=0;FinishLoad();firstTimer.Restart();firstStatus.Text="1 · Connect USB, then Detect";}else if(!show&&firstMode&&editorProject!=null)ReadProject(editorProject);firstMode=show;scroll.Content=show?firstRun:editor;UpdateWriteGates();
 }
 async Task Detect()
 {
  if(!firstTimer.IsRunning)firstTimer.Start();var ports=ReviewSandbox.Active?new[]{"COM7"}:SerialPort.GetPortNames();
  var inventory=ReviewSandbox.Active?new Inventory():await Inventory.Bound(Task.Run(()=>WindowsInventory.Read()),3000);
  var candidates=inventory.interfaces.Where(d=>d.vid is 0x1020 or 0x413d||(d.product+" "+d.manufacturer).Contains("EasySMX",StringComparison.OrdinalIgnoreCase)).Select(d=>d.product).Distinct().ToArray();
  deviceStatus.Text=candidates.Length==0?"Device not identified":string.Join(" · ",candidates);firstStatus.Text=$"Ports: {(ports.Length==0?"none":string.Join(", ",ports))} · choose explicitly · {firstTimer.Elapsed.TotalSeconds:0}s";SaveSession("detect",new{ports,candidates,errors=inventory.errors,model="x20_pro"});
 }
 async Task ProbeAsync()
 {
  if(!Ready){firstStatus.Text="Choose COM port · enable owner writes";return;}busy=true;UpdateWriteGates();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));operation=stop;
  try{var transport=Transport();using var lease=transport as IDisposable;uint id=await new ScreenSender(transport).ProbeAsync(stop.Token);flashStatus.Text=$"Flash 0x{id:X6}";firstStatus.Text=$"Identity read · {firstTimer.Elapsed.TotalSeconds:0}s";ScreenSender.RequireSupportedFlash(id);identityReady=true;SaveSession("identity",new{flashId=$"0x{id:X6}",port=portName.Text,simulated=simulate.IsChecked});}
  catch(Exception e)when(e is IOException or InvalidOperationException or TimeoutException or OperationCanceledException){identityReady=false;firstStatus.Text=e.Message;}
  finally{operation=null;busy=false;UpdateWriteGates();}
 }
 void DryRun(){if(asset==null){firstStatus.Text="Build preview first";return;}uint nonce=ScreenProtocol.Nonce(DateTime.Now);plannedNonce=nonce;var frames=ScreenTransferPlan.Build(asset,animation.IsChecked==true,delay,nonce);var opening=new[]{ScreenProtocol.Handshake(0x30,nonce,9),ScreenProtocol.Handshake(0x30,nonce)};SaveSession("dry-run",new{port=portName.Text,nonce,opening=opening.Select(Convert.ToHexString),frames=frames.Select(Convert.ToHexString),bytes=frames.Sum(f=>f.Length)+128,delay});planReady=true;UpdateWriteGates();firstStatus.Text=$"Dry run · {frames.Count+2} frames · no bytes sent";}
 string SessionRoot(){string root=ReviewSandbox.Active?Path.Combine(Path.GetTempPath(),"X20CTL-ScreenReview"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"X20CTL","Screen","FirstRun");Directory.CreateDirectory(root);return root;}
 string NewTracePath()=>Path.Combine(SessionRoot(),$"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-serial.jsonl");
 void SaveSession(string name,object value){File.WriteAllText(Path.Combine(SessionRoot(),$"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{name}.json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));}
 sealed record Project(int Version,byte[]? Source,byte[]? Encoded,int Builtin,string Name,bool Animation,double Zoom,double X,double Y,int Rotation,int Fit,double Brightness,double Contrast,double Fps,int Frames);
 internal byte[] ProjectBytes()=>JsonSerializer.SerializeToUtf8Bytes(new Project(1,sourceBytes,sourceAsset,sourceBuiltin,sourceName,animation.IsChecked==true,zoom.Value,panX.Value,panY.Value,rotation.SelectedIndex,fit.SelectedIndex,brightness.Value,contrast.Value,fps.Value,(int)frameLimit.Value));
 internal void ReadProject(byte[] bytes)
 {
  if(bytes.Length>8*1024*1024)throw new ArgumentException("Project exceeds 8 MiB.");var p=JsonSerializer.Deserialize<Project>(bytes)??throw new InvalidDataException("Invalid project.");if(p.Version!=1||p.Zoom is <1 or >4||p.X is <-1 or >1||p.Y is <-1 or >1||p.Rotation is <0 or >3||p.Fit is <0 or >1||p.Brightness is <-.5 or >.5||p.Contrast is <.25 or >2||p.Fps is <1 or >20||p.Frames is <1 or >512)throw new InvalidDataException("Invalid project settings.");
  if(p.Source!=null)LoadImageBytes(p.Source,p.Name);else if(p.Encoded!=null)LoadEncoded(p.Encoded,200);else LoadWallpaper(p.Builtin);loading=true;animation.IsChecked=p.Animation;zoom.Value=p.Zoom;panX.Value=p.X;panY.Value=p.Y;rotation.SelectedIndex=p.Rotation;fit.SelectedIndex=p.Fit;brightness.Value=p.Brightness;contrast.Value=p.Contrast;fps.Value=p.Fps;frameLimit.Value=Math.Min(p.Frames,frames.Count);loading=false;Refresh();
 }
 void SaveProject(){var dialog=new SaveFileDialog{Filter="X20CTL Screen project|*.x20screen",DefaultExt=".x20screen"};if(frames.Count>0&&dialog.ShowDialog()==true)Try(()=>File.WriteAllBytes(dialog.FileName,ProjectBytes()));}
 void Export(){if(asset==null){status.Text="Build preview first";return;}var dialog=new SaveFileDialog{Filter="Screen binary|*.bin",DefaultExt=".bin"};if(dialog.ShowDialog()==true)Try(()=>File.WriteAllBytes(dialog.FileName,asset));}
 internal void UseReviewTransport(IScreenTransport transport){if(!ReviewSandbox.Active||!transport.IsEmulated)throw new InvalidOperationException("Review transport must be emulated.");reviewTransport=transport;simulate.IsChecked=true;UpdateWriteGates();}
 public void StopPreview(){previewClock.Stop();frameIndex=0;}
 internal void SetReviewAnimation(bool enabled){animation.IsChecked=enabled;}
 internal void SetReviewResetConfirmation()=>confirmReset.IsChecked=true;
 internal async Task ReviewProbeAsync(){await ProbeAsync();DryRun();}
 internal async Task ReviewFirstUploadAsync()
 {
  if(Upload.IsEnabled)throw new InvalidOperationException("First-run upload bypassed Before confirmation.");before.IsChecked=true;if(!Upload.IsEnabled)throw new InvalidOperationException("Completed first-run checks did not enable upload.");await TransferAsync(false);if(!testUploaded)throw new InvalidOperationException("First-run test upload failed.");after.IsChecked=true;
 }
 internal Task ReviewCheckAsync()=>CheckAsync();
}
