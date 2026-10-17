using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// The Curves page's graph and editor (rebuilt 14 Oct 2026, owner: "it doesn't listen at all to what the AI does"). The
/// line is exactly what games get (<see cref="PadProcessor.Response"/>): the stick's dead zone, full-at, anti-dead zone and
/// max output from PC mode's stick settings, then the curve through its two points. Every one of those is a handle on the
/// graph and a slider below it; a live dot follows the real stick or trigger whenever a controller is connected.
/// </summary>
public sealed class CurveWorkbench
{
    public Grid Inspector { get; } = new();
    public Grid Shelf { get; } = new();
    public WrapPanel Toolbar {get;}=new(){Margin=new(0,0,0,8)};
    public CurveDraft Draft { get; }
    public CurveChannel Channel { get; private set; }
    public IEnumerable<Button> Buttons => channelButtons.Concat(presetButtons);
    public IEnumerable<Thumb> PointHandles => new[]{first,second};
    public IEnumerable<Point> PreviewPoints => response.Points;
    public Rect PlotBounds => chart.TransformToAncestor(Inspector).TransformBounds(new Rect(L,T,PW,PH));
    public int? SelectedPoint { get; private set; }
    public string PresetDescription => summary.Text;
    public TextBlock OutputAxisLabel { get; } = new() { Text="OUTPUT %",FontSize=13,Foreground=Brushes.LightSteelBlue };
    const string Hint="";
    public TextBlock PreviewNotice { get; } = new() { Text=Hint,FontSize=12.5,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap };
    public TextBlock ActivePointReadout { get; } = new() { FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap };
    /// <summary>PC mode's stick settings for a stick (null: none saved, or PC mode isn't reachable). Set by the Studio.</summary>
    public Func<CurveChannel,StickTuning?>? Tuning { get; set; }
    /// <summary>Saves a stick's settings to PC mode; returns why not, or null. Null when they can't be saved from here.</summary>
    public Func<CurveChannel,StickTuning,string?>? SaveTuning { get; set; }
    public Func<IReadOnlyList<CurveChannel>,string?>? ResetResponse { get; set; }
    public bool CanReset => Draft.CanEdit && IsModified(Channel);
    public bool CanResetAll => Draft.CanEdit && Enum.GetValues<CurveChannel>().Any(IsModified);
    bool IsModified(CurveChannel channel) => Draft[channel] != CurveSetting.Default || Tuning?.Invoke(channel) is { IsDefault: false };
    private readonly TextBlock editHint = new() { Text="← / →  1% steps · Shift  5% steps",FontSize=13,Foreground=Brushes.LightSteelBlue,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom };
    public event Action<CurveChannel>? ChannelSelected;
    public event Action? Changed;
    private readonly List<Button> channelButtons = new(), presetButtons = new();
    // the plot: 408 x 240 inside a 480 x 300 canvas
    private const double L=38,T=16,PW=408,PH=240;
    private readonly Canvas chart = new() { Width=480, Height=300 };
    private readonly Polyline response = new() { Stroke=new SolidColorBrush(Color.FromRgb(116,200,255)), StrokeThickness=3.5, StrokeLineJoin=PenLineJoin.Round, Effect=new DropShadowEffect{Color=Color.FromRgb(70,150,255),BlurRadius=12,ShadowDepth=0,Opacity=.7} };
    private readonly Polygon area = new() { Fill=new LinearGradientBrush(Color.FromArgb(30,70,150,255),Color.FromArgb(0,70,150,255),90) };
    private readonly Rectangle innerBand = new() { Fill=new SolidColorBrush(Color.FromArgb(46,232,54,79)) }, outerBand = new() { Fill=new SolidColorBrush(Color.FromArgb(40,61,214,140)) };
    private readonly Line floorLine = Guide(Color.FromArgb(150,255,196,92)), ceilingLine = Guide(Color.FromArgb(150,255,196,92)), liveGuide = Guide(Color.FromArgb(90,169,207,255));
    private readonly Thumb first = new(), second = new(), deadHandle = new(), fullHandle = new(), floorHandle = new(), ceilingHandle = new();
    private readonly TextBlock deadTag = Tag(), fullTag = Tag(), floorTag = Tag(), ceilingTag = Tag(), halfTag = Tag(), liveTag = Tag();
    private readonly Ellipse halfDot = new() { Width=10,Height=10,Fill=Brushes.White,IsHitTestVisible=false }, liveDot = new() { Width=16,Height=16,Fill=new SolidColorBrush(Color.FromRgb(61,214,140)),IsHitTestVisible=false,Visibility=Visibility.Collapsed,Effect=new DropShadowEffect{Color=Color.FromRgb(61,214,140),BlurRadius=16,ShadowDepth=0,Opacity=1} };
    private readonly TextBlock title = new() { FontSize=28,FontWeight=FontWeights.SemiBold }, summary = new() { FontSize=15, Foreground=Brushes.LightSteelBlue }, coordinates = new() { FontSize=14,Foreground=Brushes.LightSteelBlue };
    public Slider Inner { get; } = new() { Minimum=0,Maximum=40,TickFrequency=1,IsSnapToTickEnabled=true };
    public Slider Outer { get; } = new() { Minimum=60,Maximum=100,Value=100,TickFrequency=1,IsSnapToTickEnabled=true };
    public Slider Floor { get; } = new() { Minimum=0,Maximum=40,TickFrequency=1,IsSnapToTickEnabled=true };
    public Slider Ceiling { get; } = new() { Minimum=60,Maximum=100,Value=100,TickFrequency=1,IsSnapToTickEnabled=true };
    private readonly TextBlock innerText=new() { FontSize=16 }, outerText=new() { FontSize=16 }, floorText=new() { FontSize=16 }, ceilingText=new() { FontSize=16 };
    private readonly StackPanel floorPanel=new() {Margin=new(10,0,24,0)}, ceilingPanel=new() {Margin=new(10,0,24,0)};
    private readonly TextBox pointInput=new(){Width=86,MinHeight=34,Padding=new(8,3,8,3),Margin=new(6,0,14,0)},pointOutput=new(){Width=86,MinHeight=34,Padding=new(8,3,8,3),Margin=new(6,0,0,0)};
    private readonly StackPanel pointEditor=new(){Orientation=Orientation.Horizontal,Visibility=Visibility.Hidden};
    private readonly TextBlock liveReadout=new(){FontSize=14,Foreground=Brushes.LightSteelBlue,Text="Connect a controller or use Keyboard test"};
    private bool updating;
    private double liveAt=-1;
    public bool AnimationsEnabled { get; }

    static Line Guide(Color c)=>new() { Stroke=new SolidColorBrush(c),StrokeThickness=1.5,StrokeDashArray=new(){4,3},IsHitTestVisible=false };
    static TextBlock Tag()=>new() { FontSize=12.5,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,IsHitTestVisible=false };
    static double X(double percent)=>L+percent/100*PW;
    static double Y(double percent)=>T+PH-percent/100*PH;

    public CurveWorkbench(CurveDraft draft,FrameworkElement resources,bool animations=true)
    {
        Draft=draft;AnimationsEnabled=animations;
        Inspector.RowDefinitions.Add(new(){Height=GridLength.Auto});Inspector.RowDefinitions.Add(new());Inspector.RowDefinitions.Add(new(){Height=new GridLength(0)});
        var heading=new StackPanel();heading.Children.Add(Toolbar);heading.Children.Add(title);heading.Children.Add(summary);Inspector.Children.Add(heading);
        var view=new Viewbox { Child=chart,Stretch=Stretch.Uniform };Grid.SetRow(view,1);Inspector.Children.Add(view);
        var note=new StackPanel{ClipToBounds=true};note.Children.Add(coordinates);note.Children.Add(ActivePointReadout);note.Children.Add(pointEditor);note.Children.Add(liveReadout);
        pointEditor.Children.Add(new TextBlock{Text="Input %",VerticalAlignment=VerticalAlignment.Center});pointEditor.Children.Add(pointInput);pointEditor.Children.Add(new TextBlock{Text="Output %",VerticalAlignment=VerticalAlignment.Center});pointEditor.Children.Add(pointOutput);
        foreach(var box in new[]{pointInput,pointOutput}){box.IsEnabled=Draft.CanEdit;box.LostKeyboardFocus+=(_,_)=>CommitPoint();box.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter){CommitPoint();e.Handled=true;}};}
        PreviewNotice.Margin=new(0,6,0,0);note.Children.Add(PreviewNotice);Grid.SetRow(note,1);Grid.SetColumn(note,1);note.Margin=new(20,0,0,0);
        Inspector.SizeChanged+=(_,_)=>RefreshPointReadout();
        foreach(var band in new[]{innerBand,outerBand}) {band.Height=PH;Canvas.SetTop(band,T);chart.Children.Add(band);}
        foreach(int tick in new[]{0,25,50,75,100})
        {
            double x=X(tick),y=Y(tick);
            chart.Children.Add(new Line {X1=x,X2=x,Y1=T,Y2=T+PH,Stroke=new SolidColorBrush(Color.FromArgb(42,132,160,202)),StrokeThickness=1});
            chart.Children.Add(new Line {X1=L,X2=L+PW,Y1=y,Y2=y,Stroke=new SolidColorBrush(Color.FromArgb(42,132,160,202)),StrokeThickness=1});
            var horizontal=new TextBlock {Text=tick.ToString(),FontSize=13,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(horizontal,x-7);Canvas.SetTop(horizontal,T+PH+6);chart.Children.Add(horizontal);
            var vertical=new TextBlock {Text=tick.ToString(),FontSize=13,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(vertical,4);Canvas.SetTop(vertical,y-8);chart.Children.Add(vertical);
        }
        chart.Children.Add(new Line {X1=L,Y1=T+PH,X2=L+PW,Y2=T,Stroke=new SolidColorBrush(Color.FromArgb(110,200,216,244)),StrokeDashArray=new(){4,4}});
        chart.Children.Add(area);chart.Children.Add(floorLine);chart.Children.Add(ceilingLine);chart.Children.Add(liveGuide);chart.Children.Add(response);
        chart.Children.Add(halfDot);chart.Children.Add(halfTag);
        Canvas.SetLeft(OutputAxisLabel,L);Canvas.SetTop(OutputAxisLabel,0);chart.Children.Add(OutputAxisLabel);
        var input=new TextBlock {Text="INPUT %",FontSize=13,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(input,206);Canvas.SetTop(input,284);chart.Children.Add(input);

        // the curve's two points, on the line itself
        for(int index=0;index<2;index++)
        {
            Thumb thumb=index==0?first:second;thumb.Width=thumb.Height=18;thumb.Tag=index;thumb.Focusable=true;thumb.FocusVisualStyle=null;thumb.Cursor=Cursors.SizeAll;
            thumb.Template=Knob(index==0?Brushes.LightSkyBlue:new SolidColorBrush(Color.FromRgb(232,238,248)),true);
            System.Windows.Automation.AutomationProperties.SetName(thumb,$"Curve point {index+1}, arrow keys adjust input and output");
            int pointIndex=index;
            thumb.GotKeyboardFocus+=(_,_)=>SelectPoint(pointIndex);
            thumb.PreviewMouseLeftButtonDown+=(_,_)=>{thumb.Focus();SelectPoint(pointIndex);};
            thumb.LostKeyboardFocus+=(_,e)=>{if(e.NewFocus!=first && e.NewFocus!=second && e.NewFocus!=pointInput && e.NewFocus!=pointOutput) {SelectedPoint=null;RefreshPointReadout();}};
            thumb.DragDelta+=(_,e)=>
            {
                var z=Zones();var point=pointIndex==0?Draft[Channel].Point1:Draft[Channel].Point2;
                double sx=Math.Max(1,z.Outer-z.Inner)/100*PW,sy=Math.Max(1,z.Ceiling-z.Floor)/100*PH;
                SetPoint(pointIndex,point.X+(int)Math.Round(e.HorizontalChange/sx*255),point.Y-(int)Math.Round(e.VerticalChange/sy*255));
            };
            chart.Children.Add(thumb);
        }
        // the zones: dead zone and full-at slide along the input, anti-dead zone and max output along the output
        foreach(var (thumb,tag,name) in new[]{(deadHandle,deadTag,"Dead zone"),(fullHandle,fullTag,"Full at"),(floorHandle,floorTag,"Anti-dead zone"),(ceilingHandle,ceilingTag,"Max output")})
        {
            bool across=thumb==deadHandle||thumb==fullHandle;
            thumb.Width=across?8:16;thumb.Height=across?16:8;
            // the value shows only while the handle is pointed at, focused or dragged (owner: too many words)
            var said=tag;said.Visibility=Visibility.Collapsed;
            void Say(bool on)=>said.Visibility=on||thumb.IsDragging||thumb.IsKeyboardFocused||thumb.IsMouseOver?Visibility.Visible:Visibility.Collapsed;
            thumb.MouseEnter+=(_,_)=>Say(true);thumb.MouseLeave+=(_,_)=>Say(false);thumb.GotKeyboardFocus+=(_,_)=>Say(true);thumb.LostKeyboardFocus+=(_,_)=>Say(false);
            thumb.DragStarted+=(_,_)=>Say(true);thumb.DragCompleted+=(_,_)=>Say(false);thumb.Focusable=true;thumb.FocusVisualStyle=null;thumb.Cursor=across?Cursors.SizeWE:Cursors.SizeNS;thumb.ToolTip=name;
            thumb.Template=Knob(new SolidColorBrush(thumb==deadHandle?Color.FromRgb(232,54,79):thumb==fullHandle?Color.FromRgb(61,214,140):Color.FromRgb(255,196,92)),false);
            System.Windows.Automation.AutomationProperties.SetName(thumb,name+", arrow keys adjust");
            var me=thumb;
            thumb.DragDelta+=(_,e)=>{var z=Zones();if(me==deadHandle)SetZone(0,z.Inner+e.HorizontalChange/PW*100);else if(me==fullHandle)SetZone(1,z.Outer+e.HorizontalChange/PW*100);else if(me==floorHandle)SetZone(2,z.Floor-e.VerticalChange/PH*100);else SetZone(3,z.Ceiling-e.VerticalChange/PH*100);};
            chart.Children.Add(thumb);chart.Children.Add(tag);
        }
        chart.Children.Add(liveDot);chart.Children.Add(liveTag);

        Shelf.ColumnDefinitions.Add(new());Shelf.ColumnDefinitions.Add(new(){Width=new(360)});Shelf.RowDefinitions.Add(new(){Height=new GridLength(76)});Shelf.RowDefinitions.Add(new());Shelf.Children.Add(note);
        var rail=new Grid();rail.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});rail.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Shelf.Children.Add(rail);
        var channels=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};rail.Children.Add(channels);
        foreach(var channel in Enum.GetValues<CurveChannel>())
        {
            var button=new Button {Content=ShortName(channel),Tag=channel,Width=90,Height=36,Style=(Style)resources.FindResource("ActionButton"),Margin=new(0,0,9,0),ToolTip=Name(channel)};
            button.Click+=(_,_)=>SelectChannel(channel);channelButtons.Add(button);channels.Children.Add(button);
        }
        var presets=new WrapPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(presets,1);rail.Children.Add(presets);
        foreach(string preset in CurveDraft.Presets.Keys)
        {
            var button=new Button {Content=char.ToUpper(preset[0])+preset[1..],Tag=preset,Style=(Style)resources.FindResource("ActionButton"),Height=34,Padding=new(12,0,12,0),Margin=new(5,0,0,0),IsEnabled=Draft.CanEdit};var mini=new Canvas{Width=32,Height=18,Margin=new(0,0,6,0)};var points=CurveDraft.Presets[preset];mini.Children.Add(new Polyline{Stroke=Brushes.LightSkyBlue,StrokeThickness=1.5,Points=new PointCollection{new(0,18),new(points.First.Item1/255d*32,18-points.First.Item2/255d*18),new(points.Second.Item1/255d*32,18-points.Second.Item2/255d*18),new(32,0)}});button.Content=new StackPanel{Orientation=Orientation.Horizontal,Children={mini,new TextBlock{Text=char.ToUpper(preset[0])+preset[1..]}.Also(t=>t.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1)}))}};button.ToolTip="Select "+preset+" response";button.Click+=(_,_)=>SetPreset(preset);presetButtons.Add(button);presets.Children.Add(button);
        }
        rail.Children.Remove(channels);rail.Children.Remove(presets);channels.Margin=new(0);heading.Children.Add(Kit.Segments(channels));presets.HorizontalAlignment=HorizontalAlignment.Left;presets.Margin=new(0);heading.Children.Add(Kit.Segments(presets).Also(b=>b.Margin=new(0,8,0,12)));Shelf.RowDefinitions[0].Height=new(0);
        var zones=new Grid {Margin=new(0,0,0,10)};for(int c=0;c<2;c++){zones.ColumnDefinitions.Add(new());zones.RowDefinitions.Add(new());}Grid.SetRow(zones,1);Shelf.Children.Add(zones);
        int kind=0;
        foreach(var (slider,text,label) in new[]{(Inner,innerText,"Dead zone percent"),(Outer,outerText,"Full at percent"),(Floor,floorText,"Anti-dead zone percent"),(Ceiling,ceilingText,"Max output percent")})
        {
            var panel=kind==2?floorPanel:kind==3?ceilingPanel:new StackPanel {Margin=new(10,0,24,0)};panel.Children.Add(text);slider.IsEnabled=Draft.CanEdit;slider.Style=(Style)resources.FindResource("ConsoleCurveSlider");slider.Margin=new(0,8,0,0);panel.Children.Add(slider);Grid.SetColumn(panel,kind%2);Grid.SetRow(panel,kind/2);text.FontSize=14;slider.Height=24;slider.Margin=new(0);zones.Children.Add(panel);
            System.Windows.Automation.AutomationProperties.SetName(slider,label);
            slider.ToolTip="← / → adjusts 1 percentage point. Hold Shift for 5-point steps.";
            int k=kind;slider.ValueChanged+=(_,_)=>{if(!updating)SetZone(k,slider.Value);};
            kind++;
        }
        editHint.Visibility=Visibility.Collapsed;
        Refresh();
    }

    static ControlTemplate Knob(Brush fill,bool round)
    {
        var factory=new FrameworkElementFactory(round?typeof(Ellipse):typeof(Rectangle));
        factory.SetValue(Shape.FillProperty,fill);factory.SetValue(Shape.StrokeProperty,Brushes.White);factory.SetValue(Shape.StrokeThicknessProperty,2d);
        if(!round){factory.SetValue(Rectangle.RadiusXProperty,4d);factory.SetValue(Rectangle.RadiusYProperty,4d);}
        return new ControlTemplate(typeof(Thumb)){VisualTree=factory};
    }

    bool IsStick=>Channel is CurveChannel.LeftStick or CurveChannel.RightStick;
    /// <summary>The stick's settings decide the zones when PC mode holds them (they win over the curve's own, as in games).</summary>
    bool TuningOwnsZones=>IsStick && SaveTuning!=null;
    StickTuning? CurrentTuning=>IsStick?Tuning?.Invoke(Channel):null;
    SetupCurve Setup(CurveSetting s)=>new(s.Preset,s.BasePreset,s.Inner,s.Outer,s.Point1.X,s.Point1.Y,s.Point2.X,s.Point2.Y);
    /// <summary>Dead zone, full at, floor and ceiling, in percent, as games get them.</summary>
    (double Inner,double Outer,double Floor,double Ceiling) Zones()
    {
        var s=Draft[Channel];var t=CurrentTuning;
        return t==null?(s.Inner,s.Outer,0,100):t.Raw?(0,100,0,100):(t.Inner,t.Outer,t.AntiDeadzone,t.MaxOutput);
    }
    /// <summary>What games get at this push (0–1).</summary>
    public double Output(double pushed)=>PadProcessor.Response(Setup(Draft[Channel]),CurrentTuning,pushed);

    /// <summary>0 dead zone, 1 full at, 2 anti-dead zone, 3 max output.</summary>
    private void SetZone(int kind,double value)
    {
        if(!Draft.CanEdit)return;
        value=Math.Round(value);
        if(TuningOwnsZones)
        {
            var s=Draft[Channel];var t=Copy(CurrentTuning)??new StickTuning{Inner=s.Inner,Outer=s.Outer};
            switch(kind)
            {
                case 0: t.Inner=Math.Clamp(value,0,Math.Min(StickTuning.MaxInner,t.Outer-1)); break;
                case 1: t.Outer=Math.Clamp(value,Math.Max(StickTuning.MinOuter,t.Inner+1),100); break;
                case 2: t.AntiDeadzone=Math.Clamp(value,0,Math.Min(StickTuning.MaxAnti,t.MaxOutput-1)); break;
                default: t.MaxOutput=Math.Clamp(value,Math.Max(60,t.AntiDeadzone+1),100); break;
            }
            t.Raw=false;
            PreviewNotice.Text=SaveTuning!(Channel,t)??Hint;
        }
        else if(kind<2)
        {
            var s=Draft[Channel];
            Draft.Set(Channel,kind==0?s with {Preset="custom",Inner=(int)Math.Clamp(value,0,s.Outer-1)}:s with {Preset="custom",Outer=(int)Math.Clamp(value,s.Inner+1,100)});
        }
        else return;
        Refresh();Changed?.Invoke();
    }
    static StickTuning? Copy(StickTuning? t)=>t==null?null:new(){Inner=t.Inner,Outer=t.Outer,SquareGate=t.SquareGate,AxisRatio=t.AxisRatio,AntiDeadzone=t.AntiDeadzone,MaxOutput=t.MaxOutput,Raw=t.Raw};

    public void SelectChannel(CurveChannel channel) {Channel=channel;SelectedPoint=null;liveAt=-1;Refresh();ChannelSelected?.Invoke(channel);}
    private void SelectPoint(int index) {SelectedPoint=index;RefreshPointReadout();}
    public void SetPreset(string preset) {Draft.SetPreset(Channel,preset);Refresh();Changed?.Invoke();}
    public void Reset() => ResetChannels([Channel]);
    public void ResetAll() => ResetChannels(Enum.GetValues<CurveChannel>());
    void ResetChannels(IReadOnlyList<CurveChannel> channels)
    {
        if (!Draft.CanEdit) return;
        if (ResetResponse?.Invoke(channels) is { } error) { PreviewNotice.Text=error; return; }
        foreach (var channel in channels) Draft.Reset(channel);
        PreviewNotice.Text=Hint; Refresh(); Changed?.Invoke();
    }
    public void SetPoint(int index,int x,int y)
    {
        if(!Draft.CanEdit)return;var setting=Draft[Channel];
        x=Math.Clamp(x,index==0?1:setting.Point1.X+1,index==0?setting.Point2.X-1:254);y=Math.Clamp(y,0,255);
        Draft.Set(Channel,index==0?setting with {Preset="custom",Point1=(x,y)}:setting with {Preset="custom",Point2=(x,y)});Refresh();Changed?.Invoke();
    }
    public bool HandleKey(KeyEventArgs e)
    {
        int step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?5:1;
        if(Keyboard.FocusedElement is Thumb thumb && (thumb==first || thumb==second) && thumb.Tag is int index && Draft.CanEdit)
        {
            var p=index==0?Draft[Channel].Point1:Draft[Channel].Point2;step=(int)Math.Round(step*2.55);
            if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {SetPoint(index,p.X+(e.Key==Key.Right?step:e.Key==Key.Left?-step:0),p.Y+(e.Key==Key.Up?step:e.Key==Key.Down?-step:0));e.Handled=true;return true;}
        }
        if(Keyboard.FocusedElement is Thumb zone && Draft.CanEdit && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            int sign=e.Key is Key.Right or Key.Up?1:-1;var z=Zones();
            if(zone==deadHandle){SetZone(0,z.Inner+sign*step);e.Handled=true;return true;}
            if(zone==fullHandle){SetZone(1,z.Outer+sign*step);e.Handled=true;return true;}
            if(zone==floorHandle){SetZone(2,z.Floor+sign*step);e.Handled=true;return true;}
            if(zone==ceilingHandle){SetZone(3,z.Ceiling+sign*step);e.Handled=true;return true;}
        }
        if(Keyboard.FocusedElement is Slider slider && (slider==Inner || slider==Outer || slider==Floor || slider==Ceiling) && slider.IsEnabled && e.Key is Key.Left or Key.Right) {slider.Value+=e.Key==Key.Right?step:-step;e.Handled=true;return true;}
        return false;
    }

    /// <summary>The real controller, live: how far this stick (or trigger) is pushed, 0–1. Always on while one is connected.</summary>
    public void ShowLive(double pushed)
    {
        if(pushed<0){liveReadout.Text="Input unavailable · Connect a controller or use keyboard simulation";liveDot.Visibility=liveTag.Visibility=liveGuide.Visibility=Visibility.Collapsed;liveAt=-1;return;}
        pushed=Math.Clamp(pushed,0,1);liveReadout.Text=$"Input {pushed:P0} → Output {Output(pushed):P0}";
        if(pushed<.02){if(liveAt>=0){liveAt=-1;liveDot.Visibility=liveTag.Visibility=liveGuide.Visibility=Visibility.Collapsed;}return;}
        if(Math.Abs(pushed-liveAt)<.004)return;liveAt=pushed;
        double o=Output(pushed)*100,x=X(pushed*100),y=Y(o);
        Canvas.SetLeft(liveDot,x-8);Canvas.SetTop(liveDot,y-8);
        liveGuide.X1=liveGuide.X2=x;liveGuide.Y1=T;liveGuide.Y2=T+PH;
        liveTag.Text=$"{pushed*100:0}% → {o:0}%";Canvas.SetLeft(liveTag,Math.Min(x+12,L+PW-70));Canvas.SetTop(liveTag,Math.Max(T,y-26));
        liveDot.Visibility=liveTag.Visibility=liveGuide.Visibility=Visibility.Visible;
    }

    public void Refresh()
    {
        updating=true;var setting=Draft[Channel];title.Text=Name(Channel);string basis=char.ToUpper(setting.BasePreset[0])+setting.BasePreset[1..];
        var tuning=CurrentTuning;var z=Zones();bool modified=setting!=CurveSetting.Default||tuning is {IsDefault:false};
        summary.Text=setting.Preset=="custom"?$"Custom curve · based on {basis}":$"{basis} curve · {(modified?"Modified":"Unmodified")}";
        if(tuning?.Raw==true)summary.Text+=" · Raw 1:1 (curve and zones are skipped)";
        Inner.Value=z.Inner;Outer.Value=z.Outer;Floor.Value=z.Floor;Ceiling.Value=z.Ceiling;
        innerText.Text=$"{(IsStick?"Inner dead zone":"Start point")}   {z.Inner:0.#}%";outerText.Text=$"{(IsStick?"Full output at":"Full press point")}   {z.Outer:0.#}%";floorText.Text=$"Anti-dead zone   {z.Floor:0.#}%";ceilingText.Text=$"Max output   {z.Ceiling:0.#}%";
        bool limits=TuningOwnsZones;
        Inner.Maximum=limits?StickTuning.MaxInner:99;Outer.Minimum=limits?StickTuning.MinOuter:1;
        floorPanel.Visibility=ceilingPanel.Visibility=limits?Visibility.Visible:Visibility.Collapsed;
        floorHandle.Visibility=ceilingHandle.Visibility=limits?Visibility.Visible:Visibility.Collapsed;
        if(!limits)floorTag.Visibility=ceilingTag.Visibility=Visibility.Collapsed;
        // a limit's line only when it limits something
        floorLine.Visibility=limits&&z.Floor>0?Visibility.Visible:Visibility.Collapsed;ceilingLine.Visibility=limits&&z.Ceiling<100?Visibility.Visible:Visibility.Collapsed;
        foreach(var c in new UIElement[]{deadHandle,fullHandle,floorHandle,ceilingHandle,first,second,Inner,Outer,Floor,Ceiling})c.IsEnabled=Draft.CanEdit;

        innerBand.Width=z.Inner/100*PW;Canvas.SetLeft(innerBand,L);outerBand.Width=(100-z.Outer)/100*PW;Canvas.SetLeft(outerBand,X(z.Outer));
        Place(deadHandle,X(z.Inner),T+PH);Place(fullHandle,X(z.Outer),T+PH);
        Label(deadTag,$"Dead zone {z.Inner:0.#}%",Math.Max(L,X(z.Inner)-30),T+PH-30);Label(fullTag,$"Full at {z.Outer:0.#}%",Math.Min(X(z.Outer)-40,L+PW-90),T+PH-30);
        floorLine.X1=X(z.Inner);floorLine.X2=X(z.Outer);floorLine.Y1=floorLine.Y2=Y(z.Floor);
        ceilingLine.X1=X(z.Inner);ceilingLine.X2=X(z.Outer);ceilingLine.Y1=ceilingLine.Y2=Y(z.Ceiling);
        Place(floorHandle,L+PW+14,Y(z.Floor));Place(ceilingHandle,L+PW+14,Y(z.Ceiling));
        Label(floorTag,$"Anti-dead zone {z.Floor:0.#}%",L+PW-130,Y(z.Floor)-24);Label(ceilingTag,$"Max output {z.Ceiling:0.#}%",L+PW-120,Y(z.Ceiling)+6);

        var previous=response.Points.Clone();response.BeginAnimation(Polyline.PointsProperty,null);
        var line=new PointCollection(Enumerable.Range(0,201).Select(i=>new Point(X(i/2d),Y(Output(i/200d)*100))));
        response.Points=line;area.Points=new PointCollection(line.Append(new Point(L+PW,T+PH)).Append(new Point(L,T+PH)));
        bool morph=AnimationsEnabled && !Dragging;
        if(morph && previous.Count==line.Count) response.BeginAnimation(Polyline.PointsProperty,new CurvePointMorph(previous,line));
        halfDot.Visibility=halfTag.Visibility=Visibility.Collapsed;
        // each point sits on the line: its push mapped between dead zone and full-at, its output between the limits
        foreach(var (thumb,p) in new[]{(first,setting.Point1),(second,setting.Point2)})
        {
            double previousX=Canvas.GetLeft(thumb),previousY=Canvas.GetTop(thumb);thumb.BeginAnimation(Canvas.LeftProperty,null);thumb.BeginAnimation(Canvas.TopProperty,null);
            double px=z.Inner+(z.Outer-z.Inner)*p.X/255d,py=z.Floor+(z.Ceiling-z.Floor)*p.Y/255d;
            double x=X(px)-9,y=Y(py)-9;Canvas.SetLeft(thumb,x);Canvas.SetTop(thumb,y);
            if(morph && double.IsFinite(previousX)) {thumb.BeginAnimation(Canvas.LeftProperty,new DoubleAnimation(previousX,x,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});thumb.BeginAnimation(Canvas.TopProperty,new DoubleAnimation(previousY,y,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});}
        }
        if(liveAt>=0){double keep=liveAt;liveAt=-1;ShowLive(keep);}
        coordinates.Text=$"Points  {setting.Point1.X/2.55:0} → {setting.Point1.Y/2.55:0}%   ·   {setting.Point2.X/2.55:0} → {setting.Point2.Y/2.55:0}%"+(tuning is {AxisRatio:not 100} r?$"   ·   Vertical {r.AxisRatio}%":"")+(tuning?.SquareGate==true?"   ·   Square gate":"");
        coordinates.ToolTip=$"Stored point 1: {setting.Point1.X}/255, {setting.Point1.Y}/255. Point 2: {setting.Point2.X}/255, {setting.Point2.Y}/255.";
        coordinates.TextWrapping=TextWrapping.Wrap;RefreshPointReadout();
        foreach(var button in channelButtons)button.Background=(CurveChannel)button.Tag==Channel?Brushes.White:DS.Brush("Control.ButtonFill");
        foreach(var button in channelButtons)button.Foreground=(CurveChannel)button.Tag==Channel?new SolidColorBrush(Color.FromRgb(17,24,39)):Brushes.LightSteelBlue;
        foreach(var button in presetButtons)button.Foreground=(string)button.Tag==setting.Preset?new SolidColorBrush(Color.FromRgb(17,24,39)):Brushes.LightSteelBlue;
        foreach(var button in presetButtons)button.Background=(string)button.Tag==setting.Preset?Brushes.White:DS.Brush("Control.ButtonFill");
        updating=false;
    }
    bool Dragging=>new[]{first,second,deadHandle,fullHandle,floorHandle,ceilingHandle}.Any(t=>t.IsDragging);
    static void Place(Thumb t,double x,double y){Canvas.SetLeft(t,x-t.Width/2);Canvas.SetTop(t,y-t.Height/2);}
    static void Label(TextBlock t,string text,double x,double y){t.Text=text;Canvas.SetLeft(t,x);Canvas.SetTop(t,y);}
    private void RefreshPointReadout()
    {
        bool active=SelectedPoint.HasValue;
        coordinates.Visibility=Visibility.Collapsed;pointEditor.Visibility=active?Visibility.Visible:Visibility.Hidden;
        ActivePointReadout.Visibility=active?Visibility.Visible:Visibility.Hidden;
        Inspector.RowDefinitions[^1].Height=new(0);
        editHint.Text=active?$"POINT {SelectedPoint+1}    ← / → Input    ↑ / ↓ Output":"← / →  1% steps · Shift  5% steps";
        if(active)
        {
            var point=SelectedPoint==0?Draft[Channel].Point1:Draft[Channel].Point2;
            ActivePointReadout.Text=$"POINT {SelectedPoint+1}";if(!pointInput.IsKeyboardFocused)pointInput.Text=(point.X/2.55).ToString("0.0");if(!pointOutput.IsKeyboardFocused)pointOutput.Text=(point.Y/2.55).ToString("0.0");
            ActivePointReadout.ToolTip=$"Stored point coordinates: input {point.X}/255, output {point.Y}/255.";
        }
    }
    void CommitPoint()
    {
        if(SelectedPoint is not int index || !Draft.CanEdit)return;
        if(double.TryParse(pointInput.Text,out double x)&&double.TryParse(pointOutput.Text,out double y)&&double.IsFinite(x)&&double.IsFinite(y))SetPoint(index,(int)Math.Round(Math.Clamp(x,0,100)*2.55),(int)Math.Round(Math.Clamp(y,0,100)*2.55));
        else PreviewNotice.Text="Enter percentages between 0 and 100.";
    }
    public static string Name(CurveChannel channel)=>channel switch {CurveChannel.LeftStick=>"Left stick",CurveChannel.RightStick=>"Right stick",CurveChannel.LeftTrigger=>"Left trigger · LT",_=>"Right trigger · RT"};
    private static string ShortName(CurveChannel channel)=>channel switch {CurveChannel.LeftStick=>"L Stick",CurveChannel.RightStick=>"R Stick",CurveChannel.LeftTrigger=>"LT",_=>"RT"};
}

internal sealed class CurvePointMorph : AnimationTimeline
{
    private readonly PointCollection origin,destination;
    public CurvePointMorph(PointCollection from,PointCollection to)
    {
        origin=from.Clone();destination=to.Clone();origin.Freeze();destination.Freeze();Duration=new(TimeSpan.FromMilliseconds(160));FillBehavior=FillBehavior.Stop;
    }
    public override Type TargetPropertyType=>typeof(PointCollection);
    protected override Freezable CreateInstanceCore()=>new CurvePointMorph(origin,destination);
    public override object GetCurrentValue(object defaultOriginValue,object defaultDestinationValue,AnimationClock clock)
    {
        double t=clock.CurrentProgress??1;t=1-Math.Pow(1-t,3);
        return new PointCollection(origin.Select((p,i)=>new Point(p.X+(destination[i].X-p.X)*t,p.Y+(destination[i].Y-p.Y)*t)));
    }
}
