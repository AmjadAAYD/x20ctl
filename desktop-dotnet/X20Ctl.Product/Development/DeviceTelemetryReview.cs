using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace X20Ctl.Product.Development;

internal static class DeviceTelemetryReview
{
    public static async Task Run(string directory)
    {
        ReviewSandbox.Active=true;ReviewSandbox.HideLibrary=true;IntroView.SoundEnabled=false;
        Directory.CreateDirectory(directory);
        var host=new ProductHost(new ZoneScene(true),Path.Combine(directory,"assignments.json"));
        host.AutomaticTelemetryInReview=true;
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.TelemetryRefreshed+=()=>ready.TrySetResult();
        var stage=new DesignStage{Child=host};
        using var source=new HwndSource(new HwndSourceParameters("Device telemetry offscreen review"){WindowStyle=unchecked((int)0x80000000),Width=1600,Height=900,PositionX=-32000,PositionY=-32000}){RootVisual=stage};
        host.Zone.AssignController(0,host.Catalog.Get("x20_pro"));host.EnterStudio(0);
        host.Studio!.SetTransport("2.4 GHz receiver");host.Studio.ShowManagement("Device");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(25));
        // Gameplay connectivity is a presentation fixture here, not a hardware-detection claim.
        host.Studio.SetGameplayConnected(true);
        host.UpdateBatteryLevels(["receiver",null,null,null],"2.4 GHz receiver");
        await host.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle);await Task.Delay(100);
        stage.Measure(new Size(1600,900));stage.Arrange(new Rect(0,0,1600,900));stage.UpdateLayout();
        var device=host.Studio.Device!;
        if(device.BatteryText!=host.BatteryLevels[0]||device.BatteryText is "receiver" or "wired" or "Not reported over receiver")throw new InvalidOperationException("Receiver placeholder replaced measured battery.");
        var header=FindHeader(host.Studio)??throw new InvalidOperationException("Battery header missing.");
        var headerText=System.Windows.Automation.AutomationProperties.GetName(header);
        if(!headerText.Contains(device.BatteryText))throw new InvalidOperationException("Header battery differs from Device.");
        File.WriteAllText(Path.Combine(directory,"automatic-result.txt"),$"PASS: automatic refresh on Studio entry; receiver placeholder preserved measured value; {headerText}. Firmware {device.FirmwareText}; Hardware {device.RevisionText}.");
        File.WriteAllText(Path.Combine(directory,"display.json"),JsonSerializer.Serialize(new{device.BatteryText,device.FirmwareText,device.RevisionText,device.TransportText}));
        var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);bitmap.Render(stage);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(directory,"device.png"));png.Save(file);
    }
    static HeaderBattery? FindHeader(DependencyObject parent)
    {
        if(parent is HeaderBattery header)return header;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var found=FindHeader(VisualTreeHelper.GetChild(parent,i));if(found!=null)return found;}
        return null;
    }
}
