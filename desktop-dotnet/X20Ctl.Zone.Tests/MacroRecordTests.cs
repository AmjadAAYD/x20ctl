using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;

public class MacroRecordTests
{
    [Theory]
    [InlineData("0100008800200000","R3")] [InlineData("0100008800100000","L3")] [InlineData("0100008810000000","A")] [InlineData("0100008820000000","B")]
    public void ReadsTheOwnersStoredPaddleRecords(string record,string button)
    {
        var read=Assert.Single(MacroRecord.Parse(Convert.FromHexString(record)));
        Assert.Equal(new[]{button},read.Buttons);
        Assert.Equal((StickHeading.Neutral,StickHeading.Neutral,0,0),(read.Left,read.Right,read.HoldMs,read.PauseMs));
    }

    [Fact]
    public void JoinsARestToThePressBeforeIt()
    {
        var read=MacroRecord.Parse(Convert.FromHexString("0700008810008c008800001a008810002e0088000018008810001400880000180088100016 00".Replace(" ","")));
        Assert.Equal(new[]{(700,130),(230,120),(100,120),(110,0)},read.Select(e=>(e.HoldMs,e.PauseMs)));
        Assert.All(read,e=>Assert.Equal(new[]{"A"},e.Buttons));
    }

    [Fact]
    public void ReadsStickDirections()
    {
        var read=Assert.Single(MacroRecord.Parse(Convert.FromHexString("01000020000000")[..3].Concat(new byte[]{0x82,0x00,0x00,0x14,0x00}).ToArray()));
        Assert.Equal(StickHeading.Right,read.Left);Assert.Equal(StickHeading.Neutral,read.Right);Assert.Equal(100,read.HoldMs);Assert.Empty(read.Buttons);
    }

    [Fact]
    public void AnEmptySlotHasNoEvents()=>Assert.Empty(MacroRecord.Parse([0]));

    [Theory]
    [InlineData("")] [InlineData("01")] [InlineData("0100008800")] [InlineData("0100008800200000FF")]
    public void RefusesARecordOfTheWrongLength(string record)=>Assert.Throws<FormatException>(()=>MacroRecord.Parse(Convert.FromHexString(record)));

    [Fact]
    public void AReadRecordCanReplaceADraftSlot()
    {
        var draft=new MacroDraft("x20_pro");draft.Add("M1");
        draft.Replace("M1",MacroRecord.Parse(Convert.FromHexString("0100008810000000")));
        Assert.Equal(new[]{"A"},Assert.Single(draft["M1"]).Buttons);
        Assert.Throws<ArgumentException>(()=>new MacroDraft("x20_pro").Replace("M1",[new(Guid.NewGuid(),["NOPE"],StickHeading.Neutral,StickHeading.Neutral,5,0)]));
    }

    [Theory]
    [InlineData(3,new[]{0},0)] [InlineData(-1,new[]{0},0)] [InlineData(-1,new[]{1,2},1)] [InlineData(2,new[]{1,2},2)] [InlineData(3,new int[0],-1)] [InlineData(-1,new int[0],-1)]
    public void MouseFallsBackToAConnectedController(int chosen,int[] connected,int expected)=>Assert.Equal(expected,MouseProfile.PickSlot(chosen,connected));
}
