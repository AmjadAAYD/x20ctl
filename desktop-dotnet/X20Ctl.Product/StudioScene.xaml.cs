using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

public partial class StudioScene : UserControl
{
    public int Player { get; }
    public ControllerModel Model { get; }
    public ButtonDraft Draft { get; }
    public string SelectedControl { get; private set; } = "";
    public bool IsRear { get; private set; }
    internal bool RearViewAvailable => RearChoice.IsEnabled;
    private bool HasRearArtwork => Model.Id is "x20" or "x20_pro";
    public bool AssignmentsVisible => AssignmentOverview.Visibility == Visibility.Visible;
    public bool IsCurves { get; private set; }
    public bool IsMacros { get; private set; }
    public bool IsVibration {get;private set;}
    public VibrationWorkbench? Vibration {get;private set;}
    public string? ManagementPage {get;private set;}
    public DeviceWorkbench? Device {get;private set;}
    private FrameworkElement? rgbBody;
    private LightingWindow? rgbWindow;
    internal LightingWindow? LightingForReview => rgbWindow;
    public ScreenWorkbench? Screen {get;private set;}
    public TesterWorkbench? Tester {get;private set;}
    public SetupsWorkbench? Setups {get;private set;}
    /// <summary>Remap capture is engaged on Buttons (R3, or a "You press"/"Game gets" slot). Without it the controller browses.</summary>
    public bool CapturingButtonEditor=>CapturingRemap && !IsTrying && !AgentOpen;
    public void SelectPhysicalButton(string key)=>buttonsPage?.SelectPhysical(key);
    private InputOwner? lastPageOwner;
    public InputOwner InputContext=>IsMacros && Macros?.IsRecording==true?InputOwner.MacroRecording:CapturingButtonEditor?InputOwner.RemapCapture:(tryLayer != null || ManagementPage is "Tester" or "Diagnostics" || MouseCapturesInput || agentPanel?.CapturesController == true)?InputOwner.TesterCapture:InputOwner.UiNavigation;
    /// <summary>The Diagnostics tab, built on first open; it only listens while shown.</summary>
    public Diagnostics.DiagnosticsWorkbench? Diagnostics { get; private set; }
    /// <summary>Controller as mouse: the service lives in the host; the tab is built on first open.</summary>
    public MouseModeService? MouseService { get; set; }
    public MouseWorkbench? Mouse { get; private set; }
    public string NativeEngineStatus {get;set;}="Native core not connected";
    public event Action? InputContextChanged;
    public void EnableNativeNavigation(){if(!navigationMode)SetNavigationPreview(true, preserveExistingFocus: true);StatusNote.Text="Controller navigation";}
    public event Action<string?>? ActiveSetupChanged;
    private readonly SetupStore setupStore;private readonly IReadOnlyList<string> legacySources;private string? activeSetup;
    private readonly VibrationDraft vibrationDraft;
    public MacroWorkbench? Macros { get; private set; }
    private readonly MacroDraft macroDraft;
    public CurveWorkbench Curves { get; }
    public MenuItem ResetAllCurvesAction { get; }
    private string previousButtonControl = "";
    private bool navigationBeforeTester;
    public event Action? BackRequested;
    /// <summary>The title-bar Support pill; the host decides how to open the link (and the stress harness stubs it).</summary>
    public event Action? SupportRequested;
    public event Action<string>? SupportDestinationRequested;
    public event Action<string>? AgentScanRequested;
    public event Action<string>? AgentFeedbackRequested;
    private readonly ControllerLayout layout;
    private readonly Dictionary<string, PhysicalHotspot> controls = new();
    private readonly List<Button> targetButtons = new();
    public IReadOnlyList<Button> TargetButtons => targetButtons;
    public int AssignmentCount { get; private set; }
    private bool updating, navigationMode;
    private readonly bool animate;
    public bool AnimationsEnabled => animate;
    private FrameworkElement? focusTarget;
    private ConsoleFocusAdorner? focusAdorner;
    private Point? lastFocusCenter;
    public StudioScene(int player, ControllerModel model, ButtonDraft draft, bool reducedMotion = false, CurveDraft? curveDraft = null,MacroDraft? macros = null,VibrationDraft? vibration = null,SetupStore? setups=null,IReadOnlyList<string>? legacy=null,string? activeId=null)
    {
        InitializeComponent(); Player = player; Model = model; Draft = draft; PadColours.ActivePlayer = player;
        setupStore=setups??new(Path.Combine(Path.GetTempPath(),"x20ctl-render-setup-"+Guid.NewGuid().ToString("N")+".json"));legacySources=legacy??Array.Empty<string>();activeSetup=activeId;
        Unloaded+=(_,_)=>{SuspendMacroActivity();StopTry();Tester?.ExitCapture();Diagnostics?.Stop();curvesDiagnostics?.Stop();X10Hardware?.StopActiveCalibration();D10Hardware?.ShutdownSession();};
        IsEnabledChanged += (_, _) => { if (!IsEnabled) SuspendMacroActivity(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) SuspendMacroActivity(); };
        macroDraft=macros ?? new MacroDraft(model.Id);vibrationDraft=vibration??new(model.Id);
        animate = !reducedMotion && SystemParameters.ClientAreaAnimation;
        StudioBackdrop.ReducedMotion = !animate;
        ControllerContext.Text = $"{model.Name} · Player {player + 1}";
        layout = ControlRegions.Owned(model, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/x20-rear.json")));
        TargetChoice.ItemsSource = ButtonDraft.Targets.Select(key => new KeyValuePair<string, string>(key, Label(key))).ToArray();
        RearChoice.IsEnabled = HasRearArtwork;
        CurvesNav.ToolTip = draft.PcMode ? "PC mode curves · Page Down" : model.Id is "x20" or "x20_pro" or "x10" or "d10" ? "Local curve drafts · Page Down" : "Curve preview · read-only for this model · Page Down";
        Unloaded+=(_,_)=>{PcModePage.ProfileChanged-=PcProfileChanged;PcModePage.NoticeAccepted-=PcNoticeAccepted;};
        Loaded+=(_,_)=>{PcModePage.ProfileChanged-=PcProfileChanged;PcModePage.ProfileChanged+=PcProfileChanged;PcModePage.NoticeAccepted-=PcNoticeAccepted;PcModePage.NoticeAccepted+=PcNoticeAccepted;};
        BuildTargets(); BuildInventory();
        BuildTitlePills(); BuildNavigation();
        buttonsPage = new ButtonsConsolePage(model, layout, draft, Label, animate, SelectControl, OpenMacroSlot, () => { if (IsTrying) StopTry(); else StartTry(); });
        buttonsPage.Changed += RefreshEditor; ButtonsPageHost.Children.Add(buttonsPage); BuildCaptureBanner();
        PreviewGotKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(UpdateHints, System.Windows.Threading.DispatcherPriority.Background);
        foreach (var h in buttonsPage.Hotspots) RegisterFocus(h);
        Curves = new(curveDraft ?? new CurveDraft(model.Id), this,animate); ModelProfiles.Wear(Curves.Inspector, model.Id, "Curves", draft.PcMode);
        HookWorkbenchCommunityEvents();
        BuildCurvesWorkspace();
        Curves.ChannelSelected += channel => { SelectControl(channel switch { CurveChannel.LeftStick => "LSTICK_ANALOG", CurveChannel.RightStick => "RSTICK_ANALOG", CurveChannel.LeftTrigger => "LT", _ => "RT" }); UpdateCurveContext(); RefreshQuickStickControls?.Invoke(); };
        Curves.Changed += () => { UpdateCurveContext(); RefreshQuickStickControls?.Invoke(); }; foreach (Button button in Curves.Buttons) RegisterFocus(button);
        ResetAllCurvesAction=new MenuItem {Header="Reset all curves",ToolTip="Reset local drafts for both sticks and both triggers."};
        ResetAllCurvesAction.Click+=ResetAll;
        CurveMoreChoice.ContextMenu=new ContextMenu();
        CurveMoreChoice.ContextMenu.Items.Add(ResetAllCurvesAction);
        foreach(var point in Curves.PointHandles) { point.GotKeyboardFocus += (_,_)=>{if(navigationMode)ShowDetachedFocus(point);};point.LostKeyboardFocus += (_,_)=>HideDetachedFocus(); }
        UseControllerWrites();
        Loaded += (_,_) => {EnterMotion(this,14,220);foreach(var control in controls.Values)control.RevealOnce(control.Region.Key is "A" or "B" or "X" or "Y" ? 0 : control.Region.Key.StartsWith("DPAD") ? 70 : control.Region.Key is "L3" or "R3" ? 140 : 210);};
        foreach (string key in layout.Controls.Where(c=>c.Role=="macro").Select(c=>c.Key).Order())
        {
            var choice = new Button { Content = key, Tag = key, Style = (Style)FindResource("ConsoleTarget"), Width = 72, Height = 36, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
            choice.Click += (_, _) => { if (!OpenMacroSlot(key)) SelectControl(key); }; RegisterFocus(choice); RearControls.Children.Add(choice);
        }
        foreach (var button in new[] { FrontChoice, RearChoice, ResetChoice, ResetAllChoice,ResetCurveChoice,CurveMoreChoice }) RegisterFocus(button);
        SizeChanged += (_, _) =>
        {
            bool compact = ActualHeight < 750;
            bool expanded=ActualHeight>900 && ActualWidth>1300;
            RootLayout.RowDefinitions[1].Height = new(86);
            RootLayout.RowDefinitions[2].Height = new(IsCurves||IsVibration||ManagementPage is "Dashboard" or "Mouse" or "Keyboard" or "Diagnostics" or "Device" or "RGB" or "Screen" or "PcMode" or "Community"?0:expanded?44:40);
            UpdateNavigation();
            SizeAgentController();
            RootLayout.RowDefinitions[4].Height = new(IsCurves?0:IsVibration?54:compact?170:expanded?206:184);
            RootLayout.RowDefinitions[5].Height = new(expanded?78:66);
            SecondaryFooterHint.Visibility=ActualWidth<1100 || ManagementPage=="Tester"?Visibility.Collapsed:Visibility.Visible;
            StatusNote.Visibility=IsCurves||IsVibration||ManagementPage is "Dashboard" or "Mouse" or "Keyboard" or "Diagnostics" or "Device" or "RGB" or "Screen" or "PcMode" or "Community" || ActualWidth<1350?Visibility.Collapsed:Visibility.Visible;
            PrimaryHint.FontSize=OverviewHint.FontSize=expanded?16:14;
            ChangeCount.FontSize=expanded?17:15;FeatureNote.FontSize=expanded?17:15;ControlDescription.FontSize=expanded?17:15;CanvasHint.FontSize=expanded?16:14;
            InspectorStage.Margin = compact ? new(0,8,0,4) : new(0,24,0,8);
            UpdateGlyphScale();
            foreach (Button target in targetButtons){target.Height=compact?42:expanded?54:(string)target.Tag is "A" or "B" or "X" or "Y" ?50:48;target.FontSize=expanded?26:23;}
        };
        ShowController(false); ClearSelection(); UpdateButtonsPage(); ShowManagement("Dashboard");
        BuildAgent();
    }

    // ---- the agent (12 Oct 2026): the title-bar pill, and the window it opens (the round button went, 14 Oct) ----
    private Button? agentPill;

    private Agent.AgentPanel? agentPanel;
    public FrameworkElement? AgentButton => agentPill;
    public Agent.AgentPanel? AgentWindow => agentPanel;
    public bool AgentOpen => agentPanel?.IsShown == true;
    private void BuildAgent()
    {
    }
    public void OpenAgent()
    {
        CancelRemapCapture();
        if (agentPanel == null)
        {
            agentPanel = new(Model.Id, Model.Name, PcMode, RegisterFocus, Player, AgentMeter, reducedMotion:!animate);
            agentPanel.CaptureChanged += NotifyInputOwner;
            agentPanel.ControllerWrites = () => ControllerWrites && PcMode?.Service.Enabled != true;
            agentPanel.ControllerWritten += () =>
            {
                filledFromController.Remove(Draft);
                _ = LoadFromController(message => buttonsPage?.SayController(message));
            };
            Grid.SetZIndex(agentPanel.Shade, 40);
            StudioShell.Children.Add(agentPanel.Shade);
            agentPanel.Body.HorizontalAlignment = HorizontalAlignment.Right; agentPanel.Body.VerticalAlignment = VerticalAlignment.Bottom; agentPanel.Body.Margin = new(0, 0, 30, 170);
            agentPanel.CloseRequested += CloseAgent; agentPanel.ChangeControllerRequested += () => { CloseAgent(); BackRequested?.Invoke(); }; agentPanel.Walkthrough = PlayAgentTour;
            agentPanel.PageRequested += AgentGoTo;
            agentPanel.FeedbackRequested += draft => { CloseAgent(); AgentFeedbackRequested?.Invoke(draft); };
            agentPanel.MouseSwitch = on => { if (MouseService == null) return "Mouse mode isn't available in this window."; if(on){try{MouseService.TakeControl(Player,Model.Id);}catch(InvalidOperationException error){return error.Message;}}else if(MouseService.OwnerPlayer==Player && MouseService.OwnerModel==Model.Id)MouseService.Active=false; if (ManagementPage == "Mouse") ShowManagement("Mouse"); return null; };
            Grid.SetZIndex(agentPanel.Body, 41); StudioShell.Children.Add(agentPanel.Body);
        }
        // across the Studio, nearly all of it (owner's concept, 19 Oct: the screen as big as it can be), keeping a controller's
        // shape: as large as the room allows, centred
        SizeAgentController();
        agentPanel.Body.HorizontalAlignment=HorizontalAlignment.Right;agentPanel.Body.VerticalAlignment=VerticalAlignment.Bottom;
        agentPanel.Show();UpdateButtonsPage();InputContextChanged?.Invoke(); if (agentPill != null) agentPill.ToolTip = "Put Agent XCTL away";
        Dispatcher.BeginInvoke(() => agentPanel?.Input.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
    public void CloseAgent() { if (agentPanel == null) return; agentPanel.Hide();UpdateButtonsPage();InputContextChanged?.Invoke(); if (agentPill != null) agentPill.ToolTip = "Agent XCTL"; if (NeedsPcModeRefresh) PcModeSwitched?.Invoke(); }
    private void SizeAgentController()
    {
        if (agentPanel == null) return;
        double room=Math.Max(180,StudioShell.ActualHeight-70), wide=Math.Max(280,StudioShell.ActualWidth-40);
        double high=Math.Min(room,wide*Agent.AgentPanel.DesignHeight/Agent.AgentPanel.DesignWidth);
        double across=high*Agent.AgentPanel.DesignWidth/Agent.AgentPanel.DesignHeight;
        agentPanel.Body.Width=across;agentPanel.Body.Height=high;
        agentPanel.Body.Margin=new(0,0,Math.Max(20,(StudioShell.ActualWidth-across)/2),Math.Max(20,(StudioShell.ActualHeight-high)/2));
    }
    /// <summary>The agent sent the owner to a page: put the window away and go there.</summary>
    private void AgentGoTo(string page)
    {
        CloseAgent();
        switch (page)
        {
            case "ReadScan" or "WriteScan": AgentScanRequested?.Invoke(page); break;
            case "Diagnostics" or "Tools": ShowManagement("Diagnostics"); break;
            case "Mouse" or "Keyboard" or "PcMode" or "Device" or "RGB" or "Screen": ShowManagement(page); break;
            case "Curves": ShowCurves(true); break;
            case "Macros": ShowMacros(true); break;
            case "Vibration": ShowVibration(true); break;
            case "Back": ShowButtons(); buttonsPage?.ShowView(true); break;
            default: ShowButtons(); break;
        }
    }

    private Agent.AgentTour? agentTour;
    /// <summary>A stand-in for the controller's drift reading, set by review harnesses before the window opens.</summary>
    public Func<Action<string, int>, X20Ctl.Zone.Agent.IDriftMeter>? AgentMeter { get; set; }
    /// <summary>The walk-through playing now, if any.</summary>
    public Agent.AgentTour? AgentTour => agentTour;
    /// <summary>A walk-through started (for review captures).</summary>
    public event Action<Agent.AgentTour>? AgentTourStarted;
    /// <summary>After the agent changes something: the window steps aside, each change is shown on the page where it lives,
    /// then the window comes back.</summary>
    private async Task PlayAgentTour(X20Ctl.Zone.PcMode.PcModeProfile before, X20Ctl.Zone.PcMode.PcModeProfile after, X20Ctl.Zone.Agent.DriftReport? measured)
    {
        var steps = X20Ctl.Zone.Agent.AgentKnowledge.Tour(before, after);
        if (steps.Count == 0 || agentTour != null) return;
        CloseAgent();
        var tour = agentTour = new Agent.AgentTour();
        Grid.SetZIndex(tour.Layer, 45); StudioShell.Children.Add(tour.Layer); Fx.Appear(tour.Layer, .96, 320);
        AgentTourStarted?.Invoke(tour);
        try { await tour.Play(steps, before, after, measured, Visit); }
        finally
        {
            agentTour = null;
            Fx.Leave(tour.Layer, () => StudioShell.Children.Remove(tour.Layer), .97, 220);
            MarkAgentChanges(steps.Select(s => s.Target));
            OpenAgent();
        }

        void Visit(X20Ctl.Zone.Agent.TourStep step)
        {
            if (step.Kind == X20Ctl.Zone.Agent.TourKind.Button)
            {
                ShowButtons();
                if (controls.ContainsKey(step.Target)) SelectControl(step.Target);
                return;
            }
            if (!IsCurves) ShowCurves(true);
            Curves.SelectChannel(Enum.Parse<CurveChannel>(step.Target));
        }
    }
    /// <summary>Every control the agent just changed glows on the controller, on the side where most of them are.</summary>
    public IReadOnlyList<string> MarkAgentChanges(IEnumerable<string> targets)
    {
        var keys = targets.Select(t => t switch { "LeftStick" => "L3", "RightStick" => "R3", "LeftTrigger" => "LT", "RightTrigger" => "RT", _ => t })
            .Where(controls.ContainsKey).Distinct().ToList();
        if (keys.Count == 0) return keys;
        var regions = keys.Select(k => layout.Controls.First(r => r.Key == k)).ToList();
        bool rear = regions.Count(r => r.View is "back" or "shoulder") * 2 > regions.Count;
        if (rear != IsRear) ShowController(rear);
        foreach (var k in keys) controls[k].Mark();
        return keys;
    }
    private readonly ButtonsConsolePage? buttonsPage;
    /// <summary>Title-bar pills: Support (Ko-fi, opens only when chosen) in the same quiet glass as the Zone, and the controller name.</summary>
    private string? currentBatteryLevel;
    private string? currentTransport;
    private string? currentFirmware;
    private Button? deviceControl;
    private readonly TextBlock deviceState=Kit.Text("Offline",13,"DS.TextSoft",FontWeights.SemiBold);
    private readonly TextBlock batteryLabel=Kit.Text("Battery · Not reported",13,"DS.TextSoft",FontWeights.SemiBold);
    public void SetTransport(string? transport)
    {
        currentTransport = transport;
        Dashboard?.SetTransport(transport);
        Device?.SetTransport(transport);
        if (currentTransport == "2.4 GHz receiver")
        {
            if (currentFirmware == null) Device?.SetFirmware("Not readable over XInput");
            if (currentBatteryLevel is "wired" or "receiver" or null)
            {
                batteryLabel.Text = "Battery · Not reported (receiver)";
                batteryLabel.Foreground = DS.Brush("DS.TextSoft");
            }
        }
    }
    public void SetFirmware(string? fw)
    {
        currentFirmware = fw;
        Dashboard?.SetFirmware(fw);
        Device?.SetFirmware(fw);
    }
    public event Action<TelemetryData>? TelemetryUpdated;
    private string? currentHardwareId;
    // owner, 8 Nov 2026: "connected using USB and Bluetooth, yet it says it isn't connected". With the cable in Switch mode
    // XInput sees no controller, but the controller still answers over Bluetooth: a reading from it in the last five
    // minutes (the live read runs once a minute) counts as connected, without pretending there is gameplay input.
    private DateTime bluetoothSeen;
    private bool BluetoothLive => DateTime.UtcNow - bluetoothSeen < TimeSpan.FromMinutes(5);
    private bool Linked => gameplayConnected == true || BluetoothLive;
    private bool BluetoothOnly => gameplayConnected != true && BluetoothLive;
    private void ShowLink()
    {
        UpdateDeviceHeader();
        if (!navigationMode) StatusNote.Text = Linked ? (BluetoothOnly ? "Connected over Bluetooth" : "Connected") : "Controller offline";
        Dashboard?.SetInputConnected(Linked, BluetoothOnly); Device?.SetConnection(Linked, BluetoothOnly);
    }
    /// <summary>A Bluetooth reading; <paramref name="seenAt"/> is when it was read (a remembered one is not fresh).</summary>
    public void ApplyTelemetry(TelemetryData data) => ApplyTelemetry(data, DateTime.UtcNow);
    public void ApplyTelemetry(TelemetryData data, DateTime seenAt)
    {
        if ((data.Battery != null || data.Firmware != null) && seenAt > bluetoothSeen) { bluetoothSeen = seenAt; ShowLink(); }
        if(data.HardwareId!=null){currentHardwareId=data.HardwareId;Device?.SetHardwareId(data.HardwareId);}
        if (data.Firmware != null)
        {
            SetFirmware(data.Firmware);
        }
        if (data.Battery != null)
        {
            SetBatteryLevel(data.Battery);
        }
        TelemetryUpdated?.Invoke(data);
    }
    public void SetBatteryLevel(string? level)
    {
        if (currentTransport == "2.4 GHz receiver" && (level is "wired" or "receiver" or null))
        {
            if (currentBatteryLevel != null && currentBatteryLevel != "wired" && currentBatteryLevel != "receiver")
            {
                // Preserve verified Bluetooth telemetry reading
                return;
            }
            currentBatteryLevel = level;
            Dashboard?.SetBatteryLevel(level, currentTransport);
            Device?.SetBatteryLevel(level, currentTransport);
            batteryLabel.Text = "Battery · Not reported (receiver)";
            batteryLabel.Foreground = DS.Brush("DS.TextSoft");
            UpdateDeviceHeader();
            return;
        }
        currentBatteryLevel = level;
        Dashboard?.SetBatteryLevel(level, currentTransport);
        Device?.SetBatteryLevel(level, currentTransport);
        batteryLabel.Text = "Battery · " + (level switch { "full" => "Full", "medium" => "Medium", "low" => "Low", "empty" => "Empty", "wired" => "Wired", _ => level ?? "Not reported" });
        batteryLabel.Foreground = DS.Brush(level is "low" or "empty" ? "DS.Warn" : "DS.TextSoft");
        UpdateDeviceHeader();
    }
    private readonly HeaderBattery headerBattery=new();
    private readonly StatusChip controllerConnection = new("Offline", ChipKind.Neutral) { Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private void BuildTitlePills()
    {
        var heart = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) };
        var support = new Button { Cursor = Cursors.Hand, ToolTip = "Support X20CTL on Ko-fi (opens your browser)", FocusVisualStyle = null, Margin = new(0, 0, 12, 0),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            Content = new Border { CornerRadius = new(10), Height = 36, Padding = new(14, 0, 18, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(160, 255, 150, 170))),
                Background = new LinearGradientBrush(ThemeCatalog.R(Color.FromRgb(232, 54, 96)), ThemeCatalog.R(Color.FromRgb(196, 30, 78)), 0), Effect = new DropShadowEffect { Color = ThemeCatalog.R(Color.FromRgb(232, 54, 96)), BlurRadius = 18, ShadowDepth = 0, Opacity = .5 },
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { heart, new TextBlock { Text = "Support", FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } } } } };
        support.Click += (_, _) =>
        {
            if (SupportDestinationRequested is { } openUrl) ProductLinks.ShowSupportMenu(support, openUrl);
            else SupportRequested?.Invoke();
        };
        var pill = (Border)support.Content; support.MouseEnter += (_, _) => pill.BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(200, 255, 255, 255))); support.MouseLeave += (_, _) => pill.BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(160, 255, 150, 170)));
        var name = new Border { CornerRadius = new(18), Height = 36, Padding = new(14, 0, 16, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(80, 125, 182, 255))), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(60, 30, 60, 130))),
            Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = DS.Brush("DS.AccentHi"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) }, new TextBlock { Text = Model.Name.Replace("EasySMX ", ""), FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } } } };
        // Agent XCTL in the title bar, in the gap left of Support: opens (or puts away) the agent
        var agentPill = this.agentPill = new Button { Cursor = Cursors.Hand, ToolTip = "Agent XCTL", FocusVisualStyle = null, Margin = new(0, 0, 12, 0),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            Content = new Border { CornerRadius = new(18), Height = 36, Padding = new(14, 0, 16, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(150, 125, 182, 255))),
                Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(38,91,184))),
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) },
                    new TextBlock { Text = "Agent XCTL", FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } } } } };
        agentPill.Click += (_, _) => { if (AgentOpen) CloseAgent(); else OpenAgent(); };
        var agentBorder = (Border)agentPill.Content; agentPill.MouseEnter += (_, _) => agentBorder.BorderBrush = Brushes.White; agentPill.MouseLeave += (_, _) => agentBorder.BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(150, 125, 182, 255)));
        var more = new Button { Content="⋯",Style=DS.Style("Zone.Utility"),Width=38,Height=36,MinHeight=36,Padding=new(0),Margin=new(8,0,0,0),ToolTip="Help and utilities" };
        more.ContextMenu=new ContextMenu();
        var supportItem=new MenuItem {Header="Support X20CTL"};supportItem.Click+=(_,_)=>SupportRequested?.Invoke();more.ContextMenu.Items.Add(supportItem);
        var helpItem=new MenuItem {Header="Diagnostics and help"};helpItem.Click+=(_,_)=>ShowManagement("Diagnostics");more.ContextMenu.Items.Add(helpItem);
        more.Click+=(_,_)=>more.ContextMenu.IsOpen=true;
        var deviceBody=new StackPanel{Orientation=Orientation.Horizontal,Children={Kit.Text("",16,"DS.AccentHi").Also(t=>t.FontFamily=new FontFamily("Segoe Fluent Icons")),Kit.Text(Model.Name.Replace("EasySMX ",""),14,"DS.Text",FontWeights.SemiBold).Also(t=>t.Margin=new(10,0,8,0)),deviceState}};
        deviceState.TextTrimming=TextTrimming.None;
        deviceControl=new Button{Content=deviceBody,Style=DS.Style("Zone.Utility"),Height=36,MinHeight=0,MinWidth=0,Width=double.NaN,Padding=new(12,0,14,0),ToolTip=new StackPanel{Children={Kit.Text($"{Model.Name} · Player {Player+1}",14,"DS.Text",FontWeights.SemiBold),batteryLabel,Kit.Text("Transport · Not reported",13,"DS.TextSoft")}}};
        deviceControl.Click+=(_,_)=>ShowManagement("Device");System.Windows.Automation.AutomationProperties.SetName(deviceControl,"Selected controller status and details");
        AgentTitleHost.Children.Add(agentPill);
        // PC Mode entry beside Agent XCTL (owner, 6 Nov 2026): every pad except the X20 family, scan -> type -> warning -> on
        if (X20Ctl.Zone.PcMode.PcModeEntry.IsAvailable(Model.Id))
        {
            var pcPill = new Button { Content = "PC Mode", Style = DS.Style("Zone.Utility"), Height = 36, MinHeight = 36, Padding = new(14, 0, 14, 0), Margin = new(0, 0, 12, 0), ToolTip = "Remap this controller on the PC" };
            System.Windows.Automation.AutomationProperties.SetName(pcPill, "Open PC Mode");
            pcPill.Click += (_, _) => RequestPcMode();
            AgentTitleHost.Children.Add(pcPill);
        }
        TitlePills.Children.Add(deviceControl);TitlePills.Children.Add(more);
        // the battery, in the middle of the title bar (owner request 17 Oct 2026): only what the controller reports
        headerBattery.Click+=(_,_)=>ShowManagement("Device");System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(headerBattery,true);
        Grid.SetColumnSpan(headerBattery,2);HeaderGrid.Children.Add(headerBattery);UpdateDeviceHeader();

    }
    private void UpdateDeviceHeader()
    {
        if(deviceControl==null)return;
        double previousWidth=deviceControl.ActualWidth;string previousText=deviceState.Text;
        string? level=BatteryVisible?currentBatteryLevel:null;
        deviceState.Text="· "+(Linked?(level is "full" or "medium" or "low" or "empty" or "wired"?(level=="wired"?"Wired":batteryLabel.Text.Replace("Battery · ","")+" battery"):(level!=null&&level!="receiver"&&!level.Contains("Not reported")?(level.Contains("battery",StringComparison.OrdinalIgnoreCase)?level:level+" battery"):"Connected")):"Offline");
        deviceState.Foreground=DS.Brush(!Linked||level is "low" or "empty"||level?.StartsWith("1/4")==true?"DS.Warn":"DS.TextSoft");
        headerBattery.Show(Linked?level:null);
        headerBattery.Visibility=Visibility.Collapsed;
        batteryLabel.Visibility=BatteryVisible?Visibility.Visible:Visibility.Collapsed;
        if(animate&&deviceControl.IsLoaded&&previousWidth>0&&previousText!=deviceState.Text){deviceControl.BeginAnimation(WidthProperty,null);deviceControl.Width=double.NaN;deviceControl.Measure(new Size(double.PositiveInfinity,36));double next=Math.Ceiling(deviceControl.DesiredSize.Width)+2;var anim=new DoubleAnimation(previousWidth,next,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop,EasingFunction=DS.EaseOut};anim.Completed+=(_,_)=>deviceControl.Width=double.NaN;deviceControl.BeginAnimation(WidthProperty,anim);}
        else deviceControl.Width=double.NaN;
    }
    internal bool BatteryVisible => PcMode?.Service.Enabled != true;
    internal void RefreshBatteryVisibility()
    {
        UpdateDeviceHeader();
        Dashboard?.ShowBattery(BatteryVisible);
        Device?.ShowBattery(BatteryVisible);
        agentPanel?.RefreshStrip();
    }
    /// <summary>The console-design Buttons page replaces the older canvas/inspector/shelf while Buttons is active.</summary>
    private static void ShowLayer(UIElement el, bool visible)
    {
        el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        el.IsHitTestVisible = visible;
    }

    private void UpdateButtonsPage()
    {
        if(IllustratedThemeUI.Current!=null)StudioBackdrop.ArtworkPage=AgentOpen?"Agent XCTL":CurrentPage;
        IllustratedThemeUI.QueueSkin(this);
        if (buttonsPage == null) return;
        bool on = OnButtonsPage;
        ShowLayer(ButtonsPageHost, on);
        if (!on) buttonsPage.CancelListening();
        if (on) { if (editorDirty) RefreshEditor(); HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = Visibility.Collapsed; OverviewChoice.Visibility = Visibility.Collapsed; }
        UpdateHints();NotifyInputOwner();
    }
    private LiveController ActiveLive => OnButtonsPage && buttonsPage != null ? buttonsPage.Live : Live;
    private void BuildInventory()
    {
        // C and T are onboard modifier/turbo functions, not gameplay inputs, so they are not offered here.
        foreach (string key in new[] { "HOME" }.Where(key=>layout.Controls.Any(c=>c.Key==key)))
        {
            bool axis = key.EndsWith("ANALOG");
            var button = new Button { Content = key == "HOME" ? "Guide" : axis ? key.StartsWith('L') ? "L axis" : "R axis" : Glyph(key), Tag = key,
                Style = (Style)FindResource(axis ? "InventoryAction" : "ConsoleTarget"), Width = key == "HOME" ? 72 : axis ? 70 : 48, Height = axis ? 28 : 38,
                FontSize = axis ? 14 : key == "HOME" ? 17 : 22, ToolTip = Label(key) + " · inspection only", IsEnabled = layout.Controls.Any(c => c.Key == key) };
            button.Click += (_, _) => { ShowAssignments(false); SelectControl(key); }; RegisterFocus(button); SystemInventory.Children.Add(button);
            void Arrange()
            {
                double center = (SystemInventory.ActualWidth - button.Width) / 2, spread = Math.Min(50, SystemInventory.ActualWidth / 3);
                Canvas.SetLeft(button, center + (key is "CAPTURE" or "LSTICK_ANALOG" ? -spread : key is "TURBO" or "RSTICK_ANALOG" ? spread : 0));
                Canvas.SetTop(button, key == "HOME" ? 12 : (SystemInventory.ActualHeight - 38) / 2 + 12);
            }
            SystemInventory.SizeChanged += (_, _) => Arrange(); button.SizeChanged += (_, _) => Arrange();
            System.Windows.Automation.AutomationProperties.SetName(button, "Inspect " + Label(key));
        }
        foreach (string key in new[] { "SELECT", "START", "LSTICK_ANALOG", "RSTICK_ANALOG" }.Concat(layout.Controls.Where(c=>c.Role=="macro").Select(c=>c.Key).Order()).Where(key=>layout.Controls.Any(c=>c.Key==key)))
        {
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new TextBlock { Text = key.EndsWith("ANALOG") ? key.StartsWith('L') ? "Left stick" : "Right stick" : Glyph(key), FontSize = key.EndsWith("ANALOG") ? 16 : 22, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = key.StartsWith('M') ? "Not read" : key.EndsWith("ANALOG") ? "Analog role" : "Output only", FontSize = 13, Foreground = (Brush)FindResource("Secondary"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 10, 0, 0) });
            var button = new Button { Content = content, Tag = key, Style = (Style)FindResource("ConsoleMapping"), ToolTip = Label(key), IsEnabled = layout.Controls.Any(c => c.Key == key) };
            button.Click += (_, _) => { ShowAssignments(false); SelectControl(key); }; RegisterFocus(button); InventoryOverview.Children.Add(button);
        }
    }
    private void BuildTargets()
    {
        var groups = new (string Name, string[] Keys, bool Diamond)[] {
            ("FACE", ["Y", "X", "B", "A"], true),
            ("SHOULDERS", ["LB", "RB", "LT", "RT"], false),
            ("D-PAD", ["DPAD_UP", "DPAD_LEFT", "DPAD_RIGHT", "DPAD_DOWN"], true),
            ("STICKS / SYSTEM", ["L3", "R3", "SELECT", "START"], false)
        };
        foreach (var (name, keys, diamond) in groups)
        {
            int column = QuickTargets.ColumnDefinitions.Count;
            QuickTargets.ColumnDefinitions.Add(new() { Width = new GridLength(diamond ? .9 : 1.2, GridUnitType.Star) });
            var group = new Grid { Margin = new(10, 0, 10, 0) }; Grid.SetColumn(group, column); QuickTargets.Children.Add(group);
            group.RowDefinitions.Add(new() { Height = new GridLength(22) }); group.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
            group.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("ConsoleLabel"), HorizontalAlignment = HorizontalAlignment.Center, FontSize = 14 });
            Panel choices = diamond ? new Canvas() : new Grid(); Grid.SetRow(choices, 1); group.Children.Add(choices);
            if (choices is Grid pairs) for (int i = 0; i < 2; i++) { pairs.RowDefinitions.Add(new()); pairs.ColumnDefinitions.Add(new()); }
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                var choice = new Button { Content = TargetGlyph(key), Tag = key, Style = (Style)FindResource(name == "FACE" ? "ConsoleFaceGlyph" : "ConsoleTarget"), ToolTip = Label(key), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                if (!diamond) { choice.Width = name == "SHOULDERS" ? 94 : 108; choice.FontSize = key is "SELECT" or "START" ? 20 : 23; }
                System.Windows.Automation.AutomationProperties.SetName(choice, "Assign " + Label(key));
                if (diamond)
                {
                    int position = i;
                    void Arrange()
                    {
                        double center = (choices.ActualWidth - choice.Width) / 2, reach = Math.Min(76, choices.ActualWidth / 3);
                        Canvas.SetLeft(choice, center + (position == 1 ? -reach : position == 2 ? reach : 0));
                        Canvas.SetTop(choice, position == 0 ? 0 : position == 3 ? choices.ActualHeight - choice.Height : (choices.ActualHeight - choice.Height) / 2);
                    }
                    choices.SizeChanged += (_, _) => Arrange(); choice.SizeChanged += (_, _) => Arrange();
                }
                else { Grid.SetRow(choice, i / 2); Grid.SetColumn(choice, i % 2); }
                choice.Click += (_, _) => SetDraftTarget(key); RegisterFocus(choice); choices.Children.Add(choice); targetButtons.Add(choice);
            }
        }
    }
    private void BuildAssignments()
    {
        AssignmentRows.Children.Clear(); AssignmentRows.RowDefinitions.Clear(); AssignmentCount = 0;
        if (Model.Id != "x20") { AssignmentRows.Children.Add(new TextBlock { Text = "No verified mappings for this model.", FontSize = 17, TextWrapping = TextWrapping.Wrap }); return; }
        var groups = new (string Name, string[] Keys, int Columns)[] {
            ("FACE", ["A", "B", "X", "Y"], 4), ("SHOULDERS", ["LB", "RB", "LT", "RT"], 2),
            ("D-PAD", ["DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"], 4), ("STICKS", ["L3", "R3"], 2)
        };
        foreach (var (name, keys, columns) in groups)
        {
            int index = AssignmentRows.RowDefinitions.Count; AssignmentRows.RowDefinitions.Add(new() { Height = new GridLength(name == "SHOULDERS" ? 1.5 : 1, GridUnitType.Star) });
            var group = new Grid(); group.RowDefinitions.Add(new() { Height = new GridLength(22) }); group.RowDefinitions.Add(new()); Grid.SetRow(group, index); AssignmentRows.Children.Add(group);
            group.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("ConsoleLabel") });
            var mappings = new System.Windows.Controls.Primitives.UniformGrid { Columns = columns, Rows = keys.Length / columns }; Grid.SetRow(mappings, 1); group.Children.Add(mappings);
            foreach (string key in keys)
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                content.Children.Add(new TextBlock { Text = TargetGlyph(key), FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(new TextBlock { Text = " → ", FontSize = 16, Foreground = (Brush)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(new TextBlock { Text = TargetGlyph(Draft.Target(key)), FontSize = 20, Foreground = (Brush)FindResource(Draft.Target(key) == key ? "Secondary" : "Ice"), VerticalAlignment = VerticalAlignment.Center });
                var choice = new Button { Content = content, Tag = key, Style = (Style)FindResource("ConsoleMapping"), ToolTip = Label(key) + " → " + Label(Draft.Target(key)) };
                System.Windows.Automation.AutomationProperties.SetName(choice, choice.ToolTip.ToString());
                choice.Click += (_, _) => { SelectControl(key); ShowAssignments(false); }; RegisterFocus(choice); mappings.Children.Add(choice); AssignmentCount++;
            }
        }
    }
    private void EnterMotion(FrameworkElement element, double distance, int milliseconds)
    {
        element.BeginAnimation(OpacityProperty, null);
        element.RenderTransform = Transform.Identity;
        element.Opacity = 1;
    }
    public void ShowController(bool rear)
    {
        IsRear = rear; controls.Clear(); Hotspots.Children.Clear();
        ArtworkStage.RenderTransform = new ScaleTransform(Model.Id=="x20"&&!IsRear?1.1:1,Model.Id=="x20"&&!IsRear?1.1:1);
        Live.Smooth = animate; Live.Show(Model.Id, IsRear); CanvasHint.Text = IsRear ? "BACK · PHYSICAL CONTROLS" : "FRONT · PHYSICAL CONTROLS";
        foreach (var region in layout.Controls.Where(r => r.Role != "axis" && !IsOnboardFunction(r.Key) && (r.View == (IsRear ? "back" : "front") || IsRear && r.View == "shoulder"))) AddControl(region, Hotspots);
        foreach (var control in controls.Values) control.RevealOnce(control.Region.Key is "A" or "B" or "X" or "Y" ? 0 : control.Region.Key.StartsWith("DPAD") ? 70 : control.Region.Key is "L3" or "R3" ? 140 : 210);
        FrontChoice.Background = (Brush)FindResource(IsRear ? "ConsoleSurface" : "Blue");
        RearChoice.Background = (Brush)FindResource(IsRear ? "Blue" : "ConsoleSurface");
        if (animate)
        {
            ControllerCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(220)));
            var slide = new TranslateTransform(); ControllerCanvas.RenderTransform = slide;
            slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(IsRear ? 12 : -12, 0, TimeSpan.FromMilliseconds(220)));
        }
    }
    private void AddControl(ControlRegion region, Canvas canvas)
    {
        var button = new PhysicalHotspot(region, animate) { ToolTip = Label(region.Key), Tag = region.Key, IsEnabled = !IsCurves || region.Key is "L3" or "R3" or "LSTICK_ANALOG" or "RSTICK_ANALOG" or "LT" or "RT" };
        var at = LiveController.Place(Model.Id, region); Canvas.SetLeft(button, at.X); Canvas.SetTop(button, at.Y);
        button.Click += (_, _) => { if (IsCurves) Curves.SelectChannel(region.Key switch {"L3" or "LSTICK_ANALOG"=>CurveChannel.LeftStick,"R3" or "RSTICK_ANALOG"=>CurveChannel.RightStick,"LT"=>CurveChannel.LeftTrigger,_=>CurveChannel.RightTrigger}); else if (!OpenMacroSlot(region.Key)) SelectControl(region.Key); };
        button.GotKeyboardFocus += (_, _) => { if (navigationMode) { if (IsCurves) Curves.SelectChannel(region.Key switch {"L3" or "LSTICK_ANALOG"=>CurveChannel.LeftStick,"R3" or "RSTICK_ANALOG"=>CurveChannel.RightStick,"LT"=>CurveChannel.LeftTrigger,_=>CurveChannel.RightTrigger}); else SelectControl(region.Key); } };
        controls[region.Key] = button; canvas.Children.Add(button);
    }
    public void ClearSelection()
    {
        SelectedControl = ""; foreach (var control in controls.Values) control.Illuminate(false, navigationMode); RefreshEditor();
    }
    public void SelectControl(string key)
    {
        var region = layout.Controls.FirstOrDefault(r => r.Key == key) ?? throw new ArgumentException("Unknown physical control.", nameof(key));
        if (region.View is "back" or "shoulder" && !IsRear) ShowController(true);
        else if (region.View == "front" && IsRear) ShowController(false);
        bool changed = SelectedControl != key; SelectedControl = key;
        foreach (var (name, control) in controls) control.Illuminate(name == key || region.Role == "axis" && name == (key.StartsWith('L') ? "L3" : "R3"), navigationMode);
        RefreshEditor();
        if (changed) { EnterMotion(MappingHero, 12, 160); EnterMotion(MappingPair, 8, 160); EnterMotion(QuickTargets, 10, 220); }
        if (IsCurves) UpdateCurveContext();
    }
    public void SetDraftTarget(string target)
    {
        if (SelectedControl.Length == 0) return;
        Draft.Set(SelectedControl, target); RefreshEditor();
        EnterMotion(OutputTile, 8, 160);
    }
    public void FocusSelectedControl()
    {
        if(ManagementPage=="Tester"){Tester?.Exit.Focus();return;}
        if(ManagementPage=="Device"){Device?.OpenTester.Focus();return;}
        if(ManagementPage=="Setups"){Setups?.NewButton.Focus();return;}
        if(ManagementPage=="Dashboard"){Dashboard?.FocusHero();return;}
        if(ManagementPage=="Mouse"){Mouse?.Switch.Focus();return;}
        if(ManagementPage=="Diagnostics"){Diagnostics?.Reset.Focus();return;}
        if(ManagementPage=="PcMode")
        {
            var btn = ManagementBodyHost.Children.OfType<FrameworkElement>()
                .SelectMany(f => f is ScrollViewer sv && sv.Content is Panel p ? p.Children.OfType<Button>() : (f as Panel)?.Children.OfType<Button>() ?? Enumerable.Empty<Button>())
                .FirstOrDefault();
            btn?.Focus();
            return;
        }
        if(IsVibration){Vibration?.FocusPreset();return;}
        if(IsMacros) {Macros?.FocusSelection();return;}
        if(IsCurves && Curves != null) { Curves.Buttons.FirstOrDefault()?.Focus(); return; }
        if (SelectedControl.Length == 0) SelectControl("A");
        string key = SelectedControl is "LSTICK_ANALOG" ? "L3" : SelectedControl is "RSTICK_ANALOG" ? "R3" : SelectedControl;
        if (OnButtonsPage && buttonsPage?.Hotspot(key) is { } pageSpot) pageSpot.Focus(); else if (controls.TryGetValue(key, out var button)) button.Focus();
    }
    private bool editorDirty = true;
    private void RefreshEditor()
    {
        if (!OnButtonsPage)
        {
            editorDirty = true;
            return;
        }
        editorDirty = false;
        updating = true; var region = layout.Controls.FirstOrDefault(r => r.Key == SelectedControl);
        bool macro = region?.Role == "macro", editable = region != null && Draft.CanEdit(SelectedControl);
        SelectedName.Text = region == null ? "Choose a control" : Label(SelectedControl);
        ControlDescription.Text = region == null ? "Every physical control is inspectable" : macro ? Model.Id is "x20" or "x20_pro"||Draft.PcMode?"Rear paddle · Macro slot":"Rear control · Configuration role unverified" : region.Role == "axis" ? "Analog movement · Information only" : region.Role == "system" || SelectedControl is "SELECT" or "START" ? "System control · Information only" : SelectedControl.StartsWith("DPAD") ? "Directional input" : SelectedControl is "LB" or "RB" ? "Shoulder button" : SelectedControl is "LT" or "RT" ? "Analog trigger · Button output" : SelectedControl is "L3" or "R3" ? "Stick click" : "Face button";
        ControlType.Text = region == null ? "CONTROLLER-FIRST MAPPING" : macro ? Model.Id is "x20" or "x20_pro"||Draft.PcMode?"REAR MACRO SLOT":"REAR CONTROL" : region.Role == "axis" ? "ANALOG AXIS · READ ONLY" : region.Role == "system" ? "SYSTEM / SPECIAL CONTROL" : "PHYSICAL INPUT";
        CurrentCaption.Text = editable ? "CURRENT · PREVIEW" : "HARDWARE STATE";
        CurrentMapping.Text = editable ? Glyph(SelectedControl) : region == null ? "—" : "Unknown";
        DraftCaption.Text = macro ? "CONTROL ROLE" : editable ? "DRAFT OUTPUT" : "SUPPORT";
        DraftMapping.Text = macro ? Model.Id is "x20" or "x20_pro"||Draft.PcMode?"Macro\nslot":"Unverified" : editable ? Glyph(Draft.Target(SelectedControl)) : region == null ? "—" : "No write";
        UpdateGlyphScale();
        MappingArrow.Visibility = editable ? Visibility.Visible : Visibility.Hidden;
        TargetChoice.IsEnabled = editable; TargetChoice.SelectedValue = editable ? Draft.Target(SelectedControl) : null;
        TargetCaption.Text = macro ? "REAR PADDLES · INSPECT CONTROL" : editable ? "CHOOSE OUTPUT" : "OUTPUTS · SELECT A MAPPABLE CONTROL";
        QuickTargets.Visibility = macro || AssignmentsVisible ? Visibility.Collapsed : Visibility.Visible;
        RearNavigation.Visibility = macro && !AssignmentsVisible ? Visibility.Visible : Visibility.Collapsed;
        InventoryOverview.Visibility = AssignmentsVisible ? Visibility.Visible : Visibility.Collapsed;
        foreach (Button choice in targetButtons)
        {
            bool active = editable && (string)choice.Tag == Draft.Target(SelectedControl);
            choice.IsEnabled = editable; choice.Background = active ? (Brush)FindResource("Blue") : new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(117, 24, 35, 55)));
            choice.BorderBrush = active ? (Brush)FindResource("Ice") : new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(54, 88, 121, 163)));
        }
        foreach (Button choice in RearControls.Children) choice.Background = (string)choice.Tag == SelectedControl ? (Brush)FindResource("Blue") : new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(24, 255, 255, 255)));
        FeatureNote.Text = Draft.PcMode ? (region == null ? "PC mode: pick a button, then what it should do. It applies on this PC, to games." : macro ? $"PC mode can't see {SelectedControl} itself: in Macros, pick the button that plays its sequence." : !editable ? "PC mode can't change this control." : "PC mode: this applies on this PC, to games, right away. Nothing is written to the controller.") : Model.Id != "x20" ? "Configuration is unverified for this model. You can still inspect its controls." : region == null ? "Select a button on the controller. Inspect shoulders and paddles on the back." : macro ? $"{SelectedControl} is a controller-side macro slot. Editing is unavailable; hardware state is unknown." : region.Role == "axis" ? "Movement and click share this stick cap. Analog settings and live input are unavailable here." : !editable ? "This control is present. Remapping is unverified and no write is available." : "Choose an output on the shelf. This draft stays local; hardware settings are unknown.";
        ChangeCount.Text = Draft.UnsentChanges == 0 ? "No unsent changes" : $"{Draft.UnsentChanges} unsent change{(Draft.UnsentChanges == 1 ? "" : "s")}";
        DraftChip.Text = Draft.UnsentChanges == 0 ? "No local changes" : $"{Draft.UnsentChanges} local change{(Draft.UnsentChanges == 1 ? "" : "s")}";
        if(RearControls.Children.Count>4){RearControls.Columns=RearControls.Children.Count;RearControls.Width=RearControls.Children.Count*90;}
        SelectedHint.Text = region == null ? "Face · shoulders · D-pad · sticks · system" : $"{Label(SelectedControl)} · {(IsRear ? "back" : "front")}";
        CanvasHint.Text = IsRear ? "BACK · PHYSICAL CONTROLS" : "FRONT · PHYSICAL CONTROLS";
        PrimaryHint.Text = editable ? " Choose output" : region == null ? " Select control" : " Inspect control";
        OverviewHint.Text = " Assignments";
        ResetChoice.IsEnabled = editable; ResetAllChoice.IsEnabled = Draft.UnsentChanges > 0;
        DraftRows.Children.Clear();
        string context = macro ? "Macro · Not read" : region == null ? "No selected control" : !editable ? "Readback unavailable" : Draft.Target(SelectedControl) == SelectedControl ? "Unmodified preview" : $"{Glyph(SelectedControl)} → {Glyph(Draft.Target(SelectedControl))} · Modified";
        DraftRows.Children.Add(new TextBlock { Text = context, FontSize = 15, Foreground = (Brush)FindResource(editable && Draft.Target(SelectedControl) != SelectedControl ? "Ice" : "Muted"), TextWrapping = TextWrapping.Wrap });
        BuildAssignments();
        buttonsPage?.Refresh(SelectedControl);
        Apply.IsEnabled = false; updating = false; SyncPcMode();
        if (ControllerWrites) { ChangeCount.Text = "Buttons · Apply to save on the controller"; DraftChip.Text = Draft.UnsentChanges == 0 ? "No remaps" : $"{Draft.UnsentChanges} remap{(Draft.UnsentChanges == 1 ? "" : "s")}"; FeatureNote.Text = "Choose an output, then Apply to controller."; }
    }
    private void UpdateGlyphScale()
    {
        bool compact = ActualHeight > 0 && ActualHeight < 750, expanded = ActualHeight > 900;
        double diameter = compact ? 80 : expanded ? 150 : 120;
        CurrentTile.Width = CurrentTile.Height = OutputTile.Width = OutputTile.Height = diameter;
        bool editable = Draft.CanEdit(SelectedControl);
        ExpandedContext.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        AssignmentKind.Text = editable ? "Standard assignment" : SelectedControl.StartsWith('M') ? "Controller macro role" : "Physical control information";
        CurrentMapping.FontSize = DraftMapping.FontSize = editable ? compact ? 40 : expanded ? 68 : 58 : compact ? 16 : 20;
    }
    public void ShowAssignments(bool visible) { AssignmentOverview.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; MappingHero.Visibility = visible ? Visibility.Collapsed : Visibility.Visible; RefreshEditor(); if (visible) { TargetCaption.Text = "CONTROL INVENTORY · SYSTEM / REAR"; SelectedHint.Text = "Read-only roles · Hardware state unknown"; } EnterMotion(visible ? AssignmentOverview : MappingHero, 12, 220); }
    private void ToggleOverview(object sender, RoutedEventArgs e) => ShowAssignments(!AssignmentsVisible);
    private void RegisterFocus(Button button)
    {
        button.GotKeyboardFocus += (_, _) => { if (navigationMode && button is PhysicalHotspot) { ShowDetachedFocus(button); Lift(button, true); } };
        button.LostKeyboardFocus += (_, _) => { HideDetachedFocus(); Lift(button, false); };
    }
    private void Lift(Button button, bool active)
    {
        button.RenderTransformOrigin = new(.5, .5);
        // a button styled by the design system shares one frozen ScaleTransform from its style; animate a private copy
        var transform = button.RenderTransform is ScaleTransform own && !own.IsFrozen && !own.IsSealed ? own : new ScaleTransform(1, 1); button.RenderTransform = transform;
        double end = active ? Equals(button.Tag,"SetupLibraryCard") ? 1.02 : 1.08 : 1;
        if (animate) { transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(end, TimeSpan.FromMilliseconds(140))); transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(end, TimeSpan.FromMilliseconds(140))); }
        else { transform.ScaleX = end; transform.ScaleY = end; }
        button.Effect = active ? new DropShadowEffect { Color = ThemeCatalog.R(Color.FromRgb(78, 147, 255)), BlurRadius = 22, ShadowDepth = 5, Opacity = .75 } : null;
    }
    private void ShowDetachedFocus(FrameworkElement target)
    {
        HideDetachedFocus(); var layer = AdornerLayer.GetAdornerLayer(target); if (layer == null) return;
        Point center = target.TransformToAncestor(this).Transform(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        focusTarget = target; focusAdorner = new(target, lastFocusCenter.HasValue ? lastFocusCenter.Value - center : new Vector(), animate); layer.Add(focusAdorner); lastFocusCenter = center;
    }
    private void HideDetachedFocus()
    {
        if (focusTarget != null && focusAdorner != null) AdornerLayer.GetAdornerLayer(focusTarget)?.Remove(focusAdorner);
        focusTarget = null; focusAdorner = null;
    }
    private void SetNavigationPreview(bool value) => SetNavigationPreview(value, false);
    private void SetNavigationPreview(bool value, bool preserveExistingFocus)
    {
        navigationMode = value;
        StatusNote.Text = value ? "Navigation preview" : Linked ? (BluetoothOnly ? "Connected over Bluetooth" : "Connected") : "Controller offline";
        if (!value && Keyboard.FocusedElement is Button focused) Lift(focused, false);
        if (!value) { HideDetachedFocus(); lastFocusCenter = null; }
        else if (!preserveExistingFocus || Keyboard.FocusedElement == null || !IsKeyboardFocusWithin)
        {
            FocusSelectedControl();
        }
        foreach (var (key, button) in controls) button.Illuminate(key == SelectedControl, value);
    }
    private void MouseMode(object sender, MouseButtonEventArgs e) { SetNavigationPreview(false); }
    private void InspectAxis(object sender, RoutedEventArgs e) { string key=(string)((Button)sender).Tag;if(IsCurves)Curves.SelectChannel(key=="LSTICK_ANALOG"?CurveChannel.LeftStick:key=="RSTICK_ANALOG"?CurveChannel.RightStick:CurveChannel.LeftTrigger);else SelectControl(key); }
    private void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!updating && TargetChoice.SelectedValue is string target && Draft.CanEdit(SelectedControl)) SetDraftTarget(target); }
    private void Reset(object sender, RoutedEventArgs e) { Draft.Reset(SelectedControl); RefreshEditor(); }
    private void ResetAll(object sender, RoutedEventArgs e) { if (IsCurves) {Curves.ResetAll();} else {Draft.ResetAll(); RefreshEditor();} }
    private void ResetCurrentCurve(object sender,RoutedEventArgs e) {if(IsCurves)Curves.Reset();}
    private void MoreCurveActions(object sender,RoutedEventArgs e) {if(IsCurves && CurveMoreChoice.ContextMenu is {} menu) {menu.PlacementTarget=CurveMoreChoice;menu.IsOpen=true;}}
    private Diagnostics.DiagnosticsWorkbench? curvesDiagnostics;
    private Action? RefreshQuickStickControls;

    void BuildCurvesWorkspace()
    {
        CurvesPageHost.Children.Clear();
        CurvesPageHost.ColumnDefinitions.Clear();
        CurvesPageHost.ColumnDefinitions.Add(new() { Width = new(1.06, GridUnitType.Star) });
        CurvesPageHost.ColumnDefinitions.Add(new() { Width = new(14) });
        CurvesPageHost.ColumnDefinitions.Add(new() { Width = new(0.98, GridUnitType.Star) });

        // Left Column (Full Height): Curve Editor + Inline Deadzone Sliders
        var leftStack = new Grid();
        leftStack.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        leftStack.RowDefinitions.Add(new() { Height = GridLength.Auto });
        leftStack.Children.Add(Curves.Inspector);
        Grid.SetRow(Curves.Shelf, 1);
        leftStack.Children.Add(Curves.Shelf);
        var leftCard = Kit.Panel(null, null, null, leftStack, new Thickness(16, 12, 16, 12));
        CurvesPageHost.Children.Add(leftCard);

        // Right Column: Top = Live Analog Telemetry Instrument Wells, Bottom = Analog Quick Tuning + Analog Studio
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new() { Height = new(0.92, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new() { Height = new(10) });
        rightGrid.RowDefinitions.Add(new() { Height = new(1.08, GridUnitType.Star) });

        curvesDiagnostics = new Diagnostics.DiagnosticsWorkbench(Player, RegisterFocus, PcMode, compact: true, model: Model, telemetryOnly: true);
        var clearTraceBtn = new Button
        {
            Content = "Clear Trace",
            Style = DS.Style("DS.ConsoleBtn"),
            Height = 26, MinHeight = 0,
            Padding = new(10, 0, 10, 0),
            FontSize = 11.5,
        };
        clearTraceBtn.Click += (_, _) => curvesDiagnostics?.ClearMeasurements();
        var diagHeader = new DockPanel { Margin = new(0, 0, 0, 8) };
        DockPanel.SetDock(clearTraceBtn, Dock.Right);
        diagHeader.Children.Add(clearTraceBtn);
        diagHeader.Children.Add(Kit.Text("LIVE ANALOG TELEMETRY", 11.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        var diagDock = new DockPanel();
        DockPanel.SetDock(diagHeader, Dock.Top);
        diagDock.Children.Add(diagHeader);
        diagDock.Children.Add(curvesDiagnostics.TelemetryGrid);
        var diagCard = Kit.Panel(null, null, null, diagDock, new Thickness(14, 10, 14, 10));
        rightGrid.Children.Add(diagCard);

        var quickCard = BuildQuickStickTuningCard();
        Grid.SetRow(quickCard, 2);
        rightGrid.Children.Add(quickCard);

        Grid.SetColumn(rightGrid, 2);
        CurvesPageHost.Children.Add(rightGrid);
    }

    Border BuildQuickStickTuningCard()
    {
        bool syncingQuick = false;
        var linkSticks = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
        var autoDrift = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
        var rawBypass = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
        var invYQuick = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
        var swapQuick = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
        var circleBtn = new Button { Content = "Circular", Style = DS.Style("DS.ConsoleBtn"), Height = 28, MinHeight = 0, Padding = new(10, 0, 10, 0), FontSize = 11.5, Margin = new(0, 0, 3, 0) };
        var squareBtn = new Button { Content = "Square", Style = DS.Style("DS.ConsoleBtn"), Height = 28, MinHeight = 0, Padding = new(10, 0, 10, 0), FontSize = 11.5 };
        var radialDzBtn = new Button { Content = "Radial", Style = DS.Style("DS.ConsoleBtn"), Height = 28, MinHeight = 0, Padding = new(8, 0, 8, 0), FontSize = 11.5, Margin = new(0, 0, 3, 0) };
        var axialDzBtn = new Button { Content = "Axial", Style = DS.Style("DS.ConsoleBtn"), Height = 28, MinHeight = 0, Padding = new(8, 0, 8, 0), FontSize = 11.5, Margin = new(0, 0, 3, 0) };
        var hybridDzBtn = new Button { Content = "Hybrid", Style = DS.Style("DS.ConsoleBtn"), Height = 28, MinHeight = 0, Padding = new(8, 0, 8, 0), FontSize = 11.5 };
        var vertSlider = new Slider { Minimum = StickTuning.MinRatio, Maximum = StickTuning.MaxRatio, TickFrequency = 5, IsSnapToTickEnabled = true, Value = 100, Style = DS.Style("DS.Slider"), Height = 20 };
        var smoothSlider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = 0, Style = DS.Style("DS.Slider"), Height = 20 };
        var vertVal = Kit.Text("100%", 11.5, "DS.Text", FontWeights.SemiBold);
        var smoothVal = Kit.Text("0%", 11.5, "DS.Text", FontWeights.SemiBold);
        var quickNote = Kit.Text(Model.Id is "x20" or "x20_pro" && !Draft.PcMode ? "Native hardware curve · Click Apply to save on controller" : "Inline DSP tuning · Applies immediately when PC Mode is active", 11.5, "DS.TextMuted");

        string ActiveStickKey() => Curves.Channel == CurveChannel.RightStick ? "RightStick" : "LeftStick";

        void CommitProfileEdit(PcModeProfile p, string note)
        {
            p.Validate();
            var link = pcLink;
            if (Draft.PcMode && !p.IsNeutral && link != null && !PcModePage.MaySave(link.Preferences))
            {
                quickNote.Text = "Open PC Mode once to accept notice before applying PC-side changes.";
                return;
            }
            ApplyNativeAndBoost(p, saveImmediate: false);
            Curves.Refresh();
            quickNote.Text = Draft.PcMode
                ? (link?.Service.Enabled == true ? note + " · Active in PC Mode" : note + " · Saved (enable PC Mode to run live)")
                : note + (Curves.DesktopBoostActive ? " · Native HW + Desktop Boost active" : " · Compiled to native curve (press Apply to save)");
        }

        void EditProfile(Action<PcModeProfile> mutator, string note)
        {
            if (syncingQuick) return;
            try
            {
                var p = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : new PcModeProfile());
                mutator(p);
                CommitProfileEdit(p, note);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                quickNote.Text = ex.Message;
            }
        }

        void EditStick(Action<StickTuning> mutator, string note, bool allowRaw = false)
        {
            if (syncingQuick) return;
            try
            {
                var p = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : new PcModeProfile());
                var targets = linkSticks.IsChecked == true ? new[] { "LeftStick", "RightStick" } : new[] { ActiveStickKey() };
                foreach (var k in targets)
                {
                    var st = p.Sticks.TryGetValue(k, out var ex) ? ex : new StickTuning();
                    mutator(st);
                    if (!allowRaw) st.Raw = false;
                    if (st.IsDefault) p.Sticks.Remove(k); else p.Sticks[k] = st;
                }
                CommitProfileEdit(p, note);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                quickNote.Text = ex.Message;
            }
        }

        void PaintQuickState()
        {
            syncingQuick = true;
            var prof = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : null);
            var t = Curves.Tuning?.Invoke(Curves.Channel == CurveChannel.RightStick ? CurveChannel.RightStick : CurveChannel.LeftStick);
            bool sq = t?.SquareGate == true || (t?.Lab?.GateMorph ?? 0) >= 99;
            var shape = t?.Lab?.DeadzoneShape ?? DeadzoneShape.Radial;
            var activeBg = new SolidColorBrush(Color.FromRgb(42, 108, 212));
            var activeBorder = new SolidColorBrush(Color.FromRgb(111, 178, 255));
            var idleBg = new SolidColorBrush(Color.FromArgb(145, 14, 24, 48));
            var idleBorder = DS.Brush("DS.Hairline");
            circleBtn.Background = !sq ? activeBg : idleBg; circleBtn.BorderBrush = !sq ? activeBorder : idleBorder;
            squareBtn.Background = sq ? activeBg : idleBg; squareBtn.BorderBrush = sq ? activeBorder : idleBorder;
            radialDzBtn.Background = shape == DeadzoneShape.Radial ? activeBg : idleBg; radialDzBtn.BorderBrush = shape == DeadzoneShape.Radial ? activeBorder : idleBorder;
            axialDzBtn.Background = shape == DeadzoneShape.Axial ? activeBg : idleBg; axialDzBtn.BorderBrush = shape == DeadzoneShape.Axial ? activeBorder : idleBorder;
            hybridDzBtn.Background = shape == DeadzoneShape.Hybrid ? activeBg : idleBg; hybridDzBtn.BorderBrush = shape == DeadzoneShape.Hybrid ? activeBorder : idleBorder;
            autoDrift.IsChecked = t?.AutoCenterDrift == true;
            rawBypass.IsChecked = t?.Raw == true;
            invYQuick.IsChecked = t?.Lab?.InvertY == true;
            swapQuick.IsChecked = prof?.SwapSticks == true;
            vertSlider.Value = t?.AxisRatio ?? 100;
            vertVal.Text = $"{vertSlider.Value:0}%";
            smoothSlider.Value = t?.Lab?.Smoothing ?? 0;
            smoothVal.Text = $"{smoothSlider.Value:0}%";
            syncingQuick = false;
        }
        RefreshQuickStickControls = PaintQuickState;
        PaintQuickState();

        circleBtn.Click += (_, _) => { EditStick(s => { s.SquareGate = false; if (s.Lab != null) s.Lab.GateMorph = 0; }, "Circular gate selected"); PaintQuickState(); };
        squareBtn.Click += (_, _) => { EditStick(s => { s.SquareGate = true; (s.Lab ??= new StickLabSettings()).GateMorph = 100; }, "Square gate selected (faster diagonals)"); PaintQuickState(); };
        radialDzBtn.Click += (_, _) => { EditStick(s => (s.Lab ??= new StickLabSettings()).DeadzoneShape = DeadzoneShape.Radial, "Radial deadzone selected"); PaintQuickState(); };
        axialDzBtn.Click += (_, _) => { EditStick(s => (s.Lab ??= new StickLabSettings()).DeadzoneShape = DeadzoneShape.Axial, "Axial cross deadzone selected"); PaintQuickState(); };
        hybridDzBtn.Click += (_, _) => { EditStick(s => (s.Lab ??= new StickLabSettings()).DeadzoneShape = DeadzoneShape.Hybrid, "Hybrid deadzone selected"); PaintQuickState(); };
        autoDrift.Checked += (_, _) => EditStick(s => s.AutoCenterDrift = true, "Auto-fix resting drift enabled");
        autoDrift.Unchecked += (_, _) => EditStick(s => s.AutoCenterDrift = false, "Auto-fix resting drift turned off");
        rawBypass.Checked += (_, _) => EditStick(s => s.Raw = true, "Raw 1:1 bypass enabled", allowRaw: true);
        rawBypass.Unchecked += (_, _) => EditStick(s => s.Raw = false, "Raw 1:1 bypass turned off", allowRaw: true);
        invYQuick.Checked += (_, _) => EditStick(s => (s.Lab ??= new StickLabSettings()).InvertY = true, "Invert Y axis enabled");
        invYQuick.Unchecked += (_, _) => EditStick(s => (s.Lab ??= new StickLabSettings()).InvertY = false, "Invert Y axis disabled");
        swapQuick.Checked += (_, _) => EditProfile(p => p.SwapSticks = true, "Swap L & R sticks enabled");
        swapQuick.Unchecked += (_, _) => EditProfile(p => p.SwapSticks = false, "Swap L & R sticks disabled");
        vertSlider.ValueChanged += (_, _) => { vertVal.Text = $"{vertSlider.Value:0}%"; EditStick(s => s.AxisRatio = (int)Math.Round(vertSlider.Value), $"Vertical sensitivity set to {vertSlider.Value:0}%"); };
        smoothSlider.ValueChanged += (_, _) => { smoothVal.Text = $"{smoothSlider.Value:0}%"; EditStick(s => (s.Lab ??= new StickLabSettings()).Smoothing = Math.Round(smoothSlider.Value), $"Velocity smoothing set to {smoothSlider.Value:0}%"); };

        // Row 0: Gate + Deadzone shape side-by-side in crisp dark wells
        var segGrid = new Grid { Margin = new(0, 0, 0, 5) };
        segGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        segGrid.ColumnDefinitions.Add(new() { Width = new(8) });
        segGrid.ColumnDefinitions.Add(new() { Width = new(1.35, GridUnitType.Star) });
        var gateWell = new Border
        {
            CornerRadius = new(9), Background = new SolidColorBrush(Color.FromArgb(145, 7, 14, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 110, 165, 240)), BorderThickness = new(1), Padding = new(8, 4, 8, 4),
            Child = new DockPanel
            {
                Children =
                {
                    new UniformGrid { Columns = 2, Children = { circleBtn, squareBtn } }.Also(u => DockPanel.SetDock(u, Dock.Right)),
                    Kit.Text("Gate", 11.5, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center)
                }
            }
        };
        var shapeWell = new Border
        {
            CornerRadius = new(9), Background = new SolidColorBrush(Color.FromArgb(145, 7, 14, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 110, 165, 240)), BorderThickness = new(1), Padding = new(8, 4, 8, 4),
            Child = new DockPanel
            {
                Children =
                {
                    new UniformGrid { Columns = 3, Children = { radialDzBtn, axialDzBtn, hybridDzBtn } }.Also(u => DockPanel.SetDock(u, Dock.Right)),
                    Kit.Text("Shape", 11.5, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center)
                }
            }
        };
        Grid.SetColumn(shapeWell, 2);
        segGrid.Children.Add(gateWell);
        segGrid.Children.Add(shapeWell);

        // Row 1: Two side-by-side sliders (Vertical look speed & Smoothing)
        var slidersGrid = new Grid { Margin = new(0, 0, 0, 5) };
        slidersGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        slidersGrid.ColumnDefinitions.Add(new() { Width = new(10) });
        slidersGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        var vertWell = new StackPanel
        {
            Children =
            {
                new DockPanel { Children = { vertVal.Also(v => DockPanel.SetDock(v, Dock.Right)), Kit.Text("Vertical look speed", 11.5, "DS.TextSoft") } },
                vertSlider
            }
        };
        var smoothWell = new StackPanel
        {
            Children =
            {
                new DockPanel { Children = { smoothVal.Also(v => DockPanel.SetDock(v, Dock.Right)), Kit.Text("Velocity smoothing", 11.5, "DS.TextSoft") } },
                smoothSlider
            }
        };
        Grid.SetColumn(smoothWell, 2);
        slidersGrid.Children.Add(vertWell);
        slidersGrid.Children.Add(smoothWell);

        // Row 2: Compact 2-row hardware & quick toggle bar
        var toggleWell = new Border
        {
            CornerRadius = new(7), Background = new SolidColorBrush(Color.FromArgb(145, 7, 14, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 110, 165, 240)), BorderThickness = new(1), Padding = new(10, 4, 10, 4),
            Margin = new(0, 0, 0, 5),
            Child = new StackPanel
            {
                Children =
                {
                    new UniformGrid
                    {
                        Columns = 3,
                        Children =
                        {
                            new StackPanel { Orientation = Orientation.Horizontal, Children = { autoDrift, Kit.Text("Auto-fix drift", 11.5, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 0, 0); }) } },
                            new StackPanel { Orientation = Orientation.Horizontal, Children = { linkSticks, Kit.Text("Link L & R", 11.5, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 0, 0); }) } },
                            new StackPanel { Orientation = Orientation.Horizontal, Children = { rawBypass, Kit.Text("Raw 1:1 bypass", 11.5, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 0, 0); }) } },
                        }
                    },
                    new UniformGrid
                    {
                        Columns = 3,
                        Margin = new(0, 3, 0, 0),
                        Children =
                        {
                            new StackPanel { Orientation = Orientation.Horizontal, Children = { invYQuick, Kit.Text("Invert Y", 11.5, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 0, 0); }) } },
                            new StackPanel { Orientation = Orientation.Horizontal, Children = { swapQuick, Kit.Text("Swap L/R", 11.5, "DS.TextSoft").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 0, 0); }) } },
                            new Border(),
                        }
                    }
                }
            }
        };

        // Row 3: 4 structured Playstyle Preset Cards (2x2 grid with spec subtitles)
        var styleGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new(-2, 0, -2, 3) };
        Button StyleTile(string title, string specs, string preset, double inner, double outer, bool sq, bool raw)
        {
            var b = new Button
            {
                Style = DS.Style("DS.ConsoleBtn"),
                Height = 38, MinHeight = 0,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new(10, 3, 10, 3),
                Margin = new(2),
                Content = new StackPanel
                {
                    Children =
                    {
                        Kit.Text(title, 11.5, "DS.Text", FontWeights.SemiBold),
                        Kit.Text(specs, 10, "DS.TextMuted")
                    }
                }
            };
            b.Click += (_, _) =>
            {
                if (Curves.Draft.CanEdit) Curves.SetPreset(preset);
                EditStick(s => { s.Inner = inner; s.Outer = outer; s.SquareGate = sq; s.Raw = raw; }, title + " preset applied", allowRaw: raw);
                PaintQuickState();
            };
            RegisterFocus(b);
            return b;
        }
        styleGrid.Children.Add(StyleTile("FPS Precision", "Fine · 4% DZ · Circular", "fine", 4, 95, false, false));
        styleGrid.Children.Add(StyleTile("Rocket League", "Quick · 5% DZ · Square", "quick", 5, 85, true, false));
        styleGrid.Children.Add(StyleTile("Racing Smooth", "Smooth · 2% DZ · Circular", "smooth", 2, 100, false, false));
        styleGrid.Children.Add(StyleTile("Raw Linear 1:1", "Default · 0% DZ · Bypass", "default", 0, 100, false, true));

        // Row 4: Refined PS5-native Analog Studio launch strip (zero loud solid blue slop)
        var advBadge = new Border
        {
            CornerRadius = new(5),
            Background = new SolidColorBrush(Color.FromArgb(65, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(145, 56, 189, 248)),
            BorderThickness = new(1),
            Padding = new(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Kit.Text("Go Advanced  >", 11.5, "DS.AccentHi", FontWeights.Bold)
        };
        var advContent = new DockPanel();
        DockPanel.SetDock(advBadge, Dock.Right);
        advContent.Children.Add(advBadge);
        var accentStripe = new Border { Width = 3, CornerRadius = new(2), Background = DS.Brush("DS.AccentHi"), Margin = new(0, 2, 10, 2) };
        DockPanel.SetDock(accentStripe, Dock.Left);
        advContent.Children.Add(accentStripe);
        advContent.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Kit.Text("Analog Studio", 13, "DS.Text", FontWeights.Bold),
                Kit.Text("10-pt splines · Gate morph · Rapid-trigger · SOCD · X20C1 codes", 10, "DS.TextMuted").Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis)
            }
        });
        var goAdvancedBtn = new Button
        {
            Height = 42, MinHeight = 42,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new(0, 4, 0, 0),
            Padding = new(10, 4, 10, 4),
            Style = DS.Style("DS.ConsoleBtn"),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(215, 11, 22, 46))),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 56, 189, 248)),
            BorderThickness = new(1),
            ToolTip = "Open full-screen Analog Studio (10-point splines, deadzone shapes, tilt, smoothing, SOCD, rapid-trigger & live vector diagnostics)",
            Content = advContent
        };
        goAdvancedBtn.Click += (_, _) => OpenStickTools();
        RegisterFocus(goAdvancedBtn);

        var body = new DockPanel();
        DockPanel.SetDock(goAdvancedBtn, Dock.Bottom);
        body.Children.Add(goAdvancedBtn);
        body.Children.Add(new StackPanel
        {
            Children =
            {
                segGrid,
                slidersGrid,
                toggleWell,
                styleGrid,
                quickNote,
            }
        });
        return Kit.Panel("ANALOG QUICK TUNING", null, null, body, new Thickness(14, 10, 14, 10));
    }

    private readonly Dictionary<Button,Panel> curveToolbarParents=new();
    private void PlaceCurveToolbar(bool inEditor)
    {
        foreach(var button in new[]{ResetCurveChoice,CurveMoreChoice})
        {
            if(inEditor && button.Parent is Panel parent && !ReferenceEquals(parent,Curves.Toolbar)){curveToolbarParents[button]=parent;parent.Children.Remove(button);Curves.Toolbar.Children.Add(button);button.Margin=new(0,0,8,0);}
            else if(!inEditor && curveToolbarParents.TryGetValue(button,out var original)){Curves.Toolbar.Children.Remove(button);original.Children.Add(button);}
        }
        StickToolsChoice.Visibility = Visibility.Collapsed;
        TryChoice.Content = "Try it";
    }
    public void ShowCurves(bool value) => ShowCurves(value, restoreButtons: true);

    private void ShowCurves(bool value, bool restoreButtons)
    {
        // a page this model cannot use is refused before anything changes (it used to drop a Device or Setups
        // page onto Buttons with no tab lit, found by the stress run on 10 Oct 2026)
        if (value)
        {
            HideManagement(restoreButtons: false);
            if (IsVibration) ShowVibration(false, restoreButtons: false);
            if (IsMacros) ShowMacros(false, restoreButtons: false);
        }
        if (value == IsCurves) return;
        if (value) previousButtonControl = SelectedControl;
        HardwareSurface.Visibility = Visibility.Collapsed; IsCurves = value; ButtonsNav.IsChecked = !value; CurvesNav.IsChecked = value;
        if (value)
        {
            HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = Visibility.Collapsed;
            ShowLayer(CurvesPageHost, true);
            Grid.SetRowSpan(StudioBody, 2);
            curvesDiagnostics?.Start();
        }
        else
        {
            curvesDiagnostics?.Stop();
            ShowLayer(CurvesPageHost, false);
            if (restoreButtons)
            {
                Grid.SetRowSpan(StudioBody, 1);
                HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = Visibility.Visible;
            }
        }
        MappingHero.Visibility = (!value && restoreButtons) ? Visibility.Visible : Visibility.Collapsed; AssignmentOverview.Visibility = Visibility.Collapsed;
        CurveInspectorHost.Visibility = CurveShelfFrame.Visibility = Visibility.Collapsed;
        OverviewChoice.Visibility = ResetAllChoice.Visibility = (!value && restoreButtons) ? Visibility.Visible : Visibility.Collapsed;
        ResetCurveChoice.Visibility = CurveMoreChoice.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        StickToolsChoice.Visibility = Visibility.Collapsed; if (!value) CloseStickTools();
        HardwareShortcuts.Visibility = FrontChoice.Visibility = RearChoice.Visibility = (!value && restoreButtons) ? Visibility.Visible : Visibility.Collapsed;
        FrontChoice.IsEnabled = !value; RearChoice.IsEnabled = !value && HasRearArtwork;
        PlaceCurveToolbar(value);
        if (value)
        {
            RootLayout.RowDefinitions[4].Height = new(0);
            DraftBar.Visibility = Visibility.Collapsed; RootLayout.RowDefinitions[2].Height = new(0); Apply.Visibility = StatusNote.Visibility = Visibility.Collapsed;
            Curves.SelectChannel(Curves.Channel); RefreshQuickStickControls?.Invoke();
            UpdateButtonsPage();
        }
        else if (restoreButtons)
        {
            RootLayout.RowDefinitions[4].Height = new(184);
            if (previousButtonControl.Length > 0) SelectControl(previousButtonControl); else ClearSelection();
            RefreshEditor();
            UpdateButtonsPage();
        }
    }
    private void UpdateCurveContext()
    {
        if (!IsCurves) return;
        int count=Curves.Draft.ModifiedChannels;
        ChangeCount.Text = ControllerWrites ? "Curves · Apply to save on the controller" : count==0 ? "Curve preview · No local changes" : $"{count} curve channel{(count==1?"":"s")} modified · Local draft";
        DraftChip.Text = count==0 ? "No local changes" : $"{count} local change{(count==1?"":"s")}";
        PrimaryHint.Text = " Select / Adjust"; OverviewHint.Text = " Reset curve";
        ResetCurveChoice.IsEnabled=Curves.CanReset;
        ResetAllCurvesAction.IsEnabled=Curves.CanResetAll; Apply.IsEnabled=false; SyncPcMode();
        CanvasHint.Text=Curves.Channel is CurveChannel.LeftStick or CurveChannel.RightStick ? "STICK RESPONSE" : "TRIGGER TRAVEL";
        FrontChoice.IsEnabled=RearChoice.IsEnabled=false;
    }
    private void CurvesTab(object sender,RoutedEventArgs e)=>ShowCurves(true);
    /// <summary>PC mode's settings, set by the host: the Curves tab's Advanced stick tools write to them.</summary>
    public PcModeLink? PcMode { get => pcLink; set { pcLink = value; pcSync = value != null && Draft.PcMode ? new(value, Draft, Curves.Draft, macroDraft, vibrationDraft) : null; WireCurveTuning(); SyncPcMode(); RefreshBatteryVisibility(); } }
    /// <summary>The Curves graph reads and edits PC mode's stick settings (dead zone, full at, anti-dead zone, max output), so
    /// it draws exactly what games get, including what Agent XCTL changed.</summary>
    private System.Windows.Threading.DispatcherTimer? curveSaveDebounce;
    private PcModeProfile? pendingCurveSave;
    private PcModeProfile? liveNativeProfile;
    private bool nativeBoostActive;

    private void FlushPendingCurveSave()
    {
        if (curveSaveDebounce != null) curveSaveDebounce.Stop();
        if (pendingCurveSave is { } prof && pcLink is { } link)
        {
            pendingCurveSave = null;
            try
            {
                link.Store.Save(PcModePage.StoreKey, prof);
                link.Service.Reload();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal void ApplyNativeAndBoost(PcModeProfile p, bool saveImmediate = false)
    {
        liveNativeProfile = p;
        bool nativeHw = Model.Id is "x20" or "x20_pro" or "x10" or "d10" && !Draft.PcMode;
        bool boost = StickLabMath.RequiresDesktopBoost(p);
        Curves.NativeHardwareMode = nativeHw;
        Curves.DesktopBoostActive = boost;

        if (nativeHw)
        {
            foreach (var ch in Enum.GetValues<CurveChannel>())
            {
                bool isTrig = ch is CurveChannel.LeftTrigger or CurveChannel.RightTrigger;
                string key = isTrig ? (ch == CurveChannel.LeftTrigger ? "LT" : "RT") : ch.ToString();
                var st = !isTrig && p.Sticks.TryGetValue(key, out var stickVal) ? stickVal : null;
                var tt = isTrig && p.Triggers.TryGetValue(key, out var trigVal) ? trigVal : null;
                var compiled = StickLabMath.CompileToNativeHardware(Curves.Draft[ch], st, tt, p.SwapSticks, isTrig);
                if (compiled != Curves.Draft[ch])
                    Curves.Draft.Set(ch, compiled);
            }
            UpdateCurveContext();
        }

        if (pcLink is { } link)
        {
            if (saveImmediate)
            {
                pendingCurveSave = null;
                curveSaveDebounce?.Stop();
                link.Store.Save(PcModePage.StoreKey, p);
                link.Service.Reload();
            }
            else
            {
                pendingCurveSave = p;
                curveSaveDebounce ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) }.Also(t =>
                    t.Tick += (_, _) => FlushPendingCurveSave());
                curveSaveDebounce.Stop();
                curveSaveDebounce.Start();
            }

            if (nativeHw && !ReviewSandbox.Active)
            {
                if (boost && !link.Service.Enabled && PcModeSetup.BusInstalled && HidHide.Installed)
                {
                    nativeBoostActive = true;
                    link.Service.Enabled = true;
                }
                else if (!boost && nativeBoostActive)
                {
                    nativeBoostActive = false;
                    link.Service.Enabled = false;
                }
            }
        }
    }

    private void WireCurveTuning()
    {
        bool nativeEditable = Model.Id is "x20" or "x20_pro" or "x10" or "d10";
        if (pcLink is not { } link && !nativeEditable) { Curves.Tuning = null; Curves.SaveTuning = null; Curves.ResetResponse = null; Curves.Refresh(); return; }
        Curves.NativeHardwareMode = !Draft.PcMode && nativeEditable;
        Curves.ResetResponse = channels =>
        {
            try
            {
                var cur = pcLink?.Store.Get(PcModePage.StoreKey) ?? liveNativeProfile ?? new PcModeProfile();
                var next = X20Ctl.Zone.PcMode.PcModeDrafts.ResetCurves(cur, channels);
                ApplyNativeAndBoost(next, saveImmediate: true);
                pcSync?.Reload();
                return null;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { return "Couldn't reset curves: " + error.Message; }
        };
        Curves.Tuning = channel =>
        {
            var src = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : null);
            return src?.Sticks.GetValueOrDefault(channel.ToString());
        };
        Curves.SaveTuning = (channel, tuning) =>
        {
            var p = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : new PcModeProfile());
            if (tuning.IsDefault) p.Sticks.Remove(channel.ToString()); else p.Sticks[channel.ToString()] = tuning;
            try { p.Validate(); }
            catch (InvalidDataException error) { return "Not applied: " + error.Message; }
            if (Draft.PcMode && !p.IsNeutral && pcLink != null && !PcModePage.MaySave(pcLink.Preferences))
                return "Open the PC mode tab and read the notice to start applying this.";
            try
            {
                ApplyNativeAndBoost(p, saveImmediate: false);
                ChangeCount.Text = Draft.PcMode
                    ? "PC mode · Saved · Applies right away"
                    : "Native curves ready · Press Apply to controller";
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return "Couldn't save: " + error.Message; }
        };
        Curves.Refresh();
    }
    private PcModeLink? pcLink;
    /// <summary>Whether this controller gets the PC Mode entry (every pad except the X20 family).</summary>
    public bool PcModeEntryAvailable => X20Ctl.Zone.PcMode.PcModeEntry.IsAvailable(Model.Id);
    /// <summary>The PC Mode entry: always opens PC mode's own Status section (the switch and controller type),
    /// whether it is already on or the warning window is about to switch it on.</summary>
    public async void RequestPcMode()
    {
        if (pcLink == null || !PcModeEntryAvailable) return;
        PcModePage.Part = "Status";
        if (pcLink.Service.Enabled) { ShowManagement("PcMode"); return; }
        if (!PcModeGate.Ask(Window.GetWindow(this), Model.Name, out var padType)) return;
        PcModePage.ChoosePad(pcLink, padType);
        try
        {
            var why = await PcModePage.SwitchFromAgent(pcLink, true);
            if (why != null) MessageBox.Show(Window.GetWindow(this), why, "PC Mode"); else ShowManagement("PcMode");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "PC Mode");
        }
    }
    private PcModeSync? pcSync;
    internal bool NeedsPcModeRefresh => Model.Id is "x20_pro" or "x10" or "d10" && pcLink!=null && !nativeBoostActive && Draft.PcMode!=pcLink.Service.Enabled;
    internal void PcModeEnabledChanged()
    {
        RefreshBatteryVisibility();
        // Let the Agent finish its reply. Closing it picks up the new Studio mode.
        if (NeedsPcModeRefresh && !AgentOpen) PcModeSwitched?.Invoke();
        else ApplyPcDashboard();
    }
    private bool MouseCapturesInput => ManagementPage=="Mouse" && MouseService is {Active:true} mouse &&
        mouse.OwnerPlayer==Player && mouse.OwnerModel==Model.Id;
    internal void MouseModeChanged()
    {
        if (ManagementPage!="Mouse") return;
        NotifyInputOwner();QueueNavigation();
    }
    /// <summary>Reopen the X20 Pro Studio when its editor switches between native and PC Mode.</summary>
    public event Action? PcModeSwitched;
    /// <summary>Saves the tabs to PC mode when it is on, and says so in the status bar.</summary>
    private void SyncPcMode()
    {
        ApplyPcDashboard();
        if (pcSync == null || ManagementPage != null) { UpdatePcApply(); return; }
        // changes wait for Apply (owner, 6 Nov 2026: nothing may apply by itself)
        ChangeCount.Text = pcSync.HasPending ? "PC mode · Changes not applied yet. Press Apply to PC mode." : pcSync.Status; DraftChip.Text = "PC mode";
        UpdatePcApply();
    }
    private ScrollViewer? cachedPcModeView;
    private System.Windows.Controls.Primitives.ToggleButton? cachedPcModeSwitch;
    private Button? pcCurveApply;
    private void ApplyPcClick(object sender, RoutedEventArgs e)
    {
        if (pcSync == null) return;
        pcSync.Save(); ChangeCount.Text = pcSync.Status; UpdatePcApply();
    }
    /// <summary>The Apply buttons for PC mode: the bar's, and the Curves toolbar's. Lit only while there is something to apply.</summary>
    private void UpdatePcApply()
    {
        if (pcCurveApply == null)
        {
            pcCurveApply = new Button { Content = "Apply to PC mode", Style = DS.Style("DS.ConsoleBtn"), Height = 44, MinHeight = 0, Padding = new(18, 0, 20, 0), Margin = new(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            pcCurveApply.Click += ApplyPcClick; RegisterFocus(pcCurveApply); Curves.Toolbar.Children.Add(pcCurveApply);
            Apply.Click -= ApplyPcClick; Apply.Click += ApplyPcClick;
        }
        bool pending = pcSync?.HasPending == true;
        pcCurveApply.Visibility = pcSync != null ? Visibility.Visible : Visibility.Collapsed; pcCurveApply.IsEnabled = pending;
        if (pcSync != null) { Apply.Content = "Apply to PC mode"; Apply.IsEnabled = pending; Apply.ToolTip = pending ? "Save these changes to PC mode" : "Nothing to apply yet. Change a setting first."; }
    }
    private void PcProfileChanged()
    {
        if (pcSync == null) return;
        pcSync.Reload(); Curves.Refresh(); Macros?.Refresh(); RefreshEditor(); UpdateCurveContext(); UpdateMacroContext();
    }
    private void PcNoticeAccepted() => SyncPcMode();
    private void PcModeTab(object sender, RoutedEventArgs e) => ShowManagement("PcMode");
    public StickLabZone? StickLab { get; private set; }
    private StickLabZone? stickLabLayer;
    private StickLabZone? cachedStickLab;
    private bool cachedStickLabNativeHw;
    private readonly TranslateTransform rootSlideTransform = new();
    private readonly TranslateTransform labSlideTransform = new();
    private void OpenStickTools(object sender, RoutedEventArgs e) => OpenStickTools();

    /// <summary>Full-screen PS5 slide-down Analog Studio zone: hides tabs and Studio chrome cleanly until Return or Esc.</summary>
    public void OpenStickTools()
    {
        CancelRemapCapture();
        if (stickLabLayer != null) return;
        FlushPendingCurveSave();
        curvesDiagnostics?.Stop();
        bool nativeHw = !Draft.PcMode && Model.Id is "x20" or "x20_pro" or "x10" or "d10";
        if (cachedStickLab != null && cachedStickLabNativeHw == nativeHw)
        {
            var curProfile = liveNativeProfile ?? (pcLink != null ? pcLink.Store.Get(PcModePage.StoreKey) : new PcModeProfile());
            cachedStickLab.ResumeFromProfile(curProfile);
            StickLab = cachedStickLab;
        }
        else
        {
            cachedStickLab = new StickLabZone(Model, Player, PcMode, CloseStickTools, RequestPcMode, code =>
            {
                CloseStickTools();
                ShowManagement("Community");
                var exp = ExportCurrentForCommunity("curve", Draft.PcMode ? "pc-mode" : "native");
                Community?.OpenShareDialog("curve", "Custom Analog Curve", exp.Payload, code);
            }, nativeHardware: nativeHw, onProfileUpdated: p => ApplyNativeAndBoost(p, saveImmediate: false));
            cachedStickLabNativeHw = nativeHw;
            StickLab = cachedStickLab;
            StickLabOverlayHost.Children.Clear();
            StickLabOverlayHost.Children.Add(StickLab);
        }
        stickLabLayer = StickLab;
        StickLabOverlayHost.Visibility = Visibility.Visible;
        RootLayout.Visibility = Visibility.Collapsed;
        StickLab.ExitButton.Focus();
    }

    public void CloseStickTools()
    {
        if (stickLabLayer == null) return;
        stickLabLayer.FlushPendingSave();
        stickLabLayer.SuspendLiveTimer();
        FlushPendingCurveSave();
        stickLabLayer = null;
        StickLab = null;
        StickLabOverlayHost.Visibility = Visibility.Collapsed;
        RootLayout.Visibility = Visibility.Visible;
        RootLayout.Opacity = 1;
        rootSlideTransform.Y = 0;
        labSlideTransform.Y = 0;
        Curves.Refresh();
        RefreshQuickStickControls?.Invoke();
        if (IsCurves) curvesDiagnostics?.Start();
    }
    private void ButtonsTab(object sender,RoutedEventArgs e) => ShowButtons();
    /// <summary>Open the Buttons page from anywhere (tab, Dashboard hero, review harnesses).</summary>
    // The native macro Body can remain hidden behind a custom themed editor.
    // Studio lifecycle must therefore stop capture independently of Body visibility.
    internal void SuspendMacroActivity()
    {
        if (Macros is { IsRecording: true } recording) recording.CancelRecording();
        Macros?.StopPreview();
    }
    public void ShowButtons()
    {
        ShowCurves(false, restoreButtons: false);
        if (IsMacros) ShowMacros(false, restoreButtons: false);
        if (IsVibration) ShowVibration(false, restoreButtons: false);
        HideManagement(restoreButtons: false);
        ButtonsNav.IsChecked = true;
        FrontChoice.IsEnabled = true; RearChoice.IsEnabled = HasRearArtwork;
        DraftBar.Visibility = Visibility.Visible; RootLayout.RowDefinitions[2].Height = new(40);
        Apply.Visibility = ChangeCount.Visibility = Visibility.Visible;
        if (buttonsPage == null)
        {
            RootLayout.RowDefinitions[4].Height = new(184);
            Grid.SetRowSpan(StudioBody, 1);
            HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Visible;
            MappingHero.Visibility = HardwareShortcuts.Visibility = FrontChoice.Visibility = RearChoice.Visibility = Visibility.Visible;
        }
        else
        {
            RootLayout.RowDefinitions[4].Height = new(0);
            OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Collapsed;
        }
        RefreshEditor();
        UpdateButtonsPage();
    }
    public DashboardConsolePage? Dashboard {get;private set;}
    private DashboardConsolePage CreateDashboard(){var page=new DashboardConsolePage(Model,Player,Draft.UnsentChanges,setupStore.Items.Count(s=>s.ModelId==Model.Id),!animate,setupStore.Items.Where(s=>s.ModelId==Model.Id).ToArray(),id=>LoadSetup(setupStore.NativeSnapshot(id)));if(currentTransport!=null)page.SetTransport(currentTransport);if(currentFirmware!=null)page.SetFirmware(currentFirmware);page.SetBatteryLevel(currentBatteryLevel,currentTransport);page.ShowBattery(BatteryVisible);if(gameplayConnected!=null||BluetoothLive)page.SetInputConnected(Linked,BluetoothOnly);page.TelemetryUpdated+=ApplyTelemetry;page.ConfigureRequested+=ShowButtons;page.OpenCommunityRequested+=()=>ShowManagement("Community");page.MarkNotificationsReadRequested+=async()=>{await EnsureCommunityClient().MarkNotificationsReadAsync();await RefreshPulseAsync();};page.PageRequested+=target=>{if(target=="Buttons")ShowButtons();else if(target=="Curves")ShowCurves(true);else if(target=="Macros")ShowMacros(true);else ShowManagement(target);};_ = RefreshPulseAsync();return page;}
    private void DashboardTab(object sender,RoutedEventArgs e)=>ShowManagement("Dashboard");
    public void ShowMacros(bool value) => ShowMacros(value, restoreButtons: true);

    private void ShowMacros(bool value, bool restoreButtons)
    {
        if (value && PcDashboard && KeyboardNav != null) { ShowManagement("Keyboard"); return; } // PC mode: macros live in the Keyboard tab (owner, 7 Nov 2026)
        if (value)
        {
            HideManagement(restoreButtons: false);
            if (IsVibration) ShowVibration(false, restoreButtons: false);
            if (IsCurves) ShowCurves(false, restoreButtons: false);
        }
        if (value == IsMacros) return;
        IsMacros = value; MacrosNav.IsChecked = value; ButtonsNav.IsChecked = !value; CurvesNav.IsChecked = false;
        ShowLayer(MacroBodyHost, value); MacroDockFrame.Visibility = Visibility.Collapsed;
        ResetCurveChoice.Visibility = CurveMoreChoice.Visibility = Visibility.Collapsed; CurveInspectorHost.Visibility = CurveShelfFrame.Visibility = Visibility.Collapsed;
        if (value)
        {
            HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Collapsed;
            Grid.SetRowSpan(StudioBody, 2);
            RootLayout.RowDefinitions[4].Height = new(0);
            DraftBar.Visibility = Visibility.Visible; RootLayout.RowDefinitions[2].Height = new(40); Apply.Visibility = ChangeCount.Visibility = Visibility.Visible;
            if (Macros == null) { Macros = new(macroDraft, layout, this, RegisterFocus, !animate, Model.Id); ModelProfiles.Wear(Macros.Body, Model.Id, "Macros", Draft.PcMode); MacroBodyHost.Children.Add(Macros.Body); Macros.Changed += UpdateMacroContext; Macros.RecordingChanged += NotifyInputOwner; Macros.ShareCommunityRequested += () => { ShowManagement("Community"); var exp = ExportCurrentForCommunity("macro", Draft.PcMode ? "pc-mode" : "native"); Community?.OpenShareDialog("macro", "Macro Profile", exp.Payload); }; UseControllerWrites(Macros); }
            UpdateMacroContext();
            UpdateButtonsPage();
        }
        else
        {
            if (Macros?.IsRecording == true) Macros.CancelRecording();
            Macros?.StopPreview();
            if (restoreButtons)
            {
                RootLayout.RowDefinitions[4].Height = new(184);
                Grid.SetRowSpan(StudioBody, 1);
                HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Visible;
                RefreshEditor();
                UpdateButtonsPage();
            }
        }
    }
    private void UpdateMacroContext()
    {
        if(!IsMacros || Macros==null)return;
        ChangeCount.Text=$"{macroDraft.ModifiedSlots} local sequence{(macroDraft.ModifiedSlots==1?"":"s")} · Hardware contents unknown";
        DraftChip.Text="Local macro drafts";PrimaryHint.Text=" Select / Edit";OverviewHint.Text=" Add event";Apply.IsEnabled=false;SyncPcMode();
    }
    private void MacrosTab(object sender,RoutedEventArgs e)=>ShowMacros(true);
    public void ShowVibration(bool value) => ShowVibration(value, restoreButtons: true);

    private void ShowVibration(bool value, bool restoreButtons)
    {
        if (value)
        {
            HideManagement(restoreButtons: false);
            if (IsCurves) ShowCurves(false, restoreButtons: false);
            if (IsMacros) ShowMacros(false, restoreButtons: false);
        }
        if (value == IsVibration) return;
        IsVibration = value; VibrationNav.IsChecked = value; ButtonsNav.IsChecked = !value; CurvesNav.IsChecked = MacrosNav.IsChecked = false;
        ShowLayer(VibrationBodyHost, value);
        VibrationShelfFrame.Visibility = VibrationResetChoice.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        ResetCurveChoice.Visibility = CurveMoreChoice.Visibility = Visibility.Collapsed;
        if (value)
        {
            HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Collapsed;
            RootLayout.RowDefinitions[4].Height = new(54);
            DraftBar.Visibility = Visibility.Collapsed; RootLayout.RowDefinitions[2].Height = new(0); Apply.Visibility = StatusNote.Visibility = Visibility.Collapsed;
            if (Vibration == null) { Vibration = new(vibrationDraft, this, RegisterFocus, animate, Model.Id); ModelProfiles.Wear(Vibration.Body, Model.Id, "Vibration", Draft.PcMode); VibrationBodyHost.Children.Add(Vibration.Body); VibrationShelfHost.Children.Add(Vibration.Shelf); Vibration.Changed += UpdateVibrationContext; Vibration.ShareCommunityRequested += () => { ShowManagement("Community"); var exp = ExportCurrentForCommunity("vibration", Draft.PcMode ? "pc-mode" : "native"); Community?.OpenShareDialog("vibration", "Vibration Preset", exp.Payload); }; UseControllerWrites(Vibration); }
            UpdateVibrationContext();
            UpdateButtonsPage();
        }
        else
        {
            Vibration?.StopPattern();
            if (restoreButtons)
            {
                RootLayout.RowDefinitions[4].Height = new(184);
                HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Visible;
                RefreshEditor();
                UpdateButtonsPage();
            }
        }
    }
    private void UpdateVibrationContext(){if(!IsVibration)return;ChangeCount.Text="Vibration · Test on the controller";DraftChip.Text="Vibration";PrimaryHint.Text=" Adjust strength";OverviewHint.Text=" Reset draft";VibrationResetChoice.IsEnabled=vibrationDraft.Modified;Apply.IsEnabled=false;
        // PC mode (owner, 7 Nov 2026): the strength is how much of a game's vibration reaches the pad, saved with Apply like the other tabs
        if(pcSync!=null){DraftBar.Visibility=Apply.Visibility=StatusNote.Visibility=Visibility.Visible;RootLayout.RowDefinitions[2].Height=new(40);ChangeCount.Text=pcSync.HasPending?"PC mode · Vibration changed. Press Apply to PC mode.":$"PC mode · Games vibrate at {vibrationDraft.Strength}%";UpdatePcApply();}
    }
    private void VibrationTab(object sender,RoutedEventArgs e)=>ShowVibration(true);
    private void ResetVibration(object sender,RoutedEventArgs e)=>Vibration?.Reset();
    /// <summary>PC Mode is on for a pad that has the PC Mode entry: its dashboard has no Device or Diagnostics pages (owner, 6 Nov 2026).</summary>
    // owner, 7 Nov 2026: PC mode is how these controllers are set up. The X20 Pro (lab, 8 Nov) shows PC mode's tabs only while
    // PC mode is on; switched off, it is the X20 Pro Studio of the release, saving to the controller itself.
    private bool PcDashboard => PcModeEntryAvailable && pcLink != null && (Model.Id is not ("x20_pro" or "x10" or "d10") || pcLink.Service.Enabled);
    private void ApplyPcDashboard()
    {
        bool on = PcDashboard;
        // X20 Pro, X10, and D10 keep their native tabs and Device tab until PC mode is switched on.
        bool native = Model.Id is "x20_pro" or "x10" or "d10", swap = on && (!native || Draft.PcMode), noDevice = on && !native;
        DiagnosticsNav.Visibility = Visibility.Collapsed; PcModeNav.Visibility = Visibility.Collapsed;
        if (baseNavigation.Length == 0) return; // the strip is not built yet; it calls this again
        var list = new List<RadioButton>();
        foreach (var t in baseNavigation)
        {
            if (t == PcModeNav) { if (on) list.AddRange(pcTabs); }
            else if (t == X10NativeNav && (Model.Id != "x10" || Draft.PcMode)) continue;
            else if (t == D10NativeNav && (Model.Id != "d10" || Draft.PcMode)) continue;
            else if (t == DeviceNav && noDevice) continue;
            else if (t == MouseNav) continue; // placed below
            // owner, 7 Nov 2026: with PC mode on, Keyboard takes Macros' place and Mouse sits beside it
            else if (t == MacrosNav && swap && KeyboardNav != null) { list.Add(KeyboardNav); list.Add(MouseNav); }
            else list.Add(t);
        }
        if (!list.Contains(MouseNav) && baseNavigation.Contains(MouseNav)) list.Add(MouseNav); // PC mode off: Mouse on the right
        navigation = list.ToArray();
        // Until the strip is measured, the mode's whole order is the order. UpdateNavigation narrows it to what fits.
        visibleNavigation = navigation;
        // Hide what this mode does not own. Tabs it does own keep the visibility UpdateNavigation gave them,
        // so a tab collapsed into More is not flashed back onto the strip on every page change.
        if (!on) foreach (var t in pcTabs) t.Visibility = Visibility.Collapsed;
        if (Model.Id != "x10" || Draft.PcMode) X10NativeNav.Visibility = Visibility.Collapsed;
        if (Model.Id != "d10" || Draft.PcMode) D10NativeNav.Visibility = Visibility.Collapsed;
        if (noDevice) DeviceNav.Visibility = Visibility.Collapsed; // the strip only ever shows tabs, so hide it here
        if (swap && KeyboardNav != null) MacrosNav.Visibility = Visibility.Collapsed;
        if (KeyboardNav != null && !swap) KeyboardNav.Visibility = Visibility.Collapsed;
        // the strip draws its tabs in child order: put them in this order
        int at = 0;
        foreach (var t in list)
        {
            int now = NavigationTabs.Children.IndexOf(t); if (now < 0) continue;
            if (now != at) { NavigationTabs.Children.RemoveAt(now); NavigationTabs.Children.Insert(at, t); }
            at++;
        }
        QueueNavigation();
    }
    private UIElement? cachedMgmtFallbackPc;
    private UIElement? cachedMgmtFallbackMouse;

    private void ShowMgmtPanel(UIElement target)
    {
        if (!ManagementBodyHost.Children.Contains(target))
            ManagementBodyHost.Children.Add(target);
        foreach (UIElement child in ManagementBodyHost.Children)
            ShowLayer(child, ReferenceEquals(child, target));
    }

    public int RgbSelectedColor => rgbWindow?.SelectedRgb ?? 0;
    public int VibrationStrength => vibrationDraft.Strength;
    public MacroDraft MacroDraft => macroDraft;
    public SetupStore SetupStore => setupStore;

    internal LightingWindow EnsureRgbWorkbench()
    {
        if (Model.Id != "x20_pro") throw new InvalidOperationException("RGB configuration is verified for X20 Pro only.");
        if (rgbBody == null || rgbWindow == null)
        {
            rgbWindow = new LightingWindow(RegisterFocus, animate);
            rgbWindow.ShareCommunityRequested += () =>
            {
                ShowManagement("Community");
                var exp = ExportCurrentForCommunity("rgb", Draft.PcMode ? "pc-mode" : "native");
                Community?.OpenShareDialog("rgb", "RGB Lighting Profile", exp.Payload);
            };
            rgbBody = rgbWindow.DetachBody();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => rgbWindow?.ReadOnOpen());
        }
        return rgbWindow;
    }

    internal ScreenWorkbench EnsureScreenWorkbench()
    {
        if (!ModelProfiles.For(Model.Id).HasScreen) throw new InvalidOperationException("This model has no Screen artwork feature.");
        if (Screen == null)
        {
            Screen = new(this, RegisterFocus, () => ShowManagement("Device"));
            Screen.BrowseCommunityRequested += () => { ShowManagement("Community"); Community?.SetPresetCategoryAndRefresh("screen"); };
            Screen.ShareCommunityRequested += () =>
            {
                var exp = ExportCurrentForCommunity("screen", Draft.PcMode ? "pc-mode" : "native");
                ShowManagement("Community");
                Community?.OpenShareDialog("screen", "Custom Screen Art", exp.Payload, null, exp.MediaBytes, exp.MediaType);
            };
        }
        return Screen;
    }

    public void ShowManagement(string page)
    {
        if (PcDashboard && Model.Id is not ("x20_pro" or "x10" or "d10") && page is "Device" or "Diagnostics") page = "PcMode";
        ApplyPcDashboard();
        if (page is not ("Dashboard" or "PcMode" or "Device" or "X10Native" or "D10Native" or "RGB" or "Screen" or "Diagnostics" or "Mouse" or "Keyboard" or "Tester" or "Setups" or "Community")) throw new ArgumentException("Unknown Studio page.");
        if (IsCurves) ShowCurves(false, restoreButtons: false);
        if (IsMacros) ShowMacros(false, restoreButtons: false);
        if (IsVibration) ShowVibration(false, restoreButtons: false);
        if (ManagementPage != null && ManagementPage != page)
        {
            if (ManagementPage == "Screen") Screen?.StopPreview();
            if (ManagementPage == "X10Native") X10Hardware?.StopActiveCalibration();
            if (ManagementPage == "Diagnostics") Diagnostics?.Stop();
            if (ManagementPage == "Tester") { navigationMode = navigationBeforeTester; Tester?.ExitCapture(); }
            ManagementShelfHost.Children.Clear();
        }
        ManagementPage = page;
        bool immediate = page is "Dashboard" or "Mouse" or "Keyboard" or "Diagnostics" or "Device" or "X10Native" or "D10Native" or "RGB" or "Screen" or "PcMode" or "Community";
        DraftBar.Visibility = immediate ? Visibility.Collapsed : Visibility.Visible; RootLayout.RowDefinitions[2].Height = new(immediate ? 0 : 40);
        Apply.Visibility = StatusNote.Visibility = immediate ? Visibility.Collapsed : Visibility.Visible;
        ChangeCount.Visibility = page == "Dashboard" ? Visibility.Collapsed : Visibility.Visible;
        HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Collapsed;
        ShowLayer(ManagementBodyHost, true);
        ManagementShelfFrame.Visibility = Visibility.Visible;
        ButtonsNav.IsChecked = CurvesNav.IsChecked = MacrosNav.IsChecked = VibrationNav.IsChecked = false;
        DeviceNav.IsChecked = page == "Device"; X10NativeNav.IsChecked = page == "X10Native"; D10NativeNav.IsChecked = page == "D10Native"; RgbNav.IsChecked = page == "RGB"; ScreenNav.IsChecked = page == "Screen";
        DiagnosticsNav.IsChecked = page == "Diagnostics"; MouseNav.IsChecked = page == "Mouse";
        if (KeyboardNav != null) KeyboardNav.IsChecked = page == "Keyboard";
        PcModeNav.IsChecked = false;
        foreach (var t in pcTabs) t.IsChecked = page == "PcMode" && (string)t.Content == PcModePage.Part;
        TesterNav.IsChecked = page == "Tester"; SetupsNav.IsChecked = page == "Setups";
        CommunityNav.IsChecked = page == "Community"; DashboardNav.IsChecked = page == "Dashboard";
        DashboardFlip.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Hidden;
        if (page == "Dashboard") { Dashboard ??= CreateDashboard(); Dashboard.SetWorkingState(Draft.UnsentChanges, activeSetup == null ? null : setupStore.Items.FirstOrDefault(x => x.Id == activeSetup)?.Name, Draft.PcMode); ShowMgmtPanel(Dashboard); ChangeCount.Text = "Select the controller to configure it · Your games are found automatically"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "Dashboard"; Dashboard.FocusHero(); }
        else if (page == "Device") { Device ??= new(Model, Player, this, RegisterFocus, () => ShowManagement("Diagnostics"), () => ShowManagement("Setups"), () => ShowManagement("Screen"), Model.Id is "x20_pro" or "x10" && !Draft.PcMode ? FactoryReset : null, Model.Id == "x10" && !Draft.PcMode ? () => ShowManagement("X10Native") : null, Model.Id == "d10" && !Draft.PcMode ? () => ShowManagement("D10Native") : null); Device.TelemetryUpdated -= ApplyTelemetry; Device.TelemetryUpdated += ApplyTelemetry; if (currentTransport != null) Device.SetTransport(currentTransport); Device.SetBatteryLevel(currentBatteryLevel, currentTransport); Device.ShowBattery(BatteryVisible); if (currentFirmware != null) Device.SetFirmware(currentFirmware); else if (currentTransport == "2.4 GHz receiver") Device.SetFirmware("Not readable over XInput"); if (gameplayConnected != null || BluetoothLive) Device.SetConnection(Linked, BluetoothOnly); if (currentHardwareId != null) Device.SetHardwareId(currentHardwareId); Device.UpdateEngineStatus(NativeEngineStatus); ShowMgmtPanel(Device.Body); ManagementShelfHost.Children.Add(Device.Shelf); ChangeCount.Text = "Assigned model · Gameplay and configuration remain separate"; PrimaryHint.Text = " Open"; OverviewHint.Text = " My setups"; DraftChip.Text = "Controller overview"; }
        else if (page == "X10Native") { var x10w = EnsureX10Workbench(); ShowMgmtPanel(x10w.Body); ChangeCount.Text = "X10 Native hardware configuration"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "X10 Native"; }
        else if (page == "D10Native") { var d10w = EnsureD10Workbench(); ShowMgmtPanel(d10w.Body); ChangeCount.Text = "D10 Direct USB and 6-axis motion configuration"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "D10 Motion"; }
        else if (page == "RGB") { EnsureRgbWorkbench(); ShowMgmtPanel(rgbBody!); ChangeCount.Text = "RGB lighting"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "RGB"; }
        else if (page == "Screen") { var scr = EnsureScreenWorkbench(); ShowMgmtPanel(scr.Body); ChangeCount.Text = "Screen artwork · local preview"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "Screen"; }
        else if (page == "Diagnostics") { Diagnostics ??= new(Player, RegisterFocus, PcMode, model: Model); ShowMgmtPanel(Diagnostics.Body); Diagnostics.Start(); ChangeCount.Text = "Read-only input capture · Use Esc or the mouse to leave"; PrimaryHint.Text = ""; OverviewHint.Text = ""; DraftChip.Text = "Diagnostics"; Diagnostics.Reset.Focus(); }
        else if (page == "PcMode")
        {
            if (PcMode == null) { cachedMgmtFallbackPc ??= Kit.Text("PC mode isn't available in this window.", 16, "DS.TextSoft"); ShowMgmtPanel(cachedMgmtFallbackPc); }
            else
            {
                if (cachedPcModeView == null) { var back = new Button { Style = DS.Style("DS.ConsoleBtn"), Height = 46, MinHeight = 0, Padding = new(22, 0, 24, 0), Content = Kit.Text("Back to Dashboard", 15.5, "DS.Text", FontWeights.SemiBold) }; back.Click += (_, _) => ShowManagement("Dashboard"); RegisterFocus(back); var pc = new PcModePage(PcMode.Service, PcMode.Store, PcMode.Preferences, back, ShowButtons) { Margin = new(30, 6, 30, 24) }; cachedPcModeView = new ScrollViewer { Content = pc, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; cachedPcModeSwitch = pc.Switch; }
                ShowMgmtPanel(cachedPcModeView); cachedPcModeSwitch?.Focus();
            }
            ChangeCount.Text = pcLink?.Service.Enabled == true ? "PC mode is on · Buttons, Curves, Keyboard and Mouse apply on this PC" : "PC mode is off · Switch it on so games get this controller's settings"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "PC mode";
        }
        else if (page == "Mouse") { if (MouseService == null) { cachedMgmtFallbackMouse ??= Kit.Text("Mouse mode isn't available in this window.", 16, "DS.TextSoft"); ShowMgmtPanel(cachedMgmtFallbackMouse); } else { Mouse ??= new(MouseService, RegisterFocus, Player, Model, layout); ShowMgmtPanel(Mouse.Body); Mouse.Switch.Focus(); } ChangeCount.Text = "Controller as mouse · Saved on this PC"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "Mouse"; }
        else if (page == "Keyboard") ShowKeyboardPage();
        else if (page == "Tester") { Tester ??= new(Model, layout, this, RegisterFocus, () => ShowManagement("Device"), animate); Tester.Enter(); ShowMgmtPanel(Tester.Body); ManagementShelfHost.Children.Add(Tester.Shelf); ChangeCount.Text = "Tester owns gameplay input · UI input navigation paused"; PrimaryHint.Text = " Exit Tester"; OverviewHint.Text = ""; DraftChip.Text = "Gameplay tester"; navigationBeforeTester = navigationMode; navigationMode = true; Tester.Exit.Focus(); }
        else if (page == "Community") { var comm = EnsureCommunityWorkbench(); ShowMgmtPanel(comm.Body); ChangeCount.Text = "X20CTL Community Hub"; PrimaryHint.Text = " Select"; OverviewHint.Text = ""; DraftChip.Text = "Community"; }
        else { if (Setups == null) { Setups = new(setupStore, new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/catalog.json"))), Model.Id, this, RegisterFocus, () => SetupSnapshot.Capture(Model.Id, Draft, Curves.Draft, macroDraft, vibrationDraft), LoadSetup, legacySources, activeSetup); Setups.Changed += () => { if (Dashboard != null) ManagementBodyHost.Children.Remove(Dashboard); Dashboard = null; UpdateSetupContext(); }; Setups.BrowseCommunitySetupsRequested += () => { ShowManagement("Community"); Community?.SetPresetCategoryAndRefresh("setup"); }; Setups.ShareSetupRequested += saved => { ShowManagement("Community"); Community?.OpenShareDialog("setup", saved.Name, saved.Snapshot); }; } Setups.RefreshCurrent(); ShowMgmtPanel(Setups.Body); ManagementShelfHost.Children.Add(Setups.Shelf); UpdateSetupContext(); }
        PrimaryGlyph.Text = page == "Tester" ? "↵" : "A"; BackGlyph.Text = page is "Tester" or "Diagnostics" ? "Esc" : "B"; SecondaryFooterHint.Visibility = ActualWidth < 1100 || page is "Tester" or "Diagnostics" ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRowSpan(StudioBody, page is "Setups" or "Dashboard" or "Diagnostics" or "Mouse" or "Keyboard" or "PcMode" or "Device" or "X10Native" or "D10Native" or "RGB" or "Screen" or "Community" ? 2 : 1);
        if (page is "Setups" or "Dashboard" or "Diagnostics" or "Mouse" or "Keyboard" or "PcMode" or "Device" or "X10Native" or "D10Native" or "RGB" or "Screen" or "Community") ManagementShelfFrame.Visibility = Visibility.Collapsed;
        InputContextChanged?.Invoke(); Apply.IsEnabled = false;
        UpdateButtonsPage();
    }
    private void DashboardFlipClick(object sender,RoutedEventArgs e){Dashboard?.FlipController();DashboardFlip.ToolTip=Dashboard?.IsRear==true?"Show the front of this controller":"Show the back of this controller";}
    private void HideManagement() => HideManagement(restoreButtons: true);

    private void HideManagement(bool restoreButtons)
    {
        Screen?.StopPreview(); DashboardFlip.Visibility = Visibility.Hidden;
        StopTry();
        if (ManagementPage == null) return;
        if (ManagementPage == "Diagnostics") Diagnostics?.Stop();
        if (ManagementPage == "Tester") navigationMode = navigationBeforeTester;
        Tester?.ExitCapture(); ManagementPage = null; InputContextChanged?.Invoke();
        PrimaryGlyph.Text = "A"; BackGlyph.Text = "B"; SecondaryFooterHint.Visibility = ActualWidth < 1100 ? Visibility.Collapsed : Visibility.Visible;
        ManagementShelfHost.Children.Clear();
        ShowLayer(ManagementBodyHost, false);
        ManagementShelfFrame.Visibility = Visibility.Collapsed;
        foreach (UIElement c in ManagementBodyHost.Children) ShowLayer(c, false);
        X10NativeNav.IsChecked = D10NativeNav.IsChecked = RgbNav.IsChecked = ScreenNav.IsChecked = DeviceNav.IsChecked = DiagnosticsNav.IsChecked = MouseNav.IsChecked = PcModeNav.IsChecked = TesterNav.IsChecked = SetupsNav.IsChecked = CommunityNav.IsChecked = DashboardNav.IsChecked = false;
        X10Hardware?.StopActiveCalibration();
        if (KeyboardNav != null) KeyboardNav.IsChecked = false;
        if (restoreButtons)
        {
            DraftBar.Visibility = Visibility.Visible; RootLayout.RowDefinitions[2].Height = new(40); Apply.Visibility = ChangeCount.Visibility = Visibility.Visible;
            if (buttonsPage == null)
            {
                RootLayout.RowDefinitions[4].Height = new(184);
                Grid.SetRowSpan(StudioBody, 1);
                HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = OverviewChoice.Visibility = ResetAllChoice.Visibility = Visibility.Visible;
            }
            UpdateButtonsPage();
        }
    }
    private void LoadSetup(SetupSnapshot snapshot){if(snapshot.ModelId!=Model.Id)throw new InvalidOperationException("Setup model does not match this player.");snapshot.LoadInto(Draft,Curves.Draft,macroDraft,vibrationDraft);Curves.Refresh();Macros?.Refresh();Vibration?.Refresh();X10Hardware?.RefreshFromDrafts();D10Hardware?.RefreshFromDrafts();SyncPcMode();}
    private void UpdateSetupContext(){if(ManagementPage!="Setups"||Setups==null)return;activeSetup=Setups.ActiveId;ActiveSetupChanged?.Invoke(activeSetup);ChangeCount.Text=activeSetup==null?"Local setup library · Hardware application unavailable":"Setup open in local editor · Hardware unchanged";PrimaryHint.Text=" Select / Open";OverviewHint.Text=" New setup";DraftChip.Text="My setups";Apply.IsEnabled=false;}
    public bool DeliverGameplayFrame(TesterFrame frame)=>ManagementPage=="Tester" && Tester?.Receive(frame)==true;

    // ---- Buttons-page "Try it": keyboard-driven preview of the real stick caps and triggers ----
    private KeyboardSimulation? trial;
    private Grid? tryLayer;
    private Diagnostics.DiagnosticsWorkbench? tryDiagnostics;
    public bool IsTrying => trial?.Enabled == true;
    private bool OnButtonsPage => ManagementPage == null && !IsCurves && !IsMacros && !IsVibration;
    private static bool IsOnboardFunction(string key) => key is "CAPTURE" or "TURBO";
    private void ToggleTry(object sender, RoutedEventArgs e) { if (IsTrying) StopTry(); else StartTry(); }
    public void StartTry()
    {
        CancelRemapCapture();
        if (!OnButtonsPage) return;
        tryDiagnostics = new Diagnostics.DiagnosticsWorkbench(Player, _ => {}, compact: true,model:Model);
        var close = new Button { Content = "Close test", Style = DS.Style("ActionButton"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new(16) };
        close.Click += (_, _) => StopTry();
        var content = new Grid(); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new());
        var heading = new DockPanel(); DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new StackPanel { Margin = new(24,16,0,8), Children = { Kit.Text("Try your controller", 24, "DS.Text", FontWeights.SemiBold), Kit.Text("Roll both sticks around their edge · error measures deviation from a circle, not measurement uncertainty", 13, "DS.TextSoft") } });
        content.Children.Add(heading); Grid.SetRow(tryDiagnostics.Body, 1); content.Children.Add(tryDiagnostics.Body);
        tryLayer = new Grid { Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(210, 3, 6, 16))), Children = { new Border { Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(10,18,38))), BorderBrush = DS.Brush("DS.AccentHi"), BorderThickness = new(1), CornerRadius = new(22), Margin = new(40,100,40,110), Child = content } } };
        StudioShell.Children.Add(tryLayer); Panel.SetZIndex(tryLayer, 48); tryDiagnostics.Start(); InputContextChanged?.Invoke();
        trial = new KeyboardSimulation(Model.Id); trial.Enable(); PushTrialFrame(); buttonsPage?.SetTrying(true); UpdateHints();
        TryChoice.Content = "Stop"; TryChoice.Background = (Brush)FindResource("Blue");
        TryHint.Visibility = Visibility.Visible; HardwareShortcuts.Visibility = Visibility.Collapsed;
        StatusNote.Text = "Keyboard preview · No controller output";
    }
    public void StopTry()
    {
        tryDiagnostics?.Stop(); tryDiagnostics = null;
        if (tryLayer != null) { StudioShell.Children.Remove(tryLayer); tryLayer = null; InputContextChanged?.Invoke(); }
        if (trial == null) return;
        trial.Disable(); trial = null; ActiveLive.Release(); buttonsPage?.SetTrying(false); UpdateHints(); IlluminatePressed(0, 0);
        TryChoice.Content = "Try it"; TryChoice.ClearValue(BackgroundProperty);
        TryHint.Visibility = Visibility.Collapsed; HardwareShortcuts.Visibility = IsCurves ? Visibility.Collapsed : Visibility.Visible;
        StatusNote.Text = navigationMode ? "Navigation preview" : "Controller offline";
    }
    /// <summary>Called when the window loses focus so a held key can never stick.</summary>
    public void ReleaseTransientInput() { keyboardHeld.Clear(); controllerHeld.Clear(); CancelRemapCapture(); if (trial == null) return; trial.Clear(); PushTrialFrame(); }
    private static SimulationKey? TrialKey(Key key) => key switch { Key.W => SimulationKey.W, Key.A => SimulationKey.A, Key.S => SimulationKey.S, Key.D => SimulationKey.D, Key.Up => SimulationKey.Up, Key.Down => SimulationKey.Down, Key.Left => SimulationKey.Left, Key.Right => SimulationKey.Right, Key.Q => SimulationKey.Q, Key.E => SimulationKey.E, _ => null };
    private void PushTrialFrame()
    {
        if (trial == null) return; var f = trial.Frame();
        ActiveLive.SetInput(f.LeftX, f.LeftY, f.RightX, f.RightY, f.LT, f.RT); IlluminatePressed(f.LT, f.RT);
        tryDiagnostics?.PreviewKeyboard(f.LeftX, f.LeftY, f.RightX, f.RightY, f.LT, f.RT);
    }
    private void IlluminatePressed(double lt, double rt)
    {
        foreach (var (key, control) in controls)
            control.Illuminate(key == SelectedControl, navigationMode);
    }
    /// <summary>Read-only gameplay input shown on the Buttons artwork. It never navigates or writes.</summary>
    private bool? gameplayConnected;
    public void SetGameplayConnected(bool connected)
    {
        if(gameplayConnected==connected)return;
        gameplayConnected=connected;ShowLink();
        controllerConnection.Set(connected ? "Live input" : "Offline", connected ? ChipKind.Success : ChipKind.Neutral);
        GameplayConnection.Text=connected?"● Live input":"● No input";
        GameplayConnection.Foreground=(Brush)FindResource(connected?"Ice":"Amber");buttonsPage?.SetConnected(connected);
        if(!connected){Curves.ShowLive(-1);StickLab?.ClearLive();}
    }
    public void ObserveLiveInput(double lx, double ly, double rx, double ry, double lt, double rt)
    {
        StickLab?.ShowLive(lx, ly, rx, ry);
        ActiveLive.SetInput(lx, ly, rx, ry, lt, rt);
        // the Curves graph follows the real stick or trigger whenever a controller is connected; Try it is the keyboard's
        if (IsCurves && ManagementPage == null)
        {
            double lMag = Math.Sqrt(lx * lx + ly * ly), rMag = Math.Sqrt(rx * rx + ry * ry);
            if (rMag > 0.15 && rMag > lMag + 0.05 && Curves.Channel != CurveChannel.RightStick) Curves.SelectChannel(CurveChannel.RightStick);
            else if (lMag > 0.15 && lMag > rMag + 0.05 && Curves.Channel != CurveChannel.LeftStick) Curves.SelectChannel(CurveChannel.LeftStick);
            else if (lt > 0.20 && lt > rt + 0.05 && Curves.Channel != CurveChannel.LeftTrigger) Curves.SelectChannel(CurveChannel.LeftTrigger);
            else if (rt > 0.20 && rt > lt + 0.05 && Curves.Channel != CurveChannel.RightTrigger) Curves.SelectChannel(CurveChannel.RightTrigger);
            Curves.ShowLive(Curves.Channel switch { CurveChannel.LeftStick => lMag, CurveChannel.RightStick => rMag, CurveChannel.LeftTrigger => lt, _ => rt });
            return;
        }
        if (!OnButtonsPage || IsTrying) return;
        ActiveLive.SetInput(lx, ly, rx, ry, lt, rt);
    }
    // ---- the app-wide controller scheme (InputMap): X shortcut, Y secondary, LT/RT section, live hints ----
    public string CurrentPage => ManagementPage ?? (IsCurves ? "Curves" : IsMacros ? "Macros" : IsVibration ? "Vibration" : "Buttons");
    private bool PageX()
    {
        switch (CurrentPage)
        {
            case "Dashboard": ShowButtons(); return true;
            case "Buttons": if (IsTrying) StopTry(); else StartTry(); return true;
            case "Curves": if (!Curves.Draft.CanEdit) return false; Curves.Reset(); return true;
            case "Macros": Macros?.Add(); return Macros != null;
            case "Vibration": Vibration?.Reset(); return Vibration != null;
            case "Device": return false; // the Tester was removed (owner direction 10 Oct 2026)
            default: return false;
        }
    }
    private bool PageY()
    {
        switch (CurrentPage)
        {
            case "Dashboard": Dashboard?.Rescan(); return Dashboard != null;
            case "Buttons": buttonsPage?.ToggleOverview(); return buttonsPage != null;
            case "Curves":
                if (!Curves.Draft.CanEdit) return false;
                var names = CurveDraft.Presets.Keys.ToList(); int at = names.IndexOf(Curves.Draft[Curves.Channel].Preset);
                Curves.SetPreset(names[(at + 1) % names.Count]); return true;
            case "Macros": Macros?.Preview(); return Macros != null;
            case "Device": ShowManagement("Setups"); return true;
            default: return false;
        }
    }
    private bool PageSection(int step)
    {
        switch (CurrentPage)
        {
            case "Dashboard": Dashboard?.PageShelf(step); return Dashboard != null;
            case "Buttons": if (buttonsPage == null) return false; buttonsPage.ShowView(!buttonsPage.IsRear); return true;
            case "Curves":
                var all = Enum.GetValues<CurveChannel>(); int i = Array.IndexOf(all, Curves.Channel);
                Curves.SelectChannel(all[(i + step + all.Length) % all.Length]); return true;
            case "Macros":
                if (Macros == null) return false; int s = Array.IndexOf(macroDraft.SlotNames, Macros.Slot);
                Macros.SelectSlot(macroDraft.SlotNames[(s + step + macroDraft.SlotNames.Length) % macroDraft.SlotNames.Length]); return true;
            case "Vibration": if (Vibration == null || !vibrationDraft.CanEdit) return false; Vibration.Set(Math.Clamp(vibrationDraft.Strength + step * 5, 0, 100)); return true;
            default: return false;
        }
    }
    /// <summary>Rebuilds the footer from InputMap: only buttons that do something here, labelled for the focused item.</summary>
    public void UpdateHints()
    {
        string page = CurrentPage; string? focus = null;
        if (page == "Dashboard") focus = Dashboard?.FocusAction;
        else if (page == "Buttons" && Keyboard.FocusedElement is PhysicalHotspot) focus = Draft.CanEdit(SelectedControl) ? "Choose output" : "Inspect";
        else if (page == "Buttons" && buttonsPage?.IsKeyboardFocusWithin == true && Keyboard.FocusedElement is Button { Tag: string t } && ButtonDraft.Targets.Contains(t)) focus = "Assign";
        if(CapturingButtonEditor){LiveHints.Children.Clear();LiveHints.Children.Add(GlyphHint.Footer(("Esc","Cancel")));return;}
        if(page=="Mouse"&&MouseCapturesInput){LiveHints.Children.Clear();LiveHints.Children.Add(GlyphHint.Footer(("Esc","Back")));return;}
        var map = InputMap.For(page, focus, IsTrying);
        var hints = new List<(string, string)>();
        if (map.A != null) hints.Add(("A", map.A));
        hints.Add((page=="Diagnostics"?"Esc":"B", map.B));
        if (map.X != null) hints.Add(("X", map.X));
        if (map.Y != null) hints.Add(("Y", map.Y));
        if (map.Triggers != null) hints.Add(("LT/RT", map.Triggers));
        if (page == "Buttons" && CanEngageRemap) hints.Add(("R3", "Listen"));
        if (page is not ("Tester" or "Diagnostics")) hints.Add(("LB/RB", "Tabs"));
        LiveHints.Children.Clear(); LiveHints.Children.Add(GlyphHint.Footer(hints.ToArray()));
    }

    /// <summary>Rear paddles are programmable macro slots, not independent buttons: open the slot in Macros.</summary>
    private bool OpenMacroSlot(string key)
    {
        if (!macroDraft.SlotNames.Contains(key)) return false;
        StopTry(); ShowMacros(true); Macros?.SelectSlot(key); return Macros != null;
    }
    private void X10NativeTab(object sender,RoutedEventArgs e)=>ShowManagement("X10Native");
    private void D10NativeTab(object sender,RoutedEventArgs e)=>ShowManagement("D10Native");
    private void RgbTab(object sender,RoutedEventArgs e)=>ShowManagement("RGB");
    private void ScreenTab(object sender,RoutedEventArgs e)=>ShowManagement("Screen");
    private void DeviceTab(object sender,RoutedEventArgs e)=>ShowManagement("Device");private void TesterTab(object sender,RoutedEventArgs e)=>ShowManagement("Tester");private void DiagnosticsTab(object sender,RoutedEventArgs e)=>ShowManagement("Diagnostics");private void MouseTab(object sender,RoutedEventArgs e)=>ShowManagement("Mouse");private void SetupsTab(object sender,RoutedEventArgs e)=>ShowManagement("Setups");private void CommunityTab(object sender,RoutedEventArgs e)=>ShowManagement("Community");
    private void Front(object sender, RoutedEventArgs e) { ShowController(false); ClearSelection(); }
    private void Rear(object sender, RoutedEventArgs e) { ShowController(true); ClearSelection(); }
    private void Back(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    public void OnKeyUp(object sender,KeyEventArgs e){var key=e.Key==Key.System?e.SystemKey:e.Key;if(InputMap.ButtonFor(key) is {} up0 && keyboardHeld.Remove(up0) && CapturingRemap){buttonsPage!.Observe(HeldCaptureInputs());e.Handled=true;return;}if(trial!=null && TrialKey(e.Key) is {} up){trial.Release(up);PushTrialFrame();e.Handled=true;return;}if(ManagementPage=="Tester" && Tester?.HandleKeyboard(e.Key,false)==true)e.Handled=true;}
    /// <summary>The Buttons page is waiting for "the button you want" or "what it should do": every press is captured.</summary>
    public bool CapturingRemap => OnButtonsPage && buttonsPage != null && buttonsPage.Listening != ButtonsConsolePage.Listen.None;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        // the input contract: capture and recording first, then a focused text field, then theme and page keys
        bool controller = sender is ProductHost; var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!controller && !e.IsRepeat && InputMap.ButtonFor(key) is { } held) keyboardHeld.Add(held);
        if (CapturingRemap)
        {
            e.Handled = true; if (e.IsRepeat) return;
            if (e.Key == Key.Escape) { buttonsPage!.CancelListening(); return; }
            buttonsPage!.Observe(HeldCaptureInputs());
            return;
        }
        if (InputContext == InputOwner.MacroRecording) { e.Handled = true; if (e.Key == Key.Escape && !e.IsRepeat) Macros!.CancelRecording(); return; }
        if (Keyboard.FocusedElement is TextBox { IsReadOnly: false, IsEnabled: true } field && (controller ? e.Key == Key.B : e.Key == Key.Escape)) { LeaveField(field); e.Handled = true; return; }
        bool typing = !controller && Keyboard.FocusedElement is TextBox { IsReadOnly: false };
        // the Advanced stick tools own the keyboard while open (arrows nudge their sliders); Esc closes them
        if (stickLabLayer != null) { if (e.Key == Key.Escape) { CloseStickTools(); e.Handled = true; } return; }
        // the agent window: Esc puts it away, and while typing to it every key is the text box's
        if (agentTour != null) { if (e.Key == Key.Escape) agentTour.Skip(); e.Handled = true; return; }
        if (AgentOpen && e.Key == Key.Escape) { if (agentPanel!.SettingsOpen) agentPanel.ShowSettingsPage(false); else CloseAgent(); e.Handled = true; return; }
        if (AgentOpen && agentPanel!.Body.IsKeyboardFocusWithin && Keyboard.FocusedElement is TextBox) return;
        if (CapturingRemap)
        {
            e.Handled = true; if (e.IsRepeat) return;
            if (e.Key == Key.Escape) { buttonsPage!.CancelListening(); return; }
            if (InputMap.ButtonFor(e.Key == Key.System ? e.SystemKey : e.Key) is { } pressed) buttonsPage!.Press(pressed);
            return;
        }
        if(trial!=null){if(e.Key==Key.Escape){StopTry();e.Handled=true;return;}if(TrialKey(e.Key) is {} down){if(!e.IsRepeat){trial.Press(down);PushTrialFrame();}e.Handled=true;return;}}
        if(ManagementPage=="Setups" && Setups?.HandleKey(e)==true)return;
        if(ManagementPage=="Mouse"&&e.Key==Key.Escape){ShowManagement("Dashboard");e.Handled=true;return;}
        if(MouseCapturesInput)return;
        if(ManagementPage=="Diagnostics"){if(e.Key==Key.Escape){ShowManagement("Device");e.Handled=true;}return;}
        if(ManagementPage=="Tester"){if(Tester?.HandleKeyboard(e.Key,true)==true){e.Handled=true;return;}if(e.Key==Key.Escape){ShowManagement("Device");e.Handled=true;}else if(e.Key is Key.F6 or Key.A or Key.B or Key.Y){e.Handled=true;}return;}
        if(Keyboard.FocusedElement is not TextBox && !e.IsRepeat)
        {
            if(e.Key==Key.X && PageX()){e.Handled=true;UpdateHints();return;}
            if(e.Key==Key.Y && PageY()){e.Handled=true;UpdateHints();return;}
            if(e.Key is InputMap.SectionPrevious or InputMap.SectionNext && PageSection(e.Key==InputMap.SectionNext?1:-1)){e.Handled=true;UpdateHints();return;}
        }
        if(IsVibration && Vibration?.HandleKey(e)==true)return;
        if(IsMacros && Macros?.HandleKey(e)==true)return;
        if (IsCurves && Curves.HandleKey(e)) return;
        // LB/RB walk the visible tabs in order, wrapping, the same on every theme
        if(e.Key is Key.PageDown or Key.PageUp){e.Handled=true;if(!e.IsRepeat)StepTab(e.Key==Key.PageDown?1:-1);return;}
        // a text field owns letters, digits, arrows and Enter
        if (typing) return;
        if (e.Key == Key.F6) { if (!e.IsRepeat) SetNavigationPreview(!navigationMode); e.Handled = true; return; }
        if (e.Key is Key.Escape or Key.B)
        {
            if (!navigationMode && e.Key == Key.B) SetNavigationPreview(true, preserveExistingFocus: true);
            if (AssignmentsVisible) ShowAssignments(false);
            // B on Buttons with a control picked puts the card back to "Select a control" (owner request 17 Oct 2026); a second B leaves
            else if (OnButtonsPage && buttonsPage != null && SelectedControl.Length > 0) { string was = SelectedControl; ClearSelection(); if (navigationMode) buttonsPage.Hotspot(was)?.Focus(); }
            else if(IsVibration && ReferenceEquals(Vibration?.Strength,Keyboard.FocusedElement))FocusSelectedControl();
            else if (IsCurves && (Curves.PointHandles.Any(p=>ReferenceEquals(p,Keyboard.FocusedElement)) || ReferenceEquals(Curves.Inner,Keyboard.FocusedElement) || ReferenceEquals(Curves.Outer,Keyboard.FocusedElement))) FocusSelectedControl();
            else if (Keyboard.FocusedElement is Button && Keyboard.FocusedElement is not PhysicalHotspot) FocusSelectedControl();
            else BackRequested?.Invoke();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Y)
        {
            if (!navigationMode) SetNavigationPreview(true, preserveExistingFocus: true);
            if(ManagementPage=="Device"){ShowManagement("Setups");e.Handled=true;return;}
            else if(ManagementPage=="Setups")Setups?.NewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else if(IsVibration)Vibration?.Reset();
            else if(IsMacros)Macros?.Add();
            else if(IsCurves) Curves.Reset();
            else ShowAssignments(!AssignmentsVisible);
            e.Handled = true;
            return;
        }
        FocusNavigationDirection? direction = e.Key switch { Key.Up => FocusNavigationDirection.Up, Key.Down => FocusNavigationDirection.Down, Key.Left => FocusNavigationDirection.Left, Key.Right => FocusNavigationDirection.Right, _ => null };
        if (direction.HasValue)
        {
            if (!navigationMode) SetNavigationPreview(true, preserveExistingFocus: true);
            if (Keyboard.FocusedElement is RadioButton)
            {
                if (direction == FocusNavigationDirection.Down)
                {
                    FocusSelectedControl();
                    e.Handled = true;
                    return;
                }
            }
            if (Keyboard.FocusedElement is PhysicalHotspot current)
            {
                var p = current.Region.Bounds; double x = p.X + p.Width / 2, y = p.Y + p.Height / 2;
                var candidates = (OnButtonsPage && buttonsPage != null ? buttonsPage.Hotspots : (IEnumerable<PhysicalHotspot>)controls.Values).Where(c => c != current && c.IsEnabled && c.IsVisible).Select(c => new { Button = c, Dx = c.Region.Bounds.X + c.Region.Bounds.Width / 2 - x, Dy = c.Region.Bounds.Y + c.Region.Bounds.Height / 2 - y });
                candidates = direction.Value switch { FocusNavigationDirection.Right => candidates.Where(c => c.Dx > .002), FocusNavigationDirection.Left => candidates.Where(c => c.Dx < -.002), FocusNavigationDirection.Down => candidates.Where(c => c.Dy > .002), _ => candidates.Where(c => c.Dy < -.002) };
                var next = candidates.OrderBy(c => direction.Value is FocusNavigationDirection.Left or FocusNavigationDirection.Right ? Math.Abs(c.Dx) + Math.Abs(c.Dy) * 1.8 : Math.Abs(c.Dy) + Math.Abs(c.Dx) * 1.8).FirstOrDefault();
                if (next != null) { next.Button.Focus(); SelectControl(next.Button.Region.Key); }
                else if (direction == FocusNavigationDirection.Down) FocusCurrentTarget();
                else if (direction == FocusNavigationDirection.Up) GetActiveTab()?.Focus();
            }
            else if (Keyboard.FocusedElement is UIElement element)
            {
                bool moved = element.MoveFocus(new(direction.Value));
                if (!moved && direction == FocusNavigationDirection.Up && element is not RadioButton)
                {
                    GetActiveTab()?.Focus();
                }
            }
            else FocusSelectedControl();
            e.Handled = true; return;
        }
        if (e.Key is Key.Enter or Key.A)
        {
            if (!navigationMode) SetNavigationPreview(true, preserveExistingFocus: true);
            if (Keyboard.FocusedElement == null || !IsKeyboardFocusWithin) FocusSelectedControl();
            if (!e.IsRepeat && Keyboard.FocusedElement is PhysicalHotspot && IsCurves) Curves.Buttons.FirstOrDefault()?.Focus();
            else if (!e.IsRepeat && Keyboard.FocusedElement is PhysicalHotspot && Draft.CanEdit(SelectedControl)) FocusCurrentTarget();
            else if (!e.IsRepeat && Keyboard.FocusedElement is Button button && button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }
    private RadioButton? GetActiveTab() => new[] { DashboardNav, ButtonsNav, CurvesNav, MacrosNav, VibrationNav, X10NativeNav, D10NativeNav, PcModeNav, RgbNav, ScreenNav, DeviceNav, DiagnosticsNav, MouseNav, KeyboardNav, TesterNav, SetupsNav, CommunityNav }.FirstOrDefault(t => t?.IsChecked == true);
    private void FocusCurrentTarget() { if (OnButtonsPage && buttonsPage != null) { buttonsPage.FocusOutput(Draft.Target(SelectedControl)); return; } targetButtons.FirstOrDefault(b => b.IsEnabled && (string)b.Tag == Draft.Target(SelectedControl))?.Focus(); }
    private static string TargetGlyph(string key) => key switch { "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", _ => Glyph(key) };
    private static string Glyph(string key) => key switch { "DPAD_UP" => "D↑", "DPAD_DOWN" => "D↓", "DPAD_LEFT" => "D←", "DPAD_RIGHT" => "D→", "SELECT" => "View", "START" => "Menu", "HOME" => "Guide", "CAPTURE" => "C", "TURBO" => "T", _ => key };
    private static string Label(string key) => key switch { "DPAD_UP" => "D-pad Up", "DPAD_DOWN" => "D-pad Down", "DPAD_LEFT" => "D-pad Left", "DPAD_RIGHT" => "D-pad Right", "SELECT" => "View / Back", "START" => "Menu / Start", "HOME" => "Home / Guide", "CAPTURE" => "Capture · C", "TURBO" => "Turbo · T", "LSTICK_ANALOG" => "Left stick axis", "RSTICK_ANALOG" => "Right stick axis", _ => key };
    private void Minimize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) SystemCommands.MinimizeWindow(w); }
    private void Maximize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; } // fullscreen or the fixed window
    private void Close(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
