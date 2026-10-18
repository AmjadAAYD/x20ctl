using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    readonly Pill save, send;
    readonly CheckBox agree = new() { Content = "I looked at what is sent and I agree to send it" }, withTrace = new() { Content = "Include the controller's part of the Bluetooth log" };
    readonly TextBlock sendStatus = T.Label("", 14, T.Soft); readonly Border sendBox = new() { IsVisible = false };
    string? receipt;
    WriteScanReport? report; byte[]? raw; string extension = "btsnoop";

    static TextBox Field(string text) => new() { Text = text, MinWidth = 220, FontSize = 14 };

    public WriteScanPage()
    {
        save = new Pill("Save results", Pill.Kinds.Ghost, Save) { IsVisible = false };
        send = new Pill("Send to X20CTLADMIN", Pill.Kinds.Primary, Send) { Enabled = false };
        agree.IsCheckedChanged += (_, _) => send.Enabled = agree.IsChecked == true && receipt == null;
        sendBox.Child = T.Card(T.Stack(10, T.Label("Send this scan", 18, null, FontWeight.SemiBold),
            T.Label("This sends the summary, the findings and the decoded messages as a research report. Nothing goes until you press the button. The Bluetooth log part is off unless you tick it.", 13.5, T.Soft),
            withTrace, agree, T.Line(12, send), sendStatus));
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
                        T.Card(steps), T.Card(plan), T.Line(12, open, save), status, result, sendBox
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
            result.Text = report.Summary(); result.IsVisible = true; save.IsVisible = true; sendBox.IsVisible = true; receipt = null; agree.IsChecked = false; sendStatus.Text = "";
            status.Text = report.IsKeyLinker ? "KeyLinker traffic found." : "No KeyLinker traffic in that recording.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
        { status.Text = "Couldn't read that file: " + e.Message; }
    }

    async void Send()
    {
        if (report == null || agree.IsChecked != true || receipt != null) return;
        send.Enabled = false; sendStatus.Text = "Sending to X20CTLADMIN…";
        try
        {
            var zip = report.ResearchZip(withTrace.IsChecked == true, AppInfo.Version);
            receipt = await WriteScanUpload.Send(zip, report.Plan.Model);
            sendStatus.Text = $"Sent · receipt {receipt}. Keep the receipt if you want to ask about it.";
        }
        catch (WriteScanUpload.NotReady e) { sendStatus.Text = e.Message; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException or IOException or JsonException) { sendStatus.Text = "Not sent: " + e.Message; }
        finally { send.Enabled = receipt == null && agree.IsChecked == true; }
    }

    void Save()
    {
        if (report == null) return;
        try { status.Text = "Saved to " + report.Save(Folder, raw, null, extension); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { status.Text = "Couldn't save: " + e.Message; }
    }
}

/// <summary>Sends a Write scan to X20CTLADMIN as a research report (schema 2, the Read scan's /api/controller-report route). It asks the receiver first whether it takes schema 2 and sends nothing if not.
/// Only ever called from the Send button, after the owner ticked that they looked at it.</summary>
internal static class WriteScanUpload
{
    const string Route = "https://x20-admin.vercel.app/api/controller-report";
    public sealed class NotReady(string message) : Exception(message);

    public static async Task<string> Send(byte[] zip, string controllerName)
    {
        if (zip.Length > WriteScanReport.MaxResearchZipBytes) throw new InvalidDataException("The report is over the receiver's 8 MB limit.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using (var probe = await http.GetAsync(Route))
        {
            bool ready = false;
            if (probe.IsSuccessStatusCode)
                try
                {
                    using var doc = JsonDocument.Parse(await probe.Content.ReadAsStringAsync());
                    ready = doc.RootElement.TryGetProperty("reportSchemaVersions", out var v) && v.ValueKind == JsonValueKind.Array && v.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Number && x.GetInt32() == 2);
                }
                catch (JsonException) { }
            if (!ready) throw new NotReady("X20CTLADMIN can't take Write scans yet. Nothing was sent; save the results instead.");
        }
        string name = string.IsNullOrWhiteSpace(controllerName) ? "Unknown controller" : controllerName.Trim()[..Math.Min(120, controllerName.Trim().Length)];
        var metadata = JsonSerializer.Serialize(new { controllerName = name, appVersion = AppInfo.Version, scannerVersion = "write-scan-1", clientTimestamp = DateTime.UtcNow.ToString("o"), connectionType = "Bluetooth (phone)", reportSchemaVersion = 2 });
        using var form = new MultipartFormDataContent("x20ctl-" + Guid.NewGuid().ToString("N"))
        {
            { new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId" }, { new StringContent(metadata), "metadata" },
        };
        var file = new ByteArrayContent(zip); file.Headers.ContentType = new("application/zip"); form.Add(file, "report", "write-scan.zip");
        using var response = await http.PostAsync(Route, form);
        string text = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode is not (200 or 201))
        {
            string reason = $"HTTP {(int)response.StatusCode}";
            try { using var d = JsonDocument.Parse(text); if (d.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)) reason = m.GetString() ?? reason; } catch (JsonException) { }
            throw new HttpRequestException("X20CTLADMIN refused it: " + reason);
        }
        using var doc2 = JsonDocument.Parse(text);
        string id = doc2.RootElement.TryGetProperty("submissionId", out var sid) ? sid.GetString() ?? "" : "";
        if (!Regex.IsMatch(id, "^CR-[0-9A-F]{24}$")) throw new InvalidDataException("X20CTLADMIN returned no valid receipt.");
        return id;
    }
}
