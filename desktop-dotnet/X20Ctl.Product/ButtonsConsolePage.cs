using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>
/// The Buttons page in the console design (approved by the owner on 10 Oct 2026): controller visualizer
/// (layered photo + exact-shape hotspots) on the left, the remap workspace on the right, and the
/// full-width outputs shelf underneath. It only edits the local ButtonDraft; it never writes hardware.
/// </summary>
public sealed class ButtonsConsolePage : Grid
{
    private readonly ControllerModel model; private readonly ControllerLayout layout; private readonly ButtonDraft draft;
    private readonly Func<string, string> label; private readonly bool animate;
    private readonly Action<string> select; private readonly Func<string, bool> openMacro; private readonly Action toggleTry;
    private readonly Grid stage = new() { Width = 1536, Height = 1024 };
    private readonly Canvas hotspots = new() { Width = 1536, Height = 1024 };
    private readonly Canvas paddleNames = new() { Width = 1536, Height = 1024 };
    private readonly Dictionary<string, PhysicalHotspot> regions = new();
    private readonly Dictionary<string, Button> outputs = new();
    private readonly Dictionary<string, RadioButton> groups = new();
    private readonly StackPanel workspace = new(), overview = new() { Visibility = Visibility.Collapsed };
    // the mapping card: "You press" (the physical control) → "Game gets" (its draft output), in plain words
    private readonly Border pressedFace = new(), sendsFace = new(), mappingCard = new();
    private readonly TextBlock pressedName = Kit.Text("", 13, "DS.TextSoft"), sendsName = Kit.Text("", 13, "DS.TextSoft"), sentence = Kit.Text("", 15, "DS.Text"), readOnly = Kit.Text("", 14, "DS.TextSoft");
    private readonly Button restore = new() { Content = "Restore default", ToolTip = "Make this button send itself again" };
    private readonly TextBlock outputsHint = Kit.Text("", 14, "DS.TextSoft"), stepTwoHint = Kit.Text("Choose any button in Outputs below", 13, "DS.TextSoft");
    private readonly RadioButton front = new(), back = new();
    private readonly Button tryButton, allButton;
    private string selected = "",outputGroup="All";
    private readonly TextBlock showcaseTitle=Kit.Text("Buttons showcase",16,"DS.Text",FontWeights.SemiBold);
    private readonly StackPanel outputPicker=new(){Visibility=Visibility.Collapsed};
    private readonly TextBlock selectionTitle=Kit.Text("Select a control",24,"DS.Text",FontWeights.SemiBold);
    private readonly Canvas markers=new(){Width=1536,Height=1024,IsHitTestVisible=false};
    private readonly TextBlock mappingLabel=Kit.Text("",32,"DS.Text",FontWeights.SemiBold);
    private bool connected;
    public void SetConnected(bool value){connected=value;RefreshMapping();}
    public void SelectPhysical(string key){if(Listening!=Listen.None){Press(key);return;}if(layout.Controls.Any(r=>r.Key==key))Pick(key);}
    private bool TargetAllowed(string key)=>ButtonDraft.Targets.Contains(key)||key=="NONE"&&draft.PcMode;
    private string TargetName(string key)=>key=="NONE"?"Disabled":label(key);
    public PhotoController Controller { get; private set; }
    public LiveController Live => Controller.Live;
    public bool IsRear { get; private set; }

    private static readonly (string Name, string First, string[] Keys)[] Groups =
    [
        ("Face", "A", ["A", "B", "X", "Y"]), ("D-pad", "DPAD_UP", ["DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"]),
        ("Shoulders", "LB", ["LB", "RB", "LT", "RT"]), ("Sticks", "L3", ["L3", "R3", "LSTICK_ANALOG", "RSTICK_ANALOG"]), ("System", "SELECT", ["SELECT", "START", "HOME"])
    ];

    public ButtonsConsolePage(ControllerModel model, ControllerLayout layout, ButtonDraft draft, Func<string, string> label, bool animate,
        Action<string> select, Func<string, bool> openMacro, Action toggleTry)
    {
        this.model = model; this.layout = layout; this.draft = draft; this.label = label; this.animate = animate; this.select = select; this.openMacro = openMacro; this.toggleTry = toggleTry;
        Controller = new PhotoController(model.Id); stage.Children.Add(Controller); stage.Children.Add(hotspots);stage.Children.Add(markers);stage.Children.Add(paddleNames);
        RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = new(14) }); RowDefinitions.Add(new() { Height = GridLength.Auto });
        var top = new Grid(); top.ColumnDefinitions.Add(new() { Width = new(1.12, GridUnitType.Star) }); top.ColumnDefinitions.Add(new() { Width = new(16) }); top.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = 390 });
        Children.Add(top);

        // left: visualizer with a Front / Back segmented switch
        string g = "view" + Guid.NewGuid().ToString("N");
        front.Content = "Front"; back.Content = "Back";
        foreach (var r in new[] { front, back }) { r.Style = DS.Style("DS.Segment"); r.GroupName = g; r.Width = 86; r.Height = 32; }
        front.IsChecked = true; back.IsEnabled = layout.Controls.Any(c => c.View is "back" or "shoulder");
        front.Checked += (_, _) => ShowView(false); back.Checked += (_, _) => ShowView(true);
        var viewSwitch = new Border { CornerRadius = new(11), Background = new SolidColorBrush(Color.FromRgb(29,44,67)), BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderThickness = new(1), Padding = new(1), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { front, back } }, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, -60, 0, 0) };
        var visual = new Grid { Children = { new Viewbox { Child = stage }, viewSwitch } };
        top.Children.Add(Kit.Panel("Button Visualizer", "Select a control on the controller to remap it.", null, visual));

        // right: two steps around a "you press → game gets" card, then the actions
        var seg = new UniformGrid { Rows = 1 }; string sg = "grp" + Guid.NewGuid().ToString("N");
        var allCategory=new RadioButton{Content="All",Style=DS.Style("DS.Segment"),GroupName=sg};allCategory.Click+=(_,_)=>{outputGroup="All";RefreshMapping();};groups["All"]=allCategory;seg.Children.Add(allCategory);
        foreach (var (name, first, keys) in Groups)
        {
            var r = new RadioButton { Content = name, Style = DS.Style("DS.Segment"), GroupName = sg, IsEnabled = keys.Any(ButtonDraft.Targets.Contains) };
            string groupName=name;r.Click+=(_,_)=>{outputGroup=groupName;RefreshMapping();}; groups[name] = r; seg.Children.Add(r);
        }
        workspace.Children.Add(selectionTitle);
        workspace.Children.Add(BuildMappingCard());
        workspace.Children.Add(Kit.Text(draft.PcMode?"PC MODE · Processed by X20CTL on this PC. Controller memory is unchanged.":"LOCAL DRAFT · Not written to controller memory.",13,"DS.TextSoft").Also(t=>{t.TextWrapping=TextWrapping.Wrap;t.Margin=new(0,10,0,12);}));
        outputPicker.Children.Add(outputsHint);
        outputPicker.Children.Add(new Border{CornerRadius=new(12),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255)),Padding=new(2),Child=seg,Margin=new(0,8,0,10)});
        workspace.Children.Add(outputPicker);
        tryButton = ActionButton("", "Test mapping", "ActionButton"); tryButton.Click += (_, _) => this.toggleTry();
        allButton = ActionButton("", "All assignments", "ActionButton"); allButton.Click += (_, _) => ShowOverview(overview.Visibility != Visibility.Visible);
        var more = new Button { Content = "", Style = DS.Style("DS.ActionRound"), ToolTip = "Reset options" };
        more.ContextMenu = new ContextMenu { Items = { Menu("Reset this control", () => { if (Source.Length > 0 && draft.CanEdit(Source)) { draft.Reset(Source); Changed?.Invoke(); } }), Menu("Reset all controls", () => { draft.ResetAll(); Changed?.Invoke(); }) } };
        more.Click += (_, _) => { more.ContextMenu.PlacementTarget = more; more.ContextMenu.IsOpen = true; };
        var actions = new Grid { Margin = new(0, 14, 0, 0) }; actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new() { Width = new(12) }); actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new() { Width = new(12) }); actions.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        actions.Children.Add(tryButton); Grid.SetColumn(allButton, 2); actions.Children.Add(allButton); Grid.SetColumn(more, 4); actions.Children.Add(more);
        var body = new Grid { Children = { workspace, overview } };
        var right = Kit.Panel(null, null, null, new ScrollViewer{Content=new StackPanel{Children={body,actions}},VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}); Grid.SetColumn(right, 2); top.Children.Add(right);

        // bottom: outputs shelf (interactive)

        var showcase=BuildShowcase();Grid.SetRow(showcase,2);Children.Add(showcase);
        ShowView(false);
    }

    public event Action? Changed;
    public IReadOnlyCollection<PhysicalHotspot> Hotspots => regions.Values;
    public PhysicalHotspot? Hotspot(string key) => regions.GetValueOrDefault(key);

    public void ShowView(bool rear)
    {
        if (rear == IsRear && regions.Count > 0) return;
        IsRear = rear; front.IsChecked = !rear; back.IsChecked = rear;
        stage.Children.Remove(Controller); bool smooth = Controller.Live.Smooth; Controller = new PhotoController(model.Id, rear); Controller.Live.Smooth = smooth; stage.Children.Insert(0, Controller);
        hotspots.Children.Clear(); regions.Clear();
        // the paddle names sit above the controls and open their slot in Macros (owner, 25 Oct 2026)
        Controller.Live.ShowLabels = false; paddleNames.Children.Clear();
        if (rear) paddleNames.Children.Add(LiveController.PaddleLabels(model.Id, slot => openMacro(slot)));
        foreach (var region in layout.Controls.Where(r => r.Role != "axis" && r.Key is not ("CAPTURE" or "TURBO") && (r.View == (rear ? "back" : "front") || rear && r.View == "shoulder")))
        {
            var h = new PhysicalHotspot(region, animate) { ToolTip = label(region.Key), Tag = region.Key };
            var at = LiveController.Place(model.Id, region); Canvas.SetLeft(h, at.X); Canvas.SetTop(h, at.Y);
            string key = region.Key; h.Click += (_, _) => Pick(key);
            hotspots.Children.Add(h); regions[key] = h;
        }
        if (animate) { Controller.Opacity = 0; Controller.BeginAnimation(OpacityProperty, DS.To(1, 220)); }
        Refresh(selected);
    }

    /// <summary>Re-reads the draft and the selection; called by the Studio after any change.</summary>
    public void Refresh(string key)
    {
        // the view follows a newly picked control (a paddle shows the back); a refresh for the same control keeps the
        // view the player chose, so Back stays on the back even with a front button selected
        bool picked = key != selected; selected = key;Source=layout.Controls.Any(r=>r.Key==key)?key:"";
        var region = layout.Controls.FirstOrDefault(r => r.Key == key);
        if (region != null && picked)
        {
            bool needsRear = region.View is "back" or "shoulder";
            if (needsRear != IsRear && (needsRear ? back.IsEnabled : true)) { ShowView(needsRear); return; }
        }
        foreach (var (k, h) in regions) h.Illuminate(k == key || region?.Role == "axis" && k == (key.StartsWith('L') ? "L3" : "R3"), false);

        RefreshMapping();RefreshMarkers();
        if (overview.Visibility == Visibility.Visible) BuildOverview();
    }

    // ----- remapping, the way games do it: click a slot, press a button, it fills in -----
    public enum Listen { None, Source, Target }
    /// <summary>Which side of the card is waiting for a press. While it waits, every controller button is captured.</summary>
    public Listen Listening { get; private set; }
    /// <summary>The physical button on the left of the card ("you press"); empty until the player picks one.</summary>
    public string Source { get; private set; } = "";
    public void BeginListening(Listen side) { if(!connected && side!=Listen.None){Listening=Listen.None;RefreshMapping();return;}Listening = side == Listen.Target && Source.Length == 0 ? Listen.Source : side; RefreshMapping(); }
    public void CancelListening() { if (Listening == Listen.None) return; Listening = Listen.None; RefreshMapping(); }
    /// <summary>A press while listening: a controller button (from the controller or its keyboard mirror), a click on
    /// the picture, or a click in Outputs. The first fills "you press" and moves on; the second sets what it does.</summary>
    public void Press(string button)
    {
        if (Listening == Listen.Source)
        {
            if (!layout.Controls.Any(r => r.Key == button)) return;
            Source = button; select(button);
            Listening = draft.CanEdit(button) ? Listen.Target : Listen.None; RefreshMapping(); return;
        }
        if (Listening == Listen.Target && TargetAllowed(button) && draft.CanEdit(Source))
        {
            Listening = Listen.None; draft.Set(Source, button); Changed?.Invoke(); RefreshMapping();
        }
    }
    private void Pick(string key)
    {
        if (openMacro(key)) return;
        Source = key; select(key); Listening = Listen.None; RefreshMapping();
    }
    private void RefreshMapping()
    {
        bool has = Source.Length > 0 && layout.Controls.Any(r => r.Key == Source), editable = has && draft.CanEdit(Source);
        string target = editable ? draft.Target(Source) : "", name = has ? label(Source) : "";
        bool changed = editable && target != Source, waitSource = Listening == Listen.Source, waitTarget = Listening == Listen.Target;
        FillFace(pressedFace, waitSource ? "…" : has ? Short(Source) : "+", waitSource || !has ? null : Source, false, waitSource);
        FillFace(sendsFace, editable ? Short(target) : has ? "—" : "+", !editable ? null : target, changed, waitTarget);
        Pulse(pressedRing, waitSource); Pulse(sendsRing, waitTarget);
        pressedName.Text = waitSource ? "Press a button…" : has ? Kind(Source) : "Click to choose";
        sendsName.Text = waitTarget ? "Choose output below" : editable ? target=="NONE"?"Disabled":Kind(target) : has ? "Can't change" : "Then press its new job";
        selectionTitle.Text=has?label(Source):"Select a control";
        sentence.Text=!has?(connected?"Select a control on the picture or press it on the controller.":"Select a control on the picture. Physical detection is unavailable while offline."):editable?$"Current mapping: {Short(Source)} → {Short(target)}":"This input can be inspected; remapping is unavailable.";
        readOnly.Text = !has ? "" : model.Id != "x20" && !draft.PcMode ? "Remapping is not verified for this model yet, so it can only be inspected." : editable ? "" : $"{name} can't be remapped. It has no button role to swap.";
        readOnly.Visibility = readOnly.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed; sentence.Visibility = sentence.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        restore.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;restore.Content=$"Restore {Short(Source)} → {Short(Source)}";
        outputPicker.Visibility=editable?Visibility.Visible:Visibility.Collapsed;tryButton.Visibility=changed?Visibility.Visible:Visibility.Collapsed;
        stepTwoHint.Text = waitTarget ? "Waiting for a press" : editable ? "Click GAME GETS, then press a button" : has ? "Not available for this button" : "Pick a button first";
        mappingCard.BorderBrush = new SolidColorBrush(waitSource || waitTarget ? Color.FromArgb(200, 125, 182, 255) : changed ? Color.FromArgb(120, 125, 182, 255) : Color.FromArgb(38, 255, 255, 255));
        foreach (var pair in groups)pair.Value.IsChecked=pair.Key==outputGroup;showcaseHint.Text=editable?$"Assign {Short(Source)} to any output":has?"This control can't be reassigned":"Pick a control first";
        foreach (var (k,b) in outputs){Paint(b,k,editable&&k==target,editable||!has);b.Opacity=outputGroup=="All"||k=="NONE"||Groups.First(g=>g.Name==outputGroup).Keys.Contains(k)?1:.3;}
        RefreshMarkers();
        outputsHint.Text=editable?$"Assign {Short(Source)} to:":"Select a control first";
    }

    public void ToggleOverview() => ShowOverview(overview.Visibility != Visibility.Visible);
    /// <summary>A on a control jumps here: focus the tile of its current output.</summary>
    public void FocusOutput(string target) { if (outputs.TryGetValue(target, out var b) && b.IsEnabled) b.Focus(); }
    public void SetTrying(bool on) => ((TextBlock)((StackPanel)tryButton.Content).Children[1]).Text = on ? "Stop test" : "Test mapping";

    // ----- pieces -----
    private static FrameworkElement Step(int n, string title, TextBlock h, double top)
    {
        var badge = new Grid { Width = 28, Height = 28, Margin = new(0, 0, 12, 0), Children = { new System.Windows.Shapes.Ellipse { Fill = new SolidColorBrush(Kit.Blue) }, Kit.Text(n.ToString(), 14, "DS.Text", FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center) } };
        var row = new DockPanel { Margin = new(0, top, 0, 8) };
        h.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(h, Dock.Right);
        DockPanel.SetDock(badge, Dock.Left); row.Children.Add(badge); row.Children.Add(h); row.Children.Add(Kit.Text(title, 16, "DS.Text", FontWeights.SemiBold));
        return row;
    }
    private readonly System.Windows.Shapes.Ellipse pressedRing = Ring(), sendsRing = Ring();
    private static System.Windows.Shapes.Ellipse Ring() => new() { Width = 84, Height = 84, Stroke = new SolidColorBrush(Color.FromRgb(125, 182, 255)), StrokeThickness = 2, Opacity = 0, IsHitTestVisible = false, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
    private void Pulse(System.Windows.Shapes.Ellipse ring, bool on)
    {
        var s = (ScaleTransform)ring.RenderTransform;
        ring.BeginAnimation(OpacityProperty, null); s.BeginAnimation(ScaleTransform.ScaleXProperty, null); s.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        if (!on) { ring.Opacity = 0; return; }
        if (!animate) { ring.Opacity = .9; return; }
        var grow = Ambient.Rate(new System.Windows.Media.Animation.DoubleAnimation(.86, 1.18, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, EasingFunction = DS.EaseOut }, 30);
        s.BeginAnimation(ScaleTransform.ScaleXProperty, grow); s.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ring.BeginAnimation(OpacityProperty, Ambient.Rate(new System.Windows.Media.Animation.DoubleAnimation(.95, 0, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever }, 30));
    }
    private Border BuildMappingCard()
    {
        var g = new Grid(); g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new());
        g.RowDefinitions.Add(new()); g.RowDefinitions.Add(new() { Height = GridLength.Auto });
        StackPanel Side(string caption, Border face, System.Windows.Shapes.Ellipse ring, TextBlock name, Listen side, string tip)
        {
            face.Width = face.Height = 50; face.CornerRadius = new(25); face.BorderThickness = new(1.4); face.HorizontalAlignment = HorizontalAlignment.Center; face.VerticalAlignment = VerticalAlignment.Center;
            var slot = new Button { Template = Bare(), Cursor = System.Windows.Input.Cursors.Hand, FocusVisualStyle = null, ToolTip = tip, Margin = new(0, 2, 0, 2), Content = new Grid { Width = 66, Height = 66, Background = Brushes.Transparent, Children = { ring, face } } };
            slot.Click += (_, _) => BeginListening(Listening == side ? Listen.None : side);
            slot.MouseEnter += (_, _) => face.BorderThickness = new(2.4); slot.MouseLeave += (_, _) => face.BorderThickness = new(1.4);
            System.Windows.Automation.AutomationProperties.SetName(slot, caption);
            return new() { HorizontalAlignment = HorizontalAlignment.Center, Children = {
                Kit.Text(caption, 11.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center), slot,
                name.Also(t => t.HorizontalAlignment = HorizontalAlignment.Center) } };
        }
        g.Children.Add(Side("YOU PRESS", pressedFace, pressedRing, pressedName, Listen.Source, "Choose a physical input on the artwork, or capture it when connected"));
        var arrow = Kit.Icon("\uE72A", 22, "DS.AccentHi"); arrow.Margin = new(18, 0, 18, 8); Grid.SetColumn(arrow, 1); g.Children.Add(arrow);
        var right = Side("GAME GETS", sendsFace, sendsRing, sendsName, Listen.Target, "Choose an output below, or capture a button when connected"); Grid.SetColumn(right, 2); g.Children.Add(right);
        restore.Style = DS.Style("ActionButton"); restore.Height = 42; restore.MinHeight = 42; restore.Padding = new(22, 0, 22, 0); restore.FontSize = 13.5;
        restore.Click += (_, _) => { if (Source.Length > 0 && draft.CanEdit(Source)) { draft.Reset(Source); Changed?.Invoke(); RefreshMapping(); } };
        var foot = new DockPanel { Margin = new(2, 8, 0, 0) }; DockPanel.SetDock(restore, Dock.Right); foot.Children.Add(restore);
        readOnly.TextWrapping = TextWrapping.Wrap; readOnly.TextTrimming = TextTrimming.None; sentence.TextWrapping = TextWrapping.Wrap; sentence.TextTrimming = TextTrimming.None;
        foot.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { sentence, readOnly } });
        Grid.SetRow(foot, 1); Grid.SetColumnSpan(foot, 3); g.Children.Add(foot);
        mappingCard.CornerRadius = new(14); mappingCard.BorderThickness = new(1); mappingCard.Padding = new(16, 10, 16, 12); mappingCard.Margin = new(0, 12, 0, 0);
        mappingCard.Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)); mappingCard.Child = g;
        return mappingCard;
    }
    private static void FillFace(Border face, string text, string? key, bool on, bool waiting = false)
    {
        var color = key switch { "Y" => Color.FromRgb(245, 205, 80), "X" => Color.FromRgb(70, 170, 255), "B" => Color.FromRgb(255, 100, 125), "A" => Color.FromRgb(70, 220, 150), _ => (Color?)null };
        bool empty = key == null && !waiting && text == "+";
        var t = Kit.Text(text, text.Length > 2 ? 17 : empty ? 30 : 26, empty ? "DS.TextMuted" : "DS.Text", FontWeights.SemiBold); t.HorizontalAlignment = HorizontalAlignment.Center;
        if (!on && color != null) t.Foreground = new SolidColorBrush(color.Value);
        face.Child = t;
        face.Background = on ? new SolidColorBrush(Color.FromRgb(43,115,222)) : waiting ? new SolidColorBrush(Color.FromArgb(60, 61, 139, 255)) : new SolidColorBrush(Color.FromArgb(empty ? (byte)14 : (byte)30, 255, 255, 255));
        face.BorderBrush = on || waiting ? Brushes.White : color != null ? new SolidColorBrush(Color.FromArgb(150, color.Value.R, color.Value.G, color.Value.B)) : new SolidColorBrush(Color.FromArgb(empty ? (byte)90 : (byte)46, 255, 255, 255));
        face.Effect = on || waiting ? new DropShadowEffect { Color = Kit.Blue, BlurRadius = 22, ShadowDepth = 0, Opacity = .9 } : null;
    }
    private static string Kind(string key) => key switch { "A" or "B" or "X" or "Y" => $"{key} button", "LB" => "Left bumper", "RB" => "Right bumper", "LT" => "Left trigger", "RT" => "Right trigger", "L3" => "Left stick click", "R3" => "Right stick click", "SELECT" => "View button", "START" => "Menu button", "HOME" => "Guide button", "LSTICK_ANALOG" => "Left stick", "RSTICK_ANALOG" => "Right stick", _ when key.StartsWith("DPAD_") => "D-pad " + key[5..].ToLowerInvariant(), _ => key };
    private static string Short(string key) => key switch { "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", "SELECT" => "View", "START" => "Menu", "HOME" => "Guide", "NONE"=>"Disabled", "LSTICK_ANALOG" => "LS", "RSTICK_ANALOG" => "RS", _ => key };
    private void SetTarget(string target)
    {
        if (Listening == Listen.Target) { Press(target); return; }
        if(Source.Length==0){if(layout.Controls.Any(r=>r.Key==target))Pick(target);return;}
        if (!draft.CanEdit(Source)) return;
        draft.Set(Source, target); Changed?.Invoke();
    }
    // the showcase (redesigned 17 Oct 2026): every output drawn as the part of a controller it is, filling the width
    private readonly TextBlock showcaseHint = Kit.Text("", 13, "DS.TextSoft");
    private Border BuildShowcase()
    {
        var grid = new Grid { Height = 190 };
        (string Name, double Weight)[] families = [("FACE", 1.15), ("D-PAD", 1.15), ("SHOULDERS", 1.3), ("STICKS", 1.05), ("SYSTEM", 1), ("SPECIAL", 1)];
        for (int i = 0; i < families.Length; i++)
        {
            grid.ColumnDefinitions.Add(new() { Width = new(families[i].Weight, GridUnitType.Star) });
            var stage = new Grid { Height = 132, VerticalAlignment = VerticalAlignment.Center };
            var heading = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 8), Children = {
                new Border { Width = 5, Height = 5, CornerRadius = new(2.5), Background = new SolidColorBrush(Color.FromRgb(125, 182, 255)), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) },
                Kit.Text(Spaced(families[i].Name), 11.5, "DS.TextSoft", FontWeights.SemiBold) } };
            var family = new Border
            {
                CornerRadius = new(14), BorderThickness = new(1), Margin = new(6, 0, 6, 0), Padding = new(10, 12, 10, 12),
                BorderBrush = new LinearGradientBrush(Color.FromArgb(70, 140, 180, 240), Color.FromArgb(22, 140, 180, 240), 90),
                Background = new LinearGradientBrush(Color.FromArgb(150, 24, 39, 64), Color.FromArgb(150, 13, 23, 41), 90),
                Child = new DockPanel { Children = { heading.Also(h => DockPanel.SetDock(h, Dock.Top)), stage } }
            };
            Grid.SetColumn(family, i); grid.Children.Add(family);
            if (i == 0) stage.Children.Add(FaceDiamond());
            else if (i == 1) stage.Children.Add(DpadCross());
            else if (i == 2) stage.Children.Add(Shoulders());
            else if (i == 3) stage.Children.Add(Row(("L3", "L3"), ("R3", "R3")));
            else if (i == 4) stage.Children.Add(Row(("SELECT", "View"), ("START", "Menu")));
            else
            {
                var special = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = 168 };
                var defaults = new Button { Template = Bare(), Cursor = System.Windows.Input.Cursors.Hand, FocusVisualStyle = null, Height = 40, Margin = new(0, 0, 0, 10), ToolTip = "Make the picked control send itself again",
                    Content = new Border { CornerRadius = new(20), BorderThickness = new(1.2), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Background = new LinearGradientBrush(Color.FromRgb(40, 58, 86), Color.FromRgb(26, 39, 60), 90),
                        Child = IconLabel("", "Default") } };
                defaults.MouseEnter += (_, _) => ((Border)defaults.Content).BorderBrush = new SolidColorBrush(Color.FromArgb(200, 125, 182, 255));
                defaults.MouseLeave += (_, _) => ((Border)defaults.Content).BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                defaults.Click += (_, _) => { if (Source.Length > 0 && draft.CanEdit(Source)) { draft.Reset(Source); Changed?.Invoke(); RefreshMapping(); } };
                special.Children.Add(defaults);
                if (draft.PcMode) { var disabled = Output("NONE", "Disabled", 168, 40); special.Children.Add(disabled); }
                stage.Children.Add(special);
            }
        }
        var header = new DockPanel { Margin = new(6, 0, 6, 12) };
        showcaseHint.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(showcaseHint, Dock.Right); header.Children.Add(showcaseHint); header.Children.Add(showcaseTitle);
        var fit = new Viewbox { Child = grid, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        var body = new StackPanel { Children = { header, fit } };
        // fill the width; below 1180 px the whole shelf scales down instead of squeezing the parts
        body.SizeChanged += (_, e) => grid.Width = Math.Max(1180, e.NewSize.Width);
        return new Border { CornerRadius = new(16), Padding = new(12, 14, 12, 14), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 125, 170, 230)), Background = new SolidColorBrush(Color.FromArgb(80, 8, 17, 38)), Child = body };
    }
    private static string Spaced(string text) => string.Join(" ", text.ToCharArray());
    private static StackPanel IconLabel(string icon, string text) => new()
    {
        Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        Children = { new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 9, 0) },
            new TextBlock { Text = text, FontFamily = DS.Display, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } }
    };
    private void RefreshMarkers()
    {
        markers.Children.Clear();
        foreach(var (key,hot) in regions)
        {
            bool modified=draft.CanEdit(key)&&draft.Target(key)!=key;
            hot.ToolTip=modified?$"{label(key)} → {TargetName(draft.Target(key))}":label(key);
            if(!modified)continue;
            var dot=new System.Windows.Shapes.Ellipse{Width=16,Height=16,Fill=Brushes.LightSkyBlue,Stroke=Brushes.White,StrokeThickness=2};
            Canvas.SetLeft(dot,Canvas.GetLeft(hot)+hot.Width-10);Canvas.SetTop(dot,Canvas.GetTop(hot)-5);markers.Children.Add(dot);
        }
        if(regions.TryGetValue(Source,out var active)&&draft.CanEdit(Source))
        {
            mappingLabel.Text=$"{Short(Source)} → {Short(draft.Target(Source))}";mappingLabel.Background=new SolidColorBrush(Color.FromArgb(210,9,22,45));mappingLabel.Padding=new(12,5,12,5);
            Canvas.SetLeft(mappingLabel,Math.Clamp(Canvas.GetLeft(active)+active.Width+16,12,1280));Canvas.SetTop(mappingLabel,Math.Max(12,Canvas.GetTop(active)-20));markers.Children.Add(mappingLabel);
        }
    }
    private Canvas FaceDiamond()
    {
        double s = 46, w = 150, h = 132; var c = new Canvas { Width = w, Height = h, HorizontalAlignment = HorizontalAlignment.Center };
        void Put(string key, double x, double y) { var o = Output(key, key, s, s); Canvas.SetLeft(o, x); Canvas.SetTop(o, y); c.Children.Add(o); }
        Put("Y", (w - s) / 2, 0); Put("X", 6, (h - s) / 2); Put("B", w - s - 6, (h - s) / 2); Put("A", (w - s) / 2, h - s);
        return c;
    }
    /// <summary>The D-pad as one cross: four arms around a hub, each arm rounded only at its tip.</summary>
    private Canvas DpadCross()
    {
        double s = 42, gap = 2, w = 3 * s + 2 * gap, h = w; var c = new Canvas { Width = w, Height = h, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var hub = new Border { Width = s, Height = s, CornerRadius = new(4), Background = new SolidColorBrush(Color.FromRgb(22, 33, 52)), BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderThickness = new(1),
            Child = new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Fill = new RadialGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(90, 0, 0, 0)), Stroke = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)) } };
        Canvas.SetLeft(hub, s + gap); Canvas.SetTop(hub, s + gap); c.Children.Add(hub);
        void Put(string key, string text, double x, double y) { var o = Output(key, text, s, s); Canvas.SetLeft(o, x); Canvas.SetTop(o, y); c.Children.Add(o); }
        Put("DPAD_UP", "↑", s + gap, 0); Put("DPAD_LEFT", "←", 0, s + gap); Put("DPAD_RIGHT", "→", 2 * (s + gap), s + gap); Put("DPAD_DOWN", "↓", s + gap, 2 * (s + gap));
        return c;
    }
    /// <summary>Triggers on top, bumpers under them, the way they sit on the pad's top edge.</summary>
    private Grid Shoulders()
    {
        var g = new Grid { Width = 196, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = new(12) }); g.ColumnDefinitions.Add(new());
        g.RowDefinitions.Add(new() { Height = new(58) }); g.RowDefinitions.Add(new() { Height = new(10) }); g.RowDefinitions.Add(new() { Height = new(40) });
        void Put(string key, int row, int col) { var o = Output(key, key, 0, 0); Grid.SetRow(o, row); Grid.SetColumn(o, col); g.Children.Add(o); }
        Put("LT", 0, 0); Put("RT", 0, 2); Put("LB", 2, 0); Put("RB", 2, 2);
        return g;
    }
    private StackPanel Row(params (string Key, string Label)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, l) in items)
        {
            bool stick = Shape(k) == "stick"; var o = Output(k, l, stick ? 62 : 76, stick ? 62 : 40); o.Margin = new(7, 0, 7, 0);
            if (stick) row.Children.Add(new StackPanel { Children = { o, Kit.Text(k == "L3" ? "Left" : "Right", 11.5, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 8, 0, 0); }) } });
            else row.Children.Add(o);
        }
        return row;
    }
    /// <summary>The part of a controller each output is, which sets its outline.</summary>
    private static string Shape(string key) => key switch { "A" or "B" or "X" or "Y" => "face", "L3" or "R3" => "stick", "LB" or "RB" => "bumper", "LT" or "RT" => "trigger", "SELECT" or "START" or "HOME" => "system", _ when key.StartsWith("DPAD_") => "dpad", _ => "plain" };
    private static CornerRadius Corners(string key, double w, double h) => key switch
    {
        "LT" => new(26, 12, 8, 8), "RT" => new(12, 26, 8, 8), "LB" => new(16, 8, 8, 16), "RB" => new(8, 16, 16, 8),
        "DPAD_UP" => new(12, 12, 3, 3), "DPAD_DOWN" => new(3, 3, 12, 12), "DPAD_LEFT" => new(12, 3, 3, 12), "DPAD_RIGHT" => new(3, 12, 12, 3),
        _ => new(Math.Max(w, h) > 0 ? Math.Min(w, h) / 2 : 20)
    };
    private Button Output(string key, string text, double w, double h)
    {
        var face = new TextBlock { FontFamily = DS.Display, FontSize = Shape(key) is "face" ? 20 : Shape(key) is "stick" ? 17 : 16, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (key == "NONE") { face.Inlines.Add(new System.Windows.Documents.Run("  ") { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12 }); face.Inlines.Add(new System.Windows.Documents.Run(text)); }
        else face.Text = text;
        var border = new Border { Child = face, CornerRadius = Corners(key, w, h) };
        // rounded tiles without a fixed size (bumpers and triggers excepted) stay pills at any height
        if (w == 0 && Shape(key) is not ("bumper" or "trigger")) border.SetBinding(Border.CornerRadiusProperty, new System.Windows.Data.Binding("ActualHeight") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self), Converter = new HalfHeightRadius() });
        var b = new Button { Content = border, Template = Bare(), Cursor = System.Windows.Input.Cursors.Hand, Tag = key, ToolTip = TargetName(key), FocusVisualStyle = null, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
        if (w > 0) { b.Width = w; b.Height = h; }
        if (TargetAllowed(key)) { b.Click += (_, _) => SetTarget(key); outputs[key] = b; }
        else b.IsEnabled = false;
        void Lift(bool on) { if (!b.IsEnabled) return; var to = on ? 1.06 : 1; var t = (ScaleTransform)b.RenderTransform; t.BeginAnimation(ScaleTransform.ScaleXProperty, animate ? DS.To(to, 120) : null); t.BeginAnimation(ScaleTransform.ScaleYProperty, animate ? DS.To(to, 120) : null); if (!animate) t.ScaleX = t.ScaleY = to; }
        b.MouseEnter += (_, _) => { if (b.DataContext is not true) border.BorderBrush = new SolidColorBrush(Color.FromArgb(220, 160, 205, 255)); Lift(true); };
        b.MouseLeave += (_, _) => { Paint(b, key, b.DataContext is true, b.IsEnabled); Lift(false); };
        b.GotKeyboardFocus += (_, _) => border.BorderBrush = Brushes.White; b.LostKeyboardFocus += (_, _) => Paint(b, key, b.DataContext is true, b.IsEnabled);
        Paint(b, key, false, b.IsEnabled);
        return b;
    }
    /// <summary>A keycap: a soft top light over the body, the face colour on the rim and letter, blue and glowing when it is the current output.</summary>
    private static void Paint(Button b, string key, bool on, bool enabled)
    {
        var border = (Border)b.Content; var face = (TextBlock)border.Child; b.DataContext = on;
        var color = key switch { "Y" => Color.FromRgb(245, 205, 80), "X" => Color.FromRgb(70, 170, 255), "B" => Color.FromRgb(255, 100, 125), "A" => Color.FromRgb(70, 220, 150), _ => (Color?)null };
        bool stick = Shape(key) == "stick";
        b.IsEnabled = enabled;
        border.Opacity = enabled ? 1 : .45;
        border.BorderThickness = new(on ? 2 : stick ? 2.4 : 1.2);
        border.Background = on ? new LinearGradientBrush(Color.FromRgb(70, 150, 250), Color.FromRgb(36, 100, 206), 90)
            : stick ? new RadialGradientBrush(Color.FromRgb(22, 33, 52), Color.FromRgb(44, 62, 92)) { GradientOrigin = new(.5, .38), Center = new(.5, .45) }
            : new LinearGradientBrush(Color.FromRgb(42, 60, 89), Color.FromRgb(25, 37, 58), 90);
        border.BorderBrush = on ? Brushes.White : color != null ? new SolidColorBrush(Color.FromArgb(170, color.Value.R, color.Value.G, color.Value.B)) : new SolidColorBrush(Color.FromArgb(stick ? (byte)70 : (byte)52, 255, 255, 255));
        face.Foreground = on || color == null ? DS.Brush("DS.Text") : new SolidColorBrush(color.Value);
        border.Effect = on ? new DropShadowEffect { Color = Kit.Blue, BlurRadius = 20, ShadowDepth = 0, Opacity = .85 } : new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = .35 };
    }
    private void ShowOverview(bool on)
    {
        overview.Visibility = on ? Visibility.Visible : Visibility.Collapsed; workspace.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        ((TextBlock)((StackPanel)allButton.Content).Children[1]).Text = on ? "Back to control" : "All assignments";
        if (on) BuildOverview();
    }
    private void BuildOverview()
    {
        overview.Children.Clear(); overview.Children.Add(Kit.Section("All assignments", 0));
        if (model.Id != "x20" && !draft.PcMode) { overview.Children.Add(Kit.Info("No verified mappings for this model yet.")); return; }
        foreach (var (name, _, keys) in Groups.Take(4))
        {
            var rows = keys.Where(ButtonDraft.Sources.Contains).Select(k => (label(k), draft.Target(k) == k ? "Default" : "→ " + label(draft.Target(k)), draft.Target(k) == k ? (string?)null : "DS.AccentHi")).ToArray();
            if (rows.Length == 0) continue;
            overview.Children.Add(Kit.Text(name.ToUpperInvariant(), 12, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.Margin = new(0, 8, 0, 4)));
            overview.Children.Add(Kit.Table(rows));
        }
    }
    private static Button ActionButton(string icon, string text, string style) => new()
    {
        Style = DS.Style(style),
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 12, 0) }, new TextBlock { Text = text, FontFamily = DS.Display, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center } } }
    };
    private static MenuItem Menu(string header, Action act) { var m = new MenuItem { Header = header }; m.Click += (_, _) => act(); return m; }
    private static ControlTemplate Bare() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
    private static T? FindLast<T>(DependencyObject root) where T : DependencyObject
    {
        T? found = null;
        void Walk(DependencyObject d) { foreach (var c in LogicalTreeHelper.GetChildren(d)) if (c is DependencyObject o) { if (o is T t) found = t; Walk(o); } }
        Walk(root); return found;
    }
}

internal static class FluentExtensions
{
    public static T Also<T>(this T value, Action<T> act) { act(value); return value; }
}
