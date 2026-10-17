using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

public sealed record InstalledGame(string Name, string Launch, string Source);

/// <summary>Installed games, read from the launchers' own local files; nothing goes online. Steam for now.</summary>
internal static class GameShelf
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
                        games.Add(new(name.Groups[1].Value, "steam://rungameid/" + id.Groups[1].Value, "Steam"));
                }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return games.DistinctBy(g => g.Launch).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void Play(InstalledGame game)
    {
        try { Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer" : "xdg-open", game.Launch) { UseShellExecute = false }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

/// <summary>The controller, its state, and the games on this PC.</summary>
internal sealed class DashboardPage : UserControl
{
    readonly StudioState state; bool rear; int pick = -1; List<InstalledGame>? games;
    public DashboardPage(StudioState state) { this.state = state; Build(); }

    void Build()
    {
        games ??= ReviewMode.HideLibrary ? [] : GameShelf.Scan();
        var page = new Grid { Margin = new Thickness(36, 8, 36, 18), RowDefinitions = new("Auto,Auto") };

        var left = new Grid();
        left.Children.Add(new ControllerArt(state.Model.Id, rear) { Margin = new Thickness(8) });
        var flip = new Pill("Flip controller", Pill.Kinds.Ghost, () => { rear = !rear; Build(); }) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(22, 12) };
        left.Children.Add(flip);
        var o = new DeviceOverview(state.Model.Id, 1);
        var tiles = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto") };
        void Tile(string l, string v, int r, int c) { var t = Parts.Tile(l, v); t.Margin = new Thickness(c == 0 ? 0 : 5, r == 0 ? 0 : 8, c == 0 ? 5 : 0, 0); Grid.SetRow(t, r); Grid.SetColumn(t, c); tiles.Children.Add(t); }
        Tile("Connection", "Not detected", 0, 0); Tile("Battery", o.BatteryPercent is int p ? p + "%" : "Not reported", 0, 1); Tile("Firmware", o.Firmware ?? "Not reported", 1, 0); Tile("Changes", state.Changes.ToString(), 1, 1);
        var right = T.Stack(5, T.Label("Offline", 22, null, FontWeight.SemiBold), T.Label("Current setup", 12.5, T.Soft), T.Label("Current working settings", 26, null, FontWeight.SemiBold), T.Label("No game-specific setup linked", 13.5, T.Soft), new Border { Height = 8 }, tiles);

        var top = new Grid { ColumnDefinitions = new("*,1.2*"), Height = 300 };
        top.Children.Add(T.Card(left, 12)); var rc = T.Card(right, 22); rc.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(rc, 1); top.Children.Add(rc);
        page.Children.Add(top);

        var shelf = Shelf(); shelf.Margin = new Thickness(0, 18, 0, 0); Grid.SetRow(shelf, 1); page.Children.Add(shelf);
        Content = new ScrollViewer { Content = page };
    }

    Control Shelf()
    {
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        var title = T.Label("Games & profiles", 21, null, FontWeight.SemiBold); title.VerticalAlignment = VerticalAlignment.Center; head.Children.Add(title);
        var buttons = T.Line(12, new Pill("Rescan", Pill.Kinds.Ghost, () => { games = null; Build(); }) { Padding = new Thickness(22, 11) }, new Pill("Add a game", Pill.Kinds.Primary) { Padding = new Thickness(22, 11), Enabled = false }, new Pill("Add a folder", Pill.Kinds.Primary) { Padding = new Thickness(22, 11), Enabled = false });
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
                    Width = 180, Height = 150, CornerRadius = new CornerRadius(12), Cursor = new Cursor(StandardCursorType.Hand), Background = on ? T.B("#552A62C8") : T.B("#400B1430"), BorderBrush = on ? T.AccentHi : T.B("#12FFFFFF"), BorderThickness = new Thickness(on ? 2 : 1), Padding = new Thickness(14),
                    Child = new Grid { RowDefinitions = new("*,Auto"), Children = { Parts.Center(T.Label(games[i].Name[..1].ToUpperInvariant(), 44, T.Muted, FontWeight.Bold)), Bottom(T.Label(games[i].Name, 14, null, FontWeight.Medium), games[i].Source) } }
                };
                tile.PointerPressed += (_, _) => { pick = index; Build(); };
                row.Children.Add(tile);
            }
            strip = new ScrollViewer { Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        }

        var foot = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 16, 0, 0) };
        string chosen = pick >= 0 && pick < games.Count ? games[pick].Name : "Your library";
        foot.Children.Add(T.Stack(2, T.Label(pick >= 0 ? chosen : "Your library", 20, null, FontWeight.SemiBold), T.Label(games.Count == 0 ? "No games found" : games.Count + (games.Count == 1 ? " game" : " games"), 13, T.Soft)));
        var acts = T.Line(10, new Pill("Load setup", Pill.Kinds.Ghost) { Enabled = false, Padding = new Thickness(22, 11) }, new Pill("Edit", Pill.Kinds.Ghost) { Enabled = false, Padding = new Thickness(22, 11) }, new Pill("▶  Play", Pill.Kinds.Primary, () => { if (pick >= 0) GameShelf.Play(games[pick]); }) { Enabled = pick >= 0, Padding = new Thickness(26, 11) });
        acts.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(acts, 1); foot.Children.Add(acts);
        return T.Card(T.Stack(16, head, strip, foot), 22);
    }

    static Control Bottom(Control c, string source) { var s = T.Stack(2, c, T.Label(source, 11.5, T.Muted)); s.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetRow(s, 1); return s; }
}

/// <summary>Set by the screenshot run so no one's library appears in images.</summary>
internal static class ReviewMode { public static bool HideLibrary; }
