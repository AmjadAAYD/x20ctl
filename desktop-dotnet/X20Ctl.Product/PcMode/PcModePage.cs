using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Ellipse = System.Windows.Shapes.Ellipse;
using Microsoft.Win32;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>Finds and runs the two small drivers PC mode needs: the virtual controller and the pad hider.</summary>
public static class PcModeSetup
{
    public const string BusPage = "https://github.com/nefarius/ViGEmBus/releases";
    public const string HiderPage = "https://github.com/nefarius/HidHide/releases";
    public static bool BusInstalled { get { using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus"); return key != null && key.GetValue("DeleteFlag") == null; } }
    public static bool Ready => BusInstalled && HidHide.Installed;
    static string Drivers => System.IO.Path.Combine(AppContext.BaseDirectory, "drivers");

    /// <summary>The exact official installers (Nefarius, signed), pinned so a swapped file is never run.</summary>
    static readonly Dictionary<string, string> Pinned = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ViGEmBus_1.22.0_x64_x86_arm64.exe"] = "89220a7865076b342892f98865f3499fb7c4cfd673159e89d352c360fd014c6a",
        ["HidHide_1.5.230_x64.exe"] = "f4bbbcb82e6258641b887c74bc81c4c5f66e4aa811808dfc304347687b7605f6",
    };
    static string? Find(string prefix)
    {
        if (!Directory.Exists(Drivers)) return null;
        foreach (var file in Directory.GetFiles(Drivers, prefix + "*.exe"))
        {
            if (!Pinned.TryGetValue(System.IO.Path.GetFileName(file), out var expected)) continue;
            using var stream = File.OpenRead(file);
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase)) return file;
        }
        return null;
    }

    /// <summary>Installs the drivers by itself when PC mode is switched on (owner request, 17 Oct 2026), so there is no manual
    /// step. Returns null when everything is in place afterwards, else why not, in a sentence. Windows asks for permission
    /// once; a refusal or a failure is reported and never retried in a loop.</summary>
    public static async Task<string?> Ensure()
    {
        if (Ready) return null;
        try { string said = await Run(automatic: true); return Ready ? null : said; }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException) { return "Couldn't set up PC mode: " + error.Message; }
    }

    /// <summary>Runs whichever installers are missing and are bundled beside the app. Returns what happened, in a sentence.</summary>
    static readonly SemaphoreSlim installLock = new(1, 1);
    public static async Task<string> Run(bool automatic = false, bool consented = false)
    {
        if (ReviewSandbox.Active) return "Driver setup is disabled in fixtures.";
        await installLock.WaitAsync();
        try { return await Install(automatic, consented); }
        finally { installLock.Release(); }
    }

    /// <summary>Says what is about to be installed and why, and waits for a yes. The installers run quietly, so without this
    /// the only sign would be Windows' permission box and a window that flashes by.</summary>
    static bool Consent(List<string> names)
    {
        string list = string.Join("\n", names.Select(n => n == "virtual controller driver"
            ? "•  ViGEmBus: the virtual Xbox controller that games see"
            : "•  HidHide: hides the physical pad so games don't see it twice"));
        string text = "PC mode needs " + (names.Count == 1 ? "a small driver" : "two small drivers") + ":\n\n" + list
            + "\n\nThey are the signed installers from Nefarius Software Solutions, packaged with X20CTL. Windows will ask for permission and a setup window may flash by. You can remove them any time in Windows Apps.\n\nInstall now?";
        bool Ask() => MessageBox.Show(Application.Current.MainWindow, text, "X20CTL · PC mode drivers", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        return Application.Current.Dispatcher.CheckAccess() ? Ask() : Application.Current.Dispatcher.Invoke(Ask);
    }

    static async Task<string> Install(bool automatic, bool consented)
    {
        var missing = new List<(string Name, string? File, string Page)>();
        if (!BusInstalled) missing.Add(("virtual controller driver", Find("ViGEmBus"), BusPage));
        if (!HidHide.Installed) missing.Add(("controller hider", Find("HidHide"), HiderPage));
        if (missing.Count == 0) return "Everything PC mode needs is already installed.";
        var notBundled = missing.Where(m => m.File == null).ToList();
        if (notBundled.Count > 0)
        {
            if (automatic) return $"The packaged {notBundled[0].Name} is missing or failed verification. Use Set up PC mode.";
            ExternalLinks.Open(notBundled[0].Page);
            return $"This copy of X20CTL doesn't carry the {notBundled[0].Name}. The official page opened: install it, then come back here.";
        }
        if (!consented && !Consent(missing.Select(m => m.Name).ToList())) return "PC mode drivers were not installed. PC mode stays off until you allow them.";
        foreach (var (name, file, _) in missing)
        {
            // Quiet installer UI still requires Windows UAC consent.
            using var process = Process.Start(new ProcessStartInfo(file!, "/quiet /norestart") { UseShellExecute = true, Verb = "runas" });
            if (process == null) return $"Couldn't start the {name} installer.";
            await process.WaitForExitAsync();
            if (process.ExitCode is not (0 or 3010)) return $"The {name} installer stopped with code {process.ExitCode}. Nothing was changed on your controller.";
        }
        return Ready ? "Installed. PC mode starts by itself once you change a setting." : "Installed. Windows may ask for a restart before PC mode can start.";
    }
}

/// <summary>
/// PC mode: what it is, the switch, its status, which settings (every game's or a game's own), turbo and sharing. It is
/// a tab on every controller's Studio, and also in Tools. The settings themselves are edited where they always are:
/// with PC mode on, a controller's Buttons, Curves and Macros tabs apply on this PC, for any model. Everything saves as
/// it changes; nothing is written to the controller.
/// </summary>
public sealed class PcModePage : Border
{
    static readonly (string Id, string Label)[] Buttons =
    [
        ("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"), ("LB", "Left bumper"), ("RB", "Right bumper"), ("LT", "Left trigger"), ("RT", "Right trigger"),
        ("L3", "Left stick click"), ("R3", "Right stick click"), ("DPAD_UP", "D-pad up"), ("DPAD_DOWN", "D-pad down"), ("DPAD_LEFT", "D-pad left"), ("DPAD_RIGHT", "D-pad right"),
        ("SELECT", "View / Back"), ("START", "Menu / Start"), ("HOME", "Home / Guide"),
    ];
    static readonly int[] TurboRates = [0, 5, 8, 10, 15, 20, 30];
    /// <summary>The profile being edited, here, in the controller tabs and in the Advanced stick tools: "default"
    /// (every game) or a game's own. Shared by all of them, so they always edit the same thing.</summary>
    public static string ProfileKey { get; private set; } = PcModeStore.DefaultKey;
    /// <summary>Whose settings are edited (0 to 3): the controller whose Studio is open, or the one picked in Tools.
    /// PC mode gives player N's settings to the Nth controller Windows sees.</summary>
    public static int Player { get; private set; }
    /// <summary>The store key being edited: this player's every-game or game settings.</summary>
    public static string StoreKey => PcModeStore.For(Player, ProfileKey);
    /// <summary>A Studio opened for another player: edit that player's settings (a game they have none for falls back to every game).</summary>
    public static void UsePlayer(int player, PcModeStore store)
    {
        Player = Math.Clamp(player, 0, 3);
        if (ProfileKey != PcModeStore.DefaultKey && !store.Has(StoreKey)) ProfileKey = PcModeStore.DefaultKey;
    }
    /// <summary>The settings being edited were replaced (another game picked, an import, a reset): the controller tabs reload.</summary>
    public static event Action? ProfileChanged;
    /// <summary>Something outside this page (the Talk agent) replaced the settings: the controller tabs reload.</summary>
    public static void Replaced() => ProfileChanged?.Invoke();
    /// <summary>"I understand" was pressed: the controller tabs may now save what they hold.</summary>
    public static event Action? NoticeAccepted;
    /// <summary>The switch changed. The Studio reopens, so its tabs unlock (or lock) for this controller.</summary>
    public event Action<bool>? Switched;
    static event Action? SwitchedElsewhere;
    /// <summary>Agent XCTL switched PC mode: the same as the switch here, without the notice (the owner asked for it in
    /// words). Returns why it couldn't, or null.</summary>
    public static async Task<string?> SwitchFromAgent(PcModeLink link, bool on)
    {
        if (on && await PcModeSetup.Ensure() is { } why) return why;
        link.Service.Enabled = on;
        try { link.Preferences?.SetPcMode(on); } catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException) { return "Switched, but couldn't remember it: " + error.Message; }
        SwitchedElsewhere?.Invoke();
        return null;
    }

    readonly PcModeService service; readonly PcModeStore store; readonly ProductPreferences? preferences;
    readonly Dictionary<string, ComboBox> turbo = new();
    PcModeProfile? incoming; // an imported profile, used until it is saved
    bool clearAll;
    readonly ComboBox profiles = new() { MinHeight = 40, Width = 320, FontSize = 15, DisplayMemberPath = "Label", SelectedValuePath = "Id" };
    readonly ComboBox owner = new() { MinHeight = 40, Width = 220, FontSize = 15, DisplayMemberPath = "Label", SelectedValuePath = "Id" };
    readonly ComboBox apps = new() { MinHeight = 40, Width = 420, FontSize = 14.5, DisplayMemberPath = "Label", SelectedValuePath = "Id" };
    readonly StackPanel picker; readonly Button removeGame;
    readonly TextBlock statusLabel, statusDetail, saved, setupNote;
    readonly TextBlock pipeline=Wrap("",14,"DS.TextSoft",12);
    readonly Ellipse dot = new() { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
    readonly CheckBox enable = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    readonly Border notice; readonly Button setup;
    bool loading, pendingSave, syncing;

    public Button Back { get; }
    public CheckBox Switch => enable;

    /// <param name="editButtons">In a controller's Studio (whose player is <see cref="Player"/>): opens its Buttons tab.
    /// Null in Tools, where a list picks whose settings are shown.</param>
    public PcModePage(PcModeService service, PcModeStore store, ProductPreferences? preferences, Button back, Action? editButtons = null)
    {
        this.service = service; this.store = store; this.preferences = preferences; Back = back;
        CornerRadius = new(22); BorderThickness = new(1); Padding = new(28, 22, 28, 22); HorizontalAlignment = HorizontalAlignment.Stretch; VerticalAlignment = VerticalAlignment.Top;
        Background = new LinearGradientBrush(Color.FromArgb(215, 14, 24, 52), Color.FromArgb(200, 8, 14, 34), 90);
        BorderBrush = new LinearGradientBrush(Color.FromArgb(110, 120, 170, 255), Color.FromArgb(30, 120, 170, 255), 90);
        var stack = new StackPanel(); Child = stack;

        stack.Children.Add(Kit.Text("PC mode", 36, "DS.Text", FontWeights.Bold));
        stack.Children.Add(Wrap("Route gameplay input through your saved PC profile to a virtual Xbox controller. Nothing is written to hardware.", 16.5, "DS.TextSoft", 8));

        // status
        statusLabel = Kit.Text("", 20, "DS.Text", FontWeights.SemiBold); statusDetail = Wrap("", 15, "DS.TextSoft", 4);
        var statusCard = new Border { CornerRadius = new(16), Padding = new(22, 16, 22, 16), Margin = new(0, 24, 0, 0), BorderThickness = new(1), Background = new SolidColorBrush(Color.FromArgb(120, 4, 10, 26)), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 120, 170, 255)),
            Child = new StackPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { dot, statusLabel.Also(l => l.Margin = new(12, 0, 0, 0)) } }, statusDetail } } };
        stack.Children.Add(statusCard);stack.Children.Add(pipeline);

        // switch and setup
        setup = Pill("Set up PC mode"); setup.Margin = new(16, 0, 0, 0);
        setup.Click += async (_, _) => await RunSetup();
        var enableRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 20, 0, 0), Children = { enable, Kit.Text("PC mode", 17, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new(14, 0, 0, 0)), setup } };
        stack.Children.Add(enableRow);
        setupNote = Wrap("", 14, "DS.TextMuted", 8); stack.Children.Add(setupNote);
        // shows the saved choice; whether the service runs is the host's call (review and fixture windows never start it)
        enable.IsChecked = service.State==PcModeState.Active && !service.Suspended;
        enable.Checked += (_, _) => { if (!syncing) SetEnabled(true); }; enable.Unchecked += (_, _) => { if (!syncing) SetEnabled(false); };

        // where the settings are edited
        var edit = new StackPanel { Margin = new(0, 22, 0, 0), Children = { Kit.Text("Where you change the settings", 17, "DS.Text", FontWeights.SemiBold),
            Wrap(editButtons != null
                ? "Right here, in this controller's tabs. While PC mode is on, Buttons, Curves (with Advanced) and Macros apply on this PC, for any controller. Turbo is below."
                : "In each controller's tabs: open a controller from the Controller Zone. While PC mode is on, its Buttons, Curves (with Advanced) and Macros tabs apply on this PC, for any controller. Turbo is below.", 15, "DS.TextSoft", 4) } };
        if (editButtons != null) { var open = Pill("Edit buttons"); open.Margin = new(0, 12, 0, 0); open.HorizontalAlignment = HorizontalAlignment.Left; open.Click += (_, _) => editButtons(); edit.Children.Add(open); }
        stack.Children.Add(edit);

        var routingHelp=new StackPanel();
        routingHelp.Children.Add(Block("Input routing", "Physical XInput source → Buttons / Curves / Macros → Virtual Xbox output → Game. Device hiding requires the driver setup and a successful capture; a saved preference alone does not prove a route is active."));
        routingHelp.Children.Add(Block("Reported controls", "Only inputs exposed through XInput can be mapped here. Independent rear buttons and motion data are not available through this reader."));
        routingHelp.Children.Add(Block("Bypass shortcut", "The current Xbox input route uses View + Menu held for one second to bypass processing. Labels describe the XInput interface; they may differ on your physical controller."));
        stack.Children.Add(new Expander{Header="Routing details",Content=routingHelp,Margin=new(0,12,0,0)});

        // the one-time notice
        notice = new Border { CornerRadius = new(16), Padding = new(22, 18, 22, 18), Margin = new(0, 24, 0, 0), BorderThickness = new(1), Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(70, 120, 80, 10)), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 245, 200, 80)) };
        var accept = Pill("I understand"); accept.Margin = new(0, 14, 0, 0); accept.HorizontalAlignment = HorizontalAlignment.Left;
        accept.Click += (_, _) => AcceptNotice();
        notice.Child = new StackPanel { Children = {
            Kit.Text("Before PC mode starts", 18, "DS.Text", FontWeights.SemiBold),
            Wrap(NoticeLines[0], 15, "DS.TextSoft", 10), Wrap(NoticeLines[1], 15, "DS.TextSoft", 6), Wrap(NoticeLines[2], 15, "DS.TextSoft", 6), accept } };
        stack.Children.Add(notice);
        // shown up front until it's been read, so edits in the tabs can start applying
        if (preferences is { PcModeNoticeSeen: false } && (preferences?.PcMode ?? true)) notice.Visibility = Visibility.Visible;

        // whose settings, then which: every game, or a game's own
        stack.Children.Add(Section("SETTINGS FOR"));
        stack.Children.Add(Wrap(editButtons != null
            ? $"Player {Player + 1} profile · Physical XInput index {Player + 1}. This is an input index, not verified device identity; it can change after reconnecting. Game-specific profiles activate when that game is in front."
            : "Profiles are assigned by physical XInput index (1–4), not persistent hardware identity. Select a player to edit its default or game-specific profile.", 14.5, "DS.TextMuted", 0));
        if (editButtons == null)
        {
            owner.ItemsSource = Enumerable.Range(0, 4).Select(i => new Choice(i.ToString(), $"Player {i + 1}")).ToList(); owner.SelectedValue = Player.ToString();
            owner.SelectionChanged += (_, _) => { if (!loading && owner.SelectedValue is string id) { UsePlayer(int.Parse(id), store); ListProfiles(); loading = true; Fill(store.Get(StoreKey)); loading = false; saved!.Text = $"Showing Player {Player + 1}'s settings."; ProfileChanged?.Invoke(); } };
            stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), Children = { Kit.Text("Controller", 15, "DS.Text").Also(t => { t.Width = 110; t.VerticalAlignment = VerticalAlignment.Center; }), owner } });
        }
        var addGame = Pill("Give a game its own settings"); addGame.Margin = new(12, 0, 0, 0); addGame.Click += (_, _) => ShowPicker();
        removeGame = Pill("Remove this game"); removeGame.Margin = new(12, 0, 0, 0); removeGame.Click += (_, _) => RemoveGame();
        profiles.SelectionChanged += (_, _) => { if (!loading && profiles.SelectedValue is string key) SwitchTo(key); };
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), Children = { profiles, addGame, removeGame } });
        var add = Pill("Add"); add.Margin = new(12, 0, 0, 0); add.Click += (_, _) => AddGame();
        var cancel = Pill("Cancel"); cancel.Margin = new(8, 0, 0, 0); cancel.Click += (_, _) => picker!.Visibility = Visibility.Collapsed;
        picker = new StackPanel { Margin = new(0, 12, 0, 0), Visibility = Visibility.Collapsed, Children = {
            Wrap("Start the game first, then come back (Home on the controller brings X20CTL forward). It's listed first.", 14, "DS.TextSoft", 0),
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0), Children = { apps, add, cancel } } } };
        stack.Children.Add(picker);

        // turbo: the one setting no controller tab has


        var grid = new UniformGrid { Columns = 3, Margin = new(0, 12, 0, 0) };
        foreach (var (id, label) in Buttons)
        {
            var rate = new ComboBox { MinHeight = 38, Width = 132, FontSize = 14.5, ItemsSource = TurboRates.Select(r => r == 0 ? "No turbo" : r + " / s").ToList(), SelectedIndex = 0 };
            rate.SelectionChanged += (_, _) => Persist(); turbo[id] = rate;
            grid.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 20, 10), Children = { Kit.Text(label, 15, "DS.Text").Also(t => t.Width = 150), rate } });
        }
        stack.Children.Add(new Expander{Header="Turbo bindings",Content=grid,Margin=new(0,18,0,0)});

        // share


        var share = new WrapPanel { Margin = new(0, 12, 0, 0) };
        foreach (var (label, act) in new (string, Action)[] { ("Save to a file", ExportFile), ("Open a file", ImportFile), ("Copy as a code", CopyCode), ("Paste a code", PasteCode) })
        { var b = Pill(label); b.Margin = new(0, 0, 12, 8); b.Click += (_, _) => act(); share.Children.Add(b); }
        stack.Children.Add(new Expander{Header="Import / export PC profile",Content=share,Margin=new(0,12,0,0)});

        var reset = Pill($"Reset Player {Player+1} profile"); reset.Margin = new(0, 24, 12, 0);
        reset.ToolTip = "Clears every PC mode setting for what's picked under Settings for: buttons, curves, stick tools, macros and turbo";
        var confirm=new StackPanel{Visibility=Visibility.Collapsed,Margin=new(0,12,0,0)};
        confirm.Children.Add(Wrap("Reset the selected player's current PC profile? This clears its buttons, curves, stick tools, macros and turbo. Other player profiles and controller hardware are unchanged.",14,"DS.TextSoft",0));
        var yes=Pill("Reset selected profile");yes.Click+=(_,_)=>{loading=true;Fill(new());loading=false;clearAll=true;Persist();clearAll=false;confirm.Visibility=Visibility.Collapsed;};confirm.Children.Add(yes);
        var no=Pill("Cancel");no.Click+=(_,_)=>confirm.Visibility=Visibility.Collapsed;confirm.Children.Add(no);stack.Children.Add(confirm);
        reset.Click += (_, _) => confirm.Visibility=Visibility.Visible;
        back.Margin = new(0, 24, 12, 0);if(editButtons!=null)back.Visibility=Visibility.Collapsed;
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { back, reset } });
        saved = Wrap("", 14, "DS.TextMuted", 12); stack.Children.Add(saved);

        loading = true; ListProfiles(); Fill(store.Get(StoreKey)); loading = false;
        // the service outlives this page: listen only while shown
        Action changed = () => Dispatcher.BeginInvoke(Refresh);
        Action elsewhere = () => { syncing = true; enable.IsChecked = service.State==PcModeState.Active && !service.Suspended; syncing = false; Refresh(); };
        Loaded += (_, _) => { service.Changed -= changed; service.Changed += changed; SwitchedElsewhere -= elsewhere; SwitchedElsewhere += elsewhere; Refresh(); };
        Unloaded += (_, _) => { service.Changed -= changed; SwitchedElsewhere -= elsewhere; };
        Refresh();
    }

    /// <summary>The one-time notice, also shown by the Curves tab's Advanced stick tools before they first start PC mode.</summary>
    internal static readonly string[] NoticeLines =
    [
        "1. Your settings only work while X20CTL is open. Close it, or if it crashes, and your controller goes back to normal.",
        "2. Use turbo and macros with care in online games. Some games and anti-cheat systems treat them as cheating and may ban your account. You've been warned: you are responsible for the rules of the games you play, and X20CTL can't be held accountable for a ban.",
        "3. The first time you switch it on, it installs two small drivers by itself, so Windows asks for permission once.",
    ];
    sealed record Choice(string Id, string Label) { public override string ToString() => Label; }
    static string Ordinal(int player) => player switch { 0 => "first", 1 => "second", 2 => "third", _ => "fourth" };

    /// <summary>Whether edits may start PC mode yet: the notice has to be read first.</summary>
    public static bool MaySave(ProductPreferences? preferences) => preferences is not { PcModeNoticeSeen: false };

    async void SetEnabled(bool on)
    {
        if (on && !PcModeSetup.Ready)
        {
            // the two drivers go in by themselves; Windows asks for permission once
            setup.IsEnabled = false; setupNote.Text = "Setting up PC mode. Windows may ask for permission…";
            string? why = await PcModeSetup.Ensure();
            setup.IsEnabled = true;
            if (why != null) { setupNote.Text = why; Refresh(); return; }
            setupNote.Text = "";
        }
        service.Enabled = on;
        try { preferences?.SetPcMode(on); } catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException) { saved.Text = "Couldn't remember this switch: " + error.Message; }
        Refresh(); Switched?.Invoke(on);
    }

    void Refresh()
    {
        var (label, color) = service.State switch
        {
            PcModeState.Active => ("Active", Color.FromRgb(52, 211, 153)),
            PcModeState.Ready => ("Ready", Color.FromRgb(125, 182, 255)),
            PcModeState.NeedsAttention => ("Needs attention", Color.FromRgb(245, 200, 80)),
            _ => ("Off", Color.FromRgb(140, 150, 175)),
        };
        if(!PcModeSetup.Ready && service.State!=PcModeState.Active)label="Not set up";
        syncing=true;enable.IsChecked=service.State==PcModeState.Active && !service.Suspended;syncing=false;
        var route=service.Routes.FirstOrDefault(r=>r.Player==Player);
        pipeline.Text=$"Physical XInput {Player+1} → PC profile → Virtual Xbox → Game\n"+(route==null?"No active route for this player":$"Measured route: XInput {route.PhysicalSlot+1} → Xbox output {(route.VirtualSlot<0?"pending":(route.VirtualSlot+1).ToString())}")+$"\nDrivers {(PcModeSetup.Ready?"installed":"missing")} · Processing {(service.Suspended?"bypassed":service.State==PcModeState.Active?service.Mute?"paused for Mouse Mode":"active":"inactive")}";
        statusLabel.Text = label; dot.Fill = new SolidColorBrush(color); statusDetail.Text = service.Detail;
        setup.Visibility = PcModeSetup.Ready ? Visibility.Collapsed : Visibility.Visible;
        if (PcModeSetup.Ready) setupNote.Text = "";
    }

    async Task RunSetup()
    {
        setup.IsEnabled = false; setupNote.Text = "Setting up. Windows may ask for permission…";
        try { setupNote.Text = await PcModeSetup.Run(); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or IOException) { setupNote.Text = "Couldn't set up PC mode: " + error.Message; }
        finally { setup.IsEnabled = true; Refresh(); service.Reload(); }
    }

    // ---- profile <-> controls --------------------------------------------------------------------------------

    void Fill(PcModeProfile p)
    {
        foreach (var (id, _) in Buttons) turbo[id].SelectedIndex = Math.Max(0, Array.IndexOf(TurboRates, p.Turbo.GetValueOrDefault(id)));
    }

    /// <summary>The saved settings (or an import) with this page's turbo on top. The tabs own everything else.</summary>
    PcModeProfile Build()
    {
        var current = store.Get(StoreKey);
        var p = clearAll ? new PcModeProfile() : incoming ?? current;
        p.Name = current.Name; p.Turbo = new();
        foreach (var (id, _) in Buttons) { int hz = TurboRates[Math.Max(0, turbo[id].SelectedIndex)]; if (hz > 0) p.Turbo[id] = hz; }
        return p;
    }

    void Persist()
    {
        if (loading) return;
        var profile = Build();
        try { profile.Validate(); }
        catch (InvalidDataException error) { saved.Text = "Not saved: " + error.Message; return; }
        if (!profile.IsNeutral && !MaySave(preferences))
        {   // the first time PC mode would actually start: show the notice and wait for "I understand"
            pendingSave = true; notice.Visibility = Visibility.Visible; saved.Text = "Read the notice above and press \"I understand\" to start PC mode."; return;
        }
        Save(profile);
    }

    void Save(PcModeProfile profile)
    {
        bool replaced = incoming != null || clearAll;
        try
        {
            store.Save(StoreKey, profile); service.Reload(); incoming = null;
            saved.Text = profile.IsNeutral ? "Nothing to apply: your controller goes straight to the game." : service.State==PcModeState.Active?"Saved · Active PC route updated":"Saved on this PC · Routing is not active";
            if (replaced) ProfileChanged?.Invoke();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Couldn't save: " + error.Message; }
    }

    void AcceptNotice()
    {
        try { preferences?.MarkPcModeNoticeSeen(); } catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException) { saved.Text = "Couldn't remember this: " + error.Message; }
        notice.Visibility = Visibility.Collapsed;
        if (pendingSave) { pendingSave = false; Save(Build()); }
        NoticeAccepted?.Invoke();
        if (!PcModeSetup.Ready) setupNote.Text = "PC mode installs two small drivers by itself when you switch it on.";
        else if (saved.Text.Length == 0) saved.Text = "Thanks. Changes in the controller tabs now apply on this PC.";
    }

    // ---- per-game profiles ---------------------------------------------------------------------------------

    void ListProfiles()
    {
        var items = new List<Choice> { new(PcModeStore.DefaultKey, "Every game") };
        // this player's games only
        items.AddRange(store.Keys.Select(PcModeStore.Split).Where(k => k.Player == Player && k.Key.StartsWith(PcModeStore.GamePrefix))
            .Select(k => new Choice(k.Key, (store.Get(PcModeStore.For(Player, k.Key)).Name ?? k.Key[PcModeStore.GamePrefix.Length..]) + "  ·  " + k.Key[PcModeStore.GamePrefix.Length..])).OrderBy(c => c.Label));
        if (!items.Any(i => i.Id == ProfileKey)) ProfileKey = PcModeStore.DefaultKey;
        bool was = loading; loading = true; profiles.ItemsSource = items; profiles.SelectedValue = ProfileKey; loading = was;
        removeGame.Visibility = ProfileKey == PcModeStore.DefaultKey ? Visibility.Collapsed : Visibility.Visible;
    }

    void SwitchTo(string key)
    {
        ProfileKey = key;
        loading = true; Fill(store.Get(StoreKey)); loading = false;
        removeGame.Visibility = key == PcModeStore.DefaultKey ? Visibility.Collapsed : Visibility.Visible;
        saved.Text = key == PcModeStore.DefaultKey ? "Editing the settings every game gets." : "Editing this game's own settings, here and in the tabs. They apply while it's in front.";
        ProfileChanged?.Invoke();
    }

    void ShowPicker()
    {
        var running = ForegroundApp.Running();
        apps.ItemsSource = running.Select(a => new Choice(a.Exe, $"{a.Title}  ·  {a.Exe}")).ToList(); apps.SelectedIndex = running.Count > 0 ? 0 : -1;
        picker.Visibility = Visibility.Visible;
        if (running.Count == 0) saved.Text = "No game is open. Start it, come back to X20CTL, and press this again.";
    }

    void AddGame()
    {
        if (apps.SelectedItem is not Choice app) return;
        string key = PcModeStore.GameKey(app.Id);
        try
        {
            if (!store.Has(PcModeStore.For(Player, key)))
            {   // a game's settings start as a copy of this player's every-game ones, so nothing changes until they are edited
                var copy = store.Get(PcModeStore.For(Player, PcModeStore.DefaultKey)); copy.Name = app.Label.Split("  ·  ")[0] is { Length: > 0 } title ? title[..Math.Min(title.Length, 120)] : app.Id;
                store.Save(PcModeStore.For(Player, key), copy); service.Reload();
            }
            picker.Visibility = Visibility.Collapsed; ProfileKey = key; ListProfiles();
            loading = true; Fill(store.Get(StoreKey)); loading = false;
            saved.Text = "Added. Change anything in the tabs and it applies only while this game is in front.";
            ProfileChanged?.Invoke();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Couldn't add it: " + error.Message; }
    }

    void RemoveGame()
    {
        if (ProfileKey == PcModeStore.DefaultKey) return;
        try
        {
            store.Remove(StoreKey); service.Reload(); ProfileKey = PcModeStore.DefaultKey; ListProfiles(); loading = true; Fill(store.Get(StoreKey)); loading = false;
            saved.Text = "Removed. That game gets the every-game settings again."; ProfileChanged?.Invoke();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { saved.Text = "Couldn't remove it: " + error.Message; }
    }

    // ---- sharing ------------------------------------------------------------------------------------------

    void ExportFile()
    {
        var dialog = new SaveFileDialog { Filter = "X20CTL PC mode settings (*.x20pc)|*.x20pc", FileName = "pc-mode.x20pc", AddExtension = true };
        if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, PcModeShare.ToJson(Build())); saved.Text = "Saved to " + Path.GetFileName(dialog.FileName) + "."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { saved.Text = "Couldn't save the file: " + error.Message; }
    }

    void ImportFile()
    {
        var dialog = new OpenFileDialog { Filter = "X20CTL PC mode settings (*.x20pc)|*.x20pc|All files|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 64 * 1024) throw new InvalidDataException("That file is too big to be PC mode settings.");
            Apply(PcModeShare.FromJson(File.ReadAllText(dialog.FileName)), "file");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Couldn't use that file: " + error.Message; }
    }

    void CopyCode()
    {
        try { Clipboard.SetText(PcModeShare.ToCode(Build())); saved.Text = "Code copied. Paste it wherever you share it; anyone can paste it into their PC mode."; }
        catch (System.Runtime.InteropServices.ExternalException) { saved.Text = "The clipboard is busy. Try again."; }
    }

    void PasteCode()
    {
        try { string text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; Apply(PcModeShare.FromCode(text), "code"); }
        catch (InvalidDataException error) { saved.Text = error.Message; }
        catch (System.Runtime.InteropServices.ExternalException) { saved.Text = "The clipboard is busy. Try again."; }
    }

    /// <summary>Shared settings replace the ones being edited (every game's, or this game's), through the same save.</summary>
    void Apply(PcModeProfile shared, string from)
    {
        incoming = shared; loading = true; Fill(shared); loading = false;
        Persist();
        if (!pendingSave) saved.Text = $"Settings from the {from} are in. " + saved.Text;
    }

    // ---- small helpers ---------------------------------------------------------------------------------------

    static TextBlock Wrap(string text, double size, string brush, double top) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
    static TextBlock Section(string text) => Kit.Text(text, 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 32, 0, 8));
    static StackPanel Block(string title, string text) => new() { Margin = new(0, 22, 0, 0), Children = { Kit.Text(title, 17, "DS.Text", FontWeights.SemiBold), Wrap(text, 15, "DS.TextSoft", 4) } };
    static Button Pill(string text) => new() { Style = DS.Style("ActionButton"), Height = 46, MinHeight = 0, Padding = new(22, 0, 24, 0), FontSize = 15.5, Content = Kit.Text(text, 15.5, "DS.Text", FontWeights.SemiBold) };
}
