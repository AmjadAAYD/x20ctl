using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>An X20 Pro keeps its mappings, curves and macros in its own memory: the tabs open on what it holds, and Apply
/// to controller writes what changed over Bluetooth, on any transport the pad is playing on.</summary>
public partial class StudioScene
{
    static readonly HashSet<ButtonDraft> filledFromController = new(); // drafts outlive a Studio, so reopening keeps unsaved edits
    bool controllerBusy;
    bool ControllerWrites => Model.Id == "x20_pro" && !Draft.PcMode && !ReviewSandbox.Active;

    void UseControllerWrites()
    {
        if (!ControllerWrites) return;
        buttonsPage.UseController(() => _ = ApplyToController(buttonsPage.SayController));
        var curveApply = new Button { Content = "Apply to controller", Style = DS.Style("ActionButton"), Height = 44, MinHeight = 0, Padding = new(18, 0, 20, 0), Margin = new(0, 0, 8, 0) };
        curveApply.Click += (_, _) => _ = ApplyToController(text => ChangeCount.Text = text);
        RegisterFocus(curveApply); Curves.Toolbar.Children.Add(curveApply);
        // The themed Curves pages have no Apply button of their own, so a curve edit there is saved to the pad once it has settled for two seconds.
        if (ThemeCatalog.Current != ThemeCatalog.Default)
        {
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            settle.Tick += (_, _) => { settle.Stop(); if (controllerBusy) settle.Start(); else _ = ApplyToController(text => ChangeCount.Text = text); };
            Curves.Changed += () => { if (filledFromController.Contains(Draft)) { settle.Stop(); settle.Start(); } };
        }
        Loaded += (_, _) => _ = LoadFromController(buttonsPage.SayController);
    }
    void UseControllerWrites(MacroWorkbench macros)
    {
        if (ControllerWrites) macros.UseController(say => ApplyToController(say));
    }

    async Task LoadFromController(Action<string> say)
    {
        if (filledFromController.Contains(Draft)) return;
        controllerBusy = true;
        try
        {
            ControllerRecords.Load(await LightingBleClient.ReadConfigAsync(), Draft, Curves.Draft, macroDraft);
            filledFromController.Add(Draft);
            Curves.Refresh(); Macros?.Refresh(); RefreshEditor(); UpdateCurveContext(); UpdateMacroContext();
            say("Showing what's on the controller. Apply to save changes.");
        }
        catch (Exception error) when (Failure(error)) { say("Couldn't read the controller: " + error.Message); }
        finally { controllerBusy = false; }
    }

    async Task ApplyToController(Action<string> say)
    {
        if (controllerBusy) { say("The controller is busy. Try again in a moment."); return; }
        if (!filledFromController.Contains(Draft))
        {
            // writing without knowing what it holds could wipe its macros
            await LoadFromController(say);
            if (!filledFromController.Contains(Draft)) return;
            say("Read the controller. Make your change, then Apply.");
            return;
        }
        controllerBusy = true; say("Writing to the controller…");
        try
        {
            int changed = await LightingBleClient.WriteConfigAsync(held => ControllerRecords.Compose(held, Draft, Curves.Draft, macroDraft));
            say(changed == 0 ? "The controller already has this." : $"Saved on the controller · {changed} setting{(changed == 1 ? "" : "s")} changed.");
        }
        catch (Exception error) when (Failure(error)) { say("Couldn't write to the controller: " + error.Message); }
        finally { controllerBusy = false; }
    }

    async Task FactoryReset(Action<string> say)
    {
        if (ReviewSandbox.Active) return;
        if (controllerBusy) { say("The controller is busy. Try again in a moment."); return; }
        controllerBusy = true; say("Resetting the controller…");
        try
        {
            int changed = await LightingBleClient.FactoryResetAsync();
            filledFromController.Clear();
            controllerBusy = false;
            await LoadFromController(_ => { });
            say(changed == 0 ? "Nothing changed on the controller." : $"Controller reset · {changed} setting{(changed == 1 ? "" : "s")} changed.");
        }
        catch (Exception error) when (Failure(error)) { say("Couldn't finish the reset: " + error.Message); }
        finally { controllerBusy = false; }
    }
    static bool Failure(Exception error) => error is IOException or TimeoutException or FormatException or ArgumentException or InvalidOperationException or COMException or UnauthorizedAccessException;
}
