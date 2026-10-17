using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Linux;

/// <summary>The Write scan: learns what KeyLinker writes to a pad from a phone's own Bluetooth log. Nothing is ever sent to the controller, and nothing leaves this PC.</summary>
public sealed class WriteScanPage : UserControl
{
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLWriteScans");
    readonly TextBox model = Field("X20"), app = Field("KeyLinker"), setting = Field(""), original = Field(""), test = Field("");
    readonly TextBlock status = T.Label("", 14, T.Soft);
    readonly TextBox result = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("monospace"), FontSize = 12.5, MinHeight = 260, MaxHeight = 420, TextWrapping = TextWrapping.NoWrap, IsVisible = false };
    readonly Pill save;
    WriteScanReport? report; byte[]? raw; string extension = "btsnoop";

    static TextBox Field(string text) => new() { Text = text, MinWidth = 220, FontSize = 14 };

    public WriteScanPage()
    {
        save = new Pill("Save results", Pill.Kinds.Ghost, Save) { IsVisible = false };
        var steps = T.Stack(8,
            T.Label("1  On the phone, turn on Developer options, then \"Enable Bluetooth HCI snoop log\", and switch Bluetooth off and on.", 14, T.Soft),
            T.Label("2  Open KeyLinker, connect to the pad, and change one setting from its original value to the test value, once.", 14, T.Soft),
            T.Label("3  Copy the phone's Bluetooth log to this PC (a bug report or btsnoop_hci.log) and open it below.", 14, T.Soft));
        var plan = T.Wrap(14, Labelled("Controller", model), Labelled("App", app), Labelled("Setting changed", setting), Labelled("From", original), Labelled("To", test));
        var open = new Pill("Open the recording", Pill.Kinds.Primary, Open);
        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(40, 20), Child = new StackPanel
                {
                    MaxWidth = 980, Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, Children =
                    {
                        T.Label("Write scan", 34, null, FontWeight.Bold),
                        T.Label("Learn what KeyLinker writes to a controller. This reads the phone's own Bluetooth log and sends nothing to the pad.", 15, T.Soft),
                        T.Card(steps), T.Card(plan), T.Line(12, open, save), status, result
                    }
                }
            }
        };
    }

    static Control Labelled(string label, TextBox box) => T.Stack(4, T.Label(label, 12.5, T.Muted), box);

    async void Open()
    {
        var top = TopLevel.GetTopLevel(this); if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open the phone's Bluetooth log", AllowMultiple = false });
        if (files.Count == 0) return;
        status.Text = "Reading " + files[0].Name + "…";
        try
        {
            string temp = Path.Combine(Path.GetTempPath(), "x20ctl-" + Guid.NewGuid().ToString("N") + ".bin");
            await using (var input = await files[0].OpenReadAsync()) await using (var output = File.Create(temp)) await input.CopyToAsync(output);
            var plan = new TestPlan(model.Text ?? "", app.Text ?? "", setting.Text ?? "", original.Text ?? "", test.Text ?? "");
            try
            {
                var (built, log, ext) = await Task.Run(() => { var r = BtSnoop.LoadAny(temp); return (WriteScanReport.Build(plan, [], r.Records, r.Source), r.Log, r.Extension); });
                report = built; raw = log; extension = ext;
            }
            finally { try { File.Delete(temp); } catch (IOException) { } }
            result.Text = report.Summary(); result.IsVisible = true; save.IsVisible = true;
            status.Text = report.IsKeyLinker ? "KeyLinker traffic found." : "No KeyLinker traffic in that recording.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
        { status.Text = "Couldn't read that file: " + e.Message; }
    }

    void Save()
    {
        if (report == null) return;
        try { status.Text = "Saved to " + report.Save(Folder, raw, null, extension); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { status.Text = "Couldn't save: " + e.Message; }
    }
}
