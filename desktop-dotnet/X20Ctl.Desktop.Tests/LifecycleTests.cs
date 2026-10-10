using X20Ctl.Simulation;
namespace X20Ctl.Desktop.Tests;
public class LifecycleTests
{
    [Fact]
    public void SlowIntentionalMouseMovementAccumulatesButJitterDoesNotCancelDemo()
    {
        var mode = new PointerMode(); mode.EnterDemo(0, 0);
        Assert.False(mode.Observe(2, 0)); Assert.False(mode.Observe(4, 0));
        Assert.True(mode.Observe(7, 0));
    }
    [Fact]
    public void MouseJitterCanReturnToTheAnchorWithoutCancellingDemo()
    {
        var mode = new PointerMode(); mode.EnterDemo(50, 30);
        Assert.False(mode.Observe(52, 31)); Assert.False(mode.Observe(49, 30));
    }
    [Fact]
    public void ReconnectedHeldButtonCannotActivateUntilReleasedAndPressedAgain()
    {
        var nav = new Navigation();
        nav.Read(new(0, true, "sim:0", 0, Buttons.None), true);
        nav.Read(new(0, false, "sim:0", 50, Buttons.None), true);
        Assert.Equal(NavigationAction.None, nav.Read(new(0, true, "sim:0", 100, Buttons.A), true));
        nav.Read(new(0, true, "sim:0", 150, Buttons.None), true);
        Assert.Equal(NavigationAction.Activate, nav.Read(new(0, true, "sim:0", 200, Buttons.A), true));
    }
    [Fact]
    public void ChangedSourceCannotConvertHeldButtonsIntoNavigation()
    {
        var nav = new Navigation();
        nav.Read(new(0, true, "sim:0", 0, Buttons.None), true);
        Assert.Equal(NavigationAction.None, nav.Read(new(1, true, "sim:1", 100, Buttons.A), true));
    }
    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidSimulationTimeIsRejected(double time) => Assert.Throws<ArgumentOutOfRangeException>(() => Simulator.Read(time, Scenario.Standard));
    [Theory]
    [InlineData(-1)] [InlineData(4)]
    public void InvalidPlayerSlotIsRejected(int slot) => Assert.Throws<ArgumentOutOfRangeException>(() => Simulator.Read(0, Scenario.Standard, slot));
    [Fact]
    public void IncompleteScenarioRetainsAnInterruptedHoldWithoutInventedRelease()
    {
        var timeline = new PressTimeline();
        timeline.Observe(Simulator.Read(100, Scenario.Incomplete));
        Assert.Empty(timeline.Observe(Simulator.Read(1200, Scenario.Incomplete)));
        Assert.True(timeline.Interrupted); Assert.Null(timeline.LastDurationMs);
    }
}
