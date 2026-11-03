using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Lighting;

/// <summary>One colour per reported battery level, lowest first. The app applies them; the firmware has no battery effect.</summary>
public sealed record BatteryLighting(bool Enabled,int[] Colours)
{
    public static BatteryLighting Default=>new(false,[0xff0000,0xff8000,0xffff00,0x00ff00]);
    public int ColourFor(int level)=>level is >=1 and <=4?Colours[level-1]:throw new ArgumentOutOfRangeException(nameof(level));
    public string ToJson()=>JsonSerializer.Serialize(this);
    public static BatteryLighting Parse(string? json)
    {
        try
        {
            var read=json==null?null:JsonSerializer.Deserialize<BatteryLighting>(json);
            return read?.Colours is {Length:4} colours&&colours.All(c=>c is >=0 and <=0xffffff)?read:Default;
        }
        catch(JsonException){return Default;}
    }
    /// <summary>The level from a telemetry reading such as "3/4 (Charging)"; null when the pad gave none.</summary>
    public static int? ParseLevel(string? battery)
    {
        var match=Regex.Match(battery??"",@"^([1-4])/4\b");
        return match.Success?match.Groups[1].Value[0]-'0':null;
    }
    /// <summary>The level to write for this reading; null when it is unknown or already applied, so a steady battery never writes.</summary>
    public static int? LevelToApply(int? applied,string? battery)=>ParseLevel(battery) is int level&&level!=applied?level:null;
}
