namespace X20Ctl.Zone.Tests;
public class ManagementTests
{
    [Fact] public void DeviceAssignmentNeverInventsSessionOrTelemetry()
    {
        var state=new DeviceOverview("x20",2);Assert.Equal("Offline",state.GameplayStatus);Assert.Equal("Offline",state.ConfigurationStatus);Assert.Null(state.BatteryPercent);Assert.Null(state.Firmware);Assert.False(state.CanWriteHardware);
    }
    [Fact] public void TesterCaptureConsumesFramesWithoutNavigationAndRejectsStaleOrOtherModel()
    {
        var tester=new TesterState("x20");tester.Enter();var frame=new TesterFrame("x20","Review fixture",true,1,["A","M1"],["A"],.5,-.4,0,0,.8,.1);
        Assert.True(tester.Accept(frame));Assert.True(tester.Pressed("A"));Assert.False(tester.Pressed("M1"));Assert.False(tester.CanNavigateGameplay);
        Assert.False(tester.Accept(frame));Assert.False(tester.Accept(frame with {ModelId="x15",Sequence=2}));tester.Exit();Assert.False(tester.Accept(frame with {Sequence=3}));Assert.Null(tester.Frame);
    }
    [Fact] public void UnverifiedLiveFramesDoNotBecomeHardwareTruth()
    {
        var tester=new TesterState("x20");tester.Enter();Assert.False(tester.Accept(new("x20","unverified source",false,1,[],[],0,0,0,0,0,0)));
        Assert.Throws<ArgumentException>(()=>tester.Accept(new("x20","Review fixture",true,1,[],[],double.NaN,0,0,0,0,0)));
    }
}
