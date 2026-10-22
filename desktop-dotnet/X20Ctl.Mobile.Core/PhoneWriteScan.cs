using X20Ctl.Zone.Capture;

namespace X20Ctl.Mobile.Core;

/// <summary>
/// Write scan on the phone: read the phone's own Bluetooth log (or a bug report ZIP holding it) of the official app
/// changing one setting. The same Zone code as the desktop does the work; X20CTL sends nothing to the controller.
/// </summary>
public static class PhoneWriteScan
{
    public static (WriteScanReport Report, byte[] Log, string Extension) Load(TestPlan plan, string path)
    {
        var r = BtSnoop.LoadAny(path);
        return (WriteScanReport.Build(plan, [], r.Records, r.Source), r.Log, r.Extension);
    }

    /// <summary>Where Android keeps the log, and how to get it, in plain steps.</summary>
    public const string Steps =
        "1. In Developer options, turn on \"Enable Bluetooth HCI snoop log\" (choose Enabled, not Filtered). Turn Bluetooth off and on.\n" +
        "2. Write down the setting's current value. In the official app, change that one setting, then change it back.\n" +
        "3. Make a bug report (Developer options > Take bug report), or copy btsnoop_hci.log if your phone shows it.\n" +
        "4. Open the bug report ZIP or the log here.";
}
