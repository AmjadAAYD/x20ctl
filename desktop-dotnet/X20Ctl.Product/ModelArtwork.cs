using System.IO;
using System.Text.Json;
using System.Windows.Media;
namespace X20Ctl.Product;
internal static class ModelArtwork
{
    private static readonly Dictionary<string,Geometry> clips=new();
    public static Geometry Clip(string model,bool rear)
    {
        string key=model+(rear?":back":":front");if(clips.TryGetValue(key,out var cached))return cached;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/lighting.json")));var root=doc.RootElement;var geometry=Geometry.Parse(root.GetProperty("models").GetProperty(model).GetProperty(rear?"back":"front").GetString()!).Clone();var size=root.GetProperty("sourceSize").EnumerateArray().Select(v=>v.GetDouble()).ToArray();geometry.Transform=new ScaleTransform(1536/size[0],1024/size[1]);geometry.Freeze();clips[key]=geometry;return geometry;
    }
}
