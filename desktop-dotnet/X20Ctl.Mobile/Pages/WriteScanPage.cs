using X20Ctl.Mobile.Core;
using X20Ctl.Zone.Capture;

namespace X20Ctl.Mobile;

/// <summary>
/// Write scan from the phone's own Bluetooth log. The owner changes one setting in the official app; X20CTL reads the
/// recording afterwards. It sends nothing to the controller and uploads nothing.
/// </summary>
public sealed class WriteScanPage : ContentPage
{
    readonly Entry model = Ui.Field("Controller, e.g. X20"), app = Ui.Field("App used, e.g. KeyLinker"), setting = Ui.Field("Setting changed, e.g. Left vibration"),
        original = Ui.Field("Value before"), test = Ui.Field("Value you changed it to");
    readonly Label status = Ui.Note(), result = Ui.Mono();
    readonly Button save, share;
    WriteScanReport? report;
    byte[]? raw;
    string extension = "btsnoop";

    public WriteScanPage()
    {
        save = Ui.Action("Save on this phone", (_, _) => Save(), primary: false);
        share = Ui.Action("Share summary", async (_, _) => { if (report is not null) await Ui.ShareText("x20ctl-write-scan.txt", report.Summary()); }, primary: false);
        save.IsVisible = share.IsVisible = false;
        Ui.Page(this, "Write scan", Ui.Column(
            Ui.Heading("Write scan"),
            Ui.Note("See what the official app sends when it changes one setting. X20CTL only reads the phone's Bluetooth log afterwards; it never sends anything to the controller."),
            Ui.Card(Ui.Note(PhoneWriteScan.Steps)),
            model, app, setting, original, test,
            Ui.Action("Open Bluetooth log or bug report", async (_, _) => await Open()),
            status,
            Ui.Card(result),
            save, share,
            Ui.Note("A bug report and the raw log can hold device addresses and unrelated Bluetooth traffic. Look before you share them.")));
    }

    async Task Open()
    {
        FileResult? file;
        try { file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Open the phone's Bluetooth log" }); }
        catch (PermissionException) { status.Text = "X20CTL wasn't allowed to open files."; return; }
        if (file is null) return;
        status.Text = "Reading " + file.FileName + "…";
        string temp = Path.Combine(FileSystem.Current.CacheDirectory, "write-scan-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await using (var input = await file.OpenReadAsync()) await using (var output = File.Create(temp)) await input.CopyToAsync(output);
            var plan = new TestPlan(model.Text ?? "", app.Text ?? "", setting.Text ?? "", original.Text ?? "", test.Text ?? "");
            (report, raw, extension) = await Task.Run(() => PhoneWriteScan.Load(plan, temp));
            result.Text = report.Summary();
            status.Text = report.IsKeyLinker ? "KeyLinker traffic found." : "No KeyLinker traffic in that recording.";
            save.IsVisible = share.IsVisible = true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or FormatException)
        { status.Text = "Couldn't read that file: " + e.Message; }
        finally { try { File.Delete(temp); } catch (IOException) { } }
    }

    void Save()
    {
        if (report is null) return;
        try { status.Text = "Saved in the app's folder: " + Path.GetFileName(report.Save(FileSystem.Current.AppDataDirectory, raw, null, extension)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { status.Text = "Couldn't save: " + e.Message; }
    }
}
