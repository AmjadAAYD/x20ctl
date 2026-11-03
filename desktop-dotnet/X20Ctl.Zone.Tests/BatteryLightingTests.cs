using X20Ctl.Zone.Lighting;

public class BatteryLightingTests
{
    [Theory]
    [InlineData("4/4",4)] [InlineData("1/4",1)] [InlineData("3/4 (Charging)",3)]
    [InlineData(null,null)] [InlineData("",null)] [InlineData("0/4",null)] [InlineData("5/4",null)] [InlineData("wired",null)] [InlineData("10/4",null)]
    public void ReadsOnlyARealLevel(string? battery,int? level)=>Assert.Equal(level,BatteryLighting.ParseLevel(battery));

    [Theory]
    [InlineData(null,"4/4",4)] [InlineData(4,"3/4",3)] [InlineData(4,"4/4",null)] [InlineData(3,"3/4 (Charging)",null)] [InlineData(4,null,null)] [InlineData(null,"wired",null)]
    public void WritesOnlyWhenTheLevelChanges(int? applied,string? battery,int? expected)=>Assert.Equal(expected,BatteryLighting.LevelToApply(applied,battery));

    [Fact]
    public void EachLevelHasItsOwnColour()
    {
        var lighting=new BatteryLighting(true,[1,2,3,4]);
        for(int level=1;level<=4;level++)Assert.Equal(level,lighting.ColourFor(level));
        Assert.Throws<ArgumentOutOfRangeException>(()=>lighting.ColourFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>lighting.ColourFor(5));
    }

    [Fact]
    public void SavedChoiceSurvivesARoundTrip()
    {
        var read=BatteryLighting.Parse(new BatteryLighting(true,[0x112233,0x445566,0x778899,0xaabbcc]).ToJson());
        Assert.True(read.Enabled);
        Assert.Equal(new[]{0x112233,0x445566,0x778899,0xaabbcc},read.Colours);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("not json")] [InlineData("null")] [InlineData("{}")]
    [InlineData("{\"Enabled\":true,\"Colours\":[1,2,3]}")] [InlineData("{\"Enabled\":true,\"Colours\":[1,2,3,16777216]}")] [InlineData("{\"Enabled\":true,\"Colours\":[1,2,3,-1]}")]
    public void BadSettingsFallBackToOff(string? json)
    {
        var read=BatteryLighting.Parse(json);
        Assert.False(read.Enabled);
        Assert.Equal(BatteryLighting.Default.Colours,read.Colours);
    }
}
