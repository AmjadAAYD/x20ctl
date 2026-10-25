using System.Windows.Media.Animation;
﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.Json;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed class ProductHost : Grid
{
    private string?[] batteryLevels=new string?[4];
    private string? currentTransport;
    public string?[] BatteryLevels => batteryLevels;
    private string? currentFirmware;
    private TelemetryData? latestTelemetry;

    public void RecordTelemetry(TelemetryData telemetry, int player = 0)
    {
        latestTelemetry = telemetry;
        if (telemetry.Firmware != null) currentFirmware = telemetry.Firmware;
        if (telemetry.Battery != null && player < batteryLevels.Length) batteryLevels[player] = telemetry.Battery;
    }

    public async Task RefreshBleTelemetryAsync()
    {
        string? modelId = Studio?.Model.Id ?? (Zone.State.Players.Count > 0 ? Zone.State.Players[0].ModelId ?? Catalog.Models.FirstOrDefault(m => m.Name == Zone.State.Players[0].Model)?.Id : null);
        if (modelId is not ("x20" or "x20_pro")) return;

        try
        {
            var res = await PadBridge.Status(modelId);
            if (res.Ok && res.Telemetry != null)
            {
                latestTelemetry = res.Telemetry;
                if (res.Telemetry.Firmware != null) currentFirmware = res.Telemetry.Firmware;
                if (res.Telemetry.Battery != null)
                {
                    int player = Studio?.Player ?? 0;
                    if (player < batteryLevels.Length) batteryLevels[player] = res.Telemetry.Battery;
                }
                _ = Dispatcher.BeginInvoke(() =>
                {
                    if (Studio != null)
                    {
                        Studio.ApplyTelemetry(res.Telemetry);
                    }
                });
            }
        }
        catch { }
    }

    public void UpdateBatteryLevels(string?[] levels, string? transport = null)
    {
        for (int i = 0; i < Math.Min(levels.Length, batteryLevels.Length); i++)
        {
            // If we have a verified Bluetooth telemetry reading for 2.4 GHz receiver, do not overwrite with generic receiver placeholder
            if (transport == "2.4 GHz receiver" && (levels[i] is "receiver" or "wired" or null) &&
                batteryLevels[i] != null && batteryLevels[i] != "receiver" && batteryLevels[i] != "wired")
            {
                continue;
            }
            batteryLevels[i] = levels[i];
        }
        if (transport != null) currentTransport = transport;
        if (Studio is { } studio)
        {
            if (currentTransport != null) studio.SetTransport(currentTransport);
            studio.SetBatteryLevel(batteryLevels[studio.Player]);
        }
    }
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
    private string ExpectedNativeContext=>Studio?.InputContext is InputOwner.TesterCapture or InputOwner.RemapCapture?"TESTER_CAPTURE":"UI_NAVIGATION";
    public bool IntroWasPlayed {get;private set;}
    private readonly ProductPreferences preferences;
    private readonly bool preferencesHealthy;
    /// <summary>PC mode (Tools): on by default, but it only does anything once a setting has been changed.</summary>
    public PcModeService? PcMode { get; }
    /// <summary>Controller as mouse. A fixture window gets one that never listens to the controller.</summary>
    public MouseModeService? MouseMode { get; }
    private readonly X20Ctl.Zone.PcMode.PcModeStore? pcModeStore;
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
        preferences=new(assignmentPath==null?PreferencesPath:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"preferences-fixture.json"));
        UiSound.Allowed=()=>!preferencesHealthy||preferences.IntroSound;try{preferences.Load();preferencesHealthy=true;}catch(Exception error) when(error is IOException or System.Text.Json.JsonException or InvalidOperationException){Zone.FeedbackText.Text="Startup preferences unavailable; original file preserved. "+error.Message;}
        // every launch asks GitHub for a newer version (owner request, 13 Oct 2026); a popup follows the intro only when there is one.
        // Tools → "Keep X20CTL up to date" decides whether it also installs itself. Never for review fixtures.
        if(assignmentPath==null)_=CheckUpdatesAtLaunch(preferencesHealthy&&preferences.AutoUpdate);
        // a review or fixture window gets a store beside its own files and a service that never starts
        pcModeStore=new(assignmentPath==null?X20Ctl.Zone.PcMode.PcModeStore.DefaultPath:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"pc-mode-fixture.json"));pcModeStore.Load();
        PcMode=new(pcModeStore){Enabled=assignmentPath==null&&(!preferencesHealthy||preferences.PcMode)};
        MouseMode=new(assignmentPath==null?X20Ctl.Zone.Mouse.MouseProfile.DefaultPath:Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"mouse-fixture.json"),listen:assignmentPath==null){PcMode=PcMode};
        Zone.SupportRequested+=OpenSupport;Zone.SettingsRequested+=ShowSettings;
        Catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        store = new(assignmentPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "native", "player-assignments.v1.json"));
        string dataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"x20ctl");
        PadColours.Use(assignmentPath==null?Path.Combine(dataRoot,"native","pad-colours.v1.json"):Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"pad-colours-fixture.json"));
        setupStore=new(assignmentPath==null?Path.Combine(dataRoot,"native","setups.v1.json"):Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assignmentPath))!,"setups-fixture-"+Guid.NewGuid().ToString("N")+".json"));
        var sources=new List<string>();if(assignmentPath==null){string desktop=Path.Combine(dataRoot,"desktop","profiles.json");if(File.Exists(desktop))sources.Add(desktop);string cli=Path.Combine(dataRoot,"profiles");if(Directory.Exists(cli))sources.AddRange(Directory.GetFiles(cli,"*.json",SearchOption.TopDirectoryOnly));}legacySources=sources.AsReadOnly();
        // every launch opens on an empty Zone (owner direction 10 Oct 2026); review fixtures still restore their files
        try { var ids = store.Load(); if (assignmentPath != null && ids.Any(id => id != null)) Zone.RestoreAssignments(ids, Catalog); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Zone.FeedbackText.Text = "Saved assignments could not be read. Original file preserved; new choices will be session-only."; }
        Zone.ControllerRemoved += index => {try{store.Save(Zone.State.Players.Select(p=>p.ModelId).ToArray());}catch(Exception error)when(error is IOException or UnauthorizedAccessException or InvalidOperationException){Zone.FeedbackText.Text="Removed for this session; saved assignments could not be updated.";}};
        Zone.ControllerActionRequested += (index, change) => { if (change || Zone.State.Players[index].Model == null) OpenSelector(index); else VerifyThenEnter(index); };
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
    // owner, 14 Oct 2026: opening a controller scans first. A pad that names itself as another model is never opened as this
    // one; a pad that can't be told apart, or none at all, is shown as such and the player decides.
    private Grid? verifyLayer; private int verifyToken;
    private async void VerifyThenEnter(int player)
    {
        var assigned = Zone.State.Players[player];
        var model = assigned.ModelId != null ? Catalog.Find(assigned.ModelId) : Catalog.Models.FirstOrDefault(m => m.Name == assigned.Model);
        if (model == null || ReviewSandbox.Active || verifyLayer != null) { EnterStudio(player); return; }
        int token = ++verifyToken;
        var view = new ControllerVerifyView(model.Id, model.Name, Zone.ReducedMotion == false);
        var layer = new Grid { Children = { view } }; verifyLayer = layer;
        void Close() { if (verifyLayer != layer) return; verifyLayer = null; verifyToken++; Zone.IsEnabled = Selector == null && Settings == null && Intro == null; Fx.Leave(layer, () => Children.Remove(layer), 1.03, 180); }
        view.Cancelled += Close;
        Zone.IsEnabled = false; Panel.SetZIndex(layer, 36); Children.Add(layer); Fx.Appear(layer, .96, 220);
        async Task Scan()
        {
            view.BeginScan();
            var started = DateTime.UtcNow;
            var result = await ControllerVerify.Run(model.Id);
            var wait = TimeSpan.FromMilliseconds(1900) - (DateTime.UtcNow - started); if (wait > TimeSpan.Zero) await Task.Delay(wait); // long enough to read what it is doing
            if (token != verifyToken || verifyLayer != layer) return;
            void Enter()
            {
                Close();
                Children.Remove(layer);
                Zone.UpdateLayout();
                if (result.Transport is { } t)
                    currentTransport = t == "Receiver" ? "2.4 GHz receiver" : t;
                EnterStudio(player);
            }
            view.ShowResult(result, model.Name, result.DetectedName, Enter,
                id => { var other = Catalog.Find(id); Close(); if (other != null) { AssignSelection(other); EnterStudio(player); } },
                () => _ = Scan());
            if (result.Outcome == VerifyOutcome.Match) { await Task.Delay(1000); if (token == verifyToken && verifyLayer == layer) Enter(); }
        }
        await Scan();
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
        // with PC mode on, the Buttons, Curves and Macros tabs edit PC mode's settings, for any model
        bool pc=PcMode is {Enabled:true}&&pcModeStore!=null;
        if(draft.PcMode&&!pc&&model.Id!="x20")PcModeSync.Clear(draft,curveDraft,macroDraft); // while still unlocked
        draft.PcMode=curveDraft.PcMode=macroDraft.PcMode=pc;
        if(pcModeStore!=null)PcModePage.UsePlayer(player,pcModeStore); // each controller has its own PC mode settings
        if(pc)PcModeSync.Prepare(pcModeStore!,draft,curveDraft,macroDraft);
        Studio = new(player, model, draft, Zone.ReducedMotion, curveDraft,macroDraft,vibrationDraft,setupStore,legacySources,activeSetup); if(PcMode!=null&&pcModeStore!=null)Studio.PcMode=new(pcModeStore,PcMode,preferencesHealthy?preferences:null);Studio.MouseService=MouseMode;Studio.PcModeSwitched+=()=>Dispatcher.BeginInvoke(()=>{EnterStudio(player);Studio?.ShowManagement("PcMode");});Studio.BackRequested += ReturnToZone;Studio.SupportRequested+=OpenSupport;Studio.ActiveSetupChanged+=id=>activeSetups[key]=id;Studio.TelemetryUpdated+=data=>RecordTelemetry(data,player);
        Studio.SetBatteryLevel(batteryLevels[player]);
        Studio.AgentScanRequested += page => { if (page == "ReadScan") OpenCheck(); else OpenWriteScan(); };
        Studio.AgentFeedbackRequested += draft =>
        {
            ShowSettings();
            var form = Settings!.FeedbackForm;
            string previous = form.Message.Text;
            // the new draft is kept whole; it is the earlier text that gives way when the message is full
            string head = string.IsNullOrWhiteSpace(previous) ? "" : previous[..Math.Min(previous.Length, Math.Max(0, 4000 - draft.Length - 2))] + "\n\n";
            string combined = head + draft;
            form.Message.Text = combined[..Math.Min(4000, combined.Length)];
            Settings.ShowFeedback(true);
        };

        if (currentTransport != null) Studio.SetTransport(currentTransport);
        if (currentFirmware != null) Studio.SetFirmware(currentFirmware);
        if (latestTelemetry != null) Studio.ApplyTelemetry(latestTelemetry);
        // BLE telemetry is strictly user-invoked and does not auto-query on launch.
        Studio.NativeEngineStatus=NativeEngineStatus;
        Studio.InputContextChanged+=()=>_=UpdateNativeContext();
        if(Engine!=null)_=RefreshNativeCapabilities(model.Id);
        // Carry the selected controller into its Dashboard rather than replacing one screen with a zoom.
        Rect source=Rect.Empty;
        if (Zone.Visibility==Visibility.Visible && player<Zone.Docks.Count)
        {
            var art=Zone.Docks[player].ControllerArtwork;
            if(art.ActualWidth>0 && art.ActualHeight>0) source=art.TransformToAncestor(this).TransformBounds(new Rect(art.RenderSize));
        }
        Children.Add(Studio);
        Studio.Opacity=1;Studio.IsHitTestVisible=true;Zone.Visibility=Visibility.Collapsed;
        Studio.Dashboard?.CompleteEntryTransition();
    }

    public void ReturnToZone()
    {
        bool back = Studio != null;
        if (Studio != null) { var leaving = Studio; Studio = null; leaving.Diagnostics?.Stop(); Fx.Leave(leaving, () => Children.Remove(leaving), 1.06, 300); }
        Zone.BeginAnimation(OpacityProperty, null); Zone.Opacity = 1; Zone.IsHitTestVisible = true; Zone.IsEnabled = Selector == null && Settings == null && Intro == null;
        Zone.Visibility = Visibility.Visible;
        if (back) Fx.Appear(Zone, .94, 380);
        _=UpdateNativeContext();
    }
    // every launch opens with the intro, like Steam Big Picture (owner direction 10 Oct 2026), unless the player turned it
    // off in Tools (owner request 12 Oct 2026); Replay Intro still plays it
    public void Startup(){if(preferencesHealthy&&!preferences.IntroOnLaunch){IntroView.DiscardPreload();return;}StartIntro();}
    private static string PreferencesPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"x20ctl","native","preferences.v1.json");
    /// <summary>Whether this launch opens with the intro film, read before the window exists so the film can start opening early.
    /// The same rule as <see cref="Startup"/>: on unless the player turned it off, and never with reduced motion.</summary>
    public static bool IntroAtLaunch(bool reducedMotion)
    {
        if(reducedMotion||!SystemParameters.ClientAreaAnimation)return false;
        try{var launch=new ProductPreferences(PreferencesPath);launch.Load();return launch.IntroOnLaunch;}
        catch(Exception error) when(error is IOException or System.Text.Json.JsonException or InvalidOperationException){return true;}
    }
    public async Task InitializeNativeEngineAsync()
    {
        if(Engine!=null)return;
        try{Engine=new();Engine.EventReceived+=QueueNativeEvent;Engine.Disconnected+=reason=>Dispatcher.BeginInvoke(new Action(()=>{nativeSubscription=null;nativeArmed=false;NativeEngineStatus="Native core disconnected · Configuration unavailable";if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);Studio.Tester?.ExitCapture();}}));var hello=await Engine.Request("handshake",new{});if(!hello.TryGetProperty("engineEpoch",out var epoch))throw new InvalidDataException("Native handshake omitted epoch.");nativeEpoch=epoch.GetString();NativeEngineStatus="Native C++ core running · Configuration locked";Zone.ProvenanceText.Text="LIVE INPUT";await RefreshNativeCapabilities(Studio?.Model.Id??"x20");await UpdateNativeContext();}
        catch(Exception error){Zone.ProvenanceText.Text="INPUT UNAVAILABLE";NativeEngineStatus="Native core unavailable · "+error.Message;if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);}Zone.FeedbackText.Text="Native connection unavailable; local previews remain available.";}
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
        try{if(Engine==null)return;nativeSubscription=null;nativeArmed=false;lastNativeButtons.Clear();lastNativeSequence=-1;string context=ExpectedNativeContext;
            var sources=await Engine.Request("listGameplaySources",new{});
            var slots=sources.GetProperty("sources").EnumerateArray().Select(s=>s.GetProperty("slot").GetInt32()).Where(s=>!PcModeService.IsVirtual(s)).ToArray();
            int preferred=Studio?.Player??Zone.State.FocusedPlayer??0;
            int slot=slots.Contains(preferred)?preferred:slots.FirstOrDefault(0);
            var response=await Engine.Request("subscribe",new{stream="input",slot,context});if(context==ExpectedNativeContext)nativeSubscription=response.GetProperty("subscriptionId").GetString();}
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
            var payload=value.GetProperty("payload");bool connected=payload.GetProperty("connected").GetBoolean();Zone.ProvenanceText.Text=connected?"LIVE INPUT":"NO CONTROLLER";Zone.ScenarioHint.Text=connected?"XInput navigation · Model identity unverified · Configuration locked":"No gameplay source connected · Configuration locked";
            Studio?.SetGameplayConnected(connected);
            if(connected)Studio?.ObserveLiveInput(payload.GetProperty("leftX").GetDouble(),payload.GetProperty("leftY").GetDouble(),payload.GetProperty("rightX").GetDouble(),payload.GetProperty("rightY").GetDouble(),payload.GetProperty("lt").GetDouble(),payload.GetProperty("rt").GetDouble());
            // virtual buttons: triggers and left stick become digital presses with a threshold
            var now=payload.GetProperty("buttons").EnumerateArray().Select(v=>v.GetString()!).ToHashSet();
            if(connected){if(payload.GetProperty("lt").GetDouble()>.55)now.Add("LT");if(payload.GetProperty("rt").GetDouble()>.55)now.Add("RT");
                if(Studio?.IsCurves != true)
                {
                    double lx=payload.GetProperty("leftX").GetDouble(),ly=payload.GetProperty("leftY").GetDouble();if(Math.Abs(lx)>.6&&Math.Abs(lx)>=Math.Abs(ly))now.Add(lx>0?"LS_RIGHT":"LS_LEFT");else if(Math.Abs(ly)>.6)now.Add(ly>0?"LS_UP":"LS_DOWN");
                }}
            var pressed=now.Except(lastNativeButtons).ToArray();lastNativeButtons=now;
            long time=value.GetProperty("timestampUs").GetInt64();
            if(Studio?.CapturingButtonEditor==true){if(connected){foreach(var button in pressed.Where(b=>!b.StartsWith("LS_")))Studio.SelectPhysicalButton(button);}nativeArmed=false;return;}
            if(connected&&Studio?.IsCurves==true){if(pressed.Contains("L3"))Studio.Curves.SelectChannel(X20Ctl.Zone.CurveChannel.LeftStick);else if(pressed.Contains("R3"))Studio.Curves.SelectChannel(X20Ctl.Zone.CurveChannel.RightStick);}
            // Home / Guide: bring X20CTL forward from anywhere (tray, behind a game); in front it goes home
            if(connected&&pressed.Contains("HOME")&&Studio?.ManagementPage is not ("Diagnostics" or "Mouse")){HomeRequested?.Invoke();return;}
            // the controller is the mouse: its buttons click and type, so they must not also move around X20CTL (Home still comes home)
            if(MouseMode?.Active==true){nativeArmed=false;return;}
            if(Studio?.ManagementPage is "Tester" or "Diagnostics" or "Mouse")
            {
                // the Tester owns every input; only a deliberate one-second hold of B leaves it
                if(Studio.ManagementPage=="Tester" && now.Contains("B")){if(bHeldSinceUs==0)bHeldSinceUs=time;else if(time-bHeldSinceUs>=1_000_000){bHeldSinceUs=0;Studio.ShowManagement("Device");nativeArmed=false;return;}}else bHeldSinceUs=0;
                if(Studio.ManagementPage=="Tester")Studio.Tester?.ReceiveNative(value);nativeArmed=false;return;
            }
            if(!connected||Window.GetWindow(this)?.IsActive!=true){nativeArmed=false;return;}if(!nativeArmed){if(now.Count==0)nativeArmed=true;return;}
            if(Intro!=null){if(pressed.Contains("B"))Intro.Finish();else if(pressed.Contains("Y"))Intro.ToggleMute();return;}
            if(Settings!=null)
            {
                if(pressed.Contains("B"))CloseSettings();
                else if(pressed.Contains("A"))
                {
                    var target = Keyboard.FocusedElement as Button;
                    if(target == null && !Settings.IsKeyboardFocusWithin) { Settings.FocusFirst(); target = Keyboard.FocusedElement as Button; }
                    if(target!=null && target.IsEnabled) target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else foreach(var d in new[]{("DPAD_UP",FocusNavigationDirection.Up),("DPAD_DOWN",FocusNavigationDirection.Down),("DPAD_LEFT",FocusNavigationDirection.Left),("DPAD_RIGHT",FocusNavigationDirection.Right)})
                {
                    if(pressed.Contains(d.Item1))
                    {
                        if(!Settings.IsKeyboardFocusWithin) Settings.FocusFirst();
                        else if(Keyboard.FocusedElement is UIElement el) el.MoveFocus(new(d.Item2));
                    }
                }
                return;
            }
            var activeNav = new List<(string button, Key key)>();
            foreach(var (button,key) in InputMap.Controller)
            {
                bool repeat=(button.StartsWith("DPAD")||button.StartsWith("LS_"))&&now.Contains(button)&&time-lastNavigationUs>200000;
                if(pressed.Contains(button)||repeat) activeNav.Add((button,key));
            }
            if(activeNav.Count > 0)
            {
                lastNavigationUs=time;
                if(Studio!=null)Studio.EnableNativeNavigation();else Zone.EnableNativeNavigation();
                var hostWindow=Window.GetWindow(this);
                if(hostWindow!=null)
                {
                    foreach(var (button, key) in activeNav)
                    {
                        var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(hostWindow)!,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};
                        OnKeyDown(this,e);
                        OnKeyUp(this,new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(hostWindow)!,0,key){RoutedEvent=Keyboard.PreviewKeyUpEvent});
                    }
                }
            }
        }catch(Exception error) when(error is InvalidOperationException or ArgumentException or JsonException){nativeArmed=false;ResetTransientInput();NativeEngineStatus="Native input unavailable · "+error.Message;}
    }
    public void StartIntro()
    {
        CloseSettings();ReturnToZone();if(Intro!=null)return;IntroWasPlayed=true;Zone.IsEnabled=false;Zone.Opacity=0;
        try{Intro=new(Zone.ReducedMotion,muted:preferencesHealthy&&!preferences.IntroSound,handoff:Zone);Intro.MutedChanged+=SaveIntroSound;Intro.ZoneReveal+=amount=>Zone.Opacity=amount;Intro.Completed+=CompleteIntro;Children.Add(Intro);Panel.SetZIndex(Intro,30);Intro.Start();}
        catch(Exception error){CompleteIntro();Zone.FeedbackText.Text="Startup animation unavailable. Controller Zone is ready. "+error.Message;}
    }
    private void SaveIntroSound(bool muted)
    {
        if(!preferencesHealthy)return;
        try{preferences.SetIntroSound(!muted);}catch(Exception error) when(error is IOException or InvalidOperationException){Zone.FeedbackText.Text="The intro's sound setting could not be saved; original file preserved. "+error.Message;}
    }
    private void CompleteIntro()
    {
        if(Intro!=null){Children.Remove(Intro);Intro=null;}Zone.Opacity=1;Zone.IsEnabled=true;if(updateText!=null)ShowUpdateNotice();
        if(preferencesHealthy)try{preferences.MarkIntroSeen();}catch(Exception error) when(error is IOException or InvalidOperationException){Zone.FeedbackText.Text="Startup preference could not be saved; original file preserved. "+error.Message;}
    }
    private void OpenSupport(){try{support(ProductLinks.Support);}catch(Exception error){Zone.FeedbackText.Text="Could not open your browser. Support X20CTL: "+ProductLinks.Support+" · "+error.Message;}}
    public void ShowSettings(){ReturnToZone();CloseSettings();Zone.IsEnabled=false;Settings=new(OpenSupport,StartIntro,CloseSettings,OpenCheck,preferencesHealthy?preferences:null,PcMode,pcModeStore,OpenWriteScan);Children.Add(Settings);Panel.SetZIndex(Settings,20);Fx.Appear(Settings,1.04,320);Settings.FocusFirst();}
    /// <summary>"Controller not working?": the Controller Check (Scanner 2.0.0 engine) as its own full-window app over Tools.</summary>
    public Scanner.ControllerCheckView? Check {get;private set;}
    public void OpenCheck(){if(Check!=null)return;if(Studio?.ManagementPage=="Diagnostics")Studio.ShowManagement("Device"); // both use Raw Input, one registration per process
        if(PcMode!=null)PcMode.Paused=true;Check=new(CloseCheck){ContinueToWrite=read=>{CloseCheck();if(Check==null)OpenWriteScan(read);}};Children.Add(Check);Panel.SetZIndex(Check,40);Fx.Appear(Check,1.06,360);}
    /// <summary>A scan screen that lost its place (closed, but still drawn) is removed outright, so Back can never be dead.</summary>
    private bool RemoveOrphans(){var stale=Children.OfType<FrameworkElement>().Where(c=>c is Scanner.ControllerCheckView v&&v!=Check||c is Scanner.WriteScanView w&&w!=WriteScan).ToList();foreach(var c in stale)Children.Remove(c);return stale.Count>0;}
    public void CloseCheck(){if(Check==null){RemoveOrphans();return;}if(!Check.TryClose())return;var closing=Check;Check=null;closing.Shutdown();if(PcMode!=null)PcMode.Paused=false;Fx.Leave(closing,()=>Children.Remove(closing),1.05,240);Settings?.Check.Focus();}
    /// <summary>The Write scan: reads a phone's Bluetooth log of the official app changing one setting. It never talks to the controller.</summary>
    public Scanner.WriteScanView? WriteScan {get;private set;}
    public void OpenWriteScan()=>OpenWriteScan(null);
    public void OpenWriteScan(Scanner.ReadReport? read){if(WriteScan!=null)return;WriteScan=new(CloseWriteScan,read);Children.Add(WriteScan);Panel.SetZIndex(WriteScan,40);Fx.Appear(WriteScan,1.06,360);}
    public void CloseWriteScan(){if(WriteScan==null){RemoveOrphans();return;}if(!WriteScan.TryClose())return;var closing=WriteScan;WriteScan=null;closing.Shutdown();Fx.Leave(closing,()=>Children.Remove(closing),1.05,240);Settings?.WriteScan.Focus();}
    private string? updateText;private bool updateShown;
    private async Task CheckUpdatesAtLaunch(bool install)
    {
        await Task.Delay(4000);
        try{if(install)await Updater.AutoRun();else Updater.Ready=await Updater.Latest();}catch(Exception){return;}
        if(Updater.Ready is not {} release)return;
        updateText=Updater.Installed is {} installed?$"{X20Ctl.Zone.ReleaseVersions.Display(installed)} is installed. It starts the next time you open X20CTL.":$"{release.Title} is out. You have {Updater.Shown}.";
        if(Intro==null)ShowUpdateNotice();
    }
    /// <summary>Non-modal header notice, after the intro and only for a newer release.</summary>
    private void ShowUpdateNotice()
    {
        if(updateShown||updateText==null||Updater.Ready is not {} release)return;updateShown=true;
        var label=Kit.Text(Updater.Installed!=null ? "Update installed · restart to finish" : "Update available · "+release.Title,14,"DS.Text",FontWeights.SemiBold);
        label.TextTrimming=TextTrimming.CharacterEllipsis;
        label.Foreground=DS.Brush("Zone.TextSecondary");
        var go=new Button { Style=DS.Style("Zone.Utility"),MinHeight=38,Padding=new(18,0,18,0),Content=label,ToolTip=updateText,MaxWidth=460 };
        Zone.UpdateNotice.Content=go;
        go.Click+=async(_,_)=>
        {
            if(Updater.Installed!=null)return;
            if(!Updater.CanInstall) { if(!ReviewSandbox.Active)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Updater.ReleasesPage){UseShellExecute=true});return; }
            go.IsEnabled=false;label.Text="Downloading update…";
            try { var(_,text)=await Updater.Install(release); label.Text=text; }
            catch(Exception error) when(error is System.Net.Http.HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException) { label.Text="Update failed · click to retry";go.ToolTip=error.Message; }
            finally { go.IsEnabled=true; }
        };
    }
    public void CloseSettings(){if(Settings!=null){var leaving=Settings;Settings=null;Fx.Leave(leaving,()=>Children.Remove(leaving),1.03,220);}Zone.IsEnabled=true;}
    public void ResetTransientInput(){nativeArmed=false;Zone.SuspendNavigation();Studio?.Tester?.ClearKeyboardKeys();Studio?.ReleaseTransientInput();}
    public event Action? HomeRequested;
    private long bHeldSinceUs;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key=e.Key==Key.System?e.SystemKey:e.Key;
        // the identity scan popup: Esc / B backs out, A / Enter presses the focused button, nothing behind it moves
        if(verifyLayer!=null)
        {
            var verifyView = verifyLayer.Children.OfType<ControllerVerifyView>().FirstOrDefault();
            if(e.Key is Key.Escape or Key.B) verifyView?.RaiseCancel();
            else if(e.Key is Key.A or Key.Enter)
            {
                var target = Keyboard.FocusedElement as Button ?? verifyView?.FirstActionButton;
                if(target!=null && !e.IsRepeat) target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            e.Handled=true;
            return;
        }
        // the Controller Check is testing the controller: its presses are never navigation; the keyboard's Esc leaves
        if(Check!=null){if(sender is ProductHost){e.Handled=true;return;}if(e.Key==Key.Escape){CloseCheck();e.Handled=true;}return;}
        // the Write scan is typed into and clicked; the keyboard's Esc leaves, nothing else navigates behind it
        if(WriteScan!=null){if(sender is not ProductHost && e.Key==Key.Escape){CloseWriteScan();e.Handled=true;}return;}
        // remapping on the Buttons page: Menu, View and B are buttons being assigned, not navigation
        if(Studio?.CapturingRemap==true && Intro==null && Settings==null){Studio.OnKeyDown(sender,e);return;}
        if(key==InputMap.Settings && Intro==null){if(Settings!=null)CloseSettings();else ShowSettings();e.Handled=true;return;}
        if(key==InputMap.ControllerZone && Intro==null && Settings==null && Studio!=null && Studio.ManagementPage is not ("Tester" or "Diagnostics")){ReturnToZone();e.Handled=true;return;}
        if(Intro!=null){if(e.Key is Key.Escape or Key.B)Intro.Finish();else if(e.Key==Key.M&&!e.IsRepeat)Intro.ToggleMute();e.Handled=true;return;}
        if(Settings!=null)
        {
            if(e.Key is Key.Escape or Key.B){if(!Settings.StepBack())CloseSettings();e.Handled=true;}
            else if(e.Key is Key.A or Key.Enter)
            {
                var target = Keyboard.FocusedElement as Button;
                if(target == null && !Settings.IsKeyboardFocusWithin) { Settings.FocusFirst(); target = Keyboard.FocusedElement as Button; }
                if(target!=null && !e.IsRepeat) { target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled=true; }
            }
            else if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                if(!Settings.IsKeyboardFocusWithin) Settings.FocusFirst();
                else if(Keyboard.FocusedElement is UIElement focused)
                {
                    focused.MoveFocus(new(e.Key==Key.Up?FocusNavigationDirection.Up:e.Key==Key.Down?FocusNavigationDirection.Down:e.Key==Key.Left?FocusNavigationDirection.Left:FocusNavigationDirection.Right));
                }
                e.Handled=true;
            }
            return;
        }
        // the controller picker: arrows / D-pad move between cards, A / Enter chooses, B / Esc goes back
        if (Selector != null)
        {
            if (e.Key is Key.Escape or Key.B) { CloseSelector(); e.Handled = true; }
            else if (e.Key is Key.A or Key.Enter)
            {
                var card = Keyboard.FocusedElement as Button;
                if (card == null && !Selector.IsKeyboardFocusWithin) { Selector.FocusFirst(); card = Keyboard.FocusedElement as Button; }
                if (card != null && !e.IsRepeat) { card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
            }
            else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                if (!Selector.IsKeyboardFocusWithin) Selector.FocusFirst();
                else if (Keyboard.FocusedElement is UIElement focused)
                {
                    focused.MoveFocus(new(e.Key == Key.Up ? FocusNavigationDirection.Up : e.Key == Key.Down ? FocusNavigationDirection.Down : e.Key == Key.Left ? FocusNavigationDirection.Left : FocusNavigationDirection.Right));
                }
                e.Handled = true;
            }
            return;
        }
        if (Studio != null) Studio.OnKeyDown(sender, e); else Zone.OnKeyDown(sender, e);
    }
    public void OnKeyUp(object sender, KeyEventArgs e) { if(Intro!=null||Settings!=null||Check!=null||WriteScan!=null)return;if(Studio!=null)Studio.OnKeyUp(sender,e);else if (Selector == null) Zone.OnKeyUp(sender, e); }
}
