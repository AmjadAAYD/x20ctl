using X20Ctl.Zone.Telemetry;

public class AutomaticTelemetryTests
{
    [Fact]
    public void OpeningRefreshesOncePerSupportedTarget()
    {
        var policy=new TelemetryRefreshPolicy();
        Assert.True(policy.OnOpen("0:x20_pro"));
        Assert.False(policy.OnOpen("0:x20_pro"));
        Assert.True(policy.OnOpen("1:x20"));
        Assert.False(policy.OnOpen("0:d10"));
    }
    [Fact]
    public void ConnectionFramesDoNotPollButReconnectRefreshes()
    {
        var policy=new TelemetryRefreshPolicy();
        Assert.True(policy.OnOpen("0:x20_pro"));
        Assert.False(policy.OnConnection("0:x20_pro",true));
        Assert.False(policy.OnConnection("0:x20_pro",true));
        Assert.False(policy.OnConnection("0:x20_pro",false));
        Assert.True(policy.OnConnection("0:x20_pro",true));
        Assert.False(policy.OnConnection("0:x20_pro",true));
    }
    [Theory]
    [InlineData("receiver")] [InlineData("wired")] [InlineData(null)]
    public void PlaceholderCannotReplaceMeasuredBattery(string? incoming)=>
        Assert.Equal("4/4",NativeTelemetry.PreferMeasuredBattery("4/4",incoming));
}
