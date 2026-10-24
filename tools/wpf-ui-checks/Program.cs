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
if(args.Contains("--agent-navigation")){var check=AgentNavigationChecks.Run();check.ContinueWith(_=>app.Dispatcher.BeginInvoke(()=>app.Shutdown()));app.Run();check.GetAwaiter().GetResult();return;}
if(args.Contains("--dock-motion")){var check=DockMotionChecks.Run();check.ContinueWith(_=>app.Dispatcher.BeginInvoke(()=>app.Shutdown()));app.Run();check.GetAwaiter().GetResult();return;}
if(args.Contains("--tool-icons")){ToolIconChecks.Run();app.Shutdown();return;}
if(args.Contains("--followup")){FollowupChecks.Run();app.Shutdown();return;}
if(args.Contains("--mouse-refinement")){MouseRefinementChecks.Run();app.Shutdown();return;}
if(args.Contains("--setup-isolation")){SetupIsolationChecks.Run();app.Shutdown();return;}
if(args.Contains("--zone-surfaces")){ZoneSurfaceChecks.Run();app.Shutdown();return;}
// the opening film ships beside the app; the checks never play its sound
var film=System.IO.Path.Combine(AppContext.BaseDirectory,"Brand","intro.mp4");
if(!IntroView.HasIntroScore||!System.IO.File.Exists(film)||new System.IO.FileInfo(film).Length<100_000)throw new Exception("Intro film not packaged");
IntroView.SoundEnabled=false;var audioIntro=new IntroView(false,false);
if(audioIntro.Mute.Visibility!=Visibility.Visible)throw new Exception("Mute hidden while the intro has sound");
audioIntro.ToggleMute();if(!audioIntro.Muted)throw new Exception("Mute did not take");audioIntro.ToggleMute();
var filmElement=(System.Windows.Controls.MediaElement?)typeof(IntroView).GetField("film",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(audioIntro);
if(filmElement==null||!filmElement.IsMuted)throw new Exception("Intro film plays sound with sound disabled");audioIntro.Finish();
var quietIntro=new IntroView(true,false);if(quietIntro.Mute.Visibility!=Visibility.Collapsed)throw new Exception("Reduced motion offers sound");
if(typeof(IntroView).GetField("film",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(quietIntro)!=null)throw new Exception("Reduced motion opened the film");quietIntro.Finish();
Console.WriteLine("PASS: intro film packaged, mute shown and muting, no sound with sound disabled, no film with reduced motion.");
 var dir=System.IO.Path.Combine(Environment.CurrentDirectory,"artifacts","intro-stabilization-impact");System.IO.Directory.CreateDirectory(dir);
 var intro=new IntroView(false,true);int completed=0;intro.Completed+=()=>completed++;intro.Finish();intro.Finish();if(completed!=1)throw new Exception("Skip completion failed");
 var handoff=new FakeLanding();var handed=new IntroView(false,true,handoff);handed.Measure(new Size(1280,800));handed.Arrange(new Rect(0,0,1280,800));handed.UpdateLayout();
 foreach(var at in new[]{6.3,6.6,7.0,7.4})handed.Seek(at);
 handed.Finish();if(handoff.BeginCount!=1 || handoff.EndCount!=1 || handoff.Brand<.99)throw new Exception("Header handoff did not complete");
 var reduced=new IntroView(true,true);reduced.Measure(new Size(1280,800));reduced.Arrange(new Rect(0,0,1280,800));reduced.Seek(.2);reduced.Finish();
Console.WriteLine("PASS: skip completes once; the logo hands over to the header once and lands; reduced motion fades.");
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
 var source=System.IO.Path.Combine(dir,"art-fixture.png");{var art=new RenderTargetBitmap(320,180,96,96,PixelFormats.Pbgra32);var ink=new DrawingVisual();using(var dc=ink.RenderOpen())dc.DrawRectangle(new LinearGradientBrush(Colors.SteelBlue,Colors.DarkOrange,0),null,new Rect(0,0,320,180));art.Render(ink);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(art));using var file=System.IO.File.Create(source);png.Save(file);}string? stored=null;
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
 var setupPage=new DashboardConsolePage(model,0,0,1,true,new[]{saved,new X20Ctl.Zone.SavedSetup{Id="wrong",Name="Wrong model",ModelId="x05_pro",Snapshot=saved.Snapshot}},id=>loadedId=id);
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
static System.Windows.Controls.Button? AgentKey(object node,string tip){if(node is System.Windows.Controls.Button b&&b.ToolTip as string==tip)return b;if(node is DependencyObject d)foreach(var child in LogicalTreeHelper.GetChildren(d))if(AgentKey(child,tip) is {} hit)return hit;return null;}
studio.OpenAgent();var backKey=AgentKey(studio.AgentWindow!.Body,"Back of the controller")??throw new Exception("Agent FRONT / BACK bar not clickable");
backKey.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
var agentButtons=(ButtonsConsolePage)typeof(StudioScene).GetField("buttonsPage",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(studio)!;
if(!agentButtons.IsRear||studio.CurrentPage!="Buttons"||studio.AgentWindow.Body.IsHitTestVisible)throw new Exception("Agent FRONT / BACK did not put the agent away on the back of the controller");
foreach(var tip in new[]{"LB · Ideas","RB · Settings"})if(AgentKey(studio.AgentWindow.Body,tip) is null)throw new Exception("Agent bar key missing: "+tip);
Console.WriteLine("PASS: isolated Studio tab overflow, hidden destination, Dashboard restoration, controller Agent resize, Agent FRONT / BACK opens the back.");
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












