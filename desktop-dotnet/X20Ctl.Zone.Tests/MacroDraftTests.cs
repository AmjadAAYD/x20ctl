namespace X20Ctl.Zone.Tests;
public class MacroDraftTests
{
    [Fact] public void SlotsRetainIndependentLocalSequencesAndCannotApply()
    {
        var draft=new MacroDraft("x20");draft.Add("M1");draft.Add("M2");draft.Update("M1",0,draft["M1"][0] with {HoldMs=120,Left=StickHeading.Up});
        Assert.Equal(120,draft["M1"][0].HoldMs);Assert.Equal(40,draft["M2"][0].HoldMs);Assert.Empty(draft["M3"]);Assert.False(draft.CanApplyHardware);
        draft.Clear("M1");Assert.Empty(draft["M1"]);Assert.Single(draft["M2"]);
    }
    [Fact] public void CapacityAccountsForReleasePausesAndRejectsWithoutMutation()
    {
        var draft=new MacroDraft("x20");for(int i=0;i<23;i++)draft.Add("M1");Assert.Equal(46,draft.EntryCount("M1"));
        draft.Add("M1",0);Assert.Equal(47,draft.EntryCount("M1"));
        Assert.Throws<InvalidOperationException>(()=>draft.Add("M1"));Assert.Equal(24,draft["M1"].Count);
        Assert.Throws<InvalidOperationException>(()=>draft.Update("M1",23,draft["M1"][23] with {PauseMs=20}));Assert.Equal(0,draft["M1"][23].PauseMs);
    }
    [Fact] public void TimingInputAndModelGuardsPreserveExistingDraft()
    {
        var draft=new MacroDraft("x20");draft.Add("M1");var step=draft["M1"][0];
        Assert.Throws<ArgumentException>(()=>draft.Update("M1",0,step with {HoldMs=41}));
        Assert.Throws<ArgumentException>(()=>draft.Update("M1",0,step with {Buttons=new[]{"HOME"}}));Assert.Equal(step,draft["M1"][0]);
        Assert.Throws<InvalidOperationException>(()=>new MacroDraft("x15").Add("M1"));
    }
    [Fact] public void ReorderAndTimingRetainEventIdentity()
    {
        var draft=new MacroDraft("x20");draft.Add("M1");draft.Add("M1");var id=draft["M1"][0].Id;draft.Move("M1",0,1);
        Assert.Equal(id,draft["M1"][1].Id);Assert.Equal(120,draft.Duration("M1"));draft.Remove("M1",1);Assert.Equal(60,draft.Duration("M1"));
    }
    [Fact] public void DuplicateCopiesInputsAndTimingWithANewIdentity()
    {
        var draft=new MacroDraft("x20");draft.Add("M1");draft.Update("M1",0,draft["M1"][0] with {Left=StickHeading.UpRight,HoldMs=125});
        draft.Duplicate("M1",0);Assert.Equal(2,draft["M1"].Count);Assert.NotEqual(draft["M1"][0].Id,draft["M1"][1].Id);
        Assert.Equal(draft["M1"][0].Buttons,draft["M1"][1].Buttons);Assert.Equal(125,draft["M1"][1].HoldMs);Assert.Equal(StickHeading.UpRight,draft["M1"][1].Left);
    }
}
