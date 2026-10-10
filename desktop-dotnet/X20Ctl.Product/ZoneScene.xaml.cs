using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using X20Ctl.Simulation;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public partial class ZoneScene : UserControl
{
    public ZoneState State { get; private set; } = new();
    private readonly ZoneNavigation navigation = new();
    private readonly PointerMode pointerMode = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private DockView[] docks = [];
    private CancellationTokenSource? transition;
    private int? displayedHero;
    private ZoneScenario scenario;
    private Point pointer;
    public IReadOnlyList<DockView> Docks => docks;
    public event Action<int, bool>? ControllerActionRequested;
    public event Action? SupportRequested;
    public event Action? SettingsRequested;
    private void Support(object sender,RoutedEventArgs e)=>SupportRequested?.Invoke();
    private void Settings(object sender,RoutedEventArgs e)=>SettingsRequested?.Invoke();
    public bool SimulationNavigation { get; private set; }
    public bool ReducedMotion { get; }
    public bool EntranceAnimated { get; private set; }
    public bool IsTransitioning { get; private set; }
    public const int RetractMs = 130, MoveMs = 460, SettleMs = 220;
    public ZoneScene() : this(false) { }
    public ZoneScene(bool reducedMotion)
    {
        ReducedMotion = reducedMotion || !SystemParameters.ClientAreaAnimation;
        InitializeComponent(); Loaded += (_, _) => UpdateOverviewPill(); ZoneHints.Children.Add(GlyphHint.Footer(("A", "Select player"), ("B", "Back"), ("☰", "Tools")));
        // in a narrow window the developer notes in the footer give way to the controls
        Surface.SizeChanged += (_, _) => { var notes = Surface.ActualWidth < 1300 ? Visibility.Collapsed : Visibility.Visible; ScenarioHint.Visibility = MotionHint.Visibility = notes; };
        Surface.SizeChanged += (_, _) => Surface.Clip = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), OuterFrame.CornerRadius.TopLeft, OuterFrame.CornerRadius.TopLeft);
        if (ReducedMotion) MotionHint.Text = "F8 Demo assignments · Reduced motion";
        LoadScenario(ZoneScenario.Empty);
    }
    public void LoadScenario(ZoneScenario value)
    {
        transition?.Cancel(); scenario = value; State = new ZoneState(value); displayedHero = null;
        foreach (var dock in docks) DockStage.Children.Remove(dock);
        // Four persistent player views; focus changes move them, never replace their content.
        docks = Enumerable.Range(0, 4).Select(i => new DockView(i, State.Players[i])).ToArray();
        foreach (var dock in docks)
        {
            dock.Selected += async index => await FocusPlayerAsync(index);
            dock.ActionRequested += (index, change) => ControllerActionRequested?.Invoke(index, change);
            DockStage.Children.Add(dock);
        }
        ScenarioHint.Text = value switch { ZoneScenario.OneController => "One mock assignment · F9 changes fixture", ZoneScenario.MultipleControllers => "Three mock assignments · F9 changes fixture", _ => "All empty · no hardware access" };
        FeedbackText.Text = "Select any player. All four docks begin equal.";
        SnapLayout();
    }
    public void RestoreAssignments(IReadOnlyList<string?> ids, ControllerCatalog catalog)
    {
        State.Restore(ids, catalog);
        for (int i = 0; i < 4; i++) docks[i].RefreshPlayer(State.Players[i]);
        SnapLayout();
        ScenarioHint.Text = "Saved assignments · hardware disconnected";
    }
    public void AssignController(int index, ControllerModel model)
    {
        State.Assign(index, model);
        docks[index].RefreshPlayer(State.Players[index]);
        SnapLayout();
        FeedbackText.Text = $"{model.Name} assigned to Player {index + 1}. Hardware connection is separate.";
        ScenarioHint.Text = "Assigned models · hardware disconnected";
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SnapLayout();
        if (!ReducedMotion) { EntranceAnimated = true; Workspace.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))); }
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) { transition?.Cancel(); CompositionTarget.Rendering -= MoveFrame; }
    private void OnStageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        transition?.Cancel();
        SnapLayout();
    }
    private void SnapLayout()
    {
        if (DockStage.ActualWidth <= 48 || DockStage.ActualHeight <= 48) return;
        PlaceViews(0); displayedHero = State.FocusedPlayer; IsTransitioning = false;
        FocusBloom.BeginAnimation(OpacityProperty, null);
        FocusBloom.Opacity = State.FocusedPlayer.HasValue ? 1 : 0;
        foreach (var dock in docks) { dock.FadeHeroDetails(1, 0); dock.SettleArtwork(1, 0); }
    }
    private DockBounds[] moveFrom = [], moveTo = [];
    private readonly Stopwatch moveClock = new();
    private int moveMs;
    /// <summary>Move every card to its place for the current focus. With a duration, the cards glide there frame by frame
    /// (the "fwoosh": fast through the middle, soft at both ends), each one's content reflowing at its in-between size.</summary>
    private void PlaceViews(int duration)
    {
        var bounds = ZoneLayout.Calculate(DockStage.ActualWidth, DockStage.ActualHeight, State.FocusedPlayer);
        CompositionTarget.Rendering -= MoveFrame;
        if (duration <= 0) { for (int i = 0; i < 4; i++) docks[i].SetBounds(bounds[i], Role(i).hero, Role(i).secondary); return; }
        moveFrom = docks.Select(d => d.RenderedBounds.Width > 0 ? d.RenderedBounds : bounds[d.Index]).ToArray(); moveTo = bounds; moveMs = duration;
        moveClock.Restart(); CompositionTarget.Rendering += MoveFrame;
    }
    private (bool hero, bool secondary) Role(int i) => (State.FocusedPlayer == i, State.FocusedPlayer.HasValue && State.FocusedPlayer != i);
    private void MoveFrame(object? sender, EventArgs e)
    {
        double t = Math.Clamp(moveClock.Elapsed.TotalMilliseconds / moveMs, 0, 1);
        double k = t < .5 ? 16 * Math.Pow(t, 5) : 1 - Math.Pow(-2 * t + 2, 5) / 2; // ease in-out quint
        for (int i = 0; i < 4; i++)
        {
            var a = moveFrom[i]; var b = moveTo[i];
            docks[i].SetBounds(new(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k, a.Width + (b.Width - a.Width) * k, a.Height + (b.Height - a.Height) * k), Role(i).hero, Role(i).secondary);
        }
        if (t >= 1) CompositionTarget.Rendering -= MoveFrame;
    }
    private void FinishMove() { if (moveTo.Length == 4) { CompositionTarget.Rendering -= MoveFrame; for (int i = 0; i < 4; i++) docks[i].SetBounds(moveTo[i], Role(i).hero, Role(i).secondary); } }
    public async Task FocusPlayerAsync(int? index)
    {
        if (index == State.FocusedPlayer) return;
        transition?.Cancel();
        var operation = new CancellationTokenSource(); transition = operation;
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        FeedbackText.Text = State.Feedback; UpdateOverviewPill();
        if (DockStage.ActualWidth <= 48 || DockStage.ActualHeight <= 48) { transition = null; operation.Dispose(); return; }
        IsTransitioning = true;
        try
        {
            if (!ReducedMotion)
            {
                // every card's words step aside before the cards move, so nothing reflows in view
                foreach (var dock in docks) dock.FadeHeroDetails(0, RetractMs);
                await Task.Delay(RetractMs, operation.Token);
            }
            operation.Token.ThrowIfCancellationRequested();
            var start = index.HasValue ? docks[index.Value].RenderedBounds : null;
            PlaceViews(ReducedMotion ? 0 : MoveMs);
            displayedHero = index;
            foreach (var dock in docks) Panel.SetZIndex(dock, 0);
            if (index.HasValue)
            {
                docks[index.Value].FadeHeroDetails(0, 0);
                Panel.SetZIndex(docks[index.Value], 2);
                LightFocus(start!, ReducedMotion ? 0 : MoveMs);
            }
            else FocusBloom.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ReducedMotion ? 0 : MoveMs)));
            if (!ReducedMotion) await Task.Delay(MoveMs, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            FinishMove();
            foreach (var dock in docks) { dock.FadeHeroDetails(1, ReducedMotion ? 0 : SettleMs); dock.SettleArtwork(.97, ReducedMotion ? 0 : SettleMs); }
            if (!ReducedMotion) await Task.Delay(SettleMs, operation.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(transition, operation)) { IsTransitioning = false; transition = null; }
            operation.Dispose();
        }
    }
    private void LightFocus(DockBounds start, int milliseconds)
    {
        // a soft accent-blue bloom travels with the chosen card and settles behind the hero
        var target = ZoneLayout.Calculate(DockStage.ActualWidth, DockStage.ActualHeight, State.FocusedPlayer)[State.FocusedPlayer!.Value];
        FocusBloom.Width = target.Width * 1.15; FocusBloom.Height = target.Height * .95;
        FocusBloom.Fill = new RadialGradientBrush(Color.FromArgb(40, DockView.Accent.R, DockView.Accent.G, DockView.Accent.B), Color.FromArgb(0, DockView.Accent.R, DockView.Accent.G, DockView.Accent.B));
        double endX = target.X + target.Width / 2 - FocusBloom.Width / 2;
        double endY = target.Y + target.Height / 2 - FocusBloom.Height / 2;
        Canvas.SetLeft(FocusBloom, endX); Canvas.SetTop(FocusBloom, endY);
        FocusBloom.BeginAnimation(Canvas.LeftProperty, milliseconds == 0 ? null : new DoubleAnimation(start.X + start.Width / 2 - FocusBloom.Width / 2, endX, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        FocusBloom.BeginAnimation(Canvas.TopProperty, milliseconds == 0 ? null : new DoubleAnimation(start.Y + start.Height / 2 - FocusBloom.Height / 2, endY, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        FocusBloom.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(milliseconds)));
    }
    public void FocusDock(int index) => docks[index].FocusButton.Focus();
    public void SetSimulationNavigation(bool enabled)
    {
        SimulationNavigation = enabled;
        navigation.Read(Frame(Buttons.None), false, InputOwner.UiNavigation);
        navigation.Read(Frame(Buttons.None), true, InputOwner.UiNavigation);
        NavigationHint.Text = enabled ? "SIMULATED GAMEPAD · Arrows Navigate · A / Enter Focus · B / Esc Back" : "Tab / Arrows Navigate · Enter Focus · Esc Back · F6 Gamepad";
        if (enabled) { pointerMode.EnterDemo(pointer.X, pointer.Y); FocusDock(State.FocusedPlayer ?? 0); }
    }
    public void EnableNativeNavigation(){if(!SimulationNavigation)SetSimulationNavigation(true);NavigationHint.Text="Controller navigation · A Select · B Back · D-pad Navigate";}
    public void SuspendNavigation() => navigation.Read(Frame(Buttons.None), false, InputOwner.UiNavigation);
    private InputFrame Frame(Buttons buttons) => new(0, true, "simulation:zone-keys", clock.Elapsed.TotalMilliseconds, buttons);
    public async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if(Keyboard.FocusedElement is System.Windows.Controls.Button shell && (ReferenceEquals(shell,SupportButton)||ReferenceEquals(shell,ToolsButton)))
        {
            if(e.Key==Key.Enter || SimulationNavigation&&e.Key==Key.A){if(!e.IsRepeat)shell.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));e.Handled=true;return;}
            if(e.Key==Key.Down){FocusDock(State.FocusedPlayer??0);e.Handled=true;return;}
            if(e.Key is Key.Left or Key.Right){shell.MoveFocus(new(e.Key==Key.Left?FocusNavigationDirection.Left:FocusNavigationDirection.Right));e.Handled=true;return;}
        }
        if (e.Key == Key.F6) { if (!e.IsRepeat) SetSimulationNavigation(!SimulationNavigation); e.Handled = true; return; }
        if (e.Key == Key.F8) { if (!e.IsRepeat) LoadScenario((ZoneScenario)(((int)scenario + 1) % 3)); e.Handled = true; return; }
        if (e.Key == Key.Escape || SimulationNavigation && e.Key == Key.B)
        {
            e.Handled = true;
            if (State.FocusedPlayer.HasValue) await FocusPlayerAsync(null);
            else if (SimulationNavigation) SetSimulationNavigation(false);
            return;
        }
        int selected = Array.FindIndex(docks, d => d.IsKeyboardFocusWithin);
        if(selected>=0 && e.Key==Key.Up && !State.FocusedPlayer.HasValue){ToolsButton.Focus();e.Handled=true;return;}
        if (selected < 0) selected = State.FocusedPlayer ?? 0;
        var direction = e.Key switch { Key.Up => NavigationAction.Up, Key.Down => NavigationAction.Down, Key.Left => NavigationAction.Left, Key.Right => NavigationAction.Right, _ => NavigationAction.None };
        bool activate = e.Key == Key.Enter || SimulationNavigation && e.Key == Key.A;
        if (SimulationNavigation)
        {
            Buttons input = e.Key switch { Key.Up => Buttons.Up, Key.Down => Buttons.Down, Key.Left => Buttons.Left, Key.Right => Buttons.Right, Key.Enter or Key.A => Buttons.A, _ => Buttons.None };
            if (input == Buttons.None) return;
            e.Handled = true;
            var action = navigation.Read(Frame(input), Window.GetWindow(this)?.IsActive == true, InputOwner.UiNavigation);
            if (action == NavigationAction.Activate) await FocusPlayerAsync(selected);
            else if (action != NavigationAction.None) MoveFocusToDock(selected, action);
        }
        else if (direction != NavigationAction.None) { MoveFocusToDock(selected, direction); e.Handled = true; }
        else if (activate && docks[selected].FocusButton.IsKeyboardFocused)
        {
            e.Handled = true;
            if (!e.IsRepeat) await FocusPlayerAsync(selected);
        }
    }
    private void MoveFocusToDock(int selected, NavigationAction action)
    {
        if (State.FocusedPlayer.HasValue)
        {
            // the hero on the left, the other three in a column on the right
            int hero = State.FocusedPlayer.Value;
            var others = Enumerable.Range(0, 4).Where(i => i != hero).ToArray();
            int position = Array.IndexOf(others, selected);
            selected = action switch {
                NavigationAction.Left => hero,
                NavigationAction.Right when selected == hero => others[0],
                NavigationAction.Up when position >= 0 => others[Math.Max(0, position - 1)],
                NavigationAction.Down when position >= 0 => others[Math.Min(2, position + 1)],
                _ => selected };
        }
        else selected = action switch {
            // the two-by-two grid
            NavigationAction.Left when selected % 2 == 1 => selected - 1,
            NavigationAction.Right when selected % 2 == 0 => selected + 1,
            NavigationAction.Up when selected >= 2 => selected - 2,
            NavigationAction.Down when selected < 2 => selected + 2,
            _ => selected };
        FocusDock(selected);
    }
    public void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!SimulationNavigation) return;
        navigation.Read(Frame(Buttons.None), Window.GetWindow(this)?.IsActive == true, InputOwner.UiNavigation);
        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.A) e.Handled = true;
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        pointer = e.GetPosition(this);
        if (SimulationNavigation && pointerMode.Observe(pointer.X, pointer.Y)) SetSimulationNavigation(false);
    }
    /// <summary>The pill switches views (owner direction 10 Oct 2026): in the four-player grid it reads "Selected player"
    /// and brings back the hero; with a hero it reads "All controllers" and opens the grid.</summary>
    private async void BackToOverview(object sender, RoutedEventArgs e) => await FocusPlayerAsync(State.FocusedPlayer.HasValue ? null : lastFocused);
    private int lastFocused;
    private void UpdateOverviewPill()
    {
        bool hero = State.FocusedPlayer.HasValue; if (hero) lastFocused = State.FocusedPlayer!.Value;
        OverviewLabel.Text = hero ? "All controllers" : "Selected player"; OverviewIcon.Text = hero ? "\uE8A9" : "\uE740";
        System.Windows.Automation.AutomationProperties.SetName(OverviewBack, hero ? "Show all four controllers" : "Show the selected player large");
    }
    private void Minimize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) SystemCommands.MinimizeWindow(w); }
    private void Maximize(object sender, RoutedEventArgs e) { if (Window.GetWindow(this) is Window w) w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; } // fullscreen or the fixed window
    private void Close(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
