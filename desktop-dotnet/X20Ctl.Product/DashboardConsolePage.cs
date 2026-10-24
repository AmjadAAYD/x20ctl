using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>
/// The Studio landing page (owner request 10 Oct 2026): a tilted, floating controller hero that opens
/// Buttons when selected, a status/headline block with the controller pill and stats, a featured-game
/// banner, and the installed-games shelf found on this PC. Fully navigable with D-pad / arrows and A.
/// </summary>
public sealed class DashboardConsolePage : Grid
{
    private static Task<IReadOnlyList<GameEntry>>? scan;
    private readonly StackPanel shelf = new() { Orientation = Orientation.Horizontal };
    private readonly DashboardControllerPreview preview;
    private readonly Button changeIcon;
    private readonly ScrollViewer shelfScroll;
    private readonly Grid featured = new() { ClipToBounds = true };
    private readonly Image heroArt = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, Opacity = 0 };
    private readonly Image logo = new() { Stretch = Stretch.Uniform, MaxHeight = 86, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock featuredTitle = Kit.Text("", 34, "DS.Text", FontWeights.SemiBold), featuredMeta = Kit.Text("", 16, "DS.TextSoft");
    private readonly TextBlock changesCount = Kit.Text("0",18,"DS.Text",FontWeights.SemiBold);
    private readonly TextBlock connectionValue=Kit.Text("Not detected",17,"DS.TextSoft"), batteryValue=Kit.Text("Not reported",17,"DS.TextSoft"), firmwareValue=Kit.Text("Not reported",17,"DS.TextSoft");
    private readonly TextBlock profileState=Kit.Text("No game-specific setup linked",13,"DS.TextSoft");
    private string? currentTransport;
    private string? currentBatteryLevel;
    private string? currentFirmware;
    public void SetTransport(string? transport)
    {
        currentTransport = transport;
        if (inputConnected == true)
            connectionValue.Text = transport != null ? $"Detected · {transport}" : "Detected · Transport not reported";
        if (currentTransport == "2.4 GHz receiver" && currentFirmware == null)
            firmwareValue.Text = "Not readable over XInput";
    }
    public void SetBatteryLevel(string? level, string? transport = null)
    {
        if (transport != null) currentTransport = transport;
        if (currentTransport == "2.4 GHz receiver" && (level is "wired" or "receiver" or null))
        {
            if (currentBatteryLevel != null && currentBatteryLevel != "wired" && currentBatteryLevel != "receiver")
                return;
            batteryValue.Text = "Not reported (receiver)";
            return;
        }
        currentBatteryLevel = level;
        batteryValue.Text = level switch {"full"=>"Full","medium"=>"Medium","low"=>"Low","empty"=>"Empty","wired"=>"Wired",_=>level??"Not reported"};
    }
    public void SetBatteryLevel(string? level) => SetBatteryLevel(level, null);
    public event Action<TelemetryData>? TelemetryUpdated;
    public void SetFirmware(string? firmware)
    {
        currentFirmware = firmware;
        firmwareValue.Text = firmware ?? (currentTransport == "2.4 GHz receiver" ? "Not readable over XInput" : "Not reported");
    }
    private readonly TextBlock gamesCount; private readonly Button play;
    private readonly List<(Button Card, GameEntry Game)> cards = new();
    private readonly bool animate;
    private bool? inputConnected;
    private readonly TextBlock deviceState = Kit.Text("Controller offline", 28, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock stateDetail = Kit.Text("Connect your controller to use live input.", 14, "DS.TextSoft");
    private readonly TextBlock workingSetup = Kit.Text("Current working settings", 20, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock changesLabel = Kit.Text("", 14, "DS.TextSoft");
    private readonly Button setupAction = new() { Style=DS.Style("ActionButton"),HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,12,0,0) };
    public void SetWorkingState(int changes, string? setup, bool pcMode)
    {
        changesCount.Text = changes==0?"0":$"{changes} unsaved";
        workingSetup.Text = setup ?? "Current working settings";
        stateDetail.Text=pcMode?"PC mode · Settings apply on this PC":"Local working settings · Hardware unchanged";
        changesLabel.Text = pcMode ? "PC mode · Settings apply on this PC" : changes == 0 ? "No local changes" : $"{changes} local changes · Hardware unchanged";
        setupAction.Content=pcMode?"Open PC mode  →":"Edit working setup  →";
        setupAction.Tag=pcMode?"PcMode":"Buttons";
    }
    public void SetInputConnected(bool connected)
    {
        if (inputConnected == connected) return;
        inputConnected = connected;
        deviceState.Text = connected?"Connected":"Offline";
        connectionValue.Text=connected?(currentTransport!=null?$"Detected · {currentTransport}":"Detected · Transport not reported"):"Not detected";
        if (connected && currentTransport == "2.4 GHz receiver")
            firmwareValue.Text = "Not readable over XInput";
        deviceState.Foreground=DS.Brush(connected?"DS.Success":"DS.TextSoft");
    }
    private GameEntry? current;
    private int scanGeneration;
    public event Action? ConfigureRequested;
    public event Action<string>? PageRequested;

    private Button? heroFocus;
    public FrameworkElement ControllerArtwork { get; }
    /// <summary>What A does on the highlighted item, for the live footer.</summary>
    public string FocusAction => heroFocus?.IsKeyboardFocused == true ? "Configure" : play.IsKeyboardFocused ? "Play" : "Select";
    /// <summary>Y: scan the launchers again.</summary>
    public async void Rescan() { DropAll(); scanGeneration++; scan = null; cards.Clear(); covers.Clear(); shelf.Children.Clear(); Feature(null); gamesCount.Text = "…"; await Populate(); }
    /// <summary>LT/RT: jump a page (five covers) along the shelf.</summary>
    public void PageShelf(int step) { if (cards.Count == 0) return; int i = Math.Max(0, cards.FindIndex(c => c.Card.IsKeyboardFocusWithin)); cards[Math.Clamp(i + step * 5, 0, cards.Count - 1)].Card.Focus(); }
    public ControllerShowcase Showcase { get; } = new() { Tilt = 0 };
    private readonly List<(Border art, TextBlock title)> covers = new();
    private double coverHeight = FullCover;
    // tuned for the fixed 1600 x 900 design: hero 250, featured 168, a shelf of 190-pixel square games
    private const double FullCover = 190, FullFeatured = 168, FullTop = 400;

    public DashboardConsolePage(ControllerModel model, int player, int draftChanges, int savedSetups, bool reducedMotion, IReadOnlyList<SavedSetup>? setups = null, Action<string>? loadSetup = null)
    {
        animate = !reducedMotion && DS.Motion;
        ClipToBounds = true; // in a short window nothing may spill under the Studio footer
        Margin = new(0, 12, 0, 0);
        RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        RowDefinitions.Add(new() { Height = new(24) });
        RowDefinitions.Add(new() { Height = new(230) });
        RowDefinitions.Add(new() { Height = new(86) });

        var top = new Grid(); top.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); top.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); Children.Add(top);
        preview = new DashboardControllerPreview(model.Id, animate);
        ControllerArtwork = new Viewbox { Child = preview, Margin = new(0, 8, 0, 8), RenderTransformOrigin=new(.5,.5), RenderTransform=new ScaleTransform(1.18,1.18) };
        var heroButton = new Button { Content = ControllerArtwork, Template = Bare(), Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = "Configure controller buttons", HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        heroButton.Click += (_, _) => ConfigureRequested?.Invoke(); heroFocus = heroButton;
        var stage = new Grid();
        stage.Children.Add(heroButton);
        // the controller stands on the page with no card behind it, on the right (owner, 25 Oct 2026); Flip controller sits
        // at the end of the Studio's tab row
        Grid.SetColumn(stage, 1); stage.Margin = new(12, 0, 0, 0); top.Children.Add(stage);
        var status = new StackPanel();
        deviceState.Text="Offline";deviceState.FontSize=18;deviceState.FontWeight=FontWeights.SemiBold;status.Children.Add(deviceState);
        status.Children.Add(Kit.Text("Current setup",12,"DS.TextMuted",FontWeights.SemiBold).Also(t=>t.Margin=new(0,10,0,4)));status.Children.Add(workingSetup);profileState.Margin=new(0,6,0,0);status.Children.Add(profileState);
        var stateGrid=new Grid{Margin=new(0,12,0,10)};stateGrid.ColumnDefinitions.Add(new());stateGrid.ColumnDefinitions.Add(new());stateGrid.RowDefinitions.Add(new());stateGrid.RowDefinitions.Add(new());
        var cells=new[]{("Connection",connectionValue),("Battery",batteryValue),("Firmware",firmwareValue),("Changes",changesCount)};
        for(int i=0;i<cells.Length;i++){
            var text=new StackPanel();
            text.Children.Add(Kit.Text(cells[i].Item1,12,"DS.TextMuted").Also(t=>t.Margin=new(0,0,0,6)));
            cells[i].Item2.TextWrapping=TextWrapping.Wrap;
            text.Children.Add(cells[i].Item2);
            var cell=new Border{CornerRadius=new(12),Padding=new(8),Margin=new(0,0,8,8),Background=new SolidColorBrush(Color.FromArgb(16,180,205,240)),Child=text};
            if (cells[i].Item1 == "Battery" && model.Id is "x20" or "x20_pro")
            {
                cell.Cursor = Cursors.Hand;
                cell.ToolTip = "Click to refresh battery and firmware over Bluetooth";
                cell.MouseLeftButtonUp += async (_, _) =>
                {
                    batteryValue.Text = "Reading…";
                    var r = await PadBridge.Status(model.Id);
                    if (r.Ok && r.Telemetry != null)
                    {
                        if (r.Telemetry.Firmware != null) SetFirmware(r.Telemetry.Firmware);
                        if (r.Telemetry.Battery != null) SetBatteryLevel(r.Telemetry.Battery, currentTransport);
                        TelemetryUpdated?.Invoke(r.Telemetry);
                    }
                    else
                    {
                        SetBatteryLevel(currentBatteryLevel, currentTransport);
                    }
                };
            }
            Grid.SetRow(cell,i/2);Grid.SetColumn(cell,i%2);
            stateGrid.Children.Add(cell);
        }status.Children.Add(stateGrid);status.Children.Add(stateDetail);
        gamesCount=new TextBlock();SetWorkingState(draftChanges,null,false);
        var statusBox = new ScrollViewer {Content=status,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        statusBox.Margin=new(0);var statusPanel=Kit.Panel(null,null,null,statusBox);statusPanel.Margin=new(0,0,12,0);top.Children.Add(statusPanel);

        var lower = new Grid();lower.RowDefinitions.Add(new() { Height = GridLength.Auto });lower.RowDefinitions.Add(new() { Height = new(1,GridUnitType.Star) });Grid.SetRow(lower,2);Children.Add(lower);
        var header = new DockPanel { Margin = new(0,0,0,12) };
        header.Children.Add(AddButton("Add a folder",AddFolderClick));header.Children.Add(AddButton("Add a game",AddGameClick));header.Children.Add(AddButton("Rescan",Rescan));
        header.Children.Add(Kit.Text("Games & profiles",18,"DS.Text",FontWeights.SemiBold));lower.Children.Add(header);
        shelfScroll = new ScrollViewer { Content = shelf, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new(14,20,14,12) };
        Grid.SetRow(shelfScroll,1);lower.Children.Add(shelfScroll);
        shelfScroll.ScrollChanged += (_, _) => FollowShelf();

        // A single selected-game context replaces the separate Play and Game setup cards.
        var context = new Grid { Margin = new(0,12,0,0) };context.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});context.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center};featuredTitle.FontSize=18;featuredTitle.Margin=new(0,0,0,3);featuredMeta.FontSize=12;words.Children.Add(featuredTitle);words.Children.Add(featuredMeta);context.Children.Add(words);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        var choices = new ComboBox { Width=170,MinHeight=38,Margin=new(8,0,4,0),DisplayMemberPath="Name",ToolTip="Choose a saved controller setup",ItemsSource=setups?.Where(x=>x.ModelId==model.Id&&x.Snapshot.HasValue).ToArray()??[] };
        bool hasSetups=choices.Items.Count>0;choices.IsEnabled=hasSetups;choices.Visibility=Visibility.Visible;actions.Children.Add(choices);
        var load=AddButton("Load setup",()=>{
            if(choices.SelectedItem is not SavedSetup chosen||loadSetup==null)return;
            try{loadSetup(chosen.Id);workingSetup.Text=chosen.Name;}
            catch(Exception ex) when(ex is InvalidOperationException or InvalidDataException){featuredMeta.Text="This setup cannot be loaded for this controller.";}
        });load.IsEnabled=false;load.Visibility=Visibility.Visible;choices.SelectionChanged+=(_,_)=>load.IsEnabled=choices.SelectedItem!=null;actions.Children.Add(load);
        var edit=AddButton("Edit",()=>PageRequested?.Invoke("Setups"));edit.ToolTip="Manage controller setups";actions.Children.Add(edit);
        var gameActions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0)};
        play=new Button { Style=DS.Style("PrimaryButton"),Content="▶  Play",MinWidth=100,Height=42,MinHeight=42,Margin=new(8,0,0,0) };play.Click+=(_,_)=>PlayGame(current,heroArt);
        changeIcon=AddButton("Change icon",ChangeIcon);changeIcon.ToolTip="Change the selected game's image";changeIcon.Margin=new(0);changeIcon.Padding=new(10,0,10,0);gameActions.Children.Add(changeIcon);gameActions.Children.Add(play);actions.Children.Add(gameActions);
        Grid.SetColumn(actions,1);context.Children.Add(actions);Grid.SetRow(context,3);Children.Add(context);
        // One surface contains the library and its selected-game command row.
        Children.Remove(lower);Children.Remove(context);
        var library=new Grid();library.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});library.RowDefinitions.Add(new(){Height=new(70)});
        Grid.SetRow(lower,0);Grid.SetRow(context,1);library.Children.Add(lower);library.Children.Add(context);
        var libraryPanel=Kit.Panel(null,null,null,library,new Thickness(16,12,16,8));Grid.SetRow(libraryPanel,2);Grid.SetRowSpan(libraryPanel,2);Children.Add(libraryPanel);
        Feature(null);
        Loaded += async (_, _) => { if (!EntryTransitionPending) { preview.Start(); await Populate(); } };
        Unloaded += (_, _) => preview.Stop();

        PreviewKeyDown += OnKey;
        Grid.SetRowSpan(raiseLayer, 4); Children.Add(raiseLayer); // last, so a raised tile is above everything on the page
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Fit before Grid measures its children. Waiting for SizeChanged lets the old minimum
        // heights expand this page beyond the viewport, so the footer clips the game shelf.
        if (double.IsFinite(availableSize.Height)) Fit(availableSize.Height);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Fit the page to its height. Full size needs about 730 px; below that the featured banner gives way first,
    /// then the game covers, then the hero row, so every part stays whole and nothing spills under the footer.</summary>
    private void Fit(double height)
    {
        if (height <= 0) return;
        RowDefinitions[0].MinHeight = 0;
        double cover = Math.Clamp(height * .25, 110, 190);
        RowDefinitions[2].Height = new(cover + 150);
        if (Math.Abs(cover - coverHeight) > .5) { coverHeight = cover; foreach (var (art, title) in covers) SizeCover(art, title); }
    }
    private void SizeCover(Border art, TextBlock title)
    {
        double w = Math.Round(coverHeight), h = Math.Round(coverHeight); // every cover shares the Genshin square frame
        if(art.Child is Image cover)cover.Stretch=Stretch.UniformToFill;
        art.Width = w; art.Height = h; art.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14); title.Width = Math.Max(130,w); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap;
        if (art.Parent is Grid frame) { frame.Width = w + 10; frame.Height = h + 10; }
    }

    /// <summary>Moves keyboard/gamepad focus to the hero so the page is immediately navigable.</summary>
    public bool IsRear => preview.IsRear;
    /// <summary>Turns the controller over; the button for it is in the Studio's tab row.</summary>
    public void FlipController() => preview.Flip();
    public void FocusHero() => Dispatcher.BeginInvoke(() => (cards.FirstOrDefault().Card ?? (UIElement)play).Focus(), System.Windows.Threading.DispatcherPriority.Input);

    internal bool EntryTransitionPending { get; set; }
    internal void CompleteEntryTransition()
    {
        EntryTransitionPending = false;
        preview.Start();
        _ = Populate();
    }

    private async Task Populate()
    {
        if (cards.Count > 0) return;
        int generation = scanGeneration;
        scan ??= GameLibrary.ScanAsync();
        IReadOnlyList<GameEntry> list; try { list = await scan; } catch { list = []; }
        if (generation != scanGeneration || cards.Count > 0) return;
        shelf.Children.Clear();
        gamesCount.Text = list.Count.ToString();
        if (list.Count == 0) { featuredMeta.Text = "No games found"; shelf.Children.Add(Kit.Info("No installed games found. Add a game or folder.")); return; }
        foreach (var g in list) { var card = Card(g); shelf.Children.Add(card); cards.Add((card, g)); }
        Feature(list[0]);
    }

    /// <summary>Fades a cover's selection frame to the given strength (1 chosen, about .4 hovered, 0 none).</summary>
    private static void ShowRing(Border art, double strength)
    {
        if (art.Tag is not Border ring) return;
        if (ring.RenderTransform is not ScaleTransform settle) { settle = new ScaleTransform(1, 1); ring.RenderTransformOrigin = new(.5, .5); ring.RenderTransform = settle; }
        bool arriving = strength >= .9 && ring.Opacity < .5;
        if (DS.Motion) ring.BeginAnimation(OpacityProperty, DS.To(strength, 180)); else { ring.BeginAnimation(OpacityProperty, null); ring.Opacity = strength; }
        // arriving, the frame closes in from slightly outside and settles on the cover
        if (DS.Motion && arriving)
        {
            DoubleAnimation Close() => new(1.07, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            settle.BeginAnimation(ScaleTransform.ScaleXProperty, Close()); settle.BeginAnimation(ScaleTransform.ScaleYProperty, Close());
        }
    }

    // One frame follows the pointer (owner, 25 Oct 2026): the tile under it is framed exactly as a chosen game is, and the
    // chosen game's frame steps aside while the pointer is on another tile, then returns when it leaves.
    private Button? hovered;
    private void RefreshRings()
    {
        foreach (var (card, game) in cards)
        {
            double want = hovered != null ? (ReferenceEquals(card, hovered) ? 1 : 0) : (current?.Id == game.Id || card.IsKeyboardFocused ? 1 : 0);
            if (card.Tag is not Border art) continue;
            ShowRing(art, want);
            if (raised.TryGetValue(card, out var r) && r.Clone.Tag is Border copy) ShowRing(copy, want);
        }
    }

    /// <param name="pop">a copy for the raised layer: no handlers, no registration, never hit</param>
    private Button Card(GameEntry g, bool pop = false)
    {
        UIElement face;
        var cover = Bitmap(GameArtwork.PathFor(g));
        if (cover != null) face = new Image { Source = cover, Stretch = Stretch.Uniform, SnapsToDevicePixels=true, UseLayoutRounding=true };
        else
        {
            string initials = string.Concat(g.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])));
            int hue = Math.Abs(g.Name.GetHashCode()) % 360; var c = Hsl(hue, .45, .28);
            face = new Grid { Background = new LinearGradientBrush(c, Hsl(hue, .5, .16), 90), Children = { Kit.Text(initials, 40, "DS.Text").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Opacity = .85; }) } };
        }
        if(face is Image image)RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        var art = new Border { CornerRadius = new(12), Background=new SolidColorBrush(Color.FromRgb(17,26,41)),ClipToBounds = true, Child = face };
        // The selection frame: a slim blue edge with a dark line inside it (so it never fights the artwork) and a soft glow.
        // It fades in and out; it replaces the old flat 2 px border, which read as a white box on bright covers.
        // a bigger frame, set outside the cover, so a neighbouring tile rising never covers it (owner, 25 Oct 2026)
        var ring = new Border { CornerRadius = new(17), BorderThickness = new(3.5), IsHitTestVisible = false, Opacity = 0,
            BorderBrush = new LinearGradientBrush(Color.FromRgb(168, 214, 255), Color.FromRgb(58, 138, 255), 45),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(58, 138, 255), BlurRadius = 22, ShadowDepth = 0, Opacity = .7 },
            Child = new Border { CornerRadius = new(13.5), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(120, 4, 8, 20)) } };
        art.Tag = ring; art.Margin = new(5); // the frame is the tile's own 5 px edge, so it rises and grows with the cover
        var store = new Border { CornerRadius = new(9), Padding = new(8, 2, 8, 3), Background = new SolidColorBrush(Color.FromArgb(200, 6, 10, 22)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 15, 15), Child = Kit.Text(g.Store, 11.5, "DS.TextSoft", FontWeights.SemiBold) };
        var title = Kit.Text(g.Name, 14, "DS.TextSoft"); title.Margin = new(7, 1, 0, 0);
        if (!pop) covers.Add((art, title));
        SizeCover(art, title);
        var stack = new StackPanel { Children = { new Grid { HorizontalAlignment=HorizontalAlignment.Left, Children = { art, ring, store } }, title }, RenderTransformOrigin = new(.5, .4), RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, 1), new TranslateTransform() } } };
        SizeCover(art,title);
        var b = new Button { Content = stack, Template = Bare(), Margin = new(0, 0, 18, 2), Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = $"{g.Name} · {g.Store}" };
        b.Tag = art;
        if (pop) { b.IsHitTestVisible = false; b.Focusable = false; return b; }
        b.GotKeyboardFocus += (_, _) => { Feature(g); RefreshRings(); title.Foreground = DS.Brush("DS.Text"); Settle(b, g); b.BringIntoView(new Rect(-12,0,coverHeight+12,coverHeight+34)); };
        b.LostKeyboardFocus += (_, _) => { RefreshRings(); Settle(b, g); };
        b.MouseEnter+=(_,_)=>{ hovered = b; Settle(b, g); RefreshRings(); };
        b.MouseLeave+=(_,_)=>{ if (ReferenceEquals(hovered, b)) hovered = null; RefreshRings(); Settle(b, g); };

        b.Click += (_, _) => { Feature(g); RefreshRings(); };
        return b;
    }

    private Button AddButton(string text, Action click)
    {
        var button = new Button { Style = DS.Style(text.StartsWith("Add ")?"PrimaryButton":"ActionButton"), Height = 42, MinHeight = 42, Padding = new(22, 0, 22, 0), Margin = new(8, 0, 4, 0), Content = Kit.Text(text, 14, "DS.Text"), FocusVisualStyle = null };
        button.Click += (_, _) => click(); DockPanel.SetDock(button, Dock.Right); return button;
    }
    private void AddGameClick()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Programs (*.exe)|*.exe", Title = "Add a game" };
        if (dialog.ShowDialog() == true) { GameLibrary.AddGame(dialog.FileName); Rescan(); }
    }
    private void AddFolderClick()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Add a folder" };
        if (dialog.ShowDialog() == true) { GameLibrary.AddFolder(dialog.FolderName); Rescan(); }
    }

    private void ChangeIcon()
    {
        if (current is not { } game) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Change game icon", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog() != true) return;
        try {
            GameArtwork.Save(game, dialog.FileName);
            current = null; Feature(game);
            int index = cards.FindIndex(c => c.Game.Id == game.Id);
            if (index >= 0) { var replacement = Card(game); shelf.Children.Remove(cards[index].Card); shelf.Children.Insert(index, replacement); cards[index] = (replacement, game); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.IO.FileFormatException) {
            featuredMeta.Text = "Could not use this image. Choose a PNG or JPEG.";
        }
    }

    private bool playing;
    /// <summary>Play: the game's picture grows from its tile until it fills the page, the game starts, and once it is really
    /// running the page says "In game" and gives itself back (owner request, 13 Oct 2026).</summary>
    private async void PlayGame(GameEntry? game, FrameworkElement source)
    {
        if (game == null || playing) return; playing = true;
        Grid overlay = Window.GetWindow(this) is ProductWindow window ? window.Host : this;
        Border? zoom = null;
        try
        {
            var image = Bitmap(GameArtwork.PathFor(game)) ?? Bitmap(game.Hero);
            var at = source.TranslatePoint(new Point(0, 0), overlay);
            double w = Math.Max(1, source.ActualWidth), h = Math.Max(1, source.ActualHeight), pageW = overlay.ActualWidth, pageH = overlay.ActualHeight;
            Brush fill = image != null ? new ImageBrush(image) { Stretch = Stretch.Uniform } : new LinearGradientBrush(Color.FromRgb(28, 52, 110), Color.FromRgb(10, 18, 44), 90);
            zoom = new Border { Background = fill, CornerRadius = new(14), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(at.X, at.Y, 0, 0), Width = w, Height = h, ClipToBounds = true };
            var dim = new Border { Background = new SolidColorBrush(Color.FromArgb(150, 2, 6, 18)) };
            var label = Kit.Text("Starting " + game.Name, 30, "DS.Text", FontWeights.SemiBold); label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center; label.Opacity = 0;
            zoom.Child = new Grid { Children = { dim, label } };
            // Letterbox the complete artwork rather than cropping it to the screen ratio.
            Grid.SetRowSpan(zoom, 5); Panel.SetZIndex(zoom, 50); overlay.Children.Add(zoom);
            int ms = animate ? 380 : 0;
            if (ms > 0)
            {
                var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
                zoom.BeginAnimation(MarginProperty, new System.Windows.Media.Animation.ThicknessAnimation(new Thickness(0), TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
                zoom.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(pageW, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
                zoom.BeginAnimation(HeightProperty, new System.Windows.Media.Animation.DoubleAnimation(pageH, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease });
                await Task.Delay(ms);
            }
            zoom.BeginAnimation(MarginProperty, null); zoom.BeginAnimation(WidthProperty, null); zoom.BeginAnimation(HeightProperty, null);
            zoom.Margin = new(0); zoom.Width = pageW; zoom.Height = pageH; zoom.CornerRadius = new(0);
            label.Opacity = 1;
            if (!ReviewSandbox.Active)
            {
                try { GameLibrary.Launch(game); }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { label.Text = "Could not start " + game.Name; await Task.Delay(1800); return; }
                // A delay alone is never evidence that a game started.
                bool up = false;
                for (int i = 0; i < 90 && !up && IsLoaded; i++)
                {
                    await Task.Delay(1000);
                    up = await Task.Run(() => GameLibrary.IsRunning(game));
                }
                if (!up) { label.Text = "Start not detected · " + game.Name; await Task.Delay(1800); return; }
            }
            label.Text = "In game · " + game.Name;
            await Task.Delay(2200);
            if (animate) { zoom.BeginAnimation(OpacityProperty, DS.To(0, 320)); await Task.Delay(330); }
        }
        finally { if (zoom != null) overlay.Children.Remove(zoom); playing = false; }
    }

    private void Feature(GameEntry? g)
    {
        if (g != null && ReferenceEquals(g, current)) return; current = g; RefreshRings();
        for(int i=0;i<Math.Min(cards.Count,covers.Count);i++){bool selected=cards[i].Game.Id==g?.Id;covers[i].title.Foreground=DS.Brush(selected?"DS.Text":"DS.TextSoft");}
        var hero = g == null ? null : Bitmap(GameArtwork.PathFor(g)) ?? Bitmap(g.Hero); var mark = Bitmap(g?.Logo);
        if (animate && heroArt.Source != hero) { heroArt.BeginAnimation(OpacityProperty, null); heroArt.Opacity = 0; }
        heroArt.Source = hero; if (hero != null) { if (animate) heroArt.BeginAnimation(OpacityProperty, DS.To(1, 320)); else heroArt.Opacity = 1; }
        logo.Source = mark; logo.Visibility = mark != null ? Visibility.Visible : Visibility.Collapsed; logo.Margin = new(0, 8, 0, 6);
        featuredTitle.Text = g?.Name ?? "Your library"; featuredTitle.Visibility = Visibility.Visible;
        featuredMeta.Text = g == null ? "Scanning installed games…" : $"{g.Store} · {(g.LastPlayed is { } t ? "Last played " + t.ToString("d MMM yyyy") : "Installed")}";
        play.IsEnabled = changeIcon.IsEnabled = g != null;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int index = cards.FindIndex(c => c.Card.IsKeyboardFocusWithin);
        if (e.Key is Key.Right or Key.Left && index >= 0)
        {
            int step = e.Key == Key.Right ? 1 : -1;
            cards[Math.Clamp(index + step, 0, cards.Count - 1)].Card.Focus(); e.Handled = true;
        }
        else if(index>=0 && e.Key==Key.Up){heroFocus?.Focus();e.Handled=true;}
        else if(index>=0 && e.Key==Key.Down){play.Focus();e.Handled=true;}
        else if (e.Key is Key.Enter or Key.A && Keyboard.FocusedElement is Button focused && !e.IsRepeat) { focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
    }

    // A hovered or focused tile rises like a hero card on a layer above the whole page, so no panel or scroll area can cut it
    // (owner, 25 Oct 2026): its copy grows and lifts there while the tile itself waits, unseen, in its place.
    private readonly Canvas raiseLayer = new() { IsHitTestVisible = false };
    private readonly Dictionary<Button, (Button Clone, ScaleTransform Scale, TranslateTransform Move)> raised = new();
    /// <summary>Review hook: raises a tile as if hovered.</summary>
    internal void ReviewRaise(int index) { var (card, game) = cards[index]; hovered = card; Settle(card, game, 1.07); RefreshRings(); }
    private void Settle(Button b, GameEntry g, double? force = null)
    {
        double want = force ?? (b.IsMouseOver ? 1.07 : b.IsKeyboardFocused ? 1.03 : 1), y = want > 1 ? -(want - 1) * 60 : 0;
        if (!raised.TryGetValue(b, out var r))
        {
            if (want <= 1 || b.Content is not FrameworkElement stack || b.Tag is not Border art) return;
            var clone = Card(g, true); var scale = new ScaleTransform(1, 1); var move = new TranslateTransform();
            clone.Margin = new(0); clone.RenderTransformOrigin = new(.5, .4); clone.RenderTransform = new TransformGroup { Children = { scale, move } };
            if (clone.Tag is Border copy && copy.Tag is Border copyRing && art.Tag is Border ring) { copyRing.Opacity = ring.Opacity; }
            if (clone.Content is StackPanel panel && panel.Children.Count > 1 && stack is StackPanel own && own.Children.Count > 1 && panel.Children[1] is TextBlock t && own.Children[1] is TextBlock ot) t.Foreground = ot.Foreground;
            var at = b.TransformToAncestor(this).Transform(new Point(0, 0)); Canvas.SetLeft(clone, at.X); Canvas.SetTop(clone, at.Y);
            raiseLayer.Children.Add(clone); stack.Opacity = 0;
            r = raised[b] = (clone, scale, move);
        }
        if (!animate) { r.Scale.ScaleX = r.Scale.ScaleY = want; r.Move.Y = y; if (want <= 1) Drop(b); return; }
        DoubleAnimation Go(double v) => new(v, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var grow = Go(want);
        if (want <= 1) grow.Completed += (_, _) => { if (!b.IsMouseOver && !b.IsKeyboardFocused) Drop(b); };
        r.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow); r.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, Go(want)); r.Move.BeginAnimation(TranslateTransform.YProperty, Go(y));
    }
    private void Drop(Button b)
    {
        if (!raised.Remove(b, out var r)) return;
        raiseLayer.Children.Remove(r.Clone); if (b.Content is UIElement stack) stack.Opacity = 1;
    }
    /// <summary>A raised copy follows its tile when the shelf scrolls.</summary>
    private void FollowShelf()
    {
        foreach (var (b, r) in raised) { var at = b.TransformToAncestor(this).Transform(new Point(0, 0)); Canvas.SetLeft(r.Clone, at.X); Canvas.SetTop(r.Clone, at.Y); }
    }
    private void DropAll() { foreach (var b in raised.Keys.ToArray()) Drop(b); }
    private static void Enter(FrameworkElement e, int delay)
    {
        e.Opacity = 0; var t = new TranslateTransform(0, 18); e.RenderTransform = t;
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(delay) });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = DS.EaseOut });
    }
    private static BitmapImage? Bitmap(string? path)
    {
        if (path == null || !File.Exists(path)) return null;
        try { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); return b; } catch { return null; }
    }
    private static Color Hsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2;
        var (r, g, b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
    private static ControlTemplate Bare() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
}
