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
    public InputOwner InputContext=>ManagementPage=="Tester"?InputOwner.TesterCapture:InputOwner.UiNavigation;
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
        Unloaded+=(_,_)=>Tester?.ExitCapture();
        macroDraft=macros ?? new MacroDraft(model.Id);vibrationDraft=vibration??new(model.Id);
        animate = !reducedMotion && SystemParameters.ClientAreaAnimation;
        ControllerContext.Text = $"{model.Name} · Player {player + 1}";
        layout = ControlRegions.Owned(model, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/x20-rear.json")));
        TargetChoice.ItemsSource = ButtonDraft.Targets.Select(key => new KeyValuePair<string, string>(key, Label(key))).ToArray();
        RearChoice.IsEnabled = model.Id == "x20";
        CurvesNav.ToolTip = model.Id == "x20" ? "Local curve drafts · Page Down" : "Curve preview · read-only for this model · Page Down";
        BuildTargets(); BuildInventory();
        BuildTitlePills();
        buttonsPage = new ButtonsConsolePage(model, layout, draft, Label, animate, SelectControl, OpenMacroSlot, () => { if (IsTrying) StopTry(); else StartTry(); });
        buttonsPage.Changed += RefreshEditor; ButtonsPageHost.Children.Add(buttonsPage);
        PreviewGotKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(UpdateHints, System.Windows.Threading.DispatcherPriority.Background);
        foreach (var h in buttonsPage.Hotspots) RegisterFocus(h);
        Curves = new(curveDraft ?? new CurveDraft(model.Id), this,animate); ModelProfiles.Wear(Curves.Inspector, model.Id, "Curves"); CurveInspectorHost.Children.Add(Kit.Panel(null, null, null, Curves.Inspector)); CurveShelfHost.Children.Add(Curves.Shelf);
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
            RootLayout.RowDefinitions[1].Height = new(compact ? 44 : expanded?54:48);
            RootLayout.RowDefinitions[2].Height = new(expanded?44:40);
            RootLayout.RowDefinitions[4].Height = new(compact ? 170 : expanded?206:184);
            RootLayout.RowDefinitions[5].Height = new(expanded?78:66);
            SecondaryFooterHint.Visibility=ActualWidth<1100 || ManagementPage=="Tester"?Visibility.Collapsed:Visibility.Visible;
            StatusNote.Visibility=ActualWidth<1350?Visibility.Collapsed:Visibility.Visible;
            PrimaryHint.FontSize=OverviewHint.FontSize=expanded?16:14;
            ChangeCount.FontSize=expanded?17:15;FeatureNote.FontSize=expanded?17:15;ControlDescription.FontSize=expanded?17:15;CanvasHint.FontSize=expanded?16:14;
            InspectorStage.Margin = compact ? new(0,8,0,4) : new(0,24,0,8);
            UpdateGlyphScale();
            foreach (Button target in targetButtons){target.Height=compact?42:expanded?54:(string)target.Tag is "A" or "B" or "X" or "Y" ?50:48;target.FontSize=expanded?26:23;}
        };
        ShowController(false); ClearSelection(); if (layout.Controls.Any(c => c.Key == "A")) SelectControl("A"); UpdateButtonsPage(); ShowManagement("Dashboard");
    }
    private readonly ButtonsConsolePage? buttonsPage;
    /// <summary>Title-bar pills: Support (Ko-fi, opens only when chosen) in the same quiet glass as the Zone, and the controller name.</summary>
    private void BuildTitlePills()
    {
        var heart = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) };
        var support = new Button { Cursor = Cursors.Hand, ToolTip = "Support X20CTL on Ko-fi (opens your browser)", FocusVisualStyle = null, Margin = new(0, 0, 12, 0),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            Content = new Border { CornerRadius = new(17), Height = 34, Padding = new(14, 0, 18, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 150, 170)),
                Background = new LinearGradientBrush(Color.FromRgb(232, 54, 96), Color.FromRgb(196, 30, 78), 0), Effect = new DropShadowEffect { Color = Color.FromRgb(232, 54, 96), BlurRadius = 18, ShadowDepth = 0, Opacity = .5 },
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { heart, new TextBlock { Text = "Support", FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } } } } };
        support.Click += (_, _) => SupportRequested?.Invoke();
        var pill = (Border)support.Content; support.MouseEnter += (_, _) => pill.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)); support.MouseLeave += (_, _) => pill.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 150, 170));
        var name = new Border { CornerRadius = new(17), Height = 34, Padding = new(14, 0, 16, 0), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(80, 125, 182, 255)), Background = new SolidColorBrush(Color.FromArgb(60, 30, 60, 130)),
            Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Foreground = DS.Brush("DS.AccentHi"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) }, new TextBlock { Text = Model.Name.Replace("EasySMX ", ""), FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } } } };
        TitlePills.Children.Add(support); TitlePills.Children.Add(name);
    }
    /// <summary>The console-design Buttons page replaces the older canvas/inspector/shelf while Buttons is active.</summary>
    private void UpdateButtonsPage()
    {
        if (buttonsPage == null) return;
        bool on = OnButtonsPage;
        ButtonsPageHost.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) buttonsPage.CancelListening();
        if (on) { HardwareStage.Visibility = InspectorStage.Visibility = AssignmentShelf.Visibility = Visibility.Collapsed; OverviewChoice.Visibility = Visibility.Collapsed; EnterMotion(ButtonsPageHost, 14, 220); }
        UpdateHints();
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
        if (!animate) return;
        var slide = new TranslateTransform(); element.RenderTransform = slide;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        // page changes slide further and fade from clear, so every tab switch reads as a move, not a cut
        slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(distance * 2.2, 0, TimeSpan.FromMilliseconds(milliseconds + 90)) { EasingFunction = ease });
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds + 60)));
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
        if(ManagementPage=="Tester"){Tester?.Exit.Focus();return;}if(ManagementPage=="Device"){Device?.OpenSetups.Focus();return;}if(ManagementPage=="Setups"){Setups?.NewButton.Focus();return;}
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
        FeatureNote.Text = Model.Id != "x20" ? "Configuration is unverified for this model. You can still inspect its controls." : region == null ? "Select a button on the controller. Inspect shoulders and paddles on the back." : macro ? $"{SelectedControl} is a controller-side macro slot. Editing is unavailable; hardware state is unknown." : region.Role == "axis" ? "Movement and click share this stick cap. Analog settings and live input are unavailable here." : !editable ? "This control is present. Remapping is unverified and no write is available." : "Choose an output on the shelf. This draft stays local; hardware settings are unknown.";
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
        Apply.IsEnabled = false; updating = false;
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
        button.GotKeyboardFocus += (_, _) => { if (navigationMode) { ShowDetachedFocus(button); Lift(button, true); } };
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
    private void ResetAll(object sender, RoutedEventArgs e) { if (IsCurves) {Curves.Draft.ResetAll();Curves.Refresh();UpdateCurveContext();} else {Draft.ResetAll(); RefreshEditor();} }
    private void ResetCurrentCurve(object sender,RoutedEventArgs e) {if(IsCurves)Curves.Reset();}
    private void MoreCurveActions(object sender,RoutedEventArgs e) {if(IsCurves && CurveMoreChoice.ContextMenu is {} menu) {menu.PlacementTarget=CurveMoreChoice;menu.IsOpen=true;}}
    public void ShowCurves(bool value)
    {
        // a page this model cannot use is refused before anything changes (it used to drop a Device or Setups
        // page onto Buttons with no tab lit, found by the stress run on 10 Oct 2026)
        HideManagement();
        if(IsVibration)ShowVibration(false);
        if(IsMacros)ShowMacros(false);
        if (value == IsCurves) return;
        if (value) previousButtonControl = SelectedControl;
        IsCurves = value; ButtonsNav.IsChecked = !value; CurvesNav.IsChecked = value;
        if (value) HardwareStage.Visibility = InspectorStage.Visibility = Visibility.Visible;
        MappingHero.Visibility = value ? Visibility.Collapsed : Visibility.Visible; AssignmentOverview.Visibility = Visibility.Collapsed;
        CurveInspectorHost.Visibility = CurveShelfFrame.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        AssignmentShelf.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        OverviewChoice.Visibility=ResetAllChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        ResetCurveChoice.Visibility=CurveMoreChoice.Visibility=value?Visibility.Visible:Visibility.Collapsed;
        HardwareShortcuts.Visibility=FrontChoice.Visibility=RearChoice.Visibility=value?Visibility.Collapsed:Visibility.Visible;
        foreach (var control in controls.Values) control.IsEnabled = !value || control.Region.Key is "L3" or "R3" or "LT" or "RT";
        FrontChoice.IsEnabled = !value; RearChoice.IsEnabled = !value;
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
        ResetCurveChoice.IsEnabled=Curves.Draft.CanEdit && Curves.Draft[Curves.Channel]!=CurveSetting.Default;
        ResetAllCurvesAction.IsEnabled=Curves.Draft.CanEdit && count>0; Apply.IsEnabled=false;
        CanvasHint.Text=Curves.Channel is CurveChannel.LeftStick or CurveChannel.RightStick ? "STICK RESPONSE" : "TRIGGER TRAVEL";
        FrontChoice.IsEnabled=RearChoice.IsEnabled=false;
    }
    private void CurvesTab(object sender,RoutedEventArgs e)=>ShowCurves(true);
    private void ButtonsTab(object sender,RoutedEventArgs e) => ShowButtons();
    /// <summary>Open the Buttons page from anywhere (tab, Dashboard hero, review harnesses).</summary>
    public void ShowButtons() { ShowCurves(false);if(IsMacros)ShowMacros(false);if(IsVibration)ShowVibration(false);HideManagement();ButtonsNav.IsChecked=true;FrontChoice.IsEnabled=true;RearChoice.IsEnabled=Model.Id=="x20";UpdateButtonsPage(); }
    public DashboardConsolePage? Dashboard {get;private set;}
    private int savedSetupCount=0;
    private DashboardConsolePage CreateDashboard(){var page=new DashboardConsolePage(Model,Player,Draft.UnsentChanges,savedSetupCount,!animate);page.ConfigureRequested+=ShowButtons;return page;}
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
            if(Macros==null) {Macros=new(macroDraft,layout,this,RegisterFocus,!animate,Model.Id);ModelProfiles.Wear(Macros.Body,Model.Id,"Macros");MacroBodyHost.Children.Add(Macros.Body);Macros.Changed+=UpdateMacroContext;}
            UpdateMacroContext();EnterMotion(MacroBodyHost,14,220);
        }
        else {Macros?.StopPreview();RefreshEditor();}UpdateButtonsPage();
    }
    private void UpdateMacroContext()
    {
        if(!IsMacros || Macros==null)return;
        ChangeCount.Text=$"{macroDraft.ModifiedSlots} local sequence{(macroDraft.ModifiedSlots==1?"":"s")} · Hardware contents unknown";
        DraftChip.Text="Local macro drafts";PrimaryHint.Text=" Select / Edit";OverviewHint.Text=" Add event";Apply.IsEnabled=false;
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
        if(value){if(Vibration==null){Vibration=new(vibrationDraft,this,RegisterFocus,animate,Model.Id);ModelProfiles.Wear(Vibration.Body,Model.Id,"Vibration");VibrationBodyHost.Children.Add(Vibration.Body);VibrationShelfHost.Children.Add(Vibration.Shelf);Vibration.Changed+=UpdateVibrationContext;}UpdateVibrationContext();EnterMotion(VibrationBodyHost,14,220);EnterMotion(VibrationShelfHost,14,220);}
        else RefreshEditor();UpdateButtonsPage();
    }
    private void UpdateVibrationContext(){if(!IsVibration)return;ChangeCount.Text=vibrationDraft.Modified?"Linked vibration strength modified · Local draft":"Vibration preview · No local changes";DraftChip.Text="Local vibration draft";PrimaryHint.Text=" Adjust strength";OverviewHint.Text=" Reset draft";VibrationResetChoice.IsEnabled=vibrationDraft.Modified;Apply.IsEnabled=false;}
    private void VibrationTab(object sender,RoutedEventArgs e)=>ShowVibration(true);
    private void ResetVibration(object sender,RoutedEventArgs e)=>Vibration?.Reset();
    public void ShowManagement(string page)
    {
        if(page is not ("Dashboard" or "Device" or "Tester" or "Setups"))throw new ArgumentException("Unknown Studio page.");
        if(IsCurves)ShowCurves(false);if(IsMacros)ShowMacros(false);if(IsVibration)ShowVibration(false);HideManagement();ManagementPage=page;
        HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=Visibility.Collapsed;
        ManagementBodyHost.Visibility=ManagementShelfFrame.Visibility=Visibility.Visible;ManagementBodyHost.Children.Clear();ManagementShelfHost.Children.Clear();
        ButtonsNav.IsChecked=CurvesNav.IsChecked=MacrosNav.IsChecked=VibrationNav.IsChecked=false;DeviceNav.IsChecked=page=="Device";TesterNav.IsChecked=page=="Tester";SetupsNav.IsChecked=page=="Setups";DashboardNav.IsChecked=page=="Dashboard";
        if(page=="Dashboard"){Dashboard??=CreateDashboard();ManagementBodyHost.Children.Add(Dashboard);ChangeCount.Text="Select the controller to configure it · Your games are found automatically";PrimaryHint.Text=" Select";OverviewHint.Text="";DraftChip.Text="Dashboard";Dashboard.FocusHero();}
        else if(page=="Device"){Device??=new(Model,Player,this,RegisterFocus,()=>{},()=>ShowManagement("Setups"));Device.OpenTester.Visibility=Visibility.Collapsed;Device.UpdateEngineStatus(NativeEngineStatus);ManagementBodyHost.Children.Add(Device.Body);ManagementShelfHost.Children.Add(Device.Shelf);ChangeCount.Text="Assigned model · Gameplay and configuration remain separate";PrimaryHint.Text=" Open";OverviewHint.Text=" My setups";DraftChip.Text="Controller overview";}
        else if(page=="Tester"){Tester??=new(Model,layout,this,RegisterFocus,()=>ShowManagement("Device"),animate);Tester.Enter();ManagementBodyHost.Children.Add(Tester.Body);ManagementShelfHost.Children.Add(Tester.Shelf);ChangeCount.Text="Tester owns gameplay input · UI input navigation paused";PrimaryHint.Text=" Exit Tester";OverviewHint.Text="";DraftChip.Text="Gameplay tester";navigationBeforeTester=navigationMode;navigationMode=true;Tester.Exit.Focus();}
        else {if(Setups==null){Setups=new(setupStore,new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json"))),Model.Id,this,RegisterFocus,()=>SetupSnapshot.Capture(Model.Id,Draft,Curves.Draft,macroDraft,vibrationDraft),LoadSetup,legacySources,activeSetup);Setups.Changed+=UpdateSetupContext;}Setups.RefreshCurrent();ManagementBodyHost.Children.Add(Setups.Body);ManagementShelfHost.Children.Add(Setups.Shelf);UpdateSetupContext();}
        PrimaryGlyph.Text=page=="Tester"?"↵":"A";BackGlyph.Text=page=="Tester"?"Esc":"B";SecondaryFooterHint.Visibility=ActualWidth<1100 || page=="Tester"?Visibility.Collapsed:Visibility.Visible;
        Grid.SetRowSpan(StudioBody,page is "Setups" or "Dashboard"?2:1);if(page is "Setups" or "Dashboard")ManagementShelfFrame.Visibility=Visibility.Collapsed;InputContextChanged?.Invoke();Apply.IsEnabled=false;EnterMotion(ManagementBodyHost,14,220);EnterMotion(ManagementShelfHost,14,220);UpdateButtonsPage();
    }
    private void HideManagement(){StopTry();if(ManagementPage==null)return;Grid.SetRowSpan(StudioBody,1);if(ManagementPage=="Tester")navigationMode=navigationBeforeTester;Tester?.ExitCapture();ManagementPage=null;InputContextChanged?.Invoke();PrimaryGlyph.Text="A";BackGlyph.Text="B";SecondaryFooterHint.Visibility=ActualWidth<1100?Visibility.Collapsed:Visibility.Visible;ManagementBodyHost.Children.Clear();ManagementShelfHost.Children.Clear();ManagementBodyHost.Visibility=ManagementShelfFrame.Visibility=Visibility.Collapsed;DeviceNav.IsChecked=TesterNav.IsChecked=SetupsNav.IsChecked=DashboardNav.IsChecked=false;HardwareStage.Visibility=InspectorStage.Visibility=AssignmentShelf.Visibility=OverviewChoice.Visibility=ResetAllChoice.Visibility=Visibility.Visible;UpdateButtonsPage();}
    private void LoadSetup(SetupSnapshot snapshot){if(snapshot.ModelId!=Model.Id)throw new InvalidOperationException("Setup model does not match this player.");snapshot.LoadInto(Draft,Curves.Draft,macroDraft,vibrationDraft);Curves.Refresh();Macros?.Refresh();Vibration?.Refresh();}
    private void UpdateSetupContext(){if(ManagementPage!="Setups"||Setups==null)return;activeSetup=Setups.ActiveId;ActiveSetupChanged?.Invoke(activeSetup);ChangeCount.Text=activeSetup==null?"Local setup library · Hardware application unavailable":"Setup open in local editor · Hardware unchanged";PrimaryHint.Text=" Select / Open";OverviewHint.Text=" New setup";DraftChip.Text="My setups";Apply.IsEnabled=false;}
    public bool DeliverGameplayFrame(TesterFrame frame)=>ManagementPage=="Tester" && Tester?.Receive(frame)==true;

    // ---- Buttons-page "Try it": keyboard-driven preview of the real stick caps and triggers ----
    private KeyboardSimulation? trial;
    public bool IsTrying => trial?.Enabled == true;
    private bool OnButtonsPage => ManagementPage == null && !IsCurves && !IsMacros && !IsVibration;
    private static bool IsOnboardFunction(string key) => key is "CAPTURE" or "TURBO";
    private void ToggleTry(object sender, RoutedEventArgs e) { if (IsTrying) StopTry(); else StartTry(); }
    public void StartTry()
    {
        if (!OnButtonsPage) return;
        trial = new KeyboardSimulation(Model.Id); trial.Enable(); buttonsPage?.SetTrying(true); UpdateHints();
        TryChoice.Content = "Stop"; TryChoice.Background = (Brush)FindResource("Blue");
        TryHint.Visibility = Visibility.Visible; HardwareShortcuts.Visibility = Visibility.Collapsed;
        StatusNote.Text = "Keyboard preview · No controller output";
    }
    public void StopTry()
    {
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
    }
    private void IlluminatePressed(double lt, double rt)
    {
        foreach (var (key, control) in controls)
            control.Illuminate(key == SelectedControl, navigationMode);
    }
    /// <summary>Read-only gameplay input shown on the Buttons artwork. It never navigates or writes.</summary>
    public void ObserveLiveInput(double lx, double ly, double rx, double ry, double lt, double rt)
    {
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
                if (Macros == null) return false; int s = Array.IndexOf(MacroDraft.Slots, Macros.Slot);
                Macros.SelectSlot(MacroDraft.Slots[(s + step + MacroDraft.Slots.Length) % MacroDraft.Slots.Length]); return true;
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
        var map = InputMap.For(page, focus, IsTrying);
        var hints = new List<(string, string)>();
        if (map.A != null) hints.Add(("A", map.A));
        hints.Add(("B", map.B));
        if (map.X != null) hints.Add(("X", map.X));
        if (map.Y != null) hints.Add(("Y", map.Y));
        if (map.Triggers != null) hints.Add(("LT/RT", map.Triggers));
        if (page != "Tester") hints.Add(("LB/RB", "Tabs"));
        LiveHints.Children.Clear(); LiveHints.Children.Add(GlyphHint.Footer(hints.ToArray()));
    }

    /// <summary>Rear paddles are programmable macro slots, not independent buttons: open the slot in Macros.</summary>
    private bool OpenMacroSlot(string key)
    {
        if (!MacroDraft.Slots.Contains(key)) return false;
        StopTry(); ShowMacros(true); Macros?.SelectSlot(key); return Macros != null;
    }
    private void DeviceTab(object sender,RoutedEventArgs e)=>ShowManagement("Device");private void TesterTab(object sender,RoutedEventArgs e)=>ShowManagement("Tester");private void SetupsTab(object sender,RoutedEventArgs e)=>ShowManagement("Setups");
    private void Front(object sender, RoutedEventArgs e) { ShowController(false); ClearSelection(); }
    private void Rear(object sender, RoutedEventArgs e) { ShowController(true); ClearSelection(); }
    private void Back(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    public void OnKeyUp(object sender,KeyEventArgs e){if(trial!=null && TrialKey(e.Key) is {} up){trial.Release(up);PushTrialFrame();e.Handled=true;return;}if(ManagementPage=="Tester" && Tester?.HandleKeyboard(e.Key,false)==true)e.Handled=true;}
    /// <summary>The Buttons page is waiting for "the button you want" or "what it should do": every press is captured.</summary>
    public bool CapturingRemap => OnButtonsPage && buttonsPage != null && buttonsPage.Listening != ButtonsConsolePage.Listen.None;
    public void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (CapturingRemap)
        {
            e.Handled = true; if (e.IsRepeat) return;
            if (e.Key == Key.Escape) { buttonsPage!.CancelListening(); return; }
            if (InputMap.ButtonFor(e.Key == Key.System ? e.SystemKey : e.Key) is { } pressed) buttonsPage!.Press(pressed);
            return;
        }
        if(trial!=null){if(e.Key==Key.Escape){StopTry();e.Handled=true;return;}if(TrialKey(e.Key) is {} down){if(!e.IsRepeat){trial.Press(down);PushTrialFrame();}e.Handled=true;return;}}
        if(ManagementPage=="Setups" && Setups?.HandleKey(e)==true)return;
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
        // LB/RB walk the tabs: Dashboard, Buttons, Curves, Macros, Vibration, Device, Setups (the Tester is no longer a tab)
        if(e.Key is Key.PageDown or Key.PageUp && !e.IsRepeat){int index=ManagementPage switch {"Dashboard"=>0,"Device"=>5,"Setups"=>6,_=>IsVibration?4:IsMacros?3:IsCurves?2:1};int next=Math.Clamp(index+(e.Key==Key.PageDown?1:-1),0,6);if(Model.Id!="x20"&&next is >=2 and <=4)next=e.Key==Key.PageDown?5:1;if(next==0)ShowManagement("Dashboard");else if(next>=5)ShowManagement(next==5?"Device":"Setups");else if(next==4)ShowVibration(true);else if(next==3)ShowMacros(true);else if(next==2)ShowCurves(true);else ShowButtons();e.Handled=true;return;}
        if (e.Key == Key.F6) { if (!e.IsRepeat) SetNavigationPreview(!navigationMode); e.Handled = true; return; }
        if (e.Key == Key.Escape || navigationMode && e.Key == Key.B) { if (AssignmentsVisible) ShowAssignments(false);else if(IsVibration && ReferenceEquals(Vibration?.Strength,Keyboard.FocusedElement))FocusSelectedControl(); else if (IsCurves && (Curves.PointHandles.Any(p=>ReferenceEquals(p,Keyboard.FocusedElement)) || ReferenceEquals(Curves.Inner,Keyboard.FocusedElement) || ReferenceEquals(Curves.Outer,Keyboard.FocusedElement))) FocusSelectedControl(); else if (navigationMode && Keyboard.FocusedElement is Button && Keyboard.FocusedElement is not PhysicalHotspot) FocusSelectedControl(); else BackRequested?.Invoke(); e.Handled = true; return; }
        if (navigationMode && e.Key == Key.Y) { if(ManagementPage=="Device")ShowManagement("Setups");else if(ManagementPage=="Setups")Setups?.NewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));else if(IsVibration)Vibration?.Reset();else if(IsMacros)Macros?.Add();else if(IsCurves) Curves.Reset();else ShowAssignments(!AssignmentsVisible); e.Handled = true; return; }
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
