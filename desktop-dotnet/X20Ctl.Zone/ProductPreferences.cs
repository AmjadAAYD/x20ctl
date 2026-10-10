using System.Security.Cryptography;
using System.Text.Json;
namespace X20Ctl.Zone;
public sealed class ProductPreferences(string path)
{
    private bool loaded;private string? baseline;private Dictionary<string,JsonElement> data=new();
    public bool IntroSeen {get;private set;}
    public void Load(){loaded=false;if(File.Exists(path)){byte[] bytes=File.ReadAllBytes(path);data=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(bytes)??throw new InvalidDataException("Empty preferences.");if(!data.TryGetValue("version",out var version)||version.GetInt32()!=1)throw new InvalidDataException("Unsupported preferences version.");IntroSeen=data.TryGetValue("introSeen",out var seen)&&seen.GetBoolean();baseline=Convert.ToHexString(SHA256.HashData(bytes));}loaded=true;}
    public void MarkIntroSeen()
    {
        if(!loaded)throw new InvalidOperationException("Preferences were not validated.");if(IntroSeen)return;
        string absolute=Path.GetFullPath(path),directory=Path.GetDirectoryName(absolute)!;Directory.CreateDirectory(directory);
        using var transaction=new FileStream(absolute+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);byte[]? previous=File.Exists(absolute)?File.ReadAllBytes(absolute):null;
        if((previous==null?null:Convert.ToHexString(SHA256.HashData(previous)))!=baseline)throw new IOException("Preferences changed externally; original preserved.");
        var next=new Dictionary<string,JsonElement>(data){["version"]=JsonSerializer.SerializeToElement(1),["introSeen"]=JsonSerializer.SerializeToElement(true)};
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next);string temp=absolute+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{if(previous!=null){string backup=absolute+"."+baseline+".backup";if(!File.Exists(backup)){using var stream=new FileStream(backup,FileMode.CreateNew);stream.Write(previous);stream.Flush(true);}}using(var stream=new FileStream(temp,FileMode.CreateNew)){stream.Write(bytes);stream.Flush(true);}using var check=JsonDocument.Parse(File.ReadAllBytes(temp));File.Move(temp,absolute,true);IntroSeen=true;data=next;baseline=Convert.ToHexString(SHA256.HashData(bytes));}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
