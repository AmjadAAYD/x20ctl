using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed class ProductHost : Grid
{
    public ZoneScene Zone { get; }
    public ControllerCatalog Catalog { get; }
    public StudioScene? Studio { get; private set; }
    public ModelSelectorView? Selector { get; private set; }
    public IntroView? Intro {get;private set;}
    public ProductSettings? Settings {get;private set;}
    public NativeEngineClient? Engine {get;private set;}
    public string NativeEngineStatus {get;private set;}="Native core not connected";
    public JsonElement? NativeCapabilities {get;private set;}
    public long InputPresentationCoalesced {get;private set;}
    private string? nativeEpoch,nativeSubscription;
    private readonly SemaphoreSlim nativeContextLock=new(1,1);
    private readonly object nativeEventLock=new();private JsonElement? latestNativeEvent;private int nativeEventScheduled;
    private HashSet<string> lastNativeButtons=new();private bool nativeArmed;private long lastNativeSequence=-1,lastNavigationUs;
    private string ExpectedNativeContext=>Studio?.InputContext==InputOwner.TesterCapture?"TESTER_CAPTURE":"UI_NAVIGATION";
    public bool IntroWasPlayed {get;private set;}
    private readonly ProductPreferences preferences;
    private readonly bool preferencesHealthy;
    private readonly Action<string> support;
    private readonly AssignmentStore store;
    private readonly Dictionary<(int, string), ButtonDraft> drafts = new();
    private readonly Dictionary<(int, string), CurveDraft> curves = new();
    private readonly Dictionary<(int, string), MacroDraft> macros = new();
    private readonly Dictionary<(int, string), VibrationDraft> vibrations = new();
    private readonly Dictionary<(int,string),string?> activeSetups=new();
    private readonly SetupStore setupStore;
    private readonly IReadOnlyList<string> legacySources;
    private int selectorPlayer;
    public ProductHost(ZoneScene zone, string? assignmentPath = null,Action<string>? openSupport=null)
    {
        Zone = zone; Children.Add(zone);
        support=openSupport??ProductLinks.OpenSupport;
        preferences=new(assignmentPath==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"x20ctl","native","preferences.v1.json"):Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"preferences-fixture.json"));
        try{preferences.Load();preferencesHealthy=true;}catch(Exception error) when(error is IOException or System.Text.Json.JsonException or InvalidOperationException){Zone.FeedbackText.Text="Startup preferences unavailable; original file preserved. "+error.Message;}
        Zone.SupportRequested+=OpenSupport;Zone.SettingsRequested+=ShowSettings;
        Catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        store = new(assignmentPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "native", "player-assignments.v1.json"));
        string dataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"x20ctl");
        setupStore=new(assignmentPath==null?Path.Combine(dataRoot,"native","setups.v1.json"):Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"setups-fixture-"+Guid.NewGuid().ToString("N")+".json"));
        var sources=new List<string>();if(assignmentPath==null){string desktop=Path.Combine(dataRoot,"desktop","profiles.json");if(File.Exists(desktop))sources.Add(desktop);string cli=Path.Combine(dataRoot,"profiles");if(Directory.Exists(cli))sources.AddRange(Directory.GetFiles(cli,"*.json",SearchOption.TopDirectoryOnly));}legacySources=sources.AsReadOnly();
        // every launch opens on an empty Zone (owner direction 10 Oct 2026); review fixtures still restore their files
        try { var ids = store.Load(); if (assignmentPath != null && ids.Any(id => id != null)) Zone.RestoreAssignments(ids, Catalog); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Zone.FeedbackText.Text = "Saved assignments could not be read. Original file preserved; new choices will be session-only."; }
        Zone.ControllerActionRequested += (index, change) => { if (change || Zone.State.Players[index].Model == null) OpenSelector(index); else EnterStudio(index); };
    }
    public void OpenSelector(int player)
    {
        if (player is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(player));
        CloseSelector(); selectorPlayer = player;
        Selector = new(Catalog, player); Selector.Chosen += AssignSelection; Selector.Cancelled += CloseSelector;
        Zone.IsEnabled = false; if (Studio != null) Studio.IsEnabled = false;
        Children.Add(Selector); Panel.SetZIndex(Selector, 10); Fx.Appear(Selector, .97, 300);
    }
    public void AssignSelection(ControllerModel model)
    {
        int player = selectorPlayer; CloseSelector(); ReturnToZone();
        Zone.AssignController(player, model);
        try { store.Save(Zone.State.Players.Select(p => p.ModelId).ToArray()); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { Zone.FeedbackText.Text = "Assigned for this session. Existing saved data was not overwritten because saving failed."; }
    }
    public void CloseSelector()
    {
        if (Selector != null) { var leaving = Selector; Selector = null; Fx.Leave(leaving, () => Children.Remove(leaving), .97, 200); }
        Zone.IsEnabled = true; if (Studio != null) Studio.IsEnabled = true;
    }
    public void EnterStudio(int player)
    {
        var assigned = Zone.State.Players[player];
        var model = assigned.ModelId != null ? Catalog.Find(assigned.ModelId) : Catalog.Models.FirstOrDefault(m => m.Name == assigned.Model);
        if (model == null) { Zone.FeedbackText.Text = "Choose a known controller model before entering Studio."; return; }
        if (Studio != null) { var old = Studio; Studio = null; Fx.Leave(old, () => Children.Remove(old), 1.02, 160); }
        if (!drafts.TryGetValue((player, model.Id), out var draft)) drafts[(player, model.Id)] = draft = new(model.Id);
        if (!curves.TryGetValue((player, model.Id), out var curveDraft)) curves[(player, model.Id)] = curveDraft = new(model.Id);
        if (!macros.TryGetValue((player, model.Id), out var macroDraft)) macros[(player, model.Id)] = macroDraft = new(model.Id);
        if (!vibrations.TryGetValue((player, model.Id), out var vibrationDraft)) vibrations[(player, model.Id)] = vibrationDraft = new(model.Id);
        var key=(player,model.Id);activeSetups.TryGetValue(key,out var activeSetup);
        Studio = new(player, model, draft, Zone.ReducedMotion, curveDraft,macroDraft,vibrationDraft,setupStore,legacySources,activeSetup); Studio.BackRequested += ReturnToZone;Studio.SupportRequested+=OpenSupport;Studio.ActiveSetupChanged+=id=>activeSetups[key]=id;
        Studio.NativeEngineStatus=NativeEngineStatus;
        Studio.InputContextChanged+=()=>_=UpdateNativeContext();
        if(Engine!=null)_=RefreshNativeCapabilities(model.Id);
        // fwoosh into the Studio: it zooms in over the Zone, which sinks back, and only then is the Zone put away
        Children.Add(Studio); var entering = Studio;
        Fx.Appear(Studio, 1.07, 380, () => { if (Studio == entering) Zone.Visibility = Visibility.Collapsed; });
        if (SystemParameters.ClientAreaAnimation) Fx.Leave(Zone, () => { Zone.BeginAnimation(OpacityProperty, null); Zone.Opacity = 1; Zone.IsHitTestVisible = true; if (Studio != null) Zone.Visibility = Visibility.Collapsed; Zone.RenderTransform = null; }, .94, 380);
        else Zone.Visibility = Visibility.Collapsed;
    }
    public void ReturnToZone()
    {
        bool back = Studio != null;
        if (Studio != null) { var leaving = Studio; Studio = null; Fx.Leave(leaving, () => Children.Remove(leaving), 1.06, 300); }
        Zone.BeginAnimation(OpacityProperty, null); Zone.Opacity = 1; Zone.IsHitTestVisible = true; Zone.IsEnabled = Selector == null && Settings == null && Intro == null;
        Zone.Visibility = Visibility.Visible;
        if (back) Fx.Appear(Zone, .94, 380);
        _=UpdateNativeContext();
    }
    public void Startup()=>StartIntro(); // every launch opens with the intro, like Steam Big Picture (owner direction 10 Oct 2026)
    public async Task InitializeNativeEngineAsync()
    {
        if(Engine!=null)return;
        try{Engine=new();Engine.EventReceived+=QueueNativeEvent;Engine.Disconnected+=reason=>Dispatcher.BeginInvoke(new Action(()=>{nativeSubscription=null;nativeArmed=false;NativeEngineStatus="Native core disconnected · Configuration unavailable";if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);Studio.Tester?.ExitCapture();}}));var hello=await Engine.Request("handshake",new{});if(!hello.TryGetProperty("engineEpoch",out var epoch))throw new InvalidDataException("Native handshake omitted epoch.");nativeEpoch=epoch.GetString();NativeEngineStatus="Native C++ core running · Configuration locked";Zone.ProvenanceText.Text="LOCAL ASSIGNMENTS";await RefreshNativeCapabilities(Studio?.Model.Id??"x20");await UpdateNativeContext();}
        catch(Exception error){NativeEngineStatus="Native core unavailable · "+error.Message;if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);}Zone.FeedbackText.Text="Native connection unavailable; local previews remain available.";}
    }
    private async Task RefreshNativeCapabilities(string model)
    {
        try{if(Engine==null)return;NativeCapabilities=await Engine.Request("capabilities",new{model});if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);}}
        catch(Exception error){NativeEngineStatus="Native capability query unavailable · "+error.Message;}
    }
    public async Task StopNativeEngineAsync(){nativeSubscription=null;if(Engine!=null){var owned=Engine;Engine=null;await owned.DisposeAsync();}}
    private async Task UpdateNativeContext()
    {
        if(Engine==null||nativeEpoch==null)return;await nativeContextLock.WaitAsync();
        try{if(Engine==null)return;nativeSubscription=null;nativeArmed=false;lastNativeButtons.Clear();lastNativeSequence=-1;string context=ExpectedNativeContext;var response=await Engine.Request("subscribe",new{stream="input",slot=0,context});if(context==ExpectedNativeContext)nativeSubscription=response.GetProperty("subscriptionId").GetString();}
        catch(Exception error){NativeEngineStatus="Native input unavailable · "+error.Message;}finally{nativeContextLock.Release();}
    }
    private void QueueNativeEvent(JsonElement message)
    {
        lock(nativeEventLock){if(latestNativeEvent.HasValue)InputPresentationCoalesced++;latestNativeEvent=message;}
        if(Interlocked.Exchange(ref nativeEventScheduled,1)==0)Dispatcher.BeginInvoke(new Action(ConsumeNativeEvent));
    }
    private void ConsumeNativeEvent()
    {
        JsonElement? message;lock(nativeEventLock){message=latestNativeEvent;latestNativeEvent=null;Interlocked.Exchange(ref nativeEventScheduled,0);}if(!message.HasValue)return;var value=message.Value;
        try{
            if(value.GetProperty("engineEpoch").GetString()!=nativeEpoch||value.GetProperty("subscriptionId").GetString()!=nativeSubscription||value.GetProperty("inputContext").GetString()!=ExpectedNativeContext)return;
            long sequence=value.GetProperty("sequence").GetInt64();if(sequence<=lastNativeSequence)return;lastNativeSequence=sequence;
            var payload=value.GetProperty("payload");bool connected=payload.GetProperty("connected").GetBoolean();Zone.ScenarioHint.Text=connected?"XInput navigation · Model identity unverified · Configuration locked":"No gameplay source connected · Configuration locked";
            if(connected)Studio?.ObserveLiveInput(payload.GetProperty("leftX").GetDouble(),payload.GetProperty("leftY").GetDouble(),payload.GetProperty("rightX").GetDouble(),payload.GetProperty("rightY").GetDouble(),payload.GetProperty("lt").GetDouble(),payload.GetProperty("rt").GetDouble());
            // virtual buttons: triggers and left stick become digital presses with a threshold
            var now=payload.GetProperty("buttons").EnumerateArray().Select(v=>v.GetString()!).ToHashSet();
            if(connected){if(payload.GetProperty("lt").GetDouble()>.55)now.Add("LT");if(payload.GetProperty("rt").GetDouble()>.55)now.Add("RT");
                double lx=payload.GetProperty("leftX").GetDouble(),ly=payload.GetProperty("leftY").GetDouble();if(Math.Abs(lx)>.6&&Math.Abs(lx)>=Math.Abs(ly))now.Add(lx>0?"LS_RIGHT":"LS_LEFT");else if(Math.Abs(ly)>.6)now.Add(ly>0?"LS_UP":"LS_DOWN");}
            var pressed=now.Except(lastNativeButtons).ToArray();lastNativeButtons=now;
            long time=value.GetProperty("timestampUs").GetInt64();
            // Home / Guide: bring X20CTL forward from anywhere (tray, behind a game); in front it goes home
            if(connected&&pressed.Contains("HOME")){HomeRequested?.Invoke();return;}
            if(Studio?.ManagementPage=="Tester")
            {
                // the Tester owns every input; only a deliberate one-second hold of B leaves it
                if(now.Contains("B")){if(bHeldSinceUs==0)bHeldSinceUs=time;else if(time-bHeldSinceUs>=1_000_000){bHeldSinceUs=0;Studio.ShowManagement("Device");nativeArmed=false;return;}}else bHeldSinceUs=0;
                Studio.Tester?.ReceiveNative(value);nativeArmed=false;return;
            }
            if(!connected||Window.GetWindow(this)?.IsActive!=true){nativeArmed=false;return;}if(!nativeArmed){if(now.Count==0)nativeArmed=true;return;}
            if(Intro!=null){if(pressed.Contains("B"))Intro.Finish();return;}
            if(Settings!=null){if(pressed.Contains("B"))CloseSettings();else if(pressed.Contains("A")&&Keyboard.FocusedElement is Button focused&&focused.IsEnabled)focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else foreach(var d in new[]{("DPAD_UP",FocusNavigationDirection.Up),("DPAD_DOWN",FocusNavigationDirection.Down),("DPAD_LEFT",FocusNavigationDirection.Left),("DPAD_RIGHT",FocusNavigationDirection.Right)})if(pressed.Contains(d.Item1)&&Keyboard.FocusedElement is UIElement el)el.MoveFocus(new(d.Item2));return;}
            if(Studio!=null)Studio.EnableNativeNavigation();else Zone.EnableNativeNavigation();
            foreach(var (button,key) in InputMap.Controller)
            {bool repeat=(button.StartsWith("DPAD")||button.StartsWith("LS_"))&&now.Contains(button)&&time-lastNavigationUs>200000;if(!pressed.Contains(button)&&!repeat)continue;lastNavigationUs=time;var hostWindow=Window.GetWindow(this);if(hostWindow==null)return;var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(hostWindow)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};OnKeyDown(this,e);OnKeyUp(this,new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(hostWindow)!,0,key){RoutedEvent=Keyboard.PreviewKeyUpEvent});}
        }catch(Exception error) when(error is InvalidOperationException or ArgumentException or JsonException){nativeArmed=false;ResetTransientInput();NativeEngineStatus="Native input unavailable · "+error.Message;}
    }
    public void StartIntro()
    {
        CloseSettings();ReturnToZone();if(Intro!=null)return;IntroWasPlayed=true;Zone.IsEnabled=false;Zone.Opacity=0;
        try{Intro=new(Zone.ReducedMotion);var zoom=new System.Windows.Media.ScaleTransform(1.06,1.06);Zone.RenderTransformOrigin=new(.5,.5);Zone.RenderTransform=zoom;Intro.ZoneReveal+=amount=>{Zone.Opacity=amount;zoom.ScaleX=zoom.ScaleY=1.06-.06*amount;if(amount>=1)Zone.RenderTransform=null;};Intro.Completed+=CompleteIntro;Children.Add(Intro);Panel.SetZIndex(Intro,30);Intro.Start();}
        catch(Exception error){CompleteIntro();Zone.FeedbackText.Text="Startup animation unavailable. Controller Zone is ready. "+error.Message;}
    }
    private void CompleteIntro()
    {
        if(Intro!=null){Children.Remove(Intro);Intro=null;}Zone.Opacity=1;Zone.IsEnabled=true;
        if(preferencesHealthy)try{preferences.MarkIntroSeen();}catch(Exception error) when(error is IOException or InvalidOperationException){Zone.FeedbackText.Text="Startup preference could not be saved; original file preserved. "+error.Message;}
    }
    private void OpenSupport(){try{support(ProductLinks.Support);}catch(Exception error){Zone.FeedbackText.Text="Could not open your browser. Support X20CTL: "+ProductLinks.Support+" · "+error.Message;}}
    public void ShowSettings(){ReturnToZone();CloseSettings();Zone.IsEnabled=false;Settings=new(OpenSupport,StartIntro,CloseSettings,OpenCheck);Children.Add(Settings);Panel.SetZIndex(Settings,20);Fx.Appear(Settings,1.04,320);Settings.FocusFirst();}
    /// <summary>"Controller not working?": the Controller Check (Scanner 2.0.0 engine) as its own full-window app over Tools.</summary>
    public Scanner.ControllerCheckView? Check {get;private set;}
    public void OpenCheck(){if(Check!=null)return;Check=new(CloseCheck);Children.Add(Check);Panel.SetZIndex(Check,40);Fx.Appear(Check,1.06,360);}
    public void CloseCheck(){if(Check==null||!Check.TryClose())return;var closing=Check;Check=null;closing.Shutdown();Fx.Leave(closing,()=>Children.Remove(closing),1.05,240);Settings?.Check.Focus();}
    public void CloseSettings(){if(Settings!=null){var leaving=Settings;Settings=null;Fx.Leave(leaving,()=>Children.Remove(leaving),1.03,220);}Zone.IsEnabled=true;}
    public void ResetTransientInput(){nativeArmed=false;Zone.SuspendNavigation();Studio?.Tester?.ClearKeyboardKeys();Studio?.ReleaseTransientInput();}
    public event Action? HomeRequested;
    private long bHeldSinceUs;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key=e.Key==Key.System?e.SystemKey:e.Key;
        // the Controller Check is testing the controller: its presses are never navigation; the keyboard's Esc leaves
        if(Check!=null){if(sender is ProductHost){e.Handled=true;return;}if(e.Key==Key.Escape){CloseCheck();e.Handled=true;}return;}
        // remapping on the Buttons page: Menu, View and B are buttons being assigned, not navigation
        if(Studio?.CapturingRemap==true && Intro==null && Settings==null){Studio.OnKeyDown(sender,e);return;}
        if(key==InputMap.Settings && Intro==null){if(Settings!=null)CloseSettings();else ShowSettings();e.Handled=true;return;}
        if(key==InputMap.ControllerZone && Intro==null && Settings==null && Studio!=null && Studio.ManagementPage!="Tester"){ReturnToZone();e.Handled=true;return;}
        if(Intro!=null){if(e.Key is Key.Escape or Key.B)Intro.Finish();e.Handled=true;return;}
        if(Settings!=null){if(e.Key is Key.Escape or Key.B){if(!Settings.StepBack())CloseSettings();e.Handled=true;}else if(e.Key==Key.A && Keyboard.FocusedElement is Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}else if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right && Keyboard.FocusedElement is UIElement focused){focused.MoveFocus(new(e.Key==Key.Up?FocusNavigationDirection.Up:e.Key==Key.Down?FocusNavigationDirection.Down:e.Key==Key.Left?FocusNavigationDirection.Left:FocusNavigationDirection.Right));e.Handled=true;}return;}
        // the controller picker: arrows / D-pad move between cards, A / Enter chooses, B / Esc goes back
        if (Selector != null)
        {
            if (e.Key is Key.Escape or Key.B) { CloseSelector(); e.Handled = true; }
            else if (e.Key is Key.A or Key.Enter && Keyboard.FocusedElement is Button card) { if (!e.IsRepeat) card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
            else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right && Keyboard.FocusedElement is UIElement focused) { focused.MoveFocus(new(e.Key == Key.Up ? FocusNavigationDirection.Up : e.Key == Key.Down ? FocusNavigationDirection.Down : e.Key == Key.Left ? FocusNavigationDirection.Left : FocusNavigationDirection.Right)); e.Handled = true; }
            return;
        }
        if (Studio != null) Studio.OnKeyDown(sender, e); else Zone.OnKeyDown(sender, e);
    }
    public void OnKeyUp(object sender, KeyEventArgs e) { if(Intro!=null||Settings!=null||Check!=null)return;if(Studio!=null)Studio.OnKeyUp(sender,e);else if (Selector == null) Zone.OnKeyUp(sender, e); }
}
