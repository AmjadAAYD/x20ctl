using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Frontend illustration and local draft editor. No codec, live input or device writes.</summary>
public sealed class CurveWorkbench
{
    public Grid Inspector { get; } = new();
    public Grid Shelf { get; } = new();
    public CurveDraft Draft { get; }
    public CurveChannel Channel { get; private set; }
    public IEnumerable<Button> Buttons => channelButtons.Concat(presetButtons);
    public IEnumerable<Thumb> PointHandles => new[]{first,second};
    public IEnumerable<Point> PreviewPoints => response.Points;
    public Rect PlotBounds => chart.TransformToAncestor(Inspector).TransformBounds(new Rect(38,16,408,240));
    public int? SelectedPoint { get; private set; }
    public string PresetDescription => summary.Text;
    public TextBlock OutputAxisLabel { get; } = new() { Text="OUTPUT %",FontSize=12,Foreground=Brushes.LightSteelBlue };
    public TextBlock PreviewNotice { get; } = new() { Text="Preview · Actual controller response may differ until verified.",FontSize=14,Foreground=Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap,ToolTip="The plotted Hermite curve is illustrative. Firmware interpolation has not yet been verified." };
    public TextBlock ActivePointReadout { get; } = new() { FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap };
    private readonly TextBlock editHint = new() { Text="← / →  1% steps · Shift  5% steps",FontSize=13,Foreground=Brushes.LightSteelBlue,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom };
    public event Action<CurveChannel>? ChannelSelected;
    public event Action? Changed;
    private readonly List<Button> channelButtons = new(), presetButtons = new();
    private readonly Canvas chart = new() { Width=480, Height=300 };
    private readonly Polyline response = new() { Stroke=new SolidColorBrush(Color.FromRgb(116,200,255)), StrokeThickness=3.5 };
    private readonly Rectangle innerBand = new() { Fill=new SolidColorBrush(Color.FromArgb(35,70,120,230)) }, outerBand = new() { Fill=new SolidColorBrush(Color.FromArgb(30,150,165,192)) };
    private readonly Thumb first = new(), second = new();
    private readonly TextBlock title = new() { FontSize=28,FontWeight=FontWeights.SemiBold }, summary = new() { FontSize=15, Foreground=Brushes.LightSteelBlue }, coordinates = new() { FontSize=14,Foreground=Brushes.LightSteelBlue };
    public Slider Inner { get; } = new() { Minimum=0,Maximum=99,TickFrequency=1,IsSnapToTickEnabled=true };
    public Slider Outer { get; } = new() { Minimum=1,Maximum=100,Value=100,TickFrequency=1,IsSnapToTickEnabled=true };
    private readonly TextBlock innerText=new() { FontSize=16 }, outerText=new() { FontSize=16 };
    private bool updating;
    public bool AnimationsEnabled { get; }
    public CurveWorkbench(CurveDraft draft,FrameworkElement resources,bool animations=true)
    {
        Draft=draft;AnimationsEnabled=animations;
        Inspector.RowDefinitions.Add(new(){Height=new GridLength(62)});Inspector.RowDefinitions.Add(new());Inspector.RowDefinitions.Add(new(){Height=new GridLength(64)});
        var heading=new StackPanel();heading.Children.Add(title);heading.Children.Add(summary);Inspector.Children.Add(heading);
        var view=new Viewbox { Child=chart,Stretch=Stretch.Uniform };Grid.SetRow(view,1);Inspector.Children.Add(view);
        var note=new StackPanel();note.Children.Add(coordinates);note.Children.Add(ActivePointReadout);PreviewNotice.Margin=new(0,6,0,0);note.Children.Add(PreviewNotice);Grid.SetRow(note,2);Inspector.Children.Add(note);
        Inspector.SizeChanged+=(_,_)=>RefreshPointReadout();
        foreach(var band in new[]{innerBand,outerBand}) {band.Height=240;Canvas.SetTop(band,16);chart.Children.Add(band);}
        foreach(int tick in new[]{0,25,50,75,100})
        {
            double x=38+tick*4.08,y=256-tick*2.4;
            chart.Children.Add(new Line {X1=x,X2=x,Y1=16,Y2=256,Stroke=new SolidColorBrush(Color.FromArgb(42,132,160,202)),StrokeThickness=1});
            chart.Children.Add(new Line {X1=38,X2=446,Y1=y,Y2=y,Stroke=new SolidColorBrush(Color.FromArgb(42,132,160,202)),StrokeThickness=1});
            var horizontal=new TextBlock {Text=tick.ToString(),FontSize=12,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(horizontal,x-7);Canvas.SetTop(horizontal,262);chart.Children.Add(horizontal);
            var vertical=new TextBlock {Text=tick.ToString(),FontSize=12,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(vertical,4);Canvas.SetTop(vertical,y-8);chart.Children.Add(vertical);
        }
        chart.Children.Add(new Line {X1=38,Y1=256,X2=446,Y2=16,Stroke=new SolidColorBrush(Color.FromArgb(55,200,216,244)),StrokeDashArray=new(){4,4}});
        chart.Children.Add(response);
        Canvas.SetLeft(OutputAxisLabel,38);Canvas.SetTop(OutputAxisLabel,0);chart.Children.Add(OutputAxisLabel);
        var input=new TextBlock {Text="INPUT %",FontSize=12,Foreground=Brushes.LightSteelBlue};Canvas.SetLeft(input,206);Canvas.SetTop(input,284);chart.Children.Add(input);
        for(int index=0;index<2;index++)
        {
            Thumb thumb=index==0?first:second;thumb.Width=thumb.Height=18;thumb.Tag=index;thumb.Focusable=true;thumb.FocusVisualStyle=null;thumb.Cursor=Cursors.SizeAll;
            var factory=new FrameworkElementFactory(typeof(Ellipse));factory.SetValue(Shape.FillProperty,index==0?Brushes.LightSkyBlue:new SolidColorBrush(Color.FromRgb(232,238,248)));factory.SetValue(Shape.StrokeProperty,Brushes.White);factory.SetValue(Shape.StrokeThicknessProperty,2d);thumb.Template=new ControlTemplate(typeof(Thumb)){VisualTree=factory};
            System.Windows.Automation.AutomationProperties.SetName(thumb,$"Curve point {index+1}, arrow keys adjust input and output");
            int pointIndex=index;
            thumb.GotKeyboardFocus+=(_,_)=>SelectPoint(pointIndex);
            thumb.PreviewMouseLeftButtonDown+=(_,_)=>{thumb.Focus();SelectPoint(pointIndex);};
            thumb.LostKeyboardFocus+=(_,e)=>{if(e.NewFocus!=first && e.NewFocus!=second) {SelectedPoint=null;RefreshPointReadout();}};
            thumb.DragDelta+=(_,e)=>{var point=index==0?Draft[Channel].Point1:Draft[Channel].Point2;SetPoint(index,point.X+(int)Math.Round(e.HorizontalChange/408*255),point.Y-(int)Math.Round(e.VerticalChange/240*255));};chart.Children.Add(thumb);
        }
        Shelf.RowDefinitions.Add(new(){Height=new GridLength(76)});Shelf.RowDefinitions.Add(new());
        var rail=new Grid();rail.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});rail.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Shelf.Children.Add(rail);
        var channels=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};rail.Children.Add(channels);
        foreach(var channel in Enum.GetValues<CurveChannel>())
        {
            var button=new Button {Content=ShortName(channel),Tag=channel,Width=102,Height=48,Style=(Style)resources.FindResource("ConsoleTarget"),Margin=new(0,0,9,0),ToolTip=Name(channel)};
            button.Click+=(_,_)=>SelectChannel(channel);channelButtons.Add(button);channels.Children.Add(button);
        }
        var presets=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(presets,1);rail.Children.Add(presets);
        foreach(string preset in CurveDraft.Presets.Keys)
        {
            var button=new Button {Content=char.ToUpper(preset[0])+preset[1..],Tag=preset,Style=(Style)resources.FindResource("ConsoleAction"),Padding=new(12,0,12,0),Margin=new(5,0,0,0),IsEnabled=Draft.CanEdit};button.Click+=(_,_)=>SetPreset(preset);presetButtons.Add(button);presets.Children.Add(button);
        }
        var zones=new Grid {Margin=new(0,0,0,10)};zones.ColumnDefinitions.Add(new());zones.ColumnDefinitions.Add(new());Grid.SetRow(zones,1);Shelf.Children.Add(zones);
        foreach(var (slider,text,column) in new[]{(Inner,innerText,0),(Outer,outerText,1)})
        {
            var panel=new StackPanel {Margin=new(10,0,24,0)};panel.Children.Add(text);slider.IsEnabled=Draft.CanEdit;slider.Style=(Style)resources.FindResource("ConsoleCurveSlider");slider.Margin=new(0,8,0,0);panel.Children.Add(slider);Grid.SetColumn(panel,column);zones.Children.Add(panel);
            System.Windows.Automation.AutomationProperties.SetName(slider,column==0?"Inner deadzone percent":"Outer travel limit percent");
            slider.ToolTip="← / → adjusts 1 percentage point. Hold Shift for 5-point steps.";
            slider.ValueChanged+=(_,_)=>{if(updating)return;var value=Draft[Channel];Draft.Set(Channel,column==0?value with {Preset="custom",Inner=Math.Min((int)Inner.Value,value.Outer-1)}:value with {Preset="custom",Outer=Math.Max((int)Outer.Value,value.Inner+1)});Refresh();Changed?.Invoke();};
        }
        Grid.SetRow(editHint,1);Shelf.Children.Add(editHint);
        Refresh();
    }
    public void SelectChannel(CurveChannel channel) {Channel=channel;SelectedPoint=null;Refresh();ChannelSelected?.Invoke(channel);}
    private void SelectPoint(int index) {SelectedPoint=index;RefreshPointReadout();}
    public void SetPreset(string preset) {Draft.SetPreset(Channel,preset);Refresh();Changed?.Invoke();}
    public void Reset() {Draft.Reset(Channel);Refresh();Changed?.Invoke();}
    public void SetPoint(int index,int x,int y)
    {
        if(!Draft.CanEdit)return;var setting=Draft[Channel];
        x=Math.Clamp(x,index==0?1:setting.Point1.X+1,index==0?setting.Point2.X-1:254);y=Math.Clamp(y,0,255);
        Draft.Set(Channel,index==0?setting with {Preset="custom",Point1=(x,y)}:setting with {Preset="custom",Point2=(x,y)});Refresh();Changed?.Invoke();
    }
    public bool HandleKey(KeyEventArgs e)
    {
        if(Keyboard.FocusedElement is Thumb thumb && (thumb==first || thumb==second) && thumb.Tag is int index && Draft.CanEdit)
        {
            var p=index==0?Draft[Channel].Point1:Draft[Channel].Point2;
            if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down) {SetPoint(index,p.X+(e.Key==Key.Right?1:e.Key==Key.Left?-1:0),p.Y+(e.Key==Key.Up?1:e.Key==Key.Down?-1:0));e.Handled=true;return true;}
        }
        if(Keyboard.FocusedElement is Slider slider && (slider==Inner || slider==Outer) && slider.IsEnabled && e.Key is Key.Left or Key.Right) {int step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?5:1;slider.Value+=e.Key==Key.Right?step:-step;e.Handled=true;return true;}
        return false;
    }
    public void Refresh()
    {
        updating=true;var setting=Draft[Channel];title.Text=Name(Channel);string basis=char.ToUpper(setting.BasePreset[0])+setting.BasePreset[1..];summary.Text=setting.Preset=="custom"?$"Custom · based on {basis} · Modified":$"Base preset: {basis} · {(setting==CurveSetting.Default?"Unmodified":"Modified")}";
        Inner.Value=setting.Inner;Outer.Value=setting.Outer;innerText.Text=$"Inner deadzone   {setting.Inner}%";outerText.Text=$"Outer travel limit   {setting.Outer}%";
        innerBand.Width=setting.Inner*4.08;Canvas.SetLeft(innerBand,38);outerBand.Width=(100-setting.Outer)*4.08;Canvas.SetLeft(outerBand,38+setting.Outer*4.08);
        var previous=response.Points.Clone();response.BeginAnimation(Polyline.PointsProperty,null);
        response.Points=new(Enumerable.Range(0,101).Select(i=>new Point(38+i*4.08,256-Illustrate(setting,i/100d*255)/255*240)));
        bool morph=AnimationsEnabled && !first.IsDragging && !second.IsDragging;
        if(morph && previous.Count==response.Points.Count) response.BeginAnimation(Polyline.PointsProperty,new CurvePointMorph(previous,response.Points));
        foreach(var (thumb,p) in new[]{(first,setting.Point1),(second,setting.Point2)})
        {
            double previousX=Canvas.GetLeft(thumb),previousY=Canvas.GetTop(thumb);thumb.BeginAnimation(Canvas.LeftProperty,null);thumb.BeginAnimation(Canvas.TopProperty,null);
            double x=38+p.X/255d*408-9,y=256-p.Y/255d*240-9;Canvas.SetLeft(thumb,x);Canvas.SetTop(thumb,y);thumb.IsEnabled=Draft.CanEdit;
            if(morph && double.IsFinite(previousX)) {thumb.BeginAnimation(Canvas.LeftProperty,new DoubleAnimation(previousX,x,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});thumb.BeginAnimation(Canvas.TopProperty,new DoubleAnimation(previousY,y,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});}
        }
        coordinates.Text=$"Point 1 — Input {setting.Point1.X/2.55:0}% · Output {setting.Point1.Y/2.55:0}%\nPoint 2 — Input {setting.Point2.X/2.55:0}% · Output {setting.Point2.Y/2.55:0}%";
        coordinates.ToolTip=$"Stored point 1: {setting.Point1.X}/255, {setting.Point1.Y}/255. Point 2: {setting.Point2.X}/255, {setting.Point2.Y}/255.";
        coordinates.TextWrapping=TextWrapping.Wrap;RefreshPointReadout();
        foreach(var button in channelButtons)button.Background=(CurveChannel)button.Tag==Channel?new SolidColorBrush(Color.FromRgb(38,120,237)):new SolidColorBrush(Color.FromArgb(117,24,35,55));
        foreach(var button in presetButtons)button.Background=(string)button.Tag==setting.Preset?new SolidColorBrush(Color.FromRgb(38,120,237)):new SolidColorBrush(Color.FromArgb(27,255,255,255));
        updating=false;
    }
    private void RefreshPointReadout()
    {
        bool active=SelectedPoint.HasValue;
        coordinates.Visibility=active?Visibility.Collapsed:Visibility.Visible;
        ActivePointReadout.Visibility=active?Visibility.Visible:Visibility.Collapsed;
        Inspector.RowDefinitions[^1].Height=new(!active && Inspector.ActualWidth>0 && Inspector.ActualWidth<400?80:64);
        editHint.Text=active?$"POINT {SelectedPoint+1}    ← / → Input    ↑ / ↓ Output":"← / →  1% steps · Shift  5% steps";
        if(active)
        {
            var point=SelectedPoint==0?Draft[Channel].Point1:Draft[Channel].Point2;
            ActivePointReadout.Text=$"POINT {SelectedPoint+1}\nInput {point.X/2.55:0}% · Output {point.Y/2.55:0}%";
            ActivePointReadout.ToolTip=$"Stored point coordinates: input {point.X}/255, output {point.Y}/255.";
        }
    }
    // Drawing interpolation only, matching the reference's illustrative Hermite approach.
    private static double Illustrate(CurveSetting setting,double x)
    {
        var knots=new[]{(X:0d,Y:0d),(X:(double)setting.Point1.X,Y:(double)setting.Point1.Y),(X:(double)setting.Point2.X,Y:(double)setting.Point2.Y),(X:255d,Y:255d)}.OrderBy(p=>p.X).GroupBy(p=>p.X).Select(g=>g.Last()).ToArray();
        var deltas=Enumerable.Range(0,knots.Length-1).Select(i=>(knots[i+1].Y-knots[i].Y)/(knots[i+1].X-knots[i].X)).ToArray();
        var slopes=Enumerable.Range(0,knots.Length).Select(i=>i==0?deltas[0]:i==knots.Length-1?deltas[^1]:deltas[i-1]*deltas[i]<=0?0:(deltas[i-1]+deltas[i])/2).ToArray();
        for(int i=0;i<deltas.Length;i++){double d=deltas[i];if(d==0){slopes[i]=slopes[i+1]=0;continue;}double a=slopes[i]/d,b=slopes[i+1]/d;if(a*a+b*b>9){double scale=3/Math.Sqrt(a*a+b*b);slopes[i]=scale*a*d;slopes[i+1]=scale*b*d;}}
        if(x<=knots[0].X)return knots[0].Y;if(x>=knots[^1].X)return knots[^1].Y;
        int segment=Enumerable.Range(0,knots.Length-1).First(i=>x>=knots[i].X&&x<knots[i+1].X);double span=knots[segment+1].X-knots[segment].X,t=(x-knots[segment].X)/span,t2=t*t,t3=t2*t;
        return (2*t3-3*t2+1)*knots[segment].Y+(t3-2*t2+t)*span*slopes[segment]+(-2*t3+3*t2)*knots[segment+1].Y+(t3-t2)*span*slopes[segment+1];
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
