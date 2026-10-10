using X20Ctl.Simulation;
namespace X20Ctl.Zone;

public record PlayerDock(int Number, string? Model, bool IsMockConnected, string? ModelId = null);
public enum InputOwner { UiNavigation, TesterCapture, ScannerCapture, MacroRecording, RemapCapture }
public enum ZoneScenario { Empty, OneController, MultipleControllers }

// Development-only state. Model selection is never physical identity or write authorization.
public sealed class ZoneState
{
    private readonly PlayerDock[] players;
    public IReadOnlyList<PlayerDock> Players { get; }
    public int SelectedPlayer { get; private set; }
    public int? FocusedPlayer { get; private set; }
    public ZoneState(ZoneScenario scenario = ZoneScenario.Empty)
    {
        players = new[] {
            new PlayerDock(1, scenario == ZoneScenario.Empty ? null : "EasySMX X20", scenario != ZoneScenario.Empty),
            new PlayerDock(2, scenario == ZoneScenario.MultipleControllers ? "EasySMX X15" : null, scenario == ZoneScenario.MultipleControllers),
            new PlayerDock(3, null, false),
            new PlayerDock(4, scenario == ZoneScenario.MultipleControllers ? "EasySMX X05 Pro" : null, scenario == ZoneScenario.MultipleControllers) };
        Players = Array.AsReadOnly(players);
    }
    public void Collapse() { FocusedPlayer = null; Feedback = "Choose a player to bring it into focus."; }
    public string Feedback { get; private set; } = "Choose a player to get started.";
    public void Activate(int index)
    {
        if (index is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedPlayer = index;
        FocusedPlayer = index;
        Feedback = $"Player {index + 1} selected. " + (Players[index].Model == null ? "Choose your controller." : "Assignment retained. Studio follows visual approval.");
    }
    public void Assign(int index, ControllerModel model)
    {
        if (index is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(index));
        players[index] = new(index + 1, model.Name, false, model.Id);
        Activate(index);
    }
    public void Restore(IReadOnlyList<string?> ids, ControllerCatalog catalog)
    {
        if (ids.Count != 4) throw new ArgumentException("Expected four players.", nameof(ids));
        for (int i = 0; i < 4; i++)
        {
            var model = ids[i] == null ? null : catalog.Find(ids[i]!);
            players[i] = new(i + 1, ids[i] == null ? null : model?.Name ?? $"Saved model: {ids[i]}", false, ids[i]);
        }
        Collapse();
    }
}

public record DockBounds(double X, double Y, double Width, double Height)
{
    public bool Overlaps(DockBounds other) => X < other.X + other.Width && X + Width > other.X && Y < other.Y + other.Height && Y + Height > other.Y;
}
public static class ZoneLayout
{
    public static DockBounds[] Calculate(double width, double height, int? focused)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 48 || height <= 48) throw new ArgumentOutOfRangeException(nameof(width));
        if (focused is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(focused));
        const double gap = 24;
        var bounds = new DockBounds[4];
        if (focused == null)
        {
            // four equal controllers in a two-by-two grid
            double w = (width - gap) / 2, h = (height - gap) / 2;
            for (int i = 0; i < 4; i++) bounds[i] = new(i % 2 * (w + gap), i / 2 * (h + gap), w, h);
        }
        else
        {
            // the chosen controller takes the big square on the left; the other three line up smaller on the right
            double heroWidth = (width - gap) * .66, secondaryWidth = width - heroWidth - gap;
            double secondaryHeight = (height - gap * 2) / 3;
            bounds[focused.Value] = new(0, 0, heroWidth, height);
            int row = 0;
            for (int i = 0; i < 4; i++) if (i != focused.Value) bounds[i] = new(heroWidth + gap, row++ * (secondaryHeight + gap), secondaryWidth, secondaryHeight);
        }
        return bounds;
    }
}

public sealed class ZoneNavigation
{
    private readonly Navigation navigation = new();
    private bool awaitingNeutral = true;
    public NavigationAction Read(InputFrame frame, bool foreground, InputOwner owner)
    {
        bool allowed = foreground && owner == InputOwner.UiNavigation && frame.Connected;
        if (!allowed)
        {
            awaitingNeutral = true;
            navigation.Read(frame, false, true);
            return NavigationAction.None;
        }
        if (awaitingNeutral)
        {
            bool neutral = frame.Buttons == Buttons.None && Math.Abs(frame.LeftX) < .4 && Math.Abs(frame.LeftY) < .4;
            navigation.Read(frame, false);
            if (neutral) awaitingNeutral = false;
            return NavigationAction.None;
        }
        return navigation.Read(frame, true);
    }
}
