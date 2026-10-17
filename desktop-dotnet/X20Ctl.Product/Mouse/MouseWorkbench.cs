using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>Player/model scoped desktop controls. Only independently reported inputs can be assigned.</summary>
public sealed class MouseWorkbench
{
    public Grid Body { get; } = new();
    public CheckBox Switch { get; } = new() { Style=DS.Style("DS.Toggle") };
    readonly MouseModeService service;
    readonly int player; readonly ControllerModel model;
    readonly Action<Button> register;
    readonly TextBlock status=Copy("Off",16), saved=Copy("Saved on this PC · Changes apply immediately",13), mapped=Copy("",13), selected=Copy("A",24), scrollSource=Copy("",13), precisionSource=Copy("",13), previewReadout=Copy("Choose Test pointer to begin",13);
    readonly ComboBox source=Choice(new[]{"Choose input source", "XInput 1", "XInput 2", "XInput 3", "XInput 4"}), pointer=Choice(new[]{"Left stick","Right stick"});
    readonly ComboBox chord1=Choice(PadButtons.Mask.Keys.ToArray()),chord2=Choice(PadButtons.Mask.Keys.ToArray()), hold=Choice(new[]{"0.5 seconds","1 second","1.5 seconds","2 seconds","3 seconds"});
    readonly Dictionary<string,Button> mappings=new();
    readonly Dictionary<string,PhysicalHotspot> hotspots=new();
    readonly StackPanel actionChoices=new();
    readonly CheckBox scrolls=Toggle(),combo=Toggle(),horizontal=Toggle(),invertV=Toggle(),invertH=Toggle();
    readonly Slider speed=Slider(200,5000,100),curve=Slider(1,3,.1),deadzone=Slider(0,40,1),precision=Slider(10,90,5),scroll=Slider(1,60,1);
    readonly Canvas preview=new(){Width=420,Height=145,ClipToBounds=true,Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Color.FromArgb(95,5,12,30))};
    readonly Ellipse dot=new(){Width=10,Height=10,Fill=Brushes.LightSkyBlue};
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    readonly Button test;
    sealed record InputPick(string Id,string Label){public override string ToString()=>Label;}
    static string InputName(string key)=>key switch{"SELECT"=>"View","START"=>"Menu","HOME"=>"Home","DPAD_UP"=>"D-pad ↑","DPAD_DOWN"=>"D-pad ↓","DPAD_LEFT"=>"D-pad ←","DPAD_RIGHT"=>"D-pad →",_=>key};
    MouseProfile profile;
    MouseMapper? previewMapper;
    string selection="A"; bool loading,testing; double px=210,py=72;
    ushort previous;
    public MouseWorkbench(MouseModeService service,Action<Button> register,int player=0,ControllerModel? model=null,ControllerLayout? layout=null)
    {
        this.service=service;this.register=register;this.player=player;
        this.model=model??new ControllerCatalog(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))).Models.First();
        profile=service.ProfileFor(player,this.model.Id);loading=true;foreach(var chordBox in new[]{chord1,chord2}){chordBox.ItemsSource=PadButtons.Mask.Keys.Select(k=>new InputPick(k,InputName(k))).ToArray();chordBox.DisplayMemberPath="Label";chordBox.SelectedValuePath="Id";}
        var page=new StackPanel{Margin=new(12,4,12,20)};
        Body.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var head=new DockPanel();var state=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(state,Dock.Right);head.Children.Add(state);
        state.Children.Add(status);Switch.Margin=new(14,0,0,0);state.Children.Add(Switch);head.Children.Add(Copy("Controller as mouse",28,true));page.Children.Add(head);
        page.Children.Add(Copy($"Player {player+1} · {this.model.Name}",16));
        var sourceRow=new WrapPanel{Margin=new(0,12,0,12)};sourceRow.Children.Add(Copy("Desktop input source",14));source.Margin=new(14,0,12,0);sourceRow.Children.Add(source);
        sourceRow.Children.Add(Copy("XInput index · Model identity is not verified by this interface",13));page.Children.Add(sourceRow);
        page.Children.Add(Copy("One player controls the desktop at a time. PC mode output pauses while Mouse Mode is on; physical game input can still reach games unless it is hidden by your PC mode setup.",13));
        var upper=new Grid{Margin=new(0,16,0,0)};upper.ColumnDefinitions.Add(new());upper.ColumnDefinitions.Add(new());page.Children.Add(upper);
        var motion=new StackPanel();motion.Children.Add(Row("Pointer source",pointer));motion.Children.Add(Row("Speed",speed,v=>$"{v:0} px/s"));motion.Children.Add(Row("Acceleration",curve,v=>$"{v:0.0} · None → Fast"));motion.Children.Add(Row("Dead zone",deadzone,v=>$"{v:0}%"));
        curve.ToolTip="Higher values make small pushes slower. Full stick deflection still uses the speed above.";
        test=Pill("Test pointer",ToggleTest);motion.Children.Add(test);preview.Children.Add(new Line{X1=200,X2=220,Y1=72,Y2=72,Stroke=Brushes.SlateGray});preview.Children.Add(new Line{X1=210,X2=210,Y1=62,Y2=82,Stroke=Brushes.SlateGray});dot.Visibility=Visibility.Collapsed;previewReadout.Visibility=Visibility.Collapsed;preview.Children.Add(dot);
        motion.Children.Add(new Viewbox{Child=preview,Stretch=Stretch.Uniform,MaxHeight=145,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,10,0,0)});motion.Children.Add(previewReadout);
        upper.Children.Add(Card("POINTER",motion));
        var extra=new StackPanel();extra.Children.Add(Row("Scrolling",scrolls));extra.Children.Add(scrollSource);extra.Children.Add(Row("Scroll speed",scroll,v=>$"{v:0} lines/s"));
        extra.Children.Add(precisionSource);extra.Children.Add(Row("Precision speed",precision,v=>$"{v:0}%"));
        var advanced=new StackPanel();advanced.Children.Add(Row("Horizontal scroll",horizontal));advanced.Children.Add(Row("Invert vertical",invertV));advanced.Children.Add(Row("Invert horizontal",invertH));extra.Children.Add(new Expander{Header="Scroll options",Content=advanced,Margin=new(0,8,0,8)});
        extra.Children.Add(Row("Quick toggle",combo));var chord=new WrapPanel();chord.Children.Add(chord1);chord.Children.Add(Copy(" + ",16));chord.Children.Add(chord2);chord.Children.Add(hold);extra.Children.Add(chord);
        extra.Children.Add(Copy("When enabled, both shortcut buttons are reserved, including single presses. Their normal mouse mappings will not run.",12));
        var right=Card("SCROLL & PRECISION",extra);Grid.SetColumn(right,1);upper.Children.Add(right);
        var editor=new Grid{Margin=new(0,14,0,0)};editor.ColumnDefinitions.Add(new(){Width=new(1.2,GridUnitType.Star)});editor.ColumnDefinitions.Add(new());page.Children.Insert(page.Children.IndexOf(upper),editor);
        var visual=new StackPanel();visual.Children.Add(Copy("BUTTON MAPPINGS",14,true));visual.Children.Add(mapped);
        var map=new Grid{Width=1536,Height=1024};map.Children.Add(new PhotoController(this.model.Id){IsHitTestVisible=false});var overlay=new Canvas();map.Children.Add(overlay);
        var available=(layout?.Controls.Select(r=>r.Key)??this.model.FrontButtons.Keys).ToHashSet();
        foreach(var chordBox in new[]{chord1,chord2})chordBox.ItemsSource=PadButtons.Mask.Keys.Where(available.Contains).Select(k=>new InputPick(k,InputName(k))).ToArray();
        // XInput currently exposes standard controls only. Art or physical paddles alone do not establish independent input.
        foreach(var input in MouseProfile.Inputs.Where(k=>available.Contains(k)))
        {
            var button=Pill(InputName(input),()=>Select(input));mappings[input]=button;
            if(layout?.Controls.FirstOrDefault(r=>r.Key==input && r.View=="front") is {} region)
            {var hot=new PhysicalHotspot(region,false){ToolTip=input};hot.Click+=(_,_)=>Select(input);var at=LiveController.Place(this.model.Id,region);Canvas.SetLeft(hot,at.X);Canvas.SetTop(hot,at.Y);overlay.Children.Add(hot);hotspots[input]=hot;register(hot);}
        }
        visual.Children.Add(new Viewbox{Child=map,Stretch=Stretch.Uniform,MaxHeight=270});
        var overview=new WrapPanel();foreach(var button in mappings.Values)overview.Children.Add(button);visual.Children.Add(overview);
        visual.Children.Add(Copy("Press an input or select it on the artwork. Controller navigation pauses on this page. Gyro, touch surfaces and independent rear inputs are unavailable from this XInput source.",12));editor.Children.Add(Card("",visual));
        var assign=new StackPanel();assign.Children.Add(selected);assign.Children.Add(Copy("Choose its desktop action",14));assign.Children.Add(actionChoices);var assignCard=Card("ASSIGN INPUT",assign);Grid.SetColumn(assignCard,1);editor.Children.Add(assignCard);
        var presets=new WrapPanel{Margin=new(0,12,0,0)};
        foreach(var name in new[]{"Desktop","Web browsing","Media","Minimal"}) {string preset=name;presets.Children.Add(Pill(name,()=>Preset(preset)));}presets.Children.Add(Pill("Reset Mouse Mode",()=>{Save(new MouseProfile{SourceSlot=profile.SourceSlot});Fill();Select(selection);}));page.Children.Add(presets);page.Children.Add(saved);
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
    void UpdateMappings()
    {
        mapped.Text=$"{mappings.Keys.Count(k=>profile.Buttons.GetValueOrDefault(k)!=MouseAction.None && !(profile.Combo&&profile.ToggleButtons.Contains(k)))} / {mappings.Count} configured";
        foreach(var (key,button) in mappings){button.Content=$"{InputName(key)}   {(profile.Combo&&profile.ToggleButtons.Contains(key)?"Shortcut reserved":Friendly(profile.Buttons.GetValueOrDefault(key)))}";button.ToolTip=button.Content;}
        scrollSource.Text=$"Scroll source · {(profile.PointerStick=="Left"?"Right":"Left")} stick";
        precisionSource.Text="Precision activator · "+string.Join(" / ",mappings.Keys.Where(k=>profile.Buttons.GetValueOrDefault(k)==MouseAction.Precision).Select(InputName).DefaultIfEmpty("Unassigned"));
    }
    public void Select(string key)
    {
        selection=key;selected.Text=InputName(key);foreach(var pair in hotspots)pair.Value.Illuminate(pair.Key==key,false);
        actionChoices.Children.Clear();
        foreach(var group in Enum.GetValues<MouseAction>().GroupBy(Category))
        {
            var choices=new WrapPanel();foreach(var action in group){var pick=action;var b=Pill(Friendly(action),()=>{var next=profile.Clone();next.Buttons[selection]=pick;Save(next);Select(selection);});if(profile.Buttons.GetValueOrDefault(key)==action)b.BorderBrush=Brushes.LightSkyBlue;choices.Children.Add(b);}
            actionChoices.Children.Add(new Expander{Header=group.Key,Content=choices,IsExpanded=group.Contains(profile.Buttons.GetValueOrDefault(key)),Margin=new(0,6,0,0)});
        }
    }
    void Preset(string name)
    {
        var next=profile.Clone();next.Buttons=MouseProfile.Defaults();
        if(name=="Minimal")foreach(var key in next.Buttons.Keys.ToArray())if(key is not ("A" or "B" or "LT"))next.Buttons[key]=MouseAction.None;
        if(name=="Web browsing"){next.Buttons["X"]=MouseAction.PageDown;next.Buttons["Y"]=MouseAction.PageUp;}
        if(name=="Media"){next.Buttons["A"]=MouseAction.PlayPause;next.Buttons["B"]=MouseAction.Mute;next.Buttons["LT"]=MouseAction.VolumeDown;next.Buttons["RT"]=MouseAction.VolumeUp;}
        Save(next);Select(selection);
    }
    void ToggleTest()
    {
        if(testing){EndPreview();return;}
        if(profile.SourceSlot<0){saved.Text="Choose the XInput source to test first.";return;}
        try{service.TakeControl(player,model.Id,true);testing=true;preview.Visibility=previewReadout.Visibility=Visibility.Visible;previewMapper=new(profile);px=210;py=72;test.Content="Stop pointer test";}
        catch(InvalidOperationException error){service.Previewing=false;saved.Text=error.Message;}
    }
    void EndPreview(){if(!testing)return;if(OwnsSource){service.Active=false;service.Previewing=false;}testing=false;previewMapper=null;test.Content="Test pointer";previewReadout.Text="Pointer test stopped · Windows output off";}
    void Tick()
    {
        Refresh();if(!OwnsSource)return;var pad=service.Latest;
        ushort pressed=(ushort)(pad.Buttons & ~previous);previous=pad.Buttons;
        // Button capture is read-only. It never changes a mapping until the user chooses an action.
        if((!service.Active||testing) && pressed!=0)foreach(var (name,mask) in PadButtons.Mask)if((pressed&mask)!=0 && mappings.ContainsKey(name)){Select(name);break;}
        if(!testing)return;
        dot.Visibility=service.Connected?Visibility.Visible:Visibility.Collapsed;
        if(!service.Connected){previewReadout.Text="Waiting for the selected physical source · No pointer data";return;}
        var frame=previewMapper!.Step(pad,.016);px=Math.Clamp(px+frame.Dx,5,415);py=Math.Clamp(py+frame.Dy,5,140);Canvas.SetLeft(dot,px-5);Canvas.SetTop(dot,py-5);
        double x=profile.PointerStick=="Left"?pad.LeftX:pad.RightX,y=profile.PointerStick=="Left"?pad.LeftY:pad.RightY,m=Math.Min(1,Math.Sqrt(x*x+y*y)/32768);
        previewReadout.Text=$"Input {m:P0} · {MouseMapper.Push(m,profile.Deadzone,profile.Curve)*profile.Speed*(previewMapper.Precision?profile.PrecisionPercent/100:1):0} px/s · Precision {(previewMapper.Precision?"On":"Off")} · Windows output paused";
    }
    void Refresh()
    {
        loading=true;Switch.IsChecked=OwnsSource&&service.Active&&!testing;loading=false;
        status.Text=testing?"Preview":service.Active&&!OwnsSource?$"Desktop controlled by Player {service.OwnerPlayer+1}":OwnsSource&&service.Active?service.Connected?"Active":"Waiting for selected source":"Off";
    }
    static string Category(MouseAction a)=>a switch{MouseAction.None=>"DISABLED",<=MouseAction.Forward=>"MOUSE",MouseAction.Precision=>"PRECISION",>=MouseAction.VolumeUp=>"MEDIA",MouseAction.Windows or MouseAction.TaskView or MouseAction.OnScreenKeyboard=>"WINDOWS",_=>"KEYBOARD"};
    public static string Friendly(MouseAction a)=>a switch{MouseAction.None=>"Disabled",MouseAction.Precision=>"Precision mode",MouseAction.OnScreenKeyboard=>"On-screen keyboard",MouseAction.TaskView=>"Task View",MouseAction.Windows=>"Start",MouseAction.PlayPause=>"Play / pause",_=>System.Text.RegularExpressions.Regex.Replace(a.ToString(),"([a-z])([A-Z])","$1 $2").ToLowerInvariant() is var text ? char.ToUpperInvariant(text[0])+text[1..] : ""};
    static TextBlock Copy(string text,double size,bool bold=false)=>Kit.Text(text,size,bold?"DS.Text":"DS.TextSoft",bold?FontWeights.SemiBold:FontWeights.Normal).Also(t=>{t.TextWrapping=TextWrapping.Wrap;t.TextTrimming=TextTrimming.None;t.Margin=new(0,4,0,4);});
    Button Pill(string text,Action click){var b=new Button{Content=text,Style=DS.Style("ActionButton"),Height=34,MinHeight=0,Padding=new(14,0,14,0),Margin=new(0,3,6,3),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>click();register(b);return b;}
    static ComboBox Choice(string[] labels)=>new(){ItemsSource=labels,MinWidth=118,MaxWidth=190,MinHeight=34,Margin=new(0,2,6,2)};
    static Slider Slider(double min,double max,double step)=>new(){Style=DS.Style("DS.Slider"),Minimum=min,Maximum=max,TickFrequency=step,IsSnapToTickEnabled=true,SmallChange=step,LargeChange=step*5,MinWidth=90,VerticalAlignment=VerticalAlignment.Center};
    static CheckBox Toggle()=>new(){Style=DS.Style("DS.Toggle"),VerticalAlignment=VerticalAlignment.Center};
    static Border Card(string title,UIElement content)=>new(){Margin=new(0,0,12,0),Padding=new(18),CornerRadius=new(18),BorderThickness=new(1),BorderBrush=new SolidColorBrush(Color.FromArgb(50,155,194,240)),Background=new SolidColorBrush(Color.FromArgb(95,8,17,38)),Child=new StackPanel{Children={Copy(title,13,true),content}}};
    static Grid Row(string label,FrameworkElement control){var row=new Grid{Margin=new(0,5,0,5)};row.ColumnDefinitions.Add(new(){Width=new(132)});row.ColumnDefinitions.Add(new());row.Children.Add(Copy(label,14));Grid.SetColumn(control,1);row.Children.Add(control);return row;}
    static Grid Row(string label,Slider slider,Func<double,string> format){var panel=new Grid();panel.ColumnDefinitions.Add(new());panel.ColumnDefinitions.Add(new(){Width=new(132)});panel.Children.Add(slider);var value=Copy(format(slider.Value),13);Grid.SetColumn(value,1);panel.Children.Add(value);slider.ValueChanged+=(_,_)=>value.Text=format(slider.Value);return Row(label,panel);}
}
