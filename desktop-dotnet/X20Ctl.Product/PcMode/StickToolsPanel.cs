using System.IO;
using X20Ctl.Product.Scanner;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>What the Studio needs to reach PC mode: its settings file, the running service and the saved choices.</summary>
public sealed record PcModeLink(PcModeStore Store, PcModeService Service, ProductPreferences? Preferences);

/// <summary>
/// Curves → Advanced (owner request, 11 Oct 2026): the fine stick tools. Dead zones in 0.1% steps, a square gate, the
/// up/down speed, and a number table of what the game gets. PC mode applies them on this PC for any controller, EasySMX
/// or not; nothing is written to the controller. They replace PC mode's whole-percent stick dead zones.
/// </summary>
public sealed class StickToolsPanel : Border
{
    static readonly (string Name, CurveChannel Channel)[] Sticks = [("Left stick", CurveChannel.LeftStick), ("Right stick", CurveChannel.RightStick)];
    static readonly int[] TablePoints = [5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
    readonly PcModeLink link;
    sealed record Row(Slider Inner, Slider Outer, Slider Anti, Slider Max, CheckBox Square, CheckBox Raw, Slider Ratio, TextBlock Table, Panel Tuning);
    readonly Dictionary<CurveChannel, Row> rows = new();
    readonly Dictionary<CurveChannel,Border> cards=new();
    readonly Dictionary<CurveChannel,Canvas> charts=new();
    readonly CheckBox linkSticks=new(){Style=DS.Style("DS.Toggle")};
    readonly Button undoCopy=Pill("Undo copy"),applyDrift=Pill("Apply recommendations");
    CurveChannel selectedStick=CurveChannel.LeftStick;
    PcModeProfile? undoProfile;
    (DriftObservation Left,DriftObservation Right)? sampled;
    readonly CancellationTokenSource samplingCancel=new();
    bool live;double lx,ly,rx,ry;
    /// <summary>One-click starting points: (name, tooltip, inner, outer, anti-deadzone, max output). Null keeps that value.</summary>
    static readonly (string Name, string Tip, double? Inner, double? Outer, double? Anti, double? Max)[] Presets =
    [
        ("Linear range", "No dead zones, no floor, full output", 0, 100, 0, 100),
        ("Dead zone 5%", "Set the inner dead zone to 5%", 5, null, null, null),
        ("Full at 80%", "Reaches full speed a little early, for steadier aiming at the edge", null, 80, null, null),
        ("Full at 65%", "Full speed very early, for fast turns", null, 65, null, null),
        ("Anti-deadzone 8%", "Small moves register in games with a dead zone of their own", null, null, 8, null),
        ("Anti-deadzone 15%", "For games with a large dead zone of their own", null, null, 15, null),
        ("Anti-deadzone off", "No output floor", null, null, 0, null),
    ];
    readonly TextBlock status, saved;
    readonly Border notice;
    bool loading, pending;
    readonly Button measure;
    public Button Done { get; }

    public StickToolsPanel(PcModeLink link, Action close)
    {
        this.link = link;
        CornerRadius = new(22); BorderThickness = new(1); Padding = new(40, 30, 40, 30); MaxWidth = 1080; HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        Background = new LinearGradientBrush(Color.FromArgb(250, 14, 24, 52), Color.FromArgb(250, 8, 14, 34), 90);
        BorderBrush = new LinearGradientBrush(Color.FromArgb(120, 120, 170, 255), Color.FromArgb(40, 120, 170, 255), 90);
        var stack=new StackPanel();var footer=new StackPanel();var body=new Grid();body.RowDefinitions.Add(new());body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=570});Grid.SetRow(footer,1);body.Children.Add(footer);Child=body;
        stack.Children.Add(Kit.Text("Advanced stick tools", 30, "DS.Text", FontWeights.Bold));
        stack.Children.Add(Wrap("PC-side processing for accessible analog sticks · Nothing is written to controller memory. Settings save locally as you edit; sliders adjust in 0.1% steps.", 15, "DS.TextSoft", 8));
        status = Wrap("", 14, "DS.TextMuted", 10); stack.Children.Add(status);

        notice = new Border { CornerRadius = new(14), Padding = new(20, 16, 20, 16), Margin = new(0, 16, 0, 0), BorderThickness = new(1), Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(70, 120, 80, 10)), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 245, 200, 80)) };
        var accept = Pill("I understand"); accept.Margin = new(0, 12, 0, 0); accept.HorizontalAlignment = HorizontalAlignment.Left; accept.Click += (_, _) => Accept();
        notice.Child = new StackPanel { Children = { Kit.Text("Before PC mode starts", 17, "DS.Text", FontWeights.SemiBold),
            Wrap(PcModePage.NoticeLines[0], 14.5, "DS.TextSoft", 8), Wrap(PcModePage.NoticeLines[1], 14.5, "DS.TextSoft", 4), Wrap(PcModePage.NoticeLines[2], 14.5, "DS.TextSoft", 4), accept } };
        stack.Children.Add(notice);

        var selector=new WrapPanel{Margin=new(0,14,0,0)};
        foreach(var (stickName,stickChannel) in Sticks){var channel=stickChannel;var pick=Pill(stickName);pick.Margin=new(0,0,8,0);pick.Click+=(_,_)=>SelectStick(channel);selector.Children.Add(pick);}
        selector.Children.Add(linkSticks);selector.Children.Add(Label("Link stick settings",10));stack.Children.Add(selector);
        linkSticks.Checked+=(_,_)=>{if(rows.Count==2)Changed();};
        undoCopy.Visibility=Visibility.Collapsed;undoCopy.Click+=(_,_)=>{if(undoProfile==null)return;Save(undoProfile);Load();undoCopy.Visibility=Visibility.Collapsed;if(saved!=null)saved.Text="Copy undone.";};stack.Children.Add(undoCopy);

        foreach (var (name, channel) in Sticks)
        {
            var inner = Fine(0, StickTuning.MaxInner, 0); var outer = Fine(StickTuning.MinOuter, 100, 100);
            var anti = Fine(0, StickTuning.MaxAnti, 0); var max = Fine(StickTuning.MinOuter, 100, 100);
            var innerText = Value("0.0%"); var outerText = Value("100.0%"); var antiText = Value("0.0%"); var maxText = Value("100.0%");
            var square = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
            var rawBox = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
            var ratio = new Slider { Minimum = StickTuning.MinRatio, Maximum = StickTuning.MaxRatio, Width = 170, Value = 100, TickFrequency = 5, IsSnapToTickEnabled = true, SmallChange = 5, LargeChange = 10, VerticalAlignment = VerticalAlignment.Center };
            var ratioText = Value("100%");
            var table = Kit.Text("", 12.5, "DS.TextMuted").Also(t => { t.Margin = new(0, 10, 0, 0); t.TextWrapping=TextWrapping.Wrap;t.TextTrimming=TextTrimming.None; });
            inner.ValueChanged += (_, _) => { innerText.Text = $"{inner.Value:0.0}%"; Changed(); };
            outer.ValueChanged += (_, _) => { outerText.Text = $"{outer.Value:0.0}%"; Changed(); };
            anti.ValueChanged += (_, _) => { antiText.Text = $"{anti.Value:0.0}%"; Changed(); };
            max.ValueChanged += (_, _) => { maxText.Text = $"{max.Value:0.0}%"; Changed(); };
            square.Checked += (_, _) => Changed(); square.Unchecked += (_, _) => Changed();
            rawBox.Checked += (_, _) => Changed(); rawBox.Unchecked += (_, _) => Changed();
            ratio.ValueChanged += (_, _) => { ratioText.Text = $"{ratio.Value:0}%"; Changed(); };
            var other = channel == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick;
            var copy = Pill(channel == CurveChannel.LeftStick ? "Copy to right stick" : "Copy to left stick"); copy.Height = 34; copy.Padding = new(14, 0, 16, 0);
            copy.Click += (_, _) => CopyStick(channel, other); DockPanel.SetDock(copy, Dock.Right);
            var title = new DockPanel { Children = { copy, Kit.Text(name, 18, "DS.Text", FontWeights.SemiBold) } };
            var tuning = new StackPanel { Children = {
                Kit.Text("INPUT RANGE",12,"DS.TextSoft",FontWeights.SemiBold),
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), Children = {
                    Label("Inner dead zone").Also(Wide), inner, innerText, Label("Full output at",22).Also(Wide), outer, outerText } },
                Kit.Text("OUTPUT RANGE",12,"DS.TextSoft",FontWeights.SemiBold).Also(t=>t.Margin=new(0,10,0,0)),
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 6, 0, 0), Children = {
                    Label("Minimum output").Also(Wide).Also(t => t.ToolTip = "The least the game gets once the stick leaves the dead zone, so small moves register in games with a dead zone of their own"), anti, antiText,
                    Label("Maximum output",22).Also(Wide).Also(t => t.ToolTip = "The most the game gets at full push"), max, maxText } },
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = {
                    square, Kit.Text("Gate: ○ Circular / □ Square", 14, "DS.TextSoft").Also(t => { t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; t.ToolTip = "Diagonals reach full on both axes, as on a square-gated stick"; }),
                    Label("Vertical sensitivity", 30).Also(t => t.ToolTip = "Scales the Y-axis output relative to X. It applies to analog stick input, after the response curve."), ratio, ratioText } },
                PresetRow(channel) } };
            rows[channel] = new(inner, outer, anti, max, square, rawBox, ratio, table, tuning);
            var card=new Border { CornerRadius = new(14), Padding = new(20, 16, 20, 16), Margin = new(0, 18, 0, 0), Background = new SolidColorBrush(Color.FromArgb(90, 4, 8, 20)),
                Child = new StackPanel { Children = {
                    title,
                    new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = {
                        rawBox, Kit.Text("Bypass X20CTL processing", 14, "DS.TextSoft").Also(t => { t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; }),
                        Kit.Text("Uses the XInput values before X20CTL curves, range and gate adjustments. This is not raw HID input.", 13, "DS.TextMuted").Also(t => t.Margin = new(12, 0, 0, 0)) } },
                    tuning, Preview(channel),table } } };cards[channel]=card;stack.Children.Add(card);
        }

        var reset = Pill("Reset selected advanced settings"); reset.Margin = new(12, 0, 0, 0);
        reset.Click += (_, _) => { loading = true; foreach (var r in rows.Where(pair=>pair.Key==selectedStick||linkSticks.IsChecked==true).Select(pair=>pair.Value)) { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100; } loading = false; Changed(); };
        measure = Pill("Measure drift"); measure.Margin = new(12, 0, 0, 0); measure.ToolTip = "Release both sticks for 5 seconds. Review observed centre deviation before applying recommendations.";
        measure.Click += async (_, _) => await MeasureDrift();
        Done = Pill("Done"); Done.Click += (_, _) => close();
        footer.Children.Add(new WrapPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0), Children = { Done, measure, reset,applyDrift } });
        saved = Wrap("", 13.5, "DS.TextMuted", 10); footer.Children.Add(saved);

        applyDrift.Visibility=Visibility.Collapsed;applyDrift.Click+=(_,_)=>ApplyMeasured();
        Load();SelectStick(selectedStick);Unloaded+=(_,_)=>samplingCancel.Cancel();
        Action refresh = () => Dispatcher.BeginInvoke(ShowStatus);
        Loaded += (_, _) => { link.Service.Changed -= refresh; link.Service.Changed += refresh; ShowStatus(); };
        Unloaded += (_, _) => link.Service.Changed -= refresh;
        ShowStatus();
    }

    void Load()
    {
        loading = true;
        var p = link.Store.Get(PcModePage.StoreKey);
        foreach (var (_, channel) in Sticks)
        {
            var r = rows[channel]; var curve = p.Curves.GetValueOrDefault(channel.ToString());
            var t = p.Sticks.GetValueOrDefault(channel.ToString());
            // with no fine settings yet, start from PC mode's whole-percent dead zones so nothing jumps
            r.Inner.Value = t?.Inner ?? curve?.Inner ?? 0; r.Outer.Value = t?.Outer ?? curve?.Outer ?? 100;
            r.Anti.Value = t?.AntiDeadzone ?? 0; r.Max.Value = t?.MaxOutput ?? 100;
            r.Square.IsChecked = t?.SquareGate == true; r.Raw.IsChecked = t?.Raw == true; r.Ratio.Value = t?.AxisRatio ?? 100;
        }
        loading = false; UpdateTables(p);
        foreach (var r in rows.Values) r.Tuning.IsEnabled = r.Raw.IsChecked != true;
    }

    PcModeProfile Build()
    {
        var p = link.Store.Get(PcModePage.StoreKey); p.Sticks = new();
        foreach (var (channel, r) in rows)
        {
            var t = new StickTuning { Inner = Math.Round(r.Inner.Value, 1), Outer = Math.Round(r.Outer.Value, 1), AntiDeadzone = Math.Round(r.Anti.Value, 1), MaxOutput = Math.Round(r.Max.Value, 1),
                SquareGate = r.Square.IsChecked == true, Raw = r.Raw.IsChecked == true, AxisRatio = (int)Math.Round(r.Ratio.Value) };
            // the outer and max sliders start at 60%, the inner and anti ones end at 40%, so these only clash at the seams
            if (t.Inner >= t.Outer) t.Inner = Math.Round(t.Outer - .1, 1); if (t.AntiDeadzone >= t.MaxOutput) t.AntiDeadzone = Math.Round(t.MaxOutput - .1, 1);
            if (!t.IsDefault) p.Sticks[channel.ToString()] = t;
        }
        return p;
    }

    void Changed()
    {
        if (loading) return;
        if(linkSticks.IsChecked==true){loading=true;CopyValues(rows[selectedStick],rows[selectedStick==CurveChannel.LeftStick?CurveChannel.RightStick:CurveChannel.LeftStick]);loading=false;}
        foreach (var r in rows.Values) r.Tuning.IsEnabled = r.Raw.IsChecked != true;
        var p = Build(); UpdateTables(p);
        if (!p.IsNeutral && link.Preferences is { PcModeNoticeSeen: false })
        {
            pending = true; notice.Visibility = Visibility.Visible; saved.Text = "Read the notice above and press \"I understand\" to start PC mode."; return;
        }
        Save(p);
    }

    void Save(PcModeProfile p)
    {
        try { p.Validate(); link.Store.Save(PcModePage.StoreKey, p); link.Service.Reload(); saved.Text = link.Service.State==PcModeState.Active?"Saved · Applied on this PC":"Saved locally · PC routing is inactive"; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Not saved: " + error.Message; }
    }

    void Accept()
    {
        try { link.Preferences?.MarkPcModeNoticeSeen(); } catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException) { saved.Text = "Couldn't remember this: " + error.Message; }
        notice.Visibility = Visibility.Collapsed;
        if (pending) { pending = false; Save(Build()); }
    }

    void ShowStatus()
    {
        status.Text=PcModeSetup.Ready?(link.Service.State==PcModeState.Active?"PC route active":"PC routing inactive · Changes save locally"):"PC mode is not set up · Open PC mode → Set up PC mode to enable software processing.";
        if (PcModePage.ProfileKey != X20Ctl.Zone.PcMode.PcModeStore.DefaultKey)
            status.Text += $" Editing {link.Store.Get(PcModePage.StoreKey).Name ?? "this game"}'s own settings (picked on the PC mode tab).";
    }

    /// <summary>How far the stick is pushed (straight, not diagonal), and what the game gets.</summary>
    void UpdateTables(PcModeProfile p)
    {
        foreach (var (channel, r) in rows)
        {
            var curve = p.Curves.GetValueOrDefault(channel.ToString()); var t = p.Sticks.GetValueOrDefault(channel.ToString());
            r.Table.Text=t?.Raw==true?"Direct input · X20CTL processing bypassed":live?"Physical input and processed output shown above":"Live input unavailable · Connect the selected player to compare input and output";
            DrawPreview(channel,p);

        }
    }

    StackPanel PresetRow(CurveChannel channel)
    {
        var groups=new StackPanel{Margin=new(0,12,0,0)};
        foreach(var group in new[]{("Dead zone",new[]{0d,3d,5d}), ("Full output at",new[]{100d,80d,65d}), ("Minimum output",new[]{0d,8d,15d})})
        {
            var row=new WrapPanel();row.Children.Add(Label(group.Item1));foreach(double number in group.Item2){double n=number;string field=group.Item1;var button=Pill(n+"%");button.Height=30;button.Margin=new(0,0,6,6);button.Click+=(_,_)=>{var r=rows[channel];if(field=="Dead zone")r.Inner.Value=n;else if(field=="Full output at")r.Outer.Value=n;else r.Anti.Value=n;};row.Children.Add(button);}groups.Children.Add(row);
        }
        var linear=Pill("Reset range to 1:1");linear.Height=32;linear.HorizontalAlignment=HorizontalAlignment.Left;linear.Click+=(_,_)=>{loading=true;var r=rows[channel];r.Inner.Value=r.Anti.Value=0;r.Outer.Value=r.Max.Value=100;loading=false;Changed();};groups.Children.Add(linear);return groups;
    }

    void CopyStick(CurveChannel from, CurveChannel to)
    {
        undoProfile=link.Store.Get(PcModePage.StoreKey);loading=true;CopyValues(rows[from],rows[to]);loading=false;Changed();undoCopy.Visibility=Visibility.Visible;saved.Text=$"{StickName(from)} settings copied to {StickName(to)}.";
    }

    /// <summary>Centre guard: three seconds of readings with the sticks left alone. Each stick's inner dead zone goes just
    /// past the farthest it wandered from the centre (plus 0.5%), so drift and jitter never reach the game. Read-only.</summary>
    async Task MeasureDrift()
    {
        measure.IsEnabled=false;applyDrift.Visibility=Visibility.Collapsed;saved.Text="Release both sticks · Sampling 0 / 5 seconds";
        try{sampled=await Diagnostics.DriftCapture.Measure(PcModePage.Player,new Progress<double>(v=>saved.Text=$"Release both sticks · Sampling {v*5:0.0} / 5 seconds"),samplingCancel.Token);
            if(sampled is not {} result){saved.Text="Selected player input unavailable. Connect it and try again.";return;}
            if(!result.Left.Stable||!result.Right.Stable){saved.Text="A stick moved during sampling. Release both sticks and try again.";return;}
            saved.Text=$"Observed peak deviation · Left {result.Left.Peak:0.0}% · Right {result.Right.Peak:0.0}%. Suggested inner dead zone · Left {result.Left.RecommendedInner:0.0}% · Right {result.Right.RecommendedInner:0.0}%. Review before applying.";applyDrift.Visibility=Visibility.Visible;
        }catch(OperationCanceledException){}catch(Exception error) when(error is IOException or ArgumentException){saved.Text=error.Message;}finally{measure.IsEnabled=true;}
    }
    void ApplyMeasured(){if(sampled is not {} result)return;loading=true;rows[CurveChannel.LeftStick].Inner.Value=result.Left.RecommendedInner;rows[CurveChannel.RightStick].Inner.Value=result.Right.RecommendedInner;loading=false;Changed();applyDrift.Visibility=Visibility.Collapsed;}
    static string StickName(CurveChannel c)=>c==CurveChannel.LeftStick?"Left stick":"Right stick";
    static void CopyValues(Row a,Row b){b.Inner.Value=a.Inner.Value;b.Outer.Value=a.Outer.Value;b.Anti.Value=a.Anti.Value;b.Max.Value=a.Max.Value;b.Square.IsChecked=a.Square.IsChecked;b.Raw.IsChecked=a.Raw.IsChecked;b.Ratio.Value=a.Ratio.Value;}
    public void SelectStick(CurveChannel c){selectedStick=c;foreach(var pair in cards)pair.Value.Visibility=pair.Key==c?Visibility.Visible:Visibility.Collapsed;UpdateTables(Build());}
    UIElement Preview(CurveChannel channel){var canvas=new Canvas{Width=720,Height=165,ClipToBounds=true};charts[channel]=canvas;return new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=165,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,14,0,0)};}
    public void ShowLive(double leftX,double leftY,double rightX,double rightY){live=true;lx=leftX;ly=leftY;rx=rightX;ry=rightY;UpdateTables(Build());}
    public void ClearLive(){live=false;UpdateTables(Build());}
    void DrawPreview(CurveChannel channel,PcModeProfile profile)
    {
        if(!charts.TryGetValue(channel,out var canvas))return;canvas.Children.Clear();var curve=profile.Curves.GetValueOrDefault(channel.ToString());var tuning=profile.Sticks.GetValueOrDefault(channel.ToString());
        var line=new Polyline{Stroke=Brushes.LightSkyBlue,StrokeThickness=2,Points=new PointCollection(Enumerable.Range(0,101).Select(v=>new Point(30+v*3.1,138-PadProcessor.Response(curve,tuning,v/100d)*105)))};
        canvas.Children.Add(new Line{X1=30,X2=340,Y1=138,Y2=138,Stroke=Brushes.SlateGray});canvas.Children.Add(new Line{X1=30,X2=30,Y1=33,Y2=138,Stroke=Brushes.SlateGray});canvas.Children.Add(line);Put("Physical input → Game output",30,5);
        double x=channel==CurveChannel.LeftStick?lx:rx,y=channel==CurveChannel.LeftStick?ly:ry;
        var previewProfile=new PcModeProfile();if(curve!=null)previewProfile.Curves[channel.ToString()]=curve;if(tuning!=null)previewProfile.Sticks[channel.ToString()]=tuning;
        static short Raw(double n)=>(short)Math.Clamp(Math.Round(n*32767),-32768,32767);
        var output=new PadProcessor(previewProfile).Process(channel==CurveChannel.LeftStick?new PadState(0,0,0,Raw(x),Raw(y),0,0):new PadState(0,0,0,0,0,Raw(x),Raw(y)),0);
        double ox=(channel==CurveChannel.LeftStick?output.LeftX:output.RightX)/32768d,oy=(channel==CurveChannel.LeftStick?output.LeftY:output.RightY)/32768d;
        foreach(var (center,label,dx,dy) in new[]{(440d,"Physical",x,y),(605d,"Processed",ox,oy)})
        {Shape gate=center>500&&tuning?.SquareGate==true?new Rectangle{Width=84,Height=84}:new Ellipse{Width=84,Height=84};gate.Stroke=Brushes.SlateGray;gate.StrokeThickness=1;Canvas.SetLeft(gate,center-42);Canvas.SetTop(gate,45);canvas.Children.Add(gate);Put(label,center-34,10);
         if(center<500){foreach(var radius in new[]{(tuning?.Inner??0)/100*42,(tuning?.Outer??100)/100*42}){var ring=new Ellipse{Width=radius*2,Height=radius*2,Stroke=Brushes.LightSkyBlue,StrokeThickness=1};Canvas.SetLeft(ring,center-radius);Canvas.SetTop(ring,87-radius);canvas.Children.Add(ring);}}
         if(live){var dot=new Ellipse{Width=8,Height=8,Fill=Brushes.White};Canvas.SetLeft(dot,center+dx*42-4);Canvas.SetTop(dot,87-dy*42-4);canvas.Children.Add(dot);Put($"{dx:P0}, {dy:P0}",center-40,141);}}
        void Put(string text,double left,double top){var label=Kit.Text(text,13,"DS.TextSoft");Canvas.SetLeft(label,left);Canvas.SetTop(label,top);canvas.Children.Add(label);}
    }

    static void Wide(TextBlock t) => t.Width = 165;
    static Slider Fine(double min, double max, double value) => new() { Style=DS.Style("DS.Slider"),Minimum = min, Maximum = max, Value = value, Width = 160, TickFrequency = .1, IsSnapToTickEnabled = true, SmallChange = .1, LargeChange = 1, VerticalAlignment = VerticalAlignment.Center };
    static TextBlock Value(string text) => Kit.Text(text, 14, "DS.TextSoft").Also(t => { t.Width = 62; t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; });
    static TextBlock Label(string text, double left = 0) => Kit.Text(text, 14, "DS.TextSoft").Also(t => { t.Margin = new(left, 0, 10, 0); t.VerticalAlignment = VerticalAlignment.Center; });
    static TextBlock Wrap(string text, double size, string brush, double top) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
    static Button Pill(string text) => new() { Style = DS.Style("ActionButton"), Height = 44, MinHeight = 0, Padding = new(22, 0, 24, 0), Content = Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) };
}
