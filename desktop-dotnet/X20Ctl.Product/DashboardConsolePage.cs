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
    public void SetBatteryLevel(string? level)=>batteryValue.Text=level switch {"full"=>"Full","medium"=>"Medium","low"=>"Low","empty"=>"Empty","wired"=>"Wired",_=>"Not reported"};
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
        connectionValue.Text=connected?"Detected · Transport not reported":"Not detected";
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
    public async void Rescan() { scanGeneration++; scan = null; cards.Clear(); covers.Clear(); shelf.Children.Clear(); Feature(null); gamesCount.Text = "…"; await Populate(); }
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
        ControllerArtwork = new Viewbox { Child = preview, Margin = new(0, 30, 0, 24), RenderTransformOrigin=new(.5,.5), RenderTransform=new ScaleTransform(1.18,1.18) };
        var heroButton = new Button { Content = ControllerArtwork, Template = Bare(), Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = "Configure controller buttons", HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        heroButton.Click += (_, _) => ConfigureRequested?.Invoke(); heroFocus = heroButton;
        var stage = new Grid();
        stage.Children.Add(heroButton);
        var flip=new Button{Content="Flip controller",Style=DS.Style("ActionButton"),Height=42,MinHeight=42,Padding=new(22,0,22,0),FontSize=14,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,ToolTip="Show the back of this controller"};
        System.Windows.Automation.AutomationProperties.SetName(flip,"Flip controller");
        flip.Click+=(_,_)=>{preview.Flip();flip.ToolTip=preview.IsRear?"Show the front of this controller":"Show the back of this controller";};stage.Children.Add(flip);
        var controllerPanel=Kit.Panel(null,null,null,stage);controllerPanel.Margin=new(0,0,12,0);top.Children.Add(controllerPanel);
        var status = new StackPanel();
        deviceState.Text="Offline";deviceState.FontSize=18;deviceState.FontWeight=FontWeights.SemiBold;status.Children.Add(deviceState);
        status.Children.Add(Kit.Text("Current setup",12,"DS.TextMuted",FontWeights.SemiBold).Also(t=>t.Margin=new(0,10,0,4)));status.Children.Add(workingSetup);profileState.Margin=new(0,6,0,0);status.Children.Add(profileState);
        var stateGrid=new Grid{Margin=new(0,12,0,10)};stateGrid.ColumnDefinitions.Add(new());stateGrid.ColumnDefinitions.Add(new());stateGrid.RowDefinitions.Add(new());stateGrid.RowDefinitions.Add(new());
        var cells=new[]{("Connection",connectionValue),("Battery",batteryValue),("Firmware",firmwareValue),("Changes",changesCount)};
        for(int i=0;i<cells.Length;i++){var text=new StackPanel();text.Children.Add(Kit.Text(cells[i].Item1,12,"DS.TextMuted").Also(t=>t.Margin=new(0,0,0,6)));cells[i].Item2.TextWrapping=TextWrapping.Wrap;text.Children.Add(cells[i].Item2);var cell=new Border{CornerRadius=new(12),Padding=new(8),Margin=new(0,0,8,8),Background=new SolidColorBrush(Color.FromArgb(16,180,205,240)),Child=text};Grid.SetRow(cell,i/2);Grid.SetColumn(cell,i%2);stateGrid.Children.Add(cell);}status.Children.Add(stateGrid);status.Children.Add(stateDetail);
        gamesCount=new TextBlock();SetWorkingState(draftChanges,null,false);
        var statusBox = new ScrollViewer {Content=status,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        statusBox.Margin=new(0);var statusPanel=Kit.Panel(null,null,null,statusBox);Grid.SetColumn(statusPanel,1);top.Children.Add(statusPanel);

        var lower = new Grid();lower.RowDefinitions.Add(new() { Height = GridLength.Auto });lower.RowDefinitions.Add(new() { Height = new(1,GridUnitType.Star) });Grid.SetRow(lower,2);Children.Add(lower);
        var header = new DockPanel { Margin = new(0,0,0,12) };
        header.Children.Add(AddButton("Add a folder",AddFolderClick));header.Children.Add(AddButton("Add a game",AddGameClick));header.Children.Add(AddButton("Rescan",Rescan));
        header.Children.Add(Kit.Text("Games & profiles",18,"DS.Text",FontWeights.SemiBold));lower.Children.Add(header);
        shelfScroll = new ScrollViewer { Content = shelf, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new(0,4,0,8) };
        Grid.SetRow(shelfScroll,1);lower.Children.Add(shelfScroll);

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
        RowDefinitions[2].Height = new(cover + 130);
        if (Math.Abs(cover - coverHeight) > .5) { coverHeight = cover; foreach (var (art, title) in covers) SizeCover(art, title); }
    }
    private void SizeCover(Border art, TextBlock title)
    {
        double w = Math.Round(coverHeight), h = Math.Round(coverHeight); // every cover shares the Genshin square frame
        if(art.Child is Image cover)cover.Stretch=Stretch.UniformToFill;
        art.Width = w; art.Height = h; art.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14); title.Width = Math.Max(130,w); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap;
        if (art.Parent is Grid frame) { frame.Width = w; frame.Height = h; }
    }

    /// <summary>Moves keyboard/gamepad focus to the hero so the page is immediately navigable.</summary>
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

    private Button Card(GameEntry g)
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
        var art = new Border { CornerRadius = new(12), Background=new SolidColorBrush(Color.FromRgb(17,26,41)),ClipToBounds = true, Child = face, BorderThickness = new(2), BorderBrush = Brushes.Transparent };
        var store = new Border { CornerRadius = new(9), Padding = new(8, 2, 8, 3), Background = new SolidColorBrush(Color.FromArgb(200, 6, 10, 22)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 10, 10), Child = Kit.Text(g.Store, 11.5, "DS.TextSoft", FontWeights.SemiBold) };
        var title = Kit.Text(g.Name, 14, "DS.TextSoft"); title.Margin = new(2, 6, 0, 0);
        covers.Add((art, title)); SizeCover(art, title);
        var stack = new StackPanel { Children = { new Grid { HorizontalAlignment=HorizontalAlignment.Left, Children = { art, store } }, title }, RenderTransformOrigin = new(.5, .4), RenderTransform = new ScaleTransform(1, 1) };
        SizeCover(art,title);
        var b = new Button { Content = stack, Template = Bare(), Margin = new(0, 0, 12, 12), Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = $"{g.Name} · {g.Store}" };
        b.GotKeyboardFocus += (_, _) => { Feature(g); art.BorderBrush = DS.Brush("DS.AccentHi"); Lift(stack, 1.01); title.Foreground = DS.Brush("DS.Text"); b.BringIntoView(new Rect(-12,0,coverHeight+12,coverHeight+34)); };
        b.LostKeyboardFocus += (_, _) => { art.BorderBrush = current?.Id==g.Id?DS.Brush("DS.AccentHi"):Brushes.Transparent;Lift(stack,1); };
        b.MouseEnter+=(_,_)=>Lift(stack,1.01);b.MouseLeave+=(_,_)=>Lift(stack,1);

        b.Click += (_, _) => Feature(g);
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
        if (g != null && ReferenceEquals(g, current)) return; current = g;
        for(int i=0;i<Math.Min(cards.Count,covers.Count);i++){bool selected=cards[i].Game.Id==g?.Id;covers[i].art.BorderBrush=selected?DS.Brush("DS.AccentHi"):Brushes.Transparent;covers[i].title.Foreground=DS.Brush(selected?"DS.Text":"DS.TextSoft");}
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

    private void Lift(FrameworkElement e, double to)
    {
        // a transform shared from a style is frozen; give the element its own before animating it
        if(e.RenderTransform is not TranslateTransform s){s=new TranslateTransform();e.RenderTransform=s;}
        double y=to>1?-2:0;if(!animate){s.Y=y;return;}s.BeginAnimation(TranslateTransform.YProperty,DS.To(y,160));
    }
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
