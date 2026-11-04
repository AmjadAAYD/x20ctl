using System.Windows.Media.Animation;
﻿using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.Json;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed partial class ProductHost : Grid
{
    private string?[] batteryLevels=new string?[4];
    private readonly string?[] measuredBatteryLevels=new string?[4];
    private readonly X20Ctl.Zone.Telemetry.TelemetryRefreshPolicy telemetryRefresh=new();
    private bool telemetryReading;
    internal event Action? TelemetryRefreshed;
    internal bool AutomaticTelemetryInReview {get;set;}
    internal void RequestAutomaticTelemetryRefresh(bool? connected=null)
    {
        if(ReviewSandbox.Active&&!AutomaticTelemetryInReview)return;
        int player=Studio?.Player??0;
        string? model=Studio?.Model.Id??Zone.State.Players[player].ModelId;
        if(model==null)return;
        string target=$"{player}:{model}";
        bool refresh=connected.HasValue?telemetryRefresh.OnConnection(target,connected.Value):telemetryRefresh.OnOpen(target);
        if(refresh)_=RefreshBleTelemetryAsync();
    }
    private string? currentTransport;
    public string?[] BatteryLevels => batteryLevels;
    private readonly Dictionary<(int Player,string Model),TelemetryData> latestTelemetry=[];
    private readonly string?[] measuredBatteryModels=new string?[4];

    public void RecordTelemetry(TelemetryData telemetry, int player = 0)
    {
        string? model=telemetry.Controller switch {"EasySMX X20 Pro"=>"x20_pro","EasySMX X20"=>"x20",_=>null};
        if(model!=null){latestTelemetry[(player,model)]=telemetry;if(player>=0&&player<4)measuredBatteryModels[player]=model;}
        if (telemetry.Battery != null && player >= 0 && player < batteryLevels.Length) measuredBatteryLevels[player]=batteryLevels[player] = telemetry.Battery;
        TelemetryRefreshed?.Invoke();
    }

    public async Task RefreshBleTelemetryAsync()
    {
        string? modelId = Studio?.Model.Id ?? (Zone.State.Players.Count > 0 ? Zone.State.Players[0].ModelId ?? Catalog.Models.FirstOrDefault(m => m.Name == Zone.State.Players[0].Model)?.Id : null);
        if (modelId is not ("x20" or "x20_pro")) return;

        if(telemetryReading)return;
        int targetPlayer=Studio?.Player??0;
        telemetryReading=true;
        try
        {
            var res = await PadBridge.Status(modelId);
            if (res.Ok && res.Telemetry != null)
            {
                if(Studio is {} current && current.Player==targetPlayer && current.Model.Id==modelId)
                    current.ApplyTelemetry(res.Telemetry);
                else RecordTelemetry(res.Telemetry,targetPlayer);
            }
        }
        catch { }
        finally{telemetryReading=false;}
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
            batteryLevels[i] = X20Ctl.Zone.Telemetry.NativeTelemetry.PreferMeasuredBattery(measuredBatteryLevels[i],levels[i]);
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
    private readonly object nativeEventLock=new();private readonly EdgeQueue<JsonElement> nativeEvents=new();private int nativeEventScheduled;
    private HashSet<string> lastNativeButtons=new();private bool nativeArmed;private long lastNativeSequence=-1,lastNavigationUs;
    internal string ExpectedNativeContext=>NativeInputStudio?.InputContext is InputOwner.TesterCapture or InputOwner.RemapCapture or InputOwner.MacroRecording?"TESTER_CAPTURE":"UI_NAVIGATION";
    internal StudioScene? NativeInputStudio => Settings != null || Check != null || WriteScan != null || ActivePulseNotice != null || ActiveRebelNotice != null || ActiveThemeNotice != null ? null : Studio;
    private int? studioPlayer;
    public bool IntroWasPlayed {get;private set;}
    private readonly ProductPreferences preferences;
    private readonly bool preferencesHealthy;
    /// <summary>PC mode (Tools): on by default, but it only does anything once a setting has been changed.</summary>
    public PcModeService? PcMode { get; }
    /// <summary>Controller as mouse. A fixture window gets one that never listens to the controller.</summary>
    public MouseModeService? MouseMode { get; }
    private readonly X20Ctl.Zone.PcMode.PcModeStore? pcModeStore;    private readonly Action<string> support;
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
        Zone.SupportRequested+=OpenSupport;Zone.SettingsRequested+=ShowSettings;var languageMenu=new LanguageMenu(preferencesHealthy?preferences:null,zone);Children.Add(languageMenu);Panel.SetZIndex(languageMenu,9);
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
        Zone.StudioPageRequested += (index, page) => { if (Zone.State.Players[index].Model == null) return; pendingPage = page; VerifyThenEnter(index); };
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
        studioPlayer = player;
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
    private string? pendingPage;
    public void EnterStudio(int player)
    {
        var assigned = Zone.State.Players[player];
        studioPlayer = player;
        var model = assigned.ModelId != null ? Catalog.Find(assigned.ModelId) : Catalog.Models.FirstOrDefault(m => m.Name == assigned.Model);
        if (model == null) { Zone.FeedbackText.Text = "Choose a known controller model before entering Studio."; return; }
        if (Studio != null) { var old = Studio; old.SuspendMacroActivity(); Studio = null; Fx.Leave(old, () => Children.Remove(old), 1.02, 160); }
        if (!drafts.TryGetValue((player, model.Id), out var draft)) drafts[(player, model.Id)] = draft = new(model.Id);
        if (!curves.TryGetValue((player, model.Id), out var curveDraft)) curves[(player, model.Id)] = curveDraft = new(model.Id);
        if (!macros.TryGetValue((player, model.Id), out var macroDraft)) macros[(player, model.Id)] = macroDraft = new(model.Id);
        if (!vibrations.TryGetValue((player, model.Id), out var vibrationDraft)) vibrations[(player, model.Id)] = vibrationDraft = new(model.Id);
        var key=(player,model.Id);activeSetups.TryGetValue(key,out var activeSetup);
        // with PC mode on, the Buttons, Curves and Macros tabs edit PC mode's settings, for any model
        // an X20 Pro keeps these settings on the pad itself (Apply to controller), so its tabs never edit a virtual pad, whatever the PC mode switch says
        bool pc=PcMode is {Enabled:true}&&pcModeStore!=null&&model.Id!="x20_pro";
        if(draft.PcMode&&!pc&&model.Id!="x20")PcModeSync.Clear(draft,curveDraft,macroDraft); // while still unlocked
        draft.PcMode=curveDraft.PcMode=macroDraft.PcMode=pc;
        if(pcModeStore!=null)PcModePage.UsePlayer(player,pcModeStore); // each controller has its own PC mode settings
        if(pc)PcModeSync.Prepare(pcModeStore!,draft,curveDraft,macroDraft);
        Studio = new(player, model, draft, Zone.ReducedMotion, curveDraft,macroDraft,vibrationDraft,setupStore,legacySources,activeSetup); if(PcMode!=null&&pcModeStore!=null)Studio.PcMode=new(pcModeStore,PcMode,preferencesHealthy?preferences:null);Studio.MouseService=MouseMode;Studio.PcModeSwitched+=()=>Dispatcher.BeginInvoke(()=>{EnterStudio(player);Studio?.ShowManagement("PcMode");});Studio.BackRequested += ReturnToZone;Studio.SupportRequested+=OpenSupport;Studio.ActiveSetupChanged+=id=>activeSetups[key]=id;Studio.TelemetryUpdated+=data=>RecordTelemetry(data,player);
        Studio.SetBatteryLevel(batteryLevels[player]);
        Studio.ToolsRequested += ShowSettings;
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
        if(latestTelemetry.TryGetValue((player,model.Id),out var rememberedTelemetry))Studio.ApplyTelemetry(rememberedTelemetry);
        else if(measuredBatteryModels[player]!=model.Id){measuredBatteryLevels[player]=null;batteryLevels[player]=null;Studio.SetBatteryLevel(null);}
        RequestAutomaticTelemetryRefresh();
        // One session-opening read; subsequent reads follow reconnection or an explicit refresh.
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
        if (pendingPage != null) { var page = pendingPage; pendingPage = null; Studio?.ShowManagement(page); }
    }

    public void ReturnToZone()
    {
        bool back = Studio != null;
        if (Studio != null) { var leaving = Studio; leaving.SuspendMacroActivity(); Studio = null; leaving.Diagnostics?.Stop(); Fx.Leave(leaving, () => Children.Remove(leaving), 1.06, 300); }
        studioPlayer = null;
        Zone.BeginAnimation(OpacityProperty, null); Zone.Opacity = 1; Zone.IsHitTestVisible = true; Zone.IsEnabled = Selector == null && Settings == null && Intro == null;
        Zone.Visibility = Visibility.Visible;
        if (back) Fx.Appear(Zone, .94, 380);
        _=UpdateNativeContext();
    }
    // every launch opens with the intro, like Steam Big Picture (owner direction 10 Oct 2026), unless the player turned it
    // off in Tools (owner request 12 Oct 2026); Replay Intro still plays it
    public void Startup(){if(preferencesHealthy&&!preferences.IntroOnLaunch){IntroView.DiscardPreload();ShowWhatsNewIfDue();return;}StartIntro();}
    internal static string PreferencesPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"x20ctl","native","preferences.v1.json");
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
        try{Engine=new();Engine.EventReceived+=QueueNativeEvent;Engine.Disconnected+=reason=>Dispatcher.BeginInvoke(new Action(()=>{nativeSubscription=null;nativeArmed=false;NativeEngineStatus="Native core disconnected · Configuration unavailable";if(Studio!=null){Studio.NativeEngineStatus=NativeEngineStatus;Studio.Device?.UpdateEngineStatus(NativeEngineStatus);Studio.Tester?.ExitCapture();Studio.CancelRemapCapture();}}));var hello=await Engine.Request("handshake",new{});if(!hello.TryGetProperty("engineEpoch",out var epoch))throw new InvalidDataException("Native handshake omitted epoch.");nativeEpoch=epoch.GetString();NativeEngineStatus="Native C++ core running · Configuration locked";Zone.ProvenanceText.Text="LIVE INPUT";await RefreshNativeCapabilities(Studio?.Model.Id??"x20");await UpdateNativeContext();}
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
        // only analog-only samples merge: a press and its release between two renders both reach the UI, in order
        lock(nativeEventLock){nativeEvents.Offer(message,NativeEdgeKey(message));InputPresentationCoalesced=nativeEvents.Coalesced+nativeEvents.Dropped;}
        if(Interlocked.Exchange(ref nativeEventScheduled,1)==0)Dispatcher.BeginInvoke(new Action(ConsumeNativeEvents));
    }
    /// <summary>The digital state a sample carries (buttons, triggers and stick past their thresholds); samples that share it may merge.</summary>
    internal static string NativeEdgeKey(JsonElement message)
    {
        try
        {
            var p=message.GetProperty("payload");double lx=p.GetProperty("leftX").GetDouble(),ly=p.GetProperty("leftY").GetDouble();
            string stick=Math.Abs(lx)>.6&&Math.Abs(lx)>=Math.Abs(ly)?(lx>0?"R":"L"):Math.Abs(ly)>.6?(ly>0?"U":"D"):"";
            return string.Join(",",p.GetProperty("buttons").EnumerateArray().Select(v=>v.GetString()))+"|"+p.GetProperty("connected").GetBoolean()+(p.GetProperty("lt").GetDouble()>.55?"|LT":"")+(p.GetProperty("rt").GetDouble()>.55?"|RT":"")+"|"+stick+"|"+(message.TryGetProperty("subscriptionId",out var id)?id.GetString():"");
        }
        catch(Exception error) when(error is InvalidOperationException or KeyNotFoundException or FormatException){return Guid.NewGuid().ToString("N");}
    }
    private void ConsumeNativeEvents()
    {
        List<JsonElement> messages;lock(nativeEventLock){messages=nativeEvents.Drain();Interlocked.Exchange(ref nativeEventScheduled,0);}
        foreach(var message in messages)ConsumeNativeEvent(message);
    }
    private void ConsumeNativeEvent(JsonElement value)
    {
        try{
            if(value.GetProperty("engineEpoch").GetString()!=nativeEpoch||value.GetProperty("subscriptionId").GetString()!=nativeSubscription||value.GetProperty("inputContext").GetString()!=ExpectedNativeContext)return;
            long sequence=value.GetProperty("sequence").GetInt64();if(sequence<=lastNativeSequence)return;lastNativeSequence=sequence;
            var payload=value.GetProperty("payload");bool connected=payload.GetProperty("connected").GetBoolean();Zone.ProvenanceText.Text=connected?"LIVE INPUT":"NO CONTROLLER";Zone.ScenarioHint.Text=connected?"XInput navigation · Model identity unverified · Configuration locked":"No gameplay source connected · Configuration locked";
            NativeInputStudio?.SetGameplayConnected(connected);
            RequestAutomaticTelemetryRefresh(connected);
            if(connected)NativeInputStudio?.ObserveLiveInput(payload.GetProperty("leftX").GetDouble(),payload.GetProperty("leftY").GetDouble(),payload.GetProperty("rightX").GetDouble(),payload.GetProperty("rightY").GetDouble(),payload.GetProperty("lt").GetDouble(),payload.GetProperty("rt").GetDouble());
            // virtual buttons: triggers and left stick become digital presses with a threshold
            var now=payload.GetProperty("buttons").EnumerateArray().Select(v=>v.GetString()!).ToHashSet();
            if(connected){if(payload.GetProperty("lt").GetDouble()>.55)now.Add("LT");if(payload.GetProperty("rt").GetDouble()>.55)now.Add("RT");
                if(NativeInputStudio?.IsCurves != true)
                {
                    double lx=payload.GetProperty("leftX").GetDouble(),ly=payload.GetProperty("leftY").GetDouble();if(Math.Abs(lx)>.6&&Math.Abs(lx)>=Math.Abs(ly))now.Add(lx>0?"LS_RIGHT":"LS_LEFT");else if(Math.Abs(ly)>.6)now.Add(ly>0?"LS_UP":"LS_DOWN");
                }}
            var pressed=now.Except(lastNativeButtons).ToArray();lastNativeButtons=now;
            if (ActiveThemeNotice is {} themedNotice)
            {
                if (!connected || Window.GetWindow(this)?.IsActive != true) { nativeArmed = false; return; }
                if (!nativeArmed) { if (now.Count == 0) nativeArmed = true; return; }
                if (pressed.Contains("B")) ThemeNoticeButton(themedNotice, "no")?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (pressed.Contains("A") && Keyboard.FocusedElement is Button action && themedNotice.IsKeyboardFocusWithin) action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (pressed.Contains("DPAD_LEFT") || pressed.Contains("DPAD_UP")) ThemeNoticeButton(themedNotice, "no")?.Focus();
                else if (pressed.Contains("DPAD_RIGHT") || pressed.Contains("DPAD_DOWN")) ThemeNoticeButton(themedNotice, "yes")?.Focus();
                return;
            }
            if(ActiveRebelNotice is {} notice){if(!connected||Window.GetWindow(this)?.IsActive!=true){nativeArmed=false;return;}if(!nativeArmed){if(now.Count==0)nativeArmed=true;return;}if(pressed.Contains("B"))notice.Resolve(false);else if(pressed.Contains("A") && Keyboard.FocusedElement is Button action && notice.IsKeyboardFocusWithin)action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else foreach(var direction in new[]{("DPAD_LEFT",FocusNavigationDirection.Left),("DPAD_RIGHT",FocusNavigationDirection.Right),("DPAD_UP",FocusNavigationDirection.Up),("DPAD_DOWN",FocusNavigationDirection.Down)})if(pressed.Contains(direction.Item1) && Keyboard.FocusedElement is UIElement focused)focused.MoveFocus(new(direction.Item2));return;}
            long time=value.GetProperty("timestampUs").GetInt64();
            NativeInputStudio?.NoteControllerHeld(now);
            // remap capture and macro recording own every press of the pad, but only while X20CTL is in front
            if(NativeInputStudio is {} owner && (owner.CapturingRemap || owner.InputContext==InputOwner.MacroRecording))
            {
                nativeArmed=false;
                var rule=InputOwnerRules.Resolve(new(Window.GetWindow(this)?.IsActive==true,connected,false,owner.InputContext==InputOwner.MacroRecording,owner.CapturingRemap,false,false));
                if(rule==InputOwnerRules.Owner.Nobody)owner.CancelRemapCapture();
                else if(rule==InputOwnerRules.Owner.RemapCapture)owner.ObserveCapture(now);
                return;
            }
            if(connected&&NativeInputStudio?.IsCurves==true){if(pressed.Contains("L3"))NativeInputStudio!.Curves.SelectChannel(X20Ctl.Zone.CurveChannel.LeftStick);else if(pressed.Contains("R3"))NativeInputStudio!.Curves.SelectChannel(X20Ctl.Zone.CurveChannel.RightStick);}
            // Home / Guide: bring X20CTL forward from anywhere (tray, behind a game); in front it goes home
            if(connected&&pressed.Contains("HOME")&&NativeInputStudio?.ManagementPage is not ("Diagnostics" or "Mouse")){HomeRequested?.Invoke();return;}
            // the controller is the mouse: its buttons click and type, so they must not also move around X20CTL (Home still comes home)
            if(MouseMode?.Active==true){nativeArmed=false;return;}
            if(NativeInputStudio?.ManagementPage is "Tester" or "Diagnostics" or "Mouse")
            {
                // the Tester owns every input; only a deliberate one-second hold of B leaves it
                if(NativeInputStudio!.ManagementPage=="Tester" && now.Contains("B")){if(bHeldSinceUs==0)bHeldSinceUs=time;else if(time-bHeldSinceUs>=1_000_000){bHeldSinceUs=0;NativeInputStudio!.ShowManagement("Device");nativeArmed=false;return;}}else bHeldSinceUs=0;
                if(NativeInputStudio!.ManagementPage=="Tester")NativeInputStudio!.Tester?.ReceiveNative(value);nativeArmed=false;return;
            }
            if(!connected||Window.GetWindow(this)?.IsActive!=true){nativeArmed=false;return;}if(!nativeArmed){if(now.Count==0)nativeArmed=true;return;}
            if(NativeInputStudio?.CanEngageRemap==true && Settings==null && pressed.Contains("R3")){NativeInputStudio.BeginRemapCapture();return;}
            if(Intro!=null){if(pressed.Contains("B"))Intro.Finish();else if(pressed.Contains("Y"))Intro.ToggleMute();return;}
            if(ActivePulseNotice!=null)
            {
                if(pressed.Contains("B"))DismissPulseNotice(false);
                else if(pressed.Contains("A"))
                {
                    var target = Keyboard.FocusedElement as Button ?? ActivePulseNotice.Primary;
                    if(target.IsEnabled) target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else if(pressed.Contains("DPAD_LEFT")||pressed.Contains("DPAD_UP"))ActivePulseNotice.Secondary.Focus();
                else if(pressed.Contains("DPAD_RIGHT")||pressed.Contains("DPAD_DOWN"))ActivePulseNotice.Primary.Focus();
                return;
            }
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
                if(NativeInputStudio!=null)NativeInputStudio.EnableNativeNavigation();else Zone.EnableNativeNavigation();
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
        if(Intro!=null){Children.Remove(Intro);Intro=null;}Zone.Opacity=1;Zone.IsEnabled=true;if(updateText!=null)ShowUpdateNotice();ShowWhatsNewIfDue();
        if(preferencesHealthy)try{preferences.MarkIntroSeen();}catch(Exception error) when(error is IOException or InvalidOperationException){Zone.FeedbackText.Text="Startup preference could not be saved; original file preserved. "+error.Message;}
    }
    private void OpenSupport(){try{support(ProductLinks.Support);}catch(Exception error){Zone.FeedbackText.Text="Could not open your browser. Support X20CTL: "+ProductLinks.Support+" · "+error.Message;}}
    public void ShowSettings()
    {
        if (ThemeCatalog.Current.Id == "rebel") { ShowRebelSettings(); return; }
        if (ThemeCatalog.Current.Id == "pulse") { ShowPulseSettings(); return; }
        if (ThemeCatalog.Current.Id == "default") ReturnToZone();
        CloseSelector(); CloseSettings(); Zone.IsEnabled = false;
        if (Studio != null) { Studio.IsEnabled = false; Studio.Visibility = Visibility.Collapsed; }
        Settings = new(OpenSupport, StartIntro, CloseSettings, OpenCheck, preferencesHealthy ? preferences : null, PcMode, pcModeStore, OpenWriteScan)
        {
            ThemeNavigate = NavigateThemePage, SpotlightNavigate = NavigateThemePage, NexusNavigate = NavigateThemePage,
            RestartRequested = RequestCurrentThemeRestart
        };
        Children.Add(Settings); Panel.SetZIndex(Settings, 20); Fx.Appear(Settings, 1.04, 320); Settings.FocusFirst();
        _ = UpdateNativeContext();
    }

    /// <summary>"Controller not working?": the Controller Check (Scanner 2.0.0 engine) as its own full-window app over Tools.</summary>
    public Scanner.ControllerCheckView? Check {get;private set;}
    public void OpenCheck(){if(Check!=null)return;Studio?.SuspendMacroActivity();if(Studio?.ManagementPage=="Diagnostics")Studio.ShowManagement("Device"); // both use Raw Input, one registration per process
        if(PcMode!=null)PcMode.Paused=true;Check=new(CloseCheck){ContinueToWrite=read=>{CloseCheck();if(Check==null)OpenWriteScan(read);},PulseNavigate=NavigatePulsePage};if(ThemeCatalog.Current.Id == "rebel")Check.RebelNavigate=key=>{CloseCheck();if(Check==null)NavigateRebelPage(key);};Children.Add(Check);Panel.SetZIndex(Check,40);Fx.Appear(Check,1.06,360);_=UpdateNativeContext();}
    /// <summary>A scan screen that lost its place (closed, but still drawn) is removed outright, so Back can never be dead.</summary>
    private bool RemoveOrphans(){var stale=Children.OfType<FrameworkElement>().Where(c=>c is Scanner.ControllerCheckView v&&v!=Check||c is Scanner.WriteScanView w&&w!=WriteScan).ToList();foreach(var c in stale)Children.Remove(c);return stale.Count>0;}
    public void CloseCheck(){if(Check==null){RemoveOrphans();return;}if(!Check.TryClose())return;var closing=Check;Check=null;closing.Shutdown();if(PcMode!=null)PcMode.Paused=false;Fx.Leave(closing,()=>Children.Remove(closing),1.05,240);Settings?.Check.Focus();}
    /// <summary>The Write scan: reads a phone's Bluetooth log of the official app changing one setting. It never talks to the controller.</summary>
    public Scanner.WriteScanView? WriteScan {get;private set;}
    public void OpenWriteScan()=>OpenWriteScan(null);
    public void OpenWriteScan(Scanner.ReadReport? read){if(WriteScan!=null)return;Studio?.SuspendMacroActivity();WriteScan=new(CloseWriteScan,read){PulseNavigate=NavigatePulsePage};if(ThemeCatalog.Current.Id == "rebel")WriteScan.RebelNavigate=key=>{CloseWriteScan();if(WriteScan==null)NavigateRebelPage(key);};Children.Add(WriteScan);Panel.SetZIndex(WriteScan,40);Fx.Appear(WriteScan,1.06,360);_=UpdateNativeContext();}
    public void CloseWriteScan(){if(WriteScan==null){RemoveOrphans();return;}if(!WriteScan.TryClose())return;var closing=WriteScan;WriteScan=null;closing.Shutdown();Fx.Leave(closing,()=>Children.Remove(closing),1.05,240);Settings?.WriteScan.Focus();}
    private string? updateText;private bool updateShown;
    private async Task CheckUpdatesAtLaunch(bool install)
    {
        await Task.Delay(4000);
        try{if(install)await Updater.AutoRun();else Updater.Ready=await Updater.Latest();}catch(Exception){return;}
        if(Updater.Installed is {} installedVersion)RememberWhatsNew(installedVersion);
        if(Updater.Ready is not {} release)return;
        updateText=Updater.Installed is {} installed?$"{X20Ctl.Zone.ReleaseVersions.Display(installed)} is installed. It starts the next time you open X20CTL.":$"{release.Title} is out. You have {Updater.Shown}.";
        if(Intro==null)ShowUpdateNotice();
    }
    /// <summary>Non-modal header notice, after the intro and only for a newer release: "Update to version V0.0xx". A click asks first (in the app), then
    /// downloads, checks, installs and restarts, all inside X20CTL. Nothing here opens a web page.</summary>
    private void ShowUpdateNotice()
    {
        if(updateShown||updateText==null||Updater.Ready is not {} release)return;updateShown=true;
        string name=X20Ctl.Zone.ReleaseVersions.Display(release.Version);
        var label=Kit.Text(Updater.Installed!=null ? "Update installed · restart to finish" : X20Ctl.Zone.UpdateKit.Label(name,false),14,"DS.Text",FontWeights.SemiBold);
        label.TextTrimming=TextTrimming.CharacterEllipsis;
        label.Foreground=DS.Brush("Zone.TextSecondary");
        var go=CreateUpdateNoticeButton(label,updateText);
        Zone.UpdateNotice.Content=go;
        go.Click+=(_,_)=>{if(Updater.Installed!=null){if(Updater.Restart())Application.Current.Shutdown();return;}OpenUpdateFlow(release);};
    }
    internal static Button CreateUpdateNoticeButton(TextBlock label, string? tooltip) => new() { Style=DS.Style("Zone.Utility"),MinHeight=38,Padding=new(18,0,18,0),Content=label,ToolTip=tooltip,MaxWidth=460 };
    private UpdateFlowView? updateLayer;
    /// <summary>Confirm, download with progress and Cancel, restart; or the plain reason it failed. The current install is untouched on any failure.</summary>
    private void OpenUpdateFlow(Updater.Release release)
    {
        if(updateLayer!=null)return;
        var flow=new X20Ctl.Zone.UpdateFlow(X20Ctl.Zone.ReleaseVersions.Display(release.Version),Updater.CanInstall);flow.Open();
        if(flow.Step==X20Ctl.Zone.UpdateStep.Hidden)return;
        var view=new UpdateFlowView(flow);updateLayer=view;
        System.Threading.CancellationTokenSource? run=null;
        void Show(){view.Render();view.FocusFirst();}
        void Dismiss(){if(updateLayer!=view)return;updateLayer=null;Zone.IsEnabled=Selector==null&&Settings==null&&Intro==null;Fx.Leave(view,()=>Children.Remove(view),1.03,180);}
        async Task Start()
        {
            run=new();var token=run.Token;Show();
            var progress=new Progress<double>(p=>{flow.Report(p);view.Render();});
            (bool Done,string Message) result;
            try{result=await Updater.Install(release,progress,token);}
            catch(Exception error) when(error is InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException){result=(false,error.Message);}
            run?.Dispose();run=null;
            if(token.IsCancellationRequested||flow.Step!=X20Ctl.Zone.UpdateStep.Downloading)return;
            if(!result.Done){flow.Fail(result.Message);Show();return;}
            flow.Installed();Show();RememberWhatsNew(release.Version);
            await Task.Delay(800);
            if(Updater.Restart())Application.Current.Shutdown();else{flow.RestartFailed();Show();}
        }
        view.Yes+=()=>{if(flow.Yes())_=Start();};
        view.No+=()=>{flow.No();Dismiss();};
        view.Cancel+=()=>{run?.Cancel();flow.Cancel();Dismiss();};
        view.Retry+=()=>{if(flow.Retry())_=Start();};
        view.Close+=()=>{flow.Close();Dismiss();};
        Zone.IsEnabled=false;Panel.SetZIndex(view,37);Children.Add(view);Fx.Appear(view,.96,220);Show();
    }
    /// <summary>The copy that installs an update leaves a note for the next start: show What's new for this version, once.</summary>
    private void RememberWhatsNew(string version)
    {
        if(!preferencesHealthy||ReviewSandbox.Active)return;
        try{preferences.SetWhatsNewPending(version);}catch(Exception error) when(error is IOException or InvalidOperationException){}
    }
    private bool whatsNewChecked;
    /// <summary>First start after a self-update only: the new version's changelog entry (from the changelog this exe carries, so it works offline), OK or Never remind me again.</summary>
    private void ShowWhatsNewIfDue()
    {
        if(whatsNewChecked||!preferencesHealthy||ReviewSandbox.Active||updateLayer!=null)return;whatsNewChecked=true;
        if(!preferences.ShouldShowWhatsNew(Updater.Current))return;
        string name=X20Ctl.Zone.ReleaseVersions.Display(Updater.Current);
        string? entry=null;
        try{using var stream=typeof(ProductHost).Assembly.GetManifestResourceStream("CHANGELOG.md");if(stream!=null){using var reader=new StreamReader(stream);entry=X20Ctl.Zone.Changelog.Entry(reader.ReadToEnd(),Updater.Current);}}
        catch(Exception error) when(error is IOException or InvalidOperationException){}
        try{preferences.SetWhatsNewPending(null);}catch(Exception error) when(error is IOException or InvalidOperationException){}
        var view=UpdateFlowView.WhatsNew(name,entry);updateLayer=view;
        void Dismiss(){if(updateLayer!=view)return;updateLayer=null;Zone.IsEnabled=Selector==null&&Settings==null&&Intro==null;Fx.Leave(view,()=>Children.Remove(view),1.03,180);}
        view.Ok+=Dismiss;
        view.NeverRemind+=()=>{try{preferences.SetWhatsNewOff();}catch(Exception error) when(error is IOException or InvalidOperationException){}Dismiss();};
        Zone.IsEnabled=false;Panel.SetZIndex(view,37);Children.Add(view);Fx.Appear(view,.96,220);view.FocusFirst();
    }
    public void CloseSettings(){if(Settings!=null){var leaving=Settings;Settings=null;Fx.Leave(leaving,()=>Children.Remove(leaving),1.03,220);}if(Studio!=null){Studio.IsEnabled=true;Studio.Visibility=Visibility.Visible;Zone.Visibility=Visibility.Collapsed;Zone.IsEnabled=false;}else{Zone.IsEnabled=true;Zone.Visibility=Visibility.Visible;}_=UpdateNativeContext();}
    public void ResetTransientInput(){nativeArmed=false;Zone.SuspendNavigation();Studio?.Tester?.ClearKeyboardKeys();Studio?.ReleaseTransientInput();}
    public event Action? HomeRequested;
    private long bHeldSinceUs;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        var key=e.Key==Key.System?e.SystemKey:e.Key;
        if (HandleThemeNoticeKey(e)) return;
        if(ActivePulseNotice!=null){if(HandlePulseNoticeKey(e))return;}
        if(ActiveRebelNotice is {} notice){if(key is Key.Escape or Key.B)notice.Resolve(false);else if(key is Key.A or Key.Enter && !e.IsRepeat && Keyboard.FocusedElement is Button action && notice.IsKeyboardFocusWithin)action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else if(key is Key.Left or Key.Right or Key.Up or Key.Down && Keyboard.FocusedElement is UIElement focused)focused.MoveFocus(new(key is Key.Left ? FocusNavigationDirection.Left : key is Key.Right ? FocusNavigationDirection.Right : key is Key.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down));if(key != Key.Tab)e.Handled=true;return;}
        // the identity scan popup: Esc / B backs out, A / Enter presses the focused button, nothing behind it moves
        if(updateLayer!=null){updateLayer.HandleKey(key);e.Handled=true;return;}
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
        if(Studio?.OwnsAllInput==true && Intro==null && Settings==null){Studio.OnKeyDown(sender,e);return;}
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
    public void OnKeyUp(object sender, KeyEventArgs e) { if(Studio?.OwnsAllInput==true && Intro==null && Settings==null){Studio.OnKeyUp(sender,e);return;}if(Intro!=null||Settings!=null||Check!=null||WriteScan!=null)return;if(Studio!=null)Studio.OnKeyUp(sender,e);else if (Selector == null) Zone.OnKeyUp(sender, e); }
}
