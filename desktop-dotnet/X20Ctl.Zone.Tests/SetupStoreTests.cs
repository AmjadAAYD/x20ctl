using System.Text.Json;
namespace X20Ctl.Zone.Tests;
public class SetupStoreTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"format\":\"x20ctl-native-setup\"}")]
    [InlineData("{\"format\":\"x20ctl-native-setup\",\"version\":1,\"setup\":null}")]
    [InlineData("{\"format\":\"x20ctl-native-setup\",\"version\":1,\"setup\":{\"snapshot\":{}}}")]
    public void MalformedImportsCannotReplaceTheLibrary(string json)
    {
        string path=Path.Combine(Folder(),"native.json");var store=new SetupStore(path);store.Load();store.Create("Keep",Snapshot());byte[] original=File.ReadAllBytes(path);
        Assert.Throws<InvalidDataException>(()=>store.ImportNative(json));Assert.Equal(original,File.ReadAllBytes(path));Assert.Single(store.Items);
    }
    private static string Folder(){string value=Path.Combine(AppContext.BaseDirectory,"setup-test-data",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(value);return value;}
    private static SetupSnapshot Snapshot()=>SetupSnapshot.Capture("x20",new("x20"),new("x20"),new("x20"),new("x20"));
    [Fact]public void NativeLibraryCrudBacksUpAndReopensWithoutLosingPayload()
    {
        string dir=Folder(),path=Path.Combine(dir,"setups.json");var store=new SetupStore(path);store.Load();var one=store.Create("My setup",Snapshot());var copy=store.Duplicate(one.Id);store.Rename(copy.Id,"My alternate");store.Delete(one.Id);
        var reopened=new SetupStore(path);reopened.Load();Assert.Single(reopened.Items);Assert.Equal("My alternate",reopened.Items[0].Name);Assert.Equal(70,reopened.NativeSnapshot(copy.Id).Vibration);Assert.NotEmpty(Directory.GetFiles(Path.Combine(dir,"setup-backups")));
    }
    [Fact]public void LegacyCopiesAreIdempotentAndPreserveUnknownFieldsAndOriginalBytes()
    {
        string dir=Folder(),source=Path.Combine(dir,"profiles.json");byte[] bytes=System.Text.Encoding.UTF8.GetBytes("[{\"id\":\"old-1\",\"name\":\"Legacy\",\"vibration\":60,\"unknown\":{\"keep\":true}}]");File.WriteAllBytes(source,bytes);
        var store=new SetupStore(Path.Combine(dir,"native.json"));store.Load();var one=store.CopyLegacy(source).Single();store.CopyLegacy(source);Assert.Single(store.Items);Assert.Equal(bytes,File.ReadAllBytes(source));Assert.Contains("unknown",store.Export(one.Id));Assert.Throws<InvalidOperationException>(()=>store.NativeSnapshot(one.Id));
        var backup=Directory.GetFiles(Path.Combine(dir,"legacy-backups"),"*.json").Single(p=>!p.EndsWith("manifest.json"));Assert.Equal(bytes,File.ReadAllBytes(backup));
        File.WriteAllText(source,"[{\"id\":\"old-1\",\"name\":\"Changed\"}]");Assert.Throws<IOException>(()=>store.CopyLegacy(source));Assert.Single(store.Items);
    }
    [Fact]public void CorruptFutureAndStaleStoresAreNeverOverwritten()
    {
        string dir=Folder(),path=Path.Combine(dir,"native.json");File.WriteAllText(path,"{\"version\":9,\"setups\":[]}");var future=new SetupStore(path);Assert.Throws<InvalidDataException>(future.Load);Assert.Throws<InvalidOperationException>(()=>future.Create("new",Snapshot()));Assert.Contains("9",File.ReadAllText(path));
        string valid=Path.Combine(dir,"valid.json");var a=new SetupStore(valid);var b=new SetupStore(valid);a.Load();b.Load();a.Create("A",Snapshot());Assert.Throws<IOException>(()=>b.Create("B",Snapshot()));Assert.Single(a.Items);
    }
    [Fact]public void ExportImportAndUnknownNativeFieldsRoundTrip()
    {
        string dir=Folder();var a=new SetupStore(Path.Combine(dir,"a.json"));a.Load();var item=a.Create("One",Snapshot());string json=a.Export(item.Id);var b=new SetupStore(Path.Combine(dir,"b.json"));b.Load();b.ImportNative(json);b.ImportNative(json);Assert.Single(b.Items);
        using var doc=JsonDocument.Parse("true");item.Extra=new(){["futureSetting"]=doc.RootElement.Clone()};string extra=a.Export(item.Id);var c=new SetupStore(Path.Combine(dir,"c.json"));c.Load();var imported=c.ImportNative(extra);c.Rename(imported.Id,"Renamed");Assert.Contains("futureSetting",c.Export(imported.Id));
    }
}
