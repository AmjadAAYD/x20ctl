using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using X20Ctl.Product;
class FakeLanding:IIntroHandoff {
 public int BeginCount,EndCount;public double Brand;public Rect? MarkTarget(Visual to)=>new Rect(30,24,26,22);
 public void HandoffBegin()=>BeginCount++;
 public void Handoff(double reveal,double brand,double controller,double pulse){Brand=brand;if(new[]{reveal,brand,controller,pulse}.Any(v=>!double.IsFinite(v)))throw new Exception("Nonfinite handoff");}
 public void HandoffEnd()=>EndCount++;
}
class Program{
 [STAThread]static void Main(string[] args){
 var app=new Application();foreach(var t in new[]{"Colors","Typography","Controls","Design","ZoneSurfaces"})app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri($"pack://application:,,,/X20Ctl;component/Themes/{t}.xaml")});
if(args.Contains("--followup")){FollowupChecks.Run();app.Shutdown();return;}
if(args.Contains("--mouse-refinement")){MouseRefinementChecks.Run();app.Shutdown();return;}
if(args.Contains("--setup-isolation")){SetupIsolationChecks.Run();app.Shutdown();return;}
if(args.Contains("--zone-surfaces")){ZoneSurfaceChecks.Run();app.Shutdown();return;}
if(typeof(IntroView).GetField("formation",BindingFlags.Instance|BindingFlags.NonPublic)==null)throw new Exception("Intro does not use the blue/orange preview formation");
if(typeof(IntroView).GetField("stabilizationBackground",BindingFlags.Instance|BindingFlags.NonPublic)==null)throw new Exception("X stabilization has no background impact");
var impactField=typeof(IntroView).GetField("stabilizationBackground",BindingFlags.Instance|BindingFlags.NonPublic)!;
var isolatedImpact=(FrameworkElement)Activator.CreateInstance(impactField.FieldType,true)!;isolatedImpact.Measure(new Size(640,400));isolatedImpact.Arrange(new Rect(0,0,640,400));
double ImpactEnergy(double t){impactField.FieldType.GetMethod("SetTime")!.Invoke(isolatedImpact,[t]);isolatedImpact.UpdateLayout();var b=new RenderTargetBitmap(640,400,96,96,PixelFormats.Pbgra32);b.Render(isolatedImpact);var p=new byte[640*400*4];b.CopyPixels(p,640*4,0);return Enumerable.Range(0,640*400).Average(i=>(p[i*4]+p[i*4+1]+p[i*4+2])/3d);}
double beforeImpact=ImpactEnergy(-1),peakImpact=ImpactEnergy(.10),afterImpact=ImpactEnergy(1.5);
if(peakImpact<beforeImpact*2||afterImpact>peakImpact*.5)throw new Exception($"Background impact does not rise and release: {beforeImpact}/{peakImpact}/{afterImpact}");
Console.WriteLine($"PASS: background impact rendered energy {beforeImpact:0.00} → {peakImpact:0.00} → {afterImpact:0.00}; one landing pulse and release.");
 if(typeof(IntroView).GetField("material",BindingFlags.Instance|BindingFlags.NonPublic)!=null)throw new Exception("Rejected controller PNG still in intro");
// the synthesised score ships beside the app; the checks never play it
var score=System.IO.Path.Combine(AppContext.BaseDirectory,"Brand","intro.wav");
if(!IntroView.HasIntroScore||!System.IO.File.Exists(score))throw new Exception("Intro score not packaged");
using(var reader=new System.IO.BinaryReader(System.IO.File.OpenRead(score))){reader.BaseStream.Seek(22,System.IO.SeekOrigin.Begin);int channels=reader.ReadInt16(),rate=reader.ReadInt32();double seconds=(new System.IO.FileInfo(score).Length-44)/(rate*channels*2.0);
if(channels!=2||rate!=48000||Math.Abs(seconds-7.1)>.01)throw new Exception($"Intro score is {channels} ch {rate} Hz {seconds:0.00} s");}
IntroView.SoundEnabled=false;var audioIntro=new IntroView(false,false);
if(audioIntro.Mute.Visibility!=Visibility.Visible)throw new Exception("Mute hidden while the intro has sound");
audioIntro.ToggleMute();audioIntro.ToggleMute();
if(typeof(IntroView).GetField("sound",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(audioIntro)!=null)throw new Exception("Intro opened a player with sound disabled");audioIntro.Finish();
var quietIntro=new IntroView(true,false);if(quietIntro.Mute.Visibility!=Visibility.Collapsed)throw new Exception("Reduced motion offers sound");quietIntro.Finish();
Console.WriteLine($"PASS: no controller photo; intro score packaged (stereo 48 kHz, 7.1 s), mute shown, no player opened with sound disabled or reduced motion.");
 IntroView.SoundEnabled=false;var intro=new IntroView(false,false);intro.Measure(new Size(1280,800));intro.Arrange(new Rect(0,0,1280,800));intro.UpdateLayout();
 typeof(IntroView).GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(intro,null);
 var story=(Storyboard)typeof(IntroView).GetField("story",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(intro)!;
 story.Begin(intro,true);
 var dir=System.IO.Path.Combine(Environment.CurrentDirectory,"artifacts","intro-stabilization-impact");System.IO.Directory.CreateDirectory(dir);
 foreach(var t in new[]{0d,.5,1.25,1.5,1.58,1.7,1.9,2.2,2.4,2.9,3.1,3.3,3.5,3.6,3.75,3.9,3.97,4.03,4.05,4.10,4.18,4.35,4.5,4.70,5.10,4.8,5.4,6.3,6.85}){
 story.SeekAlignedToLastTick(intro,TimeSpan.FromSeconds(t),TimeSeekOrigin.BeginTime);story.Pause(intro);typeof(IntroView).GetMethod("Frame",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(intro,[t]);intro.UpdateLayout();
 var backgroundImpact=(FrameworkElement)typeof(IntroView).GetField("stabilizationBackground",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(intro)!;
 var bitmap=new RenderTargetBitmap(1280,800,96,96,PixelFormats.Pbgra32);bitmap.Render(intro);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(System.IO.Path.Combine(dir,$"intro-{t:0.00}.png"));png.Save(file);
 // the controller (1.90) and the settled mark (5.40) stay the owner's reference; the X in motion was redrawn 17 Oct
 if(new[]{1.9,5.4}.Contains(t)){
 var reference=System.IO.Path.Combine(Environment.CurrentDirectory,"artifacts","intro-shared-video-reference",$"intro-{t:0.00}.png");
 var expected=new BitmapImage();expected.BeginInit();expected.UriSource=new Uri(reference);expected.CacheOption=BitmapCacheOption.OnLoad;expected.EndInit();
 backgroundImpact.Visibility=Visibility.Collapsed;var baseline=new RenderTargetBitmap(1280,800,96,96,PixelFormats.Pbgra32);baseline.Render(intro);backgroundImpact.Visibility=Visibility.Visible;
 var expectedPixels=new byte[1280*800*4];var actualPixels=new byte[1280*800*4];
 new FormatConvertedBitmap(expected,PixelFormats.Pbgra32,null,0).CopyPixels(expectedPixels,1280*4,0);baseline.CopyPixels(actualPixels,1280*4,0);
 double error=Enumerable.Range(0,1280*680*4).Average(i=>Math.Abs(actualPixels[i]-expectedPixels[i]));
 Console.WriteLine($"REFERENCE at {t:0.00}s: mean absolute pixel difference {error:0.000000} /255 (bottom sound controls excluded).");
 if(error>1.0)throw new Exception("Intro diverges from shared blue/orange preview at "+t);
 }
 }
 if(args.Contains("--preview"))for(int frame=0;frame<213;frame++){
 double t=frame/30d;story.SeekAlignedToLastTick(intro,TimeSpan.FromSeconds(t),TimeSeekOrigin.BeginTime);story.Pause(intro);typeof(IntroView).GetMethod("Frame",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(intro,[t]);intro.UpdateLayout();
 var backgroundImpact=(FrameworkElement)typeof(IntroView).GetField("stabilizationBackground",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(intro)!;
 var bitmap=new RenderTargetBitmap(1280,800,96,96,PixelFormats.Pbgra32);bitmap.Render(intro);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(System.IO.Path.Combine(dir,$"preview-{frame:0000}.png"));png.Save(file);
 }
 story.Stop(intro);int completed=0;intro.Completed+=()=>completed++;intro.Finish();intro.Finish();if(completed!=1)throw new Exception("Skip completion failed");
 var handoff=new FakeLanding();var handed=new IntroView(false,true,handoff);handed.Measure(new Size(1280,800));handed.Arrange(new Rect(0,0,1280,800));handed.UpdateLayout();
 foreach(var at in new[]{6.0,6.4,6.85})typeof(IntroView).GetMethod("Frame",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(handed,[at]);
 handed.Finish();if(handoff.BeginCount!=1 || handoff.EndCount!=1 || handoff.Brand<.99)throw new Exception("Header handoff did not complete");
 var reduced=new IntroView(true,true);reduced.Measure(new Size(1280,800));reduced.Arrange(new Rect(0,0,1280,800));typeof(IntroView).GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(reduced,null);if(((FrameworkElement)impactField.GetValue(reduced)!).Visibility!=Visibility.Collapsed)throw new Exception("Reduced motion still shows impact");reduced.Finish();
 var model=new X20Ctl.Zone.ControllerModel("x20_pro","EasySMX X20 Pro","","","",new Dictionary<string,X20Ctl.Zone.ControlPoint>());
 var game=new GameEntry("fixture","Fixture game","Test",null,null,null,null,"fixture://never-launch");
 var page=new DashboardConsolePage(model,0,4,2,true);
 var previewField=typeof(DashboardConsolePage).GetField("preview",BindingFlags.Instance|BindingFlags.NonPublic)!; var previewObject=(System.Windows.Controls.Grid)previewField.GetValue(page)!; var previewBody=(System.Windows.Controls.Grid)previewObject.Children[0]; if(previewBody.Children.Count!=1)throw new Exception("Dashboard still contains rear artwork / automatic turn");
 var field=typeof(DashboardConsolePage).GetField("scan",BindingFlags.Static|BindingFlags.NonPublic)!;
 field.SetValue(null,Task.FromResult<IReadOnlyList<GameEntry>>(Enumerable.Range(0,18).Select(i=>game with {Id="fixture"+i,Name="Game "+i}).ToArray()));
 var populate=(Task)typeof(DashboardConsolePage).GetMethod("Populate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(page,null)!;populate.GetAwaiter().GetResult();
 foreach(var size in new[]{new Size(1500,740),new Size(1100,520)}) {
 page.Measure(size);page.Arrange(new Rect(size));page.UpdateLayout(); System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle); page.Measure(size);page.Arrange(new Rect(size));page.UpdateLayout();
 var scroll=(System.Windows.Controls.ScrollViewer)typeof(DashboardConsolePage).GetField("shelfScroll",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page)!;var lower=(System.Windows.Controls.Grid)scroll.Parent; var rail=(System.Windows.Controls.StackPanel)scroll.Content; var first=(System.Windows.Controls.Button)rail.Children[0]; var title=(System.Windows.Controls.TextBlock)((System.Windows.Controls.StackPanel)first.Content).Children[1]; if(title.TranslatePoint(new Point(0,title.ActualHeight),scroll).Y>scroll.ActualHeight-18)throw new Exception("border="+((System.Windows.Controls.Border)((System.Windows.Controls.Grid)((System.Windows.Controls.StackPanel)first.Content).Children[0]).Children[0]).Height+" desired="+((System.Windows.Controls.Grid)((System.Windows.Controls.StackPanel)first.Content).Children[0]).DesiredSize+" Game title clipped at "+size+" title="+title.TranslatePoint(new Point(0,title.ActualHeight),scroll).Y+" viewport="+scroll.ActualHeight+" rail="+rail.ActualHeight+" row="+page.RowDefinitions[2].ActualHeight+" art="+((System.Windows.Controls.Grid)((System.Windows.Controls.StackPanel)first.Content).Children[0]).ActualHeight);
 if(lower.TranslatePoint(new Point(0,lower.ActualHeight),page).Y>size.Height+.5)throw new Exception("Dashboard exceeds viewport");
 var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(page);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=System.IO.File.Create(System.IO.Path.Combine(dir,$"dashboard-{size.Width}.png"));png.Save(file);
 }
 var artType=typeof(DashboardConsolePage).Assembly.GetType("X20Ctl.Product.GameArtwork")!;
 var save=artType.GetMethod("Save")!;var pathFor=artType.GetMethod("PathFor")!;
 var isolated=game with {Id="artwork-fixture-"+Guid.NewGuid().ToString("N")};
 var source=System.IO.Path.Combine(dir,"intro-4.80.png");string? stored=null;
 try {
 save.Invoke(null,[isolated,source]);stored=(string)pathFor.Invoke(null,[isolated])!;
 if(!System.IO.File.Exists(stored)||stored==source)throw new Exception("Override not copied");
 if((string)pathFor.Invoke(null,[isolated with{Name="Renamed"}])! != stored)throw new Exception("Name broke identity");
 var before=System.IO.File.ReadAllBytes(stored);
 try {save.Invoke(null,[isolated,System.IO.Path.Combine(dir,"invalid-missing.png")]);throw new Exception("Invalid accepted");}catch(TargetInvocationException){}
 if(!before.SequenceEqual(System.IO.File.ReadAllBytes(stored)))throw new Exception("Failed replacement damaged art");
 } finally {if(stored!=null)System.IO.File.Delete(stored);}
 var previewRear=previewObject.GetType().GetProperty("IsRear")!;
var flipButton=FindFlip(page);
System.Windows.Controls.Button FindFlip(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is System.Windows.Controls.Button button && button.Content is string label && label=="Flip controller")return button;try{return FindFlip(child);}catch(InvalidOperationException){}}throw new InvalidOperationException("Flip controller missing");}
flipButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(!(bool)previewRear.GetValue(previewObject)!)throw new Exception("Manual rear view failed");
flipButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if((bool)previewRear.GetValue(previewObject)!)throw new Exception("Manual front view failed");
Console.WriteLine("PASS: manual Dashboard flip switches rear and back to front; no scheduled turn.");
Console.WriteLine("PASS: artwork copied, stable after rename, failed replacement preserves image.");
 string? loadedId=null;
 var saved=new X20Ctl.Zone.SavedSetup{Id="compatible",Name="Test setup",ModelId="x20_pro",Snapshot=System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone()};
 var setupPage=new DashboardConsolePage(model,0,0,1,true,new[]{saved,new X20Ctl.Zone.SavedSetup{Id="wrong",Name="Wrong model",ModelId="x05",Snapshot=saved.Snapshot}},id=>loadedId=id);
 setupPage.Measure(new Size(1500,740));setupPage.Arrange(new Rect(0,0,1500,740));setupPage.UpdateLayout();
 IEnumerable<DependencyObject> Descend(DependencyObject node){yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Descend(VisualTreeHelper.GetChild(node,i)))yield return child;}
 var chooser=Descend(setupPage).OfType<System.Windows.Controls.ComboBox>().Single();
 if(chooser.Items.Count!=1)throw new Exception("Wrong model setup offered");chooser.SelectedIndex=0;
 var loadButton=Descend(setupPage).OfType<System.Windows.Controls.Button>().Single(b=>b.Content is System.Windows.Controls.TextBlock tb && tb.Text=="Load setup");
 loadButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(loadedId!="compatible")throw new Exception("Load callback missing");
 Console.WriteLine("PASS: setup chooser filters model and invokes selected setup load.");
 Console.WriteLine("PASS: Dashboard populated with 18 games at 1500x740 and 1100x520; lower row stays inside viewport.");
 Console.WriteLine("PASS: isolated intro keyframes; normal/reduced construction; skip completion exactly once; header handoff completes. No window, audio playback or hardware.");var panelType=typeof(X20Ctl.Product.Agent.AgentPanel);
var agent=new X20Ctl.Product.Agent.AgentPanel("x20_pro","EasySMX X20 Pro",null,_=>{},reducedMotion:true);
agent.Body.Width=1400;agent.Body.Height=560;agent.Show();agent.Body.Measure(new Size(1400,560));agent.Body.Arrange(new Rect(0,0,1400,560));agent.Body.UpdateLayout();
if(agent.Body.Child is not System.Windows.Controls.Viewbox)throw new Exception("Agent lost controller silhouette");
agent.ShowSettingsPage(true);if(!agent.SettingsOpen)throw new Exception("Settings did not open");agent.ShowSettingsPage(false);
agent.Input.Text="Retained draft";agent.Hide();agent.Show();if(agent.Input.Text!="Retained draft")throw new Exception("Lost draft");
agent.Input.Text="";agent.Ask("what controller is this").GetAwaiter().GetResult();
var transcriptField=panelType.GetField("transcript",BindingFlags.Instance|BindingFlags.NonPublic)!;
var transcript=(System.Windows.Controls.StackPanel)transcriptField.GetValue(agent)!;
if(transcript.Children.Count<3)throw new Exception("No agent response");
var agentBitmap=new RenderTargetBitmap(1400,560,96,96,PixelFormats.Pbgra32);agentBitmap.Render(agent.Body);var agentPng=new PngBitmapEncoder();agentPng.Frames.Add(BitmapFrame.Create(agentBitmap));using(var agentFile=System.IO.File.Create(System.IO.Path.Combine(dir,"agent-controller.png")))agentPng.Save(agentFile);
panelType.GetMethod("Restart",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(agent,null);if(transcript.Children.Count!=1)throw new Exception("New chat failed");
agent.Hide();if(agent.Body.Visibility!=Visibility.Collapsed)throw new Exception("Reduced-motion close delayed");
Console.WriteLine("PASS: restored controller Agent, settings, retained draft, offline reply, new chat, reduced motion.");
var catalog=new X20Ctl.Zone.ControllerCatalog(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));
var realModel=catalog.Models.First(m=>m.Id=="x20_pro");
var studio=new StudioScene(0,realModel,new X20Ctl.Zone.ButtonDraft(realModel.Id),true);
var headerStatus=(StatusChip)typeof(StudioScene).GetField("controllerConnection",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(studio)!;
var headerStatusLabel=(System.Windows.Controls.TextBlock)((System.Windows.Controls.StackPanel)headerStatus.Child).Children[1];
var titlePills=(System.Windows.Controls.StackPanel)studio.FindName("TitlePills");
if(studio.AgentButton?.Parent!=studio.FindName("AgentTitleHost"))throw new Exception("Agent not beside brand");
if(titlePills.Children.Count!=2 || titlePills.Children[0] is not System.Windows.Controls.Button)throw new Exception("Header does not use one combined device control");
studio.SetGameplayConnected(true);if(headerStatusLabel.Text!="LIVE INPUT")throw new Exception("Header live-input state stale");
studio.SetGameplayConnected(false);if(headerStatusLabel.Text!="OFFLINE")throw new Exception("Header offline state stale");
Console.WriteLine("PASS: canonical header status next to model pill follows actual gameplay connection updates.");
foreach(var width in new[]{1500d,650d,1100d,1500d}){
 studio.Measure(new Size(width,800));studio.Arrange(new Rect(0,0,width,800));studio.UpdateLayout();
 typeof(StudioScene).GetMethod("UpdateNavigation",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(studio,null);
 var nav=(System.Windows.Controls.StackPanel)studio.FindName("NavigationTabs");var layer=(System.Windows.Controls.Grid)studio.FindName("NavigationLayer");
 if(nav.Children.OfType<FrameworkElement>().Where(x=>x.Visibility==Visibility.Visible).Sum(x=>x.DesiredSize.Width)>layer.ActualWidth+.5)throw new Exception("Navigation overflow");
 var more=(System.Windows.Controls.Button)studio.FindName("NavigationMore");
 if(width==650){if(more.ContextMenu!.Items.Count==0)throw new Exception("No overflow destinations studio="+studio.ActualWidth+" layer="+layer.ActualWidth+" nav="+nav.DesiredSize.Width);var menu=(System.Windows.Controls.MenuItem)more.ContextMenu.Items[0];menu.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));if(studio.ManagementPage!="PcMode")throw new Exception("Hidden destination failed");studio.ShowManagement("Dashboard");}
}
var tabPanel=(System.Windows.Controls.StackPanel)studio.FindName("NavigationTabs");
foreach(var name in new[]{"DashboardNav","ButtonsNav","CurvesNav","MacrosNav","VibrationNav","DeviceNav","MouseNav","SetupsNav","PcModeNav"}){
 var destination=(System.Windows.Controls.RadioButton)studio.FindName(name);destination.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.RadioButton.ClickEvent));
 if(!destination.IsChecked.GetValueOrDefault() || tabPanel.Children.OfType<System.Windows.Controls.RadioButton>().Count(tab=>tab.IsChecked==true)!=1)throw new Exception("Glass tab selection failed: "+name);
}
studio.ShowManagement("Dashboard");
var battery=(System.Windows.Controls.TextBlock)typeof(StudioScene).GetField("batteryLabel",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(studio)!;
foreach(var pair in new[]{("full","Full"),("medium","Medium"),("low","Low"),("empty","Empty"),("wired","Wired"),("unknown","Not reported")}){studio.SetBatteryLevel(pair.Item1);if(battery.Text!="Battery · "+pair.Item2)throw new Exception("Battery label incorrect");}
studio.SetBatteryLevel(null);if(battery.Text!="Battery · Not reported")throw new Exception("Missing battery report fabricated");
Console.WriteLine("PASS: every glass tab route select exactly one destination; battery coarse/wired/unavailable states.");
studio.OpenAgent();if(studio.AgentWindow!.Body.Child is not System.Windows.Controls.Viewbox)throw new Exception("Agent controller design replaced by drawer");studio.Measure(new Size(1000,650));studio.Arrange(new Rect(0,0,1000,650));studio.UpdateLayout();
if(studio.AgentWindow!.Body.Height>650||studio.AgentWindow.Body.Width>1000)throw new Exception("Controller Agent resize failed");studio.CloseAgent();
Console.WriteLine("PASS: isolated Studio tab overflow, hidden destination, Dashboard restoration, controller Agent resize.");
var installed=GameLibrary.ScanAsync().GetAwaiter().GetResult();
field.SetValue(null,Task.FromResult<IReadOnlyList<GameEntry>>(installed));
var livePage=new DashboardConsolePage(realModel,0,0,0,true);
((Task)typeof(DashboardConsolePage).GetMethod("Populate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(livePage,null)!).GetAwaiter().GetResult();livePage.SetWorkingState(0,null,true);livePage.SetInputConnected(false);
var liveRoot=new System.Windows.Controls.Grid{Background=new SolidColorBrush(Color.FromRgb(8,14,26))};liveRoot.Children.Add(new FacetBackdrop());liveRoot.Children.Add(livePage);liveRoot.Measure(new Size(1650,850));liveRoot.Arrange(new Rect(0,0,1650,850));liveRoot.UpdateLayout();
var liveBitmap=new RenderTargetBitmap(1650,850,96,96,PixelFormats.Pbgra32);liveBitmap.Render(liveRoot);var livePng=new PngBitmapEncoder();livePng.Frames.Add(BitmapFrame.Create(liveBitmap));using(var liveFile=System.IO.File.Create(System.IO.Path.Combine(dir,"dashboard-current.png")))livePng.Save(liveFile);
var fullStudio=new StudioScene(0,realModel,new X20Ctl.Zone.ButtonDraft(realModel.Id),true);fullStudio.SetGameplayConnected(false);
fullStudio.Measure(new Size(1920,1080));fullStudio.Arrange(new Rect(0,0,1920,1080));fullStudio.UpdateLayout();
var shellBitmap=new RenderTargetBitmap(1920,1080,96,96,PixelFormats.Pbgra32);shellBitmap.Render(fullStudio);var shellPng=new PngBitmapEncoder();shellPng.Frames.Add(BitmapFrame.Create(shellBitmap));using(var shellFile=System.IO.File.Create(System.IO.Path.Combine(dir,"dashboard-panel-header.png")))shellPng.Save(shellFile);
Console.WriteLine("RENDER: local installed-game artwork, no game launched; offline input state set explicitly.");
app.Shutdown();
 }
}












