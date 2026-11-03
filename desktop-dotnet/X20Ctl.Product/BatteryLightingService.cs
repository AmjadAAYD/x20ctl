using System.IO;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>Keeps the pad's colour on the choice for its battery level while the app runs. Writes only when the level changes.</summary>
internal static class BatteryLightingService
{
    static readonly TimeSpan Every=TimeSpan.FromMinutes(5),AfterLaunch=TimeSpan.FromSeconds(20);
    static CancellationTokenSource? loop;
    static int? applied;
    static string SettingsPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"x20ctl","lighting","battery.json");

    public static BatteryLighting Load()
    {
        try{return BatteryLighting.Parse(File.Exists(SettingsPath)?File.ReadAllText(SettingsPath):null);}
        catch(IOException){return BatteryLighting.Default;}
    }
    static void Save(BatteryLighting settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath,settings.ToJson());
    }
    public static void Resume(){var settings=Load();if(settings.Enabled)Start(settings,AfterLaunch);}
    public static async Task<int> ApplyAsync(int[] colours)
    {
        var previous=Load();Stop();
        var settings=new BatteryLighting(true,colours);
        try
        {
            var telemetry=(await NativeBleClient.QueryAsync("x20_pro")).Telemetry;
            int level=BatteryLighting.ParseLevel(telemetry?.Battery)??throw new IOException("The controller did not report a battery level.");
            await WriteAsync(settings,level);
            Save(settings);Start(settings,Every);
            return level;
        }
        catch{if(previous.Enabled)Start(previous,Every);throw;}
    }
    public static void Disable(){Stop();Save(Load() with{Enabled=false});}
    static void Stop(){loop?.Cancel();loop=null;applied=null;}
    static void Start(BatteryLighting settings,TimeSpan first){loop=new();_=Watch(settings,first,loop.Token);}
    static async Task WriteAsync(BatteryLighting settings,int level)
    {
        int colour=settings.ColourFor(level);
        await LightingBleClient.RunAsync([(byte)(colour>>16),(byte)(colour>>8),(byte)colour],2);
        applied=level;
    }
    static async Task Watch(BatteryLighting settings,TimeSpan first,CancellationToken token)
    {
        try
        {
            await Task.Delay(first,token);
            while(true)
            {
                // The pad may be asleep or out of range; the next check reads the record again before any write.
                try
                {
                    var telemetry=(await NativeBleClient.QueryAsync("x20_pro")).Telemetry;
                    token.ThrowIfCancellationRequested();
                    if(BatteryLighting.LevelToApply(applied,telemetry?.Battery) is int level)await WriteAsync(settings,level);
                }
                catch(Exception failure)when(failure is not OperationCanceledException){}
                await Task.Delay(Every,token);
            }
        }
        catch(OperationCanceledException){}
    }
}
