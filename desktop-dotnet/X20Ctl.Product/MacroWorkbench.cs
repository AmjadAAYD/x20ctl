using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Local sequence UI. Preview moves a playhead only; no input injection or packets.
/// Layout (10 Oct 2026): the back paddles on the real rear controller at the left, the sequence (event cards over
/// a lane timeline) at the right, and the event editor underneath, laid out like the Buttons page outputs shelf.</summary>
public sealed class MacroWorkbench
{
    public Grid Body { get; } = new();
    public Grid Dock { get; } = new();
    public MacroDraft Draft { get; }
    public string Slot { get; private set; } = "M1";
    public int SelectedIndex { get; private set; } = -1;
    public bool IsPreviewing => timer.IsEnabled;
    public int PreviewIndex {get;private set;}=-1;
    public bool PreviewIsPause {get;private set;}
    public string CapacityDescription=>summary.Text;
    public double EventShelfOffset=>sequenceViewport.HorizontalOffset;
    public bool SelectedCardInView=>SelectedIndex>=0 && CardInView(SelectedIndex);
    public bool PlayheadVisible=>playhead.Visibility==Visibility.Visible;
    public int HighlightedLaneCount=>laneBlocks.Count(b=>!ReferenceEquals(b.Shape.Fill,b.Normal));
    public Button AddEventButton=>add;
    public Button PreviewButton=>preview;
    public IEnumerable<Button> EventButtons=>steps;
    public IEnumerable<Button> InputButtons=>chords;
    public bool IsReducedMotion { get; }
    public event Action? Changed;
    private readonly FrameworkElement resources;
    private readonly Action<Button> focusRegistration;
    private readonly List<Button> slots = new(), steps = new(), chords = new();
    private readonly List<RadioButton> editorTabs=new();
    private readonly Dictionary<string,(Border Card,TextBlock Count)> slotCards=new();
    private readonly Grid inputsPage=new(),timingPage=new(),actionsPage=new(),buttonsPage=new(),sticksPage=new();
    private readonly StickDirectionSelector leftSelector=new(),rightSelector=new();
    private readonly TextBlock selectedInputs=new(){FontSize=13.5,Foreground=DS.Brush("DS.TextSoft"),TextTrimming=TextTrimming.CharacterEllipsis};
    public string EditorMode {get;private set;}="Inputs";
    public bool ShowingSticks {get;private set;}
    public StickDirectionSelector LeftSelector=>leftSelector;
    public StickDirectionSelector RightSelector=>rightSelector;
    private readonly Dictionary<string,PhysicalHotspot> paddles = new();
    private readonly Canvas timelineCanvas = new();
    private readonly Canvas laneLabels=new(){IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Top};
    private readonly ScrollViewer timeline = new() { HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
    private readonly ScrollViewer sequenceViewport = new() { HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
    private readonly List<(Rectangle Shape,int Event,int Lane,Brush Normal)> laneBlocks=new();
    private readonly StackPanel sequence = new() { Orientation=Orientation.Horizontal,Margin=new(0,8,0,8) };
    private readonly TextBlock heading = new() { FontFamily=DS.Display,FontSize=26,FontWeight=FontWeights.SemiBold }, summary = new() { FontSize=14,Foreground=DS.Brush("DS.TextSoft") }, notice = new() { FontSize=13.5,Foreground=DS.Brush("DS.TextSoft"),TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center };
    private readonly TextBlock eventTitle = new() { FontFamily=DS.Display,FontSize=19,FontWeight=FontWeights.SemiBold };
    private readonly StackPanel empty = new() { VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center };
    private readonly Border noEvent;
    private readonly TextBox hold = new(), pause = new();
    private readonly Button add,remove,earlier,later,clear,preview;
    private Button duplicate=null!;
    public Button Record { get; }
    private readonly Line playhead = new() { Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false,Effect=new DropShadowEffect {Color=Color.FromRgb(129,213,255),BlurRadius=10,ShadowDepth=0,Opacity=.8} };
    private readonly Stopwatch clock = new();
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromMilliseconds(16) };
    private double scale=1;
    private double cardWidth=190,cardHeight=56,zoom=1;
    private double Stride=>cardWidth+10;
    private readonly TextBlock zoomLabel=new(){Text="100%",FontSize=13.5,FontWeight=FontWeights.SemiBold,Width=48,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private bool updating;
    private Window? previewWindow;
    private static readonly Color PauseTone=Color.FromRgb(150,165,192);
    private const string ReadOnly="Macros for this controller are a preview until its settings are verified. Nothing is stored or sent.";
    public MacroWorkbench(MacroDraft draft,ControllerLayout layout,FrameworkElement owner,Action<Button> register,bool reducedMotion,string model="x20")
    {
        Draft=draft;resources=owner;focusRegistration=register;IsReducedMotion=reducedMotion;
        // page: [paddles | sequence] above the event editor
        Body.RowDefinitions.Add(new());Body.RowDefinitions.Add(new(){Height=new GridLength(14)});Body.RowDefinitions.Add(new(){Height=new GridLength(262)});
        var top=new Grid();top.ColumnDefinitions.Add(new(){Width=new GridLength(380)});top.ColumnDefinitions.Add(new(){Width=new GridLength(16)});top.ColumnDefinitions.Add(new());Body.Children.Add(top);

        // left: the four back paddles on the real (relit) rear controller, large enough to read
        var art=new Grid {Width=1536,Height=1024};art.Children.Add(new PhotoController(model,true));var map=new Canvas();art.Children.Add(map);
        foreach(var region in layout.Controls.Where(r=>r.Role=="macro")) {var shape=new PhysicalHotspot(region,!reducedMotion,true){ToolTip=$"{region.Key} paddle"};Canvas.SetLeft(shape,region.Bounds.X*1536);Canvas.SetTop(shape,region.Bounds.Y*1024);shape.Click+=(_,_)=>SelectSlot(region.Key);map.Children.Add(shape);paddles.Add(region.Key,shape);}
        var rear=new Viewbox {Child=art,Stretch=Stretch.Uniform,Margin=new(-18,-6,-18,0)};
        var rail=new UniformGrid {Rows=1,Margin=new(-4,10,-4,0)};
        // each model shows its own back buttons (the draft holds M1 to M4)
        var own=ModelProfiles.For(model).Paddles.Where(MacroDraft.Slots.Contains).ToArray();
        foreach(string slot in own.Length>0?own:MacroDraft.Slots) rail.Children.Add(SlotCard(slot));
        var paddleBody=new DockPanel();DockPanel.SetDock(rail,System.Windows.Controls.Dock.Bottom);paddleBody.Children.Add(rail);paddleBody.Children.Add(rear);
        top.Children.Add(Kit.Panel("Back paddles","Pick the paddle that plays this sequence",null,paddleBody));

        // right: heading and actions, event cards, lane timeline, footer
        var page=new Grid();page.RowDefinitions.Add(new(){Height=GridLength.Auto});page.RowDefinitions.Add(new(){Height=GridLength.Auto});page.RowDefinitions.Add(new());page.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel();page.Children.Add(header);
        add=Button("+ Add event",Add,"DS.ActionPrimary");preview=Button("Preview timeline",Preview,"DS.ActionSecondary");
        foreach(var b in new[]{add,preview}){b.Height=44;b.MinHeight=0;b.Padding=new(22,0,22,0);b.FontSize=15;b.Margin=new(0,0,10,0);}
        preview.ToolTip="Plays the timeline on screen only. Nothing is sent to the controller or to Windows.";
        var more=new Button{Content="",Style=DS.Style("DS.ActionRound"),Width=44,Height=44,MinHeight=0,ToolTip="More macro actions"};focusRegistration(more);var menu=new ContextMenu();
        var clearMenu=new MenuItem {Header="Clear this slot"};clearMenu.Click+=(_,_)=>Guard(Clear);menu.Items.Add(clearMenu);menu.Items.Add(new MenuItem {Header="Recording unavailable",IsEnabled=false});more.ContextMenu=menu;more.Click+=(_,_)=>{clearMenu.IsEnabled=Draft[Slot].Count>0;menu.PlacementTarget=more;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;};
                Record=new Button{Style=DS.Style("DS.ActionSecondary"),Height=44,MinHeight=0,Padding=new(18,0,20,0),Margin=new(0,0,10,0),ToolTip="Play the sequence on your controller and it becomes this slot's events (read-only input; nothing is sent)",
            Content=new StackPanel{Orientation=Orientation.Horizontal,Children={new Ellipse{Width=11,Height=11,Fill=new SolidColorBrush(Color.FromRgb(255,72,96)),Margin=new(0,0,10,0),VerticalAlignment=VerticalAlignment.Center,Effect=new DropShadowEffect{Color=Color.FromRgb(255,72,96),BlurRadius=10,ShadowDepth=0,Opacity=.9}},new TextBlock{Text="Record",FontFamily=DS.Display,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=DS.Brush("DS.Text"),VerticalAlignment=VerticalAlignment.Center}}}};
        Record.Click+=(_,_)=>ToggleRecording();focusRegistration(Record);Record.IsEnabled=Draft.CanEdit;
        var read=new Button{Style=DS.Style("DS.ActionSecondary"),Height=44,MinHeight=0,Padding=new(18,0,20,0),Margin=new(0,0,10,0),ToolTip="Read the macro stored on the controller into this slot",
            Content=new StackPanel{Orientation=Orientation.Horizontal,Children={Kit.Icon("\uE896",14,"DS.Text").Also(i=>i.Margin=new(0,0,10,0)),new TextBlock{Text="Read controller",FontFamily=DS.Display,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=DS.Brush("DS.Text"),VerticalAlignment=VerticalAlignment.Center}}}};
        read.Click+=(_,_)=>notice.Text="Reading the stored macro needs the controller's configuration link, which X20CTL can't open yet (it only reads ordinary input today). The X20's read-back command is known and will be used once that link exists. Use Record to capture a sequence now.";focusRegistration(read);
        var workspaceActions=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Children={Record,read,add,preview,more}};DockPanel.SetDock(workspaceActions,System.Windows.Controls.Dock.Right);header.Children.Add(workspaceActions);
        header.Children.Add(new StackPanel {VerticalAlignment=VerticalAlignment.Center,Children={heading,summary}});
        sequenceViewport.Content=sequence;sequenceViewport.Height=76;Grid.SetRow(sequenceViewport,1);page.Children.Add(sequenceViewport);
        sequenceViewport.PreviewMouseWheel+=(_,e)=>{if(steps.Count>0)Select(Math.Clamp(SelectedIndex-Math.Sign(e.Delta),0,steps.Count-1));e.Handled=true;};
        sequenceViewport.ScrollChanged+=(_,_)=>MaskPartialCards();
        timeline.Content=timelineCanvas;
        // the lane names sit on their own rounded chips in a column that fades into the timeline behind it
        var laneColumn=new Border {Width=124,HorizontalAlignment=HorizontalAlignment.Left,IsHitTestVisible=false,CornerRadius=new(12,0,0,12),Child=laneLabels,
            Background=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(250,11,19,42),0),new GradientStop(Color.FromArgb(250,11,19,42),.86),new GradientStop(Color.FromArgb(0,11,19,42),1)},new Point(0,0),new Point(1,0))};
        var addFirst=Button("+ Add first event",Add,"DS.ActionPrimary");addFirst.Height=46;addFirst.MinHeight=0;addFirst.Padding=new(26,0,26,0);addFirst.FontSize=15;addFirst.HorizontalAlignment=HorizontalAlignment.Center;addFirst.IsEnabled=Draft.CanEdit;addFirst.Margin=new(0,16,0,0);
        var ring=new Grid {Width=58,Height=58,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,0,0,12),Children={new Ellipse {Fill=new RadialGradientBrush(Color.FromArgb(70,61,139,255),Color.FromArgb(0,61,139,255)),Margin=new(-16)},new Ellipse {Stroke=new SolidColorBrush(Color.FromArgb(150,125,182,255)),StrokeThickness=1.5,Fill=new SolidColorBrush(Color.FromArgb(40,30,60,130))},Kit.Icon("",22,"DS.AccentHi").Also(i=>i.HorizontalAlignment=HorizontalAlignment.Center)}};
        empty.Children.Add(ring);empty.Children.Add(Kit.Text("No events in this sequence yet",20,"DS.Text",FontWeights.SemiBold).Also(t=>t.HorizontalAlignment=HorizontalAlignment.Center));
        empty.Children.Add(Kit.Text("An event is what the paddle presses, and for how long. Add one, then pick its buttons below.",14,"DS.TextSoft").Also(t=>{t.HorizontalAlignment=HorizontalAlignment.Center;t.Margin=new(0,4,0,0);}));empty.Children.Add(addFirst);
        var timelineFrame=new Border {CornerRadius=new(12),Margin=new(0,12,0,0),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(30,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(14,255,255,255)),Padding=new(0,6,0,0),
            Child=new Grid {Children={timeline,laneColumn,empty}}};Grid.SetRow(timelineFrame,2);page.Children.Add(timelineFrame);
        var timeFooter=new DockPanel {Margin=new(2,10,0,0)};var zoomTools=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(zoomTools,System.Windows.Controls.Dock.Right);timeFooter.Children.Add(zoomTools);
        zoomTools.Children.Add(Kit.Text("Timeline zoom",13.5,"DS.TextSoft").Also(t=>t.Margin=new(0,0,10,0)));
        foreach(var (glyph,delta,tip) in new[]{("",-.5,"Zoom out: fit more of the sequence"),("",.5,"Zoom in: stretch short events so they are easier to read")})
        {var button=new Button{Content=glyph,Style=DS.Style("DS.ActionRound"),Width=32,Height=32,MinHeight=0,FontSize=13,ToolTip=tip};button.Click+=(_,_)=>SetZoom(zoom+delta);focusRegistration(button);zoomTools.Children.Add(button);if(delta<0)zoomTools.Children.Add(zoomLabel);}
        timeFooter.Children.Add(notice);Grid.SetRow(timeFooter,3);page.Children.Add(timeFooter);
        var right=Kit.Panel(null,null,null,page);Grid.SetColumn(right,2);top.Children.Add(right);

        remove=Button("Delete event",Remove);earlier=Button("← Move earlier",()=>Move(-1));later=Button("Move later →",()=>Move(1));clear=Button("Clear slot",Clear);

        noEvent=Kit.Info("Add an event above, then choose what it presses here.");noEvent.HorizontalAlignment=HorizontalAlignment.Center;noEvent.VerticalAlignment=VerticalAlignment.Center;noEvent.Margin=new(0);
        BuildDock();
        var dockPanel=Kit.Panel(null,null,null,Dock,new(20,12,20,12));Grid.SetRow(dockPanel,2);Body.Children.Add(dockPanel);
        timer.Tick+=(_,_)=>{if(clock.ElapsedMilliseconds>=Draft.Duration(Slot))StopPreview();else UpdatePlayhead();};
        timeline.SizeChanged+=(_,_)=>RenderTimeline();sequenceViewport.SizeChanged+=(_,_)=>UpdateCardMetrics();
        Body.IsVisibleChanged+=(_,_)=>{if(!Body.IsVisible){StopPreview();if(IsRecording)StopRecording(true);}};Body.Unloaded+=(_,_)=>{StopPreview();if(previewWindow!=null)previewWindow.Deactivated-=Deactivated;previewWindow=null;};
        Body.Loaded+=(_,_)=>{if(previewWindow==null && Window.GetWindow(Body) is Window window){previewWindow=window;window.Deactivated+=Deactivated;}};
        Refresh();
    }
    private Button SlotCard(string slot)
    {
        var count=Kit.Text("Empty",12,"DS.TextSoft").Also(t=>t.HorizontalAlignment=HorizontalAlignment.Center);
        var card=new Border {CornerRadius=new(12),BorderThickness=new(1),Height=58,Margin=new(4,0,4,0),Child=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Children={Kit.Text(slot,19,"DS.Text",FontWeights.SemiBold).Also(t=>t.HorizontalAlignment=HorizontalAlignment.Center),count}}};
        var button=new Button {Content=card,Template=Bare(),Tag=slot,Cursor=Cursors.Hand,FocusVisualStyle=null,ToolTip=$"Edit the {slot} paddle sequence"};
        button.Click+=(_,_)=>Guard(()=>SelectSlot(slot));button.GotKeyboardFocus+=(_,_)=>card.BorderBrush=Brushes.White;button.LostKeyboardFocus+=(_,_)=>PaintSlots();
        focusRegistration(button);slots.Add(button);slotCards[slot]=(card,count);return button;
    }
    private void PaintSlots()
    {
        foreach(var (slot,(card,count)) in slotCards)
        {
            bool on=slot==Slot;int events=Draft[slot].Count;
            count.Text=events==0?"Empty":$"{events} event{(events==1?"":"s")}";
            card.Background=on?new LinearGradientBrush(Color.FromRgb(58,138,255),Color.FromRgb(30,98,232),90):new SolidColorBrush(Color.FromArgb(24,255,255,255));
            card.BorderBrush=on?new SolidColorBrush(Color.FromArgb(200,220,235,255)):new SolidColorBrush(Color.FromArgb(40,255,255,255));
            card.Effect=on?new DropShadowEffect {Color=Kit.Blue,BlurRadius=20,ShadowDepth=0,Opacity=.7}:null;
            count.Foreground=on?DS.Brush("DS.Text"):DS.Brush("DS.TextSoft");
        }
    }
    private void BuildDock()
    {
        Dock.RowDefinitions.Add(new(){Height=new GridLength(50)});Dock.RowDefinitions.Add(new());
        var headingRow=new DockPanel();Dock.Children.Add(headingRow);
        var tabs=new UniformGrid {Rows=1,Width=440};string group="macro"+Guid.NewGuid().ToString("N");
        foreach(var (name,mode,sticks) in new[]{("Buttons","Inputs",false),("Sticks","Inputs",true),("Timing","Timing",false),("Actions","Actions",false)})
        {
            var tab=new RadioButton {Content=name,Style=DS.Style("DS.Segment"),GroupName=group,Tag=(mode,sticks)};
            tab.Click+=(_,_)=>{if(mode=="Inputs")ShowInputKind(sticks);ShowEditor(mode);};editorTabs.Add(tab);tabs.Children.Add(tab);
        }
        var track=new Border {CornerRadius=new(12),Background=new SolidColorBrush(Color.FromArgb(30,255,255,255)),BorderBrush=new SolidColorBrush(Color.FromArgb(34,255,255,255)),BorderThickness=new(1),Padding=new(1),Child=tabs,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(track,System.Windows.Controls.Dock.Right);headingRow.Children.Add(track);
        var context=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,16,0)};context.Children.Add(eventTitle);context.Children.Add(selectedInputs);headingRow.Children.Add(context);
        foreach(var page in new[]{inputsPage,timingPage,actionsPage}){Grid.SetRow(page,1);Dock.Children.Add(page);}
        Grid.SetRow(noEvent,1);Dock.Children.Add(noEvent);
        inputsPage.Children.Add(buttonsPage);inputsPage.Children.Add(sticksPage);

        // the same spatial shelf as the Buttons page outputs: face diamond, shoulders, D-pad cross, sticks / system, special
        var columns=new (string Name,double Weight)[]{("FACE",1),("SHOULDERS",1.15),("D-PAD",1),("STICKS / SYSTEM",1.25),("SPECIAL",.6)};
        foreach(var (_,weight) in columns)buttonsPage.ColumnDefinitions.Add(new(){Width=new GridLength(weight,GridUnitType.Star)});
        for(int i=0;i<columns.Length;i++)
        {
            var column=new DockPanel {Margin=new(0,6,0,0)};Grid.SetColumn(column,i);buttonsPage.Children.Add(column);
            var label=Kit.Text(columns[i].Name,12.5,"DS.TextSoft",FontWeights.SemiBold);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new(0,0,0,8);DockPanel.SetDock(label,System.Windows.Controls.Dock.Top);column.Children.Add(label);
            if(i<columns.Length-1){var rule=new Border {Width=1,HorizontalAlignment=HorizontalAlignment.Right,Background=new SolidColorBrush(Color.FromArgb(26,255,255,255)),Margin=new(0,10,0,6)};Grid.SetColumn(rule,i);buttonsPage.Children.Add(rule);}
            column.Children.Add(i switch
            {
                0=>Diamond(true,("Y","Y"),("X","X"),("B","B"),("A","A")),
                1=>Pairs(("LB","LB"),("RB","RB"),("LT","LT"),("RT","RT")),
                2=>Diamond(false,("DPAD_UP","↑"),("DPAD_LEFT","←"),("DPAD_RIGHT","→"),("DPAD_DOWN","↓")),
                3=>Pairs(("L3","L3"),("R3","R3"),("SELECT","View"),("START","Menu")),
                _=>Guide()
            });
        }
        sticksPage.ColumnDefinitions.Add(new());sticksPage.ColumnDefinitions.Add(new());
        foreach(var (selector,label,column) in new[]{(leftSelector,"Left stick",0),(rightSelector,"Right stick",1)})
        {
            var panel=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(panel,column);sticksPage.Children.Add(panel);
            var description=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,28,0)};description.Children.Add(Kit.Text(label,19,"DS.Text",FontWeights.SemiBold));description.Children.Add(Kit.Text("Pick one of 8 directions, or the centre for none",13.5,"DS.TextSoft"));description.Children.Add(Kit.Text("Arrows choose · Space centres",13,"DS.TextMuted").Also(t=>t.Margin=new(0,6,0,0)));panel.Children.Add(description);panel.Children.Add(selector);focusRegistration(selector);System.Windows.Automation.AutomationProperties.SetName(selector,label+" programmed direction");
            int side=column;selector.DirectionChanged+=value=>{if(!updating)Guard(()=>SetDirection(side,value));};
        }
        var timings=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};timingPage.Children.Add(timings);
        foreach(var (label,detail,box,isPause) in new[]{("Hold","How long the inputs stay pressed",hold,false),("Pause afterward","The gap before the next event",pause,true)})
        {
            var panel=new StackPanel {Margin=new(30,0,30,0)};panel.Children.Add(Kit.Text(label,18,"DS.Text",FontWeights.SemiBold));panel.Children.Add(Kit.Text(detail,13.5,"DS.TextSoft"));var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new(0,12,0,0)};
            var minus=Button("− 5",()=>AdjustTime(isPause,-5));var plus=Button("+ 5",()=>AdjustTime(isPause,5));minus.Width=plus.Width=58;row.Children.Add(minus);box.Width=104;box.Height=44;box.FontSize=22;box.TextAlignment=TextAlignment.Center;box.Margin=new(10,0,10,0);row.Children.Add(box);row.Children.Add(plus);row.Children.Add(Kit.Text("  ms",16,"DS.TextSoft"));panel.Children.Add(row);timings.Children.Add(panel);System.Windows.Automation.AutomationProperties.SetName(box,label+" milliseconds");box.LostKeyboardFocus+=(_,_)=>CommitTiming();
        }
        duplicate=Button("Duplicate",Duplicate);
        var eventActions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};actionsPage.Children.Add(eventActions);foreach(var button in new[]{earlier,later,duplicate,remove}){button.MinWidth=160;button.Height=46;button.Margin=new(10,0,10,0);eventActions.Children.Add(button);}
        ShowInputKind(false);
    }
    private Canvas Diamond(bool round,(string Key,string Label) top,(string Key,string Label) left,(string Key,string Label) right,(string Key,string Label) bottom)
    {
        double s=round?46:44,w=160,h=136;var c=new Canvas {Width=w,Height=h,HorizontalAlignment=HorizontalAlignment.Center};
        void Put((string Key,string Label) k,double x,double y){var o=Chord(k.Key,k.Label,round,s,s);Canvas.SetLeft(o,x);Canvas.SetTop(o,y);c.Children.Add(o);}
        Put(top,(w-s)/2,0);Put(left,0,(h-s)/2);Put(right,w-s,(h-s)/2);Put(bottom,(w-s)/2,h-s);
        return c;
    }
    private UniformGrid Pairs(params (string Key,string Label)[] items)
    {
        var u=new UniformGrid {Columns=2,Rows=2,Width=210,Height=116,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        foreach(var (k,l) in items){var o=Chord(k,l,false,0,0);o.Margin=new(5);u.Children.Add(o);}
        return u;
    }
    private static FrameworkElement Guide()
    {
        var face=new Border {Width=92,Height=46,CornerRadius=new(12),BorderThickness=new(1.2),BorderBrush=new SolidColorBrush(Color.FromArgb(46,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(30,255,255,255)),Opacity=.45,
            Child=Kit.Text("Guide",17,"DS.Text",FontWeights.SemiBold).Also(t=>t.HorizontalAlignment=HorizontalAlignment.Center)};
        var holder=new Border {Child=face,Background=Brushes.Transparent,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,ToolTip="Guide can't be part of a sequence."};
        ToolTipService.SetShowOnDisabled(holder,true);return holder;
    }
    private Button Chord(string key,string text,bool round,double w,double h)
    {
        var face=new TextBlock {Text=text,FontFamily=DS.Display,FontSize=round?19:17,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var border=new Border {Child=face,CornerRadius=new(round?w/2:12),BorderThickness=new(1.2)};
        var button=new Button {Content=border,Template=Bare(),Cursor=Cursors.Hand,Tag=key,FocusVisualStyle=null,ToolTip=$"Press {Friendly(key)} during this event"};
        if(w>0){button.Width=w;button.Height=h;}
        button.Click+=(_,_)=>Guard(()=>Toggle(key));focusRegistration(button);
        button.GotKeyboardFocus+=(_,_)=>border.BorderBrush=Brushes.White;button.LostKeyboardFocus+=(_,_)=>PaintChord(button);
        chords.Add(button);PaintChord(button);return button;
    }
    private void PaintChord(Button button)
    {
        string key=(string)button.Tag;var border=(Border)button.Content;var face=(TextBlock)border.Child;
        bool selected=SelectedIndex>=0 && SelectedIndex<Draft[Slot].Count,on=selected && Draft[Slot][SelectedIndex].Buttons.Contains(key);
        var color=key switch {"Y"=>Color.FromRgb(245,205,80),"X"=>Color.FromRgb(70,170,255),"B"=>Color.FromRgb(255,100,125),"A"=>Color.FromRgb(70,220,150),_=>(Color?)null};
        button.IsEnabled=selected;border.Opacity=selected?1:.45;border.BorderThickness=new(on?2:1.2);
        border.Background=on?new LinearGradientBrush(Color.FromRgb(70,150,255),Color.FromRgb(31,104,240),90):new SolidColorBrush(Color.FromArgb(30,255,255,255));
        border.BorderBrush=button.IsKeyboardFocused?Brushes.White:on?Brushes.White:color!=null?new SolidColorBrush(Color.FromArgb(150,color.Value.R,color.Value.G,color.Value.B)):new SolidColorBrush(Color.FromArgb(46,255,255,255));
        face.Foreground=on||color==null?DS.Brush("DS.Text"):new SolidColorBrush(color.Value);
        border.Effect=on?new DropShadowEffect {Color=Kit.Blue,BlurRadius=22,ShadowDepth=0,Opacity=.9}:null;
    }
    public void ShowEditor(string mode)
    {
        EditorMode=mode;bool selected=SelectedIndex>=0;
        inputsPage.Visibility=selected && mode=="Inputs"?Visibility.Visible:Visibility.Collapsed;timingPage.Visibility=selected && mode=="Timing"?Visibility.Visible:Visibility.Collapsed;actionsPage.Visibility=selected && mode=="Actions"?Visibility.Visible:Visibility.Collapsed;
        noEvent.Visibility=selected?Visibility.Collapsed:Visibility.Visible;
        foreach(var tab in editorTabs){var (m,sticks)=((string,bool))tab.Tag;tab.IsEnabled=selected;tab.IsChecked=m==mode && (m!="Inputs" || sticks==ShowingSticks);}
    }
    public void ShowInputKind(bool sticks)
    {
        ShowingSticks=sticks;buttonsPage.Visibility=sticks?Visibility.Collapsed:Visibility.Visible;sticksPage.Visibility=sticks?Visibility.Visible:Visibility.Collapsed;
        foreach(var tab in editorTabs){var (m,s)=((string,bool))tab.Tag;tab.IsChecked=m==EditorMode && (m!="Inputs" || s==sticks);}
    }
    private void AdjustTime(bool isPause,int delta){if(SelectedIndex<0)return;var step=Draft[Slot][SelectedIndex];Update(isPause?step with {PauseMs=Math.Clamp(step.PauseMs+delta,0,327675)}:step with {HoldMs=Math.Clamp(step.HoldMs+delta,0,327675)});}
    private void UpdateEventSelection(){for(int i=0;i<steps.Count;i++){bool selected=i==SelectedIndex,active=IsPreviewing && i==PreviewIndex;steps[i].Background=active?new SolidColorBrush(Color.FromRgb(51,127,190)):Fill(selected);steps[i].BorderBrush=active?Brushes.White:selected?Brushes.LightSkyBlue:new SolidColorBrush(Color.FromArgb(30,255,255,255));steps[i].BorderThickness=new(selected||active?1.5:1);if(!steps[i].IsKeyboardFocused)steps[i].Effect=selected||active?new DropShadowEffect {Color=Color.FromRgb(46,119,217),BlurRadius=18,ShadowDepth=3,Opacity=.45}:null;}}
    private Button Button(string text,Action action,string style="ConsoleAction") {var button=new Button {Content=text,Style=(Style)resources.FindResource(style)};button.Click+=(_,_)=>Guard(action);focusRegistration(button);return button;}
    private void Guard(Action action) {try {action();}catch(Exception e) when(e is ArgumentException or InvalidOperationException){notice.Text=e.Message;}}
    public void SelectSlot(string slot) {StopPreview();Slot=slot;SelectedIndex=Draft[slot].Count==0?-1:0;Refresh();Changed?.Invoke();}
    public void Select(int index) {SelectedIndex=index>=0 && index<Draft[Slot].Count?index:-1;RefreshEditor();RenderTimeline();UpdateEventSelection();SnapTo(index);SnapTimeline(index);}
    public void Add() {if(!Draft.CanEdit){notice.Text=ReadOnly;return;}if(Draft.EntryCount(Slot)>=47){notice.Text="Maximum sequence capacity reached.";return;}StopPreview();Draft.Add(Slot,Draft.EntryCount(Slot)==46?0:20);SelectedIndex=Draft[Slot].Count-1;Refresh();Changed?.Invoke();}
    public void Remove() {if(SelectedIndex<0)return;StopPreview();Draft.Remove(Slot,SelectedIndex);SelectedIndex=Math.Min(SelectedIndex,Draft[Slot].Count-1);Refresh();Changed?.Invoke();}
    public void Move(int offset) {if(SelectedIndex<0)return;StopPreview();int target=Math.Clamp(SelectedIndex+offset,0,Draft[Slot].Count-1);Draft.Move(Slot,SelectedIndex,target);SelectedIndex=target;Refresh();Changed?.Invoke();}
    public void Clear() {StopPreview();Draft.Clear(Slot);SelectedIndex=-1;Refresh();Changed?.Invoke();}
    public void Duplicate() {if(SelectedIndex<0)return;StopPreview();Draft.Duplicate(Slot,SelectedIndex);SelectedIndex++;Refresh();Changed?.Invoke();}
    public void Update(MacroEvent step) {if(SelectedIndex<0)return;StopPreview();Draft.Update(Slot,SelectedIndex,step);Refresh();Changed?.Invoke();}
    private void Toggle(string key) {if(SelectedIndex<0)return;var step=Draft[Slot][SelectedIndex];var keys=step.Buttons.Contains(key)?step.Buttons.Where(b=>b!=key).ToArray():step.Buttons.Append(key).ToArray();Update(step with {Buttons=keys});}
    private void SetDirection(int side,StickHeading value) {if(SelectedIndex<0)return;var step=Draft[Slot][SelectedIndex];Update(side==0?step with {Left=value}:step with {Right=value});}
    private void CommitTiming()
    {
        if(updating || SelectedIndex<0)return;
        Guard(()=>{if(!int.TryParse(hold.Text,out int h)||!int.TryParse(pause.Text,out int p))throw new ArgumentException("Enter whole milliseconds in 5 ms increments.");Update(Draft[Slot][SelectedIndex] with {HoldMs=h,PauseMs=p});});
    }
    // ----- Record: play it on the controller -----
    private MacroRecorder? recorder; private DispatcherTimer? recordTimer; private int countdown;
    public bool IsRecording => recorder != null || countdown > 0;
    private void SetRecordLabel(string text){if(Record.Content is StackPanel {Children.Count:2} s && s.Children[1] is TextBlock t)t.Text=text;}
    public void ToggleRecording()
    {
        if(IsRecording){StopRecording(true);return;}
        if(!Draft.CanEdit){notice.Text=ReadOnly;return;}
        StopPreview();countdown=3;SetRecordLabel("Stop");
        recordTimer=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(1000)};
        notice.Text=$"Get ready… {countdown}   Hold the controller; recording starts when the count ends.";
        recordTimer.Tick+=(_,_)=>
        {
            if(countdown>0){countdown--;if(countdown>0){notice.Text=$"Get ready… {countdown}";return;}
                try{recorder=new MacroRecorder();recorder.Stopped+=()=>Body.Dispatcher.BeginInvoke(new Action(()=>StopRecording(false)));recorder.Start();}
                catch(Exception error) when(error is IOException or ArgumentException){recorder=null;recordTimer?.Stop();SetRecordLabel("Record");notice.Text="Can't read the controller: "+error.Message;return;}
                recordTimer!.Interval=TimeSpan.FromMilliseconds(100);}
            if(recorder==null)return;
            notice.Text=recorder.Slot<0?"● Recording · press the first button on your controller":$"● Recording on player slot {recorder.Slot+1} · holding {recorder.Holding} · {recorder.Elapsed/1000:0.0} s · stops after a 2.5 s rest";
        };
        recordTimer.Start();
    }
    private void StopRecording(bool byHand)
    {
        recordTimer?.Stop();recordTimer=null;countdown=0;SetRecordLabel("Record");
        var r=recorder;recorder=null;if(r==null){notice.Text="Recording cancelled.";return;}
        r.Stop();var events=r.Events();r.Dispose();
        if(events.Count==0){notice.Text="Nothing was pressed, so the slot is unchanged.";return;}
        // keep inside the 47-entry limit: each hold is one entry, a following pause another
        var kept=new List<MacroEvent>();int entries=0;foreach(var e in events){int cost=1+(e.PauseMs>0?1:0);if(entries+cost>47)break;kept.Add(e);entries+=cost;}
        if(kept.Count>0)kept[^1]=kept[^1] with {PauseMs=kept[^1].PauseMs};
        try
        {
            Draft.Clear(Slot);for(int i=0;i<kept.Count;i++){Draft.Add(Slot,0);Draft.Update(Slot,i,kept[i]);}
            SelectedIndex=0;Refresh();Changed?.Invoke();
            notice.Text=$"Recorded {kept.Count} event{(kept.Count==1?"":"s")} from player slot {r.Slot+1} into {Slot}{(kept.Count<events.Count?$" · {events.Count-kept.Count} more didn't fit the 47-entry limit":"")}.";
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException){notice.Text="The recording couldn't be stored: "+error.Message;Refresh();}
    }
    public void Preview() {if(IsPreviewing){StopPreview();return;}if(Draft.Duration(Slot)<=0)return;clock.Restart();timer.Start();preview.Content="Stop preview";notice.Text="Timeline preview only · No controller or Windows input is emitted.";UpdatePlayhead();}
    public void StopPreview() {timer.Stop();clock.Stop();PreviewIndex=-1;PreviewIsPause=false;preview.Content="Preview timeline";playhead.Visibility=Visibility.Collapsed;UpdateEventSelection();UpdateLaneHighlight();if(SelectedIndex>=0)SnapTo(SelectedIndex);}
    private void Deactivated(object? sender,EventArgs e)=>StopPreview();
    public void FocusSelection() {if(SelectedIndex>=0 && SelectedIndex<steps.Count)steps[SelectedIndex].Focus();else slots.Single(b=>Equals(b.Tag,Slot)).Focus();}
    public bool HandleKey(KeyEventArgs e)
    {
        if(ReferenceEquals(Keyboard.FocusedElement,hold)||ReferenceEquals(Keyboard.FocusedElement,pause)) {if(e.Key==Key.Enter){CommitTiming();e.Handled=true;}return e.Key!=Key.Escape && e.Key!=Key.F6;}
        if(Keyboard.FocusedElement is StickDirectionSelector selector && (selector==leftSelector || selector==rightSelector))return selector.HandleKey(e);
        if(e.Key==Key.Delete){Guard(Remove);e.Handled=true;return true;}
        if(Keyboard.FocusedElement is Button button && steps.Contains(button) && e.Key is Key.Left or Key.Right)
        {if((Keyboard.Modifiers&ModifierKeys.Alt)!=0){Guard(()=>Move(e.Key==Key.Right?1:-1));FocusSelection();}else {int next=Math.Clamp(SelectedIndex+(e.Key==Key.Right?1:-1),0,steps.Count-1);Select(next);steps[next].Focus();}e.Handled=true;return true;}
        return false;
    }
    public void Refresh()
    {
        if(SelectedIndex>=Draft[Slot].Count)SelectedIndex=Draft[Slot].Count-1;
        heading.Text=Slot+" · Sequence";summary.Text=$"Events {Draft[Slot].Count} · Capacity {Draft.EntryCount(Slot)} / 47 · {Draft.Duration(Slot)} ms";summary.ToolTip="Each hold uses one capacity unit; a nonzero following pause uses another. Hardware contents are unknown.";notice.Text=Draft.EntryCount(Slot)>=47?"Maximum sequence capacity reached.":"Local draft · Hold and pause timing are a sequence preview.";
        PaintSlots();foreach(var (key,shape) in paddles)shape.Illuminate(key==Slot,false);
        sequence.Children.Clear();steps.Clear();for(int i=0;i<Draft[Slot].Count;i++)
        {
            int index=i;var item=Draft[Slot][i];var content=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            content.Children.Add(new TextBlock {Text=$"{i+1:00}   {Describe(item)}",FontFamily=DS.Display,FontSize=17,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});content.Children.Add(new TextBlock {Text=$"{item.HoldMs} ms hold · {item.PauseMs} ms pause",FontSize=12.5,Foreground=DS.Brush("DS.TextSoft")});
            var button=Button("",()=>Select(index));button.Content=content;button.Width=button.MinWidth=button.MaxWidth=cardWidth;button.Height=cardHeight;button.Padding=new(14,0,12,0);button.Margin=new(0,0,10,0);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Background=Fill(i==SelectedIndex);button.GotKeyboardFocus+=(_,_)=>Select(index);steps.Add(button);sequence.Children.Add(button);
        }
        empty.Visibility=Draft[Slot].Count==0?Visibility.Visible:Visibility.Collapsed;sequenceViewport.Visibility=Draft[Slot].Count==0?Visibility.Collapsed:Visibility.Visible;add.IsEnabled=Draft.CanEdit && Draft.EntryCount(Slot)<47;clear.IsEnabled=preview.IsEnabled=Draft[Slot].Count>0;RefreshEditor();UpdateEventSelection();UpdateCardMetrics();RenderTimeline();if(SelectedIndex>=0)SnapTo(SelectedIndex);
        Body.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(Math.Abs(timelineCanvas.Height-Math.Max(76,timeline.ActualHeight-(timeline.ComputedHorizontalScrollBarVisibility==Visibility.Visible?12:0)-4))>.5)RenderTimeline();}));
    }
    private void RefreshEditor()
    {
        updating=true;bool selected=SelectedIndex>=0 && SelectedIndex<Draft[Slot].Count;eventTitle.Text=selected?$"Event {SelectedIndex+1:00} · what it presses":"Event editor";remove.IsEnabled=earlier.IsEnabled=later.IsEnabled=duplicate.IsEnabled=selected;hold.IsEnabled=pause.IsEnabled=selected;
        MacroEvent? item=selected?Draft[Slot][SelectedIndex]:null;if(!hold.IsKeyboardFocused)hold.Text=item?.HoldMs.ToString()??"—";if(!pause.IsKeyboardFocused)pause.Text=item?.PauseMs.ToString()??"—";
        selectedInputs.Text=item==null?"Select or add an event to edit it.":$"{Describe(item)} · held {item.HoldMs} ms · then {item.PauseMs} ms pause";
        foreach(var button in chords)PaintChord(button);
        leftSelector.IsEnabled=rightSelector.IsEnabled=selected;leftSelector.SetDirection(item?.Left??StickHeading.Neutral);rightSelector.SetDirection(item?.Right??StickHeading.Neutral);
        ShowEditor(EditorMode);
        updating=false;
    }
    private void RenderTimeline()
    {
        timelineCanvas.Children.Clear();laneLabels.Children.Clear();laneBlocks.Clear();double viewport=Math.Max(400,timeline.ActualWidth-132),width=Math.Max(viewport,Draft[Slot].Count*68*zoom),height=Math.Max(76,timeline.ActualHeight-(timeline.ComputedHorizontalScrollBarVisibility==Visibility.Visible?12:0)-4);timelineCanvas.Width=width+132;timelineCanvas.Height=height;laneLabels.Height=height;scale=width/Math.Max(250,Draft.Duration(Slot));double lane=(height-24)/4;
        foreach(var (label,row,tone) in new[]{("Buttons",0,Kit.Blue),("Left stick",1,Kit.Ice),("Right stick",2,Kit.Ice),("Pause",3,PauseTone)})
        {
            double chip=Math.Min(32,Math.Max(22,lane-10));
            var name=new Border {Width=110,Height=chip,CornerRadius=new(chip/2),Padding=new(12,0,10,0),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(36,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(24,255,255,255)),
                Child=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Children={new Ellipse {Width=7,Height=7,Fill=new SolidColorBrush(tone),Margin=new(0,0,9,0),VerticalAlignment=VerticalAlignment.Center},new TextBlock {Text=label,FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=DS.Brush("DS.Text"),VerticalAlignment=VerticalAlignment.Center}}}};
            Canvas.SetLeft(name,8);Canvas.SetTop(name,24+row*lane+(lane-chip)/2);laneLabels.Children.Add(name);
            timelineCanvas.Children.Add(new Line {X1=124,X2=width+132,Y1=24+row*lane,Y2=24+row*lane,Stroke=new SolidColorBrush(Color.FromArgb(40,119,151,202))});
        }
        double range=Math.Max(250,Draft.Duration(Slot)),interval=Math.Max(5,Math.Ceiling(viewport/scale/4/5)*5);for(double time=0;time<=range;time+=interval){double x=132+time*scale;timelineCanvas.Children.Add(new Line {X1=x,X2=x,Y1=20,Y2=height,Stroke=new SolidColorBrush(Color.FromArgb(30,119,151,202))});var tick=new TextBlock {Text=$"{time:0} ms",FontSize=12,Foreground=DS.Brush("DS.TextSoft")};Canvas.SetLeft(tick,time==0?x+4:Math.Min(x-12,width+132-65));Canvas.SetTop(tick,0);timelineCanvas.Children.Add(tick);}
        int start=0;for(int i=0;i<Draft[Slot].Count;i++)
        {
            var item=Draft[Slot][i];for(int row=0;row<4;row++)
            {
                bool neutral=item.Buttons.Count==0 && item.Left==StickHeading.Neutral && item.Right==StickHeading.Neutral;
                bool present=row==0?item.Buttons.Count>0 || neutral:row==1?item.Left!=StickHeading.Neutral:row==2?item.Right!=StickHeading.Neutral:item.PauseMs>0;if(!present)continue;
                int offset=row==3?item.HoldMs:0,duration=row==3?item.PauseMs:item.HoldMs;var rectangle=new Rectangle {Width=Math.Max(1,duration*scale),Height=Math.Max(12,lane-10),RadiusX=7,RadiusY=7,Fill=new SolidColorBrush(row==3?Color.FromArgb(96,PauseTone.R,PauseTone.G,PauseTone.B):Color.FromArgb(190,39,104,190)),Stroke=i==SelectedIndex?Brushes.LightSkyBlue:Brushes.Transparent,StrokeThickness=1.5,Tag=i,ToolTip=$"Event {i+1}, {duration} ms"};Canvas.SetLeft(rectangle,132+(start+offset)*scale);Canvas.SetTop(rectangle,29+row*lane);int index=i;rectangle.MouseLeftButtonDown+=(_,_)=>Select(index);timelineCanvas.Children.Add(rectangle);
                if(row==0 && neutral){rectangle.Fill=new SolidColorBrush(Color.FromArgb(25,164,194,231));rectangle.Stroke=Brushes.SlateGray;rectangle.StrokeDashArray=new(){3,3};}
                laneBlocks.Add((rectangle,i,row,rectangle.Fill));
                if(rectangle.Width>72)
                {
                    string label=row==0?neutral?"Neutral":string.Join(" + ",item.Buttons.Select(Glyph)):row==1?"L "+Symbol(item.Left):row==2?"R "+Symbol(item.Right):$"{item.PauseMs} ms";
                    var text=new TextBlock {Text=label,FontSize=14,FontWeight=FontWeights.SemiBold,Width=rectangle.Width-16,TextTrimming=TextTrimming.CharacterEllipsis,IsHitTestVisible=false};Canvas.SetLeft(text,Canvas.GetLeft(rectangle)+8);Canvas.SetTop(text,Canvas.GetTop(rectangle)+Math.Max(0,(rectangle.Height-18)/2));timelineCanvas.Children.Add(text);
                }
            }
            start+=item.HoldMs+item.PauseMs;
        }
        playhead.Y1=20;playhead.Y2=height;timelineCanvas.Children.Add(playhead);playhead.Visibility=IsPreviewing?Visibility.Visible:Visibility.Collapsed;if(IsPreviewing)UpdatePlayhead();
    }
    private void UpdatePlayhead()
    {
        long elapsed=clock.ElapsedMilliseconds;int start=0,index=-1;bool pausePhase=false;
        for(int i=0;i<Draft[Slot].Count;i++){var item=Draft[Slot][i];int end=start+item.HoldMs+item.PauseMs;if(elapsed<end){index=i;pausePhase=elapsed>=start+item.HoldMs;break;}start=end;}
        bool changed=index!=PreviewIndex;PreviewIndex=index;PreviewIsPause=pausePhase;playhead.Visibility=Visibility.Visible;playhead.X1=playhead.X2=132+elapsed*scale;
        notice.Text=$"Local preview · Event {index+1:00} · {(pausePhase?"Pause":"Hold")} · {elapsed} ms · No input emitted";
        UpdateEventSelection();UpdateLaneHighlight();if(changed && index>=0)SnapTo(index);double x=playhead.X1;if(x<timeline.HorizontalOffset+212 || x>timeline.HorizontalOffset+timeline.ViewportWidth-80)timeline.ScrollToHorizontalOffset(Math.Max(0,x-timeline.ViewportWidth*.55));
    }
    private void UpdateLaneHighlight(){foreach(var block in laneBlocks){bool eventActive=IsPreviewing && block.Event==PreviewIndex,phaseActive=eventActive && (PreviewIsPause?block.Lane==3:block.Lane!=3);block.Shape.Fill=phaseActive?new SolidColorBrush(PreviewIsPause?Color.FromRgb(176,190,214):Color.FromRgb(68,142,211)):eventActive?new SolidColorBrush(block.Lane==3?Color.FromRgb(118,132,160):Color.FromRgb(47,112,174)):block.Normal;}}
    public void SetZoom(double value){zoom=Math.Clamp(value,.5,4);zoomLabel.Text=$"{zoom*100:0}%";RenderTimeline();SnapTimeline(SelectedIndex);}
    private void SnapTimeline(int index){if(index<0)return;double time=Draft[Slot].Take(index).Sum(s=>s.HoldMs+s.PauseMs);Body.Dispatcher.BeginInvoke(new Action(()=>timeline.ScrollToHorizontalOffset(Math.Max(0,132+time*scale-timeline.ViewportWidth*.45))));}
    private void UpdateCardMetrics(){double size=sequenceViewport.ActualWidth>1000?210:190;cardWidth=size;cardHeight=58;foreach(var button in steps){button.MaxWidth=double.PositiveInfinity;button.MinWidth=button.Width=size;button.MaxWidth=size;button.Height=cardHeight;}int visible=Math.Max(1,(int)Math.Floor((sequenceViewport.ViewportWidth+10)/Stride));double slack=Math.Max(0,sequenceViewport.ViewportWidth-(visible*Stride-10));sequence.Margin=new(0,8,slack,8);if(SelectedIndex>=0)SnapTo(SelectedIndex);}
    private void SnapTo(int index){if(index<0||index>=steps.Count||index>=Draft[Slot].Count)return;string slot=Slot;Guid id=Draft[slot][index].Id;Body.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(Slot!=slot || index>=steps.Count || index>=Draft[Slot].Count || Draft[Slot][index].Id!=id)return;int count=Math.Max(1,(int)Math.Floor((sequenceViewport.ViewportWidth+10)/Stride));int first=Math.Clamp(index-count/2,0,Math.Max(0,steps.Count-count));sequenceViewport.ScrollToHorizontalOffset(first*Stride);MaskPartialCards();}));}
    private void MaskPartialCards(){foreach(var button in steps){if(!button.IsVisible||!sequenceViewport.IsAncestorOf(button))continue;var bounds=button.TransformToAncestor(sequenceViewport).TransformBounds(new Rect(button.RenderSize));bool visible=bounds.Left>=-.5 && bounds.Right<=sequenceViewport.ActualWidth+.5;button.Opacity=visible?1:0;button.IsHitTestVisible=visible;}}
    private bool CardInView(int index){if(index<0||index>=steps.Count||!sequenceViewport.IsAncestorOf(steps[index]))return false;var bounds=steps[index].TransformToAncestor(sequenceViewport).TransformBounds(new Rect(steps[index].RenderSize));return bounds.Left>=-.5 && bounds.Right<=sequenceViewport.ActualWidth+.5 && bounds.Top>=-.5 && bounds.Bottom<=sequenceViewport.ActualHeight+.5;}
    private static Brush Fill(bool selected)=>new SolidColorBrush(selected?Color.FromArgb(200,37,86,147):Color.FromArgb(70,24,35,55));
    private static ControlTemplate Bare(){var t=new ControlTemplate(typeof(Button)){VisualTree=new FrameworkElementFactory(typeof(ContentPresenter))};t.Seal();return t;}
    private static string Glyph(string key)=>key switch {"DPAD_UP"=>"↑","DPAD_DOWN"=>"↓","DPAD_LEFT"=>"←","DPAD_RIGHT"=>"→","SELECT"=>"View","START"=>"Menu",_=>key};
    private static string Friendly(string key)=>key switch {"DPAD_UP"=>"D-pad up","DPAD_DOWN"=>"D-pad down","DPAD_LEFT"=>"D-pad left","DPAD_RIGHT"=>"D-pad right","SELECT"=>"View","START"=>"Menu","L3"=>"the left stick click","R3"=>"the right stick click","LT"=>"the left trigger","RT"=>"the right trigger","LB"=>"the left bumper","RB"=>"the right bumper",_=>key};
    private static string Symbol(StickHeading value)=>value switch {StickHeading.Up=>"↑",StickHeading.UpRight=>"↗",StickHeading.Right=>"→",StickHeading.DownRight=>"↘",StickHeading.Down=>"↓",StickHeading.DownLeft=>"↙",StickHeading.Left=>"←",StickHeading.UpLeft=>"↖",_=>"·"};
    private static string Describe(MacroEvent item)=>string.Join(" + ",item.Buttons.Select(Glyph).Concat(item.Left==StickHeading.Neutral?[]:new[]{"L "+Symbol(item.Left)}).Concat(item.Right==StickHeading.Neutral?[]:new[]{"R "+Symbol(item.Right)})) is {Length:>0} text?text:"Neutral";
}
