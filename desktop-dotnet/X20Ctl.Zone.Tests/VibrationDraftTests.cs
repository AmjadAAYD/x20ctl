namespace X20Ctl.Zone.Tests;
public class VibrationDraftTests
{
    [Fact] public void StrengthChangesAreLocalBoundedAndLinked()
    {
        var draft=new VibrationDraft("x20");draft.Set(75);Assert.Equal(75,draft.Strength);Assert.True(draft.Modified);Assert.False(draft.CanApplyHardware);Assert.False(draft.CanTestHardware);
        Assert.Throws<ArgumentOutOfRangeException>(()=>draft.Set(101));Assert.Equal(75,draft.Strength);draft.Set(0);Assert.Equal(0,draft.Strength);draft.Reset();Assert.False(draft.Modified);
    }
    [Fact] public void SeparateDraftsAndUnverifiedModelsDoNotGainAuthority()
    {
        var first=new VibrationDraft("x20");var second=new VibrationDraft("x20");first.Set(100);Assert.NotEqual(first.Strength,second.Strength);
        var research=new VibrationDraft("x15");Assert.False(research.CanEdit);Assert.Throws<InvalidOperationException>(()=>research.Set(50));
    }
}
