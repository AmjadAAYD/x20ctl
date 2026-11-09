using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Zone;

namespace X20Ctl.Product.Development;

/// <summary>
/// The controller input contract on one theme, offscreen (no window is shown): bumper tab order, remap capture
/// with a release between stages, keyboard Back during capture, macro recording ownership and text-field typing.
/// Controller presses are simulated in memory; no controller, engine or virtual pad is touched.
/// </summary>
internal static class InputContractReview
{
    public static async Task Run(string directory, string theme)
    {
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true; IntroView.SoundEnabled = false;
        ThemeCatalog.Apply(ThemeCatalog.Find(theme)); Directory.CreateDirectory(directory);
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments.json"));
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("Input contract offscreen review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        host.Zone.AssignController(0, host.Catalog.Get("x20_pro")); host.EnterStudio(0);
        var s = host.Studio!; s.SetGameplayConnected(true);
        var checks = new List<object>(); int failed = 0;
        void Check(string name, bool ok, string detail = "") { checks.Add(new { name, ok, detail, page = s.CurrentPage }); if (!ok) failed++; }
        async Task Settle() { await s.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.Delay(120); stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout(); }
        void Key(Key key, bool controller = false, bool down = true)
        {
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
            object sender = controller ? host : stage;
            if (down) host.OnKeyDown(sender, e); else host.OnKeyUp(sender, e);
        }
        void Tap(Key key, bool controller = false) { Key(key, controller); Key(key, controller, false); }
        await Settle();

        // one tab order: RB from Dashboard walks the visible tabs up to Mouse, where the pad is the mouse and keeps its bumpers
        s.ShowManagement("Dashboard"); await Settle();
        var order = s.TabOrder; var walked = new List<string>(); int mouse = Array.IndexOf(order, "Mouse");
        for (int i = 0; i < mouse; i++) { Tap(System.Windows.Input.Key.PageDown, controller: true); await Settle(); walked.Add(s.CurrentPage); }
        Check("RB visits the visible tabs in order", walked.SequenceEqual(order.Skip(1).Take(mouse)), string.Join(" → ", walked));
        Tap(System.Windows.Input.Key.PageDown, controller: true); await Settle();
        Check("Mouse keeps RB (the pad is the mouse)", s.CurrentPage == "Mouse");
        s.ShowManagement("Setups"); await Settle(); Tap(System.Windows.Input.Key.PageDown, controller: true); await Settle();
        // since 7 Nov Mouse sits last with PC mode off; RB there belongs to the pointer, so the tab before it hands over to Mouse
        Check(order[^1] == "Mouse" ? "RB from Setups reaches Mouse, the last tab" : "RB from the last tab wraps to the first", s.CurrentPage == (order[^1] == "Mouse" ? "Mouse" : order[0]));
        Tap(System.Windows.Input.Key.PageUp); await Settle();
        Check("LB from the first tab wraps to the last", s.CurrentPage == order[^1]);
        s.ShowManagement("Screen"); await Settle(); Tap(System.Windows.Input.Key.PageUp); await Settle();
        Check("LB from Screen → RGB", s.CurrentPage == "RGB");
        Tap(System.Windows.Input.Key.PageDown); await Settle(); Tap(System.Windows.Input.Key.PageDown); await Settle();
        Check("RB from RGB → Screen → Device", s.CurrentPage == "Device");

        // Buttons browses: controller LB is a tab change, not a selection
        s.ShowButtons(); await Settle();
        Check("Buttons can engage remap", s.CanEngageRemap);
        // remap capture: LB as the source does not change tab; A+B in one press chooses nothing; B needs a release first
        s.BeginRemapCapture(); await Settle();
        Check("R3 engages capture", s.CapturingRemap && s.InputContext == InputOwner.RemapCapture);
        Capture(stage, Path.Combine(directory, "capture-source.png"));
        string selectedBefore = s.SelectedControl; s.ObserveCapture(["A", "B"]); await Settle();
        Check("A+B in one press chooses nothing", s.CapturingRemap && s.SelectedControl == selectedBefore && s.Draft.Target("A") == "A" && s.Draft.Target("B") == "B", selectedBefore + " → " + s.SelectedControl);
        Capture(stage, Path.Combine(directory, "capture-one-button.png"));
        s.ObserveCapture([]); s.ObserveCapture(["LB"]); await Settle();
        Check("LB is captured as the source, tab unchanged", s.CurrentPage == "Buttons" && s.SelectedControl == "LB" && s.CapturingRemap);
        s.ObserveCapture(["LB", "B"]); await Settle();
        Check("B while LB is held is not the target", s.Draft.Target("LB") == "LB" && s.CapturingRemap);
        s.ObserveCapture([]); s.ObserveCapture(["B"]); s.ObserveCapture([]); await Settle();
        Check("after a release, B is the target", s.Draft.Target("LB") == "B" && !s.CapturingRemap && s.CurrentPage == "Buttons");
        s.Draft.Reset("LB");

        // keyboard B and PageUp during capture are data in every theme (Rebel's Back used to win)
        s.BeginRemapCapture(); await Settle();
        Tap(System.Windows.Input.Key.PageUp); await Settle();
        Check("keyboard LB is captured, tab unchanged", s.CurrentPage == "Buttons" && s.SelectedControl == "LB" && s.CapturingRemap);
        Tap(System.Windows.Input.Key.B); await Settle();
        Check("keyboard B is the target and Studio stays", host.Studio == s && s.Draft.Target("LB") == "B" && s.CurrentPage == "Buttons");
        s.Draft.Reset("LB");
        s.BeginRemapCapture(); await Settle(); Tap(System.Windows.Input.Key.Escape); await Settle();
        Check("Esc cancels capture and keeps the mapping", !s.CapturingRemap && s.Draft.Target("LB") == "LB");
        s.BeginRemapCapture(); await Settle(); host.ResetTransientInput(); await Settle();
        Check("losing focus cancels capture", !s.CapturingRemap);

        // macro countdown owns the pad and the keyboard; leaving cancels without storing
        s.ShowMacros(true); await Settle();
        if (s.Macros is { } macros && macros.Draft.CanEdit)
        {
            int before = macros.Draft[macros.Slot].Count;
            macros.ToggleRecording(); await Settle();
            Check("countdown is MacroRecording", s.InputContext == InputOwner.MacroRecording && host.ExpectedNativeContext == "TESTER_CAPTURE");
            Tap(System.Windows.Input.Key.PageDown, controller: true); await Settle();
            Check("RB during countdown stays on Macros", s.CurrentPage == "Macros");
            Tap(System.Windows.Input.Key.Escape); await Settle();
            Check("Esc cancels recording, slot unchanged", !macros.IsRecording && macros.Draft[macros.Slot].Count == before && s.InputContext != InputOwner.MacroRecording);
        }
        else Check("macro slot editable", false, "skipped: read-only");

        // a text field on RGB: typing B stays in the field; the pad's B leaves the field, not the page
        s.ShowManagement("RGB"); await Settle();
        var field = Descendants(stage).OfType<TextBox>().FirstOrDefault(t => t.IsVisible && t.IsEnabled && !t.IsReadOnly);
        if (field != null)
        {
            field.Focus(); await Settle();
            Tap(System.Windows.Input.Key.B); await Settle();
            Check("typing B keeps the field and the page", s.CurrentPage == "RGB" && field.IsKeyboardFocused && host.Studio == s);
            Tap(System.Windows.Input.Key.B, controller: true); await Settle();
            Check("pad B leaves the field, page stays", s.CurrentPage == "RGB" && !field.IsKeyboardFocused && host.Studio == s);
        }
        else Check("RGB text field present", false, "no visible editable TextBox on RGB in this theme");

        File.WriteAllText(Path.Combine(directory, "input-contract.json"), JsonSerializer.Serialize(new { theme, failed, order, checks }, new JsonSerializerOptions { WriteIndented = true }));
        if (failed > 0) throw new InvalidOperationException($"{failed} input contract check(s) failed on {theme}.");
    }
    static void Capture(Visual stage, string path)
    {
        var bitmap = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(stage);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
