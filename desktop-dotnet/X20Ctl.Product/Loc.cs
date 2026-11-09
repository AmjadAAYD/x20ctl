using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace X20Ctl.Product;

/// <summary>A language the app text can be shown in; Name is written in that language so it can be found by anyone.</summary>
public sealed record AppLanguage(string Code, string Name);

/// <summary>
/// Language support (owner request 31 Oct 2026): the app's text is written in English in the code, and each language is a
/// file of English → translation pairs in Languages/. The saved choice is read once at launch (like the theme); text
/// without a translation stays English. Text is swapped as it appears on screen, so every theme gets it without its own copy.
/// </summary>
public static class Loc
{
    public static readonly AppLanguage English = new("en", "English");
    public static readonly IReadOnlyList<AppLanguage> All = new AppLanguage[]
    {
        English, new("zh", "中文"), new("fr", "Français"), new("de", "Deutsch"), new("pl", "Polski"),
        new("es", "Español"), new("ja", "日本語"), new("ru", "Русский"), new("it", "Italiano"), new("vi", "Tiếng Việt"),
    };

    public static AppLanguage Current { get; private set; } = English;
    public static AppLanguage Find(string? code) => All.FirstOrDefault(l => l.Code == code) ?? English;

    private static Dictionary<string, string> table = new(StringComparer.Ordinal);
    private static Dictionary<string, string> folded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<TextBlock, object> watched = new();
    private static bool installed;

    /// <summary>The saved choice, read before any window is built; a missing or unreadable file is English.</summary>
    public static AppLanguage Saved(string preferencesPath)
    {
        try { var preferences = new X20Ctl.Zone.ProductPreferences(preferencesPath); preferences.Load(); return Find(preferences.Language); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException) { return English; }
    }

    /// <summary>Gives the app this language, once, before the first window.</summary>
    public static void Apply(AppLanguage language)
    {
        Current = language;
        table = new(StringComparer.Ordinal); folded = new(StringComparer.OrdinalIgnoreCase);
        if (language == English) return;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Languages", language.Code + ".json");
            if (!File.Exists(path)) return;
            foreach (var (english, text) in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(path)) ?? new())
                if (!string.IsNullOrWhiteSpace(text)) { table[english] = text; folded[english] = text; shown.Add(text); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { table.Clear(); folded.Clear(); return; }
        Install();
    }

    /// <summary>The text in the chosen language, or the same text when there is none.</summary>
    public static string T(string? text)
    {
        if (string.IsNullOrEmpty(text) || table.Count == 0) return text ?? "";
        if (table.TryGetValue(text, out var exact)) return exact;
        // the code writes some labels in capitals (ToUpper), and some with a trailing mark
        if (folded.TryGetValue(text, out var other)) return IsShouting(text) ? other.ToUpper(System.Globalization.CultureInfo.CurrentUICulture) : other;
        Missing(text);
        return text;
    }

    // review aid: X20CTL_LOC_LOG names a file that collects every text shown without a translation
    private static readonly string? missingLog = Environment.GetEnvironmentVariable("X20CTL_LOC_LOG");
    private static readonly HashSet<string> missingSeen = new();
    private static HashSet<string> shown = new(StringComparer.OrdinalIgnoreCase);
    private static void Missing(string text)
    {
        if (missingLog == null || text.Trim().Length < 2 || !text.Any(char.IsLetter) || shown.Contains(text) || !missingSeen.Add(text)) return;
        try { File.AppendAllText(missingLog, text.Replace((char)13, (char)32).Replace((char)10, (char)32) + (char)10); } catch (IOException) { }
    }

    private static bool IsShouting(string text) { bool letter = false; foreach (char c in text) { if (char.IsLower(c)) return false; if (char.IsUpper(c)) letter = true; } return letter; }

    private static void Install()
    {
        if (installed) return; installed = true;
        // SizeChanged fires after the first layout, with or without a drawn window; Loaded covers the rest
        foreach (var routed in new[] { FrameworkElement.SizeChangedEvent, FrameworkElement.LoadedEvent })
        {
            EventManager.RegisterClassHandler(typeof(TextBlock), routed, new RoutedEventHandler((s, _) => WatchText((TextBlock)s)));
            EventManager.RegisterClassHandler(typeof(ContentControl), routed, new RoutedEventHandler((s, _) => WatchContent((ContentControl)s)));
        }
    }

    private static readonly ConditionalWeakTable<ContentControl, object> watchedContent = new();

    private static void WatchContent(ContentControl control)
    {
        TranslateContent(control);
        if (watchedContent.TryGetValue(control, out _)) return;
        watchedContent.Add(control, true);
        // a label set after the control is on screen is swapped as it arrives
        DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl)).AddValueChanged(control, (_, _) => TranslateContent(control));
    }

    private static void TranslateContent(ContentControl control)
    {
        if (control.Content is string label && !System.Windows.Data.BindingOperations.IsDataBound(control, ContentControl.ContentProperty)) { var next = T(label); if (next != label) control.Content = next; }
        if (control.ToolTip is string tip) { var next = T(tip); if (next != tip) control.ToolTip = next; }
    }

    private static void WatchText(TextBlock block)
    {
        Translate(block);
        if (watched.TryGetValue(block, out _)) return;
        watched.Add(block, true);
        // text set after the block is on screen (counters, statuses) is swapped as it arrives
        var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        descriptor.AddValueChanged(block, (_, _) => Translate(block));
    }

    private static void Translate(TextBlock block)
    {
        if (block.Inlines.Count > 1 || System.Windows.Data.BindingOperations.IsDataBound(block, TextBlock.TextProperty)) return;
        var text = block.Text; if (string.IsNullOrEmpty(text)) return;
        var next = T(text);
        if (next != text) block.Text = next;
    }
}
