using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Whole-controller setups kept in a local library: buttons, curves, macros and vibration together. Open, duplicate, rename, export and import.
/// Only the X20 can save one today; other models show the empty page.</summary>
internal sealed class SetupsPage : UserControl
{
    readonly StudioState state; readonly SetupStore store; string? renaming; string message = ""; string? selected;
    public SetupsPage(StudioState state)
    {
        this.state = state;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl");
        store = new SetupStore(Path.Combine(dir, "setups.json"));
        try { store.Load(); } catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { message = "The setup library could not be read: " + e.Message; }
        Build();
    }

    bool Can => state.Model.Id == "x20";
    IEnumerable<SavedSetup> Mine => store.Items.Where(s => s.ModelId == state.Model.Id);

    void Build()
    {
        var page = new Grid { Margin = new Thickness(36, 6, 36, 14), RowDefinitions = new("Auto,Auto,*") };
        page.Children.Add(Parts.Draft(state, withReset: false));
        int n = Mine.Count();
        var head = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 6, 0, 0) }; Grid.SetRow(head, 1);
        head.Children.Add(T.Stack(2, T.Label("My setups", 38, null, FontWeight.Bold), T.Label($"{n} saved {(n == 1 ? "setup" : "setups")} · {state.Model.Name}", 15, T.Soft)));
        var actions = T.Line(12, new Pill("New setup", Pill.Kinds.Ghost, () => Save(false)) { Enabled = Can, Padding = new Thickness(22, 12) }, new Pill("Save current draft", Pill.Kinds.Ghost, () => Save(true)) { Enabled = Can, Padding = new Thickness(22, 12) }, new Pill("Import Setup", Pill.Kinds.Ghost, Import) { Padding = new Thickness(22, 12) });
        actions.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(actions, 1); head.Children.Add(actions); page.Children.Add(head);

        Control body;
        if (n == 0)
        {
            var art = new ControllerArt(state.Model.Id) { Width = 340, Height = 210 };
            body = T.Stack(10, art, Parts.Center(T.Label("Make it yours", 30, null, FontWeight.SemiBold)), Parts.Center(T.Label("Keep your controller preferences together.\nCreate a setup, save your draft or import a preserved copy.", 16, T.Soft)));
            ((Control)body).HorizontalAlignment = HorizontalAlignment.Center; ((Control)body).VerticalAlignment = VerticalAlignment.Center; ((TextBlock)((Control)((StackPanel)body).Children[2])).TextAlignment = TextAlignment.Center;
        }
        else body = List();
        var shell = new Grid(); shell.Children.Add(body);
        if (message.Length > 0) { var note = T.Label(message, 14, T.Warn); note.VerticalAlignment = VerticalAlignment.Bottom; note.Margin = new Thickness(4, 0, 0, 6); shell.Children.Add(note); }
        Grid.SetRow(shell, 2); page.Children.Add(shell);
        Content = page;
    }

    Control List()
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        foreach (var setup in Mine.OrderByDescending(s => s.Created))
        {
            string id = setup.Id; bool on = selected == id;
            Control title = renaming == id ? Rename(setup) : T.Label(setup.Name, 21, null, FontWeight.SemiBold);
            var line = T.Label("Saved " + setup.Created.LocalDateTime.ToString("d MMM yyyy, HH:mm"), 13, T.Soft);
            var row = T.Wrap(8, new Pill("Open", Pill.Kinds.Primary, () => Open(id)) { Padding = new Thickness(18, 8) }, new Pill("Duplicate", Pill.Kinds.Ghost, () => Do(() => store.Duplicate(id))) { Padding = new Thickness(16, 8) },
                new Pill("Rename", Pill.Kinds.Ghost, () => { renaming = id; Build(); }) { Padding = new Thickness(16, 8) }, new Pill("Export", Pill.Kinds.Ghost, () => Export(id)) { Padding = new Thickness(16, 8) }, new Pill("Delete", Pill.Kinds.Ghost, () => Do(() => store.Delete(id))) { Padding = new Thickness(16, 8) });
            var card = T.Card(T.Stack(10, title, line, row), 20); card.Width = 420; card.Margin = new Thickness(0, 0, 16, 16);
            if (on) card.BorderBrush = T.B("#7FA5E0FF");
            card.OnTap(() => { selected = id; });
            wrap.Children.Add(card);
        }
        return new ScrollViewer { Content = wrap };
    }

    Control Rename(SavedSetup setup)
    {
        var box = new TextBox { Text = setup.Name, FontSize = 18, Background = T.B("#66060B18"), Foreground = T.Text, BorderBrush = T.B("#7DB6FF"), MaxLength = 60 };
        box.AttachedToVisualTree += (_, _) => { box.Focus(); box.SelectAll(); };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { string id = setup.Id, name = box.Text ?? ""; renaming = null; Do(() => store.Rename(id, name)); e.Handled = true; }
            else if (e.Key == Key.Escape) { renaming = null; Build(); e.Handled = true; }
        };
        return box;
    }

    void Do(Action action) { try { action(); message = ""; } catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or InvalidDataException) { message = e.Message; } Build(); }

    void Save(bool fromDraft)
    {
        Do(() =>
        {
            var vib = new VibrationDraft(state.Model.Id); vib.Set(state.VibrationPreview);
            var snap = SetupSnapshot.Capture(state.Model.Id, fromDraft ? state.Buttons : new ButtonDraft(state.Model.Id) { PcMode = true }, fromDraft ? state.Curves : new CurveDraft(state.Model.Id) { PcMode = true }, fromDraft ? state.Macros : new MacroDraft(state.Model.Id) { PcMode = true }, vib);
            store.Create(fromDraft ? "Setup " + (Mine.Count() + 1) : "New setup " + (Mine.Count() + 1), snap);
        });
    }

    void Open(string id)
    {
        Do(() =>
        {
            var snap = store.NativeSnapshot(id); var vib = new VibrationDraft(state.Model.Id);
            snap.LoadInto(state.Buttons, state.Curves, state.Macros, vib); state.VibrationPreview = vib.Strength; message = "Opened as the working draft.";
        });
        message = "Opened as the working draft.";
        Build();
    }

    async void Export(string id)
    {
        string text; try { text = store.Export(id); } catch (Exception e) when (e is InvalidOperationException or IOException) { message = e.Message; Build(); return; }
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, text); message = "Setup copied to the clipboard."; } else message = "The clipboard is not available.";
        Build();
    }

    async void Import()
    {
        string? text = TopLevel.GetTopLevel(this)?.Clipboard is { } clip ? await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clip) : null;
        if (string.IsNullOrWhiteSpace(text)) { message = "Copy a setup first, then choose Import Setup."; Build(); return; }
        Do(() => { store.ImportNative(text); message = "Setup imported."; });
        message = message.Length == 0 ? "Setup imported." : message; Build();
    }
}
