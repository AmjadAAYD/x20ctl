using X20Ctl.Zone.Telemetry;

public class NativeTelemetryTests
{
    [Fact]
    public void ReplyRequiresMatchingSerialOpcodeCrcAndDeclaredLength()
    {
        var payload=new byte[]{4,0xdf,0xaa,0,0x80};
        var good=X20Ctl.Product.X20Codec.Frame(0x19,10,9,0,payload);
        Assert.Equal(payload,NativeTelemetry.Reply(good,9));
        Assert.Null(NativeTelemetry.Reply(good,8));
        Assert.Null(NativeTelemetry.Reply(X20Ctl.Product.X20Codec.Frame(0x1f,10,9,0,payload),9));
        Assert.Null(NativeTelemetry.Reply(X20Ctl.Product.X20Codec.Frame(0x19,9,9,0,payload),9));
        good[^1]^=1;Assert.Null(NativeTelemetry.Reply(good,9));
    }
    [Theory]
    [InlineData(0x00,1,false)] [InlineData(0x20,2,false)] [InlineData(0x40,3,false)]
    [InlineData(0x80,4,false)] [InlineData(0x90,4,true)] [InlineData(0x60,2,false)]
    [InlineData(0xe0,2,false)]
    public void BatteryUsesOfficialAppBitPrecedence(byte raw,int level,bool charging)
    {
        var result=NativeTelemetry.ParsePower([4,0xdf,0xaa,0,raw]);
        Assert.Equal(level,result.Level);Assert.Equal(charging,result.Charging);
    }
    [Fact]
    public void GuidHasHardwareIdAndIndependentFirmware()
    {
        var result=NativeTelemetry.ParseHardware(Convert.FromHexString("a1ca51da724cb6103658c05d7d4e23959000"));
        Assert.Equal("2395",result.ModelCode);Assert.Equal("1.44",result.Firmware);
        Assert.Equal("A1CA51DA724CB6103658C05D7D4E2395",result.HardwareId);
    }
    [Fact]
    public void IncompleteAndUnrelatedRepliesAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>NativeTelemetry.ParseHardware(new byte[17]));
        Assert.Throws<ArgumentException>(()=>NativeTelemetry.ParsePower([4,0,0,0,0x80]));
        Assert.Throws<ArgumentException>(()=>NativeTelemetry.ParsePower([9,0xdf,0xaa,0,0x80]));
    }
}
