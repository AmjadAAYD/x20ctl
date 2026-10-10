using X20Ctl.Simulation;

namespace X20Ctl.Desktop.Tests;

public class InputBehaviorTests
{
    [Theory]
    [InlineData(-32768, -1d)]
    [InlineData(32767, 1d)]
    [InlineData(0, 0d)]
    public void SignedStickEndpointsStayInsideNormalizedRange(short raw, double expected) =>
        Assert.Equal(expected, InputFrame.NormalizeAxis(raw));

    [Fact]
    public void TriggerIntermediatesAreRetained()
    {
        var frame = new InputFrame(0, true, "simulation:xinput:0", 0, Buttons.None, LT: 128);
        Assert.Equal(128, frame.LT);
        Assert.InRange(frame.LeftTrigger, .501, .503);
    }

    [Theory]
    [InlineData(1000, 32767, 255)]
    [InlineData(3000, -32768, 255)]
    public void DeterministicSimulatorReachesFullAnalogTravel(double time, short stick, byte trigger)
    {
        var frame = Simulator.Read(time, Scenario.Standard);
        Assert.True(frame.Connected);
        Assert.Equal(stick, frame.LX);
        Assert.Equal(trigger, frame.LT);
        Assert.True(frame.IsSimulation);
        Assert.Equal(frame, Simulator.Read(time, Scenario.Standard));
    }

    [Fact]
    public void FourSlotSimulationRetainsSelectedSlot()
    {
        var frame = Simulator.Read(417, Scenario.FourSlots, 3);
        Assert.True(frame.Connected);
        Assert.Equal(3, frame.Slot);
        Assert.Equal("simulation:xinput:3", frame.SourceKey);
    }

    [Fact]
    public void RawFallbackHasDifferentProvenance()
    {
        var frame = Simulator.Read(417, Scenario.RawInputFallback);
        Assert.True(frame.Connected);
        Assert.Equal("simulation:raw-input:gamepad-1", frame.SourceKey);
    }

    [Fact]
    public void PressAndReleaseUseMonotonicDurationWithoutRepeatedEdges()
    {
        var timeline = new PressTimeline();
        Assert.Single(timeline.Observe(new(0, true, "simulation:xinput:0", 100, Buttons.A)));
        Assert.Empty(timeline.Observe(new(0, true, "simulation:xinput:0", 600, Buttons.A)));
        Assert.Equal(500, timeline.Holds["A"]);
        var release = Assert.Single(timeline.Observe(new(0, true, "simulation:xinput:0", 1100, Buttons.None)));
        Assert.False(release.Down);
        Assert.Equal(1000, release.DurationMs);
        Assert.Equal(1000, timeline.LastDurationMs);
    }

    [Fact]
    public void DisconnectDoesNotBecomeASuccessfulRelease()
    {
        var timeline = new PressTimeline();
        timeline.Observe(new(0, true, "simulation:xinput:0", 100, Buttons.A));
        Assert.Empty(timeline.Observe(new(0, false, "simulation:xinput:0", 500, Buttons.None)));
        Assert.True(timeline.Interrupted);
        Assert.Null(timeline.LastDurationMs);
        Assert.Empty(timeline.Holds);
    }

    [Fact]
    public void SourceChangeInvalidatesAnUnfinishedHold()
    {
        var timeline = new PressTimeline();
        timeline.Observe(new(0, true, "simulation:xinput:0", 100, Buttons.A));
        Assert.Empty(timeline.Observe(new(1, true, "simulation:xinput:1", 400, Buttons.None)));
        Assert.True(timeline.Interrupted);
        Assert.Null(timeline.LastDurationMs);
    }

    [Fact]
    public void ActivationRequiresFreshPressAndDoesNotRepeat()
    {
        var nav = new Navigation();
        Assert.Equal(NavigationAction.Activate, nav.Read(new(0, true, "sim", 0, Buttons.A), true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 1000, Buttons.A), true));
        nav.Read(new(0, true, "sim", 1100, Buttons.None), true);
        Assert.Equal(NavigationAction.Activate, nav.Read(new(0, true, "sim", 1200, Buttons.A), true));
    }

    [Fact]
    public void ForegroundAndCaptureOwnershipSuppressNavigationAndHeldActivationOnReturn()
    {
        var nav = new Navigation();
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 0, Buttons.A), false));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 10, Buttons.A), true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 20, Buttons.None), true, true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 30, Buttons.RB), true, true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 40, Buttons.RB), true));
    }

    [Fact]
    public void DirectionRepeatHasDelayAndReleaseDeadzoneHysteresis()
    {
        var nav = new Navigation();
        Assert.Equal(NavigationAction.Right, nav.Read(new(0, true, "sim", 0, Buttons.None, LX: 23000), true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 100, Buttons.None, LX: 18000), true));
        Assert.Equal(NavigationAction.Right, nav.Read(new(0, true, "sim", 400, Buttons.None, LX: 18000), true));
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim", 420, Buttons.None, LX: 1000), true));
        Assert.Equal(NavigationAction.Right, nav.Read(new(0, true, "sim", 450, Buttons.None, LX: 23000), true));
    }
}
