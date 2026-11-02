using System.Text.RegularExpressions;
namespace X20Ctl.Zone.PcMode;

/// <summary>Reads HidHideCLI's --dev-gaming output. Read by pattern rather than as JSON, since a device name Windows'
/// console can't encode would otherwise cut the output short. Values hold braces of their own (symbolic links and class
/// GUIDs), so each device runs from its "present" field to the next one instead of between braces.</summary>
public static class HidHideDeviceList
{
    static readonly Regex Present=new(@"""present""\s*:\s*(true|false)");
    static readonly Regex Path=new(@"""(?:deviceInstancePath|xusbDeviceInstancePath|baseContainerDeviceInstancePath)""\s*:\s*""((?:[^""\\]|\\.)*)""");

    /// <summary>The instance paths to hide for every device present: the HID device, and the USB or XUSB device that XInput reads.</summary>
    public static List<string> Parse(string output)
    {
        var paths=new List<string>();var devices=Present.Matches(output);
        for(int i=0;i<devices.Count;i++)
        {
            if(devices[i].Groups[1].Value!="true")continue;
            int end=i+1<devices.Count?devices[i+1].Index:output.Length;
            foreach(Match m in Path.Matches(output[devices[i].Index..end]))
                if(m.Groups[1].Value.Replace(@"\\",@"\") is {Length:>0} path)paths.Add(path);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
