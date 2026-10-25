using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>The interface sounds (Brand/ui/*.wav, made by tools/build_ui_sounds.py): a soft cue for tab switches, clicks, buttons,
/// hovering a game, a controller or a tool, and choosing a controller. Every ButtonBase plays "Button" unless it names its own
/// cue, so new buttons are covered without wiring. Quiet by design, off with the intro's Sound switch, never in review runs, never
/// while the app is in the background, and a hover cannot repeat faster than the ear can take it.</summary>
internal static class UiSound
{
    public enum Cue { Button, Tab, Click, HoverGame, HoverController, HoverTool, Pick, None }
    /// <summary>Asked before every sound; ProductHost points it at the player's Sound preference.</summary>
    public static Func<bool> Allowed { get; set; } = () => true;
    public static readonly DependencyProperty CueProperty = DependencyProperty.RegisterAttached("Cue", typeof(Cue), typeof(UiSound), new PropertyMetadata(Cue.Button));
    public static void SetCue(DependencyObject element, Cue cue) => element.SetValue(CueProperty, cue);
    private static readonly Dictionary<Cue, string> Files = new()
    {
        [Cue.Button] = "button", [Cue.Tab] = "tab", [Cue.Click] = "click", [Cue.HoverGame] = "hover-game",
        [Cue.HoverController] = "hover-controller", [Cue.HoverTool] = "hover-tool", [Cue.Pick] = "pick",
    };
    private static readonly Dictionary<Cue, MediaPlayer> players = [];
    private static readonly Dictionary<Cue, long> lastPlayed = [];
    private static long lastHover;
    private static bool registered;

    public static void Register()
    {
        if (registered) return; registered = true;
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler((s, _) => Play(((DependencyObject)s).GetValue(CueProperty) is Cue cue ? cue : Cue.Button)), true);
    }

    /// <summary>Hovering something: one quiet cue, at most one every 70 ms overall and one per kind every 250 ms.</summary>
    public static void Hover(FrameworkElement element, Cue cue) => element.MouseEnter += (_, _) => Play(cue);

    public static void Play(Cue cue)
    {
        if (cue == Cue.None || !Files.TryGetValue(cue, out string? name)) return;
        if (!IntroView.SoundEnabled || ReviewSandbox.Active || !Allowed() || !AppIsActive()) return;
        long now = Environment.TickCount64; bool hover = cue is Cue.HoverGame or Cue.HoverController or Cue.HoverTool;
        if (hover && (now - lastHover < 70 || lastPlayed.TryGetValue(cue, out long before) && now - before < 250)) return;
        try
        {
            if (!players.TryGetValue(cue, out var player))
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Brand", "ui", name + ".wav"); if (!File.Exists(path)) return;
                player = new MediaPlayer { Volume = 1 }; player.Open(new Uri(path)); players[cue] = player;
            }
            player.Position = TimeSpan.Zero; player.Play();
            lastPlayed[cue] = now; if (hover) lastHover = now;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UriFormatException or System.Runtime.InteropServices.COMException) { }
    }
    private static bool AppIsActive() { if (Application.Current is not { } app) return false; foreach (Window w in app.Windows) if (w.IsActive) return true; return false; }
}
