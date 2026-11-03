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

    [Theory]
    [InlineData("")] [InlineData("[]")] [InlineData("""{ "present" : false , "deviceInstancePath" : "HID\\PAD" }""")]
    public void FindsNothingWithoutAPresentDevice(string output)=>Assert.Empty(HidHideDeviceList.Parse(output));
}
