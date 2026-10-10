using Xunit;
using X20Ctl.Zone;
namespace X20Ctl.Zone.Tests;
public class CurveDraftTests
{
    [Fact] public void ChannelsKeepSeparateLocalDrafts()
    {
        var draft = new CurveDraft("x20"); draft.SetPreset(CurveChannel.LeftStick, "quick");
        Assert.Equal((35,35), draft[CurveChannel.LeftStick].Point1);
        Assert.Equal((136,204), draft[CurveChannel.LeftStick].Point2);
        Assert.Equal("default", draft[CurveChannel.RightStick].Preset);
        Assert.Equal(1,draft.ModifiedChannels); Assert.False(draft.CanApplyHardware);
        draft.Reset(CurveChannel.LeftStick); Assert.Equal(0,draft.ModifiedChannels);
    }
    [Fact] public void InvalidTravelAndPointsNeverReplaceValidDraft()
    {
        var draft = new CurveDraft("x20"); var original = draft[CurveChannel.LeftTrigger];
        Assert.Throws<ArgumentException>(()=>draft.Set(CurveChannel.LeftTrigger,original with {Inner=80,Outer=75}));
        Assert.Throws<ArgumentException>(()=>draft.Set(CurveChannel.LeftTrigger,original with {Point1=(220,20),Point2=(100,100)}));
        Assert.Throws<ArgumentException>(()=>draft.Set(CurveChannel.LeftTrigger,original with {Point2=(256,100)}));
        Assert.Equal(original,draft[CurveChannel.LeftTrigger]);
    }
    [Fact] public void UnverifiedModelDoesNotGetEditableCurves()
    {
        var draft=new CurveDraft("x15"); Assert.False(draft.CanEdit);
        Assert.Throws<InvalidOperationException>(()=>draft.SetPreset(CurveChannel.RightTrigger,"fine"));
    }
    [Fact] public void CustomEditsRetainBasePresetAcrossChannelChanges()
    {
        var draft=new CurveDraft("x20");draft.SetPreset(CurveChannel.LeftStick,"quick");
        draft.Set(CurveChannel.LeftStick,draft[CurveChannel.LeftStick] with {Preset="custom",Point2=(145,200)});
        Assert.Equal("custom",draft[CurveChannel.LeftStick].Preset);Assert.Equal("quick",draft[CurveChannel.LeftStick].BasePreset);
        draft.SetPreset(CurveChannel.RightStick,"fine");Assert.Equal("quick",draft[CurveChannel.LeftStick].BasePreset);
        draft.SetPreset(CurveChannel.LeftStick,"slow");Assert.Equal("slow",draft[CurveChannel.LeftStick].BasePreset);
    }
    [Fact] public void CurrentResetRetainsOtherDraftsAndGlobalResetClearsAll()
    {
        var draft=new CurveDraft("x20");draft.SetPreset(CurveChannel.LeftStick,"quick");draft.SetPreset(CurveChannel.RightTrigger,"fine");
        var trigger=draft[CurveChannel.RightTrigger];draft.Reset(CurveChannel.LeftStick);
        Assert.Equal(CurveSetting.Default,draft[CurveChannel.LeftStick]);Assert.Equal(trigger,draft[CurveChannel.RightTrigger]);
        draft.ResetAll();Assert.All(Enum.GetValues<CurveChannel>(),c=>Assert.Equal(CurveSetting.Default,draft[c]));
    }
}
