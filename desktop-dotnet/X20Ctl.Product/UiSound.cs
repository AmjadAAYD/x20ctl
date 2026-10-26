using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>The interface sounds: Brand/ui/&lt;theme&gt;/&lt;role&gt;.wav, made by tools/ui_sound_themes/build.py. Every theme has its own family in
/// the same dark, dense, soft voice. A cue picks a pool of roles (several variants, never the same one twice in a row, so repeating an
/// action does not wear on the ear): tab switches play navigation sounds, buttons and clicks play the button pool, choosing a controller
/// plays an enter sound, back buttons play back, typing plays the key pool. Hover is silent. Every ButtonBase plays "Button" unless it
/// names its own cue. Quiet by design, off with the intro's Sound switch, never in review runs, never while the app is in the background.</summary>
internal static class UiSound
{
    public enum Cue { Button, Tab, Click, Pick, Back, None }
    /// <summary>Asked before every sound; ProductHost points it at the player's Sound preference.</summary>
    public static Func<bool> Allowed { get; set; } = () => true;
    /// <summary>The active theme's sound family; a theme without a folder falls back to "default". The theme switch sets this.</summary>
    public static string Theme { get => theme; set { theme = string.IsNullOrWhiteSpace(value) ? "default" : value; } }
    public static readonly DependencyProperty CueProperty = DependencyProperty.RegisterAttached("Cue", typeof(Cue), typeof(UiSound), new PropertyMetadata(Cue.Button));
    public static void SetCue(DependencyObject element, Cue cue) => element.SetValue(CueProperty, cue);

    private static readonly Dictionary<Cue, string[]> Pools = new()
    {
        [Cue.Button] = ["button-1", "button-2", "button-3", "button-4"],
        [Cue.Click] = ["button-1", "button-2", "button-3", "button-4"],
        [Cue.Tab] = ["nav-next-a", "nav-next-b", "nav-next-c"],
        [Cue.Pick] = ["enter-1", "enter-2"],
        [Cue.Back] = ["back"],
    };
    private static readonly string[] KeyPool = ["key-1", "key-2", "key-3", "key-4"];
    private static readonly Dictionary<string, MediaPlayer> players = [];
    private static readonly Dictionary<string, int> lastIndex = [];
    private static readonly Random random = new();
    private static string theme = "default";
    private static long lastKey;
    private static bool registered;

    public static void Register()
    {
        if (registered) return; registered = true;
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler((s, _) => Play(((DependencyObject)s).GetValue(CueProperty) is Cue cue ? cue : Cue.Button)), true);
        EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.PreviewTextInputEvent, new TextCompositionEventHandler((_, _) => PlayKey(KeyPool)), true);
        EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, e) => { if (e.Key is Key.Back or Key.Delete) PlayKey(["key-delete"]); }), true);
    }

    public static void Play(Cue cue)
    {
        if (cue == Cue.None || !Pools.TryGetValue(cue, out var pool)) return;
        PlayRole(Pick(cue.ToString(), pool));
    }

    // Typing is limited to one key sound every 45 ms, so a held key or fast typing stays calm.
    private static void PlayKey(string[] pool)
    {
        long now = Environment.TickCount64; if (now - lastKey < 45) return; lastKey = now;
        PlayRole(Pick(pool[0] == "key-delete" ? "key-delete" : "keys", pool));
    }

    private static string Pick(string group, string[] pool)
    {
        if (pool.Length == 1) return pool[0];
        int index; do index = random.Next(pool.Length); while (lastIndex.TryGetValue(group, out int before) && before == index);
        lastIndex[group] = index; return pool[index];
    }

    private static void PlayRole(string role)
    {
        if (!IntroView.SoundEnabled || ReviewSandbox.Active || !Allowed() || !AppIsActive()) return;
        try
        {
            string key = theme + "/" + role;
            if (!players.TryGetValue(key, out var player))
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Brand", "ui", theme, role + ".wav");
                if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "Brand", "ui", "default", role + ".wav");
                if (!File.Exists(path)) return;
                player = new MediaPlayer { Volume = 1 }; player.Open(new Uri(path)); players[key] = player;
            }
            player.Position = TimeSpan.Zero; player.Play();
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UriFormatException or System.Runtime.InteropServices.COMException) { }
    }
    private static bool AppIsActive() { if (Application.Current is not { } app) return false; foreach (Window w in app.Windows) if (w.IsActive) return true; return false; }
}
