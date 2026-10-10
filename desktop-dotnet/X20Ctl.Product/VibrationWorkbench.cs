using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product;

/// <summary>Local linked-strength UI; no simulated or real haptic output.</summary>
public sealed class VibrationWorkbench
{
    public Grid Body {get;}=new();
    public Grid Shelf {get;}=new();
    public X20Ctl.Zone.VibrationDraft Draft {get;}
    public Slider Strength {get;}=new(){Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true};
    public Button Test {get;}
    public TextBlock Readback {get;}=new(){Text="Motor 1   —\nMotor 2   —",FontSize=18,Foreground=Brushes.LightSteelBlue,LineHeight=30};
    public VibrationVisual Visualizer {get;}
    private readonly TextBlock focusHint=new(){FontSize=14,Foreground=Brushes.LightSteelBlue,VerticalAlignment=VerticalAlignment.Center};
    public IEnumerable<Button> PresetButtons=>presets;
    public event Action? Changed;
    private readonly List<Button> presets=new();
    private readonly TextBlock value=new(){FontSize=76,FontWeight=FontWeights.SemiBold},sliderValue=new(){FontSize=20,FontWeight=FontWeights.SemiBold};
    private readonly bool animate;
    private readonly Border heavyBar=Bar(Color.FromRgb(80,150,255)),lightBar=Bar(Color.FromRgb(160,215,255));
    private static Border Bar(Color c)=>new(){Height=8,CornerRadius=new(4),HorizontalAlignment=HorizontalAlignment.Left,Width=0,Background=new LinearGradientBrush(Color.FromArgb(200,c.R,c.G,c.B),c,0),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=c,BlurRadius=12,ShadowDepth=0,Opacity=.8}};
    private static StackPanel Meter(string label,Border bar,double top)=>new(){Margin=new(0,top,0,0),Children={new TextBlock{Text=label,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Brushes.LightSteelBlue},new Grid{Width=150,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,6,0,0),Children={new Border{Height=8,CornerRadius=new(4),Background=new SolidColorBrush(Color.FromArgb(40,255,255,255))},bar}}}};
    private bool updating;
    public VibrationWorkbench(X20Ctl.Zone.VibrationDraft draft,FrameworkElement resources,Action<Button> register,bool animations,string model="x20")
    {
        Draft=draft;animate=animations;
        Body.ColumnDefinitions.Add(new(){Width=new GridLength(210)});Body.ColumnDefinitions.Add(new());Body.ColumnDefinitions.Add(new(){Width=new GridLength(210)});
        var strength=new StackPanel {VerticalAlignment=VerticalAlignment.Center};strength.Children.Add(new TextBlock {Text="DRAFT STRENGTH",FontSize=14,Foreground=Brushes.LightSteelBlue});strength.Children.Add(value);strength.Children.Add(new TextBlock {Text="Visual preview",FontSize=20,FontWeight=FontWeights.SemiBold});strength.Children.Add(new TextBlock {Text="Linked strength · Both grips\nPreview only, no physical output.",FontSize=15,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0)});Body.Children.Add(strength);
        // the real layered controller rumbles in place; the meters show the preview pattern driving each motor
        Visualizer=new((Geometry)resources.FindResource("ControllerOutline"),animate,model);
        strength.Children.Add(Meter("LEFT MOTOR · HEAVY",heavyBar,20));strength.Children.Add(Meter("RIGHT MOTOR · LIGHT",lightBar,10));
        Visualizer.Levels+=(h,l)=>{heavyBar.Width=150*h;lightBar.Width=150*l;};
        var controller=new Viewbox {Child=Visualizer,Stretch=Stretch.Uniform,Margin=new(-8,0,-8,0),IsHitTestVisible=false};Grid.SetColumn(controller,1);Body.Children.Add(controller);
        var hardware=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new(18,0,0,0)};Grid.SetColumn(hardware,2);Body.Children.Add(hardware);hardware.Children.Add(new TextBlock {Text="HARDWARE STATUS",FontSize=14,Foreground=Brushes.LightSteelBlue});hardware.Children.Add(new TextBlock {Text="Offline",FontSize=28,FontWeight=FontWeights.SemiBold,Margin=new(0,5,0,22)});hardware.Children.Add(new TextBlock {Text="Current motor values unavailable",FontSize=15,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap});hardware.Children.Add(Readback);
        Test=new Button {Content="Test vibration",Style=(Style)resources.FindResource("ConsoleAction"),IsEnabled=false,Margin=new(0,18,0,0),ToolTip="Unavailable until the native engine connects and authorizes a bounded vibration test."};ToolTipService.SetShowOnDisabled(Test,true);hardware.Children.Add(Test);
        Shelf.RowDefinitions.Add(new(){Height=new GridLength(70)});Shelf.RowDefinitions.Add(new());
        var choices=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Shelf.Children.Add(choices);
        foreach(int percent in new[]{0,25,50,75,100})
        {
            var button=new Button {Content=percent==0?"Off":percent+"%",Tag=percent,Width=112,Height=46,Margin=new(10,0,10,0),Style=(Style)resources.FindResource("ConsoleTarget"),IsEnabled=Draft.CanEdit};button.Click+=(_,_)=>Set(percent);button.GotKeyboardFocus+=(_,_)=>UpdateFocusHint();button.LostKeyboardFocus+=(_,_)=>UpdateFocusHint();register(button);presets.Add(button);choices.Children.Add(button);
        }
        var adjust=new Grid {Margin=new(16,0,16,9)};adjust.RowDefinitions.Add(new(){Height=new GridLength(26)});adjust.RowDefinitions.Add(new());adjust.RowDefinitions.Add(new(){Height=new GridLength(22)});Grid.SetRow(adjust,1);Shelf.Children.Add(adjust);
        adjust.Children.Add(sliderValue);Strength.Style=(Style)resources.FindResource("ConsoleCurveSlider");Strength.IsEnabled=Draft.CanEdit;Strength.Margin=new(0,5,0,0);Grid.SetRow(Strength,1);adjust.Children.Add(Strength);System.Windows.Automation.AutomationProperties.SetName(Strength,"Local linked vibration strength percent");
        Grid.SetRow(focusHint,2);adjust.Children.Add(focusHint);
        Strength.ValueChanged+=(_,_)=>{if(!updating)Set((int)Strength.Value);};Refresh();
    }
    public void Set(int percent){Draft.Set(percent);Refresh();if(animate)value.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(160)));Changed?.Invoke();}
    public void Reset(){Draft.Reset();Refresh();Changed?.Invoke();}
    public void Refresh(){updating=true;value.Text=Draft.Strength+"%";sliderValue.Text="Selected strength   "+Draft.Strength+"%";Strength.Value=Draft.Strength;Visualizer.SetStrength(Draft.Strength);foreach(var button in presets)button.Background=new SolidColorBrush((int)button.Tag==Draft.Strength?Color.FromRgb(38,120,237):Color.FromArgb(90,24,35,55));UpdateFocusHint();updating=false;}
    private void UpdateFocusHint(){var focused=presets.FirstOrDefault(b=>b.IsKeyboardFocused);focusHint.Text=focused!=null && (int)focused.Tag!=Draft.Strength?$"Focus: {focused.Content} · A selects   |   Current draft: {Draft.Strength}%":"← / →  1% steps · Shift  5% steps   |   Visual preview only";}
    public void FocusPreset(){presets.OrderBy(b=>Math.Abs((int)b.Tag-Draft.Strength)).First().Focus();}
    public bool HandleKey(KeyEventArgs e){if(Keyboard.FocusedElement!=Strength || e.Key is not (Key.Left or Key.Right))return false;int step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?5:1;Set(Math.Clamp(Draft.Strength+(e.Key==Key.Right?step:-step),0,100));e.Handled=true;return true;}
}
