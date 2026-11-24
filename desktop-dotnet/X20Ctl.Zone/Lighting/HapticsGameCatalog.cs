namespace X20Ctl.Zone.Lighting;

public readonly record struct CatalogMatch(
    HapticsGenreProfile? Profile,
    bool IsIgnored,
    string DisplayName,
    bool IsMediaApp);

/// <summary>
/// Built-in curated catalog mapping popular PC games, media players, and browser tabs
/// to recommended <see cref="HapticsGenreProfile"/> settings while ignoring desktop utilities.
/// </summary>
public static class HapticsGameCatalog
{
    static readonly HashSet<string> IgnoredExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "searchhost.exe", "startmenuexperiencehost.exe", "shellexperiencehost.exe",
        "lockapp.exe", "textinputhost.exe", "applicationframehost.exe", "taskmgr.exe",
        "osk.exe", "tabtip.exe", "systemsettings.exe", "searchapp.exe", "dwm.exe",
        "x20ctl.exe", "code.exe", "devenv.exe", "rider64.exe", "cursor.exe", "antigravity.exe",
        "notepad.exe", "notepad++.exe", "wt.exe", "windowsterminal.exe", "powershell.exe",
        "pwsh.exe", "cmd.exe", "conhost.exe", "discord.exe", "slack.exe", "teams.exe",
        "ms-teams.exe", "zoom.exe", "obs64.exe", "obs32.exe", "streamlabs obs.exe",
        "steam.exe", "steamwebhelper.exe", "epicgameslauncher.exe", "eadesktop.exe",
        "upc.exe", "galaxyclient.exe", "battlenet.exe", "riotclientservices.exe",
        "nvidia app.exe", "nvcontainer.exe", "msedgewebview2.exe",
        "winword.exe", "excel.exe", "powerpnt.exe", "onenote.exe", "outlook.exe", "thunderbird.exe",
        "telegram.exe", "whatsapp.exe", "signal.exe", "blender.exe", "photoshop.exe", "audacity.exe",
        "everything.exe", "7zfm.exe", "winrar.exe", "calculatorapp.exe", "mspaint.exe",
        "snippingtool.exe", "screenclippinghost.exe", "photos.exe", "microsoft.photos.exe",
        "lghub.exe", "icue.exe", "razercentral.exe", "armourycrate.exe", "rtss.exe",
        "msiafterburner.exe", "hwinfo64.exe",
    };

    static readonly HashSet<string> BrowserExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "brave.exe", "firefox.exe", "opera.exe", "operagx.exe", "vivaldi.exe", "arc.exe",
    };

    static readonly Dictionary<string, (HapticsGenreProfile Profile, string Name, bool IsMedia)> KnownApps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Racing & Driving
            ["forzahorizon5.exe"] = (HapticsGenreProfile.Racing, "Forza Horizon 5", false),
            ["forzahorizon4.exe"] = (HapticsGenreProfile.Racing, "Forza Horizon 4", false),
            ["forza_gaming.desktop.x64_release_final.exe"] = (HapticsGenreProfile.Racing, "Forza Motorsport", false),
            ["acs.exe"] = (HapticsGenreProfile.Racing, "Assetto Corsa", false),
            ["ac2-win64-shipping.exe"] = (HapticsGenreProfile.Racing, "Assetto Corsa Competizione", false),
            ["assettocorsaevo.exe"] = (HapticsGenreProfile.Racing, "Assetto Corsa EVO", false),
            ["f1_25.exe"] = (HapticsGenreProfile.Racing, "EA Sports F1 25", false),
            ["f1_24.exe"] = (HapticsGenreProfile.Racing, "EA Sports F1 24", false),
            ["f1_23.exe"] = (HapticsGenreProfile.Racing, "F1 23", false),
            ["f1_22.exe"] = (HapticsGenreProfile.Racing, "F1 22", false),
            ["dirtrally2.exe"] = (HapticsGenreProfile.Racing, "DiRT Rally 2.0", false),
            ["wrc.exe"] = (HapticsGenreProfile.Racing, "EA Sports WRC", false),
            ["beamng.drive.x64.exe"] = (HapticsGenreProfile.Racing, "BeamNG.drive", false),
            ["needforspeedunbound.exe"] = (HapticsGenreProfile.Racing, "Need for Speed Unbound", false),
            ["needforspeedheat.exe"] = (HapticsGenreProfile.Racing, "Need for Speed Heat", false),
            ["rocketleague.exe"] = (HapticsGenreProfile.Racing, "Rocket League", false),
            ["eurotrucks2.exe"] = (HapticsGenreProfile.Racing, "Euro Truck Simulator 2", false),
            ["amtrucks.exe"] = (HapticsGenreProfile.Racing, "American Truck Simulator", false),
            ["thecrewmotorfest.exe"] = (HapticsGenreProfile.Racing, "The Crew Motorfest", false),
            ["iracingSim64DX11.exe"] = (HapticsGenreProfile.Racing, "iRacing", false),
            ["wreckfest_x64.exe"] = (HapticsGenreProfile.Racing, "Wreckfest", false),
            ["kartkraft.exe"] = (HapticsGenreProfile.Racing, "KartKraft", false),
            ["snowrunner.exe"] = (HapticsGenreProfile.Racing, "SnowRunner", false),
            ["trackmania.exe"] = (HapticsGenreProfile.Racing, "Trackmania", false),
            ["le mans ultimate.exe"] = (HapticsGenreProfile.Racing, "Le Mans Ultimate", false),
            ["carx drift racing online.exe"] = (HapticsGenreProfile.Racing, "CarX Drift Racing Online", false),

            // FPS & Action Shooters
            ["cod.exe"] = (HapticsGenreProfile.FpsAction, "Call of Duty", false),
            ["modernwarfare.exe"] = (HapticsGenreProfile.FpsAction, "Call of Duty: Modern Warfare", false),
            ["cs2.exe"] = (HapticsGenreProfile.FpsAction, "Counter-Strike 2", false),
            ["valorant-win64-shipping.exe"] = (HapticsGenreProfile.FpsAction, "VALORANT", false),
            ["cyberpunk2077.exe"] = (HapticsGenreProfile.FpsAction, "Cyberpunk 2077", false),
            ["helldivers2.exe"] = (HapticsGenreProfile.FpsAction, "HELLDIVERS 2", false),
            ["r5apex.exe"] = (HapticsGenreProfile.FpsAction, "Apex Legends", false),
            ["fortniteclient-win64-shipping.exe"] = (HapticsGenreProfile.FpsAction, "Fortnite", false),
            ["doometernalx64vk.exe"] = (HapticsGenreProfile.FpsAction, "DOOM Eternal", false),
            ["gta5.exe"] = (HapticsGenreProfile.FpsAction, "Grand Theft Auto V", false),
            ["gta5_enhanced.exe"] = (HapticsGenreProfile.FpsAction, "GTA V Enhanced", false),
            ["rdr2.exe"] = (HapticsGenreProfile.FpsAction, "Red Dead Redemption 2", false),
            ["bf2042.exe"] = (HapticsGenreProfile.FpsAction, "Battlefield 2042", false),
            ["destiny2.exe"] = (HapticsGenreProfile.FpsAction, "Destiny 2", false),
            ["escapefromtarkov.exe"] = (HapticsGenreProfile.FpsAction, "Escape from Tarkov", false),
            ["warframe.x64.exe"] = (HapticsGenreProfile.FpsAction, "Warframe", false),
            ["overwatch.exe"] = (HapticsGenreProfile.FpsAction, "Overwatch 2", false),
            ["rainbowsix.exe"] = (HapticsGenreProfile.FpsAction, "Rainbow Six Siege", false),
            ["haloinfinite.exe"] = (HapticsGenreProfile.FpsAction, "Halo Infinite", false),
            ["titanfall2.exe"] = (HapticsGenreProfile.FpsAction, "Titanfall 2", false),
            ["starfield.exe"] = (HapticsGenreProfile.FpsAction, "Starfield", false),
            ["spaceMarine2 - retail.exe"] = (HapticsGenreProfile.FpsAction, "Space Marine 2", false),
            ["re4.exe"] = (HapticsGenreProfile.FpsAction, "Resident Evil 4", false),
            ["spider-man.exe"] = (HapticsGenreProfile.FpsAction, "Marvel's Spider-Man", false),
            ["spiderman2.exe"] = (HapticsGenreProfile.FpsAction, "Marvel's Spider-Man 2", false),
            ["marvelrivals-win64-shipping.exe"] = (HapticsGenreProfile.FpsAction, "Marvel Rivals", false),
            ["discovery.exe"] = (HapticsGenreProfile.FpsAction, "THE FINALS", false),
            ["tlou-i.exe"] = (HapticsGenreProfile.FpsAction, "The Last of Us Part I", false),
            ["tlou-ii.exe"] = (HapticsGenreProfile.FpsAction, "The Last of Us Part II", false),
            ["alanwake2.exe"] = (HapticsGenreProfile.FpsAction, "Alan Wake 2", false),
            ["control_dx12.exe"] = (HapticsGenreProfile.FpsAction, "Control", false),
            ["silenthill2-win64-shipping.exe"] = (HapticsGenreProfile.FpsAction, "Silent Hill 2", false),
            ["horizonforbiddenwest.exe"] = (HapticsGenreProfile.FpsAction, "Horizon Forbidden West", false),
            ["nms.exe"] = (HapticsGenreProfile.FpsAction, "No Man's Sky", false),
            ["subnautica.exe"] = (HapticsGenreProfile.FpsAction, "Subnautica", false),

            // RPG & Melee Action
            ["eldenring.exe"] = (HapticsGenreProfile.RpgMelee, "ELDEN RING", false),
            ["sekiro.exe"] = (HapticsGenreProfile.RpgMelee, "Sekiro: Shadows Die Twice", false),
            ["darksoulsiii.exe"] = (HapticsGenreProfile.RpgMelee, "DARK SOULS III", false),
            ["witcher3.exe"] = (HapticsGenreProfile.RpgMelee, "The Witcher 3", false),
            ["bg3.exe"] = (HapticsGenreProfile.RpgMelee, "Baldur's Gate 3", false),
            ["bg3_dx11.exe"] = (HapticsGenreProfile.RpgMelee, "Baldur's Gate 3", false),
            ["monsterhunterworld.exe"] = (HapticsGenreProfile.RpgMelee, "Monster Hunter: World", false),
            ["monsterhunterwilds.exe"] = (HapticsGenreProfile.RpgMelee, "Monster Hunter Wilds", false),
            ["hades.exe"] = (HapticsGenreProfile.RpgMelee, "Hades", false),
            ["hades2.exe"] = (HapticsGenreProfile.RpgMelee, "Hades II", false),
            ["b1-win64-shipping.exe"] = (HapticsGenreProfile.RpgMelee, "Black Myth: Wukong", false),
            ["godofwar.exe"] = (HapticsGenreProfile.RpgMelee, "God of War", false),
            ["godofwarragnarok.exe"] = (HapticsGenreProfile.RpgMelee, "God of War Ragnarok", false),
            ["ghostoftsushima.exe"] = (HapticsGenreProfile.RpgMelee, "Ghost of Tsushima", false),
            ["genshinimpact.exe"] = (HapticsGenreProfile.RpgMelee, "Genshin Impact", false),
            ["starrail.exe"] = (HapticsGenreProfile.RpgMelee, "Honkai: Star Rail", false),
            ["zenlesszonezero.exe"] = (HapticsGenreProfile.RpgMelee, "Zenless Zone Zero", false),
            ["palworld-win64-shipping.exe"] = (HapticsGenreProfile.RpgMelee, "Palworld", false),
            ["liesofp-win64-shipping.exe"] = (HapticsGenreProfile.RpgMelee, "Lies of P", false),
            ["sandfall-win64-shipping.exe"] = (HapticsGenreProfile.RpgMelee, "Clair Obscur: Expedition 33", false),
            ["armoredcore6.exe"] = (HapticsGenreProfile.RpgMelee, "ARMORED CORE VI", false),
            ["nierautomata.exe"] = (HapticsGenreProfile.RpgMelee, "NieR:Automata", false),
            ["devilmaycry5.exe"] = (HapticsGenreProfile.RpgMelee, "Devil May Cry 5", false),
            ["ff7rebirth_.exe"] = (HapticsGenreProfile.RpgMelee, "FINAL FANTASY VII REBIRTH", false),
            ["ffxvi.exe"] = (HapticsGenreProfile.RpgMelee, "FINAL FANTASY XVI", false),
            ["diablo iv.exe"] = (HapticsGenreProfile.RpgMelee, "Diablo IV", false),
            ["pathofexile_x64steam.exe"] = (HapticsGenreProfile.RpgMelee, "Path of Exile", false),
            ["pathofexile2.exe"] = (HapticsGenreProfile.RpgMelee, "Path of Exile 2", false),
            ["skyrimse.exe"] = (HapticsGenreProfile.RpgMelee, "Skyrim Special Edition", false),
            ["fallout4.exe"] = (HapticsGenreProfile.RpgMelee, "Fallout 4", false),
            ["oblivionremastered-win64-shipping.exe"] = (HapticsGenreProfile.RpgMelee, "Oblivion Remastered", false),
            ["kcd2.exe"] = (HapticsGenreProfile.RpgMelee, "Kingdom Come: Deliverance II", false),
            ["acshadows.exe"] = (HapticsGenreProfile.RpgMelee, "Assassin's Creed Shadows", false),
            ["acmirage.exe"] = (HapticsGenreProfile.RpgMelee, "Assassin's Creed Mirage", false),
            ["terraria.exe"] = (HapticsGenreProfile.RpgMelee, "Terraria", false),
            ["stardew valley.exe"] = (HapticsGenreProfile.RpgMelee, "Stardew Valley", false),

            // Sports & Arcade Fighting
            ["fc25.exe"] = (HapticsGenreProfile.SportsArcade, "EA Sports FC 25", false),
            ["fc24.exe"] = (HapticsGenreProfile.SportsArcade, "EA Sports FC 24", false),
            ["nba2k25.exe"] = (HapticsGenreProfile.SportsArcade, "NBA 2K25", false),
            ["sf6.exe"] = (HapticsGenreProfile.SportsArcade, "Street Fighter 6", false),
            ["tekken 8.exe"] = (HapticsGenreProfile.SportsArcade, "TEKKEN 8", false),
            ["polaris-win64-shipping.exe"] = (HapticsGenreProfile.SportsArcade, "TEKKEN 8", false),
            ["mk12.exe"] = (HapticsGenreProfile.SportsArcade, "Mortal Kombat 1", false),
            ["ggst-win64-shipping.exe"] = (HapticsGenreProfile.SportsArcade, "Guilty Gear Strive", false),
            ["hollow_knight.exe"] = (HapticsGenreProfile.SportsArcade, "Hollow Knight", false),
            ["silksong.exe"] = (HapticsGenreProfile.SportsArcade, "Hollow Knight: Silksong", false),
            ["hollow knight silksong.exe"] = (HapticsGenreProfile.SportsArcade, "Hollow Knight: Silksong", false),
            ["celeste.exe"] = (HapticsGenreProfile.SportsArcade, "Celeste", false),
            ["deadcells.exe"] = (HapticsGenreProfile.SportsArcade, "Dead Cells", false),
            ["oriwotw.exe"] = (HapticsGenreProfile.SportsArcade, "Ori and the Will of the Wisps", false),
            ["cuphead.exe"] = (HapticsGenreProfile.SportsArcade, "Cuphead", false),
            ["wwe2k25.exe"] = (HapticsGenreProfile.SportsArcade, "WWE 2K25", false),

            // Music Players (7)
            ["spotify.exe"] = (HapticsGenreProfile.Music, "Spotify", true),
            ["applemusic.exe"] = (HapticsGenreProfile.Music, "Apple Music", true),
            ["tidal.exe"] = (HapticsGenreProfile.Music, "TIDAL", true),
            ["foobar2000.exe"] = (HapticsGenreProfile.Music, "foobar2000", true),
            ["musicbee.exe"] = (HapticsGenreProfile.Music, "MusicBee", true),
            ["itunes.exe"] = (HapticsGenreProfile.Music, "iTunes", true),
            ["aimp.exe"] = (HapticsGenreProfile.Music, "AIMP", true),

            // Cinema / Video Players (6)
            ["vlc.exe"] = (HapticsGenreProfile.Cinema, "VLC Media Player", true),
            ["mpc-hc64.exe"] = (HapticsGenreProfile.Cinema, "MPC-HC", true),
            ["mpv.exe"] = (HapticsGenreProfile.Cinema, "mpv", true),
            ["netflix.exe"] = (HapticsGenreProfile.Cinema, "Netflix", true),
            ["primevideo.exe"] = (HapticsGenreProfile.Cinema, "Prime Video", true),
            ["disneyplus.exe"] = (HapticsGenreProfile.Cinema, "Disney+", true),
        };

    public static bool IsIgnored(string? exe) =>
        string.IsNullOrWhiteSpace(exe) || IgnoredExes.Contains(Path.GetFileName(exe.Trim()));

    public static bool IsBrowser(string? exe) =>
        !string.IsNullOrWhiteSpace(exe) && BrowserExes.Contains(Path.GetFileName(exe.Trim()));

    public static CatalogMatch Resolve(string? exe, string? windowTitle = null)
    {
        if (string.IsNullOrWhiteSpace(exe))
            return new(null, true, "Desktop", true);

        string fileName = Path.GetFileName(exe.Trim());
        if (IgnoredExes.Contains(fileName))
            return new(null, true, Path.GetFileNameWithoutExtension(fileName), true);

        if (KnownApps.TryGetValue(fileName, out var entry))
            return new(entry.Profile, false, entry.Name, entry.IsMedia);

        if (BrowserExes.Contains(fileName))
        {
            string title = windowTitle ?? string.Empty;
            if (ContainsAny(title, "spotify", "soundcloud", "apple music", "tidal", "bandcamp", "music.youtube"))
                return new(HapticsGenreProfile.Music, false, TrimBrowserTitle(title, fileName), true);
            if (ContainsAny(title, "netflix", "prime video", "disney+", "hulu", "crunchyroll", "plex", "twitch", "hbo", "max "))
                return new(HapticsGenreProfile.Cinema, false, TrimBrowserTitle(title, fileName), true);
            if (ContainsAny(title, "youtube"))
                return new(HapticsGenreProfile.Music, false, TrimBrowserTitle(title, fileName), true);

            return new(null, true, TrimBrowserTitle(title, fileName), true);
        }

        // Heuristic fallback for alternate shipping executable names (e.g. GameName-Win64-Shipping.exe)
        string titleOrExe = $"{fileName} {windowTitle}";
        if (ContainsAny(titleOrExe, "forza", "assetto", "rally", "motorsport", "f1_", "beamng", "kart", "drift", "racing"))
            return new(HapticsGenreProfile.Racing, false, CleanName(fileName), false);
        if (ContainsAny(titleOrExe, "elden", "sekiro", "souls", "witcher", "wukong", "god of war", "tsushima", "monhun"))
            return new(HapticsGenreProfile.RpgMelee, false, CleanName(fileName), false);
        if (ContainsAny(titleOrExe, "call of duty", "counter-strike", "valorant", "cyberpunk", "doom", "battlefield", "apex", "fortnite"))
            return new(HapticsGenreProfile.FpsAction, false, CleanName(fileName), false);

        return new(null, false, !string.IsNullOrWhiteSpace(windowTitle) ? windowTitle!.Trim() : CleanName(fileName), false);
    }

    static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (string n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string CleanName(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (string suffix in new[] { "-Win64-Shipping", "-WinGDK-Shipping", "_x64", "_dx12", "_dx11" })
        {
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                stem = stem[..^suffix.Length];
        }
        return stem.Replace('_', ' ').Trim();
    }

    static string TrimBrowserTitle(string title, string fallbackExe)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Path.GetFileNameWithoutExtension(fallbackExe);
        return title.Length > 48 ? title[..45] + "..." : title;
    }
}
