using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

public sealed record InstalledGame(string Name, string Launch, string Source, string? Art = null);

/// <summary>Installed games, read from the launchers' own local files; nothing goes online. Steam for now.</summary>
internal static partial class GameShelf
{
    static readonly string[] Skip = ["Steamworks Common Redistributables", "Steam Linux Runtime", "Proton"];

    public static List<InstalledGame> Scan()
    {
        var games = new List<InstalledGame>();
        try
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new[] { ".local/share/Steam", ".steam/steam", ".var/app/com.valvesoftware.Steam/.local/share/Steam", "Library/Application Support/Steam" }.Select(r => Path.Combine(home, r)).Where(Directory.Exists).ToList();
            var libraries = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in roots)
            {
                string apps = Path.Combine(root, "steamapps"); if (Directory.Exists(apps)) libraries.Add(Path.GetFullPath(apps));
                string vdf = Path.Combine(apps, "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    { string extra = Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), "steamapps"); if (Directory.Exists(extra)) libraries.Add(Path.GetFullPath(extra)); }
            }
            foreach (string lib in libraries)
                foreach (string manifest in Directory.EnumerateFiles(lib, "appmanifest_*.acf"))
                {
                    string text = File.ReadAllText(manifest);
                    var id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\""); var name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"");
                    if (id.Success && name.Success && !Skip.Any(s => name.Groups[1].Value.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                        games.Add(new(name.Groups[1].Value, "steam://rungameid/" + id.Groups[1].Value, "Steam", SteamArt(roots, id.Groups[1].Value)));
                }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        games.AddRange(Heroic()); games.AddRange(Lutris()); games.AddRange(Added());
        return games.DistinctBy(g => g.Launch).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void Play(InstalledGame game)
    {
        try { Start(game); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { }
    }
}

/// <summary>The controller, its state, and the games on this PC.</summary>
internal sealed class DashboardPage : UserControl
{
    readonly StudioState state; readonly Action<string> go; bool rear; int pick = -1; List<InstalledGame>? games;
    InstalledGame? launched; HashSet<string> before = []; string? inGame; string note = ""; double tileWidth = 180;
    readonly Avalonia.Threading.DispatcherTimer watch = new() { Interval = TimeSpan.FromSeconds(3) };
    public DashboardPage(StudioState state, Action<string> go)
    {
        this.state = state; this.go = go;
        watch.Tick += (_, _) => Watch();
        AttachedToVisualTree += (_, _) => watch.Start(); DetachedFromVisualTree += (_, _) => watch.Stop();
        SizeChanged += (_, e) => { double w = Math.Clamp((e.NewSize.Width - 72 - 12 * 5) / 6, 150, 260); if (Math.Abs(w - tileWidth) > 24) { tileWidth = w; Build(); } };
        Build();
    }

    /// <summary>"In game" while something new that looks like a game is running after Play; back to normal when it ends.</summary>
    void Watch()
    {
        if (launched == null && inGame == null) return;
        var now = GameApps.Running(); string? fresh = now.FirstOrDefault(a => !before.Contains(a.Exe))?.Title;
        string? next = fresh != null ? launched?.Name ?? fresh : null;
        if (next == null && launched != null && (DateTime.UtcNow - launchedAt).TotalSeconds < 45) return;
        if (next != inGame) { inGame = next; if (next == null) launched = null; Build(); }
    }
    DateTime launchedAt;

    void Build()
    {
        games ??= ReviewMode.HideLibrary || Phone.Active ? [] : GameShelf.Scan();
        var page = new Grid { Margin = new Thickness(36, 8, 36, 18), RowDefinitions = new("Auto,Auto") };

        var left = new Grid();
        left.Children.Add(new ControllerArt(state.Model.Id, rear) { Margin = new Thickness(8) });
        var flip = new Pill("Flip controller", Pill.Kinds.Ghost, () => { rear = !rear; Build(); }) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(22, 12) };
        left.Children.Add(flip);
        var o = new DeviceOverview(state.Model.Id, 1);
        var tiles = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto") };
        void Tile(string l, string v, int r, int c) { var t = Parts.Tile(l, v); t.Margin = new Thickness(c == 0 ? 0 : 5, r == 0 ? 0 : 8, c == 0 ? 5 : 0, 0); Grid.SetRow(t, r); Grid.SetColumn(t, c); tiles.Children.Add(t); }
        Tile("Connection", "Not detected", 0, 0); Tile("Battery", o.BatteryPercent is int p ? p + "%" : "Not reported", 0, 1); Tile("Firmware", o.Firmware ?? "Not reported", 1, 0); Tile("Changes", state.Changes.ToString(), 1, 1);
        var right = T.Stack(5, T.Label(inGame != null ? "In game · " + inGame : "Offline", 22, null, FontWeight.SemiBold), T.Label("Current setup", 12.5, T.Soft), T.Label("Current working settings", 26, null, FontWeight.SemiBold), T.Label("No game-specific setup linked", 13.5, T.Soft), new Border { Height = 8 }, tiles);

        var top = new Grid { ColumnDefinitions = new("*,1.2*"), Height = 300 };
        top.Children.Add(T.Card(left, 12)); var rc = T.Card(right, 22); rc.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(rc, 1); top.Children.Add(rc);
        page.Children.Add(top);

        // the shelf scans and launches PC games; a phone has none
        if (!Phone.Active) { var shelf = Shelf(); shelf.Margin = new Thickness(0, 18, 0, 0); Grid.SetRow(shelf, 1); page.Children.Add(shelf); }
        Content = new ScrollViewer { Content = page };
    }

    Control Shelf()
    {
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        var title = T.Label("Games & profiles", 21, null, FontWeight.SemiBold); title.VerticalAlignment = VerticalAlignment.Center; head.Children.Add(title);
        var buttons = T.Line(12, new Pill("Rescan", Pill.Kinds.Ghost, () => { games = null; Build(); }) { Padding = new Thickness(22, 11) }, new Pill("Add a game", Pill.Kinds.Primary, AddGame) { Padding = new Thickness(22, 11) }, new Pill("Add a folder", Pill.Kinds.Primary, AddFolder) { Padding = new Thickness(22, 11) });
        Grid.SetColumn(buttons, 1); head.Children.Add(buttons);

        Control strip;
        if (games!.Count == 0)
            strip = new Border { Width = 340, Height = 150, HorizontalAlignment = HorizontalAlignment.Left, Background = T.B("#400B1430"), BorderBrush = T.B("#12FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = Parts.Center(T.Label("ⓘ  No installed games found. Add a game or folder.", 13.5, T.Soft)) };
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            for (int i = 0; i < games.Count; i++)
            {
                int index = i; bool on = pick == i;
                var tile = new Border
                {
                    Width = tileWidth, Height = tileWidth * .83, CornerRadius = new CornerRadius(12), Cursor = new Cursor(StandardCursorType.Hand), Background = on ? T.B("#552A62C8") : T.B("#400B1430"), BorderBrush = on ? T.AccentHi : T.B("#12FFFFFF"), BorderThickness = new Thickness(on ? 2 : 1), Padding = new Thickness(14),
                    Child = TileFace(games[i])
                };
                tile.OnTap(() => { pick = index; Build(); });
                row.Children.Add(tile);
            }
            strip = new ScrollViewer { Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        }

        var foot = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 16, 0, 0) };
        string chosen = pick >= 0 && pick < games.Count ? games[pick].Name : "Your library";
        foot.Children.Add(T.Stack(2, T.Label(pick >= 0 ? chosen : "Your library", 20, null, FontWeight.SemiBold), T.Label(note.Length > 0 ? note : games.Count == 0 ? "No games found" : games.Count + (games.Count == 1 ? " game" : " games"), 13, T.Soft)));
        var acts = T.Line(10, new Pill("Load setup", Pill.Kinds.Ghost, () => go("Setups")) { Padding = new Thickness(22, 11) }, new Pill(pick >= 0 && games[pick].Source == "Added" ? "Remove" : "Edit", Pill.Kinds.Ghost, EditPick) { Padding = new Thickness(22, 11) }, new Pill("▶  Play", Pill.Kinds.Primary, () => { if (pick >= 0) Launch(games[pick]); }) { Enabled = pick >= 0, Padding = new Thickness(26, 11) });
        acts.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(acts, 1); foot.Children.Add(acts);
        return T.Card(T.Stack(16, head, strip, foot), 22);
    }

    static Control TileFace(InstalledGame g)
    {
        var grid = new Grid { RowDefinitions = new("*,Auto") };
        Avalonia.Media.Imaging.Bitmap? art = null;
        if (g.Art != null) try { art = new Avalonia.Media.Imaging.Bitmap(g.Art); } catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { }
        if (art != null) grid.Children.Add(new Image { Source = art, Stretch = Stretch.UniformToFill, Opacity = .55, [Grid.RowSpanProperty] = 2 });
        else grid.Children.Add(Parts.Center(T.Label(g.Name[..1].ToUpperInvariant(), 44, T.Muted, FontWeight.Bold)));
        grid.Children.Add(Bottom(T.Label(g.Name, 14, null, FontWeight.Medium), g.Source));
        return new Border { ClipToBounds = true, CornerRadius = new CornerRadius(12), Child = grid };
    }

    void Launch(InstalledGame g)
    {
        before = [.. GameApps.Running().Select(a => a.Exe)]; launched = g; launchedAt = DateTime.UtcNow; GameShelf.Play(g);
    }

    async void AddGame()
    {
        var top = TopLevel.GetTopLevel(this); if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Pick a game's program", AllowMultiple = false });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path) return;
        note = GameShelf.AddProgram(path) > 0 ? "Added." : "That isn't a program, or it's already there."; games = null; Build();
    }

    async void AddFolder()
    {
        var top = TopLevel.GetTopLevel(this); if (top == null) return;
        var dirs = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Pick a folder of games", AllowMultiple = false });
        if (dirs.Count == 0 || dirs[0].TryGetLocalPath() is not { } path) return;
        int n = GameShelf.AddFolder(path); note = n > 0 ? $"Added {n} {(n == 1 ? "game" : "games")}." : "No games found in that folder."; games = null; Build();
    }

    /// <summary>A game added by hand is removed; any other opens PC mode, where its per-game settings live.</summary>
    void EditPick()
    {
        if (pick < 0 || pick >= games!.Count) { go("PC mode"); return; }
        if (games[pick].Source == "Added") { GameShelf.Forget(games[pick]); pick = -1; games = null; Build(); }
        else go("PC mode");
    }

    static Control Bottom(Control c, string source) { var s = T.Stack(2, c, T.Label(source, 11.5, T.Muted)); s.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetRow(s, 1); return s; }
}

/// <summary>Set by the screenshot run so no one's library appears in images.</summary>
internal static class ReviewMode
{
    public static bool HideLibrary; public static IReadOnlyList<EvDevice>? Pads;
    /// <summary>The headless review (X20Ctl.Linux.Review): no launch intro or update check, and the intro stays silent.</summary>
    public static bool Headless;
}
