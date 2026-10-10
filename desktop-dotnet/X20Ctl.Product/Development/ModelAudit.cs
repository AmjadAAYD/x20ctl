using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;
internal static class ModelAudit
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);var rows=new List<object>();var window=new ProductWindow(false,Path.Combine(directory,"assignment-fixture.json"));
        try
        {
            window.Show();await Task.Delay(250);await window.Host.InitializeNativeEngineAsync();if(window.Host.Engine==null)throw new InvalidOperationException("Native engine did not start.");
            string shapes=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/control-shapes.json")),rear=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/x20-rear.json"));
            foreach(var model in window.Host.Catalog.Models)
            {
                window.Host.ReturnToZone();window.Scene.AssignController(0,model);await window.Scene.FocusPlayerAsync(0);await Capture(model.Id+"-zone.png");window.Host.EnterStudio(0);var scene=window.Host.Studio!;var layout=ControlRegions.Owned(model,shapes,rear);
                scene.SelectControl("A");await Capture(model.Id+"-buttons-front.png");scene.ShowController(true);var macro=layout.Controls.Where(c=>c.Role=="macro").ToArray();if(macro.Length>0)scene.SelectControl(macro[0].Key);await Capture(model.Id+"-buttons-back.png");
                scene.ShowManagement("Device");await Capture(model.Id+"-device.png");scene.ShowManagement("Tester");scene.Tester!.SetKeyboardSimulation(true);scene.Tester.HandleKeyboard(System.Windows.Input.Key.W,true);await Capture(model.Id+"-tester-front.png");scene.Tester.HandleKeyboard(System.Windows.Input.Key.Q,true);await Capture(model.Id+"-tester-rear.png");scene.ShowManagement("Setups");await Capture(model.Id+"-setups.png");
                var capability=await window.Host.Engine.Request("capabilities",new{model=model.Id});bool bounds=layout.Controls.All(c=>c.Bounds.X>=0&&c.Bounds.Y>=0&&c.Bounds.X+c.Bounds.Width<=1.01&&c.Bounds.Y+c.Bounds.Height<=1.01);
                if(scene.Apply.IsEnabled || model.Id!="x20" && (scene.Curves.Draft.CanEdit||scene.Setups!.NewButton.IsEnabled))throw new InvalidOperationException("Configuration capability leaked to "+model.Id);
                rows.Add(new{model=model.Id,name=model.Name,frontArt="PASS: rendered own asset",backArt="PASS: rendered own asset",frontGeometry=bounds?"PASS: own source bounds and renders":"PARTIAL: bounds need review",rearGeometry=macro.Length>0?"PASS: own model contours rendered":"PARTIAL: no named rear contour in shape source",rearControlNames=macro.Select(c=>c.Key),buttons="PARTIAL: known source roles rendered; physical inventory completeness remains model-specific",animations="PASS: owned Zone focus/Studio/front-back selection software paths",tester="PASS: model geometry and labelled keyboard simulation; real hardware unverified",curves=model.Id=="x20"?"PASS: approved local preview; hardware locked":"UNVERIFIED: editor gated",macros=model.Id=="x20"?"PASS: approved local editor; hardware locked":"UNVERIFIED: editor gated; inspection contours are not writable slots",vibration=model.Id=="x20"?"PASS: approved illustrative preview; hardware locked":"UNVERIFIED: configuration/independent motor evidence absent; editor gated",setups=model.Id=="x20"?"PASS: compatible local editor":"PASS: incompatible native setup editing gated",configurationEngine=capability,knownTransport=model.Id=="x20"?"BLE configuration reference; native transport not connected":model.Id=="x15"?"Owner-reported GATT/service research; command semantics unverified":"RESEARCH/UNVERIFIED",hardwareIdentity="UNVERIFIED",verifiedRead=false,verifiedWrite=false,hardwarePersistence="UNVERIFIED",physicalAcceptance="Owner review required for non-X20 layouts"});
            }
            var report=new{status="completed",models=rows,hardwareWrites=false,desktopInputInjected=false,gameplayData="labelled keyboard simulation only",scope="software rendering/bounds/capability isolation; no physical acceptance inferred"};File.WriteAllText(Path.Combine(directory,"model-audit.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));await window.Host.StopNativeEngineAsync();
        }
        finally{window.Close();}
        async Task Capture(string name){await Task.Delay(250);window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.Host.ActualWidth),(int)Math.Ceiling(window.Host.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window.Host);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(directory,name));encoder.Save(stream);}
    }
}
