using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace X20Ctl.Zone;
public sealed class SavedSetup
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");public string Name {get;set;}="New setup";public string ModelId {get;set;}="x20";public DateTimeOffset Created {get;set;}=DateTimeOffset.UtcNow;
    public JsonElement? Snapshot {get;set;}public JsonElement? LegacyPayload {get;set;}public string? SourceIdentity {get;set;}public string? SourceHash {get;set;}public string? SourcePath {get;set;}
    [JsonExtensionData]public Dictionary<string,JsonElement>? Extra {get;set;}
}
public sealed class SetupStore
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,Converters={new JsonStringEnumConverter()}};
    private sealed class Library {public int Version {get;set;}=1;public List<SavedSetup> Setups {get;set;}=new();[JsonExtensionData]public Dictionary<string,JsonElement>? Extra {get;set;}}
    private readonly string path;private string? baseline;private Library library=new();private bool loaded;
    public string PathName=>path;public IReadOnlyList<SavedSetup> Items=>library.Setups.AsReadOnly();
    public SetupStore(string file){path=Path.GetFullPath(file);}
    public void Load(){loaded=false;if(!File.Exists(path)){library=new();baseline=null;loaded=true;return;}var bytes=File.ReadAllBytes(path);library=Parse(bytes);baseline=Hash(bytes);loaded=true;}
    public SavedSetup Create(string name,SetupSnapshot snapshot)
    {snapshot.Validate();var saved=new SavedSetup{Name=Name(name),ModelId=snapshot.ModelId,Snapshot=JsonSerializer.SerializeToElement(snapshot,JsonOptions)};Mutate(list=>list.Add(saved));return saved;}
    public SavedSetup Duplicate(string id){var original=Find(id);var copy=JsonSerializer.Deserialize<SavedSetup>(JsonSerializer.Serialize(original,JsonOptions),JsonOptions)!;copy.Id=Guid.NewGuid().ToString("N");copy.Name=Name(original.Name+" copy");copy.SourceIdentity=null;copy.Created=DateTimeOffset.UtcNow;Mutate(list=>list.Add(copy));return copy;}
    public void Rename(string id,string name){string value=Name(name);Mutate(list=>list.Single(s=>s.Id==id).Name=value);}
    public void Delete(string id){Find(id);Mutate(list=>list.RemoveAll(s=>s.Id==id));}
    public SavedSetup Find(string id)=>library.Setups.Single(s=>s.Id==id);
    public SetupSnapshot NativeSnapshot(string id){var item=Find(id);if(!item.Snapshot.HasValue)throw new InvalidOperationException("Legacy payload is preserved, but its semantic migration has not been verified.");var snapshot=item.Snapshot.Value.Deserialize<SetupSnapshot>(JsonOptions)!;snapshot.Validate();return snapshot;}
    public string Export(string id){var saved=Find(id);return saved.LegacyPayload?.GetRawText()??JsonSerializer.Serialize(new{format="x20ctl-native-setup",version=1,setup=saved},JsonOptions);}
    public SavedSetup ImportNative(string json)
    {
        if(Encoding.UTF8.GetByteCount(json)>4*1024*1024)throw new InvalidDataException("Setup file exceeds the safe local size limit.");using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("format",out var format) || format.ValueKind!=JsonValueKind.String || format.GetString()!="x20ctl-native-setup" || !root.TryGetProperty("version",out var version) || !version.TryGetInt32(out int number) || number!=1 || !root.TryGetProperty("setup",out var payload) || payload.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Unsupported or incomplete native setup format.");
        var saved=payload.Deserialize<SavedSetup>(JsonOptions)??throw new InvalidDataException("Missing setup payload.");Validate(saved);var existing=library.Setups.FirstOrDefault(s=>s.Id==saved.Id);if(existing!=null){if(JsonSerializer.Serialize(existing,JsonOptions)!=JsonSerializer.Serialize(saved,JsonOptions))throw new IOException("A different setup already uses this identity. Original preserved.");return existing;}Mutate(list=>list.Add(saved));return saved;
    }
    public IReadOnlyList<SavedSetup> CopyLegacy(string source)
    {
        string absolute=Path.GetFullPath(source);var bytes=File.ReadAllBytes(absolute);if(bytes.Length>4*1024*1024)throw new InvalidDataException("Legacy setup file exceeds the local size limit.");using var doc=JsonDocument.Parse(bytes);var records=doc.RootElement.ValueKind==JsonValueKind.Array?doc.RootElement.EnumerateArray().Select(e=>e.Clone()).ToArray():new[]{doc.RootElement.Clone()};
        var imported=new List<SavedSetup>();int ordinal=0;foreach(var record in records)
        {
            if(record.ValueKind!=JsonValueKind.Object || !record.TryGetProperty("name",out var title) || title.ValueKind!=JsonValueKind.String)throw new InvalidDataException("Unrecognized legacy setup structure; original preserved.");
            string name=Name(title.GetString()!);string id=record.TryGetProperty("id",out var identity)?identity.ToString():"entry-"+ordinal;ordinal++;string stable=Hash(Encoding.UTF8.GetBytes(absolute.ToUpperInvariant()+"|"+id));string hash=Hash(Encoding.UTF8.GetBytes(record.GetRawText()));var existing=library.Setups.FirstOrDefault(s=>s.SourceIdentity==stable);
            if(existing!=null){if(existing.SourceHash!=hash)throw new IOException("Legacy source changed since its saved copy. Original and native copy preserved.");imported.Add(existing);continue;}
            string model=record.TryGetProperty("controllerId",out var m)?m.GetString()??"x20":record.TryGetProperty("controller_id",out m)?m.GetString()??"x20":"x20";
            imported.Add(new(){Id="legacy-"+stable,Name=name,ModelId=model,LegacyPayload=record,SourceIdentity=stable,SourceHash=hash,SourcePath=absolute});
        }
        string backupDirectory=Path.Combine(Path.GetDirectoryName(path)!,"legacy-backups");Directory.CreateDirectory(backupDirectory);string backup=Path.Combine(backupDirectory,Hash(bytes)+".json");if(File.Exists(backup)){if(Hash(File.ReadAllBytes(backup))!=Hash(bytes))throw new IOException("Existing backup is inconsistent.");}else using(var stream=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
        string manifest=backup+"."+Hash(Encoding.UTF8.GetBytes(absolute.ToUpperInvariant()))+".manifest.json";if(!File.Exists(manifest)){using var stream=new FileStream(manifest,FileMode.CreateNew,FileAccess.Write,FileShare.None);var data=JsonSerializer.SerializeToUtf8Bytes(new{source=absolute,sha256=Hash(bytes),bytes=bytes.Length,format="opaque legacy backup"},JsonOptions);stream.Write(data);stream.Flush(true);}Mutate(list=>{foreach(var item in imported)if(!list.Any(s=>s.Id==item.Id))list.Add(item);});return imported;
    }
    private void Mutate(Action<List<SavedSetup>> operation)
    {
        if(!loaded)throw new InvalidOperationException("Load and validate the native library before editing it.");var next=JsonSerializer.Deserialize<Library>(JsonSerializer.Serialize(library,JsonOptions),JsonOptions)!;operation(next.Setups);Validate(next);string directory=Path.GetDirectoryName(path)!;Directory.CreateDirectory(directory);
        using var transaction=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);byte[]? previous=File.Exists(path)?File.ReadAllBytes(path):null;if((previous==null?null:Hash(previous))!=baseline)throw new IOException("Native library changed in another app instance. Reload before saving.");
        if(previous!=null){string backupDir=Path.Combine(directory,"setup-backups");Directory.CreateDirectory(backupDir);string backup=Path.Combine(backupDir,Hash(previous)+".json");if(!File.Exists(backup)){using var stream=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(previous);stream.Flush(true);}if(Hash(File.ReadAllBytes(backup))!=Hash(previous))throw new IOException("Native backup is inconsistent; library preserved.");}
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next,JsonOptions);string temp=Path.Combine(directory,".setups-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}Parse(File.ReadAllBytes(temp));File.Move(temp,path,true);library=next;baseline=Hash(bytes);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static Library Parse(byte[] bytes){using var doc=JsonDocument.Parse(bytes);if(!doc.RootElement.TryGetProperty("version",out var version)||version.GetInt32()!=1)throw new InvalidDataException("Unsupported native library version; original preserved.");var data=JsonSerializer.Deserialize<Library>(bytes,JsonOptions)??throw new InvalidDataException("Empty native library.");Validate(data);return data;}
    private static void Validate(Library data){if(data.Version!=1||data.Setups==null||data.Setups.Any(s=>s==null)||data.Setups.Count>100||data.Setups.Select(s=>s.Id).Distinct().Count()!=data.Setups.Count)throw new InvalidDataException("Invalid native setup collection.");foreach(var setup in data.Setups)Validate(setup);}
    private static void Validate(SavedSetup setup){Name(setup.Name);if(string.IsNullOrWhiteSpace(setup.Id)||string.IsNullOrWhiteSpace(setup.ModelId)||(setup.Snapshot.HasValue==setup.LegacyPayload.HasValue))throw new InvalidDataException("Invalid setup identity or payload.");if(setup.Snapshot.HasValue){var value=setup.Snapshot.Value;if(value.ValueKind!=JsonValueKind.Object || new[]{"modelId","mappings","curves","macros","vibration"}.Any(k=>!value.TryGetProperty(k,out _)))throw new InvalidDataException("Incomplete native snapshot.");var snapshot=value.Deserialize<SetupSnapshot>(JsonOptions)??throw new InvalidDataException("Missing native snapshot.");snapshot.Validate();if(snapshot.ModelId!=setup.ModelId)throw new InvalidDataException("Setup model mismatch.");}else if(setup.LegacyPayload!.Value.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Invalid legacy payload.");}
    private static string Name(string text){if(text==null)throw new InvalidDataException("Setup name is missing.");text=text.Trim();if(text.Length is <1 or >100 || text.Any(char.IsControl))throw new ArgumentException("Use a setup name of 1–100 printable characters.");return text;}
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}
