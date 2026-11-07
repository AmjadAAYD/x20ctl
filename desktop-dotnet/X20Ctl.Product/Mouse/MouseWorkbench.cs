using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
using MouseAction = X20Ctl.Zone.Mouse.MouseAction;
using Path = System.Windows.Shapes.Path;
namespace X20Ctl.Product;

/// <summary>
/// The Mouse tab (owner, 7 Nov 2026: nothing hidden). The controller sits on the left with every button labelled, a
/// drawn mouse on the right shows which buttons send M1 to M7, and below them the picked button's choices, its turbo, a
/// list of every button, and the pointer, scroll and shortcut settings. Player and model scoped; only inputs XInput
/// reports on their own can be assigned.
/// </summary>
public sealed partial class MouseWorkbench
{
    public Grid Body { get; } = new();
    public CheckBox Switch { get; } = new() { Style=DS.Style("DS.Toggle"), VerticalAlignment=VerticalAlignment.Center };
    readonly MouseModeService service;
    readonly int player; readonly ControllerModel model;
    readonly Action<Button> register;
    readonly TextBlock status=Copy("Off",16), saved=Copy("Saved on this PC · Changes apply immediately",13), mapped=Copy("",13), selected=Copy("A",30,true), scrollSource=Copy("",13), precisionSource=Copy("",13), previewReadout=Copy("Press Test pointer, then move a stick or the D-pad",13);
    readonly TextBlock sends=Copy("",17), shortcutLine=Copy("",14), turboNote=Copy("",13);
    readonly ComboBox source=Choice(new[]{"Automatic", "XInput 1", "XInput 2", "XInput 3", "XInput 4"}), pointer=Choice(new[]{"Left stick","Right stick"});
    readonly ComboBox chord1=Choice(PadButtons.Mask.Keys.ToArray()),chord2=Choice(PadButtons.Mask.Keys.ToArray()), hold=Choice(new[]{"0.5 seconds","1 second","1.5 seconds","2 seconds","3 seconds"});
    /// <summary>One row per button in the "Every button" list; also the buttons the page can assign.</summary>
    readonly Dictionary<string,Button> mappings=new();
    readonly WrapPanel actionChoices=new(), turboChoices=new(){Margin=new(0,6,0,0)};
    readonly CheckBox scrolls=Toggle(),combo=Toggle(),horizontal=Toggle(),invertV=Toggle(),invertH=Toggle();
    readonly Slider speed=Slider(200,5000,100),curve=Slider(1,3,.1),deadzone=Slider(0,40,1),precision=Slider(10,90,5),scroll=Slider(1,60,1);
    readonly Canvas preview=new(){Width=420,Height=130,ClipToBounds=true};
    readonly Path cursor=new(){Data=Geometry.Parse("M0,0 L0,17 L4.5,12.5 L8,20 L11,18.6 L7.6,11.4 L13.5,11.4 Z"),Fill=Brushes.White,Stroke=new SolidColorBrush(Color.FromRgb(10,16,28)),StrokeThickness=1.2};
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    readonly Button test;
    readonly ControllerBoard board;
    readonly MouseDiagram mouse=new(){MaxHeight=380,Margin=new(0,4,0,0)};
    static readonly int[] TurboRates=[0,5,8,10,15,20,30];
    sealed record InputPick(string Id,string Label){public override string ToString()=>Label;}
    static string InputName(string key)=>BoardInk.InputName(key);
    MouseProfile profile;
    MouseMapper? previewMapper;
    string selection="A"; bool loading,testing; double px=210,py=65;
    ushort previous;

    public MouseWorkbench(MouseModeService service,Action<Button> register,int player=0,ControllerModel? model=null,ControllerLayout? layout=null)
    {
        this.service=service;this.register=register;this.player=player;
        this.model=model??new ControllerCatalog(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))).Models.First();
        profile=service.ProfileFor(player,this.model.Id);loading=true;
        // XInput reports the standard controls only: art or rear paddles alone don't make an input that can be assigned
        var available=(layout?.Controls.Select(r=>r.Key)??this.model.FrontButtons.Keys).ToHashSet();
        var inputs=MouseProfile.Inputs.Where(available.Contains).ToList();
        foreach(var chordBox in new[]{chord1,chord2}){chordBox.ItemsSource=PadButtons.Mask.Keys.Where(available.Contains).Select(k=>new InputPick(k,InputName(k))).ToArray();chordBox.DisplayMemberPath="Label";chordBox.SelectedValuePath="Id";}

        var page=new StackPanel{Margin=new(22,6,22,26)};
        Body.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});

        // ---- heading: what it is, whose, and the switch
        var head=new DockPanel{Margin=new(0,0,0,4)};var state=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(state,Dock.Right);head.Children.Add(state);
        state.Children.Add(status);Switch.Margin=new(14,0,0,0);state.Children.Add(Switch);
        var titles=new StackPanel();titles.Children.Add(Copy("Mouse",30,true));titles.Children.Add(Copy($"Player {player+1} · {this.model.Name} · Your controller moves the pointer, clicks and presses keys",15));
        head.Children.Add(titles);page.Children.Add(head);
        shortcutLine.Foreground=BoardInk.Soft;page.Children.Add(shortcutLine);

        // ---- the two pictures side by side
        var stage=new Grid{Margin=new(0,12,0,0)};stage.ColumnDefinitions.Add(new(){Width=new(1.5,GridUnitType.Star)});stage.ColumnDefinitions.Add(new(){Width=new(14)});stage.ColumnDefinitions.Add(new());page.Children.Add(stage);
        board=new ControllerBoard(this.model,layout,inputs,register){MaxHeight=470};board.Picked+=Select;
        var left=new StackPanel();left.Children.Add(Caption("CONTROLLER","Pick a button on the controller or its label"));left.Children.Add(board);
        stage.Children.Add(Card(left));
        var right=new StackPanel();right.Children.Add(Caption("MOUSE","Green buttons are mapped · M6 and M7 are the wheel's tilt"));right.Children.Add(mouse);
        mouse.Picked+=id=>{if(Enum.TryParse<MouseAction>(id,out var a))Assign(a);};
        preview.Children.Add(cursor);Canvas.SetLeft(cursor,px);Canvas.SetTop(cursor,py);
        var screen=new Border{CornerRadius=new(10),BorderThickness=new(1),BorderBrush=BoardInk.IdleLine,Background=new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(160,5,10,22))),Margin=new(0,10,0,0),Child=new Viewbox{Child=preview,Stretch=Stretch.Uniform,MaxHeight=130}};
        right.Children.Add(screen);
        test=Pill("Test pointer",ToggleTest);var testRow=new DockPanel{Margin=new(0,8,0,0)};DockPanel.SetDock(test,Dock.Left);testRow.Children.Add(test);previewReadout.VerticalAlignment=VerticalAlignment.Center;previewReadout.Margin=new(8,0,0,0);testRow.Children.Add(previewReadout);right.Children.Add(testRow);
        var rightCard=Card(right);Grid.SetColumn(rightCard,2);stage.Children.Add(rightCard);

        // ---- the picked button: every choice on show, grouped, and its turbo
        var assign=new StackPanel();
        var picked=new WrapPanel();picked.Children.Add(selected);sends.VerticalAlignment=VerticalAlignment.Bottom;sends.Margin=new(14,0,0,6);picked.Children.Add(sends);assign.Children.Add(picked);
        assign.Children.Add(actionChoices);
        var turboHead=new WrapPanel{Margin=new(0,14,0,0)};turboHead.Children.Add(Copy("TURBO",13,true));turboNote.Margin=new(12,4,0,4);turboHead.Children.Add(turboNote);assign.Children.Add(turboHead);assign.Children.Add(turboChoices);
        page.Children.Add(Card(assign,new(0,14,0,0)));

        // ---- every button at a glance
        var list=new StackPanel();var listHead=new WrapPanel();listHead.Children.Add(Copy("EVERY BUTTON",13,true));mapped.Margin=new(12,4,0,4);listHead.Children.Add(mapped);list.Children.Add(listHead);
        var rows=new UniformGrid{Columns=3,Margin=new(0,6,0,0)};
        foreach(var input in inputs){string key=input;var row=Row(key);mappings[key]=row;rows.Children.Add(row);}
        list.Children.Add(rows);page.Children.Add(Card(list,new(0,14,0,0)));

        // ---- settings, three cards, nothing folded away
        var settings=new Grid{Margin=new(0,14,0,0)};for(int i=0;i<5;i++)settings.ColumnDefinitions.Add(new(){Width=i%2==1?new(14):new(1,GridUnitType.Star)});page.Children.Add(settings);
        var motion=new StackPanel();motion.Children.Add(Copy("POINTER",13,true));motion.Children.Add(Setting("Pointer stick",pointer));motion.Children.Add(Setting("Speed",speed,v=>$"{v:0} px/s"));motion.Children.Add(Setting("Acceleration",curve,v=>$"{v:0.0}"));motion.Children.Add(Setting("Dead zone",deadzone,v=>$"{v:0}%"));
        motion.Children.Add(precisionSource);motion.Children.Add(Setting("Precision speed",precision,v=>$"{v:0}%"));
        curve.ToolTip="Higher values make small pushes slower. Full stick deflection still uses the speed above.";
        settings.Children.Add(Card(motion));
        var scrolling=new StackPanel();scrolling.Children.Add(Copy("SCROLL",13,true));scrolling.Children.Add(scrollSource);scrolling.Children.Add(Setting("Scrolling",scrolls));scrolling.Children.Add(Setting("Scroll speed",scroll,v=>$"{v:0} lines/s"));
        scrolling.Children.Add(Setting("Sideways scroll",horizontal));scrolling.Children.Add(Setting("Invert up/down",invertV));scrolling.Children.Add(Setting("Invert sideways",invertH));
        var scrollCard=Card(scrolling);Grid.SetColumn(scrollCard,2);settings.Children.Add(scrollCard);
        var controller=new StackPanel();controller.Children.Add(Copy("SWITCH FROM THE CONTROLLER",13,true));controller.Children.Add(Setting("Shortcut",combo));
        var chord=new WrapPanel();chord.Children.Add(chord1);chord.Children.Add(Copy("+",16));chord.Children.Add(chord2);chord.Children.Add(hold);controller.Children.Add(chord);
        controller.Children.Add(Copy("The two shortcut buttons only switch the mouse on and off.",12));
        controller.Children.Add(Setting("Controller",source));controller.Children.Add(Copy("Automatic uses the first controller Windows sees.",12));
        var controllerCard=Card(controller);Grid.SetColumn(controllerCard,4);settings.Children.Add(controllerCard);

        var presets=new WrapPanel{Margin=new(0,14,0,0)};presets.Children.Add(Copy("Start from",14).Also(t=>{t.VerticalAlignment=VerticalAlignment.Center;t.Margin=new(0,0,10,0);}));
        foreach(var name in PresetNames){string preset=name;presets.Children.Add(Pill(name,()=>Preset(preset)));}
        presets.Children.Add(Pill("Reset Mouse",()=>{Save(new MouseProfile{SourceSlot=profile.SourceSlot});Fill();Select(selection);}));page.Children.Add(presets);page.Children.Add(saved);

        foreach(var slider in new[]{speed,curve,deadzone,precision,scroll})slider.ValueChanged+=(_,_)=>Persist();
        foreach(var box in new[]{pointer,source,chord1,chord2,hold})box.SelectionChanged+=(_,_)=>Persist();
        foreach(var toggle in new[]{scrolls,combo,horizontal,invertV,invertH}){toggle.Checked+=(_,_)=>Persist();toggle.Unchecked+=(_,_)=>Persist();}
        Switch.Checked+=(_,_)=>{if(loading)return;try{service.TakeControl(player,this.model.Id);}catch(InvalidOperationException error){saved.Text=error.Message;Refresh();}};
        Switch.Unchecked+=(_,_)=>{if(!loading && OwnsSource)service.Active=false;};
        timer.Tick+=(_,_)=>Tick();Body.Loaded+=(_,_)=>{timer.Start();Refresh();};Body.Unloaded+=(_,_)=>{timer.Stop();EndPreview();};
        Fill();service.ObserveFor(player,this.model.Id);Select(mappings.ContainsKey("A")?"A":mappings.Keys.FirstOrDefault()??"A");Refresh();
    }
    bool OwnsSource=>service.OwnerPlayer==player && service.OwnerModel==model.Id;
    void Fill()
    {
        loading=true;source.SelectedIndex=profile.SourceSlot+1;pointer.SelectedIndex=profile.PointerStick=="Right"?1:0;speed.Value=profile.Speed;curve.Value=profile.Curve;deadzone.Value=profile.Deadzone;precision.Value=profile.PrecisionPercent;scroll.Value=profile.ScrollSpeed;
        scrolls.IsChecked=profile.Scroll;combo.IsChecked=profile.Combo;horizontal.IsChecked=profile.HorizontalScroll;invertV.IsChecked=profile.InvertVerticalScroll;invertH.IsChecked=profile.InvertHorizontalScroll;
        chord1.SelectedValue=profile.ToggleButtons[0];chord2.SelectedValue=profile.ToggleButtons[1];hold.SelectedIndex=Array.IndexOf(new[]{500,1000,1500,2000,3000},profile.HoldMilliseconds);loading=false;UpdateMappings();
    }
    void Persist()
    {
        if(loading)return;var next=profile.Clone();next.SourceSlot=source.SelectedIndex-1;next.PointerStick=pointer.SelectedIndex==1?"Right":"Left";next.Speed=(int)speed.Value;next.Curve=curve.Value;next.Deadzone=(int)deadzone.Value;next.PrecisionPercent=precision.Value;next.ScrollSpeed=(int)scroll.Value;
        next.Scroll=scrolls.IsChecked==true;next.Combo=combo.IsChecked==true;next.HorizontalScroll=horizontal.IsChecked==true;next.InvertVerticalScroll=invertV.IsChecked==true;next.InvertHorizontalScroll=invertH.IsChecked==true;
        next.ToggleButtons=[chord1.SelectedValue as string??"SELECT",chord2.SelectedValue as string??"Y"];next.HoldMilliseconds=new[]{500,1000,1500,2000,3000}[Math.Clamp(hold.SelectedIndex,0,4)];
        Save(next);
    }
    void Save(MouseProfile next)
    {
        try {next.Validate();service.SaveFor(player,model.Id,next);profile=next;service.ObserveFor(player,model.Id);saved.Text="Saved for this player and model · Applies immediately on this PC";previewMapper=new(profile);UpdateMappings();}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException){saved.Text=error.Message;}
    }
    bool Reserved(string key)=>profile.Combo&&profile.ToggleButtons.Contains(key);
    MouseAction ActionOf(string key)=>profile.Buttons.GetValueOrDefault(key);
    void UpdateMappings()
    {
        mapped.Text=$"{mappings.Keys.Count(k=>ActionOf(k)!=MouseAction.None && !Reserved(k))} of {mappings.Count} buttons do something";
        foreach(var key in mappings.Keys)
        {
            string turbo=profile.Turbo.TryGetValue(key,out var r)?$" · turbo {r}/s":"";
            board.Show(key,Reserved(key)?"Shortcut":ActionOf(key)==MouseAction.None?"":Short(ActionOf(key))+turbo,!Reserved(key)&&ActionOf(key)!=MouseAction.None);
            PaintRow(key,Reserved(key)?"On/off shortcut":Friendly(ActionOf(key))+turbo);
        }
        foreach(var (id,_) in MouseDiagram.Buttons)
            mouse.Show(id,mappings.Keys.Where(k=>!Reserved(k)&&ActionOf(k).ToString()==id).Select(InputName).ToList());
        scrollSource.Text=$"The {(profile.PointerStick=="Left"?"right":"left")} stick scrolls";
        precisionSource.Text="Slow pointer while holding: "+string.Join(" / ",mappings.Keys.Where(k=>ActionOf(k)==MouseAction.Precision).Select(InputName).DefaultIfEmpty("no button"));
        shortcutLine.Text=profile.Combo?$"Hold {InputName(profile.ToggleButtons[0])} + {InputName(profile.ToggleButtons[1])} for {profile.HoldMilliseconds/1000.0:0.#} s to switch the mouse on or off from the controller":"The controller shortcut is off · Use the switch to turn the mouse on or off";
    }
    public void Select(string key)
    {
        selection=key;selected.Text=InputName(key);board.Select(key);
        foreach(var k in mappings.Keys)PaintRow(k,null);
        var current=ActionOf(key);
        sends.Text=Reserved(key)?"is the on/off shortcut. Change the shortcut below to use it for something else.":current==MouseAction.None?"does nothing. Pick what it sends:":"sends "+Friendly(current);
        actionChoices.Children.Clear();
        foreach(var group in Groups)
        {
            var column=new StackPanel{Margin=new(0,10,26,0)};column.Children.Add(Copy(group.Group,12,true));
            var chips=new WrapPanel{MaxWidth=group.Actions.Length>6?560:380};
            foreach(var action in group.Actions){var pick=action;chips.Children.Add(Chip(Friendly(action),current==action,()=>Assign(pick)));}
            column.Children.Add(chips);actionChoices.Children.Add(column);
        }
        turboChoices.Children.Clear();int rate=profile.Turbo.GetValueOrDefault(key);
        foreach(int r in TurboRates){int pick=r;turboChoices.Children.Add(Chip(r==0?"Off":$"{r} a second",rate==r,()=>SetTurbo(pick)));}
        turboNote.Text=rate==0?"Hold to keep pressing: clicks or keys repeat by themselves":$"{InputName(key)} repeats {rate} times a second while held";
    }
    void SetTurbo(int rate){var next=profile.Clone();if(rate==0)next.Turbo.Remove(selection);else next.Turbo[selection]=rate;Save(next);Select(selection);}
    void Preset(string name)
    {
        var next=profile.Clone();next.Buttons=MouseProfile.Defaults();next.Turbo.Clear();
        if(name=="Minimal")foreach(var key in next.Buttons.Keys.ToArray())if(key is not ("A" or "B" or "LT"))next.Buttons[key]=MouseAction.None;
        if(name=="Web browsing"){next.Buttons["X"]=MouseAction.PageDown;next.Buttons["Y"]=MouseAction.PageUp;}
        if(name=="Media"){next.Buttons["A"]=MouseAction.PlayPause;next.Buttons["B"]=MouseAction.Mute;next.Buttons["LT"]=MouseAction.VolumeDown;next.Buttons["RT"]=MouseAction.VolumeUp;}
        Save(next);Select(selection);
    }
    void ToggleTest()
    {
        if(testing){EndPreview();return;}
        try{service.TakeControl(player,model.Id,true);testing=true;previewMapper=new(profile);px=210;py=65;test.Content="Stop test";}
        catch(InvalidOperationException error){service.Previewing=false;saved.Text=error.Message;}
    }
    void EndPreview(){if(!testing)return;if(OwnsSource){service.Active=false;service.Previewing=false;}testing=false;previewMapper=null;test.Content="Test pointer";previewReadout.Text="Test stopped · Windows' pointer was not moved";}
    void Tick()
    {
        Refresh();if(!OwnsSource)return;var pad=service.Latest;
        // live: held buttons light up on the controller, and the mouse buttons they send light up on the mouse
        var held=new HashSet<string>();foreach(var (name,mask) in PadButtons.Mask)if((pad.Buttons&mask)!=0)held.Add(name);
        if(pad.LeftTrigger>PadButtons.TriggerThreshold)held.Add("LT");if(pad.RightTrigger>PadButtons.TriggerThreshold)held.Add("RT");
        board.Held(held);mouse.Pressed(held.Where(k=>!Reserved(k)).Select(k=>ActionOf(k).ToString()));
        ushort pressed=(ushort)(pad.Buttons & ~previous);previous=pad.Buttons;
        // a press only picks that button on the page; nothing changes until a choice is clicked
        if((!service.Active||testing) && pressed!=0)foreach(var (name,mask) in PadButtons.Mask)if((pressed&mask)!=0 && mappings.ContainsKey(name)){Select(name);break;}
        if(!testing)return;
        if(!service.Connected){previewReadout.Text="Waiting for the controller";return;}
        var frame=previewMapper!.Step(pad,.016);px=Math.Clamp(px+frame.Dx,2,404);py=Math.Clamp(py+frame.Dy,2,110);Canvas.SetLeft(cursor,px);Canvas.SetTop(cursor,py);
        double x=profile.PointerStick=="Left"?pad.LeftX:pad.RightX,y=profile.PointerStick=="Left"?pad.LeftY:pad.RightY,m=Math.Min(1,Math.Sqrt(x*x+y*y)/32768);
        previewReadout.Text=$"Stick {m:P0} · Precision {(previewMapper.Precision?"on":"off")} · Windows' pointer isn't moved";
    }
    void Refresh()
    {
        loading=true;Switch.IsChecked=OwnsSource&&service.Active&&!testing;loading=false;
        status.Text=testing?"Testing":service.Active&&!OwnsSource?$"Player {service.OwnerPlayer+1} has the mouse":OwnsSource&&service.Active?service.Connected?"On":"Waiting for the controller":"Off";
    }

    static readonly MouseAction[] MouseButtons=[MouseAction.LeftClick,MouseAction.RightClick,MouseAction.MiddleClick,MouseAction.Back,MouseAction.Forward,MouseAction.TiltLeft,MouseAction.TiltRight];
    static string Category(MouseAction a)=>a switch{MouseAction.None=>"NOTHING",_ when MouseButtons.Contains(a)=>"MOUSE BUTTONS",MouseAction.Precision or MouseAction.PointerUp or MouseAction.PointerDown or MouseAction.PointerLeft or MouseAction.PointerRight=>"POINTER",>=MouseAction.VolumeUp and <=MouseAction.PlayPause=>"MEDIA",MouseAction.Windows or MouseAction.TaskView or MouseAction.OnScreenKeyboard=>"WINDOWS",_=>"KEYBOARD"};
    static readonly string[] GroupOrder=["MOUSE BUTTONS","POINTER","KEYBOARD","WINDOWS","MEDIA","NOTHING"];
    internal IReadOnlyList<(string Group, MouseAction[] Actions)> Groups=>Enum.GetValues<MouseAction>().GroupBy(Category).OrderBy(g=>Array.IndexOf(GroupOrder,g.Key)).Select(g=>(g.Key,(g.Key=="MOUSE BUTTONS"?MouseButtons:g.ToArray()))).ToList();
    public static string Friendly(MouseAction a)=>a switch
    {
        MouseAction.None=>"Nothing",MouseAction.LeftClick=>"M1 Left click",MouseAction.RightClick=>"M2 Right click",MouseAction.MiddleClick=>"M3 Wheel click",MouseAction.Back=>"M4 Back",MouseAction.Forward=>"M5 Forward",
        MouseAction.TiltLeft=>"M6 Tilt left",MouseAction.TiltRight=>"M7 Tilt right",MouseAction.Precision=>"Slow pointer",
        MouseAction.PointerUp=>"Pointer ↑",MouseAction.PointerDown=>"Pointer ↓",MouseAction.PointerLeft=>"Pointer ←",MouseAction.PointerRight=>"Pointer →",
        MouseAction.Up=>"Arrow ↑",MouseAction.Down=>"Arrow ↓",MouseAction.Left=>"Arrow ←",MouseAction.Right=>"Arrow →",
        MouseAction.OnScreenKeyboard=>"On-screen keyboard",MouseAction.TaskView=>"Task View",MouseAction.Windows=>"Start menu",MouseAction.PlayPause=>"Play / pause",MouseAction.PageUp=>"Page up",MouseAction.PageDown=>"Page down",
        MouseAction.VolumeUp=>"Volume up",MouseAction.VolumeDown=>"Volume down",_=>a.ToString(),
    };
    /// <summary>The few words a pill has room for.</summary>
    static string Short(MouseAction a)=>a switch{MouseAction.None=>"—",MouseAction.LeftClick=>"M1",MouseAction.RightClick=>"M2",MouseAction.MiddleClick=>"M3",MouseAction.Back=>"M4",MouseAction.Forward=>"M5",MouseAction.TiltLeft=>"M6",MouseAction.TiltRight=>"M7",_=>Friendly(a)};

    Button Row(string key)
    {
        var name=new TextBlock{Text=InputName(key),FontFamily=DS.Display,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=BoardInk.Text,Width=86};
        var value=new TextBlock{FontFamily=DS.Display,FontSize=14.5,Foreground=BoardInk.Soft,TextTrimming=TextTrimming.CharacterEllipsis};
        var face=new Border{CornerRadius=new(10),BorderThickness=new(1),Padding=new(12,8,12,8),Margin=new(0,3,8,3),Child=new DockPanel{Children={name,value}}};
        var b=new Button{Template=BoardInk.Bare,Content=face,Cursor=Cursors.Hand,Tag=(name,value)};b.Click+=(_,_)=>Select(key);register(b);return b;
    }
    void PaintRow(string key,string? text)
    {
        var b=mappings[key];var face=(Border)b.Content;var (_,value)=((TextBlock,TextBlock))b.Tag;
        if(text!=null)value.Text=(Reserved(key)?"":"→ ")+text;
        bool chosen=key==selection,on=!Reserved(key)&&ActionOf(key)!=MouseAction.None;
        face.Background=chosen?BoardInk.SelectedFill:on?BoardInk.Solid(40,BoardInk.Green):BoardInk.Idle;face.BorderBrush=chosen?BoardInk.SelectedLine:on?BoardInk.Solid(150,BoardInk.Green):BoardInk.IdleLine;
        value.Foreground=on?BoardInk.MappedLine:BoardInk.Soft;b.ToolTip=$"{InputName(key)} {value.Text}";
    }
    Button Chip(string text,bool on,Action click)
    {
        var face=new Border{CornerRadius=new(17),BorderThickness=new(1.5),Padding=new(14,6,14,7),Margin=new(0,4,8,4),Background=on?BoardInk.Solid(120,BoardInk.Green):BoardInk.Idle,BorderBrush=on?BoardInk.MappedLine:BoardInk.IdleLine,
            Child=new TextBlock{Text=text,FontFamily=DS.Display,FontSize=14.5,FontWeight=on?FontWeights.SemiBold:FontWeights.Normal,Foreground=BoardInk.Text}};
        var b=new Button{Template=BoardInk.Bare,Content=face,Cursor=Cursors.Hand};b.Click+=(_,_)=>click();
        b.MouseEnter+=(_,_)=>{if(!on)face.BorderBrush=BoardInk.SelectedLine;};b.MouseLeave+=(_,_)=>{if(!on)face.BorderBrush=BoardInk.IdleLine;};register(b);return b;
    }
    static StackPanel Caption(string title,string hint){var s=new StackPanel{Margin=new(0,0,0,6)};s.Children.Add(Copy(title,13,true));s.Children.Add(Copy(hint,12.5));return s;}
    static TextBlock Copy(string text,double size,bool bold=false)=>Kit.Text(text,size,bold?"DS.Text":"DS.TextSoft",bold?FontWeights.SemiBold:FontWeights.Normal).Also(t=>{t.TextWrapping=TextWrapping.Wrap;t.TextTrimming=TextTrimming.None;t.Margin=new(0,3,0,3);});
    Button Pill(string text,Action click){var b=new Button{Content=text,Style=DS.Style("ActionButton"),Height=36,MinHeight=0,Padding=new(16,0,16,0),Margin=new(0,3,8,3),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>click();register(b);return b;}
    static ComboBox Choice(string[] labels)=>new(){ItemsSource=labels,MinWidth=118,MaxWidth=190,MinHeight=34,Margin=new(0,2,6,2),HorizontalAlignment=HorizontalAlignment.Left};
    static Slider Slider(double min,double max,double step)=>new(){Style=DS.Style("DS.Slider"),Minimum=min,Maximum=max,TickFrequency=step,IsSnapToTickEnabled=true,SmallChange=step,LargeChange=step*5,MinWidth=90,VerticalAlignment=VerticalAlignment.Center};
    static CheckBox Toggle()=>new(){Style=DS.Style("DS.Toggle"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};
    static Border Card(UIElement content,Thickness? margin=null)=>new(){Margin=margin??new(0),Padding=new(18,14,18,16),CornerRadius=new(16),BorderThickness=new(1),BorderBrush=new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(50,155,194,240))),Background=new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(110,8,17,38))),Child=content};
    static Grid Setting(string label,FrameworkElement control){var row=new Grid{Margin=new(0,4,0,4)};row.ColumnDefinitions.Add(new(){Width=new(128)});row.ColumnDefinitions.Add(new());var l=Copy(label,14);l.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(l);Grid.SetColumn(control,1);row.Children.Add(control);return row;}
    static Grid Setting(string label,Slider slider,Func<double,string> format){var panel=new Grid();panel.ColumnDefinitions.Add(new());panel.ColumnDefinitions.Add(new(){Width=new(78)});panel.Children.Add(slider);var value=Copy(format(slider.Value),13);value.VerticalAlignment=VerticalAlignment.Center;value.Margin=new(10,0,0,0);Grid.SetColumn(value,1);panel.Children.Add(value);slider.ValueChanged+=(_,_)=>value.Text=format(slider.Value);return Setting(label,panel);}
}
