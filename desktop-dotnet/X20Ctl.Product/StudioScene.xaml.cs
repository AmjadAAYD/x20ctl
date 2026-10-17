using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public partial class StudioScene : UserControl
{
    public int Player { get; }
    public ControllerModel Model { get; }
    public ButtonDraft Draft { get; }
    public string SelectedControl { get; private set; } = "";
    public bool IsRear { get; private set; }
    public bool AssignmentsVisible => AssignmentOverview.Visibility == Visibility.Visible;
    public bool IsCurves { get; private set; }
    public bool IsMacros { get; private set; }
    public bool IsVibration {get;private set;}
    public VibrationWorkbench? Vibration {get;private set;}
    public string? ManagementPage {get;private set;}
    public DeviceWorkbench? Device {get;private set;}
    public TesterWorkbench? Tester {get;private set;}
    public SetupsWorkbench? Setups {get;private set;}
    public bool CapturingButtonEditor=>buttonsPage!=null && OnButtonsPage && !IsTrying && !AgentOpen;
    public void SelectPhysicalButton(string key)=>buttonsPage?.SelectPhysical(key);
    private InputOwner? lastPageOwner;
    public InputOwner InputContext=>CapturingButtonEditor?InputOwner.RemapCapture:(tryLayer != null || ManagementPage is "Tester" or "Diagnostics" or "Mouse")?InputOwner.TesterCapture:InputOwner.UiNavigation;
    /// <summary>The Diagnostics tab, built on first open; it only listens while shown.</summary>
    public Diagnostics.DiagnosticsWorkbench? Diagnostics { get; private set; }
    /// <summary>Controller as mouse: the service lives in the host; the tab is built on first open.</summary>
    public MouseModeService? MouseService { get; set; }
    public MouseWorkbench? Mouse { get; private set; }
    public string NativeEngineStatus {get;set;}="Native core not connected";
    public event Action? InputContextChanged;
    public void EnableNativeNavigation(){if(!navigationMode)SetNavigationPreview(true);StatusNote.Text="Controller navigation";}
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
        InitializeComponent(); Player = player; Model = model; Draft = draft;
        setupStore=setups??new(Path.Combine(Path.GetTempPath(),"x20ctl-render-setup-"+Guid.NewGuid().ToString("N")+".json"));legacySources=legacy??Array.Empty<string>();activeSetup=activeId;
        Unloaded+=(_,_)=>{StopTry();Tester?.ExitCapture();Diagnostics?.Stop();};
        macroDraft=macros ?? new MacroDraft(model.Id);vibrationDraft=vibration??new(model.Id);
        animate = !reducedMotion && SystemParameters.ClientAreaAnimation;
        StudioBackdrop.ReducedMotion = !animate;
        ControllerContext.Text = $"{model.Name} · Player {player + 1}";
        layout = ControlRegions.Owned(model, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/x20-rear.json")));
        TargetChoice.ItemsSource = ButtonDraft.Targets.Select(key => new KeyValuePair<string, string>(key, Label(key))).ToArray();
        RearChoice.IsEnabled = model.Id == "x20";
        CurvesNav.ToolTip = draft.PcMode ? "PC mode curves · Page Down" : model.Id == "x20" ? "Local curve drafts · Page Down" : "Curve preview · read-only for this model · Page Down";
        Unloaded+=(_,_)=>{PcModePage.ProfileChanged-=PcProfileChanged;PcModePage.NoticeAccepted-=PcNoticeAccepted;};
        Loaded+=(_,_)=>{PcModePage.ProfileChanged-=PcProfileChanged;PcModePage.ProfileChanged+=PcProfileChanged;PcModePage.NoticeAccepted-=PcNoticeAccepted;PcModePage.NoticeAccepted+=PcNoticeAccepted;};
        BuildTargets(); BuildInventory();
        BuildTitlePills(); BuildNavigation();
        buttonsPage = new ButtonsConsolePage(model, layout, draft, Label, animate, SelectControl, OpenMacroSlot, () => { if (IsTrying) StopTry(); else StartTry(); });
        buttonsPage.Changed += RefreshEditor; ButtonsPageHost.Children.Add(buttonsPage);
        PreviewGotKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(UpdateHints, System.Windows.Threading.DispatcherPriority.Background);
        foreach (var h in buttonsPage.Hotspots) RegisterFocus(h);
        Curves = new(curveDraft ?? new CurveDraft(model.Id), this,animate); ModelProfiles.Wear(Curves.Inspector, model.Id, "Curves", draft.PcMode);
        // The footer retains scope; on compact windows avoid a repeated multi-line notice crowding out the graph.
        var curveScope=Curves.Inspector.Children.OfType<Border>().FirstOrDefault();if(curveScope!=null){Curves.Inspector.ToolTip=draft.PcMode?"Saved on this PC for the selected player. Nothing is written to the controller.":"Local preview. Native configuration is unavailable for this model.";Curves.Inspector.SizeChanged+=(_,_)=>curveScope.Visibility=Curves.Inspector.ActualWidth<680?Visibility.Collapsed:Visibility.Visible;} CurveInspectorHost.Children.Add(Kit.Panel(null, null, null, Curves.Inspector)); CurveShelfHost.Children.Add(Curves.Shelf);
        Curves.ChannelSelected += channel => { SelectControl(channel switch { CurveChannel.LeftStick => "LSTICK_ANALOG", CurveChannel.RightStick => "RSTICK_ANALOG", CurveChannel.LeftTrigger => "LT", _ => "RT" }); UpdateCurveContext(); EnterMotion(CurveInspectorHost,12,220); };
        Curves.Changed += UpdateCurveContext; foreach (Button button in Curves.Buttons) RegisterFocus(button);
        ResetAllCurvesAction=new MenuItem {Header="Reset all curves",ToolTip="Reset local drafts for both sticks and both triggers."};
        ResetAllCurvesAction.Click+=ResetAll;
        CurveMoreChoice.ContextMenu=new ContextMenu();
        CurveMoreChoice.ContextMenu.Items.Add(ResetAllCurvesAction);
        foreach(var point in Curves.PointHandles) { point.GotKeyboardFocus += (_,_)=>{if(navigationMode)ShowDetachedFocus(point);};point.LostKeyboardFocus += (_,_)=>HideDetachedFocus(); }
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
            RootLayout.RowDefinitions[2].Height = new(IsCurves||IsVibration||ManagementPage is "Dashboard" or "Mouse" or "Diagnostics" or "Device" or "PcMode"?0:expanded?44:40);
            UpdateNavigation();
            SizeAgentController();
            RootLayout.RowDefinitions[4].Height = new(IsCurves?144:IsVibration?54:compact?170:expanded?206:184);
            RootLayout.RowDefinitions[5].Height = new(expanded?78:66);
            SecondaryFooterHint.Visibility=ActualWidth<1100 || ManagementPage=="Tester"?Visibility.Collapsed:Visibility.Visible;
            StatusNote.Visibility=IsCurves||IsVibration||ManagementPage is "Dashboard" or "Mouse" or "Diagnostics" or "Device" or "PcMode" || ActualWidth<1350?Visibility.Collapsed:Visibility.Visible;
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
        if (agentPanel == null)
        {
            agentPanel = new(Model.Id, Model.Name, PcMode, RegisterFocus, Player, AgentMeter, reducedMotion:!animate);
            agentPanel.Body.HorizontalAlignment = HorizontalAlignment.Right; agentPanel.Body.VerticalAlignment = VerticalAlignment.Bottom; agentPanel.Body.Margin = new(0, 0, 30, 170);
            agentPanel.CloseRequested += CloseAgent; agentPanel.ChangeControllerRequested += () => { CloseAgent(); BackRequested?.Invoke(); }; agentPanel.Walkthrough = PlayAgentTour;
            agentPanel.PageRequested += AgentGoTo;
            agentPanel.MouseSwitch = on => { if (MouseService == null) return "Mouse mode isn't available in this window."; if(on){try{MouseService.TakeControl(Player,Model.Id);}catch(InvalidOperationException error){return error.Message;}}else if(MouseService.OwnerPlayer==Player && MouseService.OwnerModel==Model.Id)MouseService.Active=false; if (ManagementPage == "Mouse") ShowManagement("Mouse"); return null; };
            Grid.SetZIndex(agentPanel.Body, 41); StudioShell.Children.Add(agentPanel.Body);
        }
        // across the Studio, from under the tabs to above the round button (owner's sketch, 13 Oct), keeping a controller's
        // shape: as wide as the room allows, centred
        SizeAgentController();
        agentPanel.Body.HorizontalAlignment=HorizontalAlignment.Right;agentPanel.Body.VerticalAlignment=VerticalAlignment.Bottom;
        agentPanel.Show();InputContextChanged?.Invoke(); if (agentPill != null) agentPill.ToolTip = "Put Agent XCTL away";
        Dispatcher.BeginInvoke(() => agentPanel?.Input.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
    public void CloseAgent() { if (agentPanel == null) return; agentPanel.Hide();InputContextChanged?.Invoke(); if (agentPill != null) agentPill.ToolTip = "Agent XCTL"; }
    private void SizeAgentController()
    {
        if (agentPanel == null) return;
        double room=Math.Max(180,StudioShell.ActualHeight-230), wide=Math.Max(280,StudioShell.ActualWidth-60);
        double high=Math.Min(room,wide*Agent.AgentPanel.DesignHeight/Agent.AgentPanel.DesignWidth);
        double across=high*Agent.AgentPanel.DesignWidth/Agent.AgentPanel.DesignHeight;
        agentPanel.Body.Width=across;agentPanel.Body.Height=high;
        agentPanel.Body.Margin=new(0,0,Math.Max(20,(StudioShell.ActualWidth-across)/2),100);
    }
    /// <summary>The agent sent the owner to a page: put the window away and go there.</summary>
    private void AgentGoTo(string page)
    {
        CloseAgent();
        switch (page)
        {
            case "Diagnostics" or "Tools": ShowManagement("Diagnostics"); break;
            case "Mouse" or "PcMode" or "Device": ShowManagement(page); break;
            case "Curves": ShowCurves(true); break;
            case "Macros": ShowMacros(true); break;
            case "Vibration": ShowVibration(true); break;
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
    private Button? deviceControl;
    private readonly TextBlock deviceState=Kit.Text("Offline",13,"DS.TextSoft",FontWeights.SemiBold);
    private readonly TextBlock batteryLabel=Kit.Text("Battery · Not reported",13,"DS.TextSoft",FontWeights.SemiBold);
    public void SetBatteryLevel(string? level)
    {
        currentBatteryLevel=level;Dashboard?.SetBatteryLevel(level);Device?.SetBatteryLevel(level);
        batteryLabel.Text="Battery · "+(level switch {"full"=>"Full","medium"=>"Medium","low"=>"Low","empty"=>"Empty","wired"=>"Wired",_=>"Not reported"});
        batteryLabel.Foreground=DS.Brush(level is "low" or "empty"?"DS.Warn":"DS.TextSoft");UpdateDeviceHeader();
    }
    private readonly HeaderBattery headerBattery=new();
    private readonly StatusChip controllerConnection = new("Offline", ChipKind.Neutral) { Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private void BuildTitlePills()
    {
        var heart = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) };
        var support = new Button { Cursor = Cursors.Hand, ToolTip = "Support X20CTL on Ko-fi (opens your browser)", FocusVisualStyle = null, Margin = new(0, 0, 12, 0),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            Content = new Border { CornerRadius = new(10), Height = 36, Padding = new(14, 0, 18, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 150, 170)),
                Background = new LinearGradientBrush(Color.FromRgb(232, 54, 96), Color.FromRgb(196, 30, 78), 0), Effect = new DropShadowEffect { Color = Color.FromRgb(232, 54, 96), BlurRadius = 18, ShadowDepth = 0, Opacity = .5 },
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { heart, new TextBlock { Text = "Support", FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } } } } };
        support.Click += (_, _) => SupportRequested?.Invoke();
        var pill = (Border)support.Content; support.MouseEnter += (_, _) => pill.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)); support.MouseLeave += (_, _) => pill.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 150, 170));
        var name = new Border { CornerRadius = new(18), Height = 36, Padding = new(14, 0, 16, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(80, 125, 182, 255)), Background = new SolidColorBrush(Color.FromArgb(60, 30, 60, 130)),
            Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = DS.Brush("DS.AccentHi"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) }, new TextBlock { Text = Model.Name.Replace("EasySMX ", ""), FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } } } };
        // Agent XCTL in the title bar, in the gap left of Support: opens (or puts away) the agent
        var agentPill = this.agentPill = new Button { Cursor = Cursors.Hand, ToolTip = "Agent XCTL", FocusVisualStyle = null, Margin = new(0, 0, 12, 0),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            Content = new Border { CornerRadius = new(18), Height = 36, Padding = new(14, 0, 16, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(150, 125, 182, 255)),
                Background = new SolidColorBrush(Color.FromRgb(38,91,184)),
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) },
                    new TextBlock { Text = "Agent XCTL", FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } } } } };
        agentPill.Click += (_, _) => { if (AgentOpen) CloseAgent(); else OpenAgent(); };
        var agentBorder = (Border)agentPill.Content; agentPill.MouseEnter += (_, _) => agentBorder.BorderBrush = Brushes.White; agentPill.MouseLeave += (_, _) => agentBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 125, 182, 255));
        var more = new Button { Content="⋯",Style=DS.Style("Zone.Utility"),Width=38,Height=36,MinHeight=36,Padding=new(0),Margin=new(8,0,0,0),ToolTip="Help and utilities" };
        more.ContextMenu=new ContextMenu();
        var supportItem=new MenuItem {Header="Support X20CTL"};supportItem.Click+=(_,_)=>SupportRequested?.Invoke();more.ContextMenu.Items.Add(supportItem);
        var helpItem=new MenuItem {Header="Diagnostics and help"};helpItem.Click+=(_,_)=>ShowManagement("Diagnostics");more.ContextMenu.Items.Add(helpItem);
        more.Click+=(_,_)=>more.ContextMenu.IsOpen=true;
        var deviceBody=new StackPanel{Orientation=Orientation.Horizontal,Children={Kit.Text("",16,"DS.AccentHi").Also(t=>t.FontFamily=new FontFamily("Segoe Fluent Icons")),Kit.Text(Model.Name.Replace("EasySMX ",""),14,"DS.Text",FontWeights.SemiBold).Also(t=>t.Margin=new(10,0,8,0)),deviceState}};
        deviceControl=new Button{Content=deviceBody,Style=DS.Style("Zone.Utility"),Height=36,MinHeight=0,Padding=new(10,0,10,0),ToolTip=new StackPanel{Children={Kit.Text($"{Model.Name} · Player {Player+1}",14,"DS.Text",FontWeights.SemiBold),batteryLabel,Kit.Text("Transport · Not reported",13,"DS.TextSoft")}}};
        deviceControl.Click+=(_,_)=>ShowManagement("Device");System.Windows.Automation.AutomationProperties.SetName(deviceControl,"Selected controller status and details");
        AgentTitleHost.Children.Add(agentPill);TitlePills.Children.Add(deviceControl);TitlePills.Children.Add(more);
        // the battery, in the middle of the title bar (owner request 17 Oct 2026): only what the controller reports
        headerBattery.Click+=(_,_)=>ShowManagement("Device");System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(headerBattery,true);
        Grid.SetColumnSpan(headerBattery,2);HeaderGrid.Children.Add(headerBattery);UpdateDeviceHeader();

    }
    private void UpdateDeviceHeader()
    {
        if(deviceControl==null)return;
        double previousWidth=deviceControl.ActualWidth;string previousText=deviceState.Text;
        deviceState.Text="· "+(gameplayConnected==true?(currentBatteryLevel is "full" or "medium" or "low" or "empty" or "wired"?(currentBatteryLevel=="wired"?"Wired":batteryLabel.Text.Replace("Battery · ","")+" battery"):"Connected"):"Offline");
        deviceState.Foreground=DS.Brush(gameplayConnected==true?currentBatteryLevel is "low" or "empty"?"DS.Warn":"DS.TextSoft":"DS.Warn");
        headerBattery.Show(gameplayConnected==true?currentBatteryLevel:null);
        if(animate&&deviceControl.IsLoaded&&previousWidth>0&&previousText!=deviceState.Text){deviceControl.BeginAnimation(WidthProperty,null);deviceControl.Width=double.NaN;deviceControl.Measure(new Size(double.PositiveInfinity,36));double next=deviceControl.DesiredSize.Width;deviceControl.Width=next;deviceControl.BeginAnimation(WidthProperty,new DoubleAnimation(previousWidth,next,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop,EasingFunction=DS.EaseOut});}
    }
    /// <summary>The console-design Buttons page replaces the older canvas/inspector/shelf while Buttons is active.</summary>
    private void UpdateButtonsPage()
    {
        if (buttonsPage == null) return;
        bool on = OnButtonsPage;
        ButtonsPageHost.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) buttonsPage.CancelListening();
        if (on) { HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = Visibility.Collapsed; OverviewChoice.Visibility = Visibility.Collapsed; EnterMotion(ButtonsPageHost, 14, 220); }
        UpdateHints();var owner=InputContext;if(lastPageOwner!=owner){lastPageOwner=owner;InputContextChanged?.Invoke();}
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
        element.BeginAnimation(OpacityProperty, null); element.Opacity = 1;
        element.RenderTransform = Transform.Identity;
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
        var button = new PhysicalHotspot(region, animate) { ToolTip = Label(region.Key), Tag = region.Key, IsEnabled = !IsCurves || region.Key is "L3" or "R3" or "LT" or "RT" };
        var at = LiveController.Place(Model.Id, region); Canvas.SetLeft(button, at.X); Canvas.SetTop(button, at.Y);
        button.Click += (_, _) => { if (IsCurves) Curves.SelectChannel(region.Key switch {"L3"=>CurveChannel.LeftStick,"R3"=>CurveChannel.RightStick,"LT"=>CurveChannel.LeftTrigger,_=>CurveChannel.RightTrigger}); else if (!OpenMacroSlot(region.Key)) SelectControl(region.Key); };
        button.GotKeyboardFocus += (_, _) => { if (navigationMode) { if (IsCurves) Curves.SelectChannel(region.Key switch {"L3"=>CurveChannel.LeftStick,"R3"=>CurveChannel.RightStick,"LT"=>CurveChannel.LeftTrigger,_=>CurveChannel.RightTrigger}); else SelectControl(region.Key); } };
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
        if(ManagementPage=="Tester"){Tester?.Exit.Focus();return;}if(ManagementPage=="Device"){Device?.OpenTester.Focus();return;}if(ManagementPage=="Setups"){Setups?.NewButton.Focus();return;}
        if(IsVibration){Vibration?.FocusPreset();return;}
        if(IsMacros) {Macros?.FocusSelection();return;}
        if (SelectedControl.Length == 0) SelectControl("A");
        string key = SelectedControl is "LSTICK_ANALOG" ? "L3" : SelectedControl is "RSTICK_ANALOG" ? "R3" : SelectedControl;
        if (OnButtonsPage && buttonsPage?.Hotspot(key) is { } pageSpot) pageSpot.Focus(); else if (controls.TryGetValue(key, out var button)) button.Focus();
    }
    private void RefreshEditor()
    {
        updating = true; var region = layout.Controls.FirstOrDefault(r => r.Key == SelectedControl);
        bool macro = region?.Role == "macro", editable = region != null && Draft.CanEdit(SelectedControl);
        SelectedName.Text = region == null ? "Choose a control" : Label(SelectedControl);
        ControlDescription.Text = region == null ? "Every physical control is inspectable" : macro ? Model.Id=="x20"?"Rear paddle · Macro slot":"Rear control · Configuration role unverified" : region.Role == "axis" ? "Analog movement · Information only" : region.Role == "system" || SelectedControl is "SELECT" or "START" ? "System control · Information only" : SelectedControl.StartsWith("DPAD") ? "Directional input" : SelectedControl is "LB" or "RB" ? "Shoulder button" : SelectedControl is "LT" or "RT" ? "Analog trigger · Button output" : SelectedControl is "L3" or "R3" ? "Stick click" : "Face button";
        ControlType.Text = region == null ? "CONTROLLER-FIRST MAPPING" : macro ? Model.Id=="x20"?"REAR MACRO SLOT":"REAR CONTROL" : region.Role == "axis" ? "ANALOG AXIS · READ ONLY" : region.Role == "system" ? "SYSTEM / SPECIAL CONTROL" : "PHYSICAL INPUT";
        CurrentCaption.Text = editable ? "CURRENT · PREVIEW" : "HARDWARE STATE";
        CurrentMapping.Text = editable ? Glyph(SelectedControl) : region == null ? "—" : "Unknown";
        DraftCaption.Text = macro ? "CONTROL ROLE" : editable ? "DRAFT OUTPUT" : "SUPPORT";
        DraftMapping.Text = macro ? Model.Id=="x20"?"Macro\nslot":"Unverified" : editable ? Glyph(Draft.Target(SelectedControl)) : region == null ? "—" : "No write";
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
            choice.IsEnabled = editable; choice.Background = active ? (Brush)FindResource("Blue") : new SolidColorBrush(Color.FromArgb(117, 24, 35, 55));
            choice.BorderBrush = active ? (Brush)FindResource("Ice") : new SolidColorBrush(Color.FromArgb(54, 88, 121, 163));
        }
        foreach (Button choice in RearControls.Children) choice.Background = (string)choice.Tag == SelectedControl ? (Brush)FindResource("Blue") : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
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
        button.Effect = active ? new DropShadowEffect { Color = Color.FromRgb(78, 147, 255), BlurRadius = 22, ShadowDepth = 5, Opacity = .75 } : null;
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
    private void SetNavigationPreview(bool value)
    {
        navigationMode = value; StatusNote.Text = value ? "Navigation preview" : "Controller offline";
        if (!value && Keyboard.FocusedElement is Button focused) Lift(focused, false);
        if (!value) { HideDetachedFocus(); lastFocusCenter = null; } else FocusSelectedControl();
        foreach (var (key, button) in controls) button.Illuminate(key == SelectedControl, value);
    }
    private void MouseMode(object sender, MouseButtonEventArgs e) { SetNavigationPreview(false); }
    private void InspectAxis(object sender, RoutedEventArgs e) { string key=(string)((Button)sender).Tag;if(IsCurves)Curves.SelectChannel(key=="LSTICK_ANALOG"?CurveChannel.LeftStick:key=="RSTICK_ANALOG"?CurveChannel.RightStick:CurveChannel.LeftTrigger);else SelectControl(key); }
    private void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!updating && TargetChoice.SelectedValue is string target && Draft.CanEdit(SelectedControl)) SetDraftTarget(target); }
    private void Reset(object sender, RoutedEventArgs e) { Draft.Reset(SelectedControl); RefreshEditor(); }
    private void ResetAll(object sender, RoutedEventArgs e) { if (IsCurves) {Curves.ResetAll();} else {Draft.ResetAll(); RefreshEditor();} }
    private void ResetCurrentCurve(object sender,RoutedEventArgs e) {if(IsCurves)Curves.Reset();}
    private void MoreCurveActions(object sender,RoutedEventArgs e) {if(IsCurves && CurveMoreChoice.ContextMenu is {} menu) {menu.PlacementTarget=CurveMoreChoice;menu.IsOpen=true;}}
    private readonly Dictionary<Button,Panel> curveToolbarParents=new();
    private void PlaceCurveToolbar(bool inEditor)
    {
        foreach(var button in new[]{ResetCurveChoice,StickToolsChoice,CurveMoreChoice,TryChoice})
        {
            if(inEditor && button.Parent is Panel parent && !ReferenceEquals(parent,Curves.Toolbar)){curveToolbarParents[button]=parent;parent.Children.Remove(button);Curves.Toolbar.Children.Add(button);button.Margin=new(0,0,8,0);}
            else if(!inEditor && curveToolbarParents.TryGetValue(button,out var original)){Curves.Toolbar.Children.Remove(button);original.Children.Add(button);}
        }
    }
    public void ShowCurves(bool value)
    {
        // a page this model cannot use is refused before anything changes (it used to drop a Device or Setups
        // page onto Buttons with no tab lit, found by the stress run on 10 Oct 2026)
        HideManagement();
        if(IsVibration)ShowVibration(false);
        if(IsMacros)ShowMacros(false);
        if (value == IsCurves) return;
        if (value) previousButtonControl = SelectedControl;
        StudioBody.ColumnDefinitions[0].Width=new(value?1:1.5,GridUnitType.Star);StudioBody.ColumnDefinitions[2].Width=new(value?1.5:1,GridUnitType.Star);
        HardwareSurface.Visibility=value?Visibility.Visible:Visibility.Collapsed;IsCurves = value; ButtonsNav.IsChecked = !value; CurvesNav.IsChecked = value;
        if (value) HardwareStage.Visibility = InspectorStage.Visibility = Visibility.Visible;
        MappingHero.Visibility = value ? Visibility.Collapsed : Visibility.Visible; AssignmentOverview.Visibility = Visibility.Collapsed;
        CurveInspectorHost.Visibility = CurveShelfFrame.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        AssignmentShelf.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        OverviewChoice.Visibility=ResetAllChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        ResetCurveChoice.Visibility=CurveMoreChoice.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        StickToolsChoice.Visibility=value&&PcMode!=null?Visibility.Visible:Visibility.Collapsed;if(!value)CloseStickTools();
        HardwareShortcuts.Visibility=FrontChoice.Visibility=RearChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        foreach (var control in controls.Values) control.IsEnabled = !value || control.Region.Key is "L3" or "R3" or "LT" or "RT";
        FrontChoice.IsEnabled = !value; RearChoice.IsEnabled = !value;
        PlaceCurveToolbar(value);RootLayout.RowDefinitions[4].Height=new(value?144:184);if(value){DraftBar.Visibility=Visibility.Collapsed;RootLayout.RowDefinitions[2].Height=new(0);Apply.Visibility=StatusNote.Visibility=Visibility.Collapsed;TryChoice.Content="Keyboard test";}
        if (value) Curves.SelectChannel(Curves.Channel); else { if(previousButtonControl.Length>0) SelectControl(previousButtonControl);else ClearSelection();RefreshEditor(); }
        EnterMotion(value ? CurveInspectorHost : MappingHero, value ? 14 : -14, 220); EnterMotion(value ? CurveShelfHost : QuickTargets, value ? 14 : -14, 220); UpdateButtonsPage();
    }
    private void UpdateCurveContext()
    {
        if (!IsCurves) return;
        int count=Curves.Draft.ModifiedChannels;
        ChangeCount.Text = count==0 ? "Curve preview · No local changes" : $"{count} curve channel{(count==1?"":"s")} modified · Local draft";
        DraftChip.Text = count==0 ? "No local changes" : $"{count} local change{(count==1?"":"s")}";
        PrimaryHint.Text = " Select / Adjust"; OverviewHint.Text = " Reset curve";
        ResetCurveChoice.IsEnabled=Curves.CanReset;
        ResetAllCurvesAction.IsEnabled=Curves.CanResetAll; Apply.IsEnabled=false; SyncPcMode();
        CanvasHint.Text=Curves.Channel is CurveChannel.LeftStick or CurveChannel.RightStick ? "STICK RESPONSE" : "TRIGGER TRAVEL";
        FrontChoice.IsEnabled=RearChoice.IsEnabled=false;
    }
    private void CurvesTab(object sender,RoutedEventArgs e)=>ShowCurves(true);
    /// <summary>PC mode's settings, set by the host: the Curves tab's Advanced stick tools write to them.</summary>
    public PcModeLink? PcMode { get => pcLink; set { pcLink = value; pcSync = value != null && Draft.PcMode ? new(value, Draft, Curves.Draft, macroDraft) : null; WireCurveTuning(); SyncPcMode(); } }
    /// <summary>The Curves graph reads and edits PC mode's stick settings (dead zone, full at, anti-dead zone, max output), so
    /// it draws exactly what games get, including what Agent XCTL changed.</summary>
    private void WireCurveTuning()
    {
        if (pcLink is not { } link || !Draft.PcMode) { Curves.Tuning = null; Curves.SaveTuning = null; Curves.ResetResponse = null; Curves.Refresh(); return; }
        Curves.ResetResponse = channels =>
        {
            try
            {
                var next = X20Ctl.Zone.PcMode.PcModeDrafts.ResetCurves(link.Store.Get(PcModePage.StoreKey), channels);
                link.Store.Save(PcModePage.StoreKey, next); link.Service.Reload(); pcSync?.Reload();
                return null;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { return "Couldn't reset curves: " + error.Message; }
        };
        Curves.Tuning = channel => link.Store.Get(PcModePage.StoreKey).Sticks.GetValueOrDefault(channel.ToString());
        Curves.SaveTuning = (channel, tuning) =>
        {
            var p = link.Store.Get(PcModePage.StoreKey);
            if (tuning.IsDefault) p.Sticks.Remove(channel.ToString()); else p.Sticks[channel.ToString()] = tuning;
            try { p.Validate(); }
            catch (InvalidDataException error) { return "Not applied: " + error.Message; }
            if (!p.IsNeutral && !PcModePage.MaySave(link.Preferences)) return "Open the PC mode tab and read the notice to start applying this.";
            try { link.Store.Save(PcModePage.StoreKey, p); link.Service.Reload(); ChangeCount.Text = "PC mode · Saved · Applies right away"; return null; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return "Couldn't save: " + error.Message; }
        };
        Curves.Refresh();
    }
    private PcModeLink? pcLink;
    private PcModeSync? pcSync;
    /// <summary>The PC mode tab's switch changed: the host reopens the Studio so the tabs unlock (or lock).</summary>
    public event Action? PcModeSwitched;
    /// <summary>Saves the tabs to PC mode when it is on, and says so in the status bar.</summary>
    private void SyncPcMode()
    {
        if (pcSync == null || ManagementPage != null) return;
        pcSync.Save(); ChangeCount.Text = pcSync.Status; DraftChip.Text = "PC mode";
    }
    private void PcProfileChanged()
    {
        if (pcSync == null) return;
        pcSync.Reload(); Curves.Refresh(); Macros?.Refresh(); RefreshEditor(); UpdateCurveContext(); UpdateMacroContext();
    }
    private void PcNoticeAccepted() => pcSync?.Save();
    private void PcModeTab(object sender, RoutedEventArgs e) => ShowManagement("PcMode");
    public StickToolsPanel? StickTools { get; private set; }
    private Grid? stickToolsLayer;
    private void OpenStickTools(object sender,RoutedEventArgs e)=>OpenStickTools();
    /// <summary>Curves → Advanced: the fine stick tools, over the Studio until Done (or B / Esc).</summary>
    public void OpenStickTools()
    {
        if(PcMode==null||stickToolsLayer!=null)return;
        StickTools=new StickToolsPanel(PcMode,CloseStickTools);
        stickToolsLayer=new Grid{Background=new SolidColorBrush(Color.FromArgb(200,3,6,16)),Children={StickTools}};
        Grid.SetZIndex(stickToolsLayer,50);StudioShell.Children.Add(stickToolsLayer);Fx.Appear(stickToolsLayer,1.03,240);StickTools.Done.Focus();
    }
    public void CloseStickTools()
    {
        if(stickToolsLayer==null)return;var leaving=stickToolsLayer;stickToolsLayer=null;StickTools=null;
        Fx.Leave(leaving,()=>StudioShell.Children.Remove(leaving),1.02,200);if(IsCurves)StickToolsChoice.Focus();
    }
    private void ButtonsTab(object sender,RoutedEventArgs e) => ShowButtons();
    /// <summary>Open the Buttons page from anywhere (tab, Dashboard hero, review harnesses).</summary>
    public void ShowButtons() { ShowCurves(false);if(IsMacros)ShowMacros(false);if(IsVibration)ShowVibration(false);HideManagement();ButtonsNav.IsChecked=true;FrontChoice.IsEnabled=true;RearChoice.IsEnabled=Model.Id=="x20";UpdateButtonsPage(); }
    public DashboardConsolePage? Dashboard {get;private set;}
    private DashboardConsolePage CreateDashboard(){var page=new DashboardConsolePage(Model,Player,Draft.UnsentChanges,setupStore.Items.Count(s=>s.ModelId==Model.Id),!animate,setupStore.Items.Where(s=>s.ModelId==Model.Id).ToArray(),id=>LoadSetup(setupStore.NativeSnapshot(id)));page.SetBatteryLevel(currentBatteryLevel);if(gameplayConnected is bool connected)page.SetInputConnected(connected);page.ConfigureRequested+=ShowButtons;page.PageRequested+=target=>{if(target=="Buttons")ShowButtons();else if(target=="Curves")ShowCurves(true);else if(target=="Macros")ShowMacros(true);else ShowManagement(target);};return page;}
    private void DashboardTab(object sender,RoutedEventArgs e)=>ShowManagement("Dashboard");
    public void ShowMacros(bool value)
    {
        HideManagement();
        if(IsVibration)ShowVibration(false);
        if(value==IsMacros)return;
        if(value && IsCurves)ShowCurves(false);
        IsMacros=value;MacrosNav.IsChecked=value;ButtonsNav.IsChecked=!value;CurvesNav.IsChecked=false;
        HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        MacroBodyHost.Visibility=value?Visibility.Visible:Visibility.Collapsed;MacroDockFrame.Visibility=Visibility.Collapsed;Grid.SetRowSpan(StudioBody,value?2:1);
        ResetCurveChoice.Visibility=CurveMoreChoice.Visibility=Visibility.Collapsed;CurveInspectorHost.Visibility=CurveShelfFrame.Visibility=Visibility.Collapsed;
        if(value)
        {
            if(Macros==null) {Macros=new(macroDraft,layout,this,RegisterFocus,!animate,Model.Id);ModelProfiles.Wear(Macros.Body,Model.Id,"Macros",Draft.PcMode);MacroBodyHost.Children.Add(Macros.Body);Macros.Changed+=UpdateMacroContext;}
            UpdateMacroContext();EnterMotion(MacroBodyHost,14,220);
        }
        else {Macros?.StopPreview();RefreshEditor();}UpdateButtonsPage();
    }
    private void UpdateMacroContext()
    {
        if(!IsMacros || Macros==null)return;
        ChangeCount.Text=$"{macroDraft.ModifiedSlots} local sequence{(macroDraft.ModifiedSlots==1?"":"s")} · Hardware contents unknown";
        DraftChip.Text="Local macro drafts";PrimaryHint.Text=" Select / Edit";OverviewHint.Text=" Add event";Apply.IsEnabled=false;SyncPcMode();
    }
    private void MacrosTab(object sender,RoutedEventArgs e)=>ShowMacros(true);
    public void ShowVibration(bool value)
    {
        HideManagement();
        if(value==IsVibration)return;
        if(value && IsCurves)ShowCurves(false);if(value && IsMacros)ShowMacros(false);
        IsVibration=value;VibrationNav.IsChecked=value;ButtonsNav.IsChecked=!value;CurvesNav.IsChecked=MacrosNav.IsChecked=false;
        HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        VibrationBodyHost.Visibility=VibrationShelfFrame.Visibility=VibrationResetChoice.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        ResetCurveChoice.Visibility=CurveMoreChoice.Visibility=Visibility.Collapsed;
        RootLayout.RowDefinitions[4].Height=new(value?54:184);
        if(value){DraftBar.Visibility=Visibility.Collapsed;RootLayout.RowDefinitions[2].Height=new(0);Apply.Visibility=StatusNote.Visibility=Visibility.Collapsed;if(Vibration==null){Vibration=new(vibrationDraft,this,RegisterFocus,animate,Model.Id);ModelProfiles.Wear(Vibration.Body,Model.Id,"Vibration");VibrationBodyHost.Children.Add(Vibration.Body);VibrationShelfHost.Children.Add(Vibration.Shelf);Vibration.Changed+=UpdateVibrationContext;}UpdateVibrationContext();EnterMotion(VibrationBodyHost,14,220);EnterMotion(VibrationShelfHost,14,220);}
        else RefreshEditor();UpdateButtonsPage();
    }
    private void UpdateVibrationContext(){if(!IsVibration)return;ChangeCount.Text=vibrationDraft.Modified?"Linked vibration strength modified · Local draft":"Vibration preview · No local changes";DraftChip.Text="Local vibration draft";PrimaryHint.Text=" Adjust strength";OverviewHint.Text=" Reset draft";VibrationResetChoice.IsEnabled=vibrationDraft.Modified;Apply.IsEnabled=false;}
    private void VibrationTab(object sender,RoutedEventArgs e)=>ShowVibration(true);
    private void ResetVibration(object sender,RoutedEventArgs e)=>Vibration?.Reset();
    public void ShowManagement(string page)
    {
        DraftBar.Visibility=page=="Dashboard"?Visibility.Collapsed:Visibility.Visible;RootLayout.RowDefinitions[2].Height=new(page=="Dashboard"?0:40);
        Apply.Visibility=StatusNote.Visibility=page=="Dashboard"?Visibility.Collapsed:Visibility.Visible;ChangeCount.Visibility=page=="Dashboard"?Visibility.Collapsed:Visibility.Visible;
        if(page is not ("Dashboard" or "PcMode" or "Device" or "Diagnostics" or "Mouse" or "Tester" or "Setups"))throw new ArgumentException("Unknown Studio page.");
        if(IsCurves)ShowCurves(false);if(IsMacros)ShowMacros(false);if(IsVibration)ShowVibration(false);HideManagement();ManagementPage=page;bool immediate=page is "Dashboard" or "Mouse" or "Diagnostics" or "Device" or "PcMode";DraftBar.Visibility=immediate?Visibility.Collapsed:Visibility.Visible;RootLayout.RowDefinitions[2].Height=new(immediate?0:40);Apply.Visibility=StatusNote.Visibility=immediate?Visibility.Collapsed:Visibility.Visible;ChangeCount.Visibility=page=="Dashboard"?Visibility.Collapsed:Visibility.Visible;
        HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=Visibility.Collapsed;
        ManagementBodyHost.Visibility=ManagementShelfFrame.Visibility=Visibility.Visible;ManagementBodyHost.Children.Clear();ManagementShelfHost.Children.Clear();
        ButtonsNav.IsChecked=CurvesNav.IsChecked=MacrosNav.IsChecked=VibrationNav.IsChecked=false;DeviceNav.IsChecked=page=="Device";DiagnosticsNav.IsChecked=page=="Diagnostics";MouseNav.IsChecked=page=="Mouse";PcModeNav.IsChecked=page=="PcMode";TesterNav.IsChecked=page=="Tester";SetupsNav.IsChecked=page=="Setups";DashboardNav.IsChecked=page=="Dashboard";
        if(page=="Dashboard"){Dashboard??=CreateDashboard();Dashboard.SetWorkingState(Draft.UnsentChanges,activeSetup==null?null:setupStore.Items.FirstOrDefault(x=>x.Id==activeSetup)?.Name,Draft.PcMode);ManagementBodyHost.Children.Add(Dashboard);ChangeCount.Text="Select the controller to configure it · Your games are found automatically";PrimaryHint.Text=" Select";OverviewHint.Text="";DraftChip.Text="Dashboard";Dashboard.FocusHero();}
        else if(page=="Device"){Device??=new(Model,Player,this,RegisterFocus,()=>ShowManagement("Diagnostics"),()=>ShowManagement("Setups"));Device.SetBatteryLevel(currentBatteryLevel);if(gameplayConnected is {} deviceConnected)Device.SetConnection(deviceConnected);Device.UpdateEngineStatus(NativeEngineStatus);ManagementBodyHost.Children.Add(Device.Body);ManagementShelfHost.Children.Add(Device.Shelf);ChangeCount.Text="Assigned model · Gameplay and configuration remain separate";PrimaryHint.Text=" Open";OverviewHint.Text=" My setups";DraftChip.Text="Controller overview";}
        else if(page=="Diagnostics"){Diagnostics??=new(Player,RegisterFocus,PcMode,model:Model);ManagementBodyHost.Children.Add(Diagnostics.Body);Diagnostics.Start();ChangeCount.Text="Read-only input capture · Use Esc or the mouse to leave";PrimaryHint.Text="";OverviewHint.Text="";DraftChip.Text="Diagnostics";Diagnostics.Reset.Focus();}
        else if(page=="PcMode")
        {
            if(PcMode==null)ManagementBodyHost.Children.Add(Kit.Text("PC mode isn't available in this window.",16,"DS.TextSoft"));
            else
            {
                var back=new Button{Style=DS.Style("ActionButton"),Height=46,MinHeight=0,Padding=new(22,0,24,0),Content=Kit.Text("Back to Dashboard",15.5,"DS.Text",FontWeights.SemiBold)};back.Click+=(_,_)=>ShowManagement("Dashboard");RegisterFocus(back);
                var pc=new PcModePage(PcMode.Service,PcMode.Store,PcMode.Preferences,back,ShowButtons){Margin=new(30,6,30,24)};pc.Switched+=_=>PcModeSwitched?.Invoke();
                ManagementBodyHost.Children.Add(new ScrollViewer{Content=pc,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});pc.Switch.Focus();
            }
            ChangeCount.Text=Draft.PcMode?"PC mode is on · Buttons, Curves and Macros apply on this PC":"PC mode is off · Switch it on to edit this controller's settings on the PC";PrimaryHint.Text=" Select";OverviewHint.Text="";DraftChip.Text="PC mode";
        }
        else if(page=="Mouse"){if(MouseService==null){ManagementBodyHost.Children.Add(Kit.Text("Mouse mode isn't available in this window.",16,"DS.TextSoft"));}else{Mouse??=new(MouseService,RegisterFocus,Player,Model,layout);ManagementBodyHost.Children.Add(Mouse.Body);Mouse.Switch.Focus();}ChangeCount.Text="Controller as mouse · Saved on this PC";PrimaryHint.Text=" Select";OverviewHint.Text="";DraftChip.Text="Mouse";}
        else if(page=="Tester"){Tester??=new(Model,layout,this,RegisterFocus,()=>ShowManagement("Device"),animate);Tester.Enter();ManagementBodyHost.Children.Add(Tester.Body);ManagementShelfHost.Children.Add(Tester.Shelf);ChangeCount.Text="Tester owns gameplay input · UI input navigation paused";PrimaryHint.Text=" Exit Tester";OverviewHint.Text="";DraftChip.Text="Gameplay tester";navigationBeforeTester=navigationMode;navigationMode=true;Tester.Exit.Focus();}
        else {if(Setups==null){Setups=new(setupStore,new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))),Model.Id,this,RegisterFocus,()=>SetupSnapshot.Capture(Model.Id,Draft,Curves.Draft,macroDraft,vibrationDraft),LoadSetup,legacySources,activeSetup);Setups.Changed+=()=>{Dashboard=null;UpdateSetupContext();};}Setups.RefreshCurrent();ManagementBodyHost.Children.Add(Setups.Body);ManagementShelfHost.Children.Add(Setups.Shelf);UpdateSetupContext();}
        PrimaryGlyph.Text=page=="Tester"?"↵":"A";BackGlyph.Text=page is "Tester" or "Diagnostics"?"Esc":"B";SecondaryFooterHint.Visibility=ActualWidth<1100 || page is "Tester" or "Diagnostics"?Visibility.Collapsed:Visibility.Visible;
        Grid.SetRowSpan(StudioBody,page is "Setups" or "Dashboard" or "Diagnostics" or "Mouse" or "PcMode" or "Device"?2:1);if(page is "Setups" or "Dashboard" or "Diagnostics" or "Mouse" or "PcMode" or "Device")ManagementShelfFrame.Visibility=Visibility.Collapsed;InputContextChanged?.Invoke();Apply.IsEnabled=false;if(IsLoaded || page!="Dashboard"){EnterMotion(ManagementBodyHost,14,220);EnterMotion(ManagementShelfHost,14,220);}UpdateButtonsPage();
    }
    private void HideManagement(){RootLayout.RowDefinitions[4].Height=new(184);DraftBar.Visibility=Visibility.Visible;RootLayout.RowDefinitions[2].Height=new(40);Apply.Visibility=ChangeCount.Visibility=Visibility.Visible;StopTry();if(ManagementPage==null)return;if(ManagementPage=="Diagnostics")Diagnostics?.Stop();Grid.SetRowSpan(StudioBody,1);if(ManagementPage=="Tester")navigationMode=navigationBeforeTester;Tester?.ExitCapture();ManagementPage=null;InputContextChanged?.Invoke();PrimaryGlyph.Text="A";BackGlyph.Text="B";SecondaryFooterHint.Visibility=ActualWidth<1100?Visibility.Collapsed:Visibility.Visible;ManagementBodyHost.Children.Clear();ManagementShelfHost.Children.Clear();ManagementBodyHost.Visibility=ManagementShelfFrame.Visibility=Visibility.Collapsed;DeviceNav.IsChecked=DiagnosticsNav.IsChecked=MouseNav.IsChecked=PcModeNav.IsChecked=TesterNav.IsChecked=SetupsNav.IsChecked=DashboardNav.IsChecked=false;HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=Visibility.Visible;UpdateButtonsPage();}
    private void LoadSetup(SetupSnapshot snapshot){if(snapshot.ModelId!=Model.Id)throw new InvalidOperationException("Setup model does not match this player.");snapshot.LoadInto(Draft,Curves.Draft,macroDraft,vibrationDraft);Curves.Refresh();Macros?.Refresh();Vibration?.Refresh();SyncPcMode();}
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
        if (!OnButtonsPage) return;
        tryDiagnostics = new Diagnostics.DiagnosticsWorkbench(Player, _ => {}, compact: true,model:Model);
        var close = new Button { Content = "Close test", Style = DS.Style("ActionButton"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new(16) };
        close.Click += (_, _) => StopTry();
        var content = new Grid(); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new());
        var heading = new DockPanel(); DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new StackPanel { Margin = new(24,16,0,8), Children = { Kit.Text("Try your controller", 24, "DS.Text", FontWeights.SemiBold), Kit.Text("Roll both sticks around their edge · error measures deviation from a circle, not measurement uncertainty", 13, "DS.TextSoft") } });
        content.Children.Add(heading); Grid.SetRow(tryDiagnostics.Body, 1); content.Children.Add(tryDiagnostics.Body);
        tryLayer = new Grid { Background = new SolidColorBrush(Color.FromArgb(210, 3, 6, 16)), Children = { new Border { Background = new SolidColorBrush(Color.FromRgb(10,18,38)), BorderBrush = DS.Brush("DS.AccentHi"), BorderThickness = new(1), CornerRadius = new(22), Margin = new(40,100,40,110), Child = content } } };
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
    public void ReleaseTransientInput() { if (trial == null) return; trial.Clear(); PushTrialFrame(); }
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
        gameplayConnected=connected;UpdateDeviceHeader();
        controllerConnection.Set(connected ? "Live input" : "Offline", connected ? ChipKind.Success : ChipKind.Neutral);
        GameplayConnection.Text=connected?"● Live input":"● No input";
        GameplayConnection.Foreground=(Brush)FindResource(connected?"Ice":"Amber");buttonsPage?.SetConnected(connected);
        Dashboard?.SetInputConnected(connected);Device?.SetConnection(connected);if(!connected){Curves.ShowLive(-1);StickTools?.ClearLive();}
    }
    public void ObserveLiveInput(double lx, double ly, double rx, double ry, double lt, double rt)
    {
        StickTools?.ShowLive(lx,ly,rx,ry);
        // the Curves graph follows the real stick or trigger whenever a controller is connected; Try it is the keyboard's
        if (IsCurves && ManagementPage == null)
        {
            Curves.ShowLive(Curves.Channel switch { CurveChannel.LeftStick => Math.Sqrt(lx * lx + ly * ly), CurveChannel.RightStick => Math.Sqrt(rx * rx + ry * ry), CurveChannel.LeftTrigger => lt, _ => rt });
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
            case "Device": return false;
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
        if(page=="Mouse"||CapturingButtonEditor){LiveHints.Children.Clear();LiveHints.Children.Add(GlyphHint.Footer(("Esc","Back")));return;}
        var map = InputMap.For(page, focus, IsTrying);
        var hints = new List<(string, string)>();
        if (map.A != null) hints.Add(("A", map.A));
        hints.Add((page=="Diagnostics"?"Esc":"B", map.B));
        if (map.X != null) hints.Add(("X", map.X));
        if (map.Y != null) hints.Add(("Y", map.Y));
        if (map.Triggers != null) hints.Add(("LT/RT", map.Triggers));
        if (page is not ("Tester" or "Diagnostics")) hints.Add(("LB/RB", "Tabs"));
        LiveHints.Children.Clear(); LiveHints.Children.Add(GlyphHint.Footer(hints.ToArray()));
    }

    /// <summary>Rear paddles are programmable macro slots, not independent buttons: open the slot in Macros.</summary>
    private bool OpenMacroSlot(string key)
    {
        if (!macroDraft.SlotNames.Contains(key)) return false;
        StopTry(); ShowMacros(true); Macros?.SelectSlot(key); return Macros != null;
    }
    private void DeviceTab(object sender,RoutedEventArgs e)=>ShowManagement("Device");private void TesterTab(object sender,RoutedEventArgs e)=>ShowManagement("Tester");private void DiagnosticsTab(object sender,RoutedEventArgs e)=>ShowManagement("Diagnostics");private void MouseTab(object sender,RoutedEventArgs e)=>ShowManagement("Mouse");private void SetupsTab(object sender,RoutedEventArgs e)=>ShowManagement("Setups");
    private void Front(object sender, RoutedEventArgs e) { ShowController(false); ClearSelection(); }
    private void Rear(object sender, RoutedEventArgs e) { ShowController(true); ClearSelection(); }
    private void Back(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    public void OnKeyUp(object sender,KeyEventArgs e){if(trial!=null && TrialKey(e.Key) is {} up){trial.Release(up);PushTrialFrame();e.Handled=true;return;}if(ManagementPage=="Tester" && Tester?.HandleKeyboard(e.Key,false)==true)e.Handled=true;}
    /// <summary>The Buttons page is waiting for "the button you want" or "what it should do": every press is captured.</summary>
    public bool CapturingRemap => OnButtonsPage && buttonsPage != null && buttonsPage.Listening != ButtonsConsolePage.Listen.None;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        // the Advanced stick tools own the keyboard while open (arrows nudge their sliders); Esc closes them
        if (stickToolsLayer != null) { if (e.Key == Key.Escape) { CloseStickTools(); e.Handled = true; } return; }
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
        if(ManagementPage=="Mouse"){if(e.Key==Key.Escape){ShowManagement("Dashboard");e.Handled=true;}return;}
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
        // LB/RB walk the tabs: Dashboard, Buttons, Curves, Macros, Vibration, PC mode, Device, Mouse, Setups (the Tester is no longer a tab)
        if(e.Key is Key.PageDown or Key.PageUp && !e.IsRepeat)
        {
            int step=e.Key==Key.PageDown?1:-1;int index=ManagementPage switch {"Dashboard"=>0,"PcMode"=>5,"Device"=>6,"Diagnostics"=>6,"Mouse"=>7,"Setups"=>8,_=>IsVibration?4:IsMacros?3:IsCurves?2:1};
            int next=Math.Clamp(index+step,0,8);
            // a model X20CTL can't write skips the tabs it can't use; with PC mode on only Vibration stays out of reach
            while(Model.Id!="x20"&&(next==4||!Draft.PcMode&&next is 2 or 3))next+=step;
            if(next==0)ShowManagement("Dashboard");else if(next>=5)ShowManagement(next switch {5=>"PcMode",6=>"Device",7=>"Mouse",_=>"Setups"});else if(next==4)ShowVibration(true);else if(next==3)ShowMacros(true);else if(next==2)ShowCurves(true);else ShowButtons();e.Handled=true;return;
        }
        if (e.Key == Key.F6) { if (!e.IsRepeat) SetNavigationPreview(!navigationMode); e.Handled = true; return; }
        if (e.Key == Key.B && sender is ProductHost && !navigationMode && OnButtonsPage && buttonsPage != null && SelectedControl.Length > 0 && !AssignmentsVisible) { ClearSelection(); e.Handled = true; return; }
        if (e.Key == Key.Escape || navigationMode && e.Key == Key.B) { if (AssignmentsVisible) ShowAssignments(false);
            // B on Buttons with a control picked puts the card back to "Select a control" (owner request 17 Oct 2026); a second B leaves
            else if (OnButtonsPage && buttonsPage != null && SelectedControl.Length > 0) { string was = SelectedControl; ClearSelection(); if (navigationMode) buttonsPage.Hotspot(was)?.Focus(); }
            else if(IsVibration && ReferenceEquals(Vibration?.Strength,Keyboard.FocusedElement))FocusSelectedControl(); else if (IsCurves && (Curves.PointHandles.Any(p=>ReferenceEquals(p,Keyboard.FocusedElement)) || ReferenceEquals(Curves.Inner,Keyboard.FocusedElement) || ReferenceEquals(Curves.Outer,Keyboard.FocusedElement))) FocusSelectedControl(); else if (navigationMode && Keyboard.FocusedElement is Button && Keyboard.FocusedElement is not PhysicalHotspot) FocusSelectedControl(); else BackRequested?.Invoke(); e.Handled = true; return; }
        if (navigationMode && e.Key == Key.Y) { if(ManagementPage=="Device")return;else if(ManagementPage=="Setups")Setups?.NewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else if(IsVibration)Vibration?.Reset();else if(IsMacros)Macros?.Add();else if(IsCurves) Curves.Reset();else ShowAssignments(!AssignmentsVisible); e.Handled = true; return; }
        FocusNavigationDirection? direction = e.Key switch { Key.Up => FocusNavigationDirection.Up, Key.Down => FocusNavigationDirection.Down, Key.Left => FocusNavigationDirection.Left, Key.Right => FocusNavigationDirection.Right, _ => null };
        if (direction.HasValue)
        {
            navigationMode = true;
            if (Keyboard.FocusedElement is PhysicalHotspot current)
            {
                var p = current.Region.Bounds; double x = p.X + p.Width / 2, y = p.Y + p.Height / 2;
                var candidates = (OnButtonsPage && buttonsPage != null ? buttonsPage.Hotspots : (IEnumerable<PhysicalHotspot>)controls.Values).Where(c => c != current && c.IsEnabled && c.IsVisible).Select(c => new { Button = c, Dx = c.Region.Bounds.X + c.Region.Bounds.Width / 2 - x, Dy = c.Region.Bounds.Y + c.Region.Bounds.Height / 2 - y });
                candidates = direction.Value switch { FocusNavigationDirection.Right => candidates.Where(c => c.Dx > .002), FocusNavigationDirection.Left => candidates.Where(c => c.Dx < -.002), FocusNavigationDirection.Down => candidates.Where(c => c.Dy > .002), _ => candidates.Where(c => c.Dy < -.002) };
                var next = candidates.OrderBy(c => direction.Value is FocusNavigationDirection.Left or FocusNavigationDirection.Right ? Math.Abs(c.Dx) + Math.Abs(c.Dy) * 1.8 : Math.Abs(c.Dy) + Math.Abs(c.Dx) * 1.8).FirstOrDefault();
                if (next != null) { next.Button.Focus(); SelectControl(next.Button.Region.Key); }
                else if (direction == FocusNavigationDirection.Down) FocusCurrentTarget();
            }
            else if (Keyboard.FocusedElement is UIElement element) element.MoveFocus(new(direction.Value));
            e.Handled = true; return;
        }
        if (navigationMode && e.Key is Key.Enter or Key.A)
        {
            if (!e.IsRepeat && Keyboard.FocusedElement is PhysicalHotspot && IsCurves) Curves.Buttons.FirstOrDefault()?.Focus();
            else if (!e.IsRepeat && Keyboard.FocusedElement is PhysicalHotspot && Draft.CanEdit(SelectedControl)) FocusCurrentTarget();
            else if (!e.IsRepeat && Keyboard.FocusedElement is Button button && button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
        }
    }
    private void FocusCurrentTarget() { if (OnButtonsPage && buttonsPage != null) { buttonsPage.FocusOutput(Draft.Target(SelectedControl)); return; } targetButtons.FirstOrDefault(b => b.IsEnabled && (string)b.Tag == Draft.Target(SelectedControl))?.Focus(); }
    private static string TargetGlyph(string key) => key switch { "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", _ => Glyph(key) };
    private static string Glyph(string key) => key switch { "DPAD_UP" => "D↑", "DPAD_DOWN" => "D↓", "DPAD_LEFT" => "D←", "DPAD_RIGHT" => "D→", "SELECT" => "View", "START" => "Menu", "HOME" => "Guide", "CAPTURE" => "C", "TURBO" => "T", _ => key };
    private static string Label(string key) => key switch { "DPAD_UP" => "D-pad Up", "DPAD_DOWN" => "D-pad Down", "DPAD_LEFT" => "D-pad Left", "DPAD_RIGHT" => "D-pad Right", "SELECT" => "View / Back", "START" => "Menu / Start", "HOME" => "Home / Guide", "CAPTURE" => "Capture · C", "TURBO" => "Turbo · T", "LSTICK_ANALOG" => "Left stick axis", "RSTICK_ANALOG" => "Right stick axis", _ => key };
    private void Minimize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) SystemCommands.MinimizeWindow(w); }
    private void Maximize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; } // fullscreen or the fixed window
    private void Close(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
