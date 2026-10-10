using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product;

internal static class ManagementVisuals
{
    public static Viewbox Artwork(ControllerModel model,FrameworkElement resources)
    {
        // the art carries its own alpha now; a geometry clip only re-roughened its edge, so the outline frames it instead
        var image=new Image {Source=ControllerArt.Front(model.Id),Stretch=Stretch.Fill};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);var outline=new ControllerOutline();outline.Show(model.Id,false);ControllerOutline.Trim(image,model.Id,false);return new(){Child=new Grid {Width=1536,Height=1024,Children={image,outline}},Stretch=Stretch.Uniform,IsHitTestVisible=false};
    }
    public static TextBlock Copy(string text,double size=16,bool strong=false)=>new(){Text=text,FontSize=size,FontWeight=strong?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Foreground=strong?Brushes.White:Brushes.LightSteelBlue};
    public static Button Action(FrameworkElement resources,string text,Action action,Action<Button> register){var button=new Button {Content=text,Style=(Style)resources.FindResource("ConsoleAction"),Margin=new(0,0,12,0)};button.Click+=(_,_)=>action();register(button);return button;}
}
public sealed class DeviceWorkbench
{
    public Grid Body {get;}=new();public Grid Shelf {get;}=new();public DeviceOverview State {get;}
    public Button OpenTester {get;}public Button OpenSetups {get;}
    private readonly TextBlock engineStatus=ManagementVisuals.Copy("Native core not connected",14);
    public void UpdateEngineStatus(string status)=>engineStatus.Text=status;
    public DeviceWorkbench(ControllerModel model,int player,FrameworkElement resources,Action<Button> register,Action tester,Action setups)
    {
        State=new(model.Id,player);Body.ColumnDefinitions.Add(new(){Width=new GridLength(1.35,GridUnitType.Star)});Body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var hardware=new Grid();hardware.RowDefinitions.Add(new(){Height=new GridLength(66)});hardware.RowDefinitions.Add(new());var modelTitle=new StackPanel();modelTitle.Children.Add(ManagementVisuals.Copy(model.Name,30,true));modelTitle.Children.Add(ManagementVisuals.Copy($"Player {player+1} · Assigned model",16));hardware.Children.Add(modelTitle);var art=ManagementVisuals.Artwork(model,resources);Grid.SetRow(art,1);hardware.Children.Add(art);Body.Children.Add(hardware);
        // the right side keeps only what describes the pad itself (owner direction 10 Oct 2026): the battery, then
        // firmware, hardware revision and transport on one line; the connections and support moved down to the shelf
        var profile=ModelProfiles.For(model.Id);
        var overview=new StackPanel {Margin=new(30,0,10,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(overview,1);Body.Children.Add(overview);
        overview.Children.Add(BatteryCard(profile));
        var line=new WrapPanel {Margin=new(4,22,0,0)};overview.Children.Add(line);
        foreach(var (icon,name) in new[]{("","Firmware"),("","Hardware revision"),("","Transport")})
        {
            if(line.Children.Count>0)line.Children.Add(new Border {Width=1,Height=34,Margin=new(18,0,18,0),Background=new SolidColorBrush(Color.FromArgb(50,160,190,235)),VerticalAlignment=VerticalAlignment.Center});
            line.Children.Add(new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Children={new TextBlock {Text=icon,FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=18,Foreground=new SolidColorBrush(Color.FromRgb(125,182,255)),Margin=new(0,0,10,0),VerticalAlignment=VerticalAlignment.Center},
                new StackPanel {Children={ManagementVisuals.Copy(name,13),ManagementVisuals.Copy("Unavailable",16,true)}}}});
        }
        Shelf.ColumnDefinitions.Add(new());Shelf.ColumnDefinitions.Add(new());Shelf.ColumnDefinitions.Add(new(){Width=new GridLength(1.5,GridUnitType.Star)});Shelf.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        int column=0;
        foreach(var (label,value,message) in new[]{("GAMEPLAY CONNECTION","Offline","No gameplay source connected"),("CONFIGURATION CONNECTION","Offline","No configuration connection available"),
            ("SUPPORTED BY X20CTL",profile.Verified?"Buttons · Curves · Macros · Vibration":"Preview",profile.Verified?"X20 reference support · Hardware changes unavailable offline":$"Read-only placeholders until verified · {profile.Summary}")})
        {
            var group=new StackPanel {Margin=new(12,14,18,0)};group.Children.Add(ManagementVisuals.Copy(label,13));group.Children.Add(ManagementVisuals.Copy(value,22,true));group.Children.Add(ManagementVisuals.Copy(message,14));
            Grid.SetColumn(group,column++);Shelf.Children.Add(group);
        }
        var controls=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,12,0)};Grid.SetColumn(controls,3);Shelf.Children.Add(controls);
        OpenTester=ManagementVisuals.Action(resources,"Open Tester",tester,register);OpenSetups=ManagementVisuals.Action(resources,"My setups",setups,register);controls.Children.Add(OpenTester);controls.Children.Add(OpenSetups);
    }
    /// <summary>The battery leads the page. The X20 reports four levels, drawn as four cells; models whose steps are unknown get one bar.</summary>
    private static Border BatteryCard(ModelProfile profile)
    {
        var cells=new System.Windows.Controls.Primitives.UniformGrid {Rows=1,Height=26,Margin=new(0,14,0,10)};
        int count=profile.BatteryLevels>0?profile.BatteryLevels:1;
        for(int i=0;i<count;i++)cells.Children.Add(new Border {CornerRadius=new(6),Margin=new(i==0?0:4,0,i==count-1?0:4,0),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(70,140,180,255)),Background=new SolidColorBrush(Color.FromArgb(34,125,182,255))});
        var head=new DockPanel();
        head.Children.Add(new TextBlock {Text="",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=24,Foreground=new SolidColorBrush(Color.FromRgb(125,182,255)),Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center});
        var state=ManagementVisuals.Copy("Unavailable",15);state.HorizontalAlignment=HorizontalAlignment.Right;state.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(state,Dock.Right);head.Children.Add(state);
        head.Children.Add(new StackPanel {Children={ManagementVisuals.Copy("BATTERY",13),ManagementVisuals.Copy(profile.BatteryLevels>0?$"{profile.BatteryLevels}-level report":profile.Battery=="Unknown"?"Level steps unknown":profile.Battery,20,true)}});
        return new Border {CornerRadius=new(16),Padding=new(20,16,20,14),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(60,140,180,255)),
            Background=new LinearGradientBrush(Color.FromArgb(160,22,36,72),Color.FromArgb(120,10,18,40),90),
            Child=new StackPanel {Children={head,cells,ManagementVisuals.Copy("Charge shows once the controller is connected. Offline, nothing is guessed.",13.5)}}};
    }
}
public sealed class TesterWorkbench
{
    public Grid Body {get;}=new();public Grid Shelf {get;}=new();public TesterState State {get;}
    public Button Exit {get;}public TextBlock SourceLabel {get;}=ManagementVisuals.Copy("Gameplay input unavailable",20,true);
    public Button KeyboardMode {get;}
    public KeyboardSimulation KeyboardTest {get;}
    private readonly ControllerModel model;private readonly ControllerLayout layout;private readonly FrameworkElement resources;private readonly bool animate;
    private readonly Image artwork=new(){Stretch=Stretch.Fill};private readonly Canvas controls=new();private readonly Dictionary<string,PhysicalHotspot> lights=new();private readonly Canvas positions=new();
    private readonly InputGauge left=new(true,"Left Stick"),right=new(true,"Right Stick"),lt=new(false,"LT"),rt=new(false,"RT");
    private readonly TextBlock pressed=ManagementVisuals.Copy("No input source",18,true),ownership=ManagementVisuals.Copy("Tester owns gameplay input. UI navigation is paused.",15);
    private bool rear;
    public TesterWorkbench(ControllerModel model,ControllerLayout geometry,FrameworkElement owner,Action<Button> register,Action exit,bool animations)
    {
        this.model=model;layout=geometry;resources=owner;animate=animations;State=new(model.Id);KeyboardTest=new(model.Id);
        Body.ColumnDefinitions.Add(new(){Width=new GridLength(1.5,GridUnitType.Star)});Body.ColumnDefinitions.Add(new());var stage=new Grid();stage.RowDefinitions.Add(new(){Height=new GridLength(42)});stage.RowDefinitions.Add(new());Body.Children.Add(stage);
        var views=new StackPanel {Orientation=Orientation.Horizontal};views.Children.Add(ManagementVisuals.Action(resources,"Front",()=>ShowRear(false),register));var back=ManagementVisuals.Action(resources,"Back",()=>ShowRear(true),register);back.IsEnabled=true;views.Children.Add(back);stage.Children.Add(views);
        var map=new Grid {Width=1536,Height=1024,Children={artwork,controls,positions}};var view=new Viewbox {Child=map,Stretch=Stretch.Uniform};Grid.SetRow(view,1);stage.Children.Add(view);
        var status=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new(28,0,20,0)};Grid.SetColumn(status,1);Body.Children.Add(status);status.Children.Add(ManagementVisuals.Copy("GAMEPLAY SOURCE",14));status.Children.Add(SourceLabel);status.Children.Add(ownership);pressed.Margin=new(0,26,0,0);status.Children.Add(pressed);status.Children.Add(ManagementVisuals.Copy("Hardware configuration remains separate. Independent paddle input is shown only when exposed by the input source.",15));Exit=ManagementVisuals.Action(resources,"Exit Tester",exit,register);Exit.Width=190;Exit.HorizontalAlignment=HorizontalAlignment.Left;Exit.Margin=new(0,22,0,0);status.Children.Add(Exit);
        KeyboardMode=ManagementVisuals.Action(resources,"Keyboard Test",()=>SetKeyboardSimulation(!KeyboardTest.Enabled),register);KeyboardMode.HorizontalAlignment=HorizontalAlignment.Left;KeyboardMode.Margin=new(0,10,0,0);status.Children.Add(KeyboardMode);status.Children.Add(ManagementVisuals.Copy("WASD · Left stick    Arrows · Right stick\nQ · Hold LT    E · Hold RT",14));
        Shelf.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Shelf.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Shelf.ColumnDefinitions.Add(new(){Width=new GridLength(.65,GridUnitType.Star)});Shelf.ColumnDefinitions.Add(new(){Width=new GridLength(.65,GridUnitType.Star)});
        int column=0;foreach(var gauge in new[]{left,right,lt,rt}){Grid.SetColumn(gauge,column++);Shelf.Children.Add(gauge);}ShowRear(false);Update();
    }
    public void Enter(){State.Enter();Update();}
    public void ExitCapture(){KeyboardTest.Disable();KeyboardMode.Content="Keyboard Test";State.Exit();Update();}
    public void SetKeyboardSimulation(bool enabled){if(enabled){KeyboardTest.Enable();State.Enter();State.Accept(KeyboardTest.Frame());}else{KeyboardTest.Disable();State.Enter();}KeyboardMode.Content=enabled?"Stop keyboard test":"Keyboard Test";ShowRear(false);Update();}
    public void ClearKeyboardKeys(){if(!KeyboardTest.Enabled)return;KeyboardTest.Clear();State.Accept(KeyboardTest.Frame());Update();}
    public bool Receive(TesterFrame frame){if(KeyboardTest.Enabled)return false;bool accepted=State.Accept(frame);if(accepted)Update();return accepted;}
    public void ReceiveNative(System.Text.Json.JsonElement message)
    {
        if(KeyboardTest.Enabled)return;var payload=message.GetProperty("payload");if(!payload.GetProperty("connected").GetBoolean()){State.Enter();Update();return;}
        State.ObserveNativeGameplay("XInput · Slot "+payload.GetProperty("slot").GetInt32()+" · Model identity unverified",message.GetProperty("sequence").GetInt64(),payload.GetProperty("buttons").EnumerateArray().Select(v=>v.GetString()!).ToArray(),payload.GetProperty("leftX").GetDouble(),payload.GetProperty("leftY").GetDouble(),payload.GetProperty("rightX").GetDouble(),payload.GetProperty("rightY").GetDouble(),payload.GetProperty("lt").GetDouble(),payload.GetProperty("rt").GetDouble());Update();
    }
    public bool HandleKeyboard(Key key,bool down)
    {
        if(!KeyboardTest.Enabled)return false;SimulationKey? mapped=key switch {Key.W=>SimulationKey.W,Key.A=>SimulationKey.A,Key.S=>SimulationKey.S,Key.D=>SimulationKey.D,Key.Up=>SimulationKey.Up,Key.Down=>SimulationKey.Down,Key.Left=>SimulationKey.Left,Key.Right=>SimulationKey.Right,Key.Q=>SimulationKey.Q,Key.E=>SimulationKey.E,_=>null};if(mapped==null)return false;
        try{if(down)KeyboardTest.Press(mapped.Value);else KeyboardTest.Release(mapped.Value);if(down && key is Key.Q or Key.E)ShowRear(true);State.Accept(KeyboardTest.Frame());Update();}catch{KeyboardTest.Disable();State.Enter();Update();throw;}return true;
    }
    public void ShowRear(bool value)
    {
        rear=value;artwork.Source=rear?ControllerArt.Rear(model.Id):ControllerArt.Front(model.Id);artwork.Clip=model.Id=="x20"?(!rear?(Geometry)resources.FindResource("ControllerOutline"):null):ModelArtwork.Clip(model.Id,rear);controls.Children.Clear();lights.Clear();
        foreach(var region in layout.Controls.Where(r=>r.Role!="axis" && (r.View==(rear?"back":"front") || rear&&r.View=="shoulder")))
        {var light=new PhysicalHotspot(region,animate){IsHitTestVisible=false,Focusable=false};Canvas.SetLeft(light,region.Bounds.X*1536);Canvas.SetTop(light,region.Bounds.Y*1024);controls.Children.Add(light);lights.Add(region.Key,light);}Update();
    }
    private void Update()
    {
        var frame=State.Frame;SourceLabel.Text=KeyboardTest.Enabled?"KEYBOARD SIMULATION":frame==null?"Gameplay input unavailable":frame.IsSimulation?"SIMULATION · "+frame.Source:frame.Source;SourceLabel.Foreground=frame==null?Brushes.White:frame.IsSimulation?Brushes.SandyBrown:Brushes.LightSkyBlue;pressed.Text=KeyboardTest.Enabled?(KeyboardTest.Held.Count==0?"Neutral · No keys held":"Held: "+string.Join(" + ",KeyboardTest.Held)):frame==null?"Waiting for native gameplay input":string.Join(" + ",frame.ButtonsDown.Where(State.Pressed).Select(k=>k switch {"DPAD_UP"=>"D-pad ↑","DPAD_DOWN"=>"D-pad ↓","DPAD_LEFT"=>"D-pad ←","DPAD_RIGHT"=>"D-pad →",_=>k}));if(frame!=null&&pressed.Text.Length==0)pressed.Text="No buttons held";
        foreach(var (key,light) in lights){bool active=State.Pressed(key) || frame!=null && (key=="LT"&&frame.LT>0 || key=="RT"&&frame.RT>0 || key=="L3"&&(frame.LeftX!=0||frame.LeftY!=0) || key=="R3"&&(frame.RightX!=0||frame.RightY!=0));light.Illuminate(active,false);}
        left.Set(frame?.LeftX,frame?.LeftY);right.Set(frame?.RightX,frame?.RightY);lt.Set(frame?.LT,null);rt.Set(frame?.RT,null);positions.Children.Clear();
        if(frame!=null&&!rear)foreach(var (key,x,y) in new[]{("L3",frame.LeftX,frame.LeftY),("R3",frame.RightX,frame.RightY)})
        {var region=layout.Controls.Single(r=>r.Key==key);double centerX=(region.Bounds.X+region.Bounds.Width/2)*1536,centerY=(region.Bounds.Y+region.Bounds.Height/2)*1024;var dot=new System.Windows.Shapes.Ellipse {Width=22,Height=22,Fill=Brushes.LightSkyBlue,Stroke=Brushes.White,StrokeThickness=3,IsHitTestVisible=false};Canvas.SetLeft(dot,centerX+x*region.Bounds.Width*1536*.3-11);Canvas.SetTop(dot,centerY-y*region.Bounds.Height*1024*.3-11);positions.Children.Add(dot);}
    }
    public bool IsIlluminated(string key)=>lights.TryGetValue(key,out var light)&&light.Light>.5;
    private sealed class InputGauge(bool stick,string label):FrameworkElement
    {
        private double? x,y;public InputGauge():this(true,""){}public void Set(double? first,double? second){x=first;y=second;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            double width=ActualWidth,height=ActualHeight;var text=new FormattedText(label,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),17,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(text,new(12,4));double size=Math.Min(height-50,width*.55),cx=12+size/2,cy=30+size/2;
            if(stick){dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(13,26,44)),new Pen(Brushes.SlateGray,1),new(cx,cy),size/2,size/2);dc.DrawLine(new Pen(Brushes.SlateGray,1),new(cx-size/2,cy),new(cx+size/2,cy));dc.DrawLine(new Pen(Brushes.SlateGray,1),new(cx,cy-size/2),new(cx,cy+size/2));if(x.HasValue)dc.DrawEllipse(Brushes.LightSkyBlue,new Pen(Brushes.White,1.5),new(cx+x.Value*size*.4,cy-(y??0)*size*.4),6,6);}
            else {dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16,30,48)),null,new Rect(12,40,width-28,28),14,14);if(x.HasValue)dc.DrawRoundedRectangle(Brushes.LightSkyBlue,null,new Rect(12,40,(width-28)*x.Value,28),14,14);}
            string value=stick?(x.HasValue?$"X {x:0.00}   Y {y:0.00}":"X —   Y —"):(x.HasValue?$"{x*100:0}%":"Unavailable");var values=new FormattedText(value,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.LightSteelBlue,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(values,stick?new(Math.Min(width-150,size+28),48):new(12,82));
        }
    }
}
