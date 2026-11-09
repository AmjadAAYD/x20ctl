using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Product.Scanner;
namespace X20Ctl.Product;

/// <summary>Vibration: a zone per motor (grips, and triggers where the pad has them) to feel each one; in PC mode also the
/// strength games vibrate the pad at (up to 200%), each grip's share, a minimum for faint effects and live game meters.</summary>
public sealed class VibrationWorkbench
{
 public Grid Body {get;}=new(); public Grid Shelf {get;}=new();
 public X20Ctl.Zone.VibrationDraft Draft {get;}
 public Slider Strength {get;}=new(){Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
 public Button Test {get;}
 public TextBlock Readback {get;}=new(){FontSize=14,Foreground=Brushes.LightSteelBlue};
 public VibrationVisual Visualizer {get;}
 public IEnumerable<Button> PresetButtons=>presets;
 public event Action? Changed;
 readonly List<Button> presets=new();readonly Dictionary<string,Button> regionsByName=new();
 public string PreviewRegion {get;private set;}="All";
 public void SelectRegion(string region){PreviewRegion=region;Visualizer.SetRegion(region);foreach(var pair in regionsByName)pair.Value.Foreground=pair.Key==region?new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(17,24,39))):Brushes.LightSteelBlue;foreach(var pair in regionsByName)pair.Value.Background=pair.Key==region?Brushes.White:DS.Brush("Control.ButtonFill");} readonly TextBlock value=new(){FontSize=54,FontWeight=FontWeights.SemiBold};
 bool updating;int previewStrength;
 /// <summary>The pad has trigger motors (X20 Pro, Dune): the tab offers four zones, else the two grips.</summary>
 public bool HasTriggers {get;private set;}
 public VibrationWorkbench(X20Ctl.Zone.VibrationDraft draft,FrameworkElement resources,Action<Button> register,bool animations,string model="x20")
 {
  Draft=draft;previewStrength=draft.Strength;HasTriggers=ModelProfiles.For(model).Motors.Contains("triggers");Body.ColumnDefinitions.Add(new(){Width=new(1.4,GridUnitType.Star)});Body.ColumnDefinitions.Add(new());
  Visualizer=new((Geometry)resources.FindResource("ControllerOutline"),animations,model);Body.Children.Add(new Viewbox{Child=Visualizer,Stretch=Stretch.Uniform});
  var controls=new StackPanel{Margin=new(24,16,20,8),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(controls,1);Body.Children.Add(controls);
  controls.Children.Add(Copy("VIBRATION",14));controls.Children.Add(value);controls.Children.Add(Copy(draft.PcMode?"How strongly games vibrate it (PC mode)":"Test strength",16));
  Strength.Maximum=draft.Max;Strength.Style=DS.Style("DS.Slider");Strength.Margin=new(0,18,0,14);controls.Children.Add(Strength);
  var choices=new WrapPanel();controls.Children.Add(choices);foreach(int percent in draft.PcMode?new[]{0,50,100,150,200}:new[]{0,25,50,75,100}){int p=percent;var b=Button(percent==0?"Off":percent+"%",()=>Set(p));b.Tag=percent;presets.Add(b);choices.Children.Add(b);}
  controls.Children.Remove(choices);controls.Children.Add(Kit.Segments(choices));
  // Owner, 9 Nov 2026: "remove the main slider". Outside PC mode it was only a test strength, which the four zones' own
  // sliders now cover; in PC mode it stays, because there it's the saved strength games vibrate the pad at.
  if(!draft.PcMode){var segments=controls.Children[^1];foreach(var gone in new UIElement[]{value,controls.Children[2],Strength,segments})controls.Children.Remove(gone);}
  controls.Children.Add(Copy(draft.PcMode?"ZONES · each grip's share of the game's rumble · click one to feel it":"ZONES · click one to feel it",14));controls.Children.Add(BuildZones(Button));
  Test=Button(HasTriggers?"Test all four":"Test both grips",()=>_=RunAll());Test.IsEnabled=!ReviewSandbox.Active;Test.Margin=new(0,12,0,0);controls.Children.Add(Test);
  if(HasTriggers&&Diagnostics.BrowserTriggerRumble.Shared is {} shared)
  {
   void Armed()=>Test.Dispatcher.BeginInvoke(ShowTriggerState);
   Body.Loaded+=(_,_)=>{shared.ArmedChanged-=Armed;shared.ArmedChanged+=Armed;_=shared.Ready();};Body.Unloaded+=(_,_)=>shared.ArmedChanged-=Armed;
  }
  ShowTriggerState();
  controls.Children.Add(Readback);if(draft.PcMode)controls.Children.Add(BuildGame());
  Shelf.Children.Add(Copy("Click a zone to feel it at its own strength",14));
  var art=(UIElement)Body.Children[0];Body.Children.Remove(art);var artPanel=Kit.Panel(null,null,null,art);artPanel.Margin=new(0,0,16,0);Body.Children.Add(artPanel);
  Body.Children.Remove(controls);controls.Margin=new(0);var controlPanel=Kit.Panel(null,null,null,new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(controlPanel,1);Body.Children.Add(controlPanel);
  Strength.ValueChanged+=(_,_)=>{if(!updating)Set((int)Strength.Value);};Refresh();SelectRegion("All");
  Button Button(string text,Action action){var b=new Button{Content=text,Style=DS.Style("ActionButton"),Height=42,MinHeight=42,Padding=new(14,0,14,0),Margin=new(0,5,8,5),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>action();register(b);return b;}
 }
 // Four zones (owner, 9 Nov 2026: "make it 4 zones, left grip, right grip, left trigger, right trigger; clicking one
 // vibrates it and each has a slider"). Each slider is that zone's test strength; the grips go through XInput, the triggers
 // through the built-in browser (the one route that reaches the trigger motors), which needs one controller button press
 // after X20CTL opens before it can see the pad.
 public enum Zone{LeftTrigger,RightTrigger,LeftGrip,RightGrip}
 static readonly (Zone Zone,string Name)[] zoneNames={(Zone.LeftTrigger,"Left trigger"),(Zone.RightTrigger,"Right trigger"),(Zone.LeftGrip,"Left grip"),(Zone.RightGrip,"Right grip")};
 readonly Dictionary<Zone,(Button Face,Slider Slider,TextBlock Value)> zones=new();
 public int ZoneStrength(Zone zone)=>zones.TryGetValue(zone,out var z)?(Draft.PcMode&&!IsTrigger(zone)?Math.Min(100,(int)z.Slider.Value*previewStrength/100):(int)z.Slider.Value):0;
 public Button? ZoneButton(Zone zone)=>zones.TryGetValue(zone,out var z)?z.Face:null;
 public Slider? ZoneSlider(Zone zone)=>zones.TryGetValue(zone,out var z)?z.Slider:null;
 static bool IsTrigger(Zone zone)=>zone is Zone.LeftTrigger or Zone.RightTrigger;
 UIElement BuildZones(Func<string,Action,Button> button)
 {
  var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new(0,0,0,4)};
  foreach(var (zone,name) in zoneNames.Where(n=>HasTriggers||!IsTrigger(n.Zone)))
  {
   var z=zone;var card=new StackPanel{Margin=new(0,0,10,10)};
   var head=new Grid();head.ColumnDefinitions.Add(new());head.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
   var face=button(name,()=>_=RunZone(z));face.IsEnabled=!ReviewSandbox.Active;face.Margin=new(0,0,8,0);head.Children.Add(face);
   var value=new TextBlock{FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(value,1);head.Children.Add(value);
   // PC mode: a grip's slider is that motor's saved share of the game's rumble, up to 200%; otherwise a test strength
   bool share=Draft.PcMode&&!IsTrigger(z);
   var slider=new Slider{Minimum=0,Maximum=share?Draft.Max:100,TickFrequency=5,IsSnapToTickEnabled=true,Value=share?(z==Zone.LeftGrip?Draft.Left:Draft.Right):75,Style=DS.Style("DS.Slider"),Margin=new(0,6,0,0)};
   slider.ValueChanged+=(_,_)=>{value.Text=(int)slider.Value+"%";if(share&&!updating){int v=(int)slider.Value;if(z==Zone.LeftGrip)Draft.SetMotors(v,Draft.Right,Draft.Floor);else Draft.SetMotors(Draft.Left,v,Draft.Floor);Changed?.Invoke();}};value.Text=(int)slider.Value+"%";
   card.Children.Add(head);card.Children.Add(slider);grid.Children.Add(card);zones[z]=(face,slider,value);
  }
  return grid;
 }
 async Task RunZone(Zone zone)
 {
  int percent=ZoneStrength(zone);string name=zoneNames.First(n=>n.Zone==zone).Name;
  if(zone is Zone.LeftGrip or Zone.RightGrip)SelectRegion(name);
  if(percent==0){Readback.Text=name+" is at 0%.";return;}
  var face=zones[zone].Face;face.IsEnabled=false;
  try
  {
   if(IsTrigger(zone))
   {
    var shared=Diagnostics.BrowserTriggerRumble.Shared;
    if(shared==null){Readback.Text="Trigger rumble isn't available.";return;}
    if(!shared.Armed){await shared.Listen();if(shared.Failure!=null){Readback.Text=shared.Failure;return;}Readback.Text="Press any button on the controller to turn on trigger rumble, then click the trigger again.";return;}
    Readback.Text=$"Buzzing the {name.ToLowerInvariant()} at {percent}%…";
    bool sent=await shared.Play(zone==Zone.LeftTrigger?percent/100.0:0,zone==Zone.RightTrigger?percent/100.0:0,1000);
    await Task.Delay(1000);
    Readback.Text=sent?$"{name} buzzed at {percent}%.":"The controller isn't reachable for trigger rumble right now.";
   }
   else
   {
    Readback.Text=$"Buzzing the {name.ToLowerInvariant()} at {percent}%…";
    bool sent=await ControllerRumble.Test(zone==Zone.LeftGrip?percent:0,zone==Zone.RightGrip?percent:0);
    Readback.Text=sent?$"{name} buzzed at {percent}%.":"No controller found.";
   }
  }
  finally{face.IsEnabled=!ReviewSandbox.Active;}
 }
 /// <summary>All four at once, each at its own strength (the triggers only once trigger rumble is on).</summary>
 async Task RunAll()
 {
  SelectRegion("All");Test.IsEnabled=false;Readback.Text=HasTriggers?"Buzzing all four…":"Buzzing both grips…";
  var shared=Diagnostics.BrowserTriggerRumble.Shared;bool triggers=HasTriggers&&shared?.Armed==true;
  if(triggers)_=shared!.Play(ZoneStrength(Zone.LeftTrigger)/100.0,ZoneStrength(Zone.RightTrigger)/100.0,1000);
  bool sent=await ControllerRumble.Test(ZoneStrength(Zone.LeftGrip),ZoneStrength(Zone.RightGrip));
  Test.IsEnabled=true;
  Readback.Text=!sent?"No controller found.":!HasTriggers?"Both grips buzzed.":triggers?"All four buzzed.":"The grips buzzed. Click a trigger to turn on trigger rumble.";
 }
 void ShowTriggerState()
 {
  bool armed=Diagnostics.BrowserTriggerRumble.Shared?.Armed==true;
  if(!HasTriggers)return;
  foreach(var zone in new[]{Zone.LeftTrigger,Zone.RightTrigger})zones[zone].Face.ToolTip=armed?null:"Trigger rumble turns on after one button press on the controller";
  if(armed&&Readback.Text.StartsWith("Press any button on the controller"))Readback.Text="Trigger rumble is on. Click a trigger.";
 }
 // Game vibration (PC mode): each motor's share of the game's rumble, a minimum for faint effects, and a live readout
 readonly List<(Slider Slider,TextBlock Label,Func<int> Get,string Unit)> motors=new();
 readonly System.Windows.Threading.DispatcherTimer live=new(){Interval=TimeSpan.FromMilliseconds(100)};
 public TextBlock LiveText {get;}=new(){FontSize=14,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,Margin=new(0,4,0,8)};
 readonly Border[] liveBars=new Border[4];
 public IReadOnlyList<Slider> MotorSliders=>motors.Select(m=>m.Slider).ToList();
 UIElement BuildGame()
 {
  var panel=new StackPanel{Margin=new(0,26,0,0)};
  panel.Children.Add(Copy("GAME VIBRATION",14));
  panel.Children.Add(Copy("What the game asks for, and what your controller gets.",14));
  string[] names={"Left motor · game","Left motor · controller","Right motor · game","Right motor · controller"};
  for(int i=0;i<4;i++)
  {
   var row=new Grid{Margin=new(0,3,0,3)};row.ColumnDefinitions.Add(new(){Width=new(170)});row.ColumnDefinitions.Add(new());
   row.Children.Add(new TextBlock{Text=names[i],FontSize=13,Foreground=Brushes.LightSteelBlue,VerticalAlignment=VerticalAlignment.Center});
   var track=new Border{Height=10,CornerRadius=new(5),Background=DS.Brush("Control.ButtonFill"),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(track,1);
   var bar=new Border{Height=10,CornerRadius=new(5),Width=0,HorizontalAlignment=HorizontalAlignment.Left,Background=i%2==0?Brushes.LightSteelBlue:new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(90,170,255)))};
   track.Child=bar;liveBars[i]=bar;row.Children.Add(track);panel.Children.Add(row);
  }
  panel.Children.Add(LiveText);
  Motor(panel,"Minimum for faint effects",0,X20Ctl.Zone.PcMode.PcModeProfile.MaxVibrationFloor,"%",()=>Draft.Floor,v=>Draft.SetMotors(Draft.Left,Draft.Right,v));
  panel.Children.Add(Copy("Above 100% weak effects get stronger; the strongest stay at full power.",13));
  live.Tick+=(_,_)=>ShowLive();
  Body.IsVisibleChanged+=(_,_)=>{if(Body.IsVisible&&!ReviewSandbox.Active)live.Start();else live.Stop();};
  Body.Unloaded+=(_,_)=>live.Stop();
  ShowLive();
  return panel;
 }
 void Motor(Panel panel,string name,int min,int max,string unit,Func<int> get,Action<int> set)
 {
  var head=new Grid{Margin=new(0,12,0,0)};head.ColumnDefinitions.Add(new());head.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
  head.Children.Add(new TextBlock{Text=name,FontSize=14,Foreground=Brushes.LightSteelBlue});
  var label=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White};Grid.SetColumn(label,1);head.Children.Add(label);panel.Children.Add(head);
  var slider=new Slider{Minimum=min,Maximum=max,TickFrequency=1,IsSnapToTickEnabled=true,Style=DS.Style("DS.Slider"),Margin=new(0,6,0,0),Value=get()};
  slider.ValueChanged+=(_,_)=>{if(updating)return;set((int)slider.Value);label.Text=get()+unit;Changed?.Invoke();};
  label.Text=get()+unit;motors.Add((slider,label,get,unit));panel.Children.Add(slider);
 }
 /// <summary>Shows the newest game vibration; after two quiet seconds it says so.</summary>
 public void ShowLive()
 {
  var s=GameRumble.Latest;bool fresh=s!=null&&Environment.TickCount64-s.At<2000&&(s.GameLeft>0||s.GameRight>0);
  int[] percent=fresh?new[]{s!.GameLeft*100/255,s.PadLeft*100/65535,s.GameRight*100/255,s.PadRight*100/65535}:new int[4];
  for(int i=0;i<4;i++)if(liveBars[i]?.Parent is Border track)liveBars[i].Width=Math.Max(0,track.ActualWidth)*percent[i]/100;
  LiveText.Text=fresh?$"Game {percent[0]}% / {percent[2]}% → controller {percent[1]}% / {percent[3]}%":"Play a game with PC mode on and its vibration shows here.";
 }
 static TextBlock Copy(string text,double size)=>new(){Text=text,FontSize=size,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};
 public void Set(int percent){previewStrength=Math.Clamp(percent,0,Draft.Max);if(Draft.CanEdit)Draft.Set(previewStrength);Refresh();Changed?.Invoke();}
 public void Reset(){if(Draft.CanEdit)Draft.Reset();previewStrength=Draft.Strength;Refresh();Changed?.Invoke();}
 public void Refresh(){updating=true;if(Draft.CanEdit)previewStrength=Draft.Strength;value.Text=previewStrength+"%";Strength.Value=previewStrength;Visualizer.SetStrength(Math.Min(100,previewStrength));foreach(var (slider,label,get,unit) in motors){slider.Value=get();label.Text=get()+unit;}if(Draft.PcMode){if(ZoneSlider(Zone.LeftGrip) is {} l)l.Value=Draft.Left;if(ZoneSlider(Zone.RightGrip) is {} r)r.Value=Draft.Right;}foreach(var b in presets)b.Foreground=(int)b.Tag==previewStrength?new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(17,24,39))):Brushes.LightSteelBlue;foreach(var b in presets)b.Background=(int)b.Tag==previewStrength?Brushes.White:DS.Brush("Control.ButtonFill");updating=false;}
 public void FocusPreset()=>presets.OrderBy(b=>Math.Abs((int)b.Tag-previewStrength)).First().Focus();
 public bool HandleKey(KeyEventArgs e){if(Keyboard.FocusedElement!=Strength || e.Key is not(Key.Left or Key.Right))return false;Set(previewStrength+(e.Key==Key.Right?1:-1)*((Keyboard.Modifiers&ModifierKeys.Shift)!=0?5:1));e.Handled=true;return true;}
}
