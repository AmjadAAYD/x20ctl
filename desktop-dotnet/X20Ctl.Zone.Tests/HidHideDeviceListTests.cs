using X20Ctl.Zone.PcMode;

public class HidHideDeviceListTests
{
    // Recorded from a wired X20 Pro, 2 Nov 2026: braces inside the values, one present device and one absent.
    const string WiredPad = """
         [ { "friendlyName" : "Controller (XBOX 360 Controller)" , "devices" : [
        { "present" : true ,
        "gamingDevice" : true ,
        "symbolicLink" : "\\\\?\\hid#vid_045e&pid_028e&ig_00#8&399a02ab&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}" ,
        "product" : "Controller (XBOX 360 Controller)" ,
        "deviceInstancePath" : "HID\\VID_045E&PID_028E&IG_00\\8&399a02ab&0&0000" ,
        "baseContainerDeviceInstancePath" : "USB\\VID_045E&PID_028E\\6&3428B9C&0&2" ,
        "baseContainerClassGuid" : "{D61CA365-5AF4-4486-998B-9DB4734C6CA3}" ,
        "baseContainerDeviceCount" : 1 } ,
        { "present" : false ,
        "gamingDevice" : false ,
        "symbolicLink" : "\\\\?\\hid#vid_045e&pid_028e&ig_01#8&1045a944&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}" ,
        "deviceInstancePath" : "HID\\VID_045E&PID_028E&IG_01\\8&1045a944&0&0000" ,
        "baseContainerDeviceInstancePath" : "USB\\VID_045E&PID_028E\\ABSENT" ,
        "baseContainerDeviceCount" : 1 } ] } ]
        """;

    [Fact]
    public void FindsTheWiredPadDespiteBracesInItsValues() =>
        Assert.Equal(new[]{@"HID\VID_045E&PID_028E&IG_00\8&399a02ab&0&0000",@"USB\VID_045E&PID_028E\6&3428B9C&0&2"},HidHideDeviceList.Parse(WiredPad));

    [Fact]
    public void KeepsTheXusbPathWhenThereIsOne() =>
        Assert.Contains(@"XUSB\PAD",HidHideDeviceList.Parse("""{ "present" : true , "deviceInstancePath" : "HID\\PAD" , "xusbDeviceInstancePath" : "XUSB\\PAD" }"""));

    // Recorded from the X20 Pro's 2.4G dongle, 7 Nov 2026: HidHide lists the HID gamepad and the composite USB parent,
    // but XInput reads the parent's MI_00 child, so XInput games saw the real pad next to the virtual one.
    [Fact]
    public void HidesTheDongleXInputInterfaceUnderAHiddenParent()
    {
        var xna = HidHideDeviceList.XInputClasses[0];
        var listed = new List<string> { @"HID\VID_413D&PID_2131&IG_00\9&2c7cccfa&0&0000", @"USB\VID_413D&PID_2131\6&3428B9C&0&2" };
        var present = new[]
        {
            (@"USB\VID_413D&PID_2131&MI_00\7&521C6FC&0&0000", @"USB\VID_413D&PID_2131\6&3428b9c&0&2", xna),
            (@"USB\VID_045E&PID_028E\OTHERPAD", @"USB\ROOT_HUB30\4&1", xna),
            (@"USB\VID_413D&PID_2131&MI_01\7&521C6FC&0&0001", @"USB\VID_413D&PID_2131\6&3428B9C&0&2", Guid.Empty),
        };
        var paths = HidHideDeviceList.WithXInputChildren(listed, present);
        Assert.Contains(@"USB\VID_413D&PID_2131&MI_00\7&521C6FC&0&0000", paths);
        Assert.DoesNotContain(@"USB\VID_045E&PID_028E\OTHERPAD", paths);
        Assert.DoesNotContain(@"USB\VID_413D&PID_2131&MI_01\7&521C6FC&0&0001", paths);
        Assert.Equal(3, paths.Count);
    }

    [Fact]
    public void AnUnreadableSettingsFileCanBeStartedOver()
    {
        string folder = Path.Combine(Path.GetTempPath(), "x20ctl-startover-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "pc-mode.json"); File.WriteAllText(file, "{ not json");
            var store = new PcModeStore(file); store.Load();
            Assert.False(store.Healthy);
            Assert.Throws<IOException>(() => store.Save(PcModeStore.DefaultKey, new PcModeProfile { Remap = { ["A"] = "B" } }));
            store.StartOverIfUnreadable();
            store.Save(PcModeStore.DefaultKey, new PcModeProfile { Remap = { ["A"] = "B" } });
            Assert.True(store.AnySetting);
            Assert.Equal("{ not json", File.ReadAllText(file + ".unreadable"));
            store.StartOverIfUnreadable(); // healthy now: leaves the saved settings alone
            Assert.True(store.AnySetting);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void ResetPutsEverySettingBackButKeepsGamesAndACopy()
    {
        string folder = Path.Combine(Path.GetTempPath(), "x20ctl-reset-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "pc-mode.json");
            var store = new PcModeStore(file); store.Load();
            store.Save(PcModeStore.DefaultKey, new PcModeProfile { Remap = { ["A"] = "B" }, Turbo = { ["X"] = 10 } });
            store.Save(PcModeStore.For(1, PcModeStore.DefaultKey), new PcModeProfile { Remap = { ["B"] = "Y" } });
            string game = PcModeStore.GameKey("eldenring.exe");
            store.Save(game, new PcModeProfile { Name = "ELDEN RING", Turbo = { ["A"] = 20 }, Combos = { new ComboRule { Id = "c1", Buttons = ["LB", "RB"], Output = "Y" } } });
            string before = File.ReadAllText(file);

            string? copy = store.ResetSettings();

            Assert.False(store.AnySetting);                               // every setting is back to default
            Assert.True(store.Has(game));                                 // the game is still listed
            Assert.Equal("ELDEN RING", store.Get(game).Name);              // with its name
            Assert.True(store.Get(game).IsNeutral);
            Assert.True(store.Get(PcModeStore.DefaultKey).IsNeutral);
            Assert.NotNull(copy); Assert.Equal(before, File.ReadAllText(copy!)); // the old settings are kept in full
            var again = new PcModeStore(file); again.Load(); Assert.True(again.Healthy); Assert.False(again.AnySetting); Assert.True(again.Has(game));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("Xbox360", "xbox360")] [InlineData("DualShock4", "dualshock4")]
    public void ThePadTypeSurvivesARestart(string type, string saved)
    {
        var t = Enum.Parse<PcModeEntry.VirtualType>(type);
        Assert.Equal(saved, PcModeEntry.SavedId(t));
        Assert.Equal(t, PcModeEntry.FromSaved(saved));
    }

    [Fact]
    public void TheSavedPadTypeIsReadBackFromPreferences()
    {
        string file = Path.Combine(Path.GetTempPath(), "x20ctl-prefs-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var p = new X20Ctl.Zone.ProductPreferences(file); p.Load(); Assert.Null(p.PcModePad);
            Assert.Equal(PcModeEntry.VirtualType.Xbox360, PcModeEntry.FromSaved(p.PcModePad));
            p.SetPcModePad("dualshock4");
            var again = new X20Ctl.Zone.ProductPreferences(file); again.Load();
            Assert.Equal(PcModeEntry.VirtualType.DualShock4, PcModeEntry.FromSaved(again.PcModePad));
        }
        finally { foreach (var f in Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + "*")) File.Delete(f); }
    }

    [Theory]
    [InlineData("")] [InlineData("[]")] [InlineData("""{ "present" : false , "deviceInstancePath" : "HID\\PAD" }""")]
    public void FindsNothingWithoutAPresentDevice(string output)=>Assert.Empty(HidHideDeviceList.Parse(output));
}
