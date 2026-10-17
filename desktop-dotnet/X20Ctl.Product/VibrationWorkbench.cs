using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>One linked strength; optional region preview never creates independent motor settings.</summary>
public sealed class VibrationWorkbench
{
 public Grid Body {get;}=new(); public Grid Shelf {get;}=new();
 public X20Ctl.Zone.VibrationDraft Draft {get;}
 public Slider Strength {get;}=new(){Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
 public Button Test {get;}
 public TextBlock Readback {get;}=new(){Text="Physical output unavailable",FontSize=14,Foreground=Brushes.LightSteelBlue};
 public VibrationVisual Visualizer {get;}
 public IEnumerable<Button> PresetButtons=>presets;
 public event Action? Changed;
 readonly List<Button> presets=new();readonly Dictionary<string,Button> regionsByName=new();
 public string PreviewRegion {get;private set;}="All";
 public void SelectRegion(string region){PreviewRegion=region;Visualizer.SetRegion(region);foreach(var pair in regionsByName)pair.Value.Foreground=pair.Key==region?new SolidColorBrush(Color.FromRgb(17,24,39)):Brushes.LightSteelBlue;foreach(var pair in regionsByName)pair.Value.Background=pair.Key==region?Brushes.White:DS.Brush("Control.ButtonFill");} readonly TextBlock value=new(){FontSize=54,FontWeight=FontWeights.SemiBold};
 bool updating;int previewStrength;
 public VibrationWorkbench(X20Ctl.Zone.VibrationDraft draft,FrameworkElement resources,Action<Button> register,bool animations,string model="x20")
 {
  Draft=draft;previewStrength=draft.Strength;Body.ColumnDefinitions.Add(new(){Width=new(1.4,GridUnitType.Star)});Body.ColumnDefinitions.Add(new());
  Visualizer=new((Geometry)resources.FindResource("ControllerOutline"),animations,model);Body.Children.Add(new Viewbox{Child=Visualizer,Stretch=Stretch.Uniform});
  var controls=new StackPanel{Margin=new(24,16,20,8),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(controls,1);Body.Children.Add(controls);
  controls.Children.Add(Copy("VIBRATION PREVIEW",14));controls.Children.Add(value);controls.Children.Add(Copy(draft.CanEdit?"Linked grip strength · Local draft":"Visual preview only · Native settings unavailable",16));
  Strength.Style=DS.Style("DS.Slider");Strength.Margin=new(0,18,0,14);controls.Children.Add(Strength);
  var choices=new WrapPanel();controls.Children.Add(choices);foreach(int percent in new[]{0,25,50,75,100}){int p=percent;var b=Button(percent==0?"Off":percent+"%",()=>Set(p));b.Tag=percent;presets.Add(b);choices.Children.Add(b);}
  controls.Children.Remove(choices);controls.Children.Add(Kit.Segments(choices));
  controls.Children.Add(Copy("Preview region",14));var regions=new WrapPanel();controls.Children.Add(regions);
  foreach(var name in new[]{"All","Both grips","Left grip","Right grip"}){string region=name;var b=Button(name,()=>SelectRegion(region));regionsByName[name]=b;regions.Children.Add(b);}
  if(model is "x20_pro" or "dune"){var triggers=Button("Triggers · Visual only",()=>SelectRegion("Triggers"));regionsByName["Triggers"]=triggers;regions.Children.Add(triggers);}
  controls.Children.Remove(regions);controls.Children.Add(Kit.Segments(regions));
  controls.Children.Add(Copy("Region selection changes the illustration only. This backend has one linked strength; independent trigger tuning is unavailable.",14));
  Test=Button("Test on controller",()=>{});Test.IsEnabled=false;Test.ToolTip="Physical testing needs a verified connection and bounded motor command.";ToolTipService.SetShowOnDisabled(Test,true);Test.Margin=new(0,18,0,0);controls.Children.Add(Test);controls.Children.Add(Readback);
  Shelf.Children.Add(Copy("Preview animation · No motor telemetry or physical vibration is generated",14));
  var art=(UIElement)Body.Children[0];Body.Children.Remove(art);var artPanel=Kit.Panel(null,null,null,art);artPanel.Margin=new(0,0,16,0);Body.Children.Add(artPanel);
  Body.Children.Remove(controls);controls.Margin=new(0);var controlPanel=Kit.Panel(null,null,null,new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(controlPanel,1);Body.Children.Add(controlPanel);
  Strength.ValueChanged+=(_,_)=>{if(!updating)Set((int)Strength.Value);};Refresh();SelectRegion("All");
  Button Button(string text,Action action){var b=new Button{Content=text,Style=DS.Style("ActionButton"),Height=42,MinHeight=42,Padding=new(14,0,14,0),Margin=new(0,5,8,5),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>action();register(b);return b;}
 }
 static TextBlock Copy(string text,double size)=>new(){Text=text,FontSize=size,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,8)};
 public void Set(int percent){previewStrength=Math.Clamp(percent,0,100);if(Draft.CanEdit)Draft.Set(previewStrength);Refresh();Changed?.Invoke();}
 public void Reset(){if(Draft.CanEdit)Draft.Reset();previewStrength=Draft.Strength;Refresh();Changed?.Invoke();}
 public void Refresh(){updating=true;if(Draft.CanEdit)previewStrength=Draft.Strength;value.Text=previewStrength+"%";Strength.Value=previewStrength;Visualizer.SetStrength(previewStrength);foreach(var b in presets)b.Foreground=(int)b.Tag==previewStrength?new SolidColorBrush(Color.FromRgb(17,24,39)):Brushes.LightSteelBlue;foreach(var b in presets)b.Background=(int)b.Tag==previewStrength?Brushes.White:DS.Brush("Control.ButtonFill");updating=false;}
 public void FocusPreset()=>presets.OrderBy(b=>Math.Abs((int)b.Tag-previewStrength)).First().Focus();
 public bool HandleKey(KeyEventArgs e){if(Keyboard.FocusedElement!=Strength || e.Key is not(Key.Left or Key.Right))return false;Set(previewStrength+(e.Key==Key.Right?1:-1)*((Keyboard.Modifiers&ModifierKeys.Shift)!=0?5:1));e.Handled=true;return true;}
}
