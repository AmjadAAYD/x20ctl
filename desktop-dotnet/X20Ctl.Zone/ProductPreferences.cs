using System.Security.Cryptography;
using System.Text.Json;
namespace X20Ctl.Zone;
public sealed class ProductPreferences(string path)
{
    private bool loaded;private string? baseline;private Dictionary<string,JsonElement> data=new();
    public bool IntroSeen {get;private set;}
    /// <summary>Tools → "Keep X20CTL up to date": on unless the player turns it off (owner request, 18 Oct 2026).</summary>
    public bool AutoUpdate {get;private set;}=true;
    /// <summary>Tools → PC mode: on unless the player turned it off. It still does nothing until a setting is changed.</summary>
    public bool PcMode {get;private set;}=true;
    /// <summary>The one-time "online games, closing the app, the driver" notice has been accepted.</summary>
    public bool PcModeNoticeSeen {get;private set;}
    /// <summary>Tools → Opening intro: the intro plays at every launch unless the player turns it off.</summary>
    public bool IntroOnLaunch {get;private set;}=true;
    /// <summary>The intro's sound: on unless the player mutes it, from the intro itself or from Tools.</summary>
    public bool IntroSound {get;private set;}=true;
    public void Load(){loaded=false;if(File.Exists(path)){byte[] bytes=File.ReadAllBytes(path);data=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(bytes)??throw new InvalidDataException("Empty preferences.");if(!data.TryGetValue("version",out var version)||version.GetInt32()!=1)throw new InvalidDataException("Unsupported preferences version.");IntroSeen=data.TryGetValue("introSeen",out var seen)&&seen.GetBoolean();AutoUpdate=!(data.TryGetValue("autoUpdate",out var auto)&&auto.ValueKind==JsonValueKind.False);PcMode=!(data.TryGetValue("pcMode",out var pc)&&pc.ValueKind==JsonValueKind.False);PcModeNoticeSeen=data.TryGetValue("pcModeNoticeSeen",out var seenNotice)&&seenNotice.ValueKind==JsonValueKind.True;IntroOnLaunch=!(data.TryGetValue("introOnLaunch",out var launch)&&launch.ValueKind==JsonValueKind.False);IntroSound=!(data.TryGetValue("introSound",out var sound)&&sound.ValueKind==JsonValueKind.False);baseline=Convert.ToHexString(SHA256.HashData(bytes));}loaded=true;}
    public void MarkIntroSeen(){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(IntroSeen)return;Write("introSeen",true);IntroSeen=true;}
    public void SetAutoUpdate(bool on){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(AutoUpdate==on)return;Write("autoUpdate",on);AutoUpdate=on;}
    public void SetPcMode(bool on){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(PcMode==on)return;Write("pcMode",on);PcMode=on;}
    public void MarkPcModeNoticeSeen(){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(PcModeNoticeSeen)return;Write("pcModeNoticeSeen",true);PcModeNoticeSeen=true;}
    public void SetIntroOnLaunch(bool on){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(IntroOnLaunch==on)return;Write("introOnLaunch",on);IntroOnLaunch=on;}
    public void SetIntroSound(bool on){if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(IntroSound==on)return;Write("introSound",on);IntroSound=on;}
    private void Write(string key,bool value)
    {
        string absolute=Path.GetFullPath(path),directory=Path.GetDirectoryName(absolute)!;Directory.CreateDirectory(directory);
        using var transaction=new FileStream(absolute+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);byte[]? previous=File.Exists(absolute)?File.ReadAllBytes(absolute):null;
        if((previous==null?null:Convert.ToHexString(SHA256.HashData(previous)))!=baseline)throw new IOException("Preferences changed externally; original preserved.");
        var next=new Dictionary<string,JsonElement>(data){["version"]=JsonSerializer.SerializeToElement(1),[key]=JsonSerializer.SerializeToElement(value)};
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next);string temp=absolute+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{if(previous!=null){string backup=absolute+"."+baseline+".backup";if(!File.Exists(backup)){using var stream=new FileStream(backup,FileMode.CreateNew);stream.Write(previous);stream.Flush(true);}}using(var stream=new FileStream(temp,FileMode.CreateNew)){stream.Write(bytes);stream.Flush(true);}using var check=JsonDocument.Parse(File.ReadAllBytes(temp));File.Move(temp,absolute,true);data=next;baseline=Convert.ToHexString(SHA256.HashData(bytes));}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
